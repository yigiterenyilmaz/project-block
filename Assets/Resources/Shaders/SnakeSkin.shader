// PURPOSE: "Yılan"'s SKIN - the material response layer over the six drawn pieces. It never
// repaints the snake: the art's own colours, its cream belly and its amber accents stay exactly as
// they were painted, and everything here is a small, physical answer to what the snake is DOING.
//
// Fed per renderer through a MaterialPropertyBlock (one material for the whole snake, twenty
// segments, no instances):
//
//   SHEEN          a broad, soft lift where the art is ALREADY lit, biased to the upper left
//                  because that is where the art's own light comes from - so a segment reads as a
//                  polished surface catching the room, and never as a white stripe crossing it.
//                  IN WORLD SPACE, so turning a piece a quarter turn does not turn its lighting.
//   LEADING EDGE   the side a moving segment leads with lifts a touch, the trailing side deepens.
//   ACCENT         the cream and amber regions - found by the art's own warmth, not by a mask -
//                  can be brought up a few percent for a gulp or a cut signal.
//   BAND           one soft band travelling ALONG the body: the swallow passing down the neck, the
//                  constriction running to the tail. The snake's own surface is the signal; there
//                  is no light orb anywhere.
//   COMPRESSION    a squeezed segment's lower half deepens and its highlight tightens, which is
//                  what makes a 4% squeeze read at all.
//   DRAIN / DIM    the life going out: a dormant segment before it wakes, and the last one as it
//                  collapses. Desaturation, never a grey tint over the top.
//
// NOTHING HERE GLOWS. No bloom, no outline, no additive halo, no neon: every effect is a lerp
// between the pixel's own colour and a slightly brighter or deeper version of it. Tagged
// Universal2D, as the 2D renderer requires; without this shader the snake draws as plain sprites
// and SnakeVfxController simply has no surface layer to drive.
Shader "ProjectBlock/SnakeSkin"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Centre ("Piece centre (world)", Vector) = (0, 0, 0, 0)
        _Half ("Piece half-size (world)", Float) = 0.5
        _Sheen ("Surface sheen", Range(0, 1)) = 0
        _SheenSoft ("How broad the sheen is", Range(0, 1)) = 0.6
        _Lead ("Leading edge light", Range(0, 1)) = 0
        _LeadDir ("The way it is leading (world)", Vector) = (1, 0, 0, 0)
        _Accent ("Cream / amber accent boost", Range(0, 1)) = 0
        _Compression ("Surface compression response", Range(0, 1)) = 0
        _Band ("Band position along the body (0 tail .. 1 head)", Float) = -1
        _BandWidth ("Band width", Float) = 0.35
        _BandStrength ("Band strength", Range(0, 1)) = 0
        _Drain ("Colour drain", Range(0, 1)) = 0
        _Dim ("Surface dim", Range(0, 1)) = 0
        _AccentColour ("What the accent warms toward", Color) = (1, 0.88, 0.66, 1)
        // THE COLOURS THE SNAKE SWALLOWED, coming up from UNDER its own surface as it dies.
        // Three pockets, each a colour and a place: xy is where it sits in the piece's own space,
        // z how wide it is, w how strongly it shows. Blended so the artwork's own shading stays
        // on top of them - a coloured sprite laid over the segment would be a sticker, which is
        // the one thing this must not look like.
        _Pocket0 ("Swallowed colour 0", Color) = (0, 0, 0, 0)
        _PocketAt0 ("Where / how wide / how strong 0", Vector) = (0, 0, 0.4, 0)
        _Pocket1 ("Swallowed colour 1", Color) = (0, 0, 0, 0)
        _PocketAt1 ("Where / how wide / how strong 1", Vector) = (0, 0, 0.4, 0)
        _Pocket2 ("Swallowed colour 2", Color) = (0, 0, 0, 0)
        _PocketAt2 ("Where / how wide / how strong 2", Vector) = (0, 0, 0.4, 0)
        _BandColour ("What the band warms toward", Color) = (1, 0.92, 0.78, 1)
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
                float2 q : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Centre;
                float _Half;
                float _Sheen;
                float _SheenSoft;
                float _Lead;
                float4 _LeadDir;
                float _Accent;
                float _Compression;
                float _Band;
                float _BandWidth;
                float _BandStrength;
                float _Drain;
                float _Dim;
                float4 _AccentColour;
                float4 _Pocket0;
                float4 _PocketAt0;
                float4 _Pocket1;
                float4 _PocketAt1;
                float4 _Pocket2;
                float4 _PocketAt2;
                float4 _BandColour;
            CBUFFER_END

            float Lum(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                // -1..1 across the piece IN WORLD SPACE: the light direction below is the art's
                // own, and a quarter turn must not take it with it.
                output.q = (world.xy - _Centre.xy) / max(_Half, 1e-4);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 c = texel.rgb * input.color.rgb;
                float alpha = texel.a * input.color.a;
                float lum = Lum(c);
                // The art's own warm regions - the cream belly and the amber accents - told apart
                // by their own warmth rather than by a painted mask.
                float warm = saturate((c.r - c.b) * 2.2);

                // ---- the life going out, and coming back ----
                c = lerp(c, lum.xxx * 0.92, _Drain);
                c *= 1.0 - 0.35 * _Dim;

                // ---- SHEEN: a broad lift where the art is already lit, from the upper left ----
                float upper = saturate(0.5 + dot(input.q, float2(-0.42, 0.78)) * 0.5);
                float broad = lerp(0.55, 0.18, saturate(_SheenSoft));
                float sheen = _Sheen * smoothstep(broad, 1.0, upper) * saturate(lum * 1.6);
                c = lerp(c, c * 1.16 + 0.03, saturate(sheen));

                // ---- the side it is leading with, and the side it is dragging ----
                float along = dot(input.q, normalize(_LeadDir.xy + float2(1e-5, 0)));
                c = lerp(c, c * 1.20, _Lead * smoothstep(0.15, 1.0, along));
                c = lerp(c, c * 0.88, _Lead * smoothstep(0.15, 1.0, -along));

                // ---- the cream and amber coming up a few percent ----
                c = lerp(c, c * _AccentColour.rgb * 1.18, saturate(_Accent * warm));

                // ---- one band travelling along the body ----
                if (_Band >= 0.0)
                {
                    float s = saturate(along * 0.5 + 0.5);
                    float d = (s - _Band) / max(_BandWidth, 1e-3);
                    float band = exp(-d * d * 2.2);
                    c = lerp(c, c * _BandColour.rgb * 1.26, _BandStrength * band * (0.72 + 0.28 * warm));
                    c = lerp(c, c * 0.86, _BandStrength * band * 0.5 * saturate(-input.q.y));
                }

                // ---- THE SWALLOWED COLOURS, from underneath ----
                //
                // Each pocket is a soft round region in the piece's own space. It is mixed in
                // UNDER the surface: the pixel is carried toward its own colour multiplied by the
                // pocket's, so the artwork's shading, its highlight and its dark underside all
                // survive and the colour reads as coming from inside rather than painted on. A
                // straight lerp to the pocket colour would flatten the piece into a coloured
                // blob, which is the sticker look.
                float3 pocket = float3(0.0, 0.0, 0.0);
                float pocketWeight = 0.0;
                for (int pi = 0; pi < 3; pi++)
                {
                    float4 at = pi == 0 ? _PocketAt0 : pi == 1 ? _PocketAt1 : _PocketAt2;
                    if (at.w <= 0.001)
                    {
                        continue;
                    }
                    float3 tint = (pi == 0 ? _Pocket0 : pi == 1 ? _Pocket1 : _Pocket2).rgb;
                    float d = length(input.q - at.xy) / max(at.z, 1e-3);
                    float here = pow(saturate(1.0 - d), 1.8) * at.w;
                    pocket += tint * here;
                    pocketWeight += here;
                }
                if (pocketWeight > 0.001)
                {
                    // The pockets keep their own identities where they overlap: the strongest one
                    // at a pixel wins the hue instead of every colour averaging into mud.
                    float3 mixed = pocket / pocketWeight;
                    float show = saturate(pocketWeight);
                    c = lerp(c, c * mixed * 2.0, show * 0.85);
                }

                // ---- squeezed: the underside deepens, the highlight tightens ----
                c *= 1.0 - _Compression * 0.12 * saturate(0.5 - input.q.y * 0.5);
                c = lerp(c, c * 1.10, _Compression * smoothstep(0.55, 1.0, upper));

                return half4(c, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
