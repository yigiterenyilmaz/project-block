// PURPOSE: The cell-space half of "Kangren" (NECROTIC TAKEOVER, GangreneView). One shader, three
// jobs, chosen per renderer with _Mode and fed through a MaterialPropertyBlock:
//
//   0 HEALTHY  the cube that is DYING, on its own tile. A necrotic FRONT crosses the face from the
//              side the rot came in on (_Axes turns the face so that side is always u = 0) and a
//              little further in from every edge than through the middle, so the last living
//              colour is in the centre. Just ahead of the front the life goes out of the material
//              - saturation, then the highlights (matte), then a cast of the rot's own colour -
//              and veins run ahead of it. Behind the front this layer simply gives its material
//              up (alpha), and the ROT layer underneath is what shows.
//   1 ROT      the dead cube UNDER it: the default tile tinted the rot colour by the vertex colour
//              (exactly as the board draws a gangrene cube) with the baked necrotic surface -
//              veins, hairline cracks, matte mottling - composited over it. Its cover LEADS the
//              healthy layer by a hair, so there is never a see-through seam at the front, and at
//              full front it is pixel for pixel what the board plus GangreneView's idle overlay
//              draws - so the hand-off at the end of the conversion shows nothing.
//   2 FLOOR    an EMPTY cell's floor being contaminated: the same front, creeping over the slot
//              from the source edge, with veins in it. The dead mass rises out of this.
//
// Two small textures, built once in code, a tile per variant: _LookTex is the BAKED surface (its
// alpha is coverage, its rgb the colour to blend to - the same over-blend the idle overlay sprite
// does, which is why they match), _DataTex the raw channels (R veins, G cracks, B mottling, A the
// front's own irregularity). Sampled in the TURNED face frame, so the veins really do come from
// the side the rot came from. Tagged Universal2D, as the 2D renderer requires. Without this
// shader GangreneView cross-fades the cube to the rot colour instead of running a front across it.
Shader "ProjectBlock/Gangrene"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _LookTex ("Baked necrotic surface (rgb colour, a coverage), a tile per variant", 2D) = "black" {}
        _DataTex ("Veins (R), cracks (G), mottling (B), front noise (A)", 2D) = "black" {}
        _Centre ("Face centre (world)", Vector) = (0, 0, 0, 0)
        _Half ("Face half-size (world)", Float) = 0.5
        _Tiles ("Variant tile, 1 / tile count", Vector) = (0, 1, 0, 0)
        _Axes ("Face turned so the source edge is u = 0 (2x2 rows)", Vector) = (1, 0, 0, 1)
        _Mode ("0 healthy, 1 rot, 2 floor", Float) = 0
        _Front ("How far the necrosis has come (0..1)", Float) = 0
        _Soft ("Front softness", Float) = 0.09
        _Creep ("How far the front follows the veins instead of running straight", Float) = 0.3
        _EdgeBias ("How much the front is edge-to-centre rather than straight from the source", Float) = 0.35
        _Drain ("Life drain strength", Range(0, 1)) = 0.9
        _DrainReach ("How far ahead of the front the life is already going", Float) = 0.45
        _Matte ("How far the highlights flatten", Range(0, 1)) = 0.8
        _VeinVis ("Vein visibility", Range(0, 1)) = 0.45
        _VeinAhead ("How far the veins run ahead of the front", Float) = 0.3
        _LookVis ("Necrotic surface visibility on the dead layer", Range(0, 1)) = 1
        _RotColour ("Dead tissue", Color) = (0.36, 0.39, 0.30, 1)
        _VeinColour ("Veins", Color) = (0.13, 0.15, 0.12, 1)
        _StainColour ("Contaminated floor", Color) = (0.15, 0.16, 0.14, 1)
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
            TEXTURE2D(_LookTex);
            SAMPLER(sampler_LookTex);
            TEXTURE2D(_DataTex);
            SAMPLER(sampler_DataTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _LookTex_ST;
                float4 _DataTex_ST;
                float4 _Centre;
                float _Half;
                float4 _Tiles;
                float4 _Axes;
                float _Mode;
                float _Front;
                float _Soft;
                float _Creep;
                float _EdgeBias;
                float _Drain;
                float _DrainReach;
                float _Matte;
                float _VeinVis;
                float _VeinAhead;
                float _LookVis;
                float4 _RotColour;
                float4 _VeinColour;
                float4 _StainColour;
            CBUFFER_END

            float Lum(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            // One variant's tile, clamped half a texel inside so a neighbour never bleeds in.
            float2 TileUV(float2 q)
            {
                float2 uv = clamp(q * 0.5 + 0.5, 0.0078125, 0.9921875);
                return float2((_Tiles.x + uv.x) * _Tiles.y, uv.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                // -1..1 across the face, in the frame the SOURCE edge sets: u = 0 is that edge.
                output.q = (world.xy - _Centre.xy) / max(_Half, 1e-4);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 fq = float2(dot(_Axes.xy, input.q), dot(_Axes.zw, input.q));
                float4 data = SAMPLE_TEXTURE2D(_DataTex, sampler_DataTex, TileUV(fq));

                // WHEN each pixel dies: from the source edge (u), and a little sooner at every
                // edge than through the middle, so the last living colour sits in the centre. The
                // 1.481 is 1 / 0.675, the largest the blend can reach, so the front runs 0..1.
                float u = saturate(dot(_Axes.xy, input.q) * 0.5 + 0.5);
                float box = max(abs(input.q.x), abs(input.q.y));
                float arrive = saturate(((1.0 - _EdgeBias) * u + _EdgeBias * (1.0 - box)) * 1.481
                    + (data.a - 0.5) * _Creep);

                // The healthy material still holds where the front has not reached; the dead layer
                // under it covers a hair EARLIER, so the two never leave a transparent seam.
                float keep = smoothstep(_Front - _Soft, _Front + _Soft, arrive);
                float cover = 1.0 - smoothstep(_Front + 0.5 * _Soft, _Front + 2.0 * _Soft, arrive);

                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 c = texel.rgb * input.color.rgb;
                float alpha = texel.a * input.color.a;
                int mode = (int)round(_Mode);

                if (mode == 1)
                {
                    // THE DEAD CUBE: the tile as the board tints it, with the baked necrotic
                    // surface over it - one lerp, the same one the idle overlay sprite blends.
                    float4 look = SAMPLE_TEXTURE2D(_LookTex, sampler_LookTex, TileUV(fq));
                    c = lerp(c, look.rgb, saturate(look.a * _LookVis));
                    return half4(c, saturate(alpha * cover));
                }
                if (mode == 2)
                {
                    // THE FLOOR of an empty cell, contaminated from the source edge: a dead stain
                    // with the mottling in it and veins through it.
                    float3 stain = _StainColour.rgb * (0.86 + 0.28 * data.b);
                    stain = lerp(stain, _VeinColour.rgb, saturate(data.r * _VeinVis * 1.2));
                    float dust = 0.72 + 0.28 * data.b;
                    return half4(stain, saturate(alpha * cover * dust));
                }

                // THE DYING CUBE. Just ahead of the front the life goes out of the material: the
                // colour first, then the highlights, then a cast of the rot's own tone. It never
                // brightens and it never flashes.
                float near = 1.0 - smoothstep(_Front + _Soft, _Front + max(_DrainReach, 0.05), arrive);
                float drain = _Drain * near;
                float lum = Lum(c);
                c = lerp(c, lum.xxx * 0.86, 0.62 * drain);
                c = lerp(c, min(c, lum * 1.02), _Matte * drain);
                c = lerp(c, _RotColour.rgb * (0.55 + 0.9 * lum), 0.42 * drain);
                // Veins running ahead of the front - the rot reaching into what is still alive.
                float ahead = 1.0 - smoothstep(_Front + _VeinAhead * 0.3, _Front + max(_VeinAhead, 0.02), arrive);
                c = lerp(c, _VeinColour.rgb, saturate(data.r * _VeinVis * ahead));
                // Hairline cracks open in the surface as it is taken, just before it gives way.
                float opening = (1.0 - smoothstep(_Front, _Front + _Soft * 2.0, arrive)) * near;
                c = lerp(c, _VeinColour.rgb * 0.7, saturate(data.g * opening));
                return half4(c, saturate(alpha * keep));
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
