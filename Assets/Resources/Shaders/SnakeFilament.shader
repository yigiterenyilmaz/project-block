// PURPOSE: The energy ribbons the block's matter leaves along, on its way into the snake's mouth
// (SnakeEatView). Each is a strip mesh whose colour and alpha come PER VERTEX - the block's own
// colour at the source, more saturated along the way, lighter as it reaches the mouth - so the
// flow down it is baked into the mesh rather than scrolled through a texture. A scrolling neon
// texture is exactly what this must not look like, and neither is a flat line.
//
// WHAT THIS DOES IS GIVE THE RIBBON A VOLUME. A single-width line with soft ends reads as a drawn
// stroke - a debug laser - however carefully it is coloured. So the cross section is three zones
// out of the one vertex colour:
//
//     CORE      the inner third, the same hue SCALED UP. Scaled, never pushed toward white:
//               multiplying keeps the ratio between the channels, so a gold core is pale GOLD and
//               a blue one pale BLUE. A white core would throw away the only thing that makes
//               each block's bite its own.
//     BODY      the ribbon proper, at the colour it was handed.
//     SHOULDER  a wide, faint outer falloff in a slightly deeper version of it, which is what
//               makes the thing read as having thickness instead of being a stroke.
//
// No texture and nothing per renderer: one shared material for every ribbon. Tagged Universal2D,
// as the 2D renderer requires, and with a fallback so a compile failure can never show up as
// Unity's magenta error material - which is how "pink lines" happen.
Shader "ProjectBlock/SnakeFilament"
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
                // u runs across the ribbon. Three zones, so it has a volume rather than a width.
                float x = abs(input.uv.x * 2.0 - 1.0);
                float core = pow(saturate(1.0 - x / 0.36), 2.0);
                float body = pow(saturate(1.0 - x / 0.74), 1.4);
                float shoulder = pow(saturate(1.0 - x), 2.6) * 0.45;
                float a = saturate(body * 0.78 + shoulder);

                // SCALED, never whitened: multiplying keeps the hue exactly, so a gold core is
                // pale gold and a violet one pale violet.
                float3 deep = input.color.rgb * 0.72;
                float3 lit = input.color.rgb * 1.22;
                float3 rgb = lerp(deep, input.color.rgb, saturate(body));
                rgb = lerp(rgb, lit, core);
                return half4(rgb, input.color.a * a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
