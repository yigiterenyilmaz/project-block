// PURPOSE: "Deprem" - a cube sinking into a crack, clipped by the GROUND LINE climbing its face.
//
// Sinking is not shrinking. When something sinks, the ground line rises up it; so the collapse
// hides the cube from the bottom up behind a line that climbs from its base to the crack's near
// edge, and the cube itself only settles a little. That line is this shader's one job.
//
// WHY PER RENDERER AND NOT A SpriteMask: every SpriteMask on the board reveals every masked sprite
// in its range, so a cube sinking beside another collapsing cell would show through the NEIGHBOUR's
// mask - ColdSinkView records the trap, and "targets next to each other" is exactly the scene that
// springs it. A clip line carried in a MaterialPropertyBlock belongs to one renderer and nothing
// else can see it.
//
// _Clip  x = world Y of the line, y = ragged amplitude (world units), z = frequency along world X,
//        w = 1 to clip at all (0 = draw everything - before the fall)
// _Soft  world units of feather on the line, so it never aliases into a staircase
// _Darken 1 = the cube's own face; less as it goes down into the dark
//
// Without this shader the view still plays everything else; the cube simply fades where it would
// have been clipped.
Shader "ProjectBlock/QuakeDrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Clip ("Clip line (y, ragged amp, freq, on)", Vector) = (0, 0, 0, 0)
        _Soft ("Feather (world)", Float) = 0.006
        _Darken ("Darken", Range(0, 1)) = 1
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
                float2 world : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _Clip;
                float _Soft;
                float _Darken;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.world = TransformObjectToWorld(input.positionOS).xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv)
                    * input.color * _Color;
                texel.rgb *= _Darken;
                if (_Clip.w > 0.5)
                {
                    // Ragged, with the same three awkward frequencies the ledge sprite uses, so
                    // the line never repeats into teeth.
                    float x = input.world.x * _Clip.z;
                    float rag = (sin(x) * 0.5 + sin(x * 1.77 + 1.3) * 0.32
                        + sin(x * 3.01 - 0.7) * 0.18) * _Clip.y;
                    float above = input.world.y - (_Clip.x + rag);
                    texel.a *= saturate(above / max(_Soft, 0.0001));
                }
                return texel;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
