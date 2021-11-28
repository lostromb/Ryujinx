using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Common
{
    public class PooledSpan
    {
        public byte[] PooledArray;
        public int Index;
        public int Length;

        public PooledSpan(int index, int length, byte[] array)
        {
            Index = index;
            Length = length;
            PooledArray = array;
        }

        public ReadOnlySpan<byte> AsSpan()
        {
            return new ReadOnlySpan<byte>(PooledArray, Index, Length);
        }
    }
}
