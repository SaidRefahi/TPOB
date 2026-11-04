Shader "TPOB/Player/XRaySilhouette"
{
    Properties
    {
        [MainColor] _BaseColor ("Silhouette Color", Color) = (0.0, 0.9, 1.0, 0.85)
        _RimPower ("Rim Exponent", Range(0.5, 8.0)) = 3.0
        _FillAlpha ("Inner Fill Alpha", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "XRayPass"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Back
            Offset -5, -5

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RimPower;
                half _FillAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                #if UNITY_REVERSED_Z
                output.positionCS.z += 0.003;
                #else
                output.positionCS.z -= 0.003;
                #endif

                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                float NdotV = saturate(dot(normalWS, viewDirWS));
                float rim = 1.0 - NdotV;
                float rimFactor = pow(rim, _RimPower);

                float alpha = lerp(_FillAlpha, 1.0, rimFactor) * _BaseColor.a;
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
