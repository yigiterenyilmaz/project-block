// PURPOSE: "Parazit"'s EMBEDDED JOKER IMPRINT - the bound joker's REAL icon, drawn INSIDE the
// parasite's membrane rather than on top of the host cube. One shared material; everything per host
// comes in through a MaterialPropertyBlock (ParasiteImprint / ParasiteHostView / CardVisual).
//
// WHAT IT REPLACES. The passenger's icon used to sit on the host as a separate logo - in the hand a
// dark disc with the icon on it, on the board a pod the nest grew round it with a film laid over the
// top. Both read as "an icon stuck next to the cube": a badge, a token, UI. The parasite does not
// carry a badge; it has TAKEN the joker the way it has taken the cube, and the icon is a biological
// brand pressed into its tissue. So nothing here draws a plate, a frame, a ring or a backdrop - the
// silhouette IS the icon's own alpha, and everything below only changes what is inside it.
//
//   TREATMENT  the icon keeps its own identity - Midas stays in the gold family, Yangın stays warm,
//              Buzluk stays cold - but the parasite suppresses it: some saturation out (_Desat), a
//              pull toward the membrane's hue that keeps the icon's own luminance structure
//              (_MembraneTint), and a retention dial (_Retention) for how much of its own colour
//              survives the take-over at all. _Treatment runs the whole thing from "raw icon" (0) to
//              "fully assimilated" (1), which is what the attach sequence animates. It is never one
//              flat magenta: tinting every icon the same colour would erase who is riding.
//   FRONT      THE MEMBRANE CROSSES IT. A thin veil of the film lies over the whole icon (_Veil),
//   MEMBRANE   and in one or two places it has grown right over the silhouette's EDGE (_Occlusion:
//              one of four deterministic variants - the top-left covered, the bottom edge covered,
//              strands across the left and right sides, an irregular partial wrap - and how much of
//              the edge it eats, 5-15%). Covered pixels go to the membrane's own colour, so the film
//              is visibly IN FRONT there and visibly behind it everywhere else.
//   DEPTH      _Depth darkens the silhouette's inner edge and softens it, which is "sunk two pixels
//              into the tissue" without any real Z.
//   DISTORTION the film is not glass: the icon is seen through it slightly bent (_Distort, a small
//              low-frequency displacement plus a radial squeeze), driven by the membrane's own
//              phase so the two move together. _Stretch is the membrane being pulled one way by the
//              cube underneath - the icon goes with it, a percent or two, never a rubber logo.
//   WAVE       _Wave is the rare contraction passing through the icon's region: one side presses,
//              then the middle, then the other side.
//   RIM        on a dark host the icon can vanish into the dark film, so a VERY thin, low-opacity
//              membrane-lit rim runs just inside its silhouette in a desaturated light version of its
//              own colour (_Rim, _RimColour). Never a neon outline, never a white halo, and off on
//              a light host.
//   SHEEN      one dark-wet band crossing it once (_Sheen) when the seal closes. Membrane-coloured,
//              never a collectible sparkle.
//   TEAR       the host dies: the icon splits along 2-4 jagged cuts into pieces that drift apart
//              WITH the film (_Tear), and its colour goes original -> drained -> dark plum -> gone
//              (_Death). It never flies out and it never escapes.
//
// SPACE. A joker icon is a single sprite cut from a larger texture (a sub-rect), drawn on a
// full-rect quad. So nothing here works in raw UV: the vertex stage turns the quad's own position
// into n (-1..1 across the sprite rect), every warp is done in n, and n is turned back into UV
// through _UVRect for the sample - clamped to the rect, so a warp can never read a neighbour's
// pixels. _Visible is where the icon's actual silhouette sits inside that rect (measured once from
// the art by ParasiteIconProfile), so the masks follow the drawing rather than its padding.
//
// DOWNSCALE. The icons are 400-odd pixels drawn at 25-40 and are imported without mipmaps, so a
// single tap shimmers into "mud". Every sample is a 4-tap rotated-grid supersample sized to the
// pixel's own footprint (ddx/ddy), which keeps the silhouette and the big shapes readable.
//
// Tagged Universal2D. Without it the view draws the icon on the plain sprite material, dimmed and
// tinted by colour alone - still recognisable, still not a badge.
Shader "ProjectBlock/ParasiteEmbeddedIcon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Joker icon", 2D) = "white" {}
        _UVRect ("The sprite's rect in the texture: xMin, yMin, xMax, yMax", Vector) = (0, 0, 1, 1)
        _Visible ("The silhouette's bounds inside the rect, in n: xMin, yMin, xMax, yMax", Vector) = (-1, -1, 1, 1)
        _Membrane ("The membrane's colour over this host", Color) = (0.26, 0.13, 0.28, 1)
        _MembraneDeep ("The membrane's deep colour", Color) = (0.12, 0.06, 0.14, 1)
        _Treatment ("0 raw icon .. 1 fully assimilated", Float) = 1
        _Retention ("How much of the icon's own colour survives", Float) = 0.65
        _Desat ("Saturation taken out", Float) = 0.25
        _MembraneTint ("Pull toward the membrane's hue", Float) = 0.25
        _Brightness ("Brightness, after contrast compensation", Float) = 0.84
        _Opacity ("The icon's own opacity inside the film", Float) = 0.78
        _Veil ("The thin film lying over the whole icon", Float) = 0.22
        _Occlusion ("Edge occlusion: variant, strength, edge consumed, seed", Vector) = (0, 1, 0.08, 0)
        _Depth ("How far it is sunk into the tissue", Float) = 1
        _Distort ("Distortion: amplitude (n), radial squeeze, phase, -", Vector) = (0.05, 0.03, 0, 0)
        _Stretch ("Membrane stretch: direction xy, along, across", Vector) = (1, 0, 0, 0)
        _Wave ("Contraction wave: direction xy, progress, strength", Vector) = (1, 0, -1, 0)
        _Rim ("Dark-host rim opacity", Float) = 0
        _RimColour ("Its colour - a desaturated light version of the icon's own", Color) = (0.7, 0.66, 0.7, 1)
        _Sheen ("Wet sheen: progress, strength, direction (radians), -", Vector) = (-1, 0, 0.6, 0)
        _Tear ("Tear: progress, seed, pieces, spread", Vector) = (0, 0, 3, 0.18)
        _Death ("Colour loss at death, 0..1", Float) = 0
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
                float4 _UVRect;
                float4 _Visible;
                float4 _Membrane;
                float4 _MembraneDeep;
                float _Treatment;
                float _Retention;
                float _Desat;
                float _MembraneTint;
                float _Brightness;
                float _Opacity;
                float _Veil;
                float4 _Occlusion;
                float _Depth;
                float4 _Distort;
                float4 _Stretch;
                float4 _Wave;
                float _Rim;
                float4 _RimColour;
                float4 _Sheen;
                float4 _Tear;
                float _Death;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.color = input.color;
                // The sprite's own UV: it already spans exactly _UVRect on a full-rect quad, and it
                // is turned into n in the fragment.
                output.uv = input.uv;
                return output;
            }

            float Hash(float n)
            {
                return frac(sin(n * 12.9898 + 4.1414) * 43758.5453);
            }

            // n (-1..1 across the sprite rect) -> texture UV.
            float2 ToUV(float2 n)
            {
                return lerp(_UVRect.xy, _UVRect.zw, n * 0.5 + 0.5);
            }

            float2 ToN(float2 uv)
            {
                return (uv - _UVRect.xy) / max(_UVRect.zw - _UVRect.xy, 1e-5) * 2.0 - 1.0;
            }

            // A 4-tap rotated-grid supersample over the pixel's own footprint. A sample that falls
            // off the rect is transparent - a warp must never read another sprite's pixels.
            static const float2 RotatedGrid[4] =
            {
                float2(-0.125, -0.375), float2(0.375, -0.125),
                float2(0.125, 0.375), float2(-0.375, 0.125)
            };

            half4 SampleIcon(float2 n, float2 dx, float2 dy)
            {
                half4 sum = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 uv = ToUV(n) + dx * RotatedGrid[i].x + dy * RotatedGrid[i].y;
                    float2 m = ToN(uv);
                    // LOD 0: the icons carry no mipmaps, and an explicit level keeps every
                    // sample legal after the early-out below.
                    half4 t = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0);
                    float inside = step(abs(m.x), 1.0) * step(abs(m.y), 1.0);
                    sum += t * inside;
                }
                return sum * 0.25;
            }

            // Alpha only, one tap - for the edge and rim probes.
            float AlphaAt(float2 n)
            {
                float inside = step(abs(n.x), 1.0) * step(abs(n.y), 1.0);
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, ToUV(n), 0).a * inside;
            }

            // Where n sits inside the silhouette's own bounds: -1..1 across what is actually drawn.
            float2 ToVisible(float2 n)
            {
                float2 c = (_Visible.xy + _Visible.zw) * 0.5;
                float2 h = max((_Visible.zw - _Visible.xy) * 0.5, 1e-3);
                return (n - c) / h;
            }

            // THE FOUR OCCLUSION VARIANTS. Each returns 0..1 "the film has grown over this pixel",
            // before it is restricted to the silhouette's outer band. Irregular on purpose: a
            // straight mask across an icon is a wipe, and a round one is a vignette.
            float OcclusionMask(float2 m, float variant, float seed)
            {
                float a = atan2(m.y, m.x);
                float wob = 0.10 * sin(a * 3.0 + seed * 6.1) + 0.06 * sin(a * 5.0 - seed * 3.7);
                float r = length(m);
                if (variant < 0.5)
                {
                    // A: the TOP-LEFT is covered.
                    float d = dot(m, normalize(float2(-1.0, 1.0)));
                    return smoothstep(0.35 + wob, 0.75 + wob, d);
                }
                if (variant < 1.5)
                {
                    // B: the BOTTOM EDGE is covered.
                    float y = -m.y + 0.12 * sin(m.x * 4.3 + seed * 5.0);
                    return smoothstep(0.52, 0.86, y);
                }
                if (variant < 2.5)
                {
                    // C: two STRANDS of film across the left and right sides.
                    float slant = m.x + m.y * 0.35;
                    float left = 1.0 - smoothstep(0.06, 0.16,
                        abs(slant + 0.62 + 0.05 * sin(m.y * 5.0 + seed)));
                    float right = 1.0 - smoothstep(0.05, 0.14,
                        abs(slant - 0.66 + 0.05 * sin(m.y * 4.1 - seed)));
                    return max(left, right);
                }
                // D: an irregular PARTIAL WRAP round the rim, heavier on one side.
                float lobe = 0.5 + 0.5 * sin(a * 2.0 + seed * 4.0);
                return smoothstep(0.62, 0.95, r + lobe * 0.28 + wob);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 n0 = ToN(input.uv);
                float2 n = n0;
                float2 m0 = ToVisible(n0);

                // ---- the tear: pieces drifting apart WITH the film ----
                float tear = saturate(_Tear.x);
                float crack = 0.0;
                if (tear > 0.0)
                {
                    float pieces = clamp(_Tear.z, 2.0, 4.0);
                    float2 push = 0;
                    [unroll]
                    for (int k = 0; k < 3; k++)
                    {
                        // 2-4 pieces is 1-3 cuts; a cut past that count simply does nothing.
                        float live = step((float)k, pieces - 1.5);
                        float ang = Hash(_Tear.y * 7.0 + k * 3.1) * 3.14159 + k * 1.05;
                        float2 nrm = float2(cos(ang), sin(ang));
                        float along = dot(m0, float2(-nrm.y, nrm.x));
                        // Jagged, because tissue tears along its own weak lines.
                        float side = dot(m0, nrm) - 0.12 * (Hash(k + _Tear.y) - 0.5)
                            + 0.07 * sin(along * 9.0 + k * 2.3) + 0.04 * sin(along * 17.0 - k);
                        push += nrm * sign(side) * live;
                        crack = max(crack, live
                            * (1.0 - smoothstep(0.0, 0.05 + 0.1 * tear, abs(side))));
                    }
                    // The pixel we see came from further in: its piece has moved outward.
                    n -= push * _Tear.w * tear;
                }

                // ---- the membrane pulling it: a percent or two along one direction ----
                if (_Stretch.z != 0.0 || _Stretch.w != 0.0)
                {
                    float2 d = normalize(_Stretch.xy + float2(1e-5, 0.0));
                    float2 p = float2(-d.y, d.x);
                    n = n - d * dot(n, d) * _Stretch.z / (1.0 + _Stretch.z)
                          - p * dot(n, p) * _Stretch.w / (1.0 + _Stretch.w);
                }

                // ---- the contraction wave: one side, the middle, the other side ----
                if (_Wave.z >= 0.0 && _Wave.w > 0.0)
                {
                    float2 d = normalize(_Wave.xy + float2(1e-5, 0.0));
                    float s = dot(m0, d);
                    float front = lerp(-1.3, 1.3, saturate(_Wave.z));
                    float bump = exp(-(s - front) * (s - front) * 9.0);
                    // A compression is a local magnification of what is ahead of the front.
                    n += d * bump * _Wave.w * 0.12;
                    n *= 1.0 + bump * _Wave.w * 0.03;
                }

                // ---- seen through the film: a low-frequency bend and a slight radial squeeze ----
                float ph = _Distort.z;
                n += _Distort.x * float2(sin(n0.y * 3.1 + ph), sin(n0.x * 2.7 - ph * 1.3));
                n *= 1.0 + _Distort.y * (1.0 - saturate(dot(m0, m0)));

                float2 dx = ddx(input.uv);
                float2 dy = ddy(input.uv);
                half4 tex = SampleIcon(n, dx, dy);
                float alpha = tex.a;
                if (alpha < 0.003)
                {
                    return half4(0, 0, 0, 0);
                }
                float3 c = tex.rgb / max(alpha, 1e-4);
                float2 m = ToVisible(n);

                // ---- THE TREATMENT: recognisable, but taken ----
                float treat = saturate(_Treatment);
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                float3 desat = lerp(c, lum.xxx, _Desat * treat);
                // The membrane's hue, carrying the icon's own light and dark - never a flat fill.
                float3 hue = _Membrane.rgb / max(dot(_Membrane.rgb, float3(0.299, 0.587, 0.114)), 1e-3);
                float3 tinted = saturate(hue * lum * 0.92);
                float3 toned = lerp(desat, tinted, _MembraneTint * treat);
                float3 kept = lerp(lerp(lum.xxx, tinted, 0.6), toned, lerp(1.0, _Retention, treat));
                c = kept * lerp(1.0, _Brightness, treat);

                // ---- depth: the inner edge sinks, so the icon sits IN the tissue ----
                float probe = 0.045;
                float aMin = min(min(AlphaAt(n + float2(probe, 0)), AlphaAt(n - float2(probe, 0))),
                    min(AlphaAt(n + float2(0, probe)), AlphaAt(n - float2(0, probe))));
                float inner = saturate(alpha - aMin);
                c *= 1.0 - 0.28 * inner * saturate(_Depth);
                c = lerp(c, _MembraneDeep.rgb, 0.18 * inner * saturate(_Depth));

                // ---- the dark-host rim: very thin, low, membrane-lit ----
                if (_Rim > 0.0)
                {
                    c = lerp(c, _RimColour.rgb, saturate(inner * 1.6) * saturate(_Rim));
                }

                // ---- the film in front: a thin veil everywhere, and the edge it has eaten ----
                float grain = 0.5 + 0.5 * sin(m.x * 5.3 + 1.1) * sin(m.y * 4.7 - 0.4);
                float veil = saturate(_Veil) * (0.55 + 0.45 * grain);
                c = lerp(c, _Membrane.rgb, veil);
                float occ = 0.0;
                if (_Occlusion.y > 0.0)
                {
                    // Only the silhouette's outer band can be eaten: the middle of the icon is
                    // where its identity is, and the film only takes bites out of its edges.
                    float band = smoothstep(0.35, 0.95, length(m));
                    float reach = saturate(_Occlusion.z / 0.08);
                    occ = OcclusionMask(m, _Occlusion.x, _Occlusion.w) * band
                        * saturate(_Occlusion.y) * saturate(0.35 + 0.65 * reach);
                    c = lerp(c, lerp(_Membrane.rgb, _MembraneDeep.rgb, 0.35), saturate(occ * 0.88));
                }

                // ---- the wet sheen of the seal: one dark band, once ----
                if (_Sheen.x >= 0.0 && _Sheen.y > 0.0)
                {
                    float2 sd = float2(cos(_Sheen.z), sin(_Sheen.z));
                    float s = dot(m, sd);
                    float front = lerp(-1.4, 1.4, saturate(_Sheen.x));
                    float lit = exp(-(s - front) * (s - front) * 28.0);
                    float trail = exp(-(s - front + 0.22) * (s - front + 0.22) * 18.0);
                    float3 wet = lerp(_Membrane.rgb, float3(0.72, 0.4, 0.55), 0.35);
                    c = lerp(c, wet * 1.35, lit * _Sheen.y);
                    c *= 1.0 - 0.18 * trail * _Sheen.y;
                }

                // ---- death: original -> drained -> dark plum -> gone ----
                float death = saturate(_Death);
                if (death > 0.0)
                {
                    float l2 = dot(c, float3(0.299, 0.587, 0.114));
                    c = lerp(c, l2.xxx * 0.8, saturate(death * 2.2));
                    c = lerp(c, _MembraneDeep.rgb, saturate(death * 2.0 - 0.6));
                    alpha *= 1.0 - smoothstep(0.55, 1.0, death);
                }
                if (tear > 0.0)
                {
                    alpha *= 1.0 - crack * saturate(tear * 2.4);
                    // The pieces come apart from their own edges inward, not as a fade.
                    float erode = Hash(floor(m0.x * 9.0) * 17.0 + floor(m0.y * 9.0) * 5.0 + _Tear.y);
                    alpha *= 1.0 - smoothstep(erode - 0.1, erode + 0.1, tear * 1.25 - 0.3);
                }

                // Occluded pixels are the FILM, so they keep a little more body than the icon.
                float a = alpha * lerp(1.0, _Opacity, treat) * (1.0 + occ * 0.15);
                return half4(saturate(c), saturate(a) * input.color.a) * half4(input.color.rgb, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
