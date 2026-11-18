// GardenWise - leaves, grass and flower petals for URP
// alpha cut-out, both sides, main light + shadows + ambient + fog, gentle sway in the wind
// Same properties as GardenWise/Foliage (built-in version)
Shader "GardenWise/FoliageURP"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (RGB), cut-out (A)", 2D) = "white" {}
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.4
        _WindStrength ("Wind strength (m)", Range(0, 0.3)) = 0.06
        _WindSpeed ("Wind speed", Range(0, 5)) = 1.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff;
            float _WindStrength;
            float _WindSpeed;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        // world position moved by the wind, more at the top than at the base
        float3 WindPosition(float3 positionOS)
        {
            float3 world = TransformObjectToWorld(positionOS);
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float height = saturate((world.y - origin.y) / 3.0);
            float phase = _Time.y * _WindSpeed + world.x * 0.35 + world.z * 0.25;
            return world + float3(sin(phase), 0, cos(phase * 0.8)) * _WindStrength * height;
        }

        half4 SampleAlbedo(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
            clip(color.a - _Cutoff);
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert (Attributes input)
            {
                Varyings output;
                output.positionWS = WindPosition(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag (Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half4 albedo = SampleAlbedo(input.uv);

                // the back of a leaf is lit like its front
                float3 normal = normalize(input.normalWS) * (frontFace ? 1 : -1);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normal, mainLight.direction)) * 0.75 + 0.25;
                half3 light = mainLight.color * diffuse * mainLight.shadowAttenuation + SampleSH(normal);

                half3 color = albedo.rgb * light;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert (Attributes input)
            {
                Varyings output;
                float3 positionWS = WindPosition(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag (Varyings input) : SV_Target
            {
                SampleAlbedo(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(WindPosition(input.positionOS.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag (Varyings input) : SV_Target
            {
                SampleAlbedo(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // used by the ambient occlusion (SSAO)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(WindPosition(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag (Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                SampleAlbedo(input.uv);
                float3 normal = normalize(input.normalWS) * (frontFace ? 1 : -1);
                return half4(normal, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
