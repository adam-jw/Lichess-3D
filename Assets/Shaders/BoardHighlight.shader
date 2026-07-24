Shader "Custom/BoardHighlight"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Shape     ("Shape 0Fill 1Dot 2Ring 3Radial", Float) = 0
        _Radius    ("Radius", Range(0,1.4)) = 0.5
        _Softness  ("Edge Softness", Range(0.001,0.5)) = 0.05
        _Thickness ("Ring Thickness", Range(0.01,0.7)) = 0.15
        _Inner ("Radial Core Radius", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _BaseColor;
            float _Shape, _Radius, _Softness, _Thickness, _Inner;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // r: 0 at center, 1 at an edge midpoint, ~1.414 at a corner
                float r = length((i.uv - 0.5) * 2.0);
                float a = _BaseColor.a;

                if (_Shape > 3.5)             // Corners: empty circle in the center,
                    a *= smoothstep(_Radius - _Softness, _Radius + _Softness, r);  // filled to the edges
                else if (_Shape > 2.5)        // Radial
                    a *= 1.0 - smoothstep(_Inner, max(_Radius, _Inner + 0.001), r);
                else if (_Shape > 1.5)        // Ring
                {
                    float inner = _Radius - _Thickness;
                    a *= smoothstep(inner - _Softness, inner + _Softness, r)
                       * (1.0 - smoothstep(_Radius - _Softness, _Radius + _Softness, r));
                }
                else if (_Shape > 0.5)        // Dot
                    a *= 1.0 - smoothstep(_Radius - _Softness, _Radius + _Softness, r);
                // else Fill

                return fixed4(_BaseColor.rgb, a);
            }
            ENDCG
        }
    }
}