// PURPOSE: The surface of "Parazit"'s harness - the clasp that rides a HOST CUBE. Used by every
// piece of it (ParasiteHostView): the four corner anchors, the four tethers running to the middle,
// the node they meet in, and the bound joker's core inside it. Per renderer through a
// MaterialPropertyBlock.
//
// It exists because the harness must not read as a TINT on the cube. The old host cube was its own
// colour lerped 55% toward magenta, which said "this one is pink" and nothing else - not that
// something is holding it down, not that a joker is riding it, not why a power bounced off it. So
// the parasite is separate geometry with its own material, and the cube underneath keeps its own
// colour, sprite and element entirely.
//
//   BODY       a low-gloss ORGANIC-HARD material: dark plum through bruised violet, lit from the
//              game's own upper-left, with the light broken across the piece rather than pooled in
//              a specular dot. _Tone picks where in that range this piece sits.
//   DEPTH      _Relief shades the piece's own form - brighter along its upper edge, darker under
//              its lower one - so an anchor reads as a thing SITTING ON the cube rather than a
//              decal printed on it.
//   SUBSURFACE _Sink darkens a piece toward its inner end, which is how a tether reads as passing
//              just under the cube's surface on its way to the middle instead of lying across it.
//   TENSION    _Tension is the one dial the whole animation drives: it deepens the body, sharpens
//              the edge sheen and pulls the highlight toward the piece's inner end. A clamp is
//              this going up; a bond failing is it collapsing.
//   SHEEN      _Sheen (0..1 along the piece, negative for none) is the connection signal that runs
//              node -> tether -> anchor on the idle pulse and again as a bond is made. A soft band
//              of the piece's own colour lifted, never an emissive line.
//   SEVER      _Sever cuts the piece short: everything past it is gone, and the last sliver before
//              it thins and darkens. That is what makes a tether BREAK rather than fade - the two
//              halves are two renderers, each severed from its own end.
//
// The piece's coordinate is its sprite's own object space normalised by _FaceHalf (a sprite is one
// unit across only when its PPU equals its pixel size, and the generated rounded sprite is not).
// x runs across the piece, y along it. Tagged Universal2D, as the 2D renderer requires. Without
// this shader ParasiteHostView draws the same geometry in flat plum, which still says a clasp is
// there - just without the material telling the story.
Shader "ProjectBlock/ParasiteHarness"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Deep ("Deep plum", Color) = (0.16, 0.09, 0.19, 1)
        _Body ("Bruised violet", Color) = (0.35, 0.18, 0.38, 1)
        _Rose ("Warm accent", Color) = (0.72, 0.36, 0.52, 1)
        _Tone ("Where in the range this piece sits", Float) = 0.5
        _Relief ("Its own form's shading", Float) = 1
        _Sink ("How far it sinks under the cube's surface toward its inner end", Float) = 0
        _Tension ("How hard it is pulling", Float) = 0
        _Sheen ("Connection signal position along the piece, <0 for none", Float) = -1
        _Sever ("Cut everything past this point along the piece, <0 for none", Float) = -1
        _Ridges ("Growth ridges across the piece - the nest's shell", Float) = 0
        _Rim ("Light on the piece's own contour - what keeps it off a dark block", Float) = 0
        _RimColour ("That light's colour", Color) = (0.58, 0.51, 0.62, 1)
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
                float4 _Deep;
                float4 _Body;
                float4 _Rose;
                float _Tone;
                float _Relief;
                float _Sink;
                float _Tension;
                float _Sheen;
                float _Sever;
                float _Ridges;
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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 f = input.face;
                // 0 at the piece's outer end, 1 at its inner one.
                float along = saturate(f.y * 0.5 + 0.5);

                // ---- the body: dark plum through bruised violet ----
                float tone = saturate(_Tone);
                float3 body = lerp(_Deep.rgb, _Body.rgb, tone);
                // Tension deepens it - a piece under load goes darker, not brighter.
                body = lerp(body, _Deep.rgb, saturate(_Tension) * 0.45);

                // ---- its own form: light on the upper edge, dark under the lower ----
                float upper = smoothstep(0.15, 1.0, f.y);
                float lower = smoothstep(0.15, 1.0, -f.y);
                float relief = saturate(_Relief);
                body *= 1.0 + 0.30 * upper * relief;
                body *= 1.0 - 0.34 * lower * relief;
                // Across the piece too, so it is round rather than flat.
                body *= 1.0 - 0.16 * smoothstep(0.4, 1.0, abs(f.x)) * relief;

                // ---- sinking under the cube's surface toward the inner end ----
                body *= 1.0 - 0.5 * saturate(_Sink) * along;

                // ---- the edge sheen, which tension sharpens ----
                float edge = smoothstep(0.62, 1.0, abs(f.x));
                body = lerp(body, _Rose.rgb, edge * (0.10 + 0.22 * saturate(_Tension)));

                // ---- GROWTH RIDGES ----
                // The nest is not a smooth bead. Three ridges run across it, each a lit crown with
                // its own shadow under it, so the body has a SHELL that has grown in stages rather
                // than a highlight that says "gem".
                if (_Ridges > 0.0)
                {
                    float band = sin(f.y * 7.4 - 0.6);
                    float crown = saturate(band);
                    float trough = saturate(-band);
                    body *= 1.0 + 0.22 * crown * _Ridges;
                    body *= 1.0 - 0.26 * trough * _Ridges;
                }

                // ---- the connection signal running along it ----
                if (_Sheen >= 0.0)
                {
                    float band = 1.0 - smoothstep(0.0, 0.28, abs(along - _Sheen));
                    body = lerp(body, _Rose.rgb, band * 0.55);
                }

                // THE CONTOUR RIM. A baked silhouette's alpha ramps across its own edge, so
                // the band where it is neither inside nor outside IS the contour - and lighting it
                // is what stops a dark plum crust from vanishing into a near-black cube. Only ever
                // as strong as the host needs (ParasiteContrastProfile); on a bright block it is
                // off, because there an outline is all it would read as.
                if (_Rim > 0.0)
                {
                    float contour = tex.a * (1.0 - tex.a) * 4.0;
                    body = lerp(body, _RimColour.rgb, saturate(contour) * saturate(_Rim));
                }
                half4 c = half4(body, tex.a) * input.color;

                // ---- severed: everything past the cut is gone, and the last sliver thins ----
                if (_Sever >= 0.0)
                {
                    if (along > _Sever)
                    {
                        c.a = 0.0;
                    }
                    else
                    {
                        float toCut = saturate((_Sever - along) / 0.22);
                        c.a *= toCut;
                        c.rgb *= 0.55 + 0.45 * toCut;
                    }
                }
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
