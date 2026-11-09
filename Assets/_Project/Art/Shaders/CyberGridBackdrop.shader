Shader "TPOB/UI/CyberGridBackdrop"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _LeftColor ("Left Accent (Cyan)", Color) = (0.0, 0.94, 1.0, 1.0)
        _RightColor ("Right Accent (Amber)", Color) = (1.0, 0.55, 0.0, 1.0)
        _BaseDarkColor ("Base Dark Color", Color) = (0.025, 0.045, 0.075, 1.0)
        _GridScale ("Grid Scale", Float) = 28.0
        _ScrollSpeed ("Scroll Speed", Float) = 0.25
        _GridIntensity ("Grid Intensity", Range(0, 1)) = 0.18
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.5)) = 0.05
        _LateralGlowPower ("Lateral Glow Spread", Range(0.1, 2.0)) = 0.85

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "CyberGridBackdrop"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _LeftColor;
                half4 _RightColor;
                half4 _BaseDarkColor;
                float _GridScale;
                float _ScrollSpeed;
                float _GridIntensity;
                float _ScanlineIntensity;
                float _LateralGlowPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _ScrollSpeed;

                // Deep dark base
                half3 baseRgb = _BaseDarkColor.rgb;

                // Dual-color lateral gradient (Cyan on left, Amber on right, matte dark in center)
                float leftSide = saturate((0.55 - uv.x) / max(0.01, _LateralGlowPower * 0.55));
                float rightSide = saturate((uv.x - 0.45) / max(0.01, _LateralGlowPower * 0.55));

                float leftPulse = 0.28 + 0.05 * sin(time * 1.8 + uv.y * 3.14);
                float rightPulse = 0.28 + 0.05 * cos(time * 1.8 - uv.y * 3.14);

                half3 leftGlow = _LeftColor.rgb * (leftSide * leftSide * leftPulse);
                half3 rightGlow = _RightColor.rgb * (rightSide * rightSide * rightPulse);
                half3 ambientGlow = leftGlow + rightGlow;

                // Animated technical blueprint perspective grid
                float2 gridUv = uv * float2(_GridScale * 1.777, _GridScale);
                gridUv.y += time * 0.4;
                float2 gridDeriv = abs(frac(gridUv - 0.5) - 0.5) / fwidth(gridUv);
                float lineMask = 1.0 - saturate(min(gridDeriv.x, gridDeriv.y));

                // Major grid lines
                float2 majorUv = gridUv * 0.25;
                float2 majorDeriv = abs(frac(majorUv - 0.5) - 0.5) / fwidth(majorUv);
                float majorMask = 1.0 - saturate(min(majorDeriv.x, majorDeriv.y));

                // Grid color matches the side glow
                half3 gridTint = lerp(_LeftColor.rgb, _RightColor.rgb, saturate(uv.x));
                float edgeGridMask = smoothstep(0.12, 0.48, abs(uv.x - 0.5)) + smoothstep(0.25, 0.5, abs(uv.y - 0.5)) * 0.4;
                float combinedGrid = (lineMask * 0.35 + majorMask * 0.85) * _GridIntensity * saturate(edgeGridMask + 0.2);

                // Subtle horizontal scanlines
                float scanline = sin(uv.y * 480.0 + time * 6.0) * 0.5 + 0.5;
                float scanMod = 1.0 - (scanline * _ScanlineIntensity);

                // Soft vignette
                float2 centered = uv - 0.5;
                float edgeVignette = 1.0 - saturate(dot(centered, centered) * 1.25);

                half3 finalRgb = (baseRgb + ambientGlow + gridTint * combinedGrid) * scanMod;
                finalRgb = lerp(finalRgb * 0.65, finalRgb, edgeVignette);

                return half4(finalRgb * input.color.rgb, input.color.a);
            }
            ENDHLSL
        }
    }
}
