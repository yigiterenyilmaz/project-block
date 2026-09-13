// PURPOSE: The material of everything "Tılsım" grows or leaves behind - the jade VINE, the gold
// RUNE KNOTS and CORNER RUNES, the half-there SPIRIT LEAVES, the TALISMAN SEED, and the flakes the
// ground comes apart into when the gift is recalled. One shader, per-renderer through a
// MaterialPropertyBlock, because all of them are the same substance at different temperatures.
//
// THE LIGHT IS IN WORLD SPACE, for the reason MapusIron already had to learn: a vine is one stem
// sprite laid along a curve at a dozen different rotations, and shaded in its own local space every
// segment's highlight would point a different way. Lit from the board's own upper left in WORLD
// space, the whole runner is lit as one object.
//
//   _Deep/_Body/_Hi   the three steps of the substance. A vine sits low in that range, a rune knot
//                     high and warm, a leaf pale and thin.
//   _Bevel            how much of its own form it shows. A stem is round; a rune stroke is nearly
//                     flat, because a fat bevel on a thin stroke is just a blur.
//   _Heat             the warmth held INSIDE a thing - a seed's ember, a rune knot answering the
//                     talisman pulse. It is an internal gradient, never an outward glow: this
//                     power's whole palette is "spirit", and spirit that emits light is neon.
//   _Spectral         how much the piece has stopped being matter. It washes the colour toward
//                     _Ghost and thins it from the middle out, which is what the harvest's shards
//                     and the recall's flakes do on their way out. Never a plain alpha fade.
//   _Rim              light on the piece's own contour, from the baked silhouette's alpha ramp so
//                     it follows the shape. The board is near-black and jade is dark; this is what
//                     keeps a vine off it. Separation, never an outline.
//
// The coordinate is the sprite's own object space over _FaceHalf (a sprite is one unit across only
// when its PPU equals its pixel size, and the baked ones are not). Tagged Universal2D; without it
// TalismanView draws everything in flat jade and gold, which still tells the story and only costs
// the material.
Shader "ProjectBlock/TalismanSpirit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Deep ("Deep jade", Color) = (0.08, 0.16, 0.15, 1)
        _Body ("Body", Color) = (0.16, 0.30, 0.28, 1)
        _Hi ("Highlight", Color) = (0.34, 0.56, 0.48, 1)
        _Ghost ("What it fades toward as it stops being matter", Color) = (0.72, 0.86, 0.80, 1)
        _Tone ("Where in the range this piece sits", Float) = 0.5
        _Bevel ("How much of its own form it shows", Float) = 1
        _Heat ("Warmth held inside it", Float) = 0
        _HeatColour ("What that warmth is", Color) = (0.80, 0.68, 0.38, 1)
        _Spectral ("How far it has stopped being matter", Float) = 0
        _Rim ("Light on its own contour", Float) = 0.3
        _RimColour ("That light's colour", Color) = (0.52, 0.70, 0.62, 1)
        _FaceHalf ("Half the sprite's object-space size", Float) = 0.5
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
                float2 face : TEXCOORD1;
                float2 worldRight : TEXCOORD2;
                float2 worldUp : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Deep;
                float4 _Body;
                float4 _Hi;
                float4 _Ghost;
                float _Tone;
                float _Bevel;
                float _Heat;
                float4 _HeatColour;
                float _Spectral;
                float _Rim;
                float4 _RimColour;
                float _FaceHalf;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.face = input.positionOS.xy / max(_FaceHalf, 1e-4);
                output.worldRight = normalize(TransformObjectToWorldDir(float3(1, 0, 0)).xy
                    + float2(1e-6, 0));
                output.worldUp = normalize(TransformObjectToWorldDir(float3(0, 1, 0)).xy
                    + float2(0, 1e-6));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (tex.a < 0.004)
                {
                    return half4(0, 0, 0, 0);
                }
                float2 f = input.face;

                // ---- the substance ----
                float3 body = lerp(_Deep.rgb, _Body.rgb, saturate(_Tone));

                // ---- its own form, lit from the board's light IN WORLD SPACE ----
                float2 slope = f.x * input.worldRight + f.y * input.worldUp;
                float2 toLight = normalize(float2(-0.6, 0.8));
                float facing = dot(normalize(slope + 1e-5), toLight) * min(length(f), 1.0);
                float bevel = saturate(_Bevel);
                // A floor under the light, as the iron needed: purely directional shading leaves
                // half of every piece at the body colour, and on this board that is nearly black.
                float lit = 0.34 + 0.66 * saturate(facing);
                body = lerp(body, _Hi.rgb, lit * 0.5 * bevel);
                body *= 1.0 - 0.26 * saturate(-facing) * bevel;

                // ---- warmth held inside it: an ember, not a lamp ----
                if (_Heat > 0.0)
                {
                    float inner = 1.0 - smoothstep(0.0, 0.9, length(f));
                    body = lerp(body, _HeatColour.rgb, saturate(_Heat) * inner);
                    body = lerp(body, _HeatColour.rgb, saturate(_Heat) * 0.18);
                }

                float alpha = tex.a;

                // ---- STOPPING BEING MATTER ----
                // The colour washes toward the ghost tint and the piece thins from its MIDDLE
                // outward, so what is left last is its contour. A plain alpha fade is a sprite
                // being switched off; this is a thing losing its substance.
                if (_Spectral > 0.0)
                {
                    float s = saturate(_Spectral);
                    body = lerp(body, _Ghost.rgb, s * 0.85);
                    float core = 1.0 - smoothstep(0.15, 1.0, length(f));
                    alpha *= 1.0 - s * (0.35 + 0.65 * core);
                }

                // ---- the contour, which is what keeps jade off a near-black board ----
                if (_Rim > 0.0)
                {
                    float contour = tex.a * (1.0 - tex.a) * 4.0;
                    body = lerp(body, _RimColour.rgb,
                        saturate(contour) * saturate(_Rim) * (0.45 + 0.55 * saturate(facing)));
                }
                return half4(body, alpha) * input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
