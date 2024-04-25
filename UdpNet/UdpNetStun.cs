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
using System.Runtime.InteropServices;
using System.Threading;

namespace MWetzko
{
	internal unsafe static class UdpNetStun
	{
		// rfc8489
		internal static IPEndPoint GetPublicAddress(UdpNetSocket socket, IPEndPoint remoteEndPoint)
		{
			ManualResetEventSlim ack = new ManualResetEventSlim(false);

			Guid transaction = Guid.Empty;

			ArraySegment<byte> current = ArraySegment<byte>.Empty;

			socket.OnNonMagicData = (x, y) =>
			{
				if (y.Equals(remoteEndPoint))
				{
					fixed (byte* b = x.Array)
					{
						Header* hdr = (Header*)b;

						if (hdr->TransactionID == transaction)
						{
							current = x;

							ack.Set();
						}
					}
				}
			};

			try
			{
				var b = new Builder();

				transaction = Guid.NewGuid();

				b.SetHeader(transaction, MethodsRegistry.Binding);

				var seg = b.Create();

				socket.Send(seg.Array, seg.Offset, seg.Count, remoteEndPoint, ack);

				return Parser.Parse(current).MappedAddress;
			}
			finally
			{
				socket.OnNonMagicData = null;
			}
		}

		class Builder
		{
			byte[] mData = new byte[1024];

			public Builder SetHeader(Guid transaction, MethodsRegistry method)
			{
				fixed (byte* b = mData)
				{
					Header* attr = (Header*)b;

					attr->Type = (ushort)method;
					attr->TransactionID = transaction;
				}

				return this;
			}

			public ArraySegment<byte> Create()
			{
				return new ArraySegment<byte>(mData, 0, sizeof(Header));
			}
		}

		class Parser
		{
			private Parser()
			{
				// nothing here
			}

			public MethodsRegistry Type { get; private set; }

			public IPEndPoint MappedAddress { get; private set; }

			public static Parser Parse(ArraySegment<byte> data)
			{
				Parser parser = new Parser();

				fixed (byte* b = &data.Array[data.Offset])
				{
					Header* hdr = (Header*)b;

					parser.Type = (MethodsRegistry)(ushort)hdr->Type;

					int num = hdr->Length;

					int pos = 0;

					while (pos < num)
					{
						AttributeHeader* attr = (AttributeHeader*)&b[sizeof(Header) + pos];

						if ((AttributesRegistry)(ushort)attr->Type == AttributesRegistry.MappedAddress)
						{
							AddressAttribute* address = (AddressAttribute*)&b[sizeof(Header) + pos + sizeof(AttributeHeader)];

							parser.MappedAddress = new IPEndPoint(new IPAddress(new ReadOnlySpan<byte>(&address->FirstByteOfAddress, address->Family == 0x01 ? 4 : 16)), address->Port);
						}

						pos += sizeof(AttributeHeader) + attr->Length;
					}
				}

				return parser;
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct Header
		{
			public UdpNetUInt16 Type;
			public UdpNetUInt16 Length;
			public UdpNetGuid TransactionID;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct AttributeHeader
		{
			public UdpNetUInt16 Type;
			public UdpNetUInt16 Length;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct AddressAttribute
		{
			byte Unused;
			public byte Family;
			public UdpNetUInt16 Port;
			public byte FirstByteOfAddress;
		}

		// rfc8489
		enum MethodsRegistry : ushort
		{
			// Reserved = 0x000
			Binding = 0x001
			// Reserved = 0x002; was SharedSecret prior to [RFC5389]
		}

		// rfc8489
		enum AttributesRegistry : ushort
		{
			// Reserved = 0x0000
			MappedAddress = 0x0001,
			// Reserved = 0x0002; was RESPONSE-ADDRESS prior to [RFC5389]
			// Reserved = 0x0003; was CHANGE-REQUEST prior to [RFC5389]
			// Reserved = 0x0004; was SOURCE-ADDRESS prior to [RFC5389]
			// Reserved = 0x0005; was CHANGED-ADDRESS prior to [RFC5389]
			Username = 0x0006,
			// Reserved = 0x0007; was PASSWORD prior to [RFC5389]
			MessageIntegrity = 0x0008,
			ErrorCode = 0x0009,
			UnknownAttributes = 0x000A,
			// Reserved = 0x000B; was REFLECTED-FROM prior to [RFC5389]
			Realm = 0x0014,
			Nonce = 0x0015,
			XorMappedAddress = 0x0020,

			Software = 0x8022,
			AlternateServer = 0x8023,
			Fingerprint = 0x8028,

			// IANA
			MessageIntegritySha256 = 0x001C,
			PasswordAlgorithm = 0x001D,
			Userhash = 0x001E,

			PasswordAlgorithms = 0x8002,
			AlternateDomain = 0x8003
		}

		// rfc8489
		enum PasswordAlgorithmsRegistry : ushort
		{
			// Reserved = 0x0000
			MD5 = 0x0001,
			SHA256 = 0x0002
		}
	}
}
