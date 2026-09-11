namespace BBOpenGL;

/// <summary>
/// The compile-time GL feature set, as booleans.
///
/// Every name here matches a symbol from GLProfiles.props. Use #if for whole
/// statements that would call a function the driver does not have; use these
/// constants where you need a value, since the compiler folds them away and
/// drops the dead branch just the same.
/// </summary>
public static class GLCaps
{
    /// <summary>GL 4.2. glTextureStorage*, immutable texture allocation.</summary>
    public const bool TextureStorage =
#if GL_HAS_TEXTURE_STORAGE
        true;
#else
        false;
#endif

    /// <summary>GL 4.2. image load/store, glBindImageTexture.</summary>
    public const bool ImageLoadStore =
#if GL_HAS_IMAGE_LOAD_STORE
        true;
#else
        false;
#endif

    /// <summary>GL 4.2. Atomic counter buffers.</summary>
    public const bool AtomicCounters =
#if GL_HAS_ATOMIC_COUNTERS
        true;
#else
        false;
#endif

    /// <summary>GL 4.3. Compute shaders and glDispatchCompute.</summary>
    public const bool ComputeShader =
#if GL_HAS_COMPUTE_SHADER
        true;
#else
        false;
#endif

    /// <summary>GL 4.3. Shader storage buffer objects.</summary>
    public const bool ShaderStorageBuffer =
#if GL_HAS_SSBO
        true;
#else
        false;
#endif

    /// <summary>GL 4.3. glMultiDrawElementsIndirect and friends.</summary>
    public const bool MultiDrawIndirect =
#if GL_HAS_MULTI_DRAW_INDIRECT
        true;
#else
        false;
#endif

    /// <summary>GL 4.3. glTextureView.</summary>
    public const bool TextureView =
#if GL_HAS_TEXTURE_VIEW
        true;
#else
        false;
#endif

    /// <summary>GL 4.3 / KHR_debug. Debug callbacks and object labels.</summary>
    public const bool DebugOutput =
#if GL_HAS_DEBUG_OUTPUT
        true;
#else
        false;
#endif

    /// <summary>GL 4.3. glInvalidateBufferData, glInvalidateFramebuffer.</summary>
    public const bool InvalidateSubdata =
#if GL_HAS_INVALIDATE_SUBDATA
        true;
#else
        false;
#endif

    /// <summary>GL 4.4. glBufferStorage, persistent mapping.</summary>
    public const bool BufferStorage =
#if GL_HAS_BUFFER_STORAGE
        true;
#else
        false;
#endif

    /// <summary>GL 4.5. Direct state access. The whole renderer is built on it.</summary>
    public const bool DirectStateAccess =
#if GL_HAS_DSA
        true;
#else
        false;
#endif

    /// <summary>GL 4.5. glClipControl, zero-to-one depth.</summary>
    public const bool ClipControl =
#if GL_HAS_CLIP_CONTROL
        true;
#else
        false;
#endif

    /// <summary>GL 4.5. glTextureBarrier.</summary>
    public const bool TextureBarrier =
#if GL_HAS_TEXTURE_BARRIER
        true;
#else
        false;
#endif

    /// <summary>GL 4.6. glPolygonOffsetClamp.</summary>
    public const bool PolygonOffsetClamp =
#if GL_HAS_POLYGON_OFFSET_CLAMP
        true;
#else
        false;
#endif

    /// <summary>GL 4.6, or EXT_texture_filter_anisotropic well before that.</summary>
    public const bool AnisotropicFilter =
#if GL_HAS_ANISOTROPIC_FILTER
        true;
#else
        false;
#endif

    /// <summary>GL 4.6. glSpecializeShader, SPIR-V shader binaries.</summary>
    public const bool SpirV =
#if GL_HAS_SPIRV
        true;
#else
        false;
#endif

    /// <summary>ARB_bindless_texture. Texture handles instead of binding slots.</summary>
    public const bool BindlessTexture =
#if GL_HAS_BINDLESS_TEXTURE
        true;
#else
        false;
#endif

    /// <summary>ARB_shader_viewport_layer_array. Writing gl_Layer from a vertex shader.</summary>
    public const bool ShaderViewportLayer =
#if GL_HAS_SHADER_VIEWPORT_LAYER
        true;
#else
        false;
#endif

    /// <summary>ARB_shading_language_include. #include resolved by the driver.</summary>
    public const bool ShadingLanguageInclude =
#if GL_HAS_SHADING_LANGUAGE_INCLUDE
        true;
#else
        false;
#endif

    /// <summary>KHR_shader_subgroup. Ballot and arithmetic across a wave.</summary>
    public const bool Subgroup =
#if GL_HAS_SUBGROUP
        true;
#else
        false;
#endif

    /// <summary>NV_mesh_shader.</summary>
    public const bool MeshShader =
#if GL_HAS_MESH_SHADER
        true;
#else
        false;
#endif

    /// <summary>NV_conservative_raster. Used by the voxelizer.</summary>
    public const bool ConservativeRaster =
#if GL_HAS_CONSERVATIVE_RASTER
        true;
#else
        false;
#endif

    /// <summary>NV_shading_rate_image.</summary>
    public const bool VariableRateShading =
#if GL_HAS_VARIABLE_RATE_SHADING
        true;
#else
        false;
#endif

    /// <summary>NV_viewport_swizzle. Used by the voxelizer.</summary>
    public const bool ViewportSwizzle =
#if GL_HAS_VIEWPORT_SWIZZLE
        true;
#else
        false;
#endif

    /// <summary>NV_geometry_shader_passthrough.</summary>
    public const bool GeometryPassthrough =
#if GL_HAS_GEOMETRY_PASSTHROUGH
        true;
#else
        false;
#endif

    /// <summary>AMD_gpu_shader_half_float. 16-bit arithmetic in shaders.</summary>
    public const bool Fp16Arithmetic =
#if GL_HAS_FP16_ARITHMETIC
        true;
#else
        false;
#endif

    /// <summary>NV_shader_atomic_fp16_vector.</summary>
    public const bool Fp16Atomics =
#if GL_HAS_FP16_ATOMICS
        true;
#else
        false;
#endif

    /// <summary>ARB/AMD_seamless_cubemap_per_texture.</summary>
    public const bool SeamlessCubemapPerTexture =
#if GL_HAS_SEAMLESS_CUBEMAP_PER_TEXTURE
        true;
#else
        false;
#endif

    /// <summary>The GL version the context is asked for.</summary>
    public const int VersionMajor = 4;

    public const int VersionMinor =
#if GL_CONTEXT_4_6
        6;
#elif GL_CONTEXT_4_5
        5;
#elif GL_CONTEXT_4_3
        3;
#else
        1;
#endif

    /// <summary>
    /// True where a compatibility context is not on offer. macOS gives you core
    /// plus forward-compatible or it gives you 2.1.
    /// </summary>
    public const bool CoreProfileOnly =
#if GL_CORE_PROFILE_ONLY
        true;
#else
        false;
#endif
}
