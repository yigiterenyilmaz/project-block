// PURPOSE: The line-space half of "Kangren" (NECROTIC TAKEOVER, GangreneView). One shader over a
// plain quad whose uv.x runs ALONG a row or column and uv.y ACROSS it, three jobs by _Mode, all
// fed per renderer through a MaterialPropertyBlock:
//
//   0 BAND   the DEAD LINE's permanent underlay: a rounded band of dead stone with its own grain,
//            a few cracks and an inner shadow, so a line the rot took whole reads as a scar the
//            board carries rather than as a tint. It is swept IN by the same front that puts the
//            line out (_Along, centre-out or end to end) and then simply stays.
//   1 PATH   the necrotic PRESSURE moving from a dead line toward the nearer edge: a vein-like
//            streak whose head travels (_Along), whose tail fades behind it, and which thins as it
//            goes. One per cube the jump is about to take, so what is coming is legible.
//   2 SWEEP  the life going out of a line: a soft dark front travelling along it, over the cells.
//            Narrow, low, and gone the moment it has passed - the cells' own wash is what stays.
//
// The grain and the cracks are procedural in CELL units, so a five-cell band and a nine-cell band
// have the same material and no texture has to be baked per length. Nothing here glows, sparkles
// or moves on its own: every value comes from _Along. Tagged Universal2D, as the 2D renderer
// requires. Without this shader GangreneView draws the band as a flat rounded sprite and skips
// the path and the sweep.
Shader "ProjectBlock/GangreneStreak"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Mode ("0 band, 1 path, 2 sweep", Float) = 0
        _Span ("Half length, half width, corner radius, feather (cells)", Vector) = (2.5, 0.5, 0.18, 0.05)
        _Along ("Sweep front / travelling head, 0..1", Float) = 1
        _Soft ("Front softness", Float) = 0.12
        _CentreOut ("1: the front runs from the middle both ways. 0: end to end", Float) = 1
        _Opacity ("Overall opacity", Range(0, 1)) = 0.5
        _Grain ("How strongly the grain reads", Range(0, 1)) = 0.5
        _CrackVis ("Crack visibility", Range(0, 1)) = 0.4
        _ShadowStrength ("Inner shadow", Range(0, 1)) = 0.5
        _VeinStrength ("Vein detail in a travelling path", Range(0, 1)) = 0.5
        _Tail ("How far a path's tail reaches behind its head", Float) = 0.55
        _Seed ("Per-line seed, so no two bands have the same grain", Float) = 0
        _BandColour ("Dead stone", Color) = (0.22, 0.21, 0.19, 1)
        _VeinColour ("Veins, cracks", Color) = (0.11, 0.12, 0.10, 1)
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
                float _Mode;
                float4 _Span;
                float _Along;
                float _Soft;
                float _CentreOut;
                float _Opacity;
                float _Grain;
                float _CrackVis;
                float _ShadowStrength;
                float _VeinStrength;
                float _Tail;
                float _Seed;
                float4 _BandColour;
                float4 _VeinColour;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45) + _Seed * 0.137);
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            /// Thin ridges: what a hairline crack in dead tissue is, at a given scale.
            float Cracks(float2 p)
            {
                float ridge = 1.0 - abs(2.0 * Noise(p) - 1.0);
                float thin = smoothstep(0.93, 1.0, ridge);
                // In patches rather than evenly all over - dead stone does not craze uniformly.
                return thin * smoothstep(0.35, 0.75, Noise(p * 0.31 + 11.3));
            }

            float RoundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 d = abs(p) - (halfSize - radius);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
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
                // Along the line and across it, both in CELLS, so grain and cracks are the same
                // size whatever the line's length.
                float along = input.uv.x;
                float across = input.uv.y;
                float2 p = float2((along - 0.5) * 2.0 * _Span.x, (across - 0.5) * 2.0 * _Span.y);
                int mode = (int)round(_Mode);
                float3 col = _BandColour.rgb;
                float alpha = 0;

                if (mode == 0)
                {
                    // THE DEAD LINE'S BAND: rounded, grained, cracked, with the light off it at
                    // its own inner edge - a scar in the board, not a wash over it.
                    float sd = RoundedBox(p, float2(_Span.x, _Span.y), _Span.z);
                    float inside = saturate(-sd / max(_Span.w, 1e-4));
                    float grain = Noise(p * 5.5) * 0.7 + Noise(p * 13.0) * 0.3;
                    col *= lerp(1.0 - 0.45 * _Grain, 1.0 + 0.3 * _Grain, grain);
                    col = lerp(col, _VeinColour.rgb, saturate(Cracks(p * 7.0) * _CrackVis));
                    // Inner shadow: the band sits BELOW the board's face.
                    float lip = 1.0 - saturate(-sd / max(_Span.z * 1.6, 1e-4));
                    col *= 1.0 - _ShadowStrength * 0.55 * lip * lip;
                    // Swept in by the front that put the line out, then it stays.
                    float d = _CentreOut > 0.5 ? abs(along - 0.5) * 2.0 : along;
                    float k = 1.0 - smoothstep(_Along - _Soft, _Along + _Soft, d);
                    alpha = inside * _Opacity * k;
                }
                else if (mode == 1)
                {
                    // THE PRESSURE PATH: a vein pushing from the dead line to the edge, thinning
                    // as it goes, its tail dying behind the head.
                    float head = _Along;
                    float body = smoothstep(head - _Tail, head - _Tail * 0.45, along)
                        * (1.0 - smoothstep(head - 0.06, head, along));
                    float wobble = 0.16 * sin(along * 7.3 + _Seed) + 0.08 * sin(along * 17.1 + _Seed * 2.3);
                    float width = 1.0 - 0.35 * along;
                    float d = abs((across - 0.5) * 2.0 - wobble) / max(width, 0.15);
                    float core = 1.0 - smoothstep(0.25, 1.0, d);
                    float threads = Cracks(float2(along * 9.0, (across - 0.5) * 4.0)) * _VeinStrength;
                    col = lerp(_BandColour.rgb, _VeinColour.rgb, 0.35 * (1.0 - core));
                    alpha = body * saturate(core + threads * core) * _Opacity;
                }
                else
                {
                    // THE EXTINGUISHING FRONT: a soft dark edge travelling along the line, over
                    // the cells, gone as soon as it has passed.
                    float d = _CentreOut > 0.5 ? abs(along - 0.5) * 2.0 : along;
                    float gap = (_Along - d) / max(_Soft, 1e-4);
                    // Just at the front, and a short breath of shade behind it.
                    float front = exp(-gap * gap * 2.5) + 0.35 * saturate(gap) * exp(-gap * 0.9);
                    float edge = 1.0 - smoothstep(0.75, 1.0, abs(across - 0.5) * 2.0);
                    col = _VeinColour.rgb;
                    alpha = saturate(front) * edge * _Opacity;
                }

                return half4(col, saturate(alpha) * input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
