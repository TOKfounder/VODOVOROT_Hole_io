Shader "Vodovorot/Edible target bracket"
{
    Properties
    {
        _Color ("Available target", Color) = (.45,1,.16,1)
        _OutlineColor ("Contrast outline", Color) = (.015,.055,.025,1)
    }
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            fixed4 _Color, _OutlineColor;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata vertex)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(vertex.vertex);
                output.uv = vertex.uv;
                return output;
            }
            fixed4 frag(v2f input):SV_Target
            {
                float2 coordinates = abs(input.uv * 2 - 1);
                float edge = max(coordinates.x, coordinates.y);
                float corner = min(coordinates.x, coordinates.y);
                float pixel = max(fwidth(edge), .005);
                float alpha = smoothstep(.70 - pixel, .70 + pixel, edge)
                    * (1 - smoothstep(.99 - pixel, .99 + pixel, edge))
                    * smoothstep(.48 - pixel, .48 + pixel, corner);
                clip(alpha - .02);
                float brightStroke = smoothstep(.78 - pixel, .78 + pixel, edge)
                    * (1 - smoothstep(.92 - pixel, .92 + pixel, edge));
                // Dark edges remain readable on pale grass; bright centres
                // remain readable on castle stone. The corner shape carries meaning.
                fixed3 color = lerp(_OutlineColor.rgb, _Color.rgb, brightStroke);
                return fixed4(color, _Color.a * alpha);
            }
            ENDCG
        }
    }
}
