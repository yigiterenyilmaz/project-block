// PURPOSE: "İstilacı"'s marked column - the corridor it will come and empty in three turns.
//
// A LANE, NOT A COLOURED SET OF CELLS. Everything is evaluated in COLUMN space: one coordinate
// across the lane and one along it. That is what makes the whole column read as a single system
// rather than a stack of tinted squares - the rails run its full length, the scan travels it end to
// end, and the heat gathers wherever it likes without restarting at a cell border.
//
// IT HAS AN INSIDE. A corridor that is only a tint and two lines is a selected column, not a
// threat. So the lane carries its own working parts: density pockets that drift, a survey scan that
// sweeps it with a tail behind, motes pulled steadily UPWARD through it, rails that are segmented
// and unevenly hot rather than uniform glow lines, and caps at both ends that say this is plugged
// into something. All of it slow and low-contrast - it sits on screen for turns at a time.
//
// THREE STATES, NOT THREE COLOURS. The countdown is carried by how the lane BEHAVES: rails tighten
// and grow uneven, the scan bites harder, motes multiply, the field breathes, and only at the last
// turn does anything snap. A player who never reads a number should still feel the deadline arrive.
//
// IT IS UNDER THE BLOCKS. Blocks are placed here while it is marked, so occupancy comes in through
// a texture and the field collapses wherever a cube stands - the threat survives around the block,
// on the rails and between the slots, and never paints over it.
//
// THE END IS AN EXTRACTION, NOT AN EXPLOSION. The sweep is a single narrow band that travels the
// lane once. What it passes is taken, not blown apart, which is the whole difference between this
// boss and the dynamite. Everything above already points that way: the motes go up, the scan goes
// up, the rails squeeze - the corridor is visibly preparing the thing it eventually does.

Shader "ProjectBlock/InvaderColumn"
{
    Properties
    {
        _MainTex ("Occupancy", 2D) = "black" {}
        _DeepColor ("Deep", Color) = (0.17, 0.075, 0.035, 1)
        _FieldColor ("Field", Color) = (0.44, 0.18, 0.065, 1)
        _HeatColor ("Heat", Color) = (0.86, 0.42, 0.12, 1)
        _RailColor ("Rail", Color) = (1, 0.72, 0.30, 1)
        _CriticalColor ("Critical", Color) = (1, 0.93, 0.74, 1)
        _MoteColor ("Mote", Color) = (1, 0.66, 0.28, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "InvaderColumn"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _DeepColor;
            float4 _FieldColor;
            float4 _HeatColor;
            float4 _RailColor;
            float4 _CriticalColor;
            float4 _MoteColor;

            // The quad's footprint in CELLS: x is across the lane, y along it.
            float2 _SizeCells;
            float _NestTime;

            // 0 at the calmest state, 1 at critical. Everything below leans on it.
            float _Threat;

            float _FieldOpacity;
            float _DepthContrast;
            float _Saturation;
            float _OccupiedOpacity;
            float _OccupiedEdge;
            float _OccupiedScan;
            float _HeatStrength;
            float _HeatScale;
            float _HazeStrength;

            float _ScanStrength;
            float _ScanSpeed;
            float _ScanWidth;
            float _ScanUpwardBias;

            float _RailWidth;
            float _RailGlow;
            float _RailInset;
            float _RailPulse;
            float _RailSegment;
            float _RailSegmentActivity;
            float _RailHotspot;

            float _MoteDensity;
            float _MoteSpeed;
            float _MoteSize;
            float _MoteOpacity;

            float _CapStrength;
            float _TickStrength;
            float _TickPitch;
            float _SparkDensity;
            float _SparkGlow;

            float _EdgeAA;
            float _CellPhase;
            float _GridStrength;

            // Extraction: how far along the lane the band has travelled, and how strong it is.
            float _SweepAt;
            float _SweepWidth;
            float _SweepGlow;
            float _SweepActive;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(127.31, 311.7));
                p += dot(p, p + 34.53);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y) * 2.0 - 1.0;
            }

            // One survey band travelling UP the lane. Asymmetric on purpose: a tight leading edge
            // with a long tail below it, so the direction is legible from a single frame - the lane
            // keeps a moment of what it has just read.
            float ScanBand(float y, float pos, float w, float bias)
            {
                float d = y - pos;
                d = d - floor(d);                      // 0 at the band, -> 1 just behind it
                float ahead = d;
                float behind = 1.0 - d;
                float wa = w * (1.0 - bias * 0.45);
                float wb = w * (1.0 + bias * 2.1);
                return exp(-(ahead * ahead) / (wa * wa))
                    + exp(-(behind * behind) / (wb * wb));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 P = IN.uv * _SizeCells;        // x across the lane, y along it
                float across = IN.uv.x;               // 0..1 wall to wall
                float along = IN.uv.y;
                float alongCells = P.y;

                // The lane itself, with a real anti-aliased edge rather than a hard cut.
                float dx = 0.5 - abs(across - 0.5);            // 0 at the wall, 0.5 in the middle
                float aa = max(fwidth(across), 1e-5) * _EdgeAA;
                float inLane = smoothstep(0.0, aa, dx);
                if (inLane <= 0.002)
                {
                    return half4(0, 0, 0, 0);
                }

                float occupied = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).r;
                float th = _Threat;

                // ---- heat haze. It warps only what lives INSIDE the corridor - this draws under
                // the blocks, so smearing them was never on the table, and the field is the only
                // thing that should look hot enough to bend.
                float2 hz = float2(
                    vnoise(float2(across * 3.1, alongCells * 0.55 - _NestTime * 0.20)),
                    vnoise(float2(across * 2.4 + 11.3, alongCells * 0.42 - _NestTime * 0.15)));
                float2 warp = hz * _HazeStrength * (0.30 + 0.70 * th);
                float2 Pw = P + warp;
                float alongW = along + warp.y / max(_SizeCells.y, 1e-4);

                // ---- the field's own life. A slow density band first, so the corridor is not
                // equally busy everywhere and the eye has somewhere to rest.
                float pocket = vnoise(float2(across * 1.3,
                    Pw.y * 0.33 - _NestTime * 0.045)) * 0.5 + 0.5;

                float heat =
                    vnoise(float2(Pw.x * _HeatScale * 2.6,
                        Pw.y * _HeatScale - _NestTime * 0.10)) * 0.62
                    + vnoise(float2(Pw.x * _HeatScale * 5.4 + 5.0,
                        Pw.y * _HeatScale * 2.3 - _NestTime * 0.19)) * 0.38;
                heat = saturate(heat * 0.5 + 0.5);
                heat = pow(heat, 2.0 - th * 0.8) * _HeatStrength * (0.55 + 0.75 * pocket);

                // ---- the survey. Two bands at different speeds so the loop never ticks like a
                // metronome, which is what makes a scan tiring to sit next to.
                float scan =
                    ScanBand(alongW, _NestTime * _ScanSpeed * (0.75 + th * 0.55),
                        _ScanWidth, _ScanUpwardBias)
                    + ScanBand(alongW, _NestTime * _ScanSpeed * 0.43 + 0.37,
                        _ScanWidth * 2.1, _ScanUpwardBias * 0.6) * 0.45;
                scan *= _ScanStrength * (0.55 + 0.75 * th);

                // ---- motes drawn UP the lane. Three sparse scrolling ranks; each mote is
                // stretched along its own travel, and they fade in at the intake and out at the
                // node so nothing pops into being at an end.
                float motes = 0.0;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float fi = (float)i;
                    float2 q = float2(across * 2.0 + fi * 3.7,
                        Pw.y * 0.85 + fi * 5.3
                            - _NestTime * _MoteSpeed * (0.8 + 0.35 * fi) * (0.75 + 0.5 * th));
                    float2 id = floor(q);
                    float2 f = q - id;
                    float h = hash21(id + fi * 17.1);
                    float live = step(1.0 - _MoteDensity * (0.55 + 0.7 * th), h);
                    float2 c = float2(0.2 + 0.6 * frac(h * 57.0), 0.2 + 0.6 * frac(h * 31.0));
                    float2 dd = (f - c) * float2(1.0, 0.42);
                    float s = _MoteSize * (0.7 + 0.6 * frac(h * 91.0));
                    motes += exp(-dot(dd, dd) / (s * s)) * live * (0.6 + 0.4 * frac(h * 13.0));
                }
                motes *= smoothstep(0.0, 0.10, along) * smoothstep(1.0, 0.86, along)
                    * _MoteOpacity;

                // ---- the ladder: measuring marks at a fixed pitch, sitting ON the rails and
                // reaching a little way in. Faint on their own - what makes them matter is that the
                // scan LIGHTS THEM UP as it passes, so the corridor reads as a machine taking
                // readings rather than as smoke drifting in a box. The middle of the lane stays
                // clear, because that is where a block has to be read.
                float tickY = frac(alongCells * _TickPitch) - 0.5;
                float tick = exp(-(tickY * tickY) / 0.0045);
                tick *= smoothstep(0.015, 0.05, dx) * (1.0 - smoothstep(0.11, 0.21, dx));
                tick *= _TickStrength
                    * (0.15 + 2.6 * saturate(scan / max(_ScanStrength, 1e-4)))
                    * (0.7 + 0.5 * th);

                // ---- rails. Two of them, just inside each wall, running the LENGTH of the lane -
                // which is what makes the column one corridor instead of a stack of cells.
                float side = step(0.5, across);
                float toWall = abs(across - 0.5) - (0.5 - _RailInset);
                float railBody = exp(-(toWall * toWall) / (_RailWidth * _RailWidth));

                // Segmented, so the rail reads as built rather than drawn, and the segments tighten
                // as the deadline closes. The two sides are offset - mirrored twins look printed.
                float seg = lerp(1.0,
                    0.62 + 0.38 * sin((alongCells + side * 0.5) * _RailSegment
                        * (1.0 + th * 0.7) * 6.2831853),
                    _RailSegmentActivity);
                // Hotspots crawling up the rail: brief concentrations, never the same two places.
                float hot = pow(saturate(vnoise(float2(side * 9.7,
                    alongCells * 0.75 - _NestTime * (0.28 + th * 0.22))) * 0.5 + 0.5), 5.0)
                    * _RailHotspot * (0.5 + 0.9 * th);
                // And a slow unevenness over the whole length, so no stretch matches another.
                float vary = 0.72 + 0.28 * (vnoise(float2(side * 3.3 + 2.1,
                    alongCells * 0.33 - _NestTime * 0.05)) * 0.5 + 0.5);
                float railPulse = 1.0 + _RailPulse * th * sin(_NestTime * (2.4 + th * 3.6));
                float rail = saturate(railBody
                    * (seg * vary * railPulse * (0.5 + 0.5 * th) + hot));

                // ---- the ends. A capture node above, an intake below: the corridor goes
                // somewhere. Kept to a thickening of what is already there rather than a device.
                float capTop = smoothstep(0.16, 0.0, 1.0 - along);
                float capBot = smoothstep(0.13, 0.0, along);
                float capPulse = 0.75 + 0.25 * sin(_NestTime * 1.6 + capBot * 2.0);
                float cap = (capTop + capBot * 0.62) * _CapStrength * capPulse * (0.6 + 0.6 * th);
                float capRail = cap * exp(-(toWall * toWall) / (_RailWidth * _RailWidth * 2.2));
                float capField = cap * 0.35 * smoothstep(0.0, 0.5, dx);

                // ---- micro-sparks, and only when the deadline is on top of the player. A handful
                // of very short snaps at the rails: tension, not a firework.
                float window = floor(_NestTime * 2.2);
                float ph = frac(_NestTime * 2.2);
                float slot = floor(alongCells * 1.5);
                float sh = hash21(float2(slot * 1.7 + side * 31.3, window));
                float fires = step(1.0 - _SparkDensity * th * th, sh);
                float when = frac(sh * 91.0) * 0.7;
                float sd2 = ph - when;
                float sparkY = frac(alongCells * 1.5) - (0.25 + frac(sh * 57.0) * 0.5);
                float spark = exp(-(sd2 * sd2) / 0.0009) * fires
                    * exp(-(toWall * toWall) / 0.0026) * exp(-(sparkY * sparkY) / 0.004);

                // ---- the board's own grid, read back through the field so placement stays legible.
                float2 g = frac(P - _CellPhase);
                float2 e = min(g, 1.0 - g);
                float w = 0.10;
                float sx = 1.0 - smoothstep(0.0, w, e.x);
                float sy = 1.0 - smoothstep(0.0, w, e.y);
                float grid = 1.0 - (1.0 - sx) * (1.0 - sy);
                float edgeNess = saturate(1.0 - min(e.x, e.y) * 2.0);
                edgeNess = edgeNess * edgeNess;

                // ---- extraction. One narrow band, once, travelling the lane.
                float sweep = 0.0;
                if (_SweepActive > 0.5)
                {
                    float sd = IN.uv.y - _SweepAt;
                    sweep = exp(-(sd * sd) / (_SweepWidth * _SweepWidth)) * _SweepGlow;
                    // Behind the band the lane is already empty and goes quiet at once - the point
                    // of an extraction is that what it passes is GONE.
                    float behind = smoothstep(_SweepAt + _SweepWidth, _SweepAt, IN.uv.y);
                    occupied = lerp(occupied, 0.0, behind);
                    heat *= lerp(1.0, 0.25, behind);
                    motes *= lerp(1.0, 0.15, behind);
                }

                // A cube standing here takes the field down to almost nothing in the middle of its
                // cell and leaves it at the rim, so the block is never competed with. The rails sit
                // in the gutter BETWEEN blocks, so they survive nearly intact and carry the threat
                // on their own once the column has been built in.
                float occlude = lerp(1.0, lerp(_OccupiedOpacity, _OccupiedEdge, edgeNess),
                    occupied);
                float occScan = lerp(1.0, _OccupiedScan, occupied);
                rail *= lerp(1.0, 0.85, occupied);

                // The seam is the BOARD'S own, so the corridor THINS over it rather than
                // lighting it - a lit grid turns the lane straight back into a stack of boxes. The
                // rails, the caps and the ladder are the corridor's own hardware and run through
                // unbroken, which is what keeps the column reading as one system.
                float body = (_FieldOpacity * (0.62 + 0.38 * th) + heat * 0.55) * occlude;
                float field = (body + scan * 0.70 * occScan + motes * occScan + tick * occScan)
                    * (1.0 - grid * _GridStrength);
                float alpha = saturate((field
                    + rail * _RailGlow
                    + capRail * 0.9 + capField
                    + spark * _SparkGlow
                    + sweep) * inLane);
                if (alpha <= 0.003)
                {
                    return half4(0, 0, 0, 0);
                }

                // Colour has DEPTH rather than one orange: the quiet field sits in burnt umber and
                // only energy carries it up through rust into amber, gold and - at the very top,
                // for the sweep and the snaps alone - near-white.
                float depth = saturate((heat * 1.6 + scan * 0.5 + pocket * 0.25) * _DepthContrast);
                float3 baseRgb = lerp(_DeepColor.rgb, _FieldColor.rgb, depth);

                // Normalised against its own total: unnormalised, every layer stacking bleaches
                // the lane to white.
                float wHeat = saturate(heat * 1.4 + scan * 0.5);
                float wRail = saturate(rail * _RailGlow + capRail);
                float wCrit = saturate(sweep + spark * 2.0 + rail * _RailGlow * th * 0.45);
                float wMote = saturate(motes * 1.5);
                wRail = saturate(wRail + tick * 1.2);       // the ladder is machined, like the rails
                float total = 1.0 + wHeat + wRail + wCrit + wMote;
                float3 rgb = (baseRgb + _HeatColor.rgb * wHeat + _RailColor.rgb * wRail
                    + _CriticalColor.rgb * wCrit + _MoteColor.rgb * wMote) / total;
                float luma = dot(rgb, float3(0.299, 0.587, 0.114));
                rgb = lerp(float3(luma, luma, luma), rgb, _Saturation);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
