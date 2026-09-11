using System;
using OpenTK.Mathematics;
using BBOpenGL;
using IDKEngine.Utils;

namespace IDKEngine.Render;

class ConeTracer : IDisposable
{
    public record struct GpuSettings
    {
        public int MaxSamples = 4;
        public float StepMultiplier = 0.16f;
        public float GIBoost = 1.3f;
        public float GISkyBoxBoost = 1.0f / 1.3f;
        public float NormalRayOffset = 1.0f;
        public bool IsTemporalAccumulation = true;

        public GpuSettings()
        {
        }
    }

    public GpuSettings Settings;

    public BBG.Texture Result;
#if GL_HAS_COMPUTE_SHADER
    private readonly BBG.AbstractShaderProgram shaderProgram;
#endif
    public ConeTracer(Vector2i size, in GpuSettings settings)
    {
#if GL_HAS_COMPUTE_SHADER
        shaderProgram = new BBG.AbstractShaderProgram(BBG.AbstractShader.FromFile(BBG.ShaderStage.Compute, "VXGI/ConeTraceGI/compute.glsl"));

        SetSize(size);

        Settings = settings;
#else
        BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn, "ConeTracer needs compute shaders (GL_HAS_COMPUTE_SHADER); disabled in this build");
        Result = new BBG.Texture(BBG.Texture.Type.Texture2D);
        Result.Allocate(1, 1, 1, BBG.Texture.InternalFormat.R16G16B16A16Float);
        Settings = settings;
#endif
    }

    public void Compute(BBG.Texture voxelsAlbedo)
    {
#if GL_HAS_COMPUTE_SHADER
        BBG.Computing.Compute("Cone Trace GI", () =>
        {
            BBG.Cmd.SetUniforms(Settings);

            BBG.Cmd.BindImageUnit(Result, 0);
            BBG.Cmd.BindTextureUnit(voxelsAlbedo, 0);
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
