using System;
using OpenTK.Graphics.OpenGL;

namespace BBOpenGL;

public static partial class BBG
{
    public static class Computing
    {
        public static void Compute(string computePassName, Action funcCompute)
        {
            Debugging.PushDebugGroup(computePassName);

            funcCompute();

            Debugging.PopDebugGroup();
        }

        public static void Dispatch(int x, int y, int z)
        {
            if (x == 0 || y == 0 || z == 0) return;
#if GL_HAS_COMPUTE_SHADER
            GL.DispatchCompute((uint)x, (uint)y, (uint)z);
#else
            WarnOnce();
#endif
        }

        public static void DispatchIndirect(Buffer commandBuffer, nint offset = 0)
        {
#if GL_HAS_COMPUTE_SHADER
            GL.BindBuffer(BufferTarget.DispatchIndirectBuffer, commandBuffer.ID);
            GL.DispatchComputeIndirect(offset);
#else
            WarnOnce();
#endif
        }

#if !GL_HAS_COMPUTE_SHADER
        private static bool warned;

        private static void WarnOnce()
        {
            if (warned) return;
            warned = true;
            BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn,
                "Compute shaders need GL 4.3; every dispatch in this build does nothing");
        }
#endif
    }
}
