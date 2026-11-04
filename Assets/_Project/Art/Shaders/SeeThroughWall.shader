Shader "TPOB/Environment/SeeThroughWall"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.3
        _SpecularColor ("Specular Color", Color) = (0.2, 0.2, 0.2, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _SpecularColor;
            half _Smoothness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Global properties passed by WallTransparencyCoordinator
        float4 _Player1Pos;      // xyz = world pos, w = radius
        float4 _Player2Pos;      // xyz = world pos, w = radius
        float4 _CutoutParams;    // x = softness, y = minAlpha

        float CalculateRayOcclusionFactor(float3 positionWS, float4 playerPos, float softness)
        {
            if (playerPos.w <= 0.05) return 1.0;

            float3 cameraPos = _WorldSpaceCameraPos;
            float3 V = playerPos.xyz - cameraPos;
            float lenSq = max(0.001, dot(V, V));

            // Scalar projection t of wall point onto sightline vector
            float t = dot(positionWS - cameraPos, V) / lenSq;

            // If wall is behind player (t > 1.05) or behind/at camera (t < 0.02), it does not occlude
            if (t > 1.05 || t < 0.02)
            {
                return 1.0;
            }

            // Closest point on line segment camera -> player
            float3 Q = cameraPos + clamp(t, 0.0, 1.0) * V;
            float dist = length(positionWS - Q);

            // Radius can expand slightly towards the player for a comfortable viewing cone
            float radius = playerPos.w * lerp(0.85, 1.0, saturate(t));
            return smoothstep(radius - softness, radius, dist);
        }

        void ApplyProximityDitherCutout(float3 positionWS, float4 positionCS)
        {
            float softness = max(0.05, _CutoutParams.x);
            float minAlpha = clamp(_CutoutParams.y, 0.0, 1.0);

            float f1 = CalculateRayOcclusionFactor(positionWS, _Player1Pos, softness);
            float f2 = CalculateRayOcclusionFactor(positionWS, _Player2Pos, softness);

            float mask = min(f1, f2);
            float targetAlpha = lerp(minAlpha, 1.0, mask);

            // Screen-space 4x4 Bayer Matrix
            int2 screenPos = int2(positionCS.xy);
            int ditherIndex = (screenPos.x % 4) + (screenPos.y % 4) * 4;
            static const float bayer4x4[16] = {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };
            float threshold = bayer4x4[ditherIndex];

            clip(targetAlpha - threshold - 0.0001);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float fogFactor     : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyProximityDitherCutout(input.positionWS, input.positionCS);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);

                // Lighting
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 directLight = mainLight.color * (NdotL * mainLight.shadowAttenuation);
                half3 ambientLight = SampleSH(normalWS);

                half3 finalColor = albedo.rgb * (directLight + ambientLight);
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
            };

            float3 _LightDirection;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.positionWS = positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyProximityDitherCutout(input.positionWS, input.positionCS);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyProximityDitherCutout(input.positionWS, input.positionCS);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
