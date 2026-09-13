// PURPOSE: A cube under "Hidrolik pres", on its way to being one of four PRESSED LAMINAE - and on
// its way back out again. Drawn on the cube's OWN face (HydraulicPressView), per renderer through a
// MaterialPropertyBlock, because flattening is a material change and not a scale:
//
//   FLATTEN    _Flatten pulls the face toward its own average colour (_Average, found on the CPU
//              from the tile itself) and flattens its contrast about that average. That is what
//              takes the BEVEL and the soft-3D shading out of a cube while leaving the material
//              perfectly recognisable - gold stays gold, water stays water. A scaleY of 0.2 is the
//              cheap version of this and is exactly what it replaces.
//   RELIEF     _Relief re-imposes the shading on the way OUT: the same dial run backwards, so a
//              lamina coming back from the press regains its volume instead of cross-fading into a
//              cube.
//   SEAM       _Seam lays a thin warm pressure line along the lamina's pressed edges (top and
//              bottom in its own face space), _SeamColour. This is the only light in the whole
//              effect and it is dull steel-amber: the press peak must never be a white flash.
//   DESATURATE _Tone.x takes a little colour out while it travels under pressure, _Tone.y takes it
//              toward cold slate. Both small - a lamina that has lost its hue has lost the point.
//   NEGATIVE   _Negative draws the plate as an EMPTY IMPRINT instead of a face: the texture's shape
//              only, filled with _NegativeColour and hollowed toward its middle, so a quadrant that
//              was empty is remembered as a dark negative plate and can never be mistaken for an
//              object. Its own alpha falls off at the rim, so it reads as pressed into the floor.
//   VOLUME     _Volume is what a CUBE has and a PLATE does not: a light band along the top of the
//              face and a dark one along the bottom. Turning it down is how the bevel goes, and it
//              is the whole reason a pressed lamina still reads as the same object.
//   PLATE      _PlateEdge puts back the ONE piece of depth a plate does have: a thin dark lip along
//              its bottom edge and a hairline catch on its top - one to three pixels of apparent
//              thickness. Without it a flattened cube is a sticker; with it, it is a thin object.
//   EDGE       _Press squeezes the face toward the lamina's own mid-line in the vertex stage. SMALL:
//              in a TOP-DOWN board a cube losing its thickness does not lose its FOOTPRINT, and
//              squashing the sprite to a fifth of its height is what turns a pressed cube into a UI
//              bar. The volume goes out of the SHADING; the silhouette stays a rounded square.
//
//
// THE FACE COORDINATE IS NOT ASSUMED. A sprite is one unit across only when its pixels-per-unit
// equals its pixel size, and the generated rounded plate is 64px at 128 PPU - HALF a unit. A shader
// that took `positionOS.xy * 2` as -1..1 was really getting -0.5..0.5, which put every corner
// feature outside the face: the countdown marks were never drawn at all, the corner stress could
// never trigger, and the dimple came out twice the size it was tuned to. So the half-extent is
// passed in (_FaceHalf, the sprite's own bounds) and the face is derived from it.
//
// It still assumes an UNROTATED renderer - which every lamina is. Tagged Universal2D, as the 2D renderer
// requires. Without this shader HydraulicPressView presses plain sprites, which still reads as four
// cubes going in and four coming out, just without the material telling the story.
Shader "ProjectBlock/PressLamina"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Average ("The face's own average colour", Color) = (0.5, 0.5, 0.5, 1)
        _Flatten ("Shading + bevel suppression", Float) = 0
        _Relief ("Volume coming back (release)", Float) = 0
        _Seam ("Pressed-edge line strength", Float) = 0
        _SeamColour ("Pressed-edge line colour", Color) = (0.78, 0.56, 0.28, 1)
        _Tone ("Desaturate, cold", Vector) = (0, 0, 0, 0)
        _ColdTint ("Cold slate", Color) = (0.42, 0.48, 0.56, 1)
        _Negative ("Draw as an empty imprint", Float) = 0
        _NegativeColour ("Imprint colour", Color) = (0.13, 0.15, 0.19, 1)
        _Press ("Silhouette squeeze toward the mid-line", Float) = 0
        _Volume ("How much bevel the face still has (1 cube, 0 plate)", Float) = 1
        _PlateEdge ("The plate's own visible thickness", Float) = 0
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
                float4 _Average;
                float _Flatten;
                float _Relief;
                float _Seam;
                float4 _SeamColour;
                float4 _Tone;
                float4 _ColdTint;
                float _Negative;
                float4 _NegativeColour;
                float _Press;
                float _Volume;
                float _PlateEdge;
                float _FaceHalf;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 face = input.positionOS.xy / max(_FaceHalf, 1e-4);
                float3 posOS = input.positionOS;
                // The silhouette gives only a LITTLE as the material flattens. This is a
                // top-down board: a pressed cube keeps its footprint and loses its bevel, so the
                // squeeze here is a few percent, not a fifth.
                posOS.y *= 1.0 - saturate(_Press) * 0.16;
                posOS.x *= 1.0 + saturate(_Press) * 0.04;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(posOS));
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.face = face;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 c = tex * input.color;
                float2 f = input.face;

                // ---- an EMPTY quadrant: the shape, and nothing of an object ----
                if (_Negative > 0.5)
                {
                    float hollow = saturate(0.35 + 0.65 * saturate(max(abs(f.x), abs(f.y))));
                    c.rgb = _NegativeColour.rgb * hollow;
                    // Pressed INTO the floor: faint in the middle, faintest at the rim.
                    c.a = tex.a * input.color.a * (0.30 + 0.30 * hollow);
                    return c;
                }

                // ---- FLATTEN: toward the face's own average, contrast taken out about it ----
                float k = saturate(_Flatten) * (1.0 - saturate(_Relief));
                c.rgb = lerp(c.rgb, _Average.rgb, k * 0.45);
                c.rgb = lerp(c.rgb, _Average.rgb + (c.rgb - _Average.rgb) * 0.35, k);

                // ---- a little colour out under pressure, a little cold in ----
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx, saturate(_Tone.x));
                c.rgb = lerp(c.rgb, _ColdTint.rgb * (0.35 + lum), saturate(_Tone.y));

                // ---- VOLUME: the bevel a cube has and a plate does not ----
                // A light band along the top of the face and a dark one along the bottom. At
                // _Volume 1 the face reads as a cube; taking it to 0 is the flattening, and it is
                // done to the SHADING rather than to the shape.
                float top = smoothstep(0.30, 1.0, f.y);
                float bottom = smoothstep(0.30, 1.0, -f.y);
                float vol = saturate(_Volume);
                c.rgb *= 1.0 + 0.26 * top * vol;
                c.rgb *= 1.0 - 0.30 * bottom * vol;
                // The side falloff goes with it, so the face stops being a rounded solid.
                float sides = smoothstep(0.45, 1.0, abs(f.x));
                c.rgb *= 1.0 - 0.14 * sides * vol;

                // ---- PLATE: the one piece of depth a thin plate really has ----
                // A dark lip along its bottom edge and a hairline catch on the top - a couple of
                // pixels of apparent thickness, which is what stops it reading as a sticker.
                float lip = smoothstep(0.80, 1.0, -f.y);
                float catchLight = smoothstep(0.86, 1.0, f.y);
                c.rgb *= 1.0 - 0.45 * lip * saturate(_PlateEdge);
                c.rgb = lerp(c.rgb, c.rgb * 1.22 + 0.02, catchLight * saturate(_PlateEdge) * 0.8);

                // ---- the pressed edges: one thin warm line, the only light in the effect ----
                float edge = smoothstep(0.72, 1.0, abs(f.y));
                c.rgb = lerp(c.rgb, _SeamColour.rgb, saturate(_Seam) * edge * 0.55);

                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
