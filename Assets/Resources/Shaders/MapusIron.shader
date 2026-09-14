// PURPOSE: The material of everything "Mapus" builds - the four WARDEN RIBS, their EDGE SOCKETS
// and the WARDEN SEAL itself. One shader, per-renderer through a MaterialPropertyBlock.
//
// THE LIGHT IS IN WORLD SPACE, AND THAT IS THE WHOLE REASON THIS FILE EXISTS. The four ribs are one
// sprite turned into four rotations. Shaded in their own local space, each one's highlight would
// point a different way in the world, and four identical pieces lit from four different directions
// do not read as one mechanism - they read as four separate objects that happen to be near each
// other. Lit from the board's own upper left in WORLD space, they read as one piece of ironwork
// closing. The snake's skin already learned this; so does this.
//
//   _Tone       where between the deep and the body colour this piece sits. A socket is darker than
//               the rib it holds, which is what sinks it into the board's own frame.
//   _Bevel      how hard its form is shaded - a lit facet toward the light, a deep one away, and a
//               darkening across the piece so it is round rather than flat. Stylised heavy alloy,
//               never a specular dot: the light is broken ACROSS the piece.
//   _Wear       one broad band of dull bronze where the iron has been worked. One, not a scratch
//               texture: noise at this size is dirt, and dirt is not the same as age.
//   _Rim        light on the piece's OWN contour, taken from its baked alpha ramp so it follows the
//               silhouette rather than a rectangle. The board is near-black and so is this iron;
//               without a contour the whole mechanism disappears into it. Kept low - it is
//               separation, never an outline.
//   _Heat       the seal's own warmth, from the inside out: a low warm gradient with the coldest
//               part at its rim, like iron that was hot a long time ago. This is the only warm
//               thing in the effect and the only property the ribs do not use.
//   _Press      how hard the piece is being squeezed right now (the lock beat, the idle check, a
//               refused placement). It DEEPENS the body and sharpens the bevel - a piece under load
//               goes darker, never brighter.
//
// The coordinate is the sprite's own object space over _FaceHalf (a sprite is one unit across only
// when its PPU equals its pixel size, and the baked ones are not). x runs across the piece, y along
// it. Tagged Universal2D; without it MapusSealView draws flat gunmetal, which still says the cell
// is shut and only costs the material's story.
Shader "ProjectBlock/MapusIron"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Deep ("Blue-black", Color) = (0.05, 0.06, 0.09, 1)
        _Body ("Gunmetal", Color) = (0.17, 0.19, 0.24, 1)
        _Hi ("Cold steel highlight", Color) = (0.40, 0.44, 0.53, 1)
        _Wear ("Dull bronze, where it has been worked", Color) = (0.32, 0.25, 0.15, 1)
        _Tone ("Where in the range this piece sits", Float) = 0.5
        _Bevel ("How hard its own form is shaded", Float) = 1
        _WearAmount ("How much of the bronze shows", Float) = 0.18
        _Rim ("Light on its own contour - what keeps it off a black board", Float) = 0.35
        _RimColour ("That light's colour", Color) = (0.46, 0.51, 0.60, 1)
        _Heat ("Internal warmth - the seal only", Float) = 0
        _HeatColour ("What that warmth is", Color) = (0.52, 0.16, 0.13, 1)
        _Debug ("0 normal, 1 flat silhouette, 2 mass regions", Float) = 0
        _MassKind ("1 = wall housing, 2 = bolt", Float) = 0
        _Groove ("A shallow recess down the middle of the piece", Float) = 0
        _Press ("How hard it is being squeezed", Float) = 0
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
                // The piece's own surface direction, IN WORLD SPACE - what lets four rotations of
                // one sprite be lit as one mechanism.
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
                float4 _Wear;
                float _Tone;
                float _Bevel;
                float _WearAmount;
                float _Rim;
                float4 _RimColour;
                float _Heat;
                float4 _HeatColour;
                float _Debug;
                float _MassKind;
                float _Groove;
                float _Press;
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

                // ---- THE TWO DEBUG MODES, and they exist because this design failed twice on a
                // question that colour and shading cannot answer ----
                if (_Debug > 0.5)
                {
                    if (_Debug < 1.5)
                    {
                        // THE PROPELLER TEST. Everything flat, one grey, no brand, no light: only
                        // the silhouette is left. If THAT still reads as a fan, nothing painted on
                        // top of it will help - which is exactly the trap the first two passes fell
                        // into, tuning materials while the shape was wrong.
                        return half4(0.55, 0.57, 0.62, tex.a) * input.color;
                    }
                    // THE EDGE-MASS TEST. Housing green, shaft blue, latch head red. The green has
                    // to be visibly the heaviest region and the red the lightest; if the mass is
                    // near the middle the piece is a spoke, and this says so at a glance.
                    if (_MassKind < 1.5)
                    {
                        return half4(0.25, 0.75, 0.30, tex.a) * input.color;
                    }
                    float3 mass = f.y > 0.55 ? float3(0.85, 0.20, 0.20) : float3(0.25, 0.45, 0.85);
                    return half4(mass, tex.a) * input.color;
                }

                // ---- the body ----
                float3 body = lerp(_Deep.rgb, _Body.rgb, saturate(_Tone));
                // Under load it DEEPENS. Iron being squeezed does not get brighter.
                body = lerp(body, _Deep.rgb, saturate(_Press) * 0.45);

                // ---- ITS OWN FORM, LIT FROM THE BOARD'S LIGHT IN WORLD SPACE ----
                // The surface normal a bevel implies, carried out into the world through the
                // piece's own axes - so all four rotations catch the light from the same side.
                float2 slope = f.x * input.worldRight + f.y * input.worldUp;
                float2 toLight = normalize(float2(-0.6, 0.8));
                float facing = dot(normalize(slope + 1e-5), toLight) * min(length(f), 1.0);
                float bevel = saturate(_Bevel);
                // A FLOOR UNDER THE LIGHT. Purely directional, half of every piece gets nothing at
                // all and sits at the body colour - which on this board is near-black, and
                // near-black iron on a near-black board is the whole reason the first pass read as
                // four flat shapes. So the light starts at a third and rises from there: the piece
                // is lit everywhere and lit MORE toward the board's own light.
                float lit = 0.32 + 0.68 * saturate(facing);
                body = lerp(body, _Hi.rgb, lit * 0.52 * bevel);
                body *= 1.0 - 0.30 * saturate(-facing) * bevel;
                // Broken ACROSS the piece as well, so it is a forged bar and not a flat cut-out.
                body *= 1.0 - 0.16 * smoothstep(0.45, 1.0, abs(f.x)) * bevel;
                // And the load sharpens the whole thing.
                body = lerp(body, _Hi.rgb, saturate(facing) * 0.18 * saturate(_Press));

                // ---- A SHALLOW GROOVE DOWN THE MIDDLE ----
                // One recess along the piece, dark in its channel with a lit lip on the side the
                // light comes from. It costs nothing and it is most of what stops a rib reading as
                // a flat polygon: a forged bar has a length to it, and a groove is how you see it.
                if (_Groove > 0.0)
                {
                    float channel = 1.0 - smoothstep(0.0, 0.34, abs(f.x + 0.04));
                    float lip = smoothstep(0.30, 0.46, -f.x) * (1.0 - smoothstep(0.46, 0.62, -f.x));
                    body *= 1.0 - 0.30 * channel * saturate(_Groove);
                    body = lerp(body, _Hi.rgb, lip * 0.22 * saturate(_Groove));
                }

                // ---- one broad worked band, never a scratch texture ----
                float band = smoothstep(0.55, 0.15, abs(f.y + 0.35))
                    * smoothstep(0.9, 0.35, abs(f.x));
                body = lerp(body, _Wear.rgb, band * saturate(_WearAmount));

                // ---- THE SEAL'S HEAT: warm inside, cold at its rim ----
                if (_Heat > 0.0)
                {
                    float inner = 1.0 - smoothstep(0.0, 0.95, length(f));
                    body = lerp(body, _HeatColour.rgb, saturate(_Heat) * inner);
                    // A little of it survives right out to the edge, so the piece is one material
                    // rather than a warm blob inside a cold ring.
                    body = lerp(body, _HeatColour.rgb, saturate(_Heat) * 0.22);
                }

                // ---- THE CONTOUR. What keeps near-black iron off a near-black board. ----
                // A baked silhouette's alpha ramps across its own edge, so the band where it is
                // neither in nor out IS the contour. Low: separation, not an outline.
                if (_Rim > 0.0)
                {
                    float contour = tex.a * (1.0 - tex.a) * 4.0;
                    // Strongest where the light would actually catch the edge.
                    float lit = 0.45 + 0.55 * saturate(facing);
                    body = lerp(body, _RimColour.rgb, saturate(contour) * saturate(_Rim) * lit);
                }
                return half4(body, tex.a) * input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
