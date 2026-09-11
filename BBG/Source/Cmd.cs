using System;
using OpenTK.Graphics.OpenGL;

namespace BBOpenGL;

public static partial class BBG
{
    public static unsafe class Cmd
    {
        // Application specific
        public const int SET_UNIFORMS_UBO_BLOCK_BINDING = 0;

        [Flags]
        public enum MemoryBarrierMask : uint
        {
            TextureFetchBarrierBit = OpenTK.Graphics.OpenGL.MemoryBarrierMask.TextureFetchBarrierBit,
            CommandBarrierBit = OpenTK.Graphics.OpenGL.MemoryBarrierMask.CommandBarrierBit,
            ShaderImageAccessBarrierBit = OpenTK.Graphics.OpenGL.MemoryBarrierMask.ShaderImageAccessBarrierBit,
            ShaderStorageBarrierBit = OpenTK.Graphics.OpenGL.MemoryBarrierMask.ShaderStorageBarrierBit,
            All = OpenTK.Graphics.OpenGL.MemoryBarrierMask.AllBarrierBits,
        }

        public static void Finish()
        {
            GL.Finish();
        }

        public static void Flush()
        {
            GL.Flush();
        }

        public static void TextureBarrier()
        {
#if GL_HAS_TEXTURE_BARRIER
            GL.TextureBarrier();
#endif
        }

        public static void MemoryBarrier(MemoryBarrierMask memoryBarrierMask)
        {
#if GL_HAS_IMAGE_LOAD_STORE
            GL.MemoryBarrier((OpenTK.Graphics.OpenGL.MemoryBarrierMask)memoryBarrierMask);
#endif
        }

        public static void UseShaderProgram(ShaderProgram shaderProgram)
        {
            GL.UseProgram(shaderProgram.ID);
        }

        public static void BindTextureUnit(Texture texture, int unit, bool doIt = true)
        {
            if (doIt)
            {
#if GL_HAS_DSA
                GL.BindTextureUnit((uint)unit, texture.ID);
#else
                GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + unit));
                GL.BindTexture((TextureTarget)texture.TextureType, texture.ID);
#endif
            }
            else
            {
                UnbindTextureUnit(unit);
            }
        }

        public static void BindImageUnit(Texture texture, int unit, int level = 0, bool layered = false, int layer = 0)
        {
#if GL_HAS_IMAGE_LOAD_STORE
            GL.BindImageTexture((uint)unit, texture.ID, level, layered, layer, BufferAccess.ReadWrite, (InternalFormat)texture.Format);
#endif
        }

        public static void BindImageUnit(Texture texture, Texture.InternalFormat format, int unit, int level = 0, bool layered = false, int layer = 0)
        {
#if GL_HAS_IMAGE_LOAD_STORE
            GL.BindImageTexture((uint)unit, texture.ID, level, layered, layer, BufferAccess.ReadWrite, (InternalFormat)format);
#endif
        }

        public static void UnbindTextureUnit(int unit)
        {
#if GL_HAS_DSA
            GL.BindTextureUnit((uint)unit, 0);
#else
            GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + unit));
            GL.BindTexture(TextureTarget.Texture2D, 0);
#endif
        }

        public static void SetUniforms<T>(in T uniforms) where T : unmanaged
        {
            globalUniformBuffer.InvalidateData();

            globalUniformBuffer.UploadData(0, sizeof(T), uniforms);
            globalUniformBuffer.BindToBufferBackedBlock(Buffer.BufferBackedBlockTarget.Uniform, SET_UNIFORMS_UBO_BLOCK_BINDING);
        }
    }
}
