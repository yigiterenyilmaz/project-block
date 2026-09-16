// PURPOSE: "Taşkın" - the SOURCE water swelling up as a LIQUID before it overflows: EVENLY on every
// side, BOILING - bubbles swelling out of its surface all round and sinking back, bubbles rising
// through it, froth at its rim - and only then a short lobe toward each cube it floods (_Spill).
//
// Two passes are buried here. A scaled sprite is a block getting bigger, not water. The next pass was a
// dome out of the top and a lobe that grew with the swell, and that read as IRREGULAR - a lump heaving
// to one side. The designer wants a pillow: the cube's rounded box grows the same amount on all four
// sides with its corners rounding off, its outline bubbling everywhere at once, and the overflow is a
// separate beat afterwards (_Spill, _Dirs: right, left, down, up). Inside, the water tile's own texture is sampled (churned); past the
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
        _Spill ("Overflow lobe", Range(0, 1)) = 0
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
                float _Spill;
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
                float2 p = cellPos;

                // A pillow: the same growth on every side, the corners rounding off as it swells.
                float h = 0.49 + 0.12 * a;
                float d = SdRoundBox(p, h, 0.06 + 0.12 * a);
                if (a > 0.0001)
                {
                    // BOILING: bubbles swelling out of the surface all the way round and sinking back.
                    for (int k = 0; k < 12; k++)
                    {
                        float kk = (float)k;
                        float ang = Hash(float2(kk, 3.0), _Seed) * 6.2832 + _Clock * 0.6 * (Hash(float2(kk, 5.0), _Seed) - 0.5);
                        float rate = 2.2 + 1.6 * Hash(float2(kk, 7.0), _Seed);
                        float ph = frac(_Clock * rate + Hash(float2(kk, 9.0), _Seed));
                        float size = sin(ph * 3.14159) * (0.045 + 0.035 * Hash(float2(kk, 11.0), _Seed)) * a;
                        float2 dv = float2(cos(ang), sin(ang));
                        float2 onEdge = dv / max(abs(dv.x), abs(dv.y)) * (h - 0.02);
                        d = Smin(d, length(p - onEdge) - size, 0.05);
                    }
                    d += a * 0.008 * sin(atan2(p.y, p.x) * 6.0 + _Clock * 7.0 + _Seed);
                }
                if (_Spill > 0.0001)
                {
                    // THE OVERFLOW, afterwards: a short lobe toward each cube it floods.
                    float2 dirs[4] = { float2(1, 0), float2(-1, 0), float2(0, -1), float2(0, 1) };
                    for (int i = 0; i < 4; i++)
                    {
                        if (_Dirs[i] > 0.0)
                        {
                            float lobe = length(p - dirs[i] * (h + 0.06)) - 0.24 * _Spill;
                            d = Smin(d, lobe, 0.12);
                        }
                    }
                }
                float inside = saturate(-d / 0.012);
                if (inside <= 0.0)
                {
                    return half4(0, 0, 0, 0);
                }

                // The water itself: the tile's own pixels, churned; past the cube, its inner colours.
                float churn = a * 0.03;
                float2 wp = p + churn * float2(sin(p.y * 12.0 + _Clock * 10.0 + _Seed), cos(p.x * 10.0 + _Clock * 9.0));
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
                // Froth all round the rim, evenly.
                float fn = Noise(float2(cellPos.x, cellPos.y - _Clock * 1.5) * 14.0, _Seed) * 0.6
                    + Noise(float2(cellPos.x, cellPos.y + _Clock * 1.1) * 29.0, _Seed + 3.0) * 0.4;
                float near = saturate(1.0 - (-d) / (0.07 + 0.06 * a));
                float foam = saturate((fn - (0.60 - 0.22 * a)) / 0.12) * near * a;
                // Bubbles boiling up through the body.
                float2 g = float2(cellPos.x, cellPos.y - _Clock * 1.6) * 7.0;
                float2 cellId = floor(g);
                float2 centre = cellId + 0.25 + 0.5 * float2(Hash(cellId, _Seed + 11.0), Hash(cellId, _Seed + 13.0));
                float radius = 0.10 + 0.14 * Hash(cellId, _Seed + 17.0);
                float ring = saturate(1.0 - abs(length(g - centre) - radius) / 0.05)
                    * step(Hash(cellId, _Seed + 9.0), 0.35 + 0.4 * a);
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
