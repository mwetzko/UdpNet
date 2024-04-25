// Author: Martin Wetzko
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Security.Cryptography;

namespace MWetzko
{
	static class UdpNetSecurity
	{
		static readonly byte[] SALT = { 3, 9, 124, 214, 2, 239, 71, 76, 137, 46, 71, 77, 92, 162, 70, 144 };

		public static int TransformFinalBlockLocked(ICryptoTransform transform, ArraySegment<byte> data)
		{
			return TransformFinalBlockLocked(transform, data.Array, data.Offset, data.Count);
		}

		public static int TransformFinalBlockLocked(ICryptoTransform transform, byte[] data, int offset, int count)
		{
			lock (transform)
			{
				var aes = transform.TransformFinalBlock(data, offset, count);

				Array.Copy(aes, 0, data, offset, aes.Length);

				return aes.Length;
			}
		}

		public static void PasswordToKeyIV(string password, int keysize, int blocksize, out byte[] key, out byte[] iv)
		{
			PasswordToKeyIV(password, SALT, 1000, keysize, blocksize, out key, out iv);
		}

		public static void PasswordToKeyIV(string password, byte[] salt, int iterations, int keysize, int blocksize, out byte[] key, out byte[] iv)
		{
			using (Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(password, salt, iterations))
			{
				key = rfc2898.GetBytes(keysize / 8);
				iv = rfc2898.GetBytes(blocksize / 8);
			}
		}
	}
}
