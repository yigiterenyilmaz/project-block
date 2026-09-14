// PURPOSE: The vapour ribbons of "Soğuk süblimleşme" (CryoSublimationView). Each ribbon is a thin
// strip mesh whose colour and alpha come per vertex - a trace of the cube's old colour at the
// root, pale blue-grey and nothing toward the top. This only softens it across its width:
// densest down the middle, gone at both sides, so it reads as thin cold vapour and never as a
// painted stripe or a glowing trail. No texture and nothing per renderer: one shared material
// for every ribbon. Tagged Universal2D, as the 2D renderer requires; without it the ribbons are
// simply not drawn.
Shader "ProjectBlock/CryoVapour"
{
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // u runs across the ribbon: soft on both sides, densest down its middle.
                float x = abs(input.uv.x * 2.0 - 1.0);
                float across = pow(saturate(1.0 - x * x), 1.6);
                return half4(input.color.rgb, input.color.a * across);
            }
            ENDHLSL
        }
    }
}
