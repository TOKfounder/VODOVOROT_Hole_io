Shader "Vodovorot/Map surround"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Pattern ("Paving detail", Range(0,1)) = 0
        _TileSize ("Tile size in metres", Float) = 1.2
        _VertexColors ("Use vertex palette", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Pattern, _TileSize, _VertexColors;
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 world:TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float light = .78 + .22 * saturate(dot(n, normalize(float3(-.4,1,-.3))));
                o.color = lerp(fixed4(1,1,1,1),v.color,_VertexColors) * _Color * fixed4(light,light,light,1);
                o.world = mul(unity_ObjectToWorld, v.vertex).xz;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 tile = i.world / max(.1, _TileSize);
                tile.x += fmod(floor(tile.y), 2.0) * .5;
                float2 edge = abs(frac(tile) - .5);
                float grout = smoothstep(.46,.49,max(edge.x,edge.y));
                float variation = frac(sin(dot(floor(tile),float2(12.9898,78.233))) * 43758.5453);
                float shade = lerp(1.0, .92 + variation * .12 - grout * .16, _Pattern);
                return fixed4(i.color.rgb * shade, 1);
            }
            ENDCG
        }
    }
}
