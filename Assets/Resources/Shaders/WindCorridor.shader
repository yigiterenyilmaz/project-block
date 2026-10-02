// PURPOSE: "Rüzgar"'s CORRIDOR - the whole body of the storm on ONE quad (WindCorridor), shared
// by the aim's preview and the cast. Everything is worked out in CELLS (_LenCells along from the
// corridor's back edge, _WidCells across), so the picture does not care how big a cell is drawn.
//
//   PRESSURE BODY   a very faint lane, soft on every side and a little CLEARER down its middle -
//                   air you look through, never a tinted rectangle laid over the grid.
//   SIDE EDGES      two thin BROKEN boundaries where the band ends. They are what tells the
//                   player how wide the wind is; the breaks run with the flow.
//   FLOW            light/dark DOUBLETS running down the lane - the board seen through moving
//                   air. Nothing here reads the frame back (no grab pass, no RenderTexture): a
//                   refraction is drawn as the pair of bands a real one leaves, the way the
//                   antimatter's lens is. _Doublet is its strength and goes to 0 as the gust dies.
//   FRONT           the storm's leading wall at _Front: bowed (its middle leads), hard on its
//                   leading side and soft behind, broken across. The body is only revealed BEHIND
//                   it (_Reveal), so the lane fills as the wind crosses rather than switching on.
//
// _Squeeze is the pressure lock before a cast (the band draws in and its edges brighten), _Flow
// the distance the air has run (driven from C#, so a refused aim can simply stop it), and
// _EdgeColor the one thing an invalid aim changes - a muted rose, never a red lane.
// Premultiplied: the darks of the doublets are alpha with no colour.
Shader "ProjectBlock/WindCorridor"
{
    Properties
    {
        _Tint ("Body tint", Color) = (0.80, 0.93, 0.98, 1)
        _EdgeColor ("Edge colour", Color) = (0.78, 0.92, 0.97, 1)
        _LenCells ("Length in cells", Float) = 7
        _WidCells ("Width in cells", Float) = 4
        _HalfBand ("Half band in cells", Float) = 1.5
        _BodyAlpha ("Body alpha", Float) = 0.04
        _EdgeAlpha ("Side edge alpha", Float) = 0.10
        _Doublet ("Flow doublet strength", Float) = 0.05
        _Flow ("Flow distance", Float) = 0
        _Front ("Front position", Float) = 99
        _FrontAlpha ("Front alpha", Float) = 0
        _Reveal ("Body revealed up to", Float) = 99
        _Squeeze ("Pressure lock", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
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
        Blend One OneMinusSrcAlpha

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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _EdgeColor;
                float _LenCells;
                float _WidCells;
                float _HalfBand;
                float _BodyAlpha;
                float _EdgeAlpha;
                float _Doublet;
                float _Flow;
                float _Front;
                float _FrontAlpha;
                float _Reveal;
                float _Squeeze;
                float _Fade;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise, smooth in both axes.
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float a = input.uv.x * _LenCells;
                float c = (input.uv.y - 0.5) * _WidCells;
                float halfBand = _HalfBand * (1.0 - 0.12 * _Squeeze);
                float side = abs(c);

                // soft on every side
                float edge = 1.0 - smoothstep(halfBand - 0.45, halfBand + 0.2, side);
                float ends = smoothstep(0.0, 0.6, a) * smoothstep(0.0, 0.6, _LenCells - a);
                float reveal = 1.0 - smoothstep(_Reveal - 0.3, _Reveal + 0.12, a);
                float lane = edge * ends * reveal;

                // the body, a little clearer down its middle
                float clear = 1.0 - 0.32 * exp(-(c * c) / 0.3);
                float body = _BodyAlpha * (1.0 + 0.5 * _Squeeze) * lane * clear;

                // the two broken boundaries
                float rimD = (side - halfBand) / 0.13;
                float rim = exp(-rimD * rimD);
                // BROKEN: whole stretches of each edge are simply not there, and the breaks run with
                // the flow. Drawn unbroken, two thin lines either side of a lane are a selection box.
                float dash = smoothstep(0.46, 0.70, Noise(float2(a * 1.35 - _Flow * 0.7, sign(c) * 7.3)));
                float sideAlpha = _EdgeAlpha * (1.0 + 0.9 * _Squeeze) * rim * dash * ends * reveal;

                // the flow: light and dark pairs running down the lane
                float2 q = float2(a * 0.5 - _Flow, c * 2.4 + 3.1);
                float n0 = Noise(q);
                float n1 = Noise(q + float2(0.11, 0.0));
                float streak = (n1 - n0) * 7.0;
                float d = streak * _Doublet * lane;
                float light = max(d, 0.0);
                float dark = max(-d, 0.0);

                // the front: bowed, hard ahead and soft behind, broken across
                float bow = side / max(halfBand, 0.001);
                float ac = _Front - 0.4 * bow * bow;
                float df = a - ac;
                float wallD = df / (df > 0.0 ? 0.075 : 0.24);
                float wall = exp(-wallD * wallD);
                wall *= 0.7 + 0.3 * Noise(float2(c * 2.6, _Flow * 1.7));
                wall *= 1.0 - smoothstep(halfBand - 0.15, halfBand + 0.25, side);
                float wallAlpha = _FrontAlpha * wall;

                float3 rgb = _Tint.rgb * body
                    + _EdgeColor.rgb * (sideAlpha + wallAlpha)
                    + light;
                float alpha = body + sideAlpha + wallAlpha * 0.6 + dark + light * 0.2;
                return half4(rgb, alpha) * _Fade;
            }
            ENDHLSL
        }
    }
}
