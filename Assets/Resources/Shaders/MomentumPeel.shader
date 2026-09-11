// PURPOSE: The sprite shader "Soğuk sökülme" (MomentumPeelView) draws a cube a moving board tore
// off with - the body, each lamina, the residue and the flecks - per renderer through a
// MaterialPropertyBlock:
//
//   LAMINA   _Band keeps one slice of the cube's own face, cut ACROSS the step (_Dir): straight
//            slices for a straight step; for a diagonal one, rounded chevrons that follow BOTH
//            leading faces (Slice) - cut straight across a diagonal, a square's corners come off
//            as triangles, and triangles read as shards. Soft by a fraction of a pixel.
//   MOTION   the vertices are carried _Motion.x along the step, stretched by _Motion.y from an
//            anchor on the slice's trailing side (so the leading edge moves first), narrowed by
//            _Motion.z across it and shifted _Across sideways. All linear - a quad stays true.
//   STREAK   _Band.w fades a stretched lamina toward its tail and lays the faintest cold edge on
//            its head - a short streak of material, not a trail of light.
//   COLD     _Tone takes the colour out (x) and the cold slate in (y).
//   BOARD    past _Clip (the board's rect) everything fades out over _ClipFade: almost at once for
//            the cube itself, which really goes BEHIND the board's edge, a little further for a
//            streak. Measured from the rect, so a corner exit clips round the corner.
//
// The face coordinate is the sprite's own object space (the body is one unit, pivot in its
// middle), so it assumes an unrotated renderer - which every lamina is. The residue and the flecks
// are rotated, but show their whole sprite (_Band wide open) and use only the board fade.
// Tagged Universal2D, as the 2D renderer requires. Without it MomentumPeelView carries the cube
// off as one plain sprite.
Shader "ProjectBlock/MomentumPeel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Dir ("Step direction (world, unit)", Vector) = (0, 1, 0, 0)
        _Centre ("Where the cube stood (world)", Vector) = (0, 0, 0, 0)
        _Half ("Cube half-size (world)", Float) = 0.5
        _Band ("Slice min, max, softness (face units along the step), streak", Vector) = (-10, 10, 0.02, 0)
        _Motion ("Travel (world), stretch, cross scale, anchor (face units)", Vector) = (0, 1, 1, -1)
        _Across ("Sideways offset (world)", Float) = 0
        _Tone ("Desaturation, cold", Vector) = (0, 0, 0, 0)
        _ColdTint ("Cold slate", Color) = (0.42, 0.48, 0.56, 1)
        _Clip ("Board rect (world): xMin, yMin, xMax, yMax", Vector) = (-10000, -10000, 10000, 10000)
        _ClipFade ("How far past the rect it fades out (world); 0 = no clip", Float) = 0
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
                float2 worldXY : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Dir;
                float4 _Centre;
                float _Half;
                float4 _Band;
                float4 _Motion;
                float _Across;
                float4 _Tone;
                float4 _ColdTint;
                float4 _Clip;
                float _ClipFade;
            CBUFFER_END

            float2 StepDir()
            {
                float2 d = _Dir.xy;
                return d / max(length(d), 1e-5);
            }

            // Where a point of the face lies across the step, -1 (trailing) to 1 (leading). A
            // straight step: the plain coordinate. A diagonal one: a smooth maximum of the two
            // leading axes, so the slices are chevrons with a rounded corner.
            float Slice(float2 face, float2 d)
            {
                float2 lead = face * sign(d);
                if (abs(d.x) < 0.01)
                {
                    return lead.y;
                }
                if (abs(d.y) < 0.01)
                {
                    return lead.x;
                }
                const float k = 0.35;
                float gap = lead.x - lead.y;
                return 0.5 * (lead.x + lead.y + sqrt(gap * gap + k * k)) - 0.5 * k;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                float2 d = StepDir();
                float2 p = float2(-d.y, d.x);
                float2 rel = world.xy - _Centre.xy;
                float along = dot(rel, d);
                float across = dot(rel, p);
                float anchor = _Motion.w * _Half;
                along = anchor + (along - anchor) * _Motion.y + _Motion.x;
                across = across * _Motion.z + _Across;
                world.xy = _Centre.xy + d * along + p * across;
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                // -1..1 across the body, before any of the motion.
                output.face = input.positionOS.xy * 2.0;
                output.worldXY = world.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float2 d = StepDir();
                float s = Slice(input.face, d);
                float band = smoothstep(_Band.x - _Band.z, _Band.x + _Band.z, s)
                    * (1.0 - smoothstep(_Band.y - _Band.z, _Band.y + _Band.z, s));
                // A lamina turning into a streak thins out toward its tail.
                float k = saturate((s - _Band.x) / max(_Band.y - _Band.x, 1e-4));
                float tail = lerp(1.0, smoothstep(0.0, 0.85, k), _Band.w);
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx, _Tone.x);
                c.rgb = lerp(c.rgb, _ColdTint.rgb * (0.35 + lum), _Tone.y);
                // The faintest cold edge on a streak's head, for its shape - matte, never a glow.
                float head = smoothstep(_Band.y - 0.12, _Band.y - _Band.z, s) * _Band.w;
                c.rgb = lerp(c.rgb, _ColdTint.rgb * 1.35, 0.22 * head);
                c.a *= band * tail;
                if (_ClipFade > 0.0)
                {
                    float2 past = max(_Clip.xy - input.worldXY, input.worldXY - _Clip.zw);
                    float outside = length(max(past, 0.0));
                    c.a *= 1.0 - smoothstep(0.0, _ClipFade, outside);
                }
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
