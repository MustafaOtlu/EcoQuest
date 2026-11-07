Shader "EcoQuest/Tools/AreaOverlay"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint", Color) = (0.3, 0.9, 0.65, 0.1)
        _Feather ("Use vertex opacity", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _DepthTest ("Depth test", Float) = 8
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Overlay" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ToolAreaOverlay"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_DepthTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Feather;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half opacity : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.opacity = lerp(1.0h, input.color.a, _Feather);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return half4(_BaseColor.rgb, _BaseColor.a * input.opacity); }
            ENDHLSL
        }
    }
}
