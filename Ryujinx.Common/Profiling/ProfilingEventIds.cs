using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Common.Profiling
{
    public static class ProfilingEventIds
    {
        public const ushort PresentFrame = 1000;
        public const ushort PresentFrameAnomaly = 1001;

        public const ushort CompileGraphicsShader = 1100;
        public const ushort CompileComputeShader = 1101;

        public const ushort MapViewOfFile3 = 2000;
        public const ushort AllocInternal = 2001;
        public const ushort AllocInternal2 = 2002;
        public const ushort CreateSharedMemory = 2003;
    }
}
