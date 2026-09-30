// PURPOSE: "Antimadde" - the cube that is about to stop being matter, drawn three ways off its OWN
// tile (AntimatterBlastView). One shader, three modes, because all three are the same silhouette
// and the whole event is about that silhouette:
//
//   0 MATTER  the real cube, held as a proxy after the rules emptied its cell. It dims and loses a
//             little colour when reality notices (_Desat, _Bright), wears a hair of chromatic
//             split and a violet edge (_Chroma, _Edge), is PULLED toward its own centre (_Pull,
//             sampled further out so the face is drawn in and the silhouette gives way from the
//             rim - never a scale), goes briefly and violently MORE itself (_Intensify, toward the
//             element's own deepest colour - gold goes deep rich gold, never white), and then its
//             colour SINKS into the middle (_Sink: the rim to void, the heart holds and heats).
//             The contact line (_Rim) is found from the silhouette's own alpha, so it hugs the
//             cube's real outline at one or two pixels whatever the tile is.
//   1 GHOST   the antimatter twin: the same silhouette with its own light - a dark ultraviolet
//             middle, a violet edge, cyan and magenta ghosts of its outline, a faint moving grain.
//             Transparent in the middle so the real matter still reads THROUGH it; it is an echo,
//             never a second solid block.
//   2 ECHO    the negative afterimage: the inverse of what stood there, pulled a little cold, with
//             a diffraction edge, internal grain, a small swim, and a dissolve that eats it from
//             the CENTRE outward (_Dissolve) - a flat inverted square is exactly what this is not.
//
// Sampled in the sprite's OBJECT space (one unit is one cube body, the pivot on its centre), with
// the tile's uv rectangle handed in (_UvMap, _UvClamp): a pull samples outside the texel it is
// drawing, so it has to know where the tile's own pixels stop. Every value comes through a
// MaterialPropertyBlock - one material for every cube. Without this shader the view falls back to
// plain sprites that tint and scale.
Shader "ProjectBlock/AntimatterMatter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _UvMap ("uv at the body centre (xy), uv per object unit (zw)", Vector) = (0.5, 0.5, 1, 1)
        _UvClamp ("tile uv rect min (xy), max (zw)", Vector) = (0, 0, 1, 1)
        _Mode ("0 matter, 1 ghost, 2 echo", Float) = 0
        _Pull ("radial pull (object units)", Float) = 0
        _Twist ("twist at the rim (radians)", Float) = 0
        _Desat ("desaturate (negative saturates)", Float) = 0
        _Bright ("brightness", Float) = 1
        _Matter ("matter colour", Color) = (1, 1, 1, 1)
        _Intensify ("toward the intense matter colour", Float) = 0
        _Sink ("colour sinking to the centre", Float) = 0
        _Rim ("contact rim", Float) = 0
        _RimColor ("rim colour", Color) = (0.9, 0.84, 1, 1)
        _Edge ("violet edge", Float) = 0
        _Chroma ("chromatic split (object units)", Float) = 0
        _Noise ("internal grain", Float) = 0
        _Swim ("uv swim (object units)", Float) = 0
        _Dissolve ("centre-out dissolve front", Float) = -1
        _Px ("one screen pixel in object units", Float) = 0.02
        _Clock ("clock", Float) = 0
        _Alpha ("alpha", Float) = 1
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
                float2 local : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _UvMap;
                float4 _UvClamp;
                float _Mode;
                float _Pull;
                float _Twist;
                float _Desat;
                float _Bright;
                float4 _Matter;
                float _Intensify;
                float _Sink;
                float _Rim;
                float4 _RimColor;
                float _Edge;
                float _Chroma;
                float _Noise;
                float _Swim;
                float _Dissolve;
                float _Px;
                float _Clock;
                float _Alpha;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.local = input.positionOS.xy;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // The tile at an OBJECT-space point, or nothing past the tile's own pixels.
            half4 Tile(float2 obj)
            {
                float2 uv = _UvMap.xy + obj * _UvMap.zw;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float inside = step(_UvClamp.x, uv.x) * step(_UvClamp.y, uv.y)
                    * step(uv.x, _UvClamp.z) * step(uv.y, _UvClamp.w);
                return c * inside;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 l = input.local;

                // A small swim (the echo only - everything else passes 0).
                l += _Swim * float2(sin(l.y * 19.0 + _Clock * 7.0), cos(l.x * 17.0 - _Clock * 6.0));

                // THE PULL: sample further out, harder toward the rim, so the face is drawn into
                // its own middle and the silhouette gives way from the edge. A little twist on
                // top for a liquid (water pulls in like something draining) - a VORTEX, strongest
                // in the middle and nothing at the rim: turning the rim turns the whole square,
                // and a cube that spins is not a liquid draining.
                float r = length(l);
                float2 dir = r > 1e-4 ? l / r : float2(0.0, 0.0);
                float2 s = l + dir * (_Pull * (0.35 + 1.6 * r));
                float tw = _Twist * pow(saturate(1.0 - r * 2.0), 1.5);
                float cs = cos(tw);
                float sn = sin(tw);
                s = float2(s.x * cs - s.y * sn, s.x * sn + s.y * cs);

                // 0 in the middle, 1 on the body's square edge.
                float boxR = max(abs(l.x), abs(l.y)) * 2.0;

                // The chromatic split: red one way, blue the other, green where it stands.
                float2 cd = float2(0.94, 0.34) * _Chroma;
                half4 c = Tile(s);
                half4 cr = Tile(s + cd);
                half4 cb = Tile(s - cd);
                half a = max(c.a, max(cr.a, cb.a) * 0.85);
                half3 rgb = half3(cr.r * cr.a, c.g * c.a, cb.b * cb.a) / max(a, 0.001);
                rgb *= input.color.rgb;
                a *= input.color.a;

                // The silhouette's edge, from its own alpha. Sampled unconditionally: a texture read
                // inside a branch is a gradient in flow control, and a handful of cubes can afford
                // four more taps.
                float p = _Px * 1.5;
                half a1 = Tile(s + float2(p, 0.0)).a;
                half a2 = Tile(s - float2(p, 0.0)).a;
                half a3 = Tile(s + float2(0.0, p)).a;
                half a4 = Tile(s - float2(0.0, p)).a;
                half edge = saturate((c.a - min(min(a1, a2), min(a3, a4))) * 2.0);

                half lum = dot(rgb, half3(0.299, 0.587, 0.114));

                if (_Mode < 0.5)
                {
                    // MATTER.
                    rgb = lerp(rgb, lum.xxx, saturate(_Desat));
                    rgb = saturate(rgb + (rgb - lum) * 0.8 * saturate(-_Desat));
                    rgb *= _Bright;
                    // Toward the element's own DEEPEST colour: the hue kept, the saturation pushed,
                    // lit by the tile's own shading so the bevel survives. Never white.
                    float3 m = _Matter.rgb;
                    float mMax = max(m.r, max(m.g, m.b));
                    float3 deep = pow(saturate(m / max(mMax, 0.001)), 1.8);
                    rgb = lerp(rgb, deep * (0.35 + 1.15 * lum), saturate(_Intensify));
                    // The colour sinks into the middle: the rim to void, the heart holds and heats.
                    half sinkEdge = saturate((boxR - (1.0 - _Sink)) / 0.4);
                    rgb = lerp(rgb, half3(0.06, 0.02, 0.12), sinkEdge * saturate(_Sink * 1.4));
                    rgb *= 1.0 + _Sink * 0.5 * saturate(1.0 - boxR * 1.6);
                    // Reality noticing: a breath of deep violet on the very edge. A wide pale band
                    // here turns the whole cube lavender, and the matter has to stay ITSELF.
                    rgb = lerp(rgb, half3(0.30, 0.12, 0.75), smoothstep(0.8, 1.0, boxR) * _Edge);
                    // The contact line: matter and antimatter touching, along the real outline.
                    rgb = lerp(rgb, _RimColor.rgb, edge * saturate(_Rim));
                    return half4(rgb, a * _Alpha);
                }

                half grain = Noise(l * 9.0 + float2(_Clock * 1.3, -_Clock * 0.9));

                if (_Mode < 1.5)
                {
                    // GHOST: the same silhouette with its own light - DEEP ultraviolet (these are
                    // linear values: a pale violet here reads as lavender paint over the matter),
                    // nearly clear in the middle so the real matter stays warm through it, and
                    // strongest along the outline, where the offset shows it as a twin.
                    half t = smoothstep(0.35, 1.0, boxR);
                    half3 g = lerp(half3(0.015, 0.004, 0.045), half3(0.20, 0.06, 0.62), t);
                    g = lerp(g, half3(0.55, 0.08, 0.45), smoothstep(0.88, 1.0, boxR) * 0.35);
                    g *= 0.75 + 0.5 * lum;
                    // Cyan and magenta ghosts of its outline, where the split leaves it.
                    g += half3(0.6, 0.05, 0.5) * saturate(cr.a - c.a) * 0.9;
                    g += half3(0.05, 0.5, 0.8) * saturate(cb.a - c.a) * 0.9;
                    g += half3(0.35, 0.2, 0.9) * edge * 0.5;
                    g *= 1.0 + (grain - 0.5) * _Noise;
                    g *= _Bright;
                    return half4(g, a * lerp(0.18, 1.0, t) * _Alpha);
                }

                // ECHO: the negative of what stood there, pulled a little cold - and a trace, not
                // a tile: thin through the middle, its outline diffracted into a cyan and a
                // magenta edge and lit, its grain visible. A flat inverted square is the failure.
                half3 neg = 1.0 - rgb;
                neg = lerp(neg, half3(0.36, 0.30, 0.78), 0.28);
                neg *= 1.0 + (grain - 0.5) * _Noise;
                half rimT = smoothstep(0.3, 1.0, boxR);
                neg += half3(0.55, 0.45, 1.0) * edge;
                neg += half3(0.6, 0.05, 0.5) * saturate(cr.a - c.a) + half3(0.05, 0.55, 0.8) * saturate(cb.a - c.a);
                neg *= _Bright;
                // Eaten from the middle outward, along a ragged front.
                half d = boxR + (Noise(l * 11.0 + 3.1) - 0.5) * 0.22;
                half keep = smoothstep(_Dissolve - 0.06, _Dissolve + 0.10, d);
                // The middle is a speckled haze in its own grain, never an even film.
                half body = lerp(0.2 + 0.6 * grain, 1.0, rimT);
                return half4(neg, a * keep * body * _Alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
