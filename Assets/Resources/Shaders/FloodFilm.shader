// PURPOSE: "Taşkın" - an ordinary cube being DROWNED: a thin water film crosses it from the side(s)
// the water came in on, and under the film the cube's own face refracts, loses its colour and
// contrast, and starts to run, until the water tile underneath takes over.
//
// THE FILM IS PER SIDE. _Film holds how far a film has crossed from the LEFT (x), RIGHT (y), BOTTOM
// (z) and TOP (w) edge, 0..1 (a negative value is "no water from that side"). Coverage is the max
// of the four, so two films are ONE mask and they meet in the middle instead of stacking two
// transformations; each front is a low-frequency wave, never a straight wipe, and carries a pale
// line that dies where another film already covers.
//
// UNDER THE FILM the cube is sampled a pixel or so off (refraction, slow), then desaturated, then its
// contrast is pressed, and only THEN does the water's colour lie over it - a straight blue tint over
// the cube is the one thing this must not be. _Liquefy lets the covered face run a few pixels down
// and sideways, as colour dissolving in water rather than wax melting.
//
// Coordinates are the sprite's OBJECT space (one unit per cube body, pivot in the middle); _UvPerLocal
// turns an object-space offset into the tile's UV, because a packed tile's UV is a sub-rect.
// Without this shader the view still plays the flood: the old cube simply fades under the water.
Shader "ProjectBlock/FloodFilm"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Film ("Film per side (L, R, B, T)", Vector) = (-1, -1, -1, -1)
        _Submerge ("Submerge", Range(0, 1)) = 0
        _Liquefy ("Liquefy", Range(0, 1)) = 0
        _Clock ("Clock", Float) = 0
        _Seed ("Seed", Float) = 0
        _Px ("Object units per screen pixel", Float) = 0.01
        _UvPerLocal ("UV per object unit", Vector) = (1, 1, 0, 0)
        _Water ("Film colour", Color) = (0.45, 0.82, 0.88, 1)
        _Edge ("Front colour", Color) = (0.82, 0.97, 0.98, 1)
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
                float2 local : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _Film;
                float _Submerge;
                float _Liquefy;
                float _Clock;
                float _Seed;
                float _Px;
                float4 _UvPerLocal;
                float4 _Water;
                float4 _Edge;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.local = input.positionOS.xy;
                return output;
            }

            void Side(float prog, float d, float along, inout float cov, inout float front, float others)
            {
                if (prog < 0.0)
                {
                    return;
                }
                float wave = 0.035 * sin(along * 9.0 + _Seed + _Clock * 3.0)
                    + 0.02 * sin(along * 17.0 - _Seed * 2.0);
                float edge = prog * 1.08 - d + wave;
                float c = saturate(edge / 0.03);
                cov = max(cov, c);
                float f = saturate(1.0 - abs(edge) / 0.03) * step(0.001, prog) * step(prog, 1.04);
                front = max(front, f);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.local;
                float2 q = p + 0.5;
                float cov = 0.0;
                float front = 0.0;
                Side(_Film.x, q.x, q.y, cov, front, 0.0);
                Side(_Film.y, 1.0 - q.x, q.y, cov, front, 0.0);
                Side(_Film.z, q.y, q.x, cov, front, 0.0);
                Side(_Film.w, 1.0 - q.y, q.x, cov, front, 0.0);
                // A front dies where the water has already come in from another side.
                float inside = saturate(cov * 1.6 - 0.6);
                front *= 1.0 - inside;

                // Refraction under the film, then the colour running as the solid dissolves.
                float2 off = cov * _Px * 1.2 * float2(sin(p.y * 20.0 + _Clock * 4.0 + _Seed),
                    cos(p.x * 18.0 + _Clock * 3.5));
                off += _Liquefy * cov * _Px * 4.0 * float2(0.35 * sin(p.y * 6.0 + _Seed), -1.0);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + off * _UvPerLocal.xy)
                    * input.color * _Color;

                float s = _Submerge * cov;
                float lum = dot(tex.rgb, float3(0.2126, 0.7152, 0.0722));
                float3 col = lerp(tex.rgb, lum.xxx, 0.6 * s);              // colour goes first
                col = (col - 0.45) * (1.0 - 0.4 * s) + 0.45;               // then contrast
                float filmA = cov * lerp(0.22, 0.55, _Submerge);            // then the water lies over
                col = lerp(col, _Water.rgb, filmA);
                col = lerp(col, _Edge.rgb, front * 0.7);
                return half4(saturate(col), tex.a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
