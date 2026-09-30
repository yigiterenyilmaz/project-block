// PURPOSE: The DYNAMITE block's own material - the red casing and the graphite strap that holds it,
// and everything "Barut tedarikçisi" loads into it turn by turn. Every dynamite tile in the game is
// drawn through this (ViewUtil.TileMaterial), and with no property block it is simply the refined
// look of a fresh, cold charge pack. The powder only ever arrives through a MaterialPropertyBlock
// that PowderMagazineView writes on the board's own renderers.
//
// TWO PASSES ARE BURIED HERE. The first charge was a soft orange glow laid over each cube - a grid
// of red lights getting more orange. The second went the other way and failed the other way: it
// told the charge through STRESS alone - crisp broken hairlines in the seams (which read as CRACKS),
// a heat haze that wobbled the whole cube including its frame (which read as the block TREMBLING),
// and chambers too small to see - so stage 1 and stage 5 were the same red block, one of them
// shaking. The accumulation has to be told with LIGHT that has a SOURCE:
//
//   POCKETS   the core of it. Up to five soft, irregular INTERNAL AMBER POCKETS (_PocketA.._PocketE,
//             in this cube's uv: centre, radius, strength), one per loaded chamber, placed by the
//             magazine in the casing beside its chamber and summed as a field - so they stay apart
//             at stage 3, start to touch at 4 and are one connected fill at 5, without anything
//             being coded for "connect". The light comes from UNDER the red: the casing is pulled
//             toward a burnt amber through its own shading, strongest in a pocket's middle, and
//             never on the strap. The block is split across its cubes; a pocket spills over the
//             cube boundary into its neighbour and simply stops where the block does, because only
//             the block's cubes are drawn through this.
//   FILL      _Fill is a broad, low, noisy warmth across the whole casing at the top stages - some
//             red sections lit from inside, never a wash.
//   UNDERGLOW the strap's lower edge leaks heat in a broken 1-2 px band (_Underglow), strongest
//             where the pocket field reaches the strap, and a faint warm reflection sits inside the
//             strap's own bottom edge. The strap itself stays graphite.
//   SEAMS     heat SOFTLY in the seams between the sticks (_SeamHeat), brighter where a pocket is -
//             a light leak, never a crisp hairline.
//   CREASE    at the top stages one short pressure crease per cube, dark with a warm lip - a fold,
//             never a crack.
//   HAZE      _LocalHeatDistortion bends the casing by under a pixel ONLY inside the pocket field and
//             never near the frame, slowly: heat, not a tremble.
//   PRESSURE  _Squash (x, y) scales the cube about its own centre in the vertex stage for the rare,
//             controlled pressure beats (a percent, never a shake).
//
// THE TILE (block_dynamite, 360 px): three red sticks, two dark vertical seams between them
// (u 0.325 and 0.669), a dark maroon frame, and one strap across the middle (v 0.408 - 0.600).
// Measured off the art; redraw the tile and those four lines are what change.
//
// _OldLook draws the PREVIOUS pass (whole-cube haze, crisp hairlines, no pockets, no underglow) for
// the animation lab's before/after only; _Raw draws the untouched art.
//
// Tagged Universal2D. Without it the tile draws on the ordinary sprite material and the magazine's
// chambers still carry the stage.
Shader "ProjectBlock/DynamiteCasing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Dynamite tile", 2D) = "white" {}
        _StrapWidth ("Strap height as a share of the art's", Float) = 0.78
        _StrapEnds ("Strap runs on: left, right (1 = into a neighbour)", Vector) = (0, 0, 0, 0)
        _StrapTension ("Strap tension 0..1", Float) = 0
        _ChannelHeat ("Ember channel inside the strap", Float) = 0
        _HeatFront ("Travelling strap heat: from u, to u, strength, -", Vector) = (0, 0, 0, 0)
        _PocketA ("Amber pocket: uv centre, radius, strength", Vector) = (0, 0, 0, 0)
        _PocketB ("Amber pocket", Vector) = (0, 0, 0, 0)
        _PocketC ("Amber pocket", Vector) = (0, 0, 0, 0)
        _PocketD ("Amber pocket", Vector) = (0, 0, 0, 0)
        _PocketE ("Amber pocket", Vector) = (0, 0, 0, 0)
        _PocketLight ("How much of the pocket field is drawn as light", Float) = 1
        _Fill ("Broad internal fill", Float) = 0
        _Embers ("Tiny ember specks inside the pockets", Float) = 0
        _Underglow ("Heat leaking under the strap", Float) = 0
        _SeamHeat ("Soft heat in the seams", Float) = 0
        _CasingWarmth ("Casing warmth", Float) = 0
        _SootAmount ("Powder soot", Float) = 0
        _Crease ("Pressure crease", Float) = 0
        _LocalHeatDistortion ("Heat haze inside the pockets, in uv", Float) = 0
        _Squash ("Pressure scale about the centre: x, y", Vector) = (1, 1, 0, 0)
        _Seed ("Per-cube seed", Float) = 0
        _Flash ("Cook-off heat over the whole casing", Float) = 0
        _OldLook ("The previous pass (the lab's before/after)", Float) = 0
        _Raw ("Draw the untouched art", Float) = 0
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
                float4 _PocketA;
                float4 _PocketB;
                float4 _PocketC;
                float4 _PocketD;
                float4 _PocketE;
                float _PocketLight;
                float _Fill;
                float _Embers;
                float _Underglow;
                float _SeamHeat;
                float _CasingWarmth;
                float _SootAmount;
                float _Crease;
                float _LocalHeatDistortion;
                float4 _Squash;
                float _Seed;
                float _Flash;
                float _OldLook;
                float _Raw;
            CBUFFER_END

            // The art's geometry, measured off block_dynamite.png (see the header).
            static const float BandLow = 0.408;
            static const float BandHigh = 0.600;
            static const float SeamA = 0.325;
            static const float SeamB = 0.669;
            static const float SeamHalf = 0.021;
            static const float FrameIn = 0.036;

            static const float3 Amber = float3(1.0, 0.62, 0.24);
            static const float3 BurntAmber = float3(0.96, 0.46, 0.14);
            static const float3 Ember = float3(0.62, 0.22, 0.08);
            static const float3 SootInk = float3(0.13, 0.08, 0.06);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS;
                // About the sprite's own pivot, the cube's centre - a percent at most.
                float2 squash = _Squash.xy;
                if (squash.x == 0.0 && squash.y == 0.0)
                {
                    squash = float2(1.0, 1.0);
                }
                pos.xy *= squash;
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

            // One pocket's contribution: an IRREGULAR soft round - its radius wanders with the
            // angle, so no pocket is a disc - falling off smoothly to zero at its own edge.
            float Pocket(float2 uv, float4 p, float salt)
            {
                if (p.w <= 0.0 || p.z <= 0.0)
                {
                    return 0.0;
                }
                float2 d = uv - p.xy;
                float a = atan2(d.y, d.x);
                float r = p.z * (1.0 + 0.2 * sin(a * 3.0 + salt * 2.1) + 0.1 * sin(a * 5.0 - salt * 1.3));
                float k = saturate(1.0 - length(d) / max(r, 1e-4));
                return k * k * (3.0 - 2.0 * k) * p.w;
            }

            float Field(float2 uv)
            {
                return Pocket(uv, _PocketA, 1.0) + Pocket(uv, _PocketB, 2.0)
                    + Pocket(uv, _PocketC, 3.0) + Pocket(uv, _PocketD, 4.0)
                    + Pocket(uv, _PocketE, 5.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                if (_Raw > 0.5)
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                }
                bool old = _OldLook > 0.5;
                float bandMid = (BandLow + BandHigh) * 0.5;
                float bandHalf = (BandHigh - BandLow) * 0.5;
                float strapHalf = bandHalf * saturate(_StrapWidth) * (1.0 - 0.03 * saturate(_StrapTension));
                float n0 = bandMid - strapHalf;
                float n1 = bandMid + strapHalf;

                // ---- where the strap ends: only where the block does ----
                float cap = 0.028;
                float leftOpen = step(0.5, _StrapEnds.x);
                float rightOpen = step(0.5, _StrapEnds.y);
                float endL = lerp(FrameIn * 0.5, -1.0, leftOpen);
                float endR = lerp(1.0 - FrameIn * 0.5, 2.0, rightOpen);
                float2 rc = float2((endL + endR) * 0.5, bandMid);
                float2 rh = float2((endR - endL) * 0.5, strapHalf);
                float2 q = abs(uv - rc) - rh + cap;
                float strapSd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - cap;
                float inStrap = 1.0 - smoothstep(-0.004, 0.004, strapSd);

                // ---- the pocket field: where the powder is ----
                float field = old ? 0.0 : Field(uv);

                // ---- the casing sample: the art's band is re-filled from just outside it ----
                float2 cuv = uv;
                if (uv.y >= BandLow && uv.y <= BandHigh)
                {
                    cuv.y = uv.y < bandMid
                        ? BandLow - 0.006 - (uv.y - BandLow)
                        : BandHigh + 0.006 + (BandHigh - uv.y);
                }
                float hz = _LocalHeatDistortion;
                if (hz > 0.0)
                {
                    float t = _Time.y;
                    if (old)
                    {
                        // THE PREVIOUS PASS: the whole cube wobbling, frame and all - kept only so
                        // the lab can show what this replaced.
                        cuv += hz * float2(sin(uv.y * 38.0 + t * 7.0 + _Seed * 3.0),
                            sin(uv.x * 33.0 - t * 5.5 + _Seed));
                    }
                    else
                    {
                        // Only inside the heat, never near the frame, and slow: heat, not tremble.
                        float interior = smoothstep(0.05, 0.12, uv.x) * smoothstep(0.05, 0.12, 1.0 - uv.x)
                            * smoothstep(0.05, 0.12, uv.y) * smoothstep(0.05, 0.12, 1.0 - uv.y);
                        float mask = saturate(field * 4.0) * interior * (1.0 - inStrap);
                        cuv += hz * mask * float2(sin(uv.y * 21.0 + t * 1.6 + _Seed * 3.0),
                            sin(uv.x * 19.0 - t * 1.3 + _Seed));
                    }
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

                float seamDist = min(abs(uv.x - SeamA), abs(uv.x - SeamB));
                float warm = _CasingWarmth + _Flash;
                c = lerp(c, c * float3(1.12, 1.06, 0.95) + float3(0.03, 0.012, 0.0), saturate(warm) * redness);

                // ---- THE POCKETS: amber light coming up from UNDER the red ----
                if (!old)
                {
                    float fillNoise = 0.55 + 0.45 * sin(uv.x * 6.3 + _Seed * 2.0) * sin(uv.y * 5.1 - _Seed);
                    // Pockets that overlap SATURATE rather than add: two meeting at stage 4-5 make
                    // one connected fill, not a doubled hot spot, and the red always survives
                    // under it (the mix is capped) - red stays the dominant colour at every stage.
                    float soft = 1.0 - exp(-field * 2.5);
                    float heat = saturate(soft * 1.2 + _Fill * fillNoise) * saturate(_PocketLight);
                    // The red warms toward vermilion first and only its heart reaches amber - the
                    // shading of the stick survives, so it reads as lit from inside, not painted.
                    float3 lit = lerp(c * float3(1.35, 1.05, 0.8), BurntAmber * (0.55 + 0.6 * dot(c, float3(0.5, 0.4, 0.1))),
                        saturate(heat * 1.2));
                    c = lerp(c, lit, min(saturate(heat * 1.4), 0.82) * redness);
                    // A few tiny embers where the powder is - still, so even a paused frame
                    // shows that something inside is alight.
                    if (_Embers > 0.0 && field > 0.01)
                    {
                        float2 ecell = floor(uv * 34.0);
                        float2 ein = frac(uv * 34.0) - 0.5;
                        float pick = step(Hash2(ecell + _Seed * 17.0), _Embers);
                        float dotK = 1.0 - smoothstep(0.08, 0.26, length(ein));
                        c = lerp(c, Amber * 1.08, saturate(pick * dotK * saturate(field * 6.0) * redness));
                    }
                }

                // ---- the strap's contact shadow on the casing below it ----
                float under = smoothstep(n0 - 0.045, n0, uv.y) * (1.0 - step(n0, uv.y));
                c *= 1.0 - (0.32 + 0.12 * saturate(_StrapTension)) * under * (1.0 - inStrap);

                // ---- UNDERGLOW: heat leaking out under the strap's lower edge ----
                float strapField = old ? 0.0 : Field(float2(uv.x, bandMid));
                if (_Underglow > 0.0 && !old)
                {
                    float leakBand = smoothstep(n0 - 0.026, n0 - 0.008, uv.y) * (1.0 - smoothstep(n0 - 0.004, n0, uv.y));
                    float broken = saturate(0.5 + 0.5 * sin(uv.x * 23.0 + _Seed * 5.0) + 0.35 * sin(uv.x * 51.0 - _Seed * 2.0));
                    float leak = leakBand * broken * _Underglow * (0.35 + 2.2 * strapField) * (1.0 - inStrap);
                    c = lerp(c, Amber, saturate(leak * 2.4));
                }

                // ---- SEAMS: soft heat leaking up the seams, brighter where the powder is ----
                if (_SeamHeat > 0.0)
                {
                    float inside = step(FrameIn, uv.y) * step(uv.y, 1.0 - FrameIn) * (1.0 - inStrap);
                    if (old)
                    {
                        // The previous pass's crisp broken hairlines - which read as cracks.
                        float lineW = 0.010;
                        float inSeamA = 1.0 - smoothstep(lineW * 0.4, lineW, abs(uv.x - SeamA));
                        float inSeamB = 1.0 - smoothstep(lineW * 0.4, lineW, abs(uv.x - SeamB));
                        float segA = step(Hash(floor(uv.y * 13.0) + _Seed * 7.0 + 1.0), _SeamHeat * 5.0);
                        float segB = step(Hash(floor(uv.y * 13.0) + _Seed * 7.0 + 31.0), _SeamHeat * 5.0);
                        c = lerp(c, Amber, saturate(max(inSeamA * segA, inSeamB * segB) * inside));
                    }
                    else
                    {
                        float glow = exp(-(seamDist * seamDist) / (0.018 * 0.018));
                        float drift = 0.6 + 0.4 * sin(uv.y * 9.0 + _Seed * 3.0);
                        float seam = glow * drift * inside * _SeamHeat * (1.0 + 3.0 * saturate(field * 3.0));
                        c = lerp(c, lerp(Ember, Amber, saturate(seam * 3.0)), saturate(seam * 2.5));
                    }
                }

                // ---- a PRESSURE CREASE: a fold, dark with a warm lip - never a crack ----
                if (_Crease > 0.0 && !old)
                {
                    float cu = 0.08 + 0.2 * Hash(_Seed * 3.0 + 7.0) + (Hash(_Seed + 2.0) < 0.5 ? 0.0 : 0.36);
                    float cv = n1 + 0.1 + 0.18 * Hash(_Seed * 5.0 + 1.0);
                    float along = smoothstep(cu, cu + 0.04, uv.x) * (1.0 - smoothstep(cu + 0.13, cu + 0.18, uv.x));
                    float across = uv.y - cv - 0.02 * sin((uv.x - cu) * 20.0);
                    float core = exp(-(across * across) / (0.005 * 0.005));
                    float lip = exp(-((across + 0.009) * (across + 0.009)) / (0.006 * 0.006));
                    float crease = along * _Crease * redness;
                    c = lerp(c, c * 0.55, core * crease * 0.8);
                    c = lerp(c, Amber, lip * crease * 0.35);
                }

                // ---- SOOT: a little dark powder at the junctions ----
                if (_SootAmount > 0.0)
                {
                    float2 cell = floor(uv * 26.0);
                    float speck = Hash2(cell + _Seed * 13.0);
                    float near = (smoothstep(n0 - 0.09, n0 - 0.01, uv.y) * (1.0 - step(n0, uv.y)))
                        + (smoothstep(n1 + 0.08, n1 + 0.01, uv.y) * step(n1, uv.y));
                    near = max(near, (1.0 - smoothstep(0.0, 0.05, seamDist)) * 0.5);
                    float soot = step(1.0 - saturate(_SootAmount) * 0.4, speck) * saturate(near);
                    float2 inCell = frac(uv * 26.0) - 0.5;
                    soot *= 1.0 - smoothstep(0.16, 0.42, length(inCell));
                    c = lerp(c, SootInk, soot * 0.5 * (1.0 - inStrap));
                }

                // ---- THE STRAP: graphite, pressing on the casing ----
                if (inStrap > 0.0)
                {
                    float sy = saturate((uv.y - n0) / max(n1 - n0, 1e-4));
                    float edge = 1.0 - smoothstep(0.0, 0.32, min(sy, 1.0 - sy));
                    float3 strap = lerp(float3(0.075, 0.08, 0.09), float3(0.23, 0.24, 0.27), edge);
                    // A steel hairline along its top, darker along its bottom.
                    strap += float3(0.15, 0.15, 0.16) * smoothstep(0.84, 0.96, sy) * (1.0 - smoothstep(0.96, 1.0, sy));
                    strap *= 1.0 - 0.25 * (1.0 - smoothstep(0.0, 0.2, sy));
                    strap *= 1.0 - 0.12 * saturate(_StrapTension);
                    float jointU = 0.2 + 0.6 * Hash(_Seed * 5.0 + 3.0);
                    float joint = 1.0 - smoothstep(0.003, 0.008, abs(uv.x - jointU));
                    strap = lerp(strap, strap * 1.45 + 0.02, joint * 0.5);
                    // A faint warm reflection inside its own bottom edge, where the heat is.
                    float reflect = (1.0 - smoothstep(0.0, 0.16, sy)) * _Underglow * (0.3 + 2.0 * strapField);
                    // The ember channel along its lower inner edge - broken, and faint.
                    float cz = (sy - 0.2) / 0.07;
                    float chan = exp(-cz * cz);
                    float broken = step(0.35, Hash(floor(uv.x * 9.0) + _Seed * 11.0 + 5.0));
                    float heat = _ChannelHeat * chan * broken * (0.5 + 1.5 * strapField) + reflect * 0.6;
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
