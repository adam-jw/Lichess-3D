Shader "Custom/PieceOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.8, 0.8, 0.8, 1)
        _OutlineWidth ("Outline Width (world units)", Float) = 0.01
    }

    SubShader
    {
        // Geometry-1: draw the shell just before the piece, so the piece's own
        // opaque render z-tests over the shell's interior and only the rim shows
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry-1" }

        Pass
        {
            Name "Outline"
            Cull Front          // draw the shell's back faces; the extruded silhouette is the outline
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes input)
            {
                Varyings o;
                // Extrude in world space so a constant width reads equally thin on a
                // big queen and a small pawn, despite BoardView's per-type scaling
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 nrmWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                posWS += nrmWS * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(posWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}