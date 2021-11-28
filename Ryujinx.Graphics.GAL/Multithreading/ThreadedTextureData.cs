using Ryujinx.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Graphics.GAL.Multithreading
{
    public struct ThreadedTextureData
    {
        public byte[] PooledData;
        public int Index;
        public int Length;

        public ThreadedTextureData(PooledSpan source)
        {
            PooledData = source.PooledArray;
            Index = source.Index;
            Length = source.Length;
        }

        public PooledSpan AsPooledSpan()
        {
            return new PooledSpan(Index, Length, PooledData);
        }
    }
}
