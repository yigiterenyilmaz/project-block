// PURPOSE: "Kütleçekim Merkezi" - the arena's pull, drawn as a FIELD rather than as arrows.
//
// AN ARROW SAYS WHICH WAY; A FIELD SAYS THE BOARD IS BEING PULLED. That is the whole brief. The
// old marker was a row of pips outside one edge: readable, and completely inert. What replaces it
// is a very low-contrast current running across the whole arena, densest where it is heading, with
// the far edge drawn tight - so the direction is legible from motion and weight rather than from a
// symbol you have to read.
//
// EVERYTHING IS IN LANE SPACE. One coordinate ALONG the pull and one ACROSS it, both in cells, so
// the same code draws all four directions and nothing is special-cased per direction. The view
// hands in a vector; turning the board's gravity is turning that vector.
//
// IT IS NOT THE CIRCUIT. The Devre joker owns hard electric blue, arcs and sparks. Gravity is the
// opposite register - a pale, desaturated teal that behaves like a current in a fluid: slow,
// slightly viscous, gathering rather than striking. Nothing here should ever read as lightning.
//
// NOTHING IS DRAWN FOR DOWN. Every board falls downward, so a permanent marker for it would be
// noise; the view simply passes strength 0 and this draws nothing at all.
//
// IT MUST NOT LIE. Only WATER obeys this pull in the rules, so the field never drags anything that
// looks like a block: it is a thin layer under the cubes, the grid stays legible through it, and
// no part of it pushes on a cube's silhouette.

Shader "ProjectBlock/GravityField"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.06, 0.12, 0.16, 1)
        _FlowColor ("Flow", Color) = (0.34, 0.62, 0.72, 1)
        _EdgeColor ("Edge", Color) = (0.62, 0.86, 0.92, 1)
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
            Name "GravityField"

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

            float4 _DeepColor;
            float4 _FlowColor;
            float4 _EdgeColor;

            /// The board's footprint in CELLS, so the field's scale is the same on a 7x7 and an
            /// 11x11 arena rather than stretching with it.
            float2 _SizeCells;

            /// The pull, as a unit vector in board space. The ONE input that says which way
            /// everything goes - flow, density, which edge is the destination, the markers.
            float2 _Dir;

            float _FieldTime;

            /// 0 draws nothing at all (gravity is DOWN, or the field is collapsing).
            float _Strength;

            float _Opacity;
            float _FlowSpeed;
            float _FlowScale;
            float _Stretch;
            float _DensityGradient;

            float _EdgeStrength;
            float _EdgeWidth;
            float _EdgePulse;

            float _Distortion;
            float _DistortionScale;

            /// The activation beat: a brief squeeze toward the middle, then one soft shove along
            /// the new direction. Both are driven from the view, not timed in here.
            float _Compression;
            float _Impulse;

            float _MarkerCount;
            float _MarkerOpacity;
            float _MarkerSize;

            float _GridStrength;
            float _CellPhase;

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

            half4 frag(Varyings IN) : SV_Target
            {
                if (_Strength <= 0.002)
                {
                    return half4(0, 0, 0, 0);
                }

                // ---- LANE SPACE. p is in cells, centred; along runs with the pull, across is
                // the width of the board seen from the pull's point of view.
                float2 p = (IN.uv - 0.5) * _SizeCells;
                float2 dir = normalize(_Dir + float2(1e-5, 0.0));
                float2 side = float2(-dir.y, dir.x);
                float along = dot(p, dir);
                float across = dot(p, side);

                // How far along the board this pixel is, 0 at the edge the field comes FROM and 1
                // at the edge it is heading for. Everything that has to know "which end" uses it.
                float span = abs(dot(_SizeCells, abs(dir)));
                float t = saturate(along / max(span, 0.001) + 0.5);

                // ---- a squeeze toward the middle on activation, so the field visibly recalibrates
                along += -along * _Compression * 0.22;

                // ---- very slight refraction, and ONLY the field is bent by it. The grid and the
                // cubes are drawn elsewhere; bending those would say blocks are being moved.
                float2 warp = float2(
                    vnoise(float2(across * _DistortionScale,
                        along * _DistortionScale * 0.6 - _FieldTime * 0.12)),
                    vnoise(float2(across * _DistortionScale + 7.3,
                        along * _DistortionScale * 0.5 - _FieldTime * 0.09)));
                float2 q = float2(across, along) + warp * _Distortion;

                // ---- the current. Features are LONG along the pull and narrow across it, which
                // is what makes a soft noise field read as a direction instead of as fog.
                float travel = _FieldTime * _FlowSpeed + _Impulse * 0.55;
                float n1 = vnoise(float2(q.x * _FlowScale,
                    (q.y - travel) * _FlowScale / max(_Stretch, 0.01)));
                float n2 = vnoise(float2(q.x * _FlowScale * 2.1 + 3.7,
                    (q.y - travel * 1.37) * _FlowScale * 1.6 / max(_Stretch, 0.01)));
                float flow = saturate((n1 * 0.66 + n2 * 0.34) * 0.5 + 0.5);
                // Sharpened a little, so the streaks separate instead of blurring into a haze.
                flow = pow(flow, 2.6);

                // ---- denser where it is going. A gradient, never a coloured band.
                float density = lerp(1.0 - _DensityGradient * 0.5, 1.0 + _DensityGradient * 0.5, t);
                flow *= density;

                // ---- the destination edge: the field gathering, not a wall of light. Streaks run
                // into it and are gone, which is what an attractor looks like from inside.
                float toEdge = 1.0 - t;
                float edge = exp(-(toEdge * toEdge) / max(_EdgeWidth * _EdgeWidth, 1e-5));
                float pulse = 1.0 + _EdgePulse * sin(_FieldTime * 1.7);
                // MODULATED BY THE CURRENT, not painted flat. A band that spans the whole width
                // is by nature perpendicular to the flow, and drawn at full strength it becomes
                // the brightest thing on the board - so the eye reads a stripe ACROSS the pull
                // instead of a current running along it. Letting the flow carve it makes it read
                // as the current arriving and gathering, which is what an attractor looks like.
                edge *= _EdgeStrength * pulse * (0.25 + 0.75 * flow);

                // ---- the markers. A few soft chevrons standing IN the edge band rather than
                // icons laid over the board - enough to settle the direction at a glance, faint
                // enough never to read as UI.
                //
                // Built in CELLS, both ways. The first version worked in normalised lane units
                // and mixed them with cell units in the same expression, so the arms never met
                // the band and nothing was drawn at all.
                float spanAcross = max(abs(dot(_SizeCells, abs(side))), 0.001);
                float pitch = spanAcross / max(_MarkerCount, 1.0);
                float slot = (frac(across / pitch + 0.5) - 0.5) * pitch;   // cells from a centre
                float toEdgeCells = toEdge * span;
                // The V's point LEADS: at the middle of a slot it sits closest to the edge, and
                // the arms fall away behind it.
                float arm = abs(slot) * 0.85 + 0.30;
                float d = abs(toEdgeCells - arm);
                float marker = exp(-(d * d) / max(_MarkerSize * _MarkerSize, 1e-5))
                    * smoothstep(pitch * 0.42, pitch * 0.24, abs(slot))
                    * smoothstep(span * 0.55, span * 0.10, toEdgeCells)
                    * _MarkerOpacity;

                // ---- the board's own grid gives way to none of this: the field THINS over a cell
                // seam rather than painting across it, so placement stays readable.
                float2 g = frac(p - _CellPhase);
                float2 e = min(g, 1.0 - g);
                float w = 0.09;
                float sx = 1.0 - smoothstep(0.0, w, e.x);
                float sy = 1.0 - smoothstep(0.0, w, e.y);
                float grid = 1.0 - (1.0 - sx) * (1.0 - sy);

                float body = flow * _Opacity;
                float alpha = saturate((body + edge * 0.9 + marker)
                    * (1.0 - grid * _GridStrength) * _Strength);
                if (alpha <= 0.003)
                {
                    return half4(0, 0, 0, 0);
                }

                // Normalised against its own total, or the layers stack into white.
                float wFlow = saturate(flow * 1.3);
                float wEdge = saturate(edge + marker * 1.5);
                float total = 1.0 + wFlow + wEdge;
                float3 rgb = (_DeepColor.rgb + _FlowColor.rgb * wFlow + _EdgeColor.rgb * wEdge)
                    / total;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
