using System;
using OpenTK.Mathematics;
using BBOpenGL;
using IDKEngine.Utils;

namespace IDKEngine.Render;

class Bloom : IDisposable
{
    public record struct GpuSettings
    {
        public float Threshold = 1.5f;
        public float MaxColor = 3.8f;

        public GpuSettings()
        {
        }
    }

    private int _minusLods;
    public int MinusLods
    {
        get => _minusLods;

        set
        {
            _minusLods = value;
#if GL_HAS_COMPUTE_SHADER
            // Multiply with 2 to get size of original src texture
            SetSize(new Vector2i(Result.Width, Result.Height) * 2);
#endif
        }
    }

#if GL_HAS_COMPUTE_SHADER
    private enum Stage : int
    {
        Downsample,
        Upsample,
    }
#endif

    public GpuSettings Settings;

    public BBG.Texture Result
    {
        get
        {
#if GL_HAS_COMPUTE_SHADER
            return upsampleTexture;
#else
            return dummyTexture;
#endif
        }
    }

#if GL_HAS_COMPUTE_SHADER
    private BBG.Texture downscaleTexture;
    private BBG.Texture upsampleTexture;
    private readonly BBG.AbstractShaderProgram shaderProgram;
#else
    private BBG.Texture dummyTexture;
#endif
    public Bloom(Vector2i size, in GpuSettings settings, int minusLods = 3)
    {
#if GL_HAS_COMPUTE_SHADER
        shaderProgram = new BBG.AbstractShaderProgram(BBG.AbstractShader.FromFile(BBG.ShaderStage.Compute, "Bloom/compute.glsl"));

        SetSize(size);

        MinusLods = minusLods;
        Settings = settings;
#else
        BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn, "Bloom needs compute shaders (GL_HAS_COMPUTE_SHADER); disabled in this build");
        dummyTexture = new BBG.Texture(BBG.Texture.Type.Texture2D);
        dummyTexture.Allocate(1, 1, 1, BBG.Texture.InternalFormat.R16G16B16A16Float);
        Settings = settings;
#endif
    }

    public void Compute(BBG.Texture src)
    {
#if GL_HAS_COMPUTE_SHADER
        BBG.Cmd.SetUniforms(Settings);

        BBG.Cmd.UseShaderProgram(shaderProgram);
        int currentWriteLod = 0;

        // Downsampling
        {
            BBG.Cmd.BindTextureUnit(src, 0);
            BBG.Cmd.BindTextureUnit(src, 0);
            shaderProgram.Upload(1, (int)Stage.Downsample);

            BBG.Computing.Compute("Copy downsample and filter into Bloom texture level 0", () =>
            {
                shaderProgram.Upload(0, currentWriteLod);
                BBG.Cmd.BindImageUnit(downscaleTexture, 0);
            
                Vector3i mipLevelSize = BBG.Texture.GetMipmapLevelSize(downscaleTexture.Width, downscaleTexture.Height, 1, currentWriteLod);
                BBG.Computing.Dispatch(MyMath.DivUp(mipLevelSize.X, 8), MyMath.DivUp(mipLevelSize.Y, 8), 1);
                BBG.Cmd.MemoryBarrier(BBG.Cmd.MemoryBarrierMask.TextureFetchBarrierBit);
                currentWriteLod++;
            });

            BBG.Cmd.BindTextureUnit(downscaleTexture, 0);
            for (; currentWriteLod < downscaleTexture.Levels; currentWriteLod++)
            {
                BBG.Computing.Compute($"Downsample Bloom texture to level {currentWriteLod}", () =>
                {
                    shaderProgram.Upload(0, currentWriteLod - 1);
                    BBG.Cmd.BindImageUnit(downscaleTexture, 0, currentWriteLod);

                    Vector3i mipLevelSize = BBG.Texture.GetMipmapLevelSize(downscaleTexture.Width, downscaleTexture.Height, 1, currentWriteLod);
                    BBG.Computing.Dispatch(MyMath.DivUp(mipLevelSize.X, 8), MyMath.DivUp(mipLevelSize.Y, 8), 1);
                    BBG.Cmd.MemoryBarrier(BBG.Cmd.MemoryBarrierMask.TextureFetchBarrierBit);
                });
            }
        }

        // Upsampling
        {
            currentWriteLod = downscaleTexture.Levels - 2;
            BBG.Cmd.BindTextureUnit(downscaleTexture, 1);
            shaderProgram.Upload(1, (int)Stage.Upsample);

            BBG.Computing.Compute($"Upsample Bloom texture to level {currentWriteLod}", () =>
            {
                shaderProgram.Upload(0, currentWriteLod + 1);
                BBG.Cmd.BindImageUnit(upsampleTexture, 0, currentWriteLod);

                Vector3i mipLevelSize = BBG.Texture.GetMipmapLevelSize(upsampleTexture.Width, upsampleTexture.Height, 1, currentWriteLod);
                BBG.Computing.Dispatch(MyMath.DivUp(mipLevelSize.X, 8), MyMath.DivUp(mipLevelSize.Y, 8), 1);
                BBG.Cmd.MemoryBarrier(BBG.Cmd.MemoryBarrierMask.TextureFetchBarrierBit);

                currentWriteLod--;
            });

            BBG.Cmd.BindTextureUnit(upsampleTexture, 1);
            for (; currentWriteLod >= 0; currentWriteLod--)
            {
                BBG.Computing.Compute($"Upsample Bloom texture to level {currentWriteLod}", () =>
                {
                    shaderProgram.Upload(0, currentWriteLod + 1);
                    BBG.Cmd.BindImageUnit(upsampleTexture, 0, currentWriteLod);

                    Vector3i mipLevelSize = BBG.Texture.GetMipmapLevelSize(upsampleTexture.Width, upsampleTexture.Height, 1, currentWriteLod);
                    BBG.Computing.Dispatch(MyMath.DivUp(mipLevelSize.X, 8), MyMath.DivUp(mipLevelSize.Y, 8), 1);
                    BBG.Cmd.MemoryBarrier(BBG.Cmd.MemoryBarrierMask.TextureFetchBarrierBit);
                });
            }
        }
#endif
    }

    public void SetSize(Vector2i size)
    {
#if GL_HAS_COMPUTE_SHADER
        size.X = (int)float.Ceiling(size.X / 2.0f);
        size.Y = (int)float.Ceiling(size.Y / 2.0f);

        int levels = int.Max(BBG.Texture.GetMaxMipmapLevel(size.X, size.Y, 1) - MinusLods, 2);

        if (downscaleTexture != null) downscaleTexture.Dispose();
        downscaleTexture = new BBG.Texture(BBG.Texture.Type.Texture2D);
        downscaleTexture.SetFilter(BBG.Sampler.MinFilter.LinearMipmapNearest, BBG.Sampler.MagFilter.Linear);
        downscaleTexture.SetWrapMode(BBG.Sampler.WrapMode.ClampToEdge, BBG.Sampler.WrapMode.ClampToEdge);
        downscaleTexture.Allocate(size.X, size.Y, 1, BBG.Texture.InternalFormat.R16G16B16A16Float, levels);

        if (upsampleTexture != null) upsampleTexture.Dispose();
        upsampleTexture = new BBG.Texture(BBG.Texture.Type.Texture2D);
        upsampleTexture.SetFilter(BBG.Sampler.MinFilter.LinearMipmapNearest, BBG.Sampler.MagFilter.Linear);
        upsampleTexture.SetWrapMode(BBG.Sampler.WrapMode.ClampToEdge, BBG.Sampler.WrapMode.ClampToEdge);
        upsampleTexture.Allocate(size.X, size.Y, 1, BBG.Texture.InternalFormat.R16G16B16A16Float, levels - 1);
#endif
    }

    public void Dispose()
    {
#if GL_HAS_COMPUTE_SHADER
        downscaleTexture.Dispose();
        upsampleTexture.Dispose();
        shaderProgram.Dispose();
#else
        dummyTexture.Dispose();
#endif
    }
}
