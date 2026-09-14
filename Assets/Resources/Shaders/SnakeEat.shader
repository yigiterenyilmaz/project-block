// PURPOSE: The block "Yılan" is eating, while its MATTER is taken off it (SnakeEatView). This is
// what makes the difference between the two readings of the same moment:
//
//     "the block shrank and vanished"   - which is what a scale-down and a fade give you
//     "the snake pulled the block apart and drank it"  - which is what this gives you
//
// The block is never scaled down and never faded out. Its SILHOUETTE is taken away from the side
// the snake is on, and what leaves it leaves as light:
//
//   EXTRACTION FRONT - _Progress walks a boundary across the face along _Dir (the direction of the
//                      mouth), and every pixel behind it is gone. NOT a straight wipe: the
//                      boundary is bent by two low-frequency lobes, so the face comes away in a
//                      few big soft tongues the way matter would, not as a sliding edge. Two
//                      cosines and nothing else - no noise texture, no per-pixel dissolve, both of
//                      which read as "generic dissolve shader" at this size. _Variant turns the
//                      lobes so two blocks never come apart identically.
//   EDGE LIGHT       - a THIN rim of the block's own energy colour where the front is working.
//                      One or two pixels (_EdgeWidth): wider than that and it is a neon dissolve.
//   DRAIN            - the mass still standing loses its highlight and some of its colour as the
//                      front comes for it (_Drain), so the block reads as being emptied rather
//                      than cropped. It keeps its OWN material throughout - this only takes the
//                      light out of it.
//
// The face itself is the block's own sprite, so a gold block still looks like gold and an obsidian
// one like obsidian: every block type goes through the same eating language and only its colours
// differ. Per renderer through a MaterialPropertyBlock (one block is being eaten at a time, so the
// batching cost is one sprite). Tagged Universal2D, as the 2D renderer requires; without this
// shader SnakeEatView falls back to shrinking the block into the mouth, which is what it used to
// do.
Shader "ProjectBlock/SnakeEat"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Dir ("Toward the mouth", Vector) = (1, 0, 0, 0)
        _Progress ("Extraction", Range(0, 1)) = 0
        _Softness ("Boundary softness", Range(0.01, 0.5)) = 0.09
        _Lobes ("Lobe depth", Range(0, 0.4)) = 0.16
        _Variant ("Lobe variant", Float) = 0
        _EdgeWidth ("Edge light width", Range(0.005, 0.12)) = 0.035
        _EdgeLight ("Edge light strength", Range(0, 1)) = 0.55
        _EnergyColour ("Energy colour", Color) = (1, 1, 1, 1)
        _Drain ("Light drawn out of what is left", Range(0, 1)) = 0
        _Activation ("Near-face activation", Range(0, 1)) = 0
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
                float4 _Dir;
                float _Progress;
                float _Softness;
                float _Lobes;
                float _Variant;
                float _EdgeWidth;
                float _EdgeLight;
                float4 _EnergyColour;
                float _Drain;
                float _Activation;
            CBUFFER_END

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
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                float2 d = _Dir.xy;
                d = length(d) > 1e-5 ? normalize(d) : float2(1.0, 0.0);
                float2 p = input.uv - 0.5;
                // 0 on the face the mouth is at, 1 on the far one - so the front always starts on
                // the side the snake is on, whichever way it came from.
                float along = saturate(0.5 - dot(p, d));
                float across = dot(p, float2(-d.y, d.x)) + 0.5;

                // Two big lobes. This is the whole difference between matter coming away and an
                // alpha wipe, and it is deliberately this smooth: anything finer reads as noise.
                const float TAU = 6.2831853;
                float ph = _Variant * 0.37;
                float lobe = 0.62 * cos((across + ph) * TAU)
                           + 0.38 * cos((across * 2.0 - ph * 1.7) * TAU);
                float soft = max(_Softness, 1e-3);
                float front = lerp(-_Lobes - soft, 1.0 + _Lobes + soft, _Progress)
                    + _Lobes * 0.5 * lobe;

                float gone = saturate((front - along) / soft);
                c.a *= 1.0 - gone;

                // What is still there is losing its light, not its material: pull it toward its own
                // average and take a little of the highlight off, hardest just ahead of the front.
                float coming = saturate(1.0 - (along - front) / 0.55);
                float grey = dot(c.rgb, float3(0.33, 0.5, 0.17));
                c.rgb = lerp(c.rgb, float3(grey, grey, grey) * 0.82, _Drain * coming * 0.55);

                // The near face waking up: the block's own energy colour, from INSIDE it, before
                // anything has come away. Never the whole block - it keeps its own shading.
                float near = saturate(1.0 - along / 0.5);
                c.rgb = lerp(c.rgb, c.rgb * _EnergyColour.rgb * 1.22,
                    _Activation * near * near * 0.5);

                // And the thin rim where the front is actually working.
                float rim = saturate(1.0 - abs(along - front) / max(_EdgeWidth, 1e-3));
                c.rgb = lerp(c.rgb, _EnergyColour.rgb, rim * rim * _EdgeLight * 0.85);
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
