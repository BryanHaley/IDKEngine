using System;
using OpenTK.Graphics.OpenGL;

namespace BBOpenGL;

public static partial class BBG
{
    public class TextureView : IDisposable
    {
        public readonly int ID;
        private readonly bool isBorrowed;

        public TextureView(Texture texture, in Sampler.SamplerState samplerState, int level = 0)
        {
#if GL_HAS_TEXTURE_VIEW && GL_HAS_DSA
            GL.GenTextures(1, ref ID);
            GL.TextureView(ID, TextureTarget.Texture2D, texture.ID, (SizedInternalFormat)texture.Format, (uint)level, 1, 0, 1);

            GL.TextureParameteri(ID, TextureParameterName.TextureMinFilter, (int)samplerState.MinFilter);
            GL.TextureParameteri(ID, TextureParameterName.TextureMagFilter, (int)samplerState.MagFilter);
            
            GL.TextureParameteri(ID, TextureParameterName.TextureWrapS, (int)samplerState.WrapModeS);
            GL.TextureParameteri(ID, TextureParameterName.TextureWrapT, (int)samplerState.WrapModeT);
            GL.TextureParameteri(ID, TextureParameterName.TextureWrapR, (int)samplerState.WrapModeR);
            
            GL.TextureParameterf(ID, TextureParameterName.TextureMaxAnisotropy, (float)samplerState.Anisotropy);

            GL.TextureParameteri(ID, TextureParameterName.TextureCompareMode, (int)samplerState.CompareMode);
            GL.TextureParameteri(ID, TextureParameterName.TextureCompareFunc, (int)samplerState.CompareFunc);
#else
            // glTextureView is 4.3. There is no way to alias one texture's storage
            // under another format below that, so the view is simply the original.
            ID = texture.ID;
            isBorrowed = true;
#endif
        }

        public void Dispose()
        {
            // borrowed means ID belongs to the texture we were made from
            if (!isBorrowed)
            {
                GL.DeleteTexture(ID);
            }
        }
    }
}
