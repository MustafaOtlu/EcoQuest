Shader "EcoQuest/Cloud sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.12,0.39,0.66,1)
        _Horizon ("Horizon", Color) = (0.69,0.8,0.84,1)
        _CloudColor ("Cloud", Color) = (1,1,1,1)
        _CloudCover ("Cover", Range(0,1)) = 0.2
        _Daylight ("Day", Range(0,1)) = 1
        _Wind ("Wind", Float) = 0.5
        _SunDirection ("Sun", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Zenith, _Horizon, _CloudColor;
            float4 _SunDirection;
            float _CloudCover, _Daylight, _Wind;
            CBUFFER_END
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.direction=i.positionOS.xyz; return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Cloud(float2 p) { return Noise(p)*0.57+Noise(p*2.1)*0.28+Noise(p*4.3)*0.15; }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 d=normalize(i.direction);
                float elevation=saturate(d.y);
                half3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(elevation,0.55));
                float sun=dot(d,normalize(_SunDirection.xyz));
                color+=half3(1,0.75,0.37)*(pow(saturate(sun),500)*1.4+pow(saturate(sun),12)*0.13)*_Daylight;
                float2 p=d.xz/max(0.18,d.y+0.23)*2.6+float2(_Time.y*_Wind*0.012,_Time.y*0.002);
                float field=Cloud(p);
                float cloud=smoothstep(0.76-_CloudCover*0.55,0.91-_CloudCover*0.55,field)*smoothstep(0,0.2,elevation);
                color=lerp(color,_CloudColor.rgb*(0.75+field*0.25),cloud*0.94);
<<<<<<< HEAD
                float star=step(0.997,Hash(floor(d.xz/max(0.1,abs(d.y))*210)))*(1-_Daylight)*smoothstep(0.1,0.4,elevation)*(1-cloud);
=======
                float night=1-smoothstep(0.02,0.18,_Daylight);
                float star=step(0.997,Hash(floor(d.xz/max(0.1,abs(d.y))*210)))*night*smoothstep(0.1,0.4,elevation)*(1-cloud);
>>>>>>> 745f61ed57cb575d070a9391ad0cb1c1d5851527
                color+=star*0.5;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
