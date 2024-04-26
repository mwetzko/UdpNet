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
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace MWetzko
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetInt16
	{
		public short BigEndianValue;

		public static implicit operator short(UdpNetInt16 arg)
		{
			return UdpNetEndianess.NetworkByteOrder(arg.BigEndianValue);
		}

		public static implicit operator UdpNetInt16(short arg)
		{
			return new UdpNetInt16() { BigEndianValue = UdpNetEndianess.NetworkByteOrder(arg) };
		}

		public override string ToString()
		{
			return ((short)this).ToString();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetUInt16
	{
		public ushort BigEndianValue;

		public static implicit operator ushort(UdpNetUInt16 arg)
		{
			return UdpNetEndianess.NetworkByteOrder(arg.BigEndianValue);
		}

		public static implicit operator UdpNetUInt16(ushort arg)
		{
			return new UdpNetUInt16() { BigEndianValue = UdpNetEndianess.NetworkByteOrder(arg) };
		}

		public override string ToString()
		{
			return ((ushort)this).ToString();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetInt32
	{
		public int BigEndianValue;

		public static implicit operator int(UdpNetInt32 arg)
		{
			return UdpNetEndianess.NetworkByteOrder(arg.BigEndianValue);
		}

		public static implicit operator UdpNetInt32(int arg)
		{
			return new UdpNetInt32() { BigEndianValue = UdpNetEndianess.NetworkByteOrder(arg) };
		}

		public override string ToString()
		{
			return ((int)this).ToString();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetUInt32
	{
		public uint BigEndianValue;

		public static implicit operator uint(UdpNetUInt32 arg)
		{
			return UdpNetEndianess.NetworkByteOrder(arg.BigEndianValue);
		}

		public static implicit operator UdpNetUInt32(uint arg)
		{
			return new UdpNetUInt32() { BigEndianValue = UdpNetEndianess.NetworkByteOrder(arg) };
		}

		public override string ToString()
		{
			return ((uint)this).ToString();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetInt64
	{
		public long BigEndianValue;

		public static implicit operator long(UdpNetInt64 arg)
		{
			return UdpNetEndianess.NetworkByteOrder(arg.BigEndianValue);
		}

		public static implicit operator UdpNetInt64(long arg)
		{
			return new UdpNetInt64() { BigEndianValue = UdpNetEndianess.NetworkByteOrder(arg) };
		}

		public override string ToString()
		{
			return ((long)this).ToString();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetUInt64
	{
		public ulong BigEndianValue;

		public static implicit operator ulong(UdpNetUInt64 arg)
		{
			return UdpNetEndianess.NetworkByteOrder(arg.BigEndianValue);
		}

		public static implicit operator UdpNetUInt64(ulong arg)
		{
			return new UdpNetUInt64() { BigEndianValue = UdpNetEndianess.NetworkByteOrder(arg) };
		}

		public override string ToString()
		{
			return ((ulong)this).ToString();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct UdpNetGuid
	{
		public fixed byte Data[16];

		public static implicit operator Guid(UdpNetGuid arg)
		{
			byte[] data = new byte[sizeof(Guid)];

			Marshal.Copy((IntPtr)arg.Data, data, 0, sizeof(UdpNetGuid));

			return new Guid(data);
		}

		public static implicit operator UdpNetGuid(Guid arg)
		{
			UdpNetGuid udpNetGuid = new UdpNetGuid();

			Marshal.Copy(arg.ToByteArray(), 0, (IntPtr)udpNetGuid.Data, sizeof(UdpNetGuid));

			return udpNetGuid;
		}

		public override string ToString()
		{
			return ((Guid)this).ToString();
		}
	}

	enum UdpNetFlags : uint
	{
		None = 0x0,
		Ack = 0x1,
		CreateChannel = 0x2,
		ChannelAsStream = 0x4,
		Disconnect = 0x8,
		IsClientCall = 0x10000,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetHdr
	{
		public UdpNetUInt32 Magic;
		public UdpNetGuid SocketId;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct UdpNetSecureHdr
	{
		public UdpNetUInt32 Flags;
		public UdpNetUInt16 SourcePort;
		public UdpNetUInt16 DestinationPort;
		public UdpNetUInt32 Order;
	}

	class UdpNetBufferSocketPair
	{
		public byte[] Buffer;
		public Socket Socket;
		public EndPoint EndPoint;
	}
}
