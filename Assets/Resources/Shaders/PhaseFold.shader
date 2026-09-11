// PURPOSE: The sprite shader "Soğuk katlama" (PhaseFoldView) draws its plates with. Everything the
// variant needs that a tint cannot do, per renderer through a MaterialPropertyBlock:
//
//   CLIP AT THE SEAM  - every pixel past the seam line (_SeamPoint, _SeamNormal) is discarded, so a
//                       plate really goes BEHIND the board's upper layer instead of shrinking. Per
//                       renderer on purpose: a SpriteMask would show every masked sprite inside every
//                       mask, and a plate sliding sideways would reappear in the next cell's pit.
//   FLEX              - the plate's LEADING vertices are pulled toward the seam (_Lead) and pinched
//                       toward its middle (_Pinch), its trailing ones lag (_Trail): a small funnel,
//                       enough to stop the move reading as a mechanical slide. Never a page curl.
//   VOLUME            - _Flatten pulls the painted bevel and shading toward the face's own average
//                       colour (sampled across its middle), and
//                       _Desat / _Cold take the colour out and the cold in - the cube losing its
//                       volume rather than being scaled down.
//   DISSOLVE          - _Dissolve clears the sprite from its middle outward, for the negative ghost.
//
// Per-renderer properties drop these few sprites out of SRP batching, which is fine at this count;
// the block tiles on the board still share one material each. Tagged Universal2D, as the 2D
// renderer requires. If this shader is missing or unsupported, PhaseFoldView falls back to plain
// sprites and squeezes each plate into its seam instead.
Shader "ProjectBlock/PhaseFold"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SeamPoint ("Seam point (world)", Vector) = (0, 0, 0, 0)
        _SeamNormal ("Toward the seam", Vector) = (1, 0, 0, 0)
        _PlateCentre ("Plate centre (world)", Vector) = (0, 0, 0, 0)
        _PlateHalf ("Plate half-size along the normal", Float) = 0.5
        _Lead ("Leading-edge pull (world)", Float) = 0
        _Trail ("Trailing-edge lag (world)", Float) = 0
        _Pinch ("Leading-edge pinch", Range(0, 1)) = 0
        _Flatten ("Volume suppression", Range(0, 1)) = 0
        _Desat ("Desaturation", Range(0, 1)) = 0
        _Cold ("Cold influence", Range(0, 1)) = 0
        _ColdTint ("Cold tint", Color) = (0.36, 0.42, 0.5, 1)
        _Dissolve ("Centre-out dissolve", Range(0, 1)) = 0
        _Clip ("Clip at the seam", Float) = 0
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
                float2 worldXY : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SeamPoint;
                float4 _SeamNormal;
                float4 _PlateCentre;
                float _PlateHalf;
                float _Lead;
                float _Trail;
                float _Pinch;
                float _Flatten;
                float _Desat;
                float _Cold;
                float4 _ColdTint;
                float _Dissolve;
                float _Clip;
            CBUFFER_END

            float2 SeamNormal()
            {
                float2 n = _SeamNormal.xy;
                float len = max(length(n), 1e-5);
                return n / len;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                float2 n = SeamNormal();
                float2 p = float2(-n.y, n.x);
                float2 rel = world.xy - _PlateCentre.xy;
                // 0 on the trailing edge, 1 on the leading one.
                float lead = saturate(dot(rel, n) / max(_PlateHalf, 1e-4) * 0.5 + 0.5);
                world.xy += n * lerp(-_Trail, _Lead, lead);
                world.xy -= p * dot(rel, p) * _Pinch * lead;
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldXY = world.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Past the seam the board's upper layer covers the plate.
                float beyond = dot(input.worldXY - _SeamPoint.xy, SeamNormal());
                clip(_Clip > 0.5 ? -beyond : 1.0);

                float2 uv = input.uv;
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (_Flatten > 0.0001)
                {
                    // Pull the painted bevel and shading toward the face's own average colour,
                    // taken across its middle: a face plate, not a blur. (A blur a few texels wide
                    // is under a pixel on a board cell, and wider taps ghost.)
                    half3 mean = 0;
                    [unroll] for (int gy = 0; gy < 3; gy++)
                    {
                        [unroll] for (int gx = 0; gx < 3; gx++)
                        {
                            mean += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                                float2(0.3 + 0.2 * gx, 0.3 + 0.2 * gy)).rgb;
                        }
                    }
                    texel.rgb = lerp(texel.rgb, mean / 9.0, _Flatten);
                }
                half4 c = texel * input.color;
                half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx, _Desat);
                c.rgb = lerp(c.rgb, _ColdTint.rgb * (0.35 + lum), _Cold);
                if (_Dissolve > 0.0001)
                {
                    // Square, like the cube it was: a round clearing reads as a hole.
                    float2 q = abs(uv - 0.5) * 2.0;
                    float d = max(q.x, q.y);
                    c.a *= saturate((d - _Dissolve * 1.25 + 0.25) / 0.25);
                }
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
