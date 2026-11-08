Shader "Vodovorot/Edible target bracket"
{
    Properties { _Color ("Color", Color) = (.45,1,.16,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=abs(i.uv*2-1);
                float edge=max(p.x,p.y);
                float corners=step(.5,min(p.x,p.y));
                float stroke=step(.78,edge)*(1-step(.97,edge));
                fixed alpha=stroke*corners;
                clip(alpha-.1);
                fixed whiteEdge=step(.91,edge);
                return fixed4(lerp(_Color.rgb,fixed3(1,1,1),whiteEdge*.65),_Color.a*alpha);
            }
            ENDCG
        }
    }
}
