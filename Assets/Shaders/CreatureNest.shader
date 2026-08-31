// PURPOSE: "Besleme"'s patch - the habitat UNDER the board's slots, not a thing sitting in them.
//
// IT IS A COORDINATE, NOT AN OBJECT. Blocks are placed on these cells, so the block is always the
// subject and this is always background: thin, low, desaturated, and collapsing to almost nothing
// wherever a cube actually stands. An empty cell here must still read as EMPTY AND PLACEABLE - if
// the player stops seeing somewhere to put a block, the effect has cost more than it gave.
//
// ACTIVITY LIVES AT THE EDGES. The middle of every cell is kept deliberately quiet, because that is
// where a block will sit. What identifies the zone is its perimeter and the space between slots,
// which survives a cube being dropped on top of it.
//
// WHY A SHADER AND NOT A PAINTED TEXTURE. The two things this effect has to be at once - a clean
// high-resolution contour and a dense interior that MOVES - cannot both come off a texture the CPU
// repaints. Matching the screen would mean roughly eighty texels per cell and three quarters of a
// million pixels rewritten every frame. So the shape is uploaded once and everything else is
// evaluated per pixel, per frame, for free.
//
// THE CONTOUR IS A DISTANCE FIELD, NOT A PICTURE. _MainTex holds the SIGNED DISTANCE to the region
// boundary, not a mask of it. That is the whole reason the staircase is gone: a mask magnified
// eight times shows its texels, but a distance field INTERPOLATES - halfway between two texels is
// genuinely halfway to the edge - so a modest resolution still resolves the boundary to well under
// a pixel. The edge is then anti-aliased against fwidth, which makes it exactly one screen pixel
// wide at any zoom, any board size and any resolution.
//
// THE FLOW IS DOMAIN WARPED. Scrolling noise slides like a conveyor; bending the sample position by
// a second, slower noise first makes it curl and pool instead. One extra lookup, and it is the
// difference between a moving pattern and something circulating inside a liquid.
//
// IT IS SAMPLED IN REGION SPACE. All the noise is evaluated in board-cell coordinates, so nothing
// restarts at a cell boundary: on a five-cell cross the flow runs from one arm through the middle
// and out of another, which is what makes it one pool rather than five.

Shader "ProjectBlock/CreatureNest"
{
    Properties
    {
        _MainTex ("Distance Field", 2D) = "black" {}
        _GelColor ("Gel", Color) = (0.86, 0.30, 0.52, 1)
        _DeepColor ("Depth", Color) = (0.40, 0.14, 0.44, 1)
        _FlowColor ("Flow", Color) = (1, 0.46, 0.56, 1)
        _GlowColor ("Glow", Color) = (1, 0.84, 0.92, 1)
        _MembraneColor ("Membrane", Color) = (1, 0.56, 0.74, 1)
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
            Name "CreatureNest"

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
            float4 _MainTex_TexelSize;

            float4 _GelColor;
            float4 _DeepColor;
            float4 _FlowColor;
            float4 _GlowColor;
            float4 _MembraneColor;

            // The texture's own footprint in CELLS, so every noise below can be evaluated in board
            // space and nothing restarts at a cell edge.
            float2 _SizeCells;
            // Cells per unit of stored distance: turns the packed 0..1 back into real distance.
            float _DistRange;

            float _NestTime;
            float _Opacity;
            float _DepthStrength;
            float _DensityVariation;
            float _DensityScale;

            float _FlowStrength;
            float _FlowScale;
            float _FlowSpeed;
            float _FlowWarp;
            float _FlowWarpScale;
            float _FlowContrast;
            /// How hard the currents are turned to run ALONG the region rather than across it.
            float _FlowShapeBias;
            float _FlowThickness;

            float _MembraneWidth;
            float _MembraneFalloff;
            float _MembraneStrength;
            float _MembraneVariation;
            float _SheenStrength;
            float _SheenWidth;

            float _Breathing;
            float _EdgeAA;

            // The board's own grid, read back INSIDE the region. Without it five cells covered by
            // one continuous surface stop being five cells and the whole thing collapses into a
            // single icon - which is exactly what it did.
            float _CellSeam;
            float _CellSeamWidth;
            float _CellSeamSoftness;
            float _CentreCalm;
            float _OccupiedOpacity;
            float _OccupiedEdge;
            float _CellPhase;
            float _SheenScale;
            float _SheenMotion;
            float _SheenBroad;
            float _DensityContrast;

            // xy = position in cell space, z = age in seconds, w = 1 while alive.
            float4 _Feeds[6];
            float _FeedGlow;
            float _FeedRadius;
            float _FeedRipple;
            float _FeedPull;
            float _FeedLife;

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
                // Stored 0..1 with 0.5 on the boundary; unpacked back to cells.
                float2 fieldSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rg;
                float d = (fieldSample.r - 0.5) * 2.0 * _DistRange;
                // G carries how OCCUPIED this spot is, filtered by the same bilinear read - so a
                // cube's influence fades across its cell instead of switching at its border.
                float occupied = fieldSample.g;

                // ANTI-ALIASING. fwidth is how much d changes over one screen pixel, so the edge is
                // always exactly one pixel wide - at any zoom, board size or resolution.
                float aa = max(fwidth(d), 1e-5) * _EdgeAA;
                float inField = smoothstep(-aa, aa, d);
                if (inField <= 0.001)
                {
                    return half4(0, 0, 0, 0);
                }

                float2 P = IN.uv * _SizeCells;   // board-cell space: continuous across the region

                // ---- feeding: swallow, pull the currents in, and push a pressure wave out
                float feed = 0.0;
                float ripple = 0.0;
                float2 pull = float2(0.0, 0.0);
                [unroll]
                for (int fi = 0; fi < 6; fi++)
                {
                    float4 F = _Feeds[fi];
                    if (F.w < 0.5)
                    {
                        continue;
                    }
                    float2 delta = P - F.xy;
                    float r = length(delta);
                    float near = exp(-(r * r) / (_FeedRadius * _FeedRadius));
                    float fall = saturate(1.0 - F.z / _FeedLife);
                    feed += near * fall * fall * _FeedGlow;
                    // Thick and short: a pressure wave through gel, not a ring on water.
                    float front = r - F.z * 3.0;
                    ripple += exp(-front * front * 7.0) * near * fall * _FeedRipple;
                    pull -= delta * near * _FeedPull * fall;
                }

                // ---- SHAPE AWARENESS. The gradient of the distance field points straight out
                // of the region, so its perpendicular runs ALONG it. Advecting the flow down that
                // tangent makes a narrow arm carry its currents lengthways instead of across,
                // with no knowledge of the region's shape anywhere in the code - the field
                // already knows. And because the packed field flattens deep inside, the gradient
                // fades there on its own: directional in a thin arm, loose circulation in a wide
                // pool, which is exactly the difference asked for.
                float2 tx = _MainTex_TexelSize.xy * 2.0;
                float gx = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(tx.x, 0)).r
                    - SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(tx.x, 0)).r;
                float gy = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, tx.y)).r
                    - SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, tx.y)).r;
                float2 grad = float2(gx, gy);
                float glen = length(grad);
                float2 along = glen > 1e-5 ? float2(-grad.y, grad.x) / glen : float2(1.0, 0.0);
                float2 advect = along * (glen * 40.0) * _FlowShapeBias * _NestTime * _FlowSpeed;

                // ---- currents, domain warped so they curl instead of sliding
                float2 W = float2(
                    vnoise(P * _FlowWarpScale + float2(_NestTime * _FlowSpeed * 0.5, 0.0)),
                    vnoise(P * _FlowWarpScale + float2(31.7, -_NestTime * _FlowSpeed * 0.5)));
                float2 S = P * _FlowScale + pull + W * _FlowWarp + advect
                    + float2(_NestTime * _FlowSpeed, -_NestTime * _FlowSpeed * 0.7);
                float flowN = vnoise(S);
                // RIDGED, not squeezed. Bands live where the noise CROSSES ZERO, so taking the
                // absolute value and raising it to a power puts nothing where the vein should be
                // and a broad smear either side of it. Inverting first puts the ridge on the
                // crossing and the power narrows it into a vein.
                float flow = pow(saturate(1.0 - abs(flowN)), _FlowContrast) * _FlowStrength;

                // ---- body density. TWO octaves, and the second one DRIFTS, so the pockets are
                // not a fixed pattern printed on the surface - they wander the way concentrations
                // in a liquid do.
                float dens = vnoise(P * _DensityScale + float2(11.3, -7.1));
                dens += vnoise(P * _DensityScale * 2.7
                    + float2(-_NestTime * 0.035, _NestTime * 0.028)) * 0.55;
                dens = sign(dens) * pow(abs(dens / 1.55), _DensityContrast) * _DensityVariation;

                // ---- ONE highlight across the WHOLE region, not one per cell. Broad, slow and
                // low: this is what says a single wet surface rather than several lit panels.
                float broad = vnoise(P * _SheenScale
                    + float2(_NestTime * _SheenMotion, -_NestTime * _SheenMotion * 0.6));
                float broadSheen = saturate(broad) * _SheenBroad;

                // ---- membrane. A THICKENING of the gel at the rim, with its own slight variation
                // so it never reads as a stroke drawn round the outside.
                float memVar = 1.0 + vnoise(P * 1.9 + float2(5.5, 2.2)) * _MembraneVariation;
                float m = max(0.0, d - _MembraneWidth * memVar) / _MembraneFalloff;
                float membrane = (d < 0.0) ? 0.0 : exp(-m * m) * _MembraneStrength;
                float sh = (d < 0.0) ? 3.0 : d / _SheenWidth;
                float sheen = exp(-sh * sh) * _SheenStrength;

                // ---- cell seams. The gel is continuous, but the SLOTS under it are not, and the
                // player still has to read this as an area of five cells rather than one shape.
                float2 g = frac(P - _CellPhase);
                float2 e = min(g, 1.0 - g);
                // PER AXIS, then combined as a probabilistic OR - never min(). min() has a ridge
                // wherever its two arguments are equal, which for a cell is both diagonals, and
                // that ridge is exactly the X-shaped crease that made every cell look like a
                // padded cushion. This form has no crease anywhere.
                float w = _CellSeamWidth * _CellSeamSoftness;
                float sx = 1.0 - smoothstep(0.0, w, e.x);
                float sy = 1.0 - smoothstep(0.0, w, e.y);
                float seam = 1.0 - (1.0 - sx) * (1.0 - sy);
                seam = pow(seam, 1.8);

                // How close to a cell EDGE this pixel is, 0 in the middle and 1 at the border.
                // Detail is pushed out here and away from the centre, because the centre is where
                // a block goes.
                float edgeNess = saturate(1.0 - min(e.x, e.y) * 2.0);
                edgeNess = edgeNess * edgeNess;
                float calm = lerp(1.0, edgeNess, _CentreCalm);

                float pulse = 1.0 + _Breathing
                    * (sin(_NestTime * 2.39) * 0.62 + sin(_NestTime * 1.45) * 0.38);

                // The currents also make the gel THICKER where they gather. Colour alone barely
                // registers here - the normalised blend cancels most of the gain as the weight
                // grows - and a nutrient concentration should have body, not just a hue.
                // A cube standing here takes the habitat down to almost nothing in the middle of
                // its cell and leaves it only around the rim - so the block is never competed
                // with, and the zone is still legible as an underglow between the slots.
                float occlude = lerp(1.0, lerp(_OccupiedOpacity, _OccupiedEdge, edgeNess),
                    occupied);

                float alpha = saturate((_Opacity * (1.0 + dens * 0.4 + flow * _FlowThickness * calm)
                    * pulse + membrane + sheen * 0.4 + broadSheen * 0.3 * calm + feed * 0.35)
                    * inField * occlude);
                // Thinner over a seam, so the slot beneath shows through and the footprint reads.
                alpha *= 1.0 - seam * _CellSeam * inField;

                // ---- colour. Weights are normalised against their own total: every one of these
                // stacking unnormalised is how a layered material bleaches to white.
                // The seam only just tints toward depth now: the footprint is meant to be read
                // THROUGH the gel, not stamped onto it.
                float wDeep = saturate(-dens) * _DepthStrength + seam * 0.35;
                float wFlow = flow;
                float wGlow = saturate(sheen + broadSheen + feed + ripple);
                float wMem = saturate(membrane);
                float total = 1.0 + wDeep + wFlow + wGlow + wMem;
                float3 rgb = (_GelColor.rgb
                    + _DeepColor.rgb * wDeep
                    + _FlowColor.rgb * wFlow
                    + _GlowColor.rgb * wGlow
                    + _MembraneColor.rgb * wMem) / total;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
