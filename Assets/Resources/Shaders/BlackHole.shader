// PURPOSE: "Kara Delik" - the hole itself, drawn procedurally on one quad a few cells wide: the EVENT
// HORIZON (a near-black disc with no detail in it, a soft 1-3 px lensing rim), the ACCRETION DISK
// (two bands of flowing matter at different speeds, uneven in thickness, with one or two denser
// patches carried round and a very local warm highlight - dark and cold for the most part, never a
// neon galaxy), the MASS layer (a faint second density band just outside the disk that fills with
// the round's swallowed count - not a HUD ring), and a faint darkening of the ground around it.
//
// Not a portal, not a vortex: no spiral lines, no stars, no glow. The disk is noise stretched along
// its own flow, in the disk's own rotating frame, with a periodic angle so there is no seam.
//
// Every number that moves comes from the view through a property block (_Clock is the view's own
// scaled clock, so the lab's time scale slows the hole with everything else). Positions are in CELLS:
// object space (a unit quad) times _Span. Output is premultiplied.
Shader "ProjectBlock/BlackHole"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Span ("Quad size in cells", Float) = 2.4
        _Clock ("Clock", Float) = 0
        _Seed ("Seed", Float) = 0
        _CoreR ("Event horizon radius (cells)", Float) = 0.17
        _Open ("Horizon open", Range(0, 1)) = 1
        _Pinch ("Space pinch", Range(0, 1)) = 0
        _Ignite ("Disk ignition", Range(0, 1)) = 1
        _Angle ("Inner disk angle (rad)", Float) = 0
        _Angle2 ("Outer disk angle (rad)", Float) = 0
        _Bright ("Disk brightness", Range(0, 3)) = 1
        _DiskScale ("Disk radius scale", Range(0.3, 1.5)) = 1
        _Silence ("Disk silenced", Range(0, 1)) = 0
        _Rim ("Rim strength", Range(0, 3)) = 1
        _Warm ("Warm highlight", Range(0, 2)) = 1
        _LensDark ("Ground darkening", Range(0, 2)) = 1
        _Progress ("Mass progress", Range(0, 1)) = 0
        _Pulse ("Mass pulse", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Debug ("Debug (0 off, 1 core, 2 disk, 3 mass)", Float) = 0
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
        Blend One OneMinusSrcAlpha

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

            CBUFFER_START(UnityPerMaterial)
                float _Span;
                float _Clock;
                float _Seed;
                float _CoreR;
                float _Open;
                float _Pinch;
                float _Ignite;
                float _Angle;
                float _Angle2;
                float _Bright;
                float _DiskScale;
                float _Silence;
                float _Rim;
                float _Warm;
                float _LensDark;
                float _Progress;
                float _Pulse;
                float _Fade;
                float _Debug;
            CBUFFER_END

            static const float TAU = 6.2831853;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.local = input.positionOS.xy;
                return output;
            }

            float BhHash(float2 i, float s)
            {
                return frac(sin(dot(i, float2(127.1, 311.7)) + s * 74.7) * 43758.5453);
            }

            // Value noise whose x wraps every `period` cells, so an angle can be fed in without a seam.
            float BhWrapNoise(float2 x, float period, float s)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float i0 = i.x - period * floor(i.x / period);
                float i1 = (i.x + 1.0) - period * floor((i.x + 1.0) / period);
                float a = BhHash(float2(i0, i.y), s);
                float b = BhHash(float2(i1, i.y), s);
                float c = BhHash(float2(i0, i.y + 1.0), s);
                float d = BhHash(float2(i1, i.y + 1.0), s);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float BhSq(float x)
            {
                return x * x;
            }

            float BhWrapAngle(float a)
            {
                return a - TAU * floor((a + 3.14159265) / TAU);
            }

            // One band of flowing matter: radial profile x streaks along the flow x uneven thickness.
            float BhBand(float r, float u, float r0, float width, float streakFreq, float seed)
            {
                float thick = 1.0 + 0.38 * sin(u + seed) + 0.22 * sin(u * 2.0 + seed * 1.7);
                float w = width * max(thick, 0.35);
                float t = (r - r0) / w;
                float profile = smoothstep(0.0, 0.28, t) * (1.0 - smoothstep(0.52, 1.0, t));
                // The flow: coarse along the angle, fine across the radius - arcs, not spokes.
                float ua = u / TAU * streakFreq;
                // ...and drifting INWARD with time, so the matter flows rather than the disk simply
                // turning as a rigid picture.
                float n = BhWrapNoise(float2(ua, r * 34.0 + _Clock * 0.45), streakFreq, seed) * 0.6
                        + BhWrapNoise(float2(ua * 2.0, r * 71.0 + _Clock * 0.8), streakFreq * 2.0, seed + 5.0) * 0.4;
                return profile * (0.12 + 0.88 * pow(n, 1.5));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.local * _Span;
                float r = length(p);
                float a = atan2(p.y, p.x);
                float coreR = max(_CoreR * _Open, 0.0001);
                float scale = _DiskScale;

                float3 C = 0;
                float A = 0;

                // The ground bends toward it: a faint darkening that dies well before the rim.
                float halo = _LensDark * 0.55 * (1.0 - smoothstep(_CoreR * 1.0, _CoreR * 4.6, r))
                    * max(_Open, _Pinch);
                A = halo;

                // ---- the accretion disk (two bands, the outer one slower)
                float silence = 1.0 - _Silence;
                float u1 = a - _Angle;
                float u2 = a - _Angle2;
                float inner = BhBand(r, u1, _CoreR * 1.12 * scale, _CoreR * 1.75 * scale, 7.0, _Seed);
                float outer = BhBand(r, u2, _CoreR * 2.10 * scale, _CoreR * 2.10 * scale, 5.0, _Seed + 11.0)
                    * (0.55 + 0.45 * saturate(_Progress * 1.6));
                // Density patches carried round with the flow.
                float p1 = exp(-BhSq(BhWrapAngle(u1 - 0.7)) / 0.30);
                float p2 = exp(-BhSq(BhWrapAngle(u1 + 2.3)) / 0.16) * 0.6;
                float patch = p1 + p2;
                // Ignition: the disk arrives as a few broken arcs before it closes.
                float arcs = frac(u1 / TAU * 3.0 + _Seed * 0.13);
                float ignite = saturate(_Ignite * 1.6 - arcs * 0.9 * (1.0 - _Ignite));
                ignite = lerp(ignite, 1.0, smoothstep(0.75, 1.0, _Ignite));

                float density = saturate((inner * (0.85 + 0.45 * patch) + outer * 0.75) * ignite);
                float diskA = saturate(density * 1.25 * _Bright) * silence;
                // Cold for the most part: violet-black -> indigo -> desaturated blue-violet.
                float3 deep = float3(0.045, 0.030, 0.085);
                float3 indigo = float3(0.200, 0.170, 0.420);
                float3 blueViolet = float3(0.540, 0.540, 0.800);
                float3 disk = lerp(deep, indigo, smoothstep(0.05, 0.45, density));
                disk = lerp(disk, blueViolet, smoothstep(0.45, 0.95, density) * 0.8);
                // The approaching side is a little brighter, and only the patches there go warm.
                float doppler = 0.5 + 0.5 * cos(a - _Angle - 0.9);
                float3 warm = float3(0.95, 0.72, 0.44);
                disk = lerp(disk, warm, saturate(patch * pow(doppler, 3.0) * inner * 0.9 * _Warm));
                disk *= 0.75 + 0.5 * _Bright * doppler;
                C = C * (1.0 - diskA) + disk * diskA;
                A = A + diskA * (1.0 - A);

                // ---- the mass layer: a faint second density band that fills with the count
                float massR0 = _CoreR * 4.05 * scale;
                float massT = (r - massR0 - 0.025 * sin(a * 3.0 + _Seed) - 0.015 * sin(a * 5.0 - _Angle2)) / (_CoreR * 0.55);
                float massProfile = smoothstep(0.0, 0.35, massT) * (1.0 - smoothstep(0.55, 1.0, massT));
                float along = frac((1.5707963 - a) / TAU);       // clockwise from the top
                float fill = smoothstep(_Progress + 0.02, _Progress - 0.02, along);
                float grain = pow(BhWrapNoise(float2((a - _Angle2) / TAU * 14.0, r * 26.0 + _Clock * 0.3), 14.0, _Seed + 23.0), 2.2);
                float massA = massProfile * fill * grain
                    * (0.10 + 0.45 * _Progress + 0.60 * _Pulse) * (0.4 + 0.6 * silence);
                float3 massCol = lerp(indigo, blueViolet, _Progress * 0.6);
                massCol = lerp(massCol, warm, smoothstep(0.75, 1.0, _Progress) * grain * 0.35);
                C = C * (1.0 - massA) + massCol * massA;
                A = A + massA * (1.0 - A);

                // ---- the event horizon: no light at all inside it
                float coreMask = 1.0 - smoothstep(coreR - 0.010, coreR + 0.006, r);
                float pinch = _Pinch * (1.0 - smoothstep(0.0, 0.045, r));
                float horizon = saturate(max(coreMask * _Open, pinch));
                float3 black = float3(0.006, 0.007, 0.016);
                C = C * (1.0 - horizon) + black * horizon;
                A = A + horizon * (1.0 - A);

                // ---- the lensing rim: a hair of bent light on the horizon's edge
                float rim = exp(-BhSq((r - coreR * 1.03) / 0.010)) * _Open * _Rim;
                float3 rimCol = float3(0.62, 0.62, 0.86);
                rimCol = lerp(rimCol, warm, pow(saturate(cos(a - _Angle - 0.9)), 10.0) * 0.8 * _Warm);
                // Bent light is brightest on the approaching side, never an even ring (an even ring is an icon).
                rim *= 0.75 * (0.18 + 0.82 * pow(saturate(cos(a - _Angle - 0.9) * 0.5 + 0.5), 3.0));
                C += rimCol * rim * 0.9;
                A = max(A, saturate(rim));

                if (_Debug > 0.5)
                {
                    float3 dbg = _Debug < 1.5 ? float3(1, 0, 0) * horizon
                        : _Debug < 2.5 ? float3(0, 1, 0) * diskA : float3(1, 1, 0) * massA * 3.0;
                    C = lerp(C, dbg, 0.8);
                    A = max(A, max(dbg.r, dbg.g));
                }

                float alpha = saturate(A) * _Fade * input.color.a;
                return half4(C * _Fade * input.color.a, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
