// PURPOSE: "Tamagotchi" FURIOUS - the hatred that leaves the character and gets into the screen.
// One lightweight material, drawn on two full-screen quads by TamagotchiHatredField:
//
//   _Mode 0, UNDER the board (so cells and cards keep every bit of their colour): a dark berry
//   vignette that is heavier at the edges and heaviest on the pet's side, an irregular PRESSURE
//   FRONT that runs out from the pet across the whole screen in a quarter of a second
//   (_WaveProgress 0..1 - a soft band with a low-frequency wobble on its radius, never a hard
//   circle), the pressure still passing behind it, a very faint smoky unevenness, and a neutral veil
//   that takes a little saturation out of whatever is behind (_Desaturation).
//
//   _Mode 1, OVER everything, at the very rim of the screen only: a magenta line and a violet one
//   beside it, a pixel or two wide and a little uneven along the edge - the "chromatic split" of a
//   screen under strain, drawn as two coloured lines because nothing here reads the frame back.
//
// No RenderTexture, no grab pass, no cloud texture: the unevenness is two octaves of value noise
// at a low frequency, moving so slowly it is felt as weight rather than seen as smoke. The alpha is
// written PERCEPTUALLY and converted for linear blending (a 0.10 near-black overlay blended in
// linear space is a 5% drop nobody sees - the trap the debt vignette and Tilsim's stain record).
//
// _Screen  xy = centre of the view (world), zw = its half extents
// _Source  xy = the pet (world), z = how far the wave has to run to cover the screen
Shader "ProjectBlock/TamagotchiHatred"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _HatredStrength ("Hatred strength", Range(0, 1)) = 0
        _BerryTint ("Berry tint", Color) = (0.20, 0.03, 0.12, 1)
        _Desaturation ("Desaturation", Range(0, 1)) = 0
        _EdgeChromaticOffset ("Edge chromatic offset (world)", Float) = 0.012
        _Vignette ("Vignette", Range(0, 1)) = 0.1
        _OrganicNoise ("Organic noise", Range(0, 1)) = 0.12
        _WaveProgress ("Wave progress", Range(0, 1.2)) = 0
        _Screen ("Screen (centre, half extents)", Vector) = (0, 0, 9, 5)
        _Source ("Source (x, y, reach)", Vector) = (8, -4, 20, 0)
        _Mode ("Mode (0 field, 1 rim)", Float) = 0
        _Clock ("Clock", Float) = 0
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
                float2 world : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _BerryTint;
                float4 _Screen;
                float4 _Source;
                float _HatredStrength;
                float _Desaturation;
                float _EdgeChromaticOffset;
                float _Vignette;
                float _OrganicNoise;
                float _WaveProgress;
                float _Mode;
                float _Clock;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.world = TransformObjectToWorld(input.positionOS).xy;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // a perceptual opacity, for blending in linear colour
            float LinearAlpha(float a)
            {
                return 1.0 - pow(saturate(1.0 - a), 2.2);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 world = input.world;
                float2 p = (world - _Screen.xy) / max(_Screen.zw, 0.001);
                float n = ValueNoise(world * 0.42 + _Clock * 0.05) - 0.5;
                float n2 = ValueNoise(world * 1.1 - _Clock * 0.03) - 0.5;

                if (_Mode > 0.5)
                {
                    // THE RIM: two thin lines at the edge of the screen, a little uneven along it
                    float2 toEdge = _Screen.zw - abs(world - _Screen.xy);
                    float e = min(toEdge.x, toEdge.y);
                    float off = max(_EdgeChromaticOffset, 0.0005) * (1.0 + n * 1.4);
                    float magenta = saturate(1.0 - e / off);
                    float violet = saturate(1.0 - abs(e - off * 1.6) / off);
                    float3 rim = float3(0.78, 0.10, 0.46) * magenta + float3(0.36, 0.14, 0.70) * violet;
                    float ra = saturate(magenta + violet * 0.8) * _HatredStrength;
                    return half4(rim / max(magenta + violet, 0.001), LinearAlpha(ra * 0.85)) * input.color;
                }

                // THE VIGNETTE: heavier toward the edges, heaviest on the pet's side
                float edge = max(length(p * float2(0.86, 0.96)), max(abs(p.x), abs(p.y)) * 0.92);
                float toward = saturate(1.0 - distance(world, _Source.xy) / max(_Source.z * 0.9, 0.001));
                float vig = smoothstep(0.50, 1.12, edge + n * _OrganicNoise + toward * 0.22);

                // THE PRESSURE FRONT: out from the pet, irregular, soft on both sides
                float reach = max(_Source.z, 0.001);
                float d = distance(world, _Source.xy) + (n * 1.6 + n2 * 0.7) * _OrganicNoise * 4.0;
                float r = _WaveProgress * reach;
                float w = reach * 0.11;
                float alive = saturate(_WaveProgress * 4.0) * saturate((1.08 - _WaveProgress) * 5.0);
                float front = exp(-((d - r) / w) * ((d - r) / w)) * alive;
                float behind = smoothstep(r, r - w * 2.5, d) * alive;

                float a = _Vignette * vig + 0.30 * front + 0.10 * behind;
                a += _OrganicNoise * 0.22 * saturate(n2 + 0.12) * (0.35 + 0.65 * vig) * _Vignette * 3.0;
                a = saturate(a * _HatredStrength);

                float3 col = _BerryTint.rgb * (0.6 + 0.9 * front);
                // the veil that takes the colour out of what is behind
                float veil = _Desaturation * _HatredStrength * (0.35 + 0.65 * vig);
                col = lerp(col, float3(0.085, 0.078, 0.095), saturate(veil * 1.6));
                a = saturate(a + veil * 0.35);
                return half4(col, LinearAlpha(a)) * input.color;
            }
            ENDHLSL
        }
    }

    // NO FALLBACK, on purpose: this is a full-screen quad, and a sprite shader standing in for it
    // would paint the whole screen. TamagotchiHatredField checks isSupported and draws nothing instead.
}
