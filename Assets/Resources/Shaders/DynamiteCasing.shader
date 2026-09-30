// PURPOSE: The DYNAMITE block's own material - the red casing and the graphite strap that holds it,
// and everything "Barut tedarikçisi" loads into it turn by turn. Every dynamite tile in the game is
// drawn through this (ViewUtil.TileMaterial), and with no property block it is simply the refined
// look of a fresh, cold charge pack. The powder only ever arrives through a MaterialPropertyBlock
// that PowderMagazineView writes on the board's own renderers.
//
// WHAT IT REPLACES. The charge used to be a soft orange glow laid over each cube, brighter every
// turn - a grid of red lights getting more orange, where stage 3 and stage 5 differed by how much
// orange fog sat on top. Nothing about the material changed. Here the MATERIAL changes, and the
// casing stays red at every stage: what rises is pressure, heat in the seams, tension in the strap
// and soot at the junctions - never a wash over the block.
//
// THE TILE (block_dynamite, 360 px): three red sticks, two dark vertical SEAMS between them
// (u 0.325 and 0.669), a dark maroon frame round the edge, and one horizontal strap across the
// middle (v 0.408 - 0.600). Those numbers are measured off the art; if the tile is redrawn they
// are the four lines to change.
//
//   STRAP     the art's flat black band is REDRAWN as a graphite tension strap: matte black in its
//             middle, steel-charcoal at its edges, a hairline of light along its top and a small
//             contact shadow on the casing under it - a strap pressing on something rather than a
//             black rectangle. It is narrower than the art's (_StrapWidth, 0.78): the band the art
//             drew is re-filled with casing sampled from just outside it, so no red is painted.
//             Its ends round off only where the block ends (_StrapEnds says which side runs on
//             into a neighbour), and one faint tension joint sits in it per cube.
//   SEAMS     heat comes up THROUGH the seams between the sticks as broken amber hairlines
//             (_SeamHeat is the share of each seam that is lit) - never a continuous neon grid.
//   CHANNEL   a narrow ember channel runs under the strap's lower edge (_ChannelHeat), and a
//             travelling front of heat can run along it (_HeatFront, the load cycle's pulse).
//   KNOT      one warm pressure knot where a seam meets the strap (_Knot).
//   SOOT      dark powder deposits gather at the strap-casing junctions (_SootAmount).
//   PRESSURE  _InternalPressure warms the casing near its seams; _CasingWarmth warms the whole
//             casing a few per cent; _Swell grows or compresses the whole cube about its centre
//             by a percent or two (the pressure beats, the cook-off's compression) - in the vertex
//             stage, so no transform the board owns is ever touched.
//   HAZE      _LocalHeatDistortion bends the casing by under a pixel at the top stages.
//
// Stage 0 (no block) is also where the base casing is refined: a deeper, lacquered red, a lighter
// upper edge and a burgundy lower one - never a plastic sweet.
//
// Tagged Universal2D. Without it the tile draws on the ordinary sprite material and the magazine's
// primers and straps still carry the stage.
Shader "ProjectBlock/DynamiteCasing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Dynamite tile", 2D) = "white" {}
        _StrapWidth ("Strap height as a share of the art's", Float) = 0.78
        _StrapEnds ("Strap runs on: left, right (1 = into a neighbour)", Vector) = (0, 0, 0, 0)
        _StrapTension ("Strap tension 0..1", Float) = 0
        _ChannelHeat ("Ember channel under the strap", Float) = 0
        _HeatFront ("Travelling strap heat: from u, to u, strength, -", Vector) = (0, 0, 0, 0)
        _SeamHeat ("Share of each seam lit", Float) = 0
        _InternalPressure ("Internal pressure", Float) = 0
        _CasingWarmth ("Casing warmth", Float) = 0
        _SootAmount ("Powder soot", Float) = 0
        _Knot ("Pressure knot: u, v, strength, -", Vector) = (0.325, 0.4, 0, 0)
        _LocalHeatDistortion ("Heat haze, in uv", Float) = 0
        _Swell ("Pressure swell (negative compresses)", Float) = 0
        _Seed ("Per-cube seed", Float) = 0
        _Flash ("Cook-off heat over the whole casing", Float) = 0
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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _StrapWidth;
                float4 _StrapEnds;
                float _StrapTension;
                float _ChannelHeat;
                float4 _HeatFront;
                float _SeamHeat;
                float _InternalPressure;
                float _CasingWarmth;
                float _SootAmount;
                float4 _Knot;
                float _LocalHeatDistortion;
                float _Swell;
                float _Seed;
                float _Flash;
            CBUFFER_END

            // The art's geometry, measured off block_dynamite.png (see the header).
            static const float BandLow = 0.408;
            static const float BandHigh = 0.600;
            static const float SeamA = 0.325;
            static const float SeamB = 0.669;
            static const float SeamHalf = 0.021;
            static const float FrameIn = 0.036;

            static const float3 Amber = float3(1.0, 0.62, 0.24);
            static const float3 Ember = float3(0.62, 0.22, 0.08);
            static const float3 SootInk = float3(0.10, 0.06, 0.05);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS;
                // The swell is about the sprite's own pivot, which is the cube's centre.
                pos.xy *= 1.0 + _Swell;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(pos));
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float Hash(float n)
            {
                return frac(sin(n * 12.9898 + 4.1414) * 43758.5453);
            }

            float Hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float bandMid = (BandLow + BandHigh) * 0.5;
                float bandHalf = (BandHigh - BandLow) * 0.5;
                // The strap narrows under tension - a hair, it is holding, not crushing.
                float strapHalf = bandHalf * saturate(_StrapWidth) * (1.0 - 0.03 * saturate(_StrapTension));
                float n0 = bandMid - strapHalf;
                float n1 = bandMid + strapHalf;

                // ---- where the strap ends: only where the block does ----
                float cap = 0.028;
                float leftOpen = step(0.5, _StrapEnds.x);
                float rightOpen = step(0.5, _StrapEnds.y);
                float endL = lerp(FrameIn * 0.5, -1.0, leftOpen);
                float endR = lerp(1.0 - FrameIn * 0.5, 2.0, rightOpen);
                // A rounded-rectangle mask for the strap body.
                float2 rc = float2((endL + endR) * 0.5, bandMid);
                float2 rh = float2((endR - endL) * 0.5, strapHalf);
                float2 q = abs(uv - rc) - rh + cap;
                float strapSd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - cap;
                float inStrap = 1.0 - smoothstep(-0.004, 0.004, strapSd);

                // ---- the casing sample: the art's band is re-filled from just outside it ----
                float2 cuv = uv;
                if (uv.y >= BandLow && uv.y <= BandHigh)
                {
                    cuv.y = uv.y < bandMid
                        ? BandLow - 0.006 - (uv.y - BandLow)
                        : BandHigh + 0.006 + (BandHigh - uv.y);
                }
                // A little heat haze on the casing at the top stages - under a pixel.
                float hz = _LocalHeatDistortion;
                if (hz > 0.0)
                {
                    float t = _Time.y;
                    cuv += hz * float2(sin(uv.y * 38.0 + t * 7.0 + _Seed * 3.0),
                        sin(uv.x * 33.0 - t * 5.5 + _Seed));
                }
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, cuv);
                if (tex.a < 0.004 && inStrap < 0.01)
                {
                    return half4(0, 0, 0, 0);
                }
                float3 c = tex.rgb;
                float redness = saturate((c.r - max(c.g, c.b)) * 3.0);

                // ---- the casing, refined: deeper lacquered red, lit top, burgundy bottom ----
                float3 casing = c * float3(0.9, 0.8, 0.84);
                casing *= lerp(0.88, 1.07, saturate(uv.y));
                float lacquer = smoothstep(0.86, 0.93, uv.y) * (1.0 - smoothstep(0.95, 0.975, uv.y));
                casing = lerp(casing, casing * 1.18 + 0.02, lacquer * redness * 0.6);
                c = lerp(c, casing, redness);

                // ---- warmth: a few per cent, and more near the seams under pressure ----
                float seamDist = min(abs(uv.x - SeamA), abs(uv.x - SeamB));
                float nearSeam = 1.0 - smoothstep(SeamHalf, SeamHalf + 0.09, seamDist);
                float warm = _CasingWarmth + _InternalPressure * 0.35 * nearSeam + _Flash;
                c = lerp(c, c * float3(1.12, 1.06, 0.95) + float3(0.03, 0.012, 0.0), saturate(warm) * redness);

                // ---- the strap's contact shadow on the casing below it ----
                float under = smoothstep(n0 - 0.045, n0, uv.y) * (1.0 - step(n0, uv.y));
                c *= 1.0 - (0.32 + 0.12 * saturate(_StrapTension)) * under * (1.0 - inStrap);

                // ---- SEAM HEAT: broken amber hairlines, never a continuous line ----
                if (_SeamHeat > 0.0)
                {
                    float lineW = 0.010;
                    float inSeamA = 1.0 - smoothstep(lineW * 0.4, lineW, abs(uv.x - SeamA));
                    float inSeamB = 1.0 - smoothstep(lineW * 0.4, lineW, abs(uv.x - SeamB));
                    float segA = step(Hash(floor(uv.y * 13.0) + _Seed * 7.0 + 1.0), _SeamHeat);
                    float segB = step(Hash(floor(uv.y * 13.0) + _Seed * 7.0 + 31.0), _SeamHeat);
                    float inside = step(FrameIn, uv.y) * step(uv.y, 1.0 - FrameIn);
                    float seam = max(inSeamA * segA, inSeamB * segB) * inside * (1.0 - inStrap);
                    // Hotter at a segment's middle than its ends, so each reads as a crack glowing.
                    float mid = 1.0 - abs(frac(uv.y * 13.0) - 0.5) * 1.6;
                    c = lerp(c, lerp(Ember, Amber, saturate(mid)), saturate(seam * (0.55 + 0.45 * mid)));
                }

                // ---- the PRESSURE KNOT where a seam meets the strap ----
                if (_Knot.z > 0.0)
                {
                    float kd = length((uv - _Knot.xy) * float2(1.0, 1.6));
                    float knot = (1.0 - smoothstep(0.0, 0.07, kd)) * _Knot.z;
                    c = lerp(c, lerp(Ember, Amber, knot), saturate(knot * 0.7) * (1.0 - inStrap));
                }

                // ---- SOOT at the strap-casing junctions ----
                if (_SootAmount > 0.0)
                {
                    float2 cell = floor(uv * 26.0);
                    float speck = Hash2(cell + _Seed * 13.0);
                    float near = (smoothstep(n0 - 0.09, n0 - 0.01, uv.y) * (1.0 - step(n0, uv.y)))
                        + (smoothstep(n1 + 0.08, n1 + 0.01, uv.y) * step(n1, uv.y));
                    near = max(near, (1.0 - smoothstep(0.0, 0.05, seamDist)) * 0.5);
                    float soot = step(1.0 - saturate(_SootAmount) * 0.45, speck) * saturate(near);
                    float2 inCell = frac(uv * 26.0) - 0.5;
                    soot *= 1.0 - smoothstep(0.18, 0.45, length(inCell));
                    c = lerp(c, SootInk, soot * 0.55 * (1.0 - inStrap));
                }

                // ---- THE STRAP: graphite, pressing on the casing ----
                if (inStrap > 0.0)
                {
                    float sy = saturate((uv.y - n0) / max(n1 - n0, 1e-4));
                    float edge = 1.0 - smoothstep(0.0, 0.32, min(sy, 1.0 - sy));
                    float3 strap = lerp(float3(0.075, 0.08, 0.09), float3(0.23, 0.24, 0.27), edge);
                    // The light is upper-left: a hairline along its top, darker along its bottom.
                    strap += float3(0.13, 0.13, 0.14) * smoothstep(0.84, 0.96, sy) * (1.0 - smoothstep(0.96, 1.0, sy));
                    strap *= 1.0 - 0.25 * (1.0 - smoothstep(0.0, 0.2, sy));
                    // Tension darkens it a little: it is doing more work.
                    strap *= 1.0 - 0.12 * saturate(_StrapTension);
                    // One faint tension joint per cube, never a belt of rivets.
                    float jointU = 0.2 + 0.6 * Hash(_Seed * 5.0 + 3.0);
                    float joint = 1.0 - smoothstep(0.003, 0.008, abs(uv.x - jointU));
                    strap = lerp(strap, strap * 1.45 + 0.02, joint * 0.5);
                    // THE EMBER CHANNEL along its lower inner edge - broken, and faint.
                    float cz = (sy - 0.2) / 0.07;
                    float chan = exp(-cz * cz);
                    float broken = step(0.35, Hash(floor(uv.x * 9.0) + _Seed * 11.0 + 5.0));
                    float heat = _ChannelHeat * chan * broken;
                    // The travelling front of the load cycle's heat.
                    if (_HeatFront.z > 0.0)
                    {
                        float lo = min(_HeatFront.x, _HeatFront.y);
                        float hi = max(_HeatFront.x, _HeatFront.y);
                        float along = smoothstep(lo - 0.08, lo, uv.x) * (1.0 - smoothstep(hi, hi + 0.08, uv.x));
                        float fz = (sy - 0.5) / 0.28;
                        heat += _HeatFront.z * along * exp(-fz * fz);
                    }
                    strap = lerp(strap, Amber * 0.9, saturate(heat));
                    c = lerp(c, strap, inStrap);
                }

                float alpha = max(tex.a, inStrap * step(FrameIn * 0.5, uv.x) * step(uv.x, 1.0 - FrameIn * 0.5));
                return half4(saturate(c), alpha) * input.color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
