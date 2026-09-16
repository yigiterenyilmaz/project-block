// PURPOSE: "Yangın" - ONE CUBE CATCHING FIRE. The front that crosses a cube as it is lit, and the
// only part of that effect that could not be done with sprites.
//
// IT IS THE SAME SHADER TWICE, on two faces stacked on one cell: the cube as it WAS, being eaten
// from the side the fire arrived on (_Invert 1), and the FIRE cube appearing behind that same
// front (_Invert 0). One front, two materials - so the two can never disagree about where the
// burn line is, which is what a cross-fade between them would look like.
//
// WHY A FRONT AND NOT A FADE. A cross-fade says "this cube was replaced". A front says "the fire
// came in from THAT side and took it", which is the rule: a cube catches from the neighbour that
// lit it. The direction is the report's, never guessed here.
//
// THE FRONT IS RAGGED, and by two low-frequency waves only - the lesson the talisman's stain
// already paid for: high frequency on a boundary reads as noise, not as burning. Behind it the
// old colour is drained and sooted rather than tinted red, and just behind the line the cube
// glows through EMBER VEINS - thin hot streaks running the way the fire is travelling, which is
// what makes it read as heating from inside rather than as paint arriving.
Shader "ProjectBlock/FireBloom"
{
    Properties
    {
        [PerRendererData] _MainTex ("Face", 2D) = "white" {}
        _Front ("How far the burn has crossed", Float) = 0
        _Dir ("Which way it is travelling", Vector) = (1, 0, 0, 0)
        _Band ("How soft the front is", Float) = 0.22
        _Ragged ("How far the front wanders", Float) = 0.12
        _Invert ("0 reveals this face, 1 burns it away", Float) = 0
        _Rim ("Heat on the front line itself", Float) = 1
        _RimColour ("", Color) = (1, 0.78, 0.32, 1)
        _CoreColour ("", Color) = (1, 0.94, 0.62, 1)
        _Soot ("Darkening behind the front", Float) = 0.35
        _Drain ("Colour taken out of what is left", Float) = 0.7
        _Veins ("Ember veins behind the front", Float) = 0.5
        _Heat ("Warmth over the whole face", Float) = 0
        _Seed ("Per-cube variation", Float) = 0
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
                float _Front;
                float4 _Dir;
                float _Band;
                float _Ragged;
                float _Invert;
                float _Rim;
                float4 _RimColour;
                float4 _CoreColour;
                float _Soot;
                float _Drain;
                float _Veins;
                float _Heat;
                float _Seed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 face = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 p = input.uv - 0.5;
                float2 dir = normalize(_Dir.xy + float2(0.0001, 0.0));
                float2 side = float2(-dir.y, dir.x);
                // HOW FAR ACROSS THE CUBE this pixel is, along the way the fire is travelling:
                // 0 at the edge it came in on, 1 at the far side.
                float d = dot(p, dir) + 0.5;
                float across = dot(p, side);
                // TWO LOW-FREQUENCY WAVES and no more. High frequency here is noise; this has to
                // read as a burning edge.
                float wobble = sin(across * 9.0 + _Seed * 6.283) * 0.6
                    + sin(across * 15.0 - _Seed * 3.1) * 0.4;
                d += wobble * _Ragged;

                // 1 where the fire has already been, 0 where it has not reached.
                float burnt = saturate((_Front - d) / max(_Band, 1e-4));
                burnt = burnt * burnt * (3.0 - 2.0 * burnt);
                // The line itself, for the heat that rides on it. NARROW: at 1.4 bands wide it
                // was a broad cream stripe crossing the cube - which is a wipe with a highlight
                // on it, not something burning.
                float hot = saturate(1.0 - abs(d - _Front) / max(_Band * 0.75, 1e-4));
                hot = hot * hot;

                half3 rgb = face.rgb;
                float alpha = face.a * input.color.a;

                if (_Invert > 0.5)
                {
                    // THE OLD CUBE, being taken. What is left of it loses its colour and darkens
                    // as the front nears - it is not tinted red, it is going OUT.
                    float near = saturate((_Front + _Band * 1.6 - d) / max(_Band * 2.2, 1e-4));
                    float grey = dot(rgb, half3(0.299, 0.587, 0.114));
                    rgb = lerp(rgb, half3(grey, grey, grey), _Drain * near);
                    rgb *= 1.0 - _Soot * near;
                    alpha *= 1.0 - burnt;
                }
                else
                {
                    // THE FIRE, arriving behind that same front - and just behind it, EMBER
                    // VEINS: thin hot streaks running the way the fire is going, brightest where
                    // it has only just passed.
                    // THE VEINS LIVE JUST BEHIND THE FRONT and die out behind that: they are
                    // the cube still glowing where the fire has only now passed, not a pattern
                    // printed across the whole face. And they run ALONG the way the fire is
                    // travelling - skewed by d they came out as diagonal stripes over everything.
                    float behind = saturate(1.0 - (_Front - d) / 0.42) * saturate((_Front - d) / 0.08);
                    float vein = pow(saturate(sin(across * 34.0 + _Seed * 9.0)), 12.0);
                    rgb += _CoreColour.rgb * vein * _Veins * behind;
                    rgb += _RimColour.rgb * _Heat * 0.35;
                    alpha *= burnt;
                }
                // The burn line is hot on both faces, which is what stitches them into one event.
                // Amber first and only a touch of the pale core at its very centre - the line
                // is hot metal, not a light.
                rgb += _RimColour.rgb * hot * _Rim * 0.75;
                rgb += _CoreColour.rgb * hot * hot * _Rim * 0.3;
                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
