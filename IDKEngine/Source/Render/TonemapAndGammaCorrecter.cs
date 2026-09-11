using System;
using OpenTK.Mathematics;
using BBOpenGL;
using IDKEngine.Utils;

namespace IDKEngine.Render;

class TonemapAndGammaCorrect : IDisposable
{
    public record struct GpuSettings
    {
        public float Exposure = 0.45f;
        public float Saturation = 1.06f;
        public float Linear = 0.18f;
        public float Peak = 1.0f;
        public float Compression = 0.1f;
        public bool DoTonemapAndSrgbTransform = true;

        public GpuSettings()
        {
        }
    }

    public GpuSettings Settings;

    public BBG.Texture Result;
#if GL_HAS_COMPUTE_SHADER
    private readonly BBG.AbstractShaderProgram tonemapAndGammaCorrecterProgram;
#endif
    public TonemapAndGammaCorrect(Vector2i size, in GpuSettings settings)
    {
#if GL_HAS_COMPUTE_SHADER
        tonemapAndGammaCorrecterProgram = new BBG.AbstractShaderProgram(BBG.AbstractShader.FromFile(BBG.ShaderStage.Compute, "TonemapAndGammaCorrect/compute.glsl"));

        SetSize(size);

        Settings = settings;
#else
        BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn, "TonemapAndGammaCorrect needs compute shaders (GL_HAS_COMPUTE_SHADER); disabled in this build");
        Result = new BBG.Texture(BBG.Texture.Type.Texture2D);
        Result.Allocate(1, 1, 1, BBG.Texture.InternalFormat.R8G8B8A8Unorm);
        Settings = settings;
#endif
    }

    public void Compute(BBG.Texture texture0 = null, BBG.Texture texture1 = null, BBG.Texture texture2 = null)
    {
#if GL_HAS_COMPUTE_SHADER
        BBG.Computing.Compute("Merge Textures and do Tonemapping + Gamma Correction", () =>
        {
            BBG.Cmd.SetUniforms(Settings);

            BBG.Cmd.BindImageUnit(Result, 0);
            BBG.Cmd.BindTextureUnit(texture0, 0, texture0 != null);
            BBG.Cmd.BindTextureUnit(texture1, 1, texture1 != null);
            BBG.Cmd.BindTextureUnit(texture2, 2, texture2 != null);
            BBG.Cmd.UseShaderProgram(tonemapAndGammaCorrecterProgram);

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
        Result.Allocate(size.X, size.Y, 1, BBG.Texture.InternalFormat.R8G8B8A8Unorm);
#endif
    }
    
    public void Dispose()
    {
#if GL_HAS_COMPUTE_SHADER
        Result.Dispose();
        tonemapAndGammaCorrecterProgram.Dispose();
#else
        Result.Dispose();
#endif
    }
}
