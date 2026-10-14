Shader "HoldTheCircle/Rite Ink"
{
    Properties
    {
        _MainTex ("Etched Art", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _UvRect ("Atlas Rectangle", Vector) = (0,0,1,1)
        _Fill ("Fill", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            UNITY_INSTANCING_BUFFER_START(Rite)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UvRect)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fill)
            UNITY_INSTANCING_BUFFER_END(Rite)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float fill = UNITY_ACCESS_INSTANCED_PROP(Rite, _Fill);
                clip(fill - input.uv.x - 0.00001);
                float4 rect = UNITY_ACCESS_INSTANCED_PROP(Rite, _UvRect);
                half4 ink = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, rect.xy + input.uv * rect.zw);
                return ink * input.color * UNITY_ACCESS_INSTANCED_PROP(Rite, _Tint);
            }
            ENDHLSL
        }
    }
}
