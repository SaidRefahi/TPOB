Shader "TPOB/Environment/SeeThroughWall"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.3
        _SpecularColor ("Specular Color", Color) = (0.2, 0.2, 0.2, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
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
            half4 _EmissionColor;
            half _Smoothness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Global properties passed by WallTransparencyCoordinator
        float4 _Player1Pos;      // xyz = world pos, w = radius
        float4 _Player2Pos;      // xyz = world pos, w = radius
        float4 _CutoutParams;    // x = softness, y = minAlpha, z = ceilingRadiusMult, w = coneTaper
        float4 _PlayerFloorY;    // x = player1MinY, y = player2MinY, z = backWallMargin, w = floorProtectionMargin

        float CalculateRayOcclusionFactor(
            float3 positionWS,
            float3 normalWS,
            float4 playerPos,
            float playerMinY,
            float softness,
            float ceilingMult,
            float coneTaper,
            float backWallMargin)
        {
            if (playerPos.w <= 0.05) return 1.0;

            float horizontalness = saturate((abs(normalWS.y) - 0.45) / 0.35);

            // Never cut out the floor the player is currently standing on
            if (horizontalness > 0.4 && positionWS.y <= playerMinY)
            {
                return 1.0;
            }

            // A surface acts as a ceiling only if it is horizontal and above the player's center
            float isAbove = step(playerPos.y + 0.15, positionWS.y);
            float ceilingFactor = horizontalness * isAbove;

            float3 cameraPos = _WorldSpaceCameraPos;
            float3 V = playerPos.xyz - cameraPos;
            float lenSq = max(0.001, dot(V, V));

            // Horizontal (XZ) depth check along camera -> player direction:
            // Prevents walls behind the robot from becoming transparent
            float2 vXZ = playerPos.xz - cameraPos.xz;
            float lenXZ = length(vXZ);
            float2 dirXZ = lenXZ > 0.001 ? (vXZ / lenXZ) : float2(0.0, 1.0);
            float behindDist = dot(positionWS.xz - playerPos.xz, dirXZ);

            float allowedBehind = lerp(backWallMargin, max(backWallMargin, 0.65), ceilingFactor);
            if (behindDist >= allowedBehind)
            {
                return 1.0;
            }
            float behindFade = smoothstep(allowedBehind - 0.25, allowedBehind, behindDist);

            // Scalar projection t of surface point onto sightline vector
            float t = dot(positionWS - cameraPos, V) / lenSq;
            float maxT = lerp(1.0, 1.15, ceilingFactor);
            if (t > maxT || t < 0.02)
            {
                return 1.0;
            }

            // Closest point on line segment camera -> player
            float3 Q = cameraPos + clamp(t, 0.0, 1.0) * V;
            float dist = length(positionWS - Q);

            float effectiveRadius = playerPos.w * lerp(1.0, max(0.5, ceilingMult), ceilingFactor);
            float radius = effectiveRadius * lerp(coneTaper, 1.0, saturate(t));
            float radialFactor = smoothstep(radius - softness, radius, dist);

            return max(radialFactor, behindFade);
        }

        void ApplyProximityDitherCutout(float3 positionWS, float3 normalWS, float4 positionCS)
        {
            float softness = max(0.05, _CutoutParams.x);
            float minAlpha = clamp(_CutoutParams.y, 0.0, 1.0);
            float ceilingMult = _CutoutParams.z > 0.01 ? _CutoutParams.z : 1.35;
            float coneTaper = _CutoutParams.w > 0.01 ? _CutoutParams.w : 0.85;
            float backWallMargin = _PlayerFloorY.z;

            float f1 = CalculateRayOcclusionFactor(positionWS, normalWS, _Player1Pos, _PlayerFloorY.x, softness, ceilingMult, coneTaper, backWallMargin);
            float f2 = CalculateRayOcclusionFactor(positionWS, normalWS, _Player2Pos, _PlayerFloorY.y, softness, ceilingMult, coneTaper, backWallMargin);

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
                float3 normalWS = normalize(input.normalWS);
                ApplyProximityDitherCutout(input.positionWS, normalWS, input.positionCS);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Lighting
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 directLight = mainLight.color * (NdotL * mainLight.shadowAttenuation);
                half3 ambientLight = SampleSH(normalWS);

                half3 emission = _EmissionColor.rgb * _EmissionColor.a;
                half3 finalColor = albedo.rgb * (directLight + ambientLight) + emission;
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
                float3 normalWS     : TEXCOORD1;
            };

            float3 _LightDirection;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyProximityDitherCutout(input.positionWS, normalize(input.normalWS), input.positionCS);
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
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyProximityDitherCutout(input.positionWS, normalize(input.normalWS), input.positionCS);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
