// PURPOSE: "Tutuştur" - a fire cube BURNING OUT, not fading: it heats, then chars from its edges and
// a few irregular patches inward, with a thin ember band on the burning front, until the last heat
// is a small hot core and then charcoal.
//
// THE MASK IS BAKED, THE THRESHOLD IS ANIMATED. _BurnMask holds, per texel, WHEN that texel chars:
// low values early. It is value noise pulled low toward the cube's edges (IgnitionShapes bakes it),
// so the char eats in from the outline and in irregular patches at once and the centre goes last -
// heat leaving the block from the outside in. It is sampled in the sprite's OBJECT space (one unit is
// the cube body, pivot in the middle), never the sprite UV, which on a packed tile is a sub-rect.
// _MaskRot turns it a quarter at a time so neighbouring cubes do not char in the same shape.
//
// _Heat  0..1: saturation and brightness up, the middle toward pale yellow-orange, edges toward a
//        darker red.
// _Char  0..1: how far the burn has gone.
//
// Without this shader the view still burns the cube - its tint goes to charcoal - with less detail.
Shader "ProjectBlock/IgnitionBurn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BurnMask ("Burn mask (R)", 2D) = "gray" {}
        _Heat ("Heat", Range(0, 1)) = 0
        _Char ("Char", Range(0, 1)) = 0
        _MaskRot ("Mask rotation (cos, sin)", Vector) = (1, 0, 0, 0)
        _Charcoal ("Charcoal", Color) = (0.10, 0.07, 0.06, 1)
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
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                float2 local : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BurnMask);
            SAMPLER(sampler_BurnMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Heat;
                float _Char;
                float4 _MaskRot;
                float4 _Charcoal;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.local = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color * _Color;
                float2 p = input.local;
                float2 q = float2(p.x * _MaskRot.x - p.y * _MaskRot.y, p.x * _MaskRot.y + p.y * _MaskRot.x);
                float m = SAMPLE_TEXTURE2D(_BurnMask, sampler_BurnMask, saturate(q + 0.5)).r;

                float centre = saturate(1.0 - length(p) / 0.5);
                float lum = dot(tex.rgb, float3(0.2126, 0.7152, 0.0722));
                float3 sat = tex.rgb + (tex.rgb - lum) * 0.12 * _Heat;
                float3 hot = sat * (1.0 + 0.15 * _Heat) + float3(0.22, 0.11, 0.0) * pow(centre, 1.5) * _Heat;
                float edgeDark = saturate(1.0 - centre * 1.6) * _Heat * 0.25;
                hot = lerp(hot, float3(0.45, 0.08, 0.02), edgeDark);

                float t = _Char * 1.08;
                float charred = saturate((t - m) / 0.04);
                float front = saturate(1.0 - abs(m - t) / 0.07) * step(0.0001, _Char) * (1.0 - charred);
                float3 col = lerp(hot, float3(1.0, 0.55, 0.15), front * 0.8);
                col = lerp(col, _Charcoal.rgb + lum * 0.18, charred);
                return half4(saturate(col), tex.a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
