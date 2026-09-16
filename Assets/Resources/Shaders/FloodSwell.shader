// PURPOSE: "Taşkın" - the SOURCE water heaving up as a LIQUID before it overflows: not the water cube
// scaled, but a body of water whose surface wobbles, rises into a dome, bulges into a lobe toward each
// cube it is about to flood, churns inside, froths at its surface and throws up bubbles.
//
// A scaled sprite is a block getting bigger; that is exactly what this replaced. The body is a signed
// distance field on a quad a couple of cells wide: the cube's own rounded box, smooth-unioned with a
// DOME rising out of its top and a LOBE per real target direction (_Dirs: right, left, down, up),
// its outline rippling with time. Inside, the water tile's own texture is sampled (churned); past the
// cube's edges - in the dome and the lobes - its interior colours are used, so the added water is
// the same water and not its frame. A pale meniscus just inside the surface, froth hugging the
// surface (heavier on top), and rings of bubbles rising through the body and popping near the top.
//
// _Amount 0..1 drives all of it, and at 0 the body IS the cube: the rounded box, the tile's own pixels
// and alpha, no dome, no lobes, no froth - so the hand-off to the board's own cube shows nothing.
// Positions are in CELLS: object space (a unit quad) times _Span. _WaterPivot/_WaterUnit map a cell
// offset into the water tile's UV (its body is one unit on a packed or unpacked texture alike).
Shader "ProjectBlock/FloodSwell"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WaterTex ("Water tile", 2D) = "white" {}
        _WaterPivot ("Water body centre (uv)", Vector) = (0.5, 0.5, 0, 0)
        _WaterUnit ("Water body unit (uv)", Vector) = (1, 1, 0, 0)
        _Span ("Quad size in cells", Float) = 2.2
        _Amount ("Amount", Range(0, 1)) = 0
        _Dirs ("Lobes (R, L, D, U)", Vector) = (0, 0, 0, 0)
        _Clock ("Clock", Float) = 0
        _Seed ("Seed", Float) = 0
        _Foam ("Foam colour", Color) = (0.93, 0.98, 1, 1)
        _Rim ("Meniscus colour", Color) = (0.80, 0.96, 0.98, 1)
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
                float2 local : TEXCOORD0;
            };

            TEXTURE2D(_WaterTex);
            SAMPLER(sampler_WaterTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _WaterPivot;
                float4 _WaterUnit;
                float _Span;
                float _Amount;
                float4 _Dirs;
                float _Clock;
                float _Seed;
                float4 _Foam;
                float4 _Rim;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.local = input.positionOS.xy;
                return output;
            }

            float SdRoundBox(float2 p, float h, float r)
            {
                float2 q = abs(p) - (h - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float Smin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            float Hash(float2 i, float s)
            {
                return frac(sin(dot(i, float2(127.1, 311.7)) + s * 74.7) * 43758.5453);
            }

            float Noise(float2 x, float s)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i, s);
                float b = Hash(i + float2(1, 0), s);
                float c = Hash(i + float2(0, 1), s);
                float d = Hash(i + float2(1, 1), s);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float a = _Amount;
                float2 cellPos = input.local * _Span;
                float2 p = cellPos - float2(0.0, 0.05 * a);

                float d = SdRoundBox(p, 0.49 + 0.03 * a, 0.06);
                if (a > 0.0001)
                {
                    // The DOME: the water heaving up out of its top.
                    float dome = length(float2(p.x * 0.9, p.y - (0.30 + 0.16 * a))) - 0.34 * a;
                    d = Smin(d, dome, 0.18 * a + 0.0001);
                    // A LOBE toward each cube it is about to flood.
                    float2 dirs[4] = { float2(1, 0), float2(-1, 0), float2(0, -1), float2(0, 1) };
                    for (int i = 0; i < 4; i++)
                    {
                        float lobeWeight = _Dirs[i];
                        if (lobeWeight > 0.0)
                        {
                            float reach = 0.44 + 0.22 * a;
                            float lobe = length(p - dirs[i] * reach) - 0.30 * a * lobeWeight;
                            d = Smin(d, lobe, 0.16 * a + 0.0001);
                        }
                    }
                    float ang = atan2(p.y, p.x);
                    d += a * 0.022 * (sin(ang * 5.0 + _Clock * 9.0 + _Seed) + 0.6 * sin(ang * 9.0 - _Clock * 13.0 + _Seed * 2.0));
                }
                float inside = saturate(-d / 0.012);
                if (inside <= 0.0)
                {
                    return half4(0, 0, 0, 0);
                }

                // The water itself: the tile's own pixels, churned; past the cube, its inner colours.
                float churn = a * 0.035;
                float2 wp = p + churn * float2(sin(p.y * 12.0 + _Clock * 8.0 + _Seed), cos(p.x * 10.0 + _Clock * 7.0));
                // Only water that is truly PAST the cube borrows the interior colours; the cube's own
                // frame is its own pixels, so at amount 0 nothing here differs from the board's cube.
                float outsideCube = step(0.495, max(abs(p.x), abs(p.y))) * step(0.0001, a);
                // As it heaves, the cube's hard frame melts into the body, so no rectangle is left
                // floating inside the water; at amount 0 this is 0 and the frame is exact.
                float frameMelt = smoothstep(0.40, 0.49, max(abs(p.x), abs(p.y))) * a;
                // Folded smoothly into the tile's interior (a clamp there drew hard seams in the body).
                wp = lerp(wp, 0.36 * sin(wp * 2.2), max(outsideCube, frameMelt));
                half4 water = SAMPLE_TEXTURE2D(_WaterTex, sampler_WaterTex, _WaterPivot.xy + wp * _WaterUnit.xy);
                float3 col = water.rgb * input.color.rgb * _Color.rgb;
                float alpha = inside * lerp(water.a, 1.0, outsideCube);

                // Meniscus.
                float rim = saturate(1.0 - abs(d + 0.02) / 0.02) * a;
                col = lerp(col, _Rim.rgb, 0.6 * rim);
                // Froth hugging the surface, heavier on top.
                float fn = Noise(float2(cellPos.x, cellPos.y - _Clock * 1.2) * 14.0, _Seed) * 0.6
                    + Noise(float2(cellPos.x, cellPos.y - _Clock * 2.0) * 29.0, _Seed + 3.0) * 0.4;
                float near = saturate(1.0 - (-d) / (0.08 + 0.07 * a));
                float top = saturate((p.y + 0.1) / 0.5);
                float foam = saturate((fn - (0.58 - 0.24 * a)) / 0.12) * near * (0.55 + 0.45 * top) * a;
                // Bubbles rising through the body, popping at the top.
                float2 g = float2(cellPos.x, cellPos.y - _Clock * 0.9) * 6.0;
                float2 cellId = floor(g);
                float2 centre = cellId + 0.25 + 0.5 * float2(Hash(cellId, _Seed + 11.0), Hash(cellId, _Seed + 13.0));
                float radius = 0.10 + 0.14 * Hash(cellId, _Seed + 17.0);
                float ring = saturate(1.0 - abs(length(g - centre) - radius) / 0.05)
                    * step(Hash(cellId, _Seed + 9.0), 0.25 + 0.35 * a);
                float bubble = ring * a * step(0.03, -d);
                col = lerp(col, _Foam.rgb, foam);
                col = lerp(col, _Foam.rgb, 0.7 * bubble);
                return half4(saturate(col), alpha * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
