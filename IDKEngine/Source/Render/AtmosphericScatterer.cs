using System;
using BBOpenGL;
using IDKEngine.Utils;

namespace IDKEngine.Render;

class AtmosphericScatterer : IDisposable
{
    public record struct GpuSettings
    {
        public int ISteps = 40;
        public int JSteps = 8;
        public float LightIntensity = 15.0f;
        public float Azimuth = 0.0f;
        public float Elevation = 0.0f;

        public GpuSettings()
        {
        }
    }

    public GpuSettings Settings;

    public BBG.Texture Result;
#if GL_HAS_COMPUTE_SHADER
    private readonly BBG.AbstractShaderProgram shaderProgram;
#endif
    public AtmosphericScatterer(int size, in GpuSettings settings)
    {
#if GL_HAS_COMPUTE_SHADER
        shaderProgram = new BBG.AbstractShaderProgram(BBG.AbstractShader.FromFile(BBG.ShaderStage.Compute, "AtmosphericScattering/compute.glsl"));

        Settings = settings;

        SetSize(size);
#else
        BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn, "AtmosphericScatterer needs compute shaders (GL_HAS_COMPUTE_SHADER); disabled in this build");
        Result = new BBG.Texture(BBG.Texture.Type.Cubemap);
        Result.Allocate(1, 1, 1, BBG.Texture.InternalFormat.R32G32B32A32Float);
        Settings = settings;
#endif
    }

    public void Compute()
    {
#if GL_HAS_COMPUTE_SHADER
        BBG.Computing.Compute("Compute Atmospheric Scattering", () =>
        {
            Settings.LightIntensity = float.MaxNative(Settings.LightIntensity, 0.0f);

            BBG.Cmd.SetUniforms(Settings);

            BBG.Cmd.BindImageUnit(Result, 0, 0, true);
            BBG.Cmd.UseShaderProgram(shaderProgram);

            BBG.Computing.Dispatch(MyMath.DivUp(Result.Width, 8), MyMath.DivUp(Result.Width, 8), 6);
            BBG.Cmd.MemoryBarrier(BBG.Cmd.MemoryBarrierMask.TextureFetchBarrierBit);
        });
#endif
    }

    public void SetSize(int size)
    {
#if GL_HAS_COMPUTE_SHADER
        if (Result != null) Result.Dispose();
        Result = new BBG.Texture(BBG.Texture.Type.Cubemap);
        Result.Allocate(size, size, 1, BBG.Texture.InternalFormat.R32G32B32A32Float);
        Result.SetFilter(BBG.Sampler.MinFilter.Linear, BBG.Sampler.MagFilter.Linear);
#endif
    }

    public void Dispose()
    {
#if GL_HAS_COMPUTE_SHADER
        shaderProgram.Dispose();
        Result.Dispose();
#else
        Result.Dispose();
#endif
    }
}
