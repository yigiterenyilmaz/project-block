// PURPOSE: A resting Matruşka doll's MATERIAL - what makes it read as lacquered wood with something
// still inside it, instead of a small picture set on a cube.
//
// WHY LIGHT AND NOT MOTION. On the board a doll is a few dozen pixels tall, and a pixel of movement is
// not seen. What IS seen is the colour inside the sprite moving. So the idle's work is done here, in
// layers that each run on their own slow period and are small on their own:
//
//   DEPTH    the lacquer a touch warmer toward the top and a deeper wine toward the foot
//   WARMTH   a broad warm pocket that swells and settles under the lacquer of the lower body
//   CORE     a quieter warmth deeper in, strongest in the generations that still hold dolls
//   SEAM     the painted seam catching a little of that warmth at its peak - a joint, never a line
//   SHEEN    a wide soft highlight on the upper left that breathes and changes shape IN PLACE
//   GOLD     the ornament answering the sheen, and now and then one spot of it catching the light
//   RIM      a warm edge on the side the light comes from, where the dark board would swallow it
//
// Nothing here tints the whole doll at once, and the FACE takes part in none of it. Every region is
// read from the doll's material mask (R lacquer, G gold, B face, A seam), which Tools/ArtPrep cuts
// from the art itself.
//
// NO PER-RENDERER PROPERTIES, for the reason BlockWarp gives: a property block drops the renderer out
// of batching. Each generation has one shared material (its mask, its multipliers, its glint points),
// and each doll's phase comes from where it stands - so eight dolls never breathe or glint together.
//
// Written for the 2D renderer: the pass is tagged LightMode = Universal2D.
Shader "ProjectBlock/MatryoshkaIdle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _MaskTex ("Material mask (R lacquer, G gold, B face, A seam)", 2D) = "black" {}

        _WarmthStrength ("Body warmth", Range(0, 1)) = 0.3
        _WarmthSpeed ("Body warmth breaths per second", Range(0, 1)) = 0.22
        _WarmthScale ("Body warmth pocket size", Range(0.1, 1)) = 0.42
        _CoreStrength ("Inner core", Range(0, 1)) = 0.22
        _CoreSpeed ("Inner core breaths per second", Range(0, 1)) = 0.28
        _SeamResponse ("Seam response", Range(0, 0.5)) = 0.12
        _SeamWidth ("Seam response width", Range(0.05, 1)) = 0.45
        _SheenStrength ("Lacquer sheen", Range(0, 0.5)) = 0.2
        _SheenSpeed ("Lacquer sheen breaths per second", Range(0, 1)) = 0.16
        _SheenSoftness ("Lacquer sheen softness", Range(0, 1)) = 0.55
        _GlintStrength ("Gold glint", Range(0, 1)) = 0.6
        _GlintDuration ("Gold glint seconds", Range(0.05, 0.5)) = 0.18
        _GlintIntervalMin ("Gold glint interval min (s)", Range(1, 20)) = 4
        _GlintIntervalMax ("Gold glint interval max (s)", Range(1, 30)) = 10
        _GlintA ("Gold glint point A (sprite 0-1)", Vector) = (0.3, 0.6, 0, 0)
        _GlintB ("Gold glint point B (sprite 0-1)", Vector) = (0.7, 0.6, 0, 0)
        _GlintC ("Gold glint point C (sprite 0-1)", Vector) = (0.5, 0.45, 0, 0)
        _RimStrength ("Rim", Range(0, 1)) = 0.35
        _RimWidth ("Rim width (share of the doll's width)", Range(0.005, 0.1)) = 0.035
        _DepthStrength ("Colour depth", Range(0, 0.3)) = 0.08
        _PhaseVariation ("Phase variation between dolls", Range(0, 1)) = 1
        _SheenColor ("Sheen colour", Color) = (1, 0.93, 0.84, 1)
        _GlintColor ("Gold light colour", Color) = (1, 0.86, 0.56, 1)
        _RimColor ("Rim colour", Color) = (1, 0.64, 0.48, 1)
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
                float phase : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float _WarmthStrength;
                float _WarmthSpeed;
                float _WarmthScale;
                float _CoreStrength;
                float _CoreSpeed;
                float _SeamResponse;
                float _SeamWidth;
                float _SheenStrength;
                float _SheenSpeed;
                float _SheenSoftness;
                float _GlintStrength;
                float _GlintDuration;
                float _GlintIntervalMin;
                float _GlintIntervalMax;
                float4 _GlintA;
                float4 _GlintB;
                float4 _GlintC;
                float _RimStrength;
                float _RimWidth;
                float _DepthStrength;
                float _PhaseVariation;
                float4 _SheenColor;
                float4 _GlintColor;
                float4 _RimColor;
            CBUFFER_END

            static const float DollTau = 6.28318530718;

            // What warmth does to the lacquer: more red, a little less of everything else - a wine
            // warming, not a light coming on.
            static const float3 WarmShift = float3(1.28, 0.93, 0.86);

            float Hash(float a, float b)
            {
                return frac(sin(a * 127.1 + b * 311.7) * 43758.5453);
            }

            // A breath rather than a sine: it rises, holds a moment at the top, and settles slowly.
            float Breath(float x)
            {
                return smoothstep(0.0, 0.35, x) * (1.0 - smoothstep(0.5, 1.0, x));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                // The doll's own place on the board, as its phase in every cycle below.
                float3 originWS = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                output.phase = (originWS.x * 2.17 + originWS.y * 3.41) * _PhaseVariation;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);
                float lacquer = mask.r;
                float gold = mask.g;
                float keep = 1.0 - mask.b;          // the face takes part in nothing
                float seam = mask.a;
                float3 col = texel.rgb;
                float t = _Time.y;
                float ph = input.phase;
                // The sprite is taller than it is wide; this keeps spots and offsets round.
                float aspect = _MainTex_TexelSize.x > 0.0
                    ? _MainTex_TexelSize.y / _MainTex_TexelSize.x : 0.6;

                // DEPTH - warmer toward the top, a deeper wine toward the foot, on the lacquer only.
                col *= 1.0 + _DepthStrength * lacquer * keep * (uv.y - 0.45) * 2.0
                    * float3(1.2, 0.5, 0.3);

                // Every layer below ADDS a little warm light as well as shifting colour. On dark wine
                // lacquer a shift of hue alone does not change how bright a pixel is, and at board size
                // it simply was not seen: measured, the whole idle moved the image by a quarter of a
                // percent while the one layer that added light - the rim - was all anyone could see.

                // WARMTH - a broad warm pocket in the lower body that drifts a little and breathes.
                float wt = t * _WarmthSpeed + ph * 0.37;
                float breath = Breath(frac(wt));
                float2 pocket = float2(0.5 + 0.06 * sin(DollTau * wt * 0.5 + ph),
                                       0.30 + 0.04 * sin(DollTau * wt * 0.37 + ph * 1.3));
                float2 dp = (uv - pocket) / float2(_WarmthScale * 0.75, _WarmthScale * 0.8);
                float warm = exp(-dot(dp, dp) * 2.0) * breath * _WarmthStrength * lacquer * keep;
                col = lerp(col, col * WarmShift, warm);
                col += warm * float3(0.30, 0.09, 0.05);

                // CORE - always a little there, deeper in, a little more at the top of its own breath.
                float ct = t * _CoreSpeed + ph * 0.61;
                float coreBreath = 1.0 + 0.15 * Breath(frac(ct));
                float2 dc = (uv - float2(0.5, 0.28)) / float2(0.30, 0.18);
                float core = exp(-dot(dc, dc) * 1.6) * coreBreath * _CoreStrength * lacquer * keep;
                col = lerp(col, col * WarmShift, core * 0.5);
                col += core * float3(0.16, 0.05, 0.03);

                // SEAM - warmer where the joint is, only while the warmth peaks. Never a line of light.
                float seamBand = saturate((seam - (1.0 - _SeamWidth)) / max(_SeamWidth, 0.001));
                col += seamBand * _SeamResponse * breath * keep * float3(0.35, 0.16, 0.08);

                // SHEEN - wide and soft on the body's upper left, below the collar and clear of the face,
                // swelling and turning slightly where it is. It never travels.
                float st = t * _SheenSpeed + ph * 0.83;
                float sheenBreath = 0.5 + 0.5 * sin(DollTau * st);
                float angle = 0.35 + 0.12 * sin(DollTau * st * 0.5 + ph);
                float2 ds = (uv - float2(0.27, 0.46)) * float2(1.0, 1.0 / aspect);
                float2 turned = float2(ds.x * cos(angle) + ds.y * sin(angle),
                                       -ds.x * sin(angle) + ds.y * cos(angle));
                turned /= float2(0.10 + 0.02 * sin(DollTau * st * 0.7 + ph), 0.22 / aspect)
                    * (0.6 + _SheenSoftness);
                float sheen = exp(-dot(turned, turned) * 1.8) * sheenBreath * _SheenStrength
                    * lacquer * keep;
                col = lerp(col, _SheenColor.rgb, sheen);

                // GOLD - the ornament answers the sheen with a little light of its own...
                col += gold * sheenBreath * _GlintStrength * 0.12 * float3(1.0, 0.8, 0.45);

                // ...and now and then one spot of it catches the light. One spot, briefly, then a long
                // quiet: the time between glints runs from the interval's minimum to its maximum.
                float slot = max((_GlintIntervalMin + _GlintIntervalMax) * 0.5, 0.01);
                float spread = saturate((_GlintIntervalMax - _GlintIntervalMin)
                    / max(2.0 * (_GlintIntervalMax + _GlintIntervalMin), 0.01));
                float gt = t + ph * 3.1;
                float epoch = floor(gt / slot);
                float local = gt - epoch * slot;
                float start = (0.5 - spread + 2.0 * spread * Hash(epoch, ph)) * slot;
                float glintAge = saturate((local - start) / max(_GlintDuration, 0.001));
                float pulse = sin(glintAge * PI) * step(start, local) * step(local, start + _GlintDuration);
                float pick = Hash(epoch + 17.0, ph);
                float2 anchor = pick < 0.3333 ? _GlintA.xy : (pick < 0.6667 ? _GlintB.xy : _GlintC.xy);
                float2 dg = (uv - anchor) / float2(0.14, 0.14 * aspect);
                float glint = exp(-dot(dg, dg) * 2.2) * pulse * _GlintStrength * gold;
                col += glint * float3(0.60, 0.50, 0.28);

                // RIM - where stepping toward the light leaves the doll: its upper-left edge, strongest on
                // the hood and gone before the foot, so it reads as light arriving from above-left and not
                // as a line drawn down one side.
                float2 toLight = float2(-_RimWidth, _RimWidth * aspect);
                half outside = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + toLight).a;
                float rim = saturate(texel.a - outside) * _RimStrength * keep
                    * smoothstep(0.30, 0.80, uv.y);
                col = lerp(col, _RimColor.rgb, rim);

                                return half4(saturate(col), texel.a) * input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
