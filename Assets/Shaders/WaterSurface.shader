Shader "Toymageddon/Water Surface"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.30588236, 0.5137255, 0.6627451, 0.85)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82
        _Metallic ("Metallic", Range(0, 1)) = 0
        [NoScaleOffset] _FlowMap ("Flow Map", 2D) = "black" {}
        [NoScaleOffset] _DerivHeightMap ("Deriv Height Map", 2D) = "gray" {}
        _WaveA ("Wave A", Vector) = (1, 0.1, 0.28, 12)
        _WaveB ("Wave B", Vector) = (0.8, 0.6, 0.22, 8)
        _WaveC ("Wave C", Vector) = (-0.6, 0.8, 0.18, 18)
        _WaveD ("Wave D", Vector) = (0.2, -1, 0.12, 5)
        _Tiling ("Tiling", Float) = 3
        _Speed ("Speed", Float) = 0.5
        _FlowStrength ("Flow Strength", Float) = 0.1
        _FlowOffset ("Flow Offset", Float) = 0
        _UJump ("U Jump", Float) = 0.24
        _VJump ("V Jump", Float) = 0.2083333
        _HeightScale ("Height Scale", Float) = 0.1
        _HeightScaleModulated ("Height Scale Modulated", Float) = 0.9
        _SimulationTime ("Simulation Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float4 _WaveD;
                float _Smoothness;
                float _Metallic;
                float _Tiling;
                float _Speed;
                float _FlowStrength;
                float _FlowOffset;
                float _UJump;
                float _VJump;
                float _HeightScale;
                float _HeightScaleModulated;
                float _SimulationTime;
            CBUFFER_END

            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);
            TEXTURE2D(_DerivHeightMap);
            SAMPLER(sampler_DerivHeightMap);

            static const float Gravity = 9.8;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 GerstnerWave(float4 wave, float3 gridPoint, float time, inout float3 tangent, inout float3 bitangent)
            {
                float2 direction = normalize(wave.xy);
                float steepness = saturate(wave.z);
                float wavelength = max(0.1, wave.w);
                float waveNumber = TWO_PI / wavelength;
                float phaseSpeed = sqrt(Gravity / waveNumber);
                float phase = waveNumber * (dot(direction, gridPoint.xz) - (phaseSpeed * time));
                float amplitude = steepness / waveNumber;
                float sine = sin(phase);
                float cosine = cos(phase);

                tangent += float3(
                    -direction.x * direction.x * (steepness * sine),
                    direction.x * (steepness * cosine),
                    -direction.x * direction.y * (steepness * sine)
                );

                bitangent += float3(
                    -direction.x * direction.y * (steepness * sine),
                    direction.y * (steepness * cosine),
                    -direction.y * direction.y * (steepness * sine)
                );

                return float3(
                    direction.x * (amplitude * cosine),
                    amplitude * sine,
                    direction.y * (amplitude * cosine)
                );
            }

            float3 FlowUVW(float2 uv, float2 flowVector, float2 jump, float flowOffset, float tiling, float time, float phaseOffset)
            {
                float progress = frac(time + phaseOffset);
                float3 uvw;
                uvw.xy = uv - flowVector * (progress + flowOffset);
                uvw.xy *= tiling;
                uvw.xy += phaseOffset;
                uvw.xy += (time - progress) * jump;
                uvw.z = 1.0 - abs(1.0 - (2.0 * progress));
                return uvw;
            }

            float3 UnpackDerivativeHeight(float4 packedData)
            {
                float3 derivativeHeight = packedData.agb;
                derivativeHeight.xy = derivativeHeight.xy * 2.0 - 1.0;
                return derivativeHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 gridPoint = input.positionOS.xyz;
                float3 tangentOS = float3(1.0, 0.0, 0.0);
                float3 bitangentOS = float3(0.0, 0.0, 1.0);
                float3 positionOS = gridPoint;

                positionOS += GerstnerWave(_WaveA, gridPoint, _SimulationTime, tangentOS, bitangentOS);
                positionOS += GerstnerWave(_WaveB, gridPoint, _SimulationTime, tangentOS, bitangentOS);
                positionOS += GerstnerWave(_WaveC, gridPoint, _SimulationTime, tangentOS, bitangentOS);
                positionOS += GerstnerWave(_WaveD, gridPoint, _SimulationTime, tangentOS, bitangentOS);

                float3 normalOS = normalize(cross(bitangentOS, tangentOS));
                float3 tangentWorld = normalize(TransformObjectToWorldDir(tangentOS));
                float3 bitangentWorld = normalize(TransformObjectToWorldDir(bitangentOS));
                float3 normalWorld = normalize(TransformObjectToWorldNormal(normalOS));

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalWorld;
                output.tangentWS = tangentWorld;
                output.bitangentWS = bitangentWorld;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 flowSample = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, input.uv);
                float2 flowVector = (flowSample.rg * 2.0 - 1.0) * _FlowStrength;
                float flowSpeed = flowSample.b * abs(_FlowStrength);
                float time = (_SimulationTime * _Speed) + flowSample.a;
                float2 jump = float2(_UJump, _VJump);

                float3 uvwA = FlowUVW(input.uv, flowVector, jump, _FlowOffset, _Tiling, time, 0.0);
                float3 uvwB = FlowUVW(input.uv, flowVector, jump, _FlowOffset, _Tiling, time, 0.5);

                float finalHeightScale = _HeightScale + (flowSpeed * _HeightScaleModulated);
                float3 derivHeightA = UnpackDerivativeHeight(
                    SAMPLE_TEXTURE2D(_DerivHeightMap, sampler_DerivHeightMap, uvwA.xy)
                ) * (uvwA.z * finalHeightScale);
                float3 derivHeightB = UnpackDerivativeHeight(
                    SAMPLE_TEXTURE2D(_DerivHeightMap, sampler_DerivHeightMap, uvwB.xy)
                ) * (uvwB.z * finalHeightScale);

                float3 macroNormal = normalize(input.normalWS);
                float3 tangentWorld = normalize(input.tangentWS - macroNormal * dot(input.tangentWS, macroNormal));
                float3 bitangentWorld = normalize(input.bitangentWS - macroNormal * dot(input.bitangentWS, macroNormal));
                float3 detailNormalTS = normalize(float3(
                    -(derivHeightA.x + derivHeightB.x),
                    -(derivHeightA.y + derivHeightB.y),
                    1.0
                ));
                float3 detailNormalWS = normalize(
                    tangentWorld * detailNormalTS.x +
                    bitangentWorld * detailNormalTS.y +
                    macroNormal * detailNormalTS.z
                );

                float crestMask = saturate((derivHeightA.z + derivHeightB.z) * 0.5);
                float3 baseTint = lerp(_BaseColor.rgb * 0.72, _BaseColor.rgb * 1.15 + 0.05, crestMask);

                float3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();

                float NdotL = saturate(dot(detailNormalWS, mainLight.direction));
                float3 ambient = SampleSH(detailNormalWS) * baseTint;
                float3 diffuse = baseTint * mainLight.color * (NdotL * mainLight.distanceAttenuation);

                float3 halfVector = SafeNormalize(mainLight.direction + viewDirection);
                float specularExponent = exp2(4.0 + (_Smoothness * 6.0));
                float specularFactor = pow(saturate(dot(detailNormalWS, halfVector)), specularExponent) * (_Smoothness * 2.0);
                float3 specularColor = lerp(float3(0.04, 0.04, 0.04), baseTint, _Metallic);
                float3 specular = specularColor * specularFactor * mainLight.color;

                float fresnel = pow(1.0 - saturate(dot(detailNormalWS, viewDirection)), 5.0);
                float3 finalColor = ambient + diffuse + specular + (fresnel * 0.25);
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
