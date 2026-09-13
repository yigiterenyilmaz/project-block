// PURPOSE: "Parazit"'s MEMBRANE - the thin translucent living wrap that holds a host cube down.
// Drawn on one shared subdivided mesh (ParasiteMembraneMesh) per host, everything per-instance in a
// MaterialPropertyBlock.
//
// This is the main visual layer of the mechanic, and it exists because of two earlier failures: a
// host used to be its own colour lerped toward magenta ("this one is pink"), and then a set of
// rounded-rectangle parts bolted to the corners ("a UI lock"). The cube is not marked and it is not
// clamped - it is WRAPPED, and the wrap is a living thing that does not let go.
//
//   SILHOUETTE  it is never the square the mesh is. An irregular contour built from a few
//               low-frequency lobes runs close to the cube's edge in places and pulls back toward
//               the middle in others, so the boundary reads as tissue that settled rather than as a
//               texture laid over the cell. Soft-edged by a pixel or two - never a vector border.
//   THICKNESS   the film is not evenly opaque. _Thick raises it in some regions and thins it in
//               others, and the THIN places are where the cube's own colour shows through most,
//               which is what keeps the block underneath legible.
//   BULGE       _Bulge (xy centre in UV, z strength, w radius) LIFTS the mesh in one region and
//               thins the film there. That is the idle: the cube pushing out from underneath. The
//               displacement is in the vertex stage, which is the whole reason this is a mesh.
//   TENSION     _Tension pulls the whole film in, deepens it and sharpens its edge - the parasite
//               gripping harder. A clamp is this going up.
//   EDGE WRAP   _EdgeWrap darkens and thickens the last band before the contour, so the membrane
//               reads as spilling over the cube's bevel instead of stopping flat on its face.
//               _Curl makes that UNEVEN around the contour: in some places the film curls right
//               over the bevel in a thick dark lip with a lit top, in others it stops short on the
//               face. An even band all the way round is a border, and a border is what a decal has.
//   FOLDS       _FoldA / _FoldB / _FoldC are two or three GATHERS of the film - broad ridges
//               crossing the face, lit along one flank, shadowed and creased along the other, and
//               a little more opaque through their middle. They are shading and thickness in this
//               shader rather than geometry over it, and that is the whole point: a gather has no
//               silhouette of its own, because it IS the sheet. Drawn as separate pieces they were
//               a chain of convex segments, which is a tube however wide it is made - a cable
//               lying on the cube. Tension flattens them; slack heaps them up.
//   WINDOWS     _WindowA / _WindowB open NEGATIVE SPACE - two regions where there is no film at
//               all and the cube is simply itself, seen through the wrap rather than under it. The
//               film gathers into a thicker lit lip around each one. Without holes a translucent
//               sheet over a whole face reads as a filter on the cell; the holes are what say the
//               block is a separate thing being covered.
//   TEAR        _Tear (xy direction, z progress, w width) opens an irregular gap along a band - the
//               player's line shearing the wrap. It is a tear, never a straight laser cut: the
//               contour itself is what fails, so the edge is as ragged as the silhouette is.
//   SHEEN       a broad soft highlight biased to the game's own upper-left light. Satin, never wet.
//
// Tagged Universal2D, as the 2D renderer requires. Without it ParasiteHostView draws the membrane
// as a flat plum patch, which still says something is wrapped around the cube.
Shader "ProjectBlock/ParasiteMembrane"
{
    Properties
    {
        _Deep ("Deep plum", Color) = (0.16, 0.09, 0.19, 1)
        _Body ("Bruised violet", Color) = (0.35, 0.18, 0.38, 1)
        _Rose ("Warm vein", Color) = (0.72, 0.36, 0.52, 1)
        _Opacity ("How opaque the film is at its normal thickness", Float) = 0.55
        _Thick ("Thickness variation", Float) = 0.5
        _Shape ("Contour reach, lobe strength, lobe turn, edge softness", Vector) = (0.86, 0.13, 0.7, 0.1)
        _Bulge ("Bulge: uv centre, strength, radius", Vector) = (0.5, 0.5, 0, 0.3)
        _Tension ("How hard the wrap is pulling", Float) = 0
        _EdgeWrap ("How far it spills over the cube's edge", Float) = 0.5
        _Tear ("Tear: direction xy, progress z, width w", Vector) = (1, 0, 0, 0.2)
        _Sheen ("Satin highlight", Float) = 0.3
        _Vein ("Warm vein showing through", Float) = 0
        _FoldA ("A gather: direction (radians), offset across, bow, half width", Vector) = (0, 0, 0, 0)
        _FoldB ("A second gather", Vector) = (0, 0, 0, 0)
        _FoldC ("A third gather", Vector) = (0, 0, 0, 0)
        _FoldRelief ("How strongly the gathers are lit and creased", Float) = 1
        _Curl ("How unevenly it curls over the cube's edge", Float) = 0.6
        _WindowA ("A hole in the film: uv centre, strength, radius", Vector) = (0.5, 0.5, 0, 0.1)
        _WindowB ("A second hole: uv centre, strength, radius", Vector) = (0.5, 0.5, 0, 0.1)
        _Rim ("Separation rim on the contour - for a dark host", Float) = 0
        _RimColour ("That rim's colour", Color) = (0.62, 0.54, 0.66, 1)
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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 face : TEXCOORD1;
                float lift : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Deep;
                float4 _Body;
                float4 _Rose;
                float _Opacity;
                float _Thick;
                float4 _Shape;
                float4 _Bulge;
                float _Tension;
                float _EdgeWrap;
                float4 _Tear;
                float _Sheen;
                float _Vein;
                float4 _FoldA;
                float4 _FoldB;
                float4 _FoldC;
                float _FoldRelief;
                float _Curl;
                float4 _WindowA;
                float4 _WindowB;
                float _Rim;
                float4 _RimColour;
            CBUFFER_END

            // How far the bulge reaches this point, 0..1.
            float BulgeAt(float2 uv)
            {
                float d = length(uv - _Bulge.xy) / max(_Bulge.w, 1e-4);
                float f = 1.0 - saturate(d);
                return f * f * (3.0 - 2.0 * f) * _Bulge.z;
            }

            // WHERE THIS POINT SITS ACROSS ONE GATHER, in half-widths: 0 on its crest, +-1 at its
            // flanks, beyond that outside it. The fold runs at an angle with a low-frequency bow,
            // so it is never a straight seam across the face.
            float FoldAcross(float2 f, float4 fold)
            {
                if (fold.w <= 1e-4)
                {
                    return 10.0;
                }
                float2 dir = float2(cos(fold.x), sin(fold.x));
                float2 across = float2(-dir.y, dir.x);
                float along = dot(f, dir);
                float side = dot(f, across) - fold.y - sin(along * 1.9) * fold.z;
                return side / fold.w;
            }

            // The shading ONE gather adds: a lit flank, a falling one, and the crease where the
            // heap meets the flat film again. Returns the light multiplier; `swell` comes back as
            // how much thicker the film is here.
            float FoldShade(float2 f, float4 fold, out float swell)
            {
                float u = FoldAcross(f, fold);
                swell = 0.0;
                if (abs(u) > 1.6)
                {
                    return 1.0;
                }
                // The heap itself - fullest on the crest, gone by the flanks.
                float body = 1.0 - smoothstep(0.0, 1.0, abs(u));
                swell = body;
                // Lit on the side it rolls toward, falling away on the other, and creased just
                // outside that. Never a symmetric highlight: a ridge lit down its middle is a tube.
                float lit = 1.0 - smoothstep(0.0, 0.9, abs(u + 0.45));
                float fall = smoothstep(-0.1, 1.0, u);
                float crease = smoothstep(0.85, 1.25, u) * (1.0 - smoothstep(1.25, 1.6, u));
                return (1.0 + 0.30 * lit) * (1.0 - 0.34 * fall * body)
                    * (1.0 - 0.30 * crease);
            }

            // WHERE THIS POINT SITS INSIDE ONE OF THE FILM'S HOLES, 0..strength. Its own lobes,
            // so the opening is torn rather than punched - a circle with the block's full colour
            // inside it is a status light, and two of them are a pair of them.
            float WindowAt(float4 win, float2 uv)
            {
                if (win.z <= 0.0)
                {
                    return 0.0;
                }
                float2 v = uv - win.xy;
                float a = atan2(v.y, v.x);
                // NO ONE HARMONIC MAY DOMINATE. A single strong three-lobe term makes a clover,
                // which is a recognisable symbol and worse than the circle it replaced; three weak
                // ones at different frequencies just make the opening irregular.
                float p = win.x * 21.0 + win.y * 13.0;
                float r = win.w * (1.0 + 0.15 * sin(a * 2.0 + p)
                    + 0.11 * sin(a * 3.0 - p * 1.7)
                    + 0.07 * sin(a * 5.0 + p * 0.6));
                float d = length(v) / max(r, 1e-4);
                float f = 1.0 - saturate(d);
                return f * f * (3.0 - 2.0 * f) * win.z;
            }

            // How far a region (uv centre, strength, radius) reaches this point, 0..strength.
            float RegionAt(float4 region, float2 uv)
            {
                float d = length(uv - region.xy) / max(region.w, 1e-4);
                float f = 1.0 - saturate(d);
                return f * f * (3.0 - 2.0 * f) * region.z;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS;
                // THE CUBE PUSHING OUT. One region of the film lifts; the rest of it stays where it
                // is. A quad cannot do this - which is why the membrane is a subdivided mesh.
                float lift = BulgeAt(input.uv);
                float2 away = input.uv - _Bulge.xy;
                pos.xy += away * lift * 0.16;
                // And the whole film draws IN as the wrap tightens.
                pos.xy *= 1.0 - saturate(_Tension) * 0.035;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(pos));
                output.uv = input.uv;
                output.face = (input.uv - 0.5) * 2.0;
                output.lift = lift;
                return output;
            }

            // The membrane's own contour: a few low-frequency lobes, so it hugs the cube's edge in
            // some places and pulls back toward the middle in others. Never the square it is drawn
            // on, and never a circle either.
            float ContourRadius(float2 f)
            {
                float a = atan2(f.y, f.x);
                float lobes = sin(a * 3.0 + _Shape.z) * 0.6
                    + sin(a * 5.0 - _Shape.z * 1.7) * 0.3
                    + sin(a * 2.0 + _Shape.z * 0.5) * 0.4;
                return _Shape.x + lobes * _Shape.y;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 f = input.face;
                // Chebyshev-ish so the film follows a CELL rather than a disc, softened toward the
                // radial so its corners are not square.
                float square = max(abs(f.x), abs(f.y));
                float round = length(f);
                float d = lerp(round, square, 0.55);
                float contour = ContourRadius(f);

                // ---- the silhouette ----
                float soft = max(_Shape.w, 1e-4);
                float inside = 1.0 - smoothstep(contour - soft, contour + soft * 0.5, d);
                if (inside <= 0.001)
                {
                    return half4(0, 0, 0, 0);
                }

                // ---- thickness: some regions heavier, some barely there ----
                float grain = sin(f.x * 4.1 + 1.3) * sin(f.y * 3.3 - 0.7);
                float thickness = 0.55 + 0.45 * grain * saturate(_Thick);
                // Where the cube pushes out the film STRETCHES and thins, which is what lets the
                // block's own colour show through the bulge.
                thickness *= 1.0 - saturate(input.lift) * 0.55;
                // And tightening thickens it again.
                thickness *= 1.0 + saturate(_Tension) * 0.3;

                // ---- THE GATHERS ----
                // The film is not a flat sheet: it heaps up in two or three broad ridges. They
                // thicken it where they run, which is why the block goes deadest under them.
                float sa, sb, sc;
                float foldLight = FoldShade(f, _FoldA, sa) * FoldShade(f, _FoldB, sb)
                    * FoldShade(f, _FoldC, sc);
                float swell = max(sa, max(sb, sc));
                thickness *= 1.0 + swell * 0.55;

                // ---- the body colour ----
                float3 body = lerp(_Deep.rgb, _Body.rgb, saturate(thickness));
                body *= lerp(1.0, foldLight, saturate(_FoldRelief));
                body = lerp(body, _Deep.rgb, saturate(_Tension) * 0.35);
                // A warm vein, only when something is circulating.
                body = lerp(body, _Rose.rgb, saturate(_Vein) * 0.4 * saturate(grain * 0.5 + 0.5));

                // ---- the edge wrap: the last band before the contour goes over the bevel ----
                // AND IT IS NOT EVEN. Where curl is high the film has rolled right over the cube's
                // edge - a thick dark lip with its own lit top; where it is low the film stops
                // short on the face. A band of the same weight all the way round is a BORDER, and
                // that is exactly what a sticker has.
                float ang = atan2(f.y, f.x);
                float curl = 0.5 + 0.5 * sin(ang * 2.0 + _Shape.z * 1.3
                    + sin(ang * 3.0 - _Shape.z) * 0.6);
                // At _Curl 0 this is exactly the old even band, so the dial goes from "a border"
                // to "somewhere it grips and somewhere it does not" and never overshoots on its
                // way there.
                float wrapHere = lerp(1.0, lerp(0.25, 1.45, curl), saturate(_Curl));
                float rim = smoothstep(contour - soft * 4.0, contour, d);
                body *= 1.0 - 0.45 * rim * saturate(_EdgeWrap) * wrapHere;
                // A hairline of its own light just inside that, so the boundary has a form - and
                // it is brightest exactly where the film has curled hardest, which is the top of
                // the roll going over the bevel.
                float lip = smoothstep(contour - soft * 6.0, contour - soft * 3.0, d)
                    * (1.0 - rim);
                body = lerp(body, _Rose.rgb, lip * (0.14 + 0.22 * curl * saturate(_Curl)));
                // AND A SEPARATION RIM when the block under it is dark. Without it a smoky film
                // over obsidian has no boundary at all - the parasite simply is not there.
                float rimBand = _Rim > 0.0
                    ? smoothstep(contour - soft * 3.0, contour - soft * 0.5, d)
                        * (1.0 - smoothstep(contour - soft * 0.5, contour + soft * 0.5, d))
                    : 0.0;
                body = lerp(body, _RimColour.rgb, saturate(rimBand) * saturate(_Rim));

                // ---- satin sheen, broad and biased to the upper-left light ----
                float lit = saturate(0.5 + 0.5 * dot(normalize(float2(-0.6, 0.8)), f));
                body *= 1.0 + _Sheen * (lit - 0.5);
                // The bulge catches a little more of it - it is the part standing proud.
                body *= 1.0 + 0.25 * saturate(input.lift);

                float alpha = inside * _Opacity * thickness;
                // A gather is more of the same film, so it is more opaque - never a different
                // colour laid over it.
                alpha *= 1.0 + swell * 0.35 * saturate(_FoldRelief);
                // The bulge is also where the film is most transparent.
                alpha *= 1.0 - saturate(input.lift) * 0.4;
                alpha *= 1.0 + rim * saturate(_EdgeWrap) * 0.5 * wrapHere;

                // ---- NEGATIVE SPACE: where there is no film at all ----
                // The cube is simply itself in these, seen THROUGH the wrap rather than under it,
                // and the film gathers into a thicker lit lip around each hole - tissue that has
                // pulled away and heaped at the edge, never a punched-out circle.
                float holes = max(WindowAt(_WindowA, input.uv), WindowAt(_WindowB, input.uv));
                if (holes > 0.0)
                {
                    float gap = saturate(holes);
                    float lipRing = saturate(holes * 3.4) - gap;
                    alpha *= 1.0 - smoothstep(0.25, 0.75, gap);
                    alpha *= 1.0 + saturate(lipRing) * 0.8;
                    body = lerp(body, _Rose.rgb, saturate(lipRing) * 0.22);
                }

                // ---- the tear: the wrap failing along the line's band ----
                if (_Tear.z > 0.0)
                {
                    float2 dir = normalize(_Tear.xy + float2(1e-5, 0));
                    float2 across = float2(-dir.y, dir.x);
                    float band = abs(dot(f, across));
                    // Ragged, because the contour's own lobes decide where it gives first.
                    float ragged = _Tear.w * (0.6 + 0.4 * sin(dot(f, dir) * 6.1));
                    float open = ragged * saturate(_Tear.z);
                    float gap = 1.0 - smoothstep(open * 0.55, open, band);
                    alpha *= 1.0 - gap;
                    // The torn edge lifts a little of its own colour, briefly.
                    float lipTear = smoothstep(open, open * 1.35, band)
                        * (1.0 - smoothstep(open * 1.35, open * 1.9, band));
                    body = lerp(body, _Rose.rgb, lipTear * 0.35 * saturate(_Tear.z));
                }

                // The rim also thickens the film a touch, so on a dark host the boundary
                // has real presence rather than being a tinted line.
                alpha = saturate(alpha + rimBand * saturate(_Rim) * 0.45);
                return half4(body, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
