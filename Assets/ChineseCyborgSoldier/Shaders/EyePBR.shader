Shader "Custom/Eye"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _PupilColor("Pupil Color", Color) = (1,1,1,1)
        _PupilEmission("Pupil Emission", Float) = 1

        _Pupil("Pupil", 2D) = "white"{}        
        _PupilScale("Pupil Scale", Float) = 0.5
        _PupilSize("Pupil Size", Range(0, 0.5)) = 0.5
        _IOR_AirDivInternal("IOR_Air / IOR_Internal", Float) = 0.77
        
        _IrisMask("Iris Mask", 2D) = "white"{}

        _OffsetMul("Offset Multiplier", Float) = 0.5

        _IrisRadius("Iris Radius", Float) = 0.5        

        _EyeCornerColor("EyeCornerColor", Color) = (1,0,0,1)
        _EyeCornerPow("EyeCornerPow", Float) = 0.15

        _MetallicMap("Metallic Map",2D) = "white"{}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        _RoughnessMap("Roughness Map", 2D) = "white"{}
        _Roughness("Roughness", Range(0.0, 1.0)) = 0.5

        _NormalMap("Normal Map",2D) = "bump"{}
        _Normal("Normal",float) = 1.0

        _OcclusionMap("OcclusionMap",2D) = "white"{}
        _OcclusionStrength("Occlusion Strength",Range(0.0,1.0)) = 1.0

        _EmissionMap("Emission Map",2D) = "black"{}
        [HDR]_EmissionColor("Emission Color", Color) = (1,1,1,1)

        
        _SkyBoxCubeMap("SkyBox", Cube) = ""{}

        _EnvRotation("EnvRotation",Range(0.0,360.0)) = 0.0

        [Toggle(_DIFFUSE_OFF)] _DIFFUSE_OFF("DIFFUSE OFF",Float) = 0.0
        [Toggle(_SPECULAR_OFF)] _SPECULAR_OFF("SPECULAR OFF",Float) = 0.0
        [Toggle(_SH_OFF)] _SH_OFF("SH OFF",Float) = 0.0
        [Toggle(_IBL_OFF)] _IBL_OFF("IBL OFF",Float) = 0.0
    }

        SubShader
        {
            Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" "ShaderModel" = "4.5"}
            LOD 300

            // ------------------------------------------------------------------
            //  Forward pass. Shades all light in a single pass. GI + emission + Fog
            Pass
            {
                // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
                // no LightMode tag are also rendered by Universal Render Pipeline
                Tags{"LightMode" = "UniversalForward"}

                //Blend [_SrcBlend][_DstBlend]
                ZWrite on
                Blend One Zero
                Cull off

                HLSLPROGRAM
                #pragma exclude_renderers gles gles3 glcore
                #pragma target 4.5

            // -------------------------------------
            // Material Keywords            
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DIFFUSE_OFF
            #pragma shader_feature_local_fragment _SPECULAR_OFF
            #pragma shader_feature_local_fragment _SH_OFF
            #pragma shader_feature_local_fragment _IBL_OFF
            #pragma shader_feature_local_fragment _SCREEN_SPACE_REFLECTION_ON
            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Eye_Include.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Roughness;
                half _Normal;
                half _OcclusionStrength;

                half _EnvRotation;
                float4 _EmissionColor;            

                float _PupilScale;
                float _PupilSize;
                float4 _PupilColor;
                float4 _EyeCornerColor;
                float _IrisRadius;
                float _IOR_AirDivInternal;
                float _EyeCornerPow;

                float _OffsetMul;

                half4x4 LocalToWorldMatrix_Inverse;

                float ScaleMul;

                float _PupilEmission;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);         SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicMap);     SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_RoughnessMap);    SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_NormalMap);       SAMPLER(sampler_NormalMap);
            TEXTURE2D(_OcclusionMap);    SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);     SAMPLER(sampler_EmissionMap);            

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURECUBE(_SkyBoxCubeMap);
            SAMPLER(sampler_SkyBoxCubeMap);

            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionOS   : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
                float3 normalWS     : TEXCOORD3;
                half4  tangentWS    : TEXCOORD4;    // xyz: tangent, w: sign
                float4 shadowCoord  : TEXCOORD5;
                float4 positionCS   : SV_POSITION;                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o;

                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                o.uv = TRANSFORM_TEX(i.texcoord, _BaseMap);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(i.positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(i.normalOS, i.tangentOS);

                o.normalWS = normalInput.normalWS;

                real sign = i.tangentOS.w * GetOddNegativeScale();
                half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);

                o.tangentWS = tangentWS;

                o.shadowCoord = GetShadowCoord(vertexInput);

                o.positionOS = i.positionOS;
                o.positionCS = vertexInput.positionCS;
                o.positionWS = vertexInput.positionWS;

                return o;
            }

            

            void LightDataInitialization(Varyings i, out lightDatas o)
            {
                o = (lightDatas)0;
                o.positionWS = i.positionWS;
                o.V = GetWorldSpaceNormalizeViewDir(o.positionWS);
                o.N = normalize(i.normalWS);
                o.T = i.tangentWS.xyz;
                o.B = normalize(cross(o.N, o.T) * i.tangentWS.w);
                o.screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
            }

            half3 RefractedViewDir(half3 normalWS, half3 viewWS)
            {
                half facing = dot(normalWS, viewWS);
                half w = _IOR_AirDivInternal * facing;
                half k = sqrt(1 + (w + _IOR_AirDivInternal) * (w - _IOR_AirDivInternal));
                return -normalize((w - k) * normalWS - _IOR_AirDivInternal * viewWS);
            }

            half3 SurfaceDataInitialization(Varyings i, out surfaceDatas o, half3 V, half3 Tan, half3 Bitan, half3 N, inout half3 emissionColor)
            {
                o = (surfaceDatas)0;
                half4 color;

                half dis = length(half2(0.5, 0.5) - i.uv);
                

                float4 normalTS = normalize(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                o.normalTS = UnpackNormalScale(normalTS, _Normal);

                half3x3 TBN = half3x3(Tan, Bitan, N);
                half3 normal = normalize(mul(o.normalTS, TBN));
               
                half2 halfuv = half2(0.5, 0.5);                              

                if (length(halfuv - i.uv) < _IrisRadius)
                {
                    half2 pupilUV = pow((i.uv - halfuv) / _PupilScale, 1)+ halfuv;

                    // physically based refraction------------------------------------------

                    half3 refractedW = RefractedViewDir(normal,V);
                    half cosAlpha = dot(normal, -refractedW);
                    half height = saturate(1 - (pow((pupilUV - halfuv).x, 2) + pow((pupilUV - halfuv).y, 2)) * 4);
                    half dist = height / cosAlpha;
                    half4 offsetW = half4(dist * refractedW,0);
                    
                    half2 offsetL = mul((float4x4)LocalToWorldMatrix_Inverse, offsetW).xy;
                    
                    _OffsetMul *= ScaleMul;
                    pupilUV += half2(_OffsetMul * offsetL.x, -_OffsetMul * offsetL.y);

                    // physically based refraction------------------------------------------


                    half4 s = SAMPLE_TEXTURE2D(_Pupil, sampler_Pupil, pupilUV);
                    color = s * _PupilColor;
                    emissionColor = color;
                }
                else
                {
                    //half4 irisCol = half4(1, 0, 0, 1);
                    half t = SAMPLE_TEXTURE2D(_IrisMask, sampler_IrisMask, i.uv);
                    t = pow(t, _EyeCornerPow);
                    color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                    color.rgb = color.rgb * lerp(_BaseColor, _EyeCornerColor, t);
                }
                //albedo & alpha & specular
                o.albedo = color.rgb;

                o.specular = (half3)0;
                //metallic & roughness
                half metallic = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, i.uv).r * _Metallic;
                o.metallic = saturate(metallic);
                half roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, i.uv).r * _Roughness;
                o.roughness = max(saturate(roughness), 0.001f);
                half smoothness= SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, i.uv).a * _Metallic;
                o.roughness = max(saturate((1 - smoothness)* (1 - smoothness)), 0.001f);
             
                
                //occlusion
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).r;
                o.occlusion = lerp(1.0, occlusion, _OcclusionStrength);

                return normal;
            }

            float4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                lightDatas _lightDatas;
                surfaceDatas _surfaceDatas;
                LightDataInitialization(i, _lightDatas);

                half3 emission = 0;

                _lightDatas.N = SurfaceDataInitialization(i, _surfaceDatas, _lightDatas.V, _lightDatas.T, _lightDatas.B, _lightDatas.N, emission);

                float4 litRes = StandardLit(_lightDatas, _surfaceDatas, i.positionWS, i.shadowCoord, _EnvRotation) + float4(emission * _PupilEmission, 1);

                return litRes;
            }                        

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // -------------------------------------
            // Universal Pipeline keywords

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

            // This pass is used when drawing to a _CameraNormalsTexture texture
            Pass
            {
                Name "DepthNormals"
                Tags{"LightMode" = "DepthNormals"}

                ZWrite On
                Cull[_Cull]

                HLSLPROGRAM
                #pragma exclude_renderers gles gles3 glcore
                #pragma target 4.5

                #pragma vertex DepthNormalsVertex
                #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
        }
}