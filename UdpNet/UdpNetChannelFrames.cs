// Author: Martin Wetzko
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;

namespace MWetzko
{
	public class UdpNetChannelFrames
	{
		internal UdpNetChannelFrames(UdpNetChannel channel)
		{
			this.Channel = channel;
		}

		public UdpNetChannel Channel { get; private set; }
		public Guid RemoteSocketId => Channel.Remote.SocketId;
		public ushort LocalPort => Channel.LocalPort;
		public ushort RemotePort => Channel.RemotePort;
		public int PreferredBufferSize => Channel.Remote.DataSize;

		public Action<ArraySegment<byte>, uint> OnReceiveFrame { get => this.Channel.OnReceiveFrame; set => this.Channel.OnReceiveFrame = value; }

		public void WriteFrame(byte[] buffer)
		{
			this.Channel.WriteFrame(buffer, 0, buffer.Length);
		}

		public void WriteFrame(byte[] buffer, int offset, int count)
		{
			this.Channel.WriteFrame(buffer, offset, count);
		}
	}
}
