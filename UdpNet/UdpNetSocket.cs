// Author: Martin Wetzko
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MWetzko
{
	public unsafe partial class UdpNetSocket : IDisposable
	{
		internal const int MTU = 1400;

		Socket mSocket;

		[ThreadStatic]
		static byte[] MtuBuffer;

		static void EnsureBuffer()
		{
			if (MtuBuffer == null)
			{
				MtuBuffer = new byte[MTU];
			}
		}

		public UdpNetSocket(uint magic, Guid socketId)
		{
			this.Magic = magic;
			this.SocketId = socketId;
			this.RemoteSockets = new ConcurrentDictionary<Guid, UdpNetRemote>();
		}

		bool mDisposed;

		~UdpNetSocket()
		{
			this.Dispose(false);
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!mDisposed)
			{
				if (disposing)
				{
					this.Stop();

					if (this.RemoteSockets != null)
					{
						this.RemoteSockets.Clear();
						this.RemoteSockets = null;
					}
				}

				mDisposed = true;
			}
		}

		public uint Magic { get; private set; }
		public Guid SocketId { get; private set; }

		public IPEndPoint LocalEndPoint => (IPEndPoint)mSocket.LocalEndPoint;

		public void Start(int port)
		{
			this.Start(IPAddress.Any, port);
		}

		public void Start(IPAddress ipAddress, int port)
		{
			this.Start(new IPEndPoint(ipAddress, port));
		}

		public void Start(IPEndPoint endPoint)
		{
			mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

			try
			{
				mSocket.Bind(endPoint);
			}
			catch (Exception)
			{
				mSocket.Dispose();
				mSocket = null;
				throw;
			}

			this.BeginReceive(mSocket);
		}

		public void Stop()
		{
			if (mSocket != null)
			{
				mSocket.Dispose();
				mSocket = null;
			}
		}

		void BeginReceive(Socket socket)
		{
			// pass socket explicitly to allow stop method
			// using mSocket may result in a race situation
			BufferSocketPair pair = new BufferSocketPair();

			pair.Buffer = new byte[MTU];
			pair.Socket = socket;
			pair.EndPoint = new IPEndPoint(IPAddress.Any, 0);

			socket.BeginReceiveFrom(pair.Buffer, 0, pair.Buffer.Length, SocketFlags.None, ref pair.EndPoint, EndReceive, pair);
		}

		public Action<ArraySegment<byte>, IPEndPoint> OnNonMagicData { get; set; }

		void EndReceive(IAsyncResult ar)
		{
			var state = ar.AsyncState as BufferSocketPair;

			int num = state.Socket.EndReceiveFrom(ar, ref state.EndPoint);

			BeginReceive(state.Socket);

			fixed (byte* b = state.Buffer)
			{
				var hdr = (Hdr*)b;

				if (hdr->Magic == this.Magic)
				{
					this.HandleMagicData(new ArraySegment<byte>(state.Buffer, sizeof(Hdr), num - sizeof(Hdr)), (IPEndPoint)state.EndPoint, hdr->Magic, hdr->SocketId);
				}
				else
				{
					OnNonMagicData?.Invoke(new ArraySegment<byte>(state.Buffer, 0, num), (IPEndPoint)state.EndPoint);
				}
			}
		}

		internal ConcurrentDictionary<Guid, UdpNetRemote> RemoteSockets { get; private set; }

		protected virtual void HandleMagicData(ArraySegment<byte> data, IPEndPoint endPoint, uint magic, Guid socketId)
		{
			if (this.RemoteSockets.TryGetValue(socketId, out UdpNetRemote remote))
			{
				int num = UdpNetSecurity.TransformFinalBlockLocked(remote.Decrypter, data);

				fixed (byte* b = &data.Array[data.Offset])
				{
					var hdr = (SecureHdr*)b;

					this.HandleEncryptedChannelData(new ArraySegment<byte>(data.Array, data.Offset + sizeof(SecureHdr), num - sizeof(SecureHdr)), remote, endPoint, (UdpNetFlags)(uint)hdr->Flags, hdr->SourcePort, hdr->DestinationPort, hdr->Order);
				}
			}
		}

		public void Authorize(Guid remoteSocketId, string password)
		{
			if (this.SocketId == remoteSocketId)
			{
				throw new ArgumentException("Cannot authorize a socket that has the same id as this socket!", nameof(remoteSocketId));
			}

			this.RemoteSockets.GetOrAdd(remoteSocketId, x => new UdpNetRemote(this, remoteSocketId)).SetPassword(password);
		}

		public void Unauthorize(Guid remoteSocketId)
		{
			if (this.SocketId == remoteSocketId)
			{
				throw new ArgumentException("Cannot unauthorize a socket that has the same id as this socket!", nameof(remoteSocketId));
			}

			this.RemoteSockets.TryRemove(remoteSocketId, out _);
		}

		public UdpNetChannelStream ConnectStream(Guid remoteSocketId, IPEndPoint endPoint, ushort remotePort)
		{
			if (!this.RemoteSockets.TryGetValue(remoteSocketId, out var remote))
			{
				throw new ArgumentException("Remote socket has not been authorized!", nameof(remoteSocketId));
			}

			return remote.CreateStream(endPoint, remotePort);
		}

		public UdpNetChannelFrames ConnectFrames(Guid remoteSocketId, IPEndPoint endPoint, ushort remotePort)
		{
			if (!this.RemoteSockets.TryGetValue(remoteSocketId, out var remote))
			{
				throw new ArgumentException("Remote socket has not been authorized!", nameof(remoteSocketId));
			}

			return remote.CreateFrames(endPoint, remotePort);
		}

		public Action<UdpNetChannelFrames> OnConnectFrames { get; set; }
		public Action<UdpNetChannelStream> OnConnectStream { get; set; }

		void HandleEncryptedChannelData(ArraySegment<byte> data, UdpNetRemote remote, IPEndPoint endPoint, UdpNetFlags flags, ushort sourcePort, ushort destinationPort, uint order)
		{
			if (flags.Has(UdpNetFlags.CreateChannel))
			{
				var buffered = flags.Has(UdpNetFlags.ChannelAsStream);

				var sub = remote.Incoming.GetOrAdd(destinationPort, x => new ConcurrentDictionary<ushort, WeakReference<UdpNetChannel>>());

				var weak = sub.GetOrAdd(sourcePort, new WeakReference<UdpNetChannel>(null));

				var chan = new UdpNetChannel(remote, endPoint, destinationPort, sourcePort, buffered, false);

				weak.SetTarget(chan);

				if (chan.IsBuffered)
				{
					var invoke = this.OnConnectStream;

					if (invoke != null)
					{
						this.WriteAck(chan, endPoint, 0);

						Task.Run(() => invoke(new UdpNetChannelStream(chan)));
					}
				}
				else
				{
					var invoke = this.OnConnectFrames;

					if (invoke != null)
					{
						this.WriteAck(chan, endPoint, 0);

						Task.Run(() => invoke(new UdpNetChannelFrames(chan)));
					}
				}
			}
			else
			{
				var remotes = flags.Has(UdpNetFlags.IsClientCall) ? remote.Incoming : remote.Outgoing;

				if (remotes.TryGetValue(destinationPort, out var sub) && sub.TryGetValue(sourcePort, out var weak) && weak.TryGetTarget(out var existing))
				{
					if (flags.Has(UdpNetFlags.Ack))
					{
						if (flags.Has(UdpNetFlags.Disconnect))
						{
							existing.NotifyWriteAck();
						}
						else
						{
							existing.NotifyWriteAck(order);
						}
					}
					else if (flags.Has(UdpNetFlags.Disconnect))
					{
						existing.IsDisconnected = true;

						this.WriteAck(existing, endPoint, 0, UdpNetFlags.Disconnect);

						Task.Run(() => existing.OnDisconnect());
					}
					else
					{
						if (!existing.IsDisconnected)
						{
							if (existing.IsBuffered)
							{
								if (existing.AddBuffer(data, order))
								{
									this.WriteAck(existing, endPoint, order);
								}
							}
							else
							{
								existing?.OnReceiveFrame(data, order);
							}
						}
					}
				}
			}
		}

		int GetSecureFrame(byte[] mtu, ICryptoTransform encrypter, byte[] buffer, int offset, int count, uint magic, UdpNetFlags flags, ushort sourcePort, ushort destinationPort, uint order)
		{
			fixed (byte* b = &mtu[0])
			{
				var hdr = (Hdr*)b;

				hdr->Magic = magic;
				hdr->SocketId = this.SocketId;
			}

			fixed (byte* b = &mtu[sizeof(Hdr)])
			{
				var hdr = (SecureHdr*)b;

				hdr->Flags = (uint)flags;
				hdr->SourcePort = sourcePort;
				hdr->DestinationPort = destinationPort;
				hdr->Order = order;
			}

			if (count > 0)
			{
				Array.Copy(buffer, offset, mtu, sizeof(Hdr) + sizeof(SecureHdr), count);
			}

			return sizeof(Hdr) + UdpNetSecurity.TransformFinalBlockLocked(encrypter, mtu, sizeof(Hdr), sizeof(SecureHdr) + count);
		}

		public int Send(byte[] buffer, IPEndPoint remoteEndPoint)
		{
			return this.Send(buffer, 0, buffer.Length, remoteEndPoint);
		}

		public int Send(byte[] buffer, int offset, int count, IPEndPoint remoteEndPoint)
		{
			return mSocket.SendTo(buffer, offset, count, SocketFlags.None, remoteEndPoint);
		}

		internal int Send(byte[] buffer, int offset, int count, IPEndPoint remoteEndPoint, ManualResetEventSlim ack)
		{
			ack.Reset();

			for (int i = 0; i < 5; i++)
			{
				mSocket.SendTo(buffer, offset, count, SocketFlags.None, remoteEndPoint);

				if (ack.Wait((i * 500) + 1000))
				{
					return count;
				}
			}

			throw new TimeoutException("The send operation timed out.");
		}

		internal int WriteFrame(UdpNetChannel channel, byte[] buffer, int offset, int count, UdpNetFlags flags, uint order, ManualResetEventSlim ack = null)
		{
			return this.WriteFrame(channel.Remote.DataSize, channel.Remote.Encrypter, channel.RemoteEndPoint, buffer, offset, count, this.Magic, flags | channel.Flags, channel.LocalPort, channel.RemotePort, order, ack);
		}

		internal int WriteFrame(int datasize, ICryptoTransform encrypter, IPEndPoint endPoint, byte[] buffer, int offset, int count, uint magic, UdpNetFlags flags, ushort localPort, ushort remotePort, uint order, ManualResetEventSlim ack = null)
		{
			count = count > datasize ? datasize : count;

			EnsureBuffer();

			int num = GetSecureFrame(MtuBuffer, encrypter, buffer, offset, count, magic, flags, localPort, remotePort, order);

			if (ack == null)
			{
				this.Send(MtuBuffer, 0, num, endPoint);
			}
			else
			{
				this.Send(MtuBuffer, 0, num, endPoint, ack);
			}

			return count;
		}

		void WriteAck(UdpNetChannel channel, IPEndPoint endPoint, uint order, UdpNetFlags flags = UdpNetFlags.None)
		{
			EnsureBuffer();

			int num = GetSecureFrame(MtuBuffer, channel.Remote.Encrypter, null, 0, 0, this.Magic, UdpNetFlags.Ack | flags | channel.Flags, channel.LocalPort, channel.RemotePort, order);

			this.Send(MtuBuffer, 0, num, endPoint);
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal struct Hdr
		{
			public UdpNetUInt32 Magic;
			public UdpNetGuid SocketId;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal struct SecureHdr
		{
			public UdpNetUInt32 Flags;
			public UdpNetUInt16 SourcePort;
			public UdpNetUInt16 DestinationPort;
			public UdpNetUInt32 Order;
		}

		class BufferSocketPair
		{
			public byte[] Buffer;
			public Socket Socket;
			public EndPoint EndPoint;
		}
	}
}
