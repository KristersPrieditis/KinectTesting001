#ifndef PBR_LIT_INCLUDED
#define PBR_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

#define CUSTOM_NAMESPACE_START(namespace) struct _##namespace {
#define CUSTOM_NAMESPACE_CLOSE(namespace) }; _##namespace namespace;

#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)


TEXTURE2D(_SSSLUT);
SAMPLER(sampler_SSSLUT);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

CUSTOM_NAMESPACE_START(Common)
inline half Pow2(half x)
{
    return x * x;
}
inline half Pow4(half x)
{
    return x * x * x * x;
}
inline half Pow5(half x)
{
    return x * x * x * x * x;
}
inline half3 RotateDirection(half3 R, half degrees)
{
    float3 reflUVW = R;
    half theta = degrees * PI / 180.0f;
    half costha = cos(theta);
    half sintha = sin(theta);
    reflUVW = half3(reflUVW.x * costha - reflUVW.z * sintha, reflUVW.y, reflUVW.x * sintha + reflUVW.z * costha);
    return reflUVW;
}
CUSTOM_NAMESPACE_CLOSE(Common)


struct lightDatas
{
    float3 positionWS;
    half3  V; //ViewDirWS
    half3  N; //NormalWS
    half3  B; //BinormalWS
    half3  T; //TangentWS
    float2 screenUV;
};

struct surfaceDatas
{
    half3 albedo;
    half3 specular;
    half3 normalTS;
    half  metallic;
    half  roughness;
    half  occlusion;
    half  alpha;
    half  mask;
};


float GGXNormalDistribution(float roughness, float NdotH)       // D function
{
    float roughnessSqr = roughness * roughness;
    float NdotHSqr = NdotH * NdotH;
    float TanNdotHSqr = (1 - NdotHSqr) / NdotHSqr;
    return (1.0 / 3.1415926535) * Common.Pow2(roughness / (NdotHSqr * (roughnessSqr + TanNdotHSqr)));
}

float GGXGeometricShadowingFunction(float roughness, float NdotL, float NdotV) // G function
{
    float roughnessSqr = roughness * roughness;
    float NdotLSqr = NdotL * NdotL;
    float NdotVSqr = NdotV * NdotV;

    float SmithL = (2 * NdotL) / (NdotL + sqrt(roughnessSqr +
        (1 - roughnessSqr) * NdotLSqr));
    float SmithV = (2 * NdotV) / (NdotV + sqrt(roughnessSqr +
        (1 - roughnessSqr) * NdotVSqr));

    float Gs = (SmithL * SmithV);
    return Gs;
}

real D_GGXNoPI1(real NdotH, real roughness)
{
    real a2 = Sq(roughness);
    real s = (NdotH * a2 - NdotH) * NdotH + 1.0;

    // If roughness is 0, returns (NdotH == 1 ? 1 : 0).
    // That is, it returns 1 for perfect mirror reflection, and 0 otherwise.
    return SafeDiv(a2, s * s);
}

real D_GGX1(real NdotH, real roughness)
{
    return INV_PI * D_GGXNoPI1(NdotH, roughness);
}

// Ref: Understanding the Masking-Shadowing Function in Microfacet-Based BRDFs, p. 19, 29.
// p. 84 (37/60)
real G_MaskingSmithGGX1(real NdotV, real roughness)
{
    // G1(V, H)    = HeavisideStep(VdotH) / (1 + Lambda(V)).
    // Lambda(V)        = -0.5 + 0.5 * sqrt(1 + 1 / a^2).
    // a           = 1 / (roughness * tan(theta)).
    // 1 + Lambda(V)    = 0.5 + 0.5 * sqrt(1 + roughness^2 * tan^2(theta)).
    // tan^2(theta) = (1 - cos^2(theta)) / cos^2(theta) = 1 / cos^2(theta) - 1.
    // Assume that (VdotH > 0), e.i. (acos(LdotV) < Pi).

    return 1.0 / (0.5 + 0.5 * sqrt(1.0 + Sq(roughness) * (1.0 / Sq(NdotV) - 1.0)));
}

//-----------------------------------------------------------  F -------------------------------------------------------------------
float3 F_None(float3 SpecularColor)
{
    return SpecularColor;
}

// [Schlick 1994, "An Inexpensive BRDF Model for Physically-Based Rendering"]
float3 F_Schlick_UE5(float3 SpecularColor, float VoH)
{
    float Fc = Common.Pow5(1 - VoH);					// 1 sub, 3 mul
    //return Fc + (1 - Fc) * SpecularColor;		// 1 add, 3 mad

    // Anything less than 2% is physically impossible and is instead considered to be shadowing
    return saturate(50.0 * SpecularColor.g) * Fc + (1 - Fc) * SpecularColor;
}
//-----------------------------------------------------------  F -------------------------------------------------------------------

float Specularity(half NdotL, half NdotV, half NdotH, half LdotH, half VdotH, float roughness, float3 specular)
{
    return D_GGX1(NdotH, roughness) * G_MaskingSmithGGX1(NdotV, roughness) * F_Schlick_UE5(specular, VdotH);
}

half DirectBRDF_Specular(float roughness, float NdotH, float LdotH)
{
    half roughness2 = Common.Pow2(roughness);
    float d = NdotH * NdotH * (roughness2 - half(1.0)) + 1.00001f;

    half LoH2 = LdotH * LdotH;
    half specularTerm = roughness2 / ((d * d) * max(0.1, LoH2) * (roughness * (half)4.0 + half(2.0)));
    return specularTerm;
}

half OneMinusReflectivityMetallicCustom(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in kDielectricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    half oneMinusDielectricSpec = kDielectricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

float3 standardBRDF(lightDatas lightDat, surfaceDatas surfDat, half3 L, half3 lightCol, float shadow)
{
    float a2 = Common.Pow4(surfDat.roughness);
    //float a2 = Common.Pow2(surfDat.roughness);

    half3 H = normalize(lightDat.V + L);
    half NdotH = saturate(dot(lightDat.N, H));
    half NdotV = saturate(abs(dot(lightDat.N, lightDat.V)) + 1e-5);
    half NdotL = saturate(dot(lightDat.N, L));
    half VdotH = saturate(dot(lightDat.V, H));//LoH
    half LdotH = saturate(dot(H, L));
    float3 radiance = NdotL * lightCol * shadow;

    float3 diffuseTerm = surfDat.albedo * OneMinusReflectivityMetallicCustom(surfDat.metallic);
#if defined(_DIFFUSE_OFF)
    diffuseTerm = half3(0, 0, 0);
#endif

    float3 specularTerm = Specularity(NdotL, NdotV, NdotH, LdotH, VdotH, surfDat.roughness, surfDat.specular);//DirectBRDF_Specular(surfDat.roughness, NdotH, LdotH)* lerp(kDieletricSpec.rgb, surfDat.albedo, surfDat.metallic);// * surfDat.metallic;

#if defined(_SPECULAR_OFF)
    specularTerm = half3(0, 0, 0);
#endif    

    return (diffuseTerm + specularTerm) * radiance;
}

float3 SkinBRDF(lightDatas lightDat, surfaceDatas surfDat, half3 L, half3 lightCol, float shadow, float curvature)
{
    float a2 = Common.Pow4(surfDat.roughness);

    half3 H = normalize(lightDat.V + L);
    half NdotH = saturate(dot(lightDat.N, H));
    half NdotV = saturate(abs(dot(lightDat.N, lightDat.V)) + 1e-5);
    half NdotL = dot(lightDat.N, L);
    half VdotH = saturate(dot(lightDat.V, H));//LoH
    half LdotH = saturate(dot(H, L));
    float3 radiance = saturate(NdotL) * lightCol * shadow;

    half wrapNoL = saturate(NdotL * 0.5 + 0.5);
    //half wrapNoL = lerp(0, 0.99, NdotL);

    float3 sss = SAMPLE_TEXTURE2D(_SSSLUT, sampler_SSSLUT, float2(lerp(0.01, 0.99, wrapNoL), lerp(0.01, 0.99, curvature)));

    float3 diffuseTerm = surfDat.albedo * OneMinusReflectivityMetallicCustom(surfDat.metallic) * sss;
#if defined(_DIFFUSE_OFF)
    diffuseTerm = half3(0, 0, 0);
#endif

    float3 specularTerm = DirectBRDF_Specular(surfDat.roughness, NdotH, LdotH) * lerp(kDieletricSpec.rgb, surfDat.albedo, surfDat.metallic);// * surfDat.metallic;

#if defined(_SPECULAR_OFF)
    specularTerm = half3(0, 0, 0);
#endif

    return  diffuseTerm * lightCol * shadow + specularTerm * radiance;
}

half3 SkinShading(lightDatas lightDat, surfaceDatas surfDat, float3 positionWS, float4 shadowCoord, float curvature)
{


    half3 directLighting = (half3)0;
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float4 positionCS = TransformWorldToHClip(positionWS);
    shadowCoord = ComputeScreenPos(positionCS);
#else
    shadowCoord = TransformWorldToShadowCoord(positionWS);
#endif
    half4 shadowMask = (half4)1.0;


    //add light
    half3 directLighting_AddLight = (half3)0;

    uint pixelLightCount = 0;
#ifdef _ADDITIONAL_LIGHTS
    pixelLightCount = GetAdditionalLightsCount();
#endif
    if (surfDat.mask > 0.5)
    {
        //main light
        half3 directLighting_MainLight = (half3)0;
        {
            Light light = GetMainLight(shadowCoord, positionWS, shadowMask);
            half3 L = light.direction;
            half3 lightColor = light.color;
            //SSAO
#if defined(_SCREEN_SPACE_OCCLUSION)
            AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(lightDat.screenUV);
            lightColor *= aoFactor.directAmbientOcclusion;
#endif
            half shadow = light.shadowAttenuation;
            directLighting_MainLight = SkinBRDF(lightDat, surfDat, L, lightColor, shadow, curvature);
        }


        UNITY_LOOP
            for (uint lightIndex = 0; lightIndex < pixelLightCount; lightIndex++)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
                half3 L = light.direction;
                half3 lightColor = light.color;
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                directLighting_AddLight += SkinBRDF(lightDat, surfDat, L, lightColor, shadow, curvature);
            }
        return directLighting_MainLight + directLighting_AddLight;
    }
    else
    {
        //main light
        half3 directLighting_MainLight = (half3)0;
        {
            Light light = GetMainLight(shadowCoord, positionWS, shadowMask);
            half3 L = light.direction;
            half3 lightColor = light.color;
            //SSAO
#if defined(_SCREEN_SPACE_OCCLUSION)
            AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(lightDat.screenUV);
            lightColor *= aoFactor.directAmbientOcclusion;
#endif
            half shadow = light.shadowAttenuation;
            directLighting_MainLight = standardBRDF(lightDat, surfDat, L, lightColor, shadow);
        }

        UNITY_LOOP
            for (uint lightIndex = 0; lightIndex < pixelLightCount; lightIndex++)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
                half3 L = light.direction;
                half3 lightColor = light.color;
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                directLighting_AddLight += standardBRDF(lightDat, surfDat, L, lightColor, shadow);
            }
        return directLighting_MainLight + directLighting_AddLight;
    }

    return 0;
}

half3 EnvBRDFApprox(half3 SpecularColor, half Roughness, half NoV)
{
    // [ Lazarov 2013, "Getting More Physical in Call of Duty: Black Ops II" ]
    // Adaptation to fit our G term.
    const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
    const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
    half4 r = Roughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    half2 AB = half2(-1.04, 1.04) * a004 + r.zw;

    // Anything less than 2% is physically impossible and is instead considered to be shadowing
    // Note: this is needed for the 'specular' show flag to work, since it uses a SpecularColor of 0
    AB.y *= saturate(50.0 * SpecularColor.g);

    return SpecularColor * AB.x + AB.y;
}

half3 EnvBRDF(lightDatas litDat, surfaceDatas surfDat, float envRotation, float3 positionWS)
{
    half NoV = saturate(abs(dot(litDat.N, litDat.V)) + 1e-5);//区分正反面
    half3 R = reflect(-litDat.V, litDat.N);
    R = Common.RotateDirection(R, envRotation);

    //SH
    float3 diffuseAO = GTAOMultiBounce(surfDat.occlusion, surfDat.albedo);
    float3 radianceSH = SampleSH(litDat.N);
    float3 indirectDiffuseTerm = radianceSH * surfDat.albedo * diffuseAO;
#if defined(_SH_OFF)
    indirectDiffuseTerm = half3(0, 0, 0);
#endif

    //IBL
    //The Split Sum: 1nd Stage
    half3 specularLD = GlossyEnvironmentReflection(R, positionWS, surfDat.roughness, surfDat.occlusion);
    //The Split Sum: 2nd Stage
    half3 specularDFG = EnvBRDFApprox(surfDat.specular, surfDat.roughness, NoV);
    //AO 处理漏光
    float specularOcclusion = GetSpecularOcclusionFromAmbientOcclusion(NoV, surfDat.occlusion, surfDat.roughness);
    float3 specularAO = GTAOMultiBounce(specularOcclusion, surfDat.specular);

    float3 indirectSpecularTerm = specularLD * specularDFG * specularAO;
#if defined(_IBL_OFF)
    indirectSpecularTerm = half3(0, 0, 0);
#endif
    return indirectDiffuseTerm + indirectSpecularTerm;
}

half3 EnvShading(lightDatas litDat, surfaceDatas surfDat, float envRotation, float3 positionWS)
{
    half3 inDirectLighting = (half3)0;

    inDirectLighting = EnvBRDF(litDat, surfDat, envRotation, positionWS);

    return inDirectLighting;
}

half4 StandardLit(inout lightDatas lightDat, surfaceDatas surfDat, float3 positionWS, float4 shadowCoord, float envRotation, float curvature)
{
    float3 albedo = surfDat.albedo;
    surfDat.albedo = lerp(surfDat.albedo, float3(0.0, 0.0, 0.0), surfDat.metallic);
    surfDat.specular = lerp(float3(0.04, 0.04, 0.04), albedo, surfDat.metallic);
    half3x3 TBN = half3x3(lightDat.T, lightDat.B, lightDat.N);
    lightDat.N = normalize(mul(surfDat.normalTS, TBN));

    //SSAO
#if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(lightDat.screenUV);
    surfDat.occlusion = min(surfDat.occlusion, aoFactor.indirectAmbientOcclusion);
#endif

    //DirectLighting
    half3 directLighting = SkinShading(lightDat, surfDat, positionWS, shadowCoord, curvature);

    //IndirectLighting
    half3 inDirectLighting = EnvShading(lightDat, surfDat, envRotation, positionWS);

    return half4(directLighting + inDirectLighting, surfDat.alpha);
}

#endif