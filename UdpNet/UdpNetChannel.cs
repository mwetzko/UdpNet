// Author: Martin Wetzko
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Net;
using System.Threading;

namespace MWetzko
{
	public unsafe class UdpNetChannel : IDisposable
	{
		volatile uint mNumberRead;
		ManualResetEventSlim mReadWait;
		UdpNetBuffer mReadBuffer;

		volatile uint mNumberWrite;
		ManualResetEventSlim mWriteWait;

		bool mGiveBack;

		internal UdpNetChannel(UdpNetRemote remote, IPEndPoint endPoint, ushort port, ushort remotePort, bool buffered, bool isClient)
		{
			this.Remote = remote;
			this.RemoteEndPoint = endPoint;
			this.LocalPort = port;
			this.RemotePort = remotePort;
			this.IsBuffered = buffered;

			mReadWait = new ManualResetEventSlim(false);
			mWriteWait = new ManualResetEventSlim(false);

			if (this.IsBuffered)
			{
				mReadBuffer = new UdpNetBuffer(45);
			}

			mGiveBack = isClient;
			this.Flags = isClient ? UdpNetFlags.IsClientCall : UdpNetFlags.None;
		}

		bool mDisposed;

		~UdpNetChannel()
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
				if (mGiveBack)
				{
					this.Remote.GiveChannelBack(this);
				}

				if (disposing)
				{
					this.Remote = null;

					if (mReadWait != null)
					{
						mReadWait.Dispose();
						mReadWait = null;
					}

					if (mWriteWait != null)
					{
						mWriteWait.Dispose();
						mWriteWait = null;
					}

					if (mReadBuffer != null)
					{
						mReadBuffer.Dispose();
						mReadBuffer = null;
					}
				}

				mDisposed = true;
			}
		}

		public UdpNetRemote Remote { get; private set; }
		public IPEndPoint RemoteEndPoint { get; private set; }
		public ushort LocalPort { get; private set; }
		public ushort RemotePort { get; private set; }
		public bool IsBuffered { get; private set; }
		internal UdpNetFlags Flags { get; }
		internal bool IsDisconnected { get; set; }

		internal bool AddBuffer(ArraySegment<byte> data, uint order)
		{
			lock (mReadBuffer)
			{
				if (mNumberRead == order)
				{
					if (mReadBuffer.Add(data))
					{
						mNumberRead++;

						mReadWait.Set();

						return true;
					}
				}
				else if (mNumberRead == (order + 1))
				{
					// send ack for last package
					return true;
				}
			}

			return false;
		}

		internal void NotifyWriteAck()
		{
			mWriteWait.Set();
		}

		internal void NotifyWriteAck(uint order)
		{
			if (mNumberWrite == order)
			{
				mWriteWait.Set();
			}
		}

		internal Action<ArraySegment<byte>, uint> OnReceiveFrame;

		internal int Read(byte[] buffer, int offset, int count, int timeout)
		{
			EnsureDisconnected();

			do
			{
				if (IsDisconnected)
				{
					return 0;
				}

				int total = 0;

				while (true)
				{
					lock (mReadBuffer)
					{
						int num = mReadBuffer.Read(buffer, offset + total, count - total);

						if (num > 0)
						{
							total += num;
							continue;
						}

						if (total > 0)
						{
							return total;
						}

						mReadWait.Reset();

						break;
					}
				}
			}
			while (mReadWait.Wait(timeout));

			throw new TimeoutException("Read operation timed out");
		}

		internal void WriteFrame(byte[] buffer, int offset, int count)
		{
			EnsureDisconnected();

			this.Remote.Socket.WriteFrame(this, buffer, offset, count, UdpNetFlags.None, mNumberWrite);

			mNumberWrite++;
		}

		internal int WriteStreamFrameWithAck(byte[] buffer, int offset, int count, int timeout)
		{
			EnsureDisconnected();

			int num = this.Remote.Socket.WriteFrame(this, buffer, offset, count, UdpNetFlags.None, mNumberWrite, mWriteWait);

			mNumberWrite++;

			return num;
		}

		internal UdpNetChannelFrames CreateFrames()
		{
			this.Remote.Socket.WriteFrame(this, null, 0, 0, UdpNetFlags.CreateChannel, 0, mWriteWait);

			return new UdpNetChannelFrames(this);
		}

		internal UdpNetChannelStream CreateStream()
		{
			this.Remote.Socket.WriteFrame(this, null, 0, 0, UdpNetFlags.CreateChannel | UdpNetFlags.ChannelAsStream, 0, mWriteWait);

			return new UdpNetChannelStream(this);
		}

		internal void Disconnect()
		{
			this.Remote.Socket.WriteFrame(this, null, 0, 0, UdpNetFlags.Disconnect, 0, mWriteWait);
		}

		internal void OnDisconnect()
		{
			var wait = mReadWait;

			mReadWait = null;

			wait?.Set();
			wait?.Dispose();
		}

		internal void EnsureDisconnected()
		{
			if (IsDisconnected)
			{
				throw new Exception("Channel has been disconnected");
			}
		}
	}
}
