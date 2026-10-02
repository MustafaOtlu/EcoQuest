Shader "LowPolyWater/WaterShaded"
{
    Properties
    {
        _BaseColor ("Base color", Color) = (0.54, 0.95, 0.99, 0.5)
        _SpecColor ("Specular Material Color", Color) = (1, 1, 1, 1)
        _Shininess ("Shininess", Float) = 10
        _ShoreTex ("Shore & Foam texture", 2D) = "black" {}
        _InvFadeParemeter ("Auto blend parameter (Edge, Shore, Distance scale)", Vector) = (0.2, 0.39, 0.5, 1)
        _BumpTiling ("Foam Tiling", Vector) = (1, 1, -2, 3)
        _BumpDirection ("Foam movement", Vector) = (1, 1, -1, 1)
        _Foam ("Foam (intensity, cutoff)", Vector) = (0.1, 0.375, 0, 0)
        [Toggle] _isInnerAlphaBlendOrColor ("Fade inner to color or alpha?", Float) = 0
        [Toggle(WATER_EDGEBLEND_ON)] _UseShoreDepth ("Depth shoreline (requires camera Depth Texture)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _ WATER_EDGEBLEND_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_ShoreTex);
            SAMPLER(sampler_ShoreTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SpecColor;
                float4 _InvFadeParemeter;
                float4 _BumpTiling;
                float4 _BumpDirection;
                float4 _Foam;
                float _Shininess;
                float _isInnerAlphaBlendOrColor;
                float _UseShoreDepth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 foamUV : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Preserve world-space tiling: the supplied material has a zero texture scale.
                output.foamUV = (positions.positionWS.xzxz + _Time.x * _BumpDirection) * _BumpTiling * 2;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();
                half diffuse = saturate(dot(normal, sun.direction));
                half specular = pow(saturate(dot(reflect(-sun.direction, normal), view)), max(_Shininess, 1));
                half3 ambient = max(SampleSH(normal), half3(0.08, 0.08, 0.08));
                half3 color = _BaseColor.rgb * (ambient + sun.color * diffuse);
                color += _SpecColor.rgb * sun.color * specular * diffuse;
                half edge = 1;
                half shore = 0;
                #if defined(WATER_EDGEBLEND_ON)
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float rawDepth = SampleSceneDepth(screenUV);
                    float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float waterDepth = -TransformWorldToView(input.positionWS).z;
                    float gap = max(0, sceneDepth - waterDepth);
                    edge = saturate(gap * _InvFadeParemeter.x);
                    shore = 1 - saturate(gap * _InvFadeParemeter.y);
                #endif
                half3 foam = SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, input.foamUV.xy).rgb
                    * SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, input.foamUV.zw).rgb;
                color += max(foam - 0.125, 0) * _Foam.x * shore;
                half alpha = _BaseColor.a;
                if (_isInnerAlphaBlendOrColor > 0.5) alpha *= edge;
                else color += 1 - edge;
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
