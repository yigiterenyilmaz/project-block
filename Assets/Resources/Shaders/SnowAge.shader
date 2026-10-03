// PURPOSE: The SNOW tile's material - one shared material, every cube's state in a
// MaterialPropertyBlock (SnowView). Snow has to read as a MATERIAL that lives on the board: it ages
// toward melting, an avalanche packs it, a merge refreshes it - and none of that is a number on it.
//
// What each property does, in tile uv (the snow tile is its own texture, uv 0..1 over the body):
//
//   _Age    0 FRESH (5 turns) .. 1 COLLAPSING (1 turn). Desaturates and cools the face a little,
//           opens soft blue-grey WET POCKETS out of value noise, takes the powder brightness off the
//           rim and, past 0.75, cuts one or two thin wet channels. The rim recedes a few percent,
//           IRREGULARLY (noise on the edge), so a cube one turn from melting is visibly smaller at
//           the corners - never uniformly scaled.
//   _Melt   0..1 the final melt: the centre goes pale-blue and wet, the outline eats inward along
//           the noise (so it shrinks toward the middle irregularly) and the last fifth fades.
//   _Packed 0..1 snow an avalanche laid: fine pressure GROOVES across the flow, less fluffy edge.
//   _Press  0..1 the pressure of an avalanche about to go: deeper grooves and pockets.
//   _Seam   0..1 the seam between two layers of one avalanche, a 1-2 px pale blue-grey line on the
//           edge facing the flow (_BlockFlowDir, the board's gravity, a shader global).
//   _Sweep  xy origin (uv), z radius, w strength - the cooling sweep of a refresh, a soft band.
//   _Glow   aim: eligible snow brightens.   _Mute aim: spent snow greys a little.
//   _Seed   per-cube variation, so no two cubes wear the same pockets.
//
// Light is never added as a flat wash: every change lerps a pixel toward its own cooler, wetter or
// brighter colour. Without the shader the tile draws plain (Sprites/Default) and SnowView tints it.
Shader "ProjectBlock/SnowAge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Age ("Age", Range(0, 1)) = 0
        _Melt ("Melt", Range(0, 1)) = 0
        _Packed ("Packed", Range(0, 1)) = 0
        _Press ("Press", Range(0, 1)) = 0
        _Seam ("Seam", Range(0, 1)) = 0
        _Sweep ("Sweep (uv, radius, strength)", Vector) = (0.5, 0.5, 0, 0)
        _Glow ("Glow", Range(0, 1)) = 0
        _Mute ("Mute", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
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

            float4 _BlockFlowDir;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Age;
                float _Melt;
                float _Packed;
                float _Press;
                float _Seam;
                float4 _Sweep;
                float _Glow;
                float _Mute;
                float _Seed;
            CBUFFER_END

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
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                    lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float RoundedBox(float2 p, float h, float r)
            {
                float2 q = abs(p) - (h - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color * _Color;
                float3 col = texel.rgb;
                float2 p = uv - 0.5;
                float2 seed = float2(_Seed * 1.37, _Seed * 2.91);
                float lum = dot(col, float3(0.299, 0.587, 0.114));

                // ---- the outline: recession with age, eaten inward by the melt, both irregular
                float edgeNoise = Noise(uv * 7.0 + seed) * 0.6 + Noise(uv * 17.0 + seed * 1.7) * 0.4;
                float recede = _Age * 0.03 * (0.6 + 0.8 * edgeNoise)
                    + _Melt * 0.5 * (0.7 + 0.6 * edgeNoise);
                float sdf = RoundedBox(p, 0.5 - recede, 0.08 + recede * 0.6);
                float inside = saturate(-sdf / 0.012);

                // ---- age: cooler, a little greyer, the powder rim going flat
                float3 cool = float3(0.90, 0.95, 1.02);
                float3 aged = lerp(float3(lum, lum, lum), col, 0.82) * cool;
                col = lerp(col, aged, saturate(_Age * 0.75 + _Melt * 0.5));
                col *= 1.0 - _Age * 0.05;
                float rim = saturate(1.0 - (-sdf) / 0.09);
                col = lerp(col, col * 1.08 + 0.03, rim * (1.0 - _Age) * (1.0 - _Packed) * 0.5);

                // ---- wet pockets, opening as it ages, and the channels of the last turn
                float pocket = Noise(uv * 5.0 + seed * 3.1);
                float wet = smoothstep(0.66, 0.78, pocket) * smoothstep(0.25, 0.9, _Age + _Melt);
                float3 wetBlue = float3(0.63, 0.74, 0.86);
                col = lerp(col, wetBlue * (0.9 + lum * 0.15), wet * 0.42);
                // a hair of light on each pocket's upper-left lip, so it reads as a hollow
                float lip = smoothstep(0.62, 0.66, Noise((uv + float2(0.012, -0.012)) * 5.0 + seed * 3.1))
                    * (1.0 - smoothstep(0.66, 0.70, pocket));
                col += lip * wet * 0.05;
                float channel = 1.0 - smoothstep(0.0, 0.035,
                    abs(frac(uv.x * 2.3 + Noise(uv * 3.0 + seed) * 0.9 + _Seed * 0.13) - 0.5));
                col = lerp(col, wetBlue, channel * smoothstep(0.75, 1.0, _Age) * 0.35 * (1.0 - rim));

                // ---- packed and pressed: grooves ACROSS the flow (lines along the across axis)
                float2 flow = _BlockFlowDir.xy;
                if (dot(flow, flow) < 0.5) { flow = float2(0, -1); }
                float along = dot(p, flow);
                float grooves = smoothstep(0.80, 1.0, sin(along * 46.0 + Noise(uv * 4.0 + seed) * 3.0) * 0.5 + 0.5);
                float pack = saturate(_Packed * 0.55 + _Press);
                col = lerp(col, float3(0.66, 0.74, 0.84) * (0.85 + lum * 0.2), grooves * pack * 0.32);
                col = lerp(col, wetBlue, smoothstep(0.7, 0.8, pocket) * _Press * 0.35);

                // ---- the seam on the flow-facing edge: the boundary between two strata
                float seamLine = smoothstep(0.455, 0.475, along) * (1.0 - smoothstep(0.495, 0.5, along));
                col = lerp(col, float3(0.60, 0.68, 0.79), seamLine * _Seam * 0.85);

                // ---- the melt's wet centre
                float centre = exp(-dot(p, p) / 0.05);
                col = lerp(col, wetBlue * 1.02, centre * smoothstep(0.0, 0.6, _Melt) * 0.55);

                // ---- a refresh: a soft cold band running out from where the fresh snow landed
                float r = distance(uv, _Sweep.xy);
                float band = exp(-pow(r - _Sweep.z, 2.0) / 0.006) * _Sweep.w;
                col = lerp(col, float3(0.95, 0.98, 1.0), band * 0.6);

                // ---- aim
                col = col * (1.0 + _Glow * 0.10) + _Glow * 0.03;
                col = lerp(col, float3(lum, lum, lum) * 0.86, _Mute * 0.45);

                float alpha = texel.a * inside * (1.0 - smoothstep(0.78, 1.0, _Melt));
                return half4(saturate(col), alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
