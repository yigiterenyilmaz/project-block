// PURPOSE: The sprite shader "Soğuk süblimleşme" (CryoSublimationView) draws a removed cube with,
// per renderer through a MaterialPropertyBlock:
//
//   THERMAL DRAIN  _Drain kills the highlights, calms the contrast toward the face's own average
//                  and takes a little saturation - still the cube's own colour, no blue yet.
//   FROST          _MaskTex's red channel says, pixel by pixel, WHEN the frost arrives (corners,
//                  edges and a few irregular patches first, the last warm core last); _Frost is
//                  how far it has come. Frosted material is matte pale steel that keeps the
//                  bevel's relative shading - frosted, never glass. _CoreFade dims what is still
//                  warm just before the frost takes it.
//   SUBLIMATION    the green channel says when each pixel's MASS goes (a few large soft lobes);
//                  _Erode is how far the erosion has come, so the silhouette really loses
//                  material instead of fading. _Origins pales the first places it will take.
//   SHELL          where the mass has gone, _Shell leaves a thin pale rim along the cube's own
//                  silhouette and next to nothing inside - hollow, not glass.
//   COLLAPSE       _Inset draws each side in toward the middle by its own amount.
//
// Both masks live in one small texture built once in code, a tile per variant, smooth values
// so every edge is soft at any zoom; the pattern is fixed per cell and never boils. Tagged
// Universal2D, as the 2D renderer requires. Without this shader CryoSublimationView tints the
// cube to frost and fades it instead.
Shader "ProjectBlock/CryoSublimation"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Frost (R) and erosion (G) arrival, a tile per variant", 2D) = "black" {}
        _Centre ("Cube centre (world)", Vector) = (0, 0, 0, 0)
        _Half ("Cube half-size (world)", Float) = 0.5
        _Tiles ("Frost tile, erosion tile, 1 / tile count", Vector) = (0, 0, 1, 0)
        _FrostAxes ("Frost mask orientation (2x2 rows)", Vector) = (1, 0, 0, 1)
        _ErodeFlip ("Erosion mask mirror", Float) = 1
        _Drain ("Thermal drain", Range(0, 1)) = 0
        _Frost ("Frost front (share of the face frosted)", Float) = -1
        _FrostSoft ("Frost edge softness", Float) = 0.07
        _FrostAmount ("How far the frosted material goes", Range(0, 1)) = 0.9
        _Retention ("Original colour kept in the frost", Range(0, 1)) = 0.12
        _CoreFade ("The last warm core dying", Range(0, 1)) = 0
        _Origins ("Sublimation origins", Range(0, 1)) = 0
        _Erode ("Erosion front (share of the mass gone)", Float) = -1
        _ErodeSoft ("Erosion edge softness", Float) = 0.05
        _Shell ("Frost shell", Range(0, 1)) = 0
        _ShellOpacity ("Frost shell opacity", Range(0, 1)) = 0.3
        _Inset ("Collapse inset: right, up, left, down (world)", Vector) = (0, 0, 0, 0)
        _FrostColour ("Frosted material", Color) = (0.58, 0.64, 0.72, 1)
        _HollowColour ("Inside the empty shell", Color) = (0.05, 0.06, 0.09, 1)
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
                float2 q : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MaskTex_ST;
                float4 _Centre;
                float _Half;
                float4 _Tiles;
                float4 _FrostAxes;
                float _ErodeFlip;
                float _Drain;
                float _Frost;
                float _FrostSoft;
                float _FrostAmount;
                float _Retention;
                float _CoreFade;
                float _Origins;
                float _Erode;
                float _ErodeSoft;
                float _Shell;
                float _ShellOpacity;
                float4 _Inset;
                float4 _FrostColour;
                float4 _HollowColour;
            CBUFFER_END

            float Lum(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            // One variant's tile of the mask, clamped half a texel inside so a neighbouring
            // tile never bleeds in.
            float2 MaskUV(float2 q, float tile)
            {
                float2 uv = clamp(q * 0.5 + 0.5, 0.0078125, 0.9921875);
                return float2((tile + uv.x) * _Tiles.z, uv.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                // -1..1 across the cube's body, taken BEFORE the collapse so the shell's
                // pattern goes in with its sides.
                float2 q = (world.xy - _Centre.xy) / max(_Half, 1e-4);
                // The empty shell losing its hold: each side drawn in by its own amount -
                // structure giving way, not a squash.
                world.x -= q.x > 0.0 ? _Inset.x * q.x : _Inset.z * q.x;
                world.y -= q.y > 0.0 ? _Inset.y * q.y : _Inset.w * q.y;
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.q = q;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 tint = input.color.rgb;
                float3 c = texel.rgb * tint;
                // The face's own average across its middle: what the drain calms toward.
                float3 mean = 0;
                [unroll] for (int gy = 0; gy < 3; gy++)
                {
                    [unroll] for (int gx = 0; gx < 3; gx++)
                    {
                        mean += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                            float2(0.3 + 0.2 * gx, 0.3 + 0.2 * gy)).rgb;
                    }
                }
                mean = mean / 9.0 * tint;
                float lumC = Lum(c);
                float lumM = max(Lum(mean), 0.02);

                // THERMAL DRAIN: the life goes out of the material before any cold shows.
                float3 drained = lerp(c, mean + (c - mean) * 0.7, _Drain);
                drained -= max(Lum(drained) - lumM, 0.0) * 0.5 * _Drain;
                drained = lerp(drained, Lum(drained).xxx, 0.2 * _Drain);
                drained *= 1.0 - 0.08 * _Drain;

                // FROST, in the order the mask gives.
                float2 fq = float2(dot(_FrostAxes.xy, input.q), dot(_FrostAxes.zw, input.q));
                float arrive = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, MaskUV(fq, _Tiles.x)).r;
                float frost = smoothstep(arrive - _FrostSoft, arrive + _FrostSoft, _Frost);
                // Matte pale steel that keeps the bevel's relative shading: the volume reads.
                float shade = clamp(pow(max(lumC, 1e-4) / lumM, 0.5), 0.6, 1.25);
                float3 frosted = _FrostColour.rgb * shade;
                float3 kept = drained * min(Lum(frosted) / max(Lum(drained), 0.02), 4.0);
                frosted = lerp(frosted, kept, _Retention);
                frosted = lerp(drained, frosted, _FrostAmount);
                // What is still warm dies a little before the frost takes it.
                float3 warm = lerp(drained, Lum(drained).xxx * 0.9, 0.55 * _CoreFade);
                float3 col = lerp(warm, frosted, frost);
                // A breath of cold scattering right at the front, no more.
                col += _FrostColour.rgb * 0.06 * (4.0 * frost * (1.0 - frost));

                // SUBLIMATION: the mass goes where the erosion has reached.
                float2 eq = float2(input.q.x * _ErodeFlip, input.q.y);
                float leave = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, MaskUV(eq, _Tiles.y)).g;
                float gone = smoothstep(leave - _ErodeSoft, leave + _ErodeSoft, _Erode);
                float origin = _Origins * (1.0 - smoothstep(0.015, 0.085, leave));
                col = lerp(col, _FrostColour.rgb * 1.25, 0.45 * origin);
                // The frost thinning just before it goes: a pale fringe on the front.
                float thin = smoothstep(leave - 2.5 * _ErodeSoft, leave - 0.5 * _ErodeSoft, _Erode) * (1.0 - gone);
                col += _FrostColour.rgb * 0.18 * thin;
                float mass = texel.a * (1.0 - gone);

                // Where it has gone: for a moment a faint pale haze - the solid just turned to
                // vapour - then only THE SHELL, a thin pale rim along the silhouette with next to
                // nothing inside.
                float haze = 0.2 * (1.0 - smoothstep(0.0, 0.22, _Erode - leave - _ErodeSoft));
                float box = max(abs(input.q.x), abs(input.q.y));
                float rim = smoothstep(0.72, 0.96, box);
                float shell = _Shell * _ShellOpacity * lerp(0.22, 1.0, rim);
                float3 shellCol = lerp(_HollowColour.rgb, _FrostColour.rgb * 1.08, lerp(0.2, 1.0, rim));
                float3 emptyCol = (_FrostColour.rgb * 1.1 * haze + shellCol * shell) / max(haze + shell, 1e-4);
                float empty = texel.a * gone * saturate(haze + shell);

                float a = mass + empty;
                float3 rgb = (col * mass + emptyCol * empty) / max(a, 1e-4);
                return half4(rgb, saturate(a) * input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
