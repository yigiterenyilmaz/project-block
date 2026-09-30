// PURPOSE: "Antimadde" - ADDITIVE light for the baked shapes (AntimatterBlastView): the peak cores,
// the rings, the crown's centre, the spectral particles, the reflections on the cubes that stay.
// A sprite through Sprites/Default is alpha-blended, which lays a coloured sheet OVER the board; this
// adds light to it instead, which is the difference between a violet decal and violet energy. The
// sprite's own alpha is the light's falloff and the renderer colour its colour and strength.
Shader "ProjectBlock/AntimatterGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
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
        Blend SrcAlpha One

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
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                // Never white, however many of these stack in one place.
                texel.rgb = min(texel.rgb, half3(0.95, 0.9, 1.0));
                return texel;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
