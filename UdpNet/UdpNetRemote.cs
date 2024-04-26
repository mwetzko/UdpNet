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
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;

namespace MWetzko
{
	partial class UdpNetRemote : IDisposable
	{
		bool mDisposed;
		ushort mLocalPort = ushort.MaxValue;
		Queue<ushort> mReusePorts;

		public UdpNetRemote(UdpNetSocket socket, Guid socketId)
		{
			this.Socket = socket;
			this.SocketId = socketId;
			this.Outgoing = new ConcurrentDictionary<ushort, ConcurrentDictionary<ushort, WeakReference<UdpNetChannel>>>();
			this.Incoming = new ConcurrentDictionary<ushort, ConcurrentDictionary<ushort, WeakReference<UdpNetChannel>>>();
			mReusePorts = new Queue<ushort>();
		}

		~UdpNetRemote()
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
					this.Socket = null;

					if (this.Encrypter != null)
					{
						this.Encrypter.Dispose();
						this.Encrypter = null;
					}

					if (this.Decrypter != null)
					{
						this.Decrypter.Dispose();
						this.Decrypter = null;
					}

					if (this.Outgoing != null)
					{
						this.Outgoing.Clear();
						this.Outgoing = null;
					}

					if (this.Incoming != null)
					{
						this.Incoming.Clear();
						this.Incoming = null;
					}
				}

				mDisposed = true;
			}
		}

		public UdpNetSocket Socket { get; private set; }
		public Guid SocketId { get; private set; }
		public ConcurrentDictionary<ushort, ConcurrentDictionary<ushort, WeakReference<UdpNetChannel>>> Outgoing { get; private set; }
		public ConcurrentDictionary<ushort, ConcurrentDictionary<ushort, WeakReference<UdpNetChannel>>> Incoming { get; private set; }
		internal ICryptoTransform Encrypter { get; private set; }
		internal ICryptoTransform Decrypter { get; private set; }
		internal int DataSize { get; private set; }

		public unsafe void SetPassword(string password)
		{
			lock (this)
			{
				using (var crypto = Aes.Create())
				{
					UdpNetSecurity.PasswordToKeyIV(password, crypto.KeySize, crypto.BlockSize, out byte[] key, out byte[] iv);

					crypto.Padding = PaddingMode.PKCS7;
					crypto.Key = key;
					crypto.IV = iv;

					this.SetCrypters(crypto.CreateEncryptor(), crypto.CreateDecryptor());

					this.DataSize = CalcDataSize(crypto);
				}
			}
		}

		unsafe static int CalcDataSize(Aes crypto)
		{
			int blockBytes = crypto.BlockSize / 8;

			int frm = (UdpNetSocket.MTU / blockBytes) * blockBytes;

			return frm - sizeof(UdpNetHdr) - sizeof(UdpNetSecureHdr);
		}

		void SetCrypters(ICryptoTransform encrypter, ICryptoTransform decrypter)
		{
			var enc = this.Encrypter;
			var dec = this.Decrypter;

			this.Encrypter = encrypter;
			this.Decrypter = decrypter;

			if (enc != null)
			{
				enc.Dispose();
			}

			if (dec != null)
			{
				dec.Dispose();
			}
		}

		UdpNetChannel EnsureChannel(IPEndPoint endPoint, ushort remotePort)
		{
			ushort port;

			lock (mReusePorts)
			{
				if (mReusePorts.TryDequeue(out ushort reuse))
				{
					port = reuse;
				}
				else if (mLocalPort == 0)
				{
					throw new Exception("All ports are in use!");
				}
				else
				{
					port = mLocalPort--;
				}
			}

			try
			{
				var sub = this.Outgoing.GetOrAdd(port, x => new ConcurrentDictionary<ushort, WeakReference<UdpNetChannel>>());

				var weak = sub.GetOrAdd(remotePort, new WeakReference<UdpNetChannel>(null));

				var channel = new UdpNetChannel(this, endPoint, port, remotePort, true, true);

				weak.SetTarget(channel);

				return channel;
			}
			catch (Exception)
			{
				lock (mReusePorts)
				{
					mReusePorts.Enqueue(port);
				}

				throw;
			}
		}

		internal void GiveChannelBack(UdpNetChannel channel)
		{
			lock (mReusePorts)
			{
				mReusePorts.Enqueue(channel.LocalPort);
			}
		}

		public UdpNetChannelStream CreateStream(IPEndPoint endPoint, ushort remotePort)
		{
			return this.EnsureChannel(endPoint, remotePort).CreateStream();
		}

		public UdpNetChannelFrames CreateFrames(IPEndPoint endPoint, ushort remotePort)
		{
			return this.EnsureChannel(endPoint, remotePort).CreateFrames();
		}
	}
}
