AppInclude(include/Constants.glsl)
AppInclude(include/GpuTypes.glsl)

// Keep in sync between shader and client code!
#define GPU_MAX_UBO_POINT_SHADOW_COUNT 128
#define GPU_MAX_UBO_LIGHT_COUNT 256

// Binding 0 is reserved for temporary UBOs

layout(std140, binding = 1) uniform PerFrameDataUBO
{
    GpuPerFrameData perFrameDataUBO;
};

layout(std140, binding = 2) uniform LightsUBO
{
    GpuLight Lights[GPU_MAX_UBO_LIGHT_COUNT];
    int Count;
} lightsUBO;

layout(std140, binding = 3) uniform ShadowsUBO
{
    GpuPointShadow PointShadows[GPU_MAX_UBO_POINT_SHADOW_COUNT];
    int Count;
} shadowsUBO;

layout(std140, binding = 4) uniform TaaDataUBO
{
    vec2 Jitter;
    int SampleCount;
    float MipmapBias;
    ENUM_ANTI_ALIASING_MODE TemporalAntiAliasingMode;
} taaDataUBO;

#if APP_HAS_BINDLESS_TEXTURE
layout(std140, binding = 5) uniform SkyBoxUBO
{
    samplerCube Albedo;
} skyBoxUBO;
#define SkyBoxTex skyBoxUBO.Albedo
#else
// no bindless: the same textures, bound to fixed units by the client
layout(binding = 14) uniform samplerCube SkyBoxTex;
#endif

layout(std140, binding = 6) uniform VoxelizerDataUBO
{
    vec3 GridMin;
    float _pad0;
    vec3 GridMax;
    float _pad1;
} voxelizerDataUBO;

#if APP_HAS_BINDLESS_TEXTURE
layout(std140, binding = 7) uniform GBufferDataUBO
{
    sampler2D Albedo;
    sampler2D Normal;
    sampler2D MetallicRoughness;
    sampler2D Emissive;
    sampler2D Velocity;
    sampler2D Depth;
} gBufferDataUBO;
#define GBufferTex_Albedo            gBufferDataUBO.Albedo
#define GBufferTex_Normal            gBufferDataUBO.Normal
#define GBufferTex_MetallicRoughness gBufferDataUBO.MetallicRoughness
#define GBufferTex_Emissive          gBufferDataUBO.Emissive
#define GBufferTex_Velocity          gBufferDataUBO.Velocity
#define GBufferTex_Depth             gBufferDataUBO.Depth
#else
layout(binding = 8)  uniform sampler2D GBufferTex_Albedo;
layout(binding = 9)  uniform sampler2D GBufferTex_Normal;
layout(binding = 10) uniform sampler2D GBufferTex_MetallicRoughness;
layout(binding = 11) uniform sampler2D GBufferTex_Emissive;
layout(binding = 12) uniform sampler2D GBufferTex_Velocity;
layout(binding = 13) uniform sampler2D GBufferTex_Depth;
#endif