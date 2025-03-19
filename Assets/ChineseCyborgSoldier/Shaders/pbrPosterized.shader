Shader "Custom/PBR_Posterized"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _MetallicMap("Metallic Map",2D) = "white"{}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        _RoughnessMap("Roughness Map", 2D) = "white"{}
        _Roughness("Roughness", Range(0.0, 1.0)) = 0.5

        _NormalMap("Normal Map",2D) = "bump"{}
        _Normal("Normal",float) = 1.0

        _OcclusionMap("OcclusionMap",2D) = "white"{}
        _OcclusionStrength("Occlusion Strength",Range(0.0,1.0)) = 1.0

        _EmissionMap("Emission Map",2D) = "black"{}
        _EmissionColor("Emission Color", Color) = (1,1,1,1)

        
        _SkyBoxCubeMap("SkyBox", Cube) = ""{}

        _EnvRotation("EnvRotation",Range(0.0,360.0)) = 0.0

        _Step("Toon Color Steps", Float) = 5

        [Toggle(_DIFFUSE_OFF)] _DIFFUSE_OFF("DIFFUSE OFF",Float) = 0.0
        [Toggle(_SPECULAR_OFF)] _SPECULAR_OFF("SPECULAR OFF",Float) = 0.0
        [Toggle(_SH_OFF)] _SH_OFF("SH OFF",Float) = 0.0
        [Toggle(_IBL_OFF)] _IBL_OFF("IBL OFF",Float) = 0.0


        _Ior("Index of Refraction",  Range(1,4)) = 1.5
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
                Blend srcAlpha oneMinusSrcAlpha
                Cull Back

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

            //不支持合批是因为后面的Pass用的cbuffer中的内容不一致，此处仅为了演示PBR效果故不更改
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "pbrLitInclude.hlsl"
            
            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Roughness;
                half _Normal;
                half _OcclusionStrength;
                half _Cutoff;
                half _EnvRotation;
                float _Ior;
                float4 _EmissionColor;

                // matrices for SSR calculation
                float4x4 _InverseProjectionMatrix;
                float4x4 _InverseViewMatrix;
                float4x4 _VM;
                float4x4 _PV;
                float4x4 _Camera_INV_VP;

                float _Step;
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

            void SurfaceDataInitialization(Varyings i, out surfaceDatas o)
            {
                o = (surfaceDatas)0;

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                //albedo & alpha & specular
                o.albedo = color.rgb;
                o.alpha = color.a;
#if defined(_ALPHATEST_ON)
                clip(o.alpha - _Cutoff);
#endif
                o.specular = (half3)0;
                //o.specular = 

                //metallic & roughness
                half metallic = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, i.uv).r * _Metallic;
                o.metallic = saturate(metallic);
                half roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, i.uv).r * _Roughness;
                o.roughness = max(saturate(roughness), 0.001f);
                half smoothness= SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, i.uv).a * _Metallic;
                //o.roughness = max(saturate((1 - o.metallic)* (1 - o.metallic)), 0.001f);
                o.roughness = max(saturate((1 - smoothness)* (1 - smoothness)), 0.001f);
                //o.roughness = (1 - smoothness);

                //normalTS (tangent Space)
                float4 normalTS = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv);
                o.normalTS = UnpackNormalScale(normalTS, _Normal);

                //occlusion
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).r;
                o.occlusion = lerp(1.0, occlusion, _OcclusionStrength);
            }

            float4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                lightDatas _lightDatas;
                surfaceDatas _surfaceDatas;
                LightDataInitialization(i, _lightDatas);
                SurfaceDataInitialization(i, _surfaceDatas);

                float4 litRes = StandardLit(_lightDatas, _surfaceDatas, i.positionWS, i.shadowCoord, _EnvRotation, _Ior) + float4(_EmissionColor * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).xyz, pow(_surfaceDatas.alpha, 2)* _BaseColor.a);

 
                float4 col = litRes;

                col = pow(col, 0.4545);
                float3 c = RgbToHsv(col);
                c.z = round(c.z * _Step) / _Step;
                col = float4(HsvToRgb(c), col.a);
                col = pow(col, 2.3);
                return  col;
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