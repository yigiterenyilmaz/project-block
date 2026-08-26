// PURPOSE: The one shader that makes a painted block tile MOVE by distorting itself rather
// than by changing colour. Two blocks use it, and they want opposite things at the border:
//
//   WATER  - only the INSIDE may swirl. The frame, the bevel and the rounded corners are
//            furniture and must stay dead still, or the cube stops reading as a solid block
//            with water in it. _EdgeHold 1.
//   GHOST  - the whole tile flows, border included. A ghost has no business having a crisp
//            edge; that is the point of it. _EdgeHold 0.
//
// The warp itself is two octaves of sine, the second sampling the first (a domain warp), which
// is what makes it read as blobby and rolling instead of a shearing wobble. Amplitude is in UV
// space so it is resolution independent.
//
// NO PER-RENDERER PROPERTIES. Every cube needs its own phase or a field of water pulses in
// unison like one animal, and the cheap way to get that is a MaterialPropertyBlock - which
// silently drops the renderer out of SRP batching. Instead the phase is derived in the vertex
// stage from the object's WORLD ORIGIN: neighbouring cells are a cell apart, so they get
// different phases for free, and one shared material serves the whole board.
//
// Written for the 2D renderer: the pass is tagged LightMode = Universal2D or Renderer2D will
// not draw it at all.
Shader "ProjectBlock/BlockWarp"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _WarpAmp ("Warp amplitude (UV)", Range(0, 0.15)) = 0.02
        _WarpFreq ("Warp frequency", Range(0.5, 24)) = 6
        _WarpSpeed ("Warp speed", Range(0, 4)) = 0.7
        _WarpDrift ("Drift along the flow", Range(0, 1)) = 0.25

        // 1 = hold the border still and clamp sampling inside it (water, void)
        // 0 = let the whole tile flow, border and all (ghost)
        _EdgeHold ("Edge hold", Range(0, 1)) = 1
        _EdgeSoft ("Edge mask width (UV)", Range(0.01, 0.4)) = 0.16

        // Rotation about the tile's centre, radians per second at the very middle, falling off
        // to nothing at the rim. This is the VOID's stir - the one block that turns rather
        // than flows, because it is a hole and a hole has a middle.
        _Swirl ("Stir (rad/sec at centre)", Range(-3, 3)) = 0
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
                float phase : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _WarpAmp;
                float _WarpFreq;
                float _WarpSpeed;
                float _WarpDrift;
                float _EdgeHold;
                float _EdgeSoft;
                float _Swirl;
            CBUFFER_END

            // Which way water settles this round ("Kütleçekim merkezi" turns it). Set once by
            // BoardView as a global, because it is a property of the BOARD and not of a cube.
            float4 _BlockFlowDir;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                // The cell's own place in the world, turned into a phase offset. Irrational-ish
                // multipliers so a row of cubes never lands back in step with itself.
                float3 originWS = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                output.phase = originWS.x * 2.17 + originWS.y * 3.41;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _WarpSpeed + input.phase;

                // The STIR, applied first so the flow warp rides on top of an already-turning
                // field. It rotates about the centre and dies off toward the rim (squared, so
                // the outer half barely moves) - which is also what keeps the border still
                // without needing the edge mask below.
                if (abs(_Swirl) > 0.0001)
                {
                    float2 fromCentre = uv - 0.5;
                    float radius = saturate(length(fromCentre) * 2.0);
                    float falloff = (1.0 - radius) * (1.0 - radius);
                    float angle = _Swirl * falloff * (_Time.y + input.phase);
                    float sinA = sin(angle);
                    float cosA = cos(angle);
                    fromCentre = float2(fromCentre.x * cosA - fromCentre.y * sinA,
                                        fromCentre.x * sinA + fromCentre.y * cosA);
                    uv = fromCentre + 0.5;
                }

                // Two octaves, the second riding on the first: a domain warp, which rolls and
                // folds where a plain sine only slides.
                // The feedback term (how much of the first octave the second one is sampled
                // through) is what decides BLOBBY versus rippling: at a low value the two
                // octaves just add up into a wave, at a high one the field folds over itself
                // and the highlights round off into moving blobs.
                float2 w1 = float2(sin(uv.y * _WarpFreq + t),
                                   cos(uv.x * _WarpFreq * 0.9 - t * 1.1));
                float2 w2 = float2(sin((uv.y + w1.y * 0.62) * _WarpFreq * 1.9 - t * 0.7),
                                   cos((uv.x + w1.x * 0.62) * _WarpFreq * 1.5 + t * 0.9));
                float2 offset = (w1 * 0.45 + w2 * 0.55) * _WarpAmp;

                // A slow current along the way water falls, so the inside of a cube leans the
                // way the round's gravity pulls instead of milling about in place.
                offset += _BlockFlowDir.xy * _WarpAmp * _WarpDrift
                        * (0.5 + 0.5 * sin(t * 0.6));

                // The border mask: 0 right at the edge, 1 well inside. At _EdgeHold 0 it is
                // ignored entirely and the frame flows with everything else.
                float2 toEdge = min(uv, 1.0 - uv);
                float mask = smoothstep(0.0, _EdgeSoft, min(toEdge.x, toEdge.y));
                offset *= lerp(1.0, mask, _EdgeHold);

                // Held borders also refuse to be sampled from: without this a strong swirl
                // drags frame pixels into the field and the bevel smears inward.
                float2 warped = uv + offset;
                float inset = _EdgeSoft * 0.5;
                warped = lerp(warped, clamp(warped, inset, 1.0 - inset), _EdgeHold);

                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped);
                return texel * input.color * _Color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
