Shader "BitBox/Splashguard/Crane Cable Line"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.12, 0.105, 0.085, 1)
        _StripeColor ("Stripe Color", Color) = (0.34, 0.295, 0.225, 1)
        _StripeFrequency ("Stripe Frequency", Float) = 18
        _StripeWidth ("Stripe Width", Range(0.05, 0.95)) = 0.42
        _EdgeDarkness ("Edge Darkness", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _StripeColor;
                float _StripeFrequency;
                float _StripeWidth;
                float _EdgeDarkness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float diagonalBand = frac(input.uv.x * max(0.01, _StripeFrequency) + input.uv.y * 0.5);
                float stripe = smoothstep(_StripeWidth - 0.06, _StripeWidth, diagonalBand);
                half4 color = lerp(_StripeColor, _BaseColor, stripe) * input.color;

                float centerDistance = abs(input.uv.y - 0.5) * 2.0;
                float edgeShade = lerp(1.0, 1.0 - _EdgeDarkness, saturate(centerDistance));
                color.rgb *= edgeShade;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
