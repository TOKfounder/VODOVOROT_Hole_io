Shader "Vodovorot/Cutaway dither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [PerRendererData] _CutawayAlpha ("Visibility", Range(0,1)) = 1
        _Unlit ("Unlit source", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100
        CGPROGRAM
        #pragma surface surf Lambert noforwardadd
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        float _CutawayAlpha, _Unlit;
        struct Input { float2 uv_MainTex; float4 screenPos; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            float2 p = fmod(floor(IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy), 4.0);
            float4 row = p.y < 1 ? float4(0,8,2,10) : p.y < 2 ? float4(12,4,14,6) :
                p.y < 3 ? float4(3,11,1,9) : float4(15,7,13,5);
            float threshold = p.x < 1 ? row.x : p.x < 2 ? row.y : p.x < 3 ? row.z : row.w;
            clip(_CutawayAlpha - (threshold + .5) / 16.0);
            fixed3 color = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
            o.Albedo = color * (1 - _Unlit);
            o.Emission = color * _Unlit;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
