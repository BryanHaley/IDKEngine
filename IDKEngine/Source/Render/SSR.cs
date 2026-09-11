using System;
using OpenTK.Mathematics;
using BBOpenGL;
using IDKEngine.Utils;

namespace IDKEngine.Render;

class SSR : IDisposable
{
    public record struct GpuSettings
    {
        public int SampleCount = 30;
        public int BinarySearchCount = 8;
        public float MaxDist = 50.0f;

        public GpuSettings()
        {
        }
    }

    public GpuSettings Settings;


    public BBG.Texture Result;
#if GL_HAS_COMPUTE_SHADER
    private readonly BBG.AbstractShaderProgram shaderProgram;
#endif
    public SSR(Vector2i size, in GpuSettings settings)
    {
#if GL_HAS_COMPUTE_SHADER
        shaderProgram = new BBG.AbstractShaderProgram(BBG.AbstractShader.FromFile(BBG.ShaderStage.Compute, "SSR/compute.glsl"));

        SetSize(size);

        Settings = settings;
#else
        BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn, "SSR needs compute shaders (GL_HAS_COMPUTE_SHADER); disabled in this build");
        Result = new BBG.Texture(BBG.Texture.Type.Texture2D);
        Result.Allocate(1, 1, 1, BBG.Texture.InternalFormat.R16G16B16A16Float);
        Settings = settings;
#endif
    }

    public void Compute(BBG.Texture colorTexture)
    {
#if GL_HAS_COMPUTE_SHADER
        BBG.Computing.Compute("Compute SSR", () =>
        {
            BBG.Cmd.SetUniforms(Settings);

            BBG.Cmd.BindImageUnit(Result, 0);
            BBG.Cmd.BindTextureUnit(colorTexture, 0);
            BBG.Cmd.UseShaderProgram(shaderProgram);
            
            BBG.Computing.Dispatch(MyMath.DivUp(Result.Width, 8), MyMath.DivUp(Result.Height, 8), 1);
            BBG.Cmd.MemoryBarrier(BBG.Cmd.MemoryBarrierMask.TextureFetchBarrierBit);
        });
#endif
    }

    public void SetSize(Vector2i size)
    {
#if GL_HAS_COMPUTE_SHADER
        if (Result != null) Result.Dispose();
        Result = new BBG.Texture(BBG.Texture.Type.Texture2D);
        Result.SetFilter(BBG.Sampler.MinFilter.Linear, BBG.Sampler.MagFilter.Linear);
        Result.SetWrapMode(BBG.Sampler.WrapMode.ClampToEdge, BBG.Sampler.WrapMode.ClampToEdge);
        Result.Allocate(size.X, size.Y, 1, BBG.Texture.InternalFormat.R16G16B16A16Float);
#endif
    }

    public void Dispose()
    {
#if GL_HAS_COMPUTE_SHADER
        Result.Dispose();
        shaderProgram.Dispose();
#else
        Result.Dispose();
#endif
    }
}
