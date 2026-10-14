Shader "HoldTheCircle/Lantern Shade"
{
    Properties
    {
        _BaseMap("Painted Surface", 2D) = "white" {}
        _BaseColor("MR Surface", Color) = (0.35,0.35,0.35,1)
        _AccentColor("Lantern Accent", Color) = (0.45,0.8,1,1)
        _Flash("Hit", Range(0,1)) = 0
        _Threat("Attack Windup", Range(0,1)) = 0
        _Sway("Cloth Sway", Float) = 0.015
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "LanternSurface"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _AccentColor;
                half _Flash;
                half _Threat;
                float _Sway;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float weight = saturate(-input.positionOS.y + 0.15);
                input.positionOS.x += sin(_Time.y * 2.0 + input.positionOS.y * 7.0) * _Sway * weight;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS) * (frontFace ? 1 : -1);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                Light key = GetMainLight();
                half wrap = saturate(dot(normal, key.direction) * 0.5 + 0.5);
                // A soft fill preserves painted detail in rooms without virtual scene lights.
                half3 lighting = half3(1.55,1.62,1.8) + key.color * wrap * 1.2;
                half rim = pow(1 - saturate(abs(dot(normal, view))), 3.5);
                half warmth = saturate((painted.r - painted.b) * 3.0) * saturate(painted.r - 0.3);
                half3 glow = _AccentColor.rgb * (rim * (0.28 + _Threat * 0.9) + warmth * 0.22);
                half3 hit = half3(1.0,0.78,0.4) * _Flash * (0.2 + rim);
                return half4(painted * _BaseColor.rgb * lighting + glow + hit, 1);
            }
            ENDHLSL
        }
    }
}
