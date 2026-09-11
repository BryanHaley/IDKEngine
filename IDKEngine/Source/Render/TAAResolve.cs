using System;
using OpenTK.Mathematics;
using BBOpenGL;
using IDKEngine.Utils;

namespace IDKEngine.Render;

class TAAResolve : IDisposable
{
    public record struct GpuSettings
    {
        public bool IsNaiveTaa;
        public float PreferAliasingOverBlur = 0.25f;

        public GpuSettings()
        {
        }
    }

    public GpuSettings Settings;

#if GL_HAS_COMPUTE_SHADER
    public BBG.Texture Result => (frame % 2 == 0) ? taaPing : taaPong;
    public BBG.Texture PrevResult => (frame % 2 == 0) ? taaPong : taaPing;

    private BBG.Texture taaPing;
    private BBG.Texture taaPong;
    private readonly BBG.AbstractShaderProgram taaResolveProgram;
    private int frame;
#else
    public BBG.Texture Result;
    public BBG.Texture PrevResult => Result;
#endif
    public TAAResolve(Vector2i size, in GpuSettings settings)
    {
#if GL_HAS_COMPUTE_SHADER
        taaResolveProgram = new BBG.AbstractShaderProgram(BBG.AbstractShader.FromFile(BBG.ShaderStage.Compute, "TAAResolve/compute.glsl"));

        SetSize(size);
        Settings = settings;
#else
        BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn, "TAA Resolve needs compute shaders (GL_HAS_COMPUTE_SHADER); disabled in this build");
        Result = new BBG.Texture(BBG.Texture.Type.Texture2D);
        Result.Allocate(1, 1, 1, BBG.Texture.InternalFormat.R16G16B16A16Float);
        Settings = settings;
#endif
    }

    public void Compute(BBG.Texture color)
    {
#if GL_HAS_COMPUTE_SHADER
        frame++;

        BBG.Computing.Compute("Temporal Anti Aliasing", () =>
        {
            BBG.Cmd.SetUniforms(Settings);

            BBG.Cmd.BindImageUnit(Result, 0);
            BBG.Cmd.BindTextureUnit(PrevResult, 0);
            BBG.Cmd.BindTextureUnit(color, 1);
            BBG.Cmd.UseShaderProgram(taaResolveProgram);

            BBG.Computing.Dispatch(MyMath.DivUp(Result.Width, 8), MyMath.DivUp(Result.Height, 8), 1);
            BBG.Cmd.MemoryBarrier(BBG.Cmd.MemoryBarrierMask.TextureFetchBarrierBit);
        });
#endif
    }

    public void SetSize(Vector2i size)
    {
#if GL_HAS_COMPUTE_SHADER
        if (taaPing != null) taaPing.Dispose();
        taaPing = new BBG.Texture(BBG.Texture.Type.Texture2D);
        taaPing.SetFilter(BBG.Sampler.MinFilter.Linear, BBG.Sampler.MagFilter.Linear);
        taaPing.SetWrapMode(BBG.Sampler.WrapMode.ClampToEdge, BBG.Sampler.WrapMode.ClampToEdge);
        taaPing.Allocate(size.X, size.Y, 1, BBG.Texture.InternalFormat.R16G16B16A16Float);

        if (taaPong != null) taaPong.Dispose();
        taaPong = new BBG.Texture(BBG.Texture.Type.Texture2D);
        taaPong.SetFilter(BBG.Sampler.MinFilter.Linear, BBG.Sampler.MagFilter.Linear);
        taaPong.SetWrapMode(BBG.Sampler.WrapMode.ClampToEdge, BBG.Sampler.WrapMode.ClampToEdge);
        taaPong.Allocate(size.X, size.Y, 1, BBG.Texture.InternalFormat.R16G16B16A16Float);
#endif
    }

    public void Dispose()
    {
#if GL_HAS_COMPUTE_SHADER
        taaPing.Dispose();
        taaPong.Dispose();
        taaResolveProgram.Dispose();
#else
        Result.Dispose();
#endif
    }

    public static float GetRecommendedMipmapBias(int renderWidth, int displayWith)
    {
        return float.Log2((float)renderWidth / displayWith) - 1.0f;
    }
}
