// PURPOSE: "Antimadde" - the LIGHT the annihilation releases: the energy rays out of each hero
// cell, the crown's blades, the column, the tension filaments and the payoff's trail
// (AntimatterBlastView). A photon discharge, never a laser and never electricity.
//
// A straight constant-width line in one colour is a laser whatever colour it is, so the cross
// section is FOUR layers out of the one quad:
//
//     BLOOM   a wide soft outer falloff (_Shape.w)
//     BODY    the ray proper, violet (or the element's colour near the root, _RootColor)
//     CORE    a narrow near-white LAVENDER centre - never white (_CoreColor, capped below)
//     FRINGE  a thin chromatic edge, pale cyan one side and magenta the other
//
// and along its length it TAPERS (_Shape.y is the width at the tip), wanders a little in width, and
// is turbulent: a slow grain runs down it and a few dim BREAKS interrupt it (_Shape.z). Nothing
// zig-zags - the ray's path is straight, only its light is broken.
//
// The mesh runs x 0..1 from root to tip and y across; uv.x is along, uv.y across (0..1). _Params
// grows it from the root (x), cuts it back from the root outward as it retracts (y), sets its
// strength (z) and its seed (w). A strip mesh (a filament) uses the same convention, and its vertex
// colour carries its own broken sections. Additive: this is light on a dark board.
Shader "ProjectBlock/AntimatterBeam"
{
    Properties
    {
        _BodyColor ("body", Color) = (0.58, 0.32, 1, 1)
        _CoreColor ("core", Color) = (0.92, 0.86, 1, 1)
        _RootColor ("root colour (a = share of the length it covers)", Color) = (0, 0, 0, 0)
        _FringeA ("fringe, one side", Color) = (0.45, 0.9, 1, 1)
        _FringeB ("fringe, the other", Color) = (1, 0.35, 0.85, 1)
        _Params ("grow, retract, strength, seed", Vector) = (1, 0, 1, 0)
        _Shape ("core width, tip width, turbulence, bloom", Vector) = (0.14, 0.25, 0.6, 0.5)
        _Aspect ("length over width", Float) = 8
        _Clock ("clock", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Cull Off
        ZWrite Off
        Blend One One

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

            CBUFFER_START(UnityPerMaterial)
                float4 _BodyColor;
                float4 _CoreColor;
                float4 _RootColor;
                float4 _FringeA;
                float4 _FringeB;
                float4 _Params;
                float4 _Shape;
                float _Aspect;
                float _Clock;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y * 2.0 - 1.0;
                float seed = _Params.w;

                // Where the ray exists along its length: grown from the root, retracting from it,
                // and soft at both ends so it never ends in a hard cap.
                float grow = _Params.x;
                float retract = _Params.y;
                float along = smoothstep(retract, retract + 0.06, u)
                    * (1.0 - smoothstep(grow - 0.2, grow, u));

                // Tapered, and a little irregular in width - never a ruler line.
                float w = lerp(1.0, _Shape.y, pow(saturate(u), 0.85));
                w *= 1.0 + 0.10 * sin(u * 11.0 + seed * 6.283) + 0.06 * sin(u * 29.0 + seed * 3.1);
                float x = abs(v) / max(w, 0.02);

                float coreW = max(_Shape.x, 0.01);
                float core = exp(-(x * x) / (coreW * coreW));
                float body = exp(-(x * x) / 0.09);
                float bloom = exp(-(x * x) * 1.6) * _Shape.w;
                // The chromatic edge hugs the body's own edge, one side cyan and the other magenta.
                float fr = exp(-pow((x - 0.34) / 0.07, 2.0));
                float3 fringe = lerp(_FringeB.rgb, _FringeA.rgb, step(0.0, v)) * fr * 0.55;

                // Turbulent: a grain running down it and a few dim breaks. Never a zig-zag.
                float grain = Noise(float2(u * _Aspect * 0.9 - _Clock * 5.0 + seed * 17.0, v * 1.5));
                float breaks = smoothstep(0.72, 0.9, Noise(float2(u * _Aspect * 0.28 + seed * 9.0, 0.5)));
                float turb = 1.0 - _Shape.z * (0.35 * grain + 0.55 * breaks);

                float3 bodyCol = lerp(_RootColor.rgb, _BodyColor.rgb,
                    saturate(u / max(_RootColor.a, 0.001)));
                float3 rgb = bodyCol * (body * 0.85 + bloom * 0.5) + _CoreColor.rgb * core + fringe;
                rgb *= along * max(turb, 0.0) * _Params.z * input.color.rgb * input.color.a;
                // Near-white LAVENDER at the very most: this light is never white.
                rgb = min(rgb, float3(0.95, 0.9, 1.0));
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
