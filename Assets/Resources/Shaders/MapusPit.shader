// PURPOSE: The floor of a cell "Mapus" has sealed - the OUBLIETTE. Drawn over the board's own empty
// slot by MapusSealView, one renderer per sealed cell, everything per-instance in a
// MaterialPropertyBlock.
//
// It exists because the boss's first read has to be DEPTH. A cell that is merely a different colour
// is a cell somebody painted, and that is the whole failure this replaces; a cell that has dropped
// a step into the board is somewhere a block cannot go for a reason the eye supplies by itself.
//
//   THE PIT IS LIT LIKE A HOLE, WHICH IS THE INVERSE OF A BUMP. The board's light comes from the
//   upper left, so inside a hole it falls on the LOWER-RIGHT inner wall and leaves the UPPER-LEFT
//   one in shadow. Get that the wrong way round and the same shading reads as a dome sitting on the
//   cell - it is one sign, and it decides the entire effect.
//   _Depth      how far it has dropped: it deepens the floor, darkens the middle and strengthens
//               both inner walls together. The spawn drives this from 0, and it is what makes the
//               cell SINK rather than fade.
//   _Inner      how black the middle goes. Never to actual black: a pure black disc in a cell is a
//               hole punched in the render, and the void has to keep a hue of its own (a cold
//               indigo) so it separates from the board's own near-black.
//   _Rim        how tight the cell's own edge is drawn - the board's slot closing down on it.
//   _Warm       the seal's heat, bounced off the floor around it. Very low: it is the only warm
//               thing in the effect and it must never become a glow.
//
// The coordinate is the sprite's own object space over _FaceHalf, because a sprite is one unit
// across only when its PPU equals its pixel size and the generated ones are not. Tagged Universal2D
// as the 2D renderer requires; without it MapusSealView draws a flat dark slot, which still says
// the cell is shut.
Shader "ProjectBlock/MapusPit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Floor ("The sunken floor", Color) = (0.09, 0.09, 0.12, 1)
        _Void ("The dark in the middle", Color) = (0.04, 0.04, 0.07, 1)
        _Lip ("Light on the lower inner wall", Color) = (0.30, 0.33, 0.40, 1)
        _Depth ("How far the floor has dropped", Float) = 1
        _Inner ("How dark the middle goes", Float) = 1
        _Rim ("How tight the cell's own edge is", Float) = 0.5
        _Warm ("The seal's heat on the floor", Float) = 0
        _WarmColour ("That heat's colour", Color) = (0.42, 0.14, 0.11, 1)
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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Floor;
                float4 _Void;
                float4 _Lip;
                float _Depth;
                float _Inner;
                float _Rim;
                float _Warm;
                float4 _WarmColour;
                float _FaceHalf;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.face = input.positionOS.xy / max(_FaceHalf, 1e-4);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (tex.a < 0.01)
                {
                    return half4(0, 0, 0, 0);
                }
                float2 f = input.face;
                float depth = saturate(_Depth);
                // How far into the slot this pixel is: 0 at its edge, 1 in the middle. Chebyshev,
                // softened toward the radial, so the pit follows the CELL and not a disc.
                float square = max(abs(f.x), abs(f.y));
                float round = length(f);
                float d = lerp(round, square, 0.7);
                float inside = 1.0 - smoothstep(0.0, 1.0, d);

                // ---- the floor, dropping away toward the middle ----
                float sink = pow(saturate(inside), 0.7) * depth;
                float3 col = lerp(_Floor.rgb, _Void.rgb, sink * saturate(_Inner));

                // ---- THE INNER WALLS ----
                // A HOLE, not a bump: the board's upper-left light falls on the LOWER-RIGHT inner
                // wall and leaves the upper-left one dark. One sign, and it decides whether this
                // cell reads as sunken or as something standing on the board.
                float2 toLight = normalize(float2(-0.6, 0.8));
                float wall = smoothstep(0.30, 0.98, d) * (1.0 - smoothstep(0.98, 1.0, d));
                float facing = dot(normalize(f + 1e-5), toLight);
                // Facing the light: this is the near wall, so it is in shadow.
                col *= 1.0 - 0.55 * wall * saturate(facing) * depth;
                // Away from it: the far wall catches what falls in.
                col = lerp(col, _Lip.rgb, wall * saturate(-facing) * 0.42 * depth);

                // ---- the slot's own edge closing down ----
                float rim = smoothstep(0.86, 1.0, d);
                col *= 1.0 - 0.35 * rim * saturate(_Rim);

                // ---- the seal's heat, bounced off the floor. Never a glow. ----
                float warm = saturate(_Warm) * (1.0 - smoothstep(0.0, 0.62, d));
                col = lerp(col, _WarmColour.rgb, warm * 0.30);

                return half4(col, tex.a) * input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
