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
	partial class UdpNetBuffer : IDisposable
	{
		BufferItem[] mItems;
		int mPosition;
		int mCount;

		public UdpNetBuffer(int num)
		{
			mItems = new BufferItem[num];
		}

		bool mDisposed;

		~UdpNetBuffer()
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
					if (mItems != null)
					{
						Array.Clear(mItems, 0, mItems.Length);
						mItems = null;
					}
				}

				mDisposed = true;
			}
		}

		public bool Add(ArraySegment<byte> data)
		{
			if (mCount < mItems.Length)
			{
				mItems[(mPosition + mCount) % mItems.Length] = new BufferItem() { Data = data.Array, Offset = data.Offset, Count = data.Count };

				mCount++;

				return true;
			}

			return false;
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			if (mCount > 0)
			{
				var state = mItems[mPosition];

				if (state.Count > count)
				{
					Array.Copy(state.Data, state.Offset, buffer, offset, count);
					state.Offset += count;
					state.Count -= count;
					return count;
				}
				else
				{
					Array.Copy(state.Data, state.Offset, buffer, offset, state.Count);

					if (!(++mPosition < mItems.Length))
					{
						mPosition = 0;
					}

					mCount--;

					return state.Count;
				}
			}

			return 0;
		}

		class BufferItem
		{
			public byte[] Data;
			public int Offset;
			public int Count;
		}
	}
}
