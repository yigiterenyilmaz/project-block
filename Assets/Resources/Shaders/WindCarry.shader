// PURPOSE: "Rüzgar" carrying a cube - the proxy WindWaterReaction draws while the water is being
// pushed. The cube keeps its own face; what changes is its SHAPE, in the vertex stage and in WORLD
// space (so it does not matter how the sprite is turned): stretched along the wind (_Along),
// squeezed across it (_Across), and TAPERED so the leading edge runs thin while the trailing one
// bulges (_Taper) - water leaning into a wind, not a sprite being scaled. A sprite's quad is four
// vertices, so the taper is a trapezoid; that is all the shape needs.
//
// One shared material, a property block per proxy. _Sheen lays a pale streak along the wind over
// the face (the surface highlight being drawn out), _Wet is the cube's own alpha.
// Without this shader the proxy is the plain sprite, moved - it still travels, it just does not lean.
Shader "ProjectBlock/WindCarry"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Centre ("Centre (world)", Vector) = (0, 0, 0, 0)
        _Dir ("Wind direction (world)", Vector) = (1, 0, 0, 0)
        _HalfSize ("Half size (world)", Float) = 0.5
        _Along ("Stretch along", Float) = 1
        _Across ("Squash across", Float) = 1
        _Taper ("Taper", Float) = 0
        _Sheen ("Sheen", Range(0, 1)) = 0
        _SheenColor ("Sheen colour", Color) = (0.85, 0.97, 1, 1)
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
            "CanUseSpriteAtlas" = "True"
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
                float2 wind : TEXCOORD1; // x: -1 trailing .. +1 leading, y: -1 .. +1 across
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Centre;
                float4 _Dir;
                float4 _SheenColor;
                float _HalfSize;
                float _Along;
                float _Across;
                float _Taper;
                float _Sheen;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                float2 dir = normalize(_Dir.xy + float2(0.00001, 0.0));
                float2 perp = float2(-dir.y, dir.x);
                float2 rel = world.xy - _Centre.xy;
                float along = dot(rel, dir);
                float across = dot(rel, perp);
                float n = along / max(_HalfSize, 0.0001);
                float narrow = 1.0 - _Taper * clamp(n, -1.0, 1.0);
                world.xy = _Centre.xy + dir * (along * _Along) + perp * (across * _Across * narrow);
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = input.uv;
                output.wind = float2(n, across / max(_HalfSize, 0.0001));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                // the highlight drawn out along the wind: a streak a third of the way up the face,
                // long along the flow, fading toward the trailing edge
                float band = exp(-pow(abs(input.wind.y - 0.3), 2.0) / 0.03);
                float run = smoothstep(-0.9, 0.2, input.wind.x) * (1.0 - smoothstep(0.75, 1.0, input.wind.x));
                float sheen = _Sheen * band * run;
                c.rgb = lerp(c.rgb, _SheenColor.rgb, saturate(sheen * 0.7));
                return c;
            }
            ENDHLSL
        }
    }
}
