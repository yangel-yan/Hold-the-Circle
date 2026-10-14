Shader "HoldTheCircle/Lantern Bone"
{
    Properties
    {
        _BaseMap("Bone and Armor", 2D) = "white" {}
        _BumpMap("Surface Normal", 2D) = "bump" {}
        _MaskMap("Metal AO Smoothness", 2D) = "white" {}
        _EmissionMap("Carved Glow", 2D) = "black" {}
        _BaseColor("MR Surface", Color) = (0.35,0.35,0.35,1)
        _AccentColor("Rite Color", Color) = (0.6,0.8,1,1)
        _Flash("Impact", Range(0,1)) = 0
        _Threat("Windup", Range(0,1)) = 0
        _Dissolve("Death Fade", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _AccentColor;
                half _Flash, _Threat, _Dissolve;
            CBUFFER_END
            struct A
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half4 tangentWS:TEXCOORD2;
                float2 uv:TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            V Vert(A a)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(a.normalOS, a.tangentOS);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=n.normalWS;
                o.tangentWS=half4(n.tangentWS,a.tangentOS.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(a.uv,_BaseMap);
                return o;
            }
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                clip(InterleavedGradientNoise(v.positionCS.xy,0)-_Dissolve);
                half3 base = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).rgb;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,v.uv);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,v.uv));
                half3 bitangent=cross(v.normalWS,v.tangentWS.xyz)*v.tangentWS.w;
                half3 n=normalize(TransformTangentToWorld(normalTS,half3x3(v.tangentWS.xyz,bitangent,v.normalWS)));
                half3 view=GetWorldSpaceNormalizeViewDir(v.positionWS);
                half rim=pow(1-saturate(dot(n,view)),3.2);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=base*_BaseColor.rgb;
                surface.metallic=mask.r; surface.smoothness=min(mask.a,0.65);
                surface.occlusion=mask.g; surface.alpha=1;
                half3 carving=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,v.uv).rgb;
                surface.emission=surface.albedo*0.95+
                    _AccentColor.rgb*(carving*3.2+rim*(0.45+_Threat*1.3))+
                    half3(1,0.8,0.4)*_Flash*(0.3+rim);
                InputData input=(InputData)0;
                input.positionWS=v.positionWS; input.normalWS=n; input.viewDirectionWS=view;
                input.shadowCoord=float4(0,0,0,0); input.bakedGI=SampleSH(n);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
                input.shadowMask=half4(1,1,1,1);
                return UniversalFragmentPBR(input,surface);
            }
            ENDHLSL
        }
    }
}
