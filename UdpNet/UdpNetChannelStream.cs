// Author: Martin Wetzko
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.IO;

namespace MWetzko
{
	public unsafe class UdpNetChannelStream : Stream
	{
		internal UdpNetChannelStream(UdpNetChannel channel)
		{
			this.Channel = channel;
			this.ReadTimeout = 15000;
			this.WriteTimeout = 15000;
		}

		public UdpNetChannel Channel { get; private set; }
		public Guid RemoteSocketId => Channel.Remote.SocketId;
		public ushort LocalPort => Channel.LocalPort;
		public ushort RemotePort => Channel.RemotePort;
		public int PreferredBufferSize => Channel.Remote.DataSize;

		public override bool CanTimeout => true;

		public override int ReadTimeout { get; set; }

		public override int WriteTimeout { get; set; }

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush()
		{
			// nothing to do here
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return this.Channel.Read(buffer, offset, count, this.ReadTimeout);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			// todo: proper timeout handling

			while (count > 0)
			{
				var num = this.Channel.WriteStreamFrameWithAck(buffer, offset, count, this.WriteTimeout);

				offset += num;
				count -= num;
			}
		}

		public void Disconnect()
		{
			this.Channel.Disconnect();
		}
	}
}
