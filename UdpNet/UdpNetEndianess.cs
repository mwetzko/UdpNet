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

namespace MWetzko
{
	static class UdpNetEndianess
	{
		public static short NetworkByteOrder(short arg)
		{
			return BitConverter.IsLittleEndian ? IPAddress.HostToNetworkOrder(arg) : arg;
		}

		public static ushort NetworkByteOrder(ushort arg)
		{
			return BitConverter.IsLittleEndian ? (ushort)IPAddress.HostToNetworkOrder((short)arg) : arg;
		}

		public static int NetworkByteOrder(int arg)
		{
			return BitConverter.IsLittleEndian ? IPAddress.HostToNetworkOrder(arg) : arg;
		}

		public static uint NetworkByteOrder(uint arg)
		{
			return BitConverter.IsLittleEndian ? (uint)IPAddress.HostToNetworkOrder((int)arg) : arg;
		}

		public static long NetworkByteOrder(long arg)
		{
			return BitConverter.IsLittleEndian ? IPAddress.HostToNetworkOrder(arg) : arg;
		}

		public static ulong NetworkByteOrder(ulong arg)
		{
			return BitConverter.IsLittleEndian ? (ulong)IPAddress.HostToNetworkOrder((long)arg) : arg;
		}
	}
}
