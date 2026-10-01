// PURPOSE: "Simetri"'s light - one material for every trace, beam, flare, ring and spark the
// symmetry payout draws (SymmetryRewardView). Vertex colour times the texture (the sprite's own for
// a SpriteRenderer, plain white for the strip meshes, whose soft edges are written into their
// vertex alphas), PREMULTIPLIED, with how additive it is as one number:
//
//   _Additive 0  ordinary alpha blending  (out = src + dst * (1 - a))
//   _Additive 1  pure light               (out = src + dst)
//
// The brief asks for light that SITS ON the board without swallowing it: the cubes under a trace
// keep their own colour, only brighter, so the payout says "this region was recognised" and never
// "this region was painted". So the light is mostly additive (0.85) - a pale cyan line over a gold
// cube reads as light on gold, not as a cyan sticker - with a breath of alpha kept so it still
// shows over the brightest tiles. Nothing ever reaches white: the colours are capped where they
// are chosen, not here.
Shader "ProjectBlock/SymmetryGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Additive ("Additive", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Additive;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                half a = saturate(c.a);
                return half4(c.rgb * a, a * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
}
