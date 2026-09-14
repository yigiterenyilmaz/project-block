// PURPOSE: The COLOUR the parasite is taking out of its host. Drawn as a desaturated, dimmed copy
// of the cube's OWN sprite, laid over the board's cube and clipped to the wrap's coverage - so the
// block goes pale exactly where the membrane lies thick on it, and keeps its colour where the film
// is thin.
//
// This layer exists because the wrap alone could never say it. A translucent film over a cube reads
// as a film over a cube: clean, weightless, and - as the first pass proved - rather like glass. What
// makes a parasite read as a PARASITE is that the thing under it is dying: its colour is being
// pulled out of it. That cannot be done by tinting the cube (a flat tint is a global recolour, and
// the whole point is that it is LOCAL), and it cannot be done from inside the membrane's own shader
// either, which has no way to read what is behind it. So the cube's face is drawn a second time,
// drained, and masked to the coverage.
//
//   COVERAGE  the same irregular contour the membrane uses (_Shape), so the drain can never appear
//             anywhere the wrap is not, and the two move together.
//   FOLDS     _FoldA / _FoldB / _FoldC are the film's GATHERS, and the block is deadest under
//             them - the same three the membrane draws, so the heaviest wrap and the deepest
//             withering are in the same places rather than two unrelated patterns.
//   THICKNESS the drain follows the film's own thickness: heavy where the membrane is thick, almost
//             nothing where it is thin. That is what makes it read as being SUCKED OUT rather than
//             painted on.
//   WINDOWS   _WindowA / _WindowB are the membrane's own holes, and the block keeps ALL of its
//             colour in them. They have to be here as well as in the film: a hole with the drain
//             still running through it would say the cube is dying where nothing is touching it,
//             and the whole point of the negative space is that it is the one place the block is
//             still itself.
//   RELIEF    _Relief is the escape attempt. Where the cube pushes the film thin, its colour comes
//             BACK for a moment - the one beat in the idle where the block looks alive again.
//   DEATH     _Drain takes saturation and brightness out and pulls what is left a little toward the
//             parasite's own dead mauve. It never goes to greyscale and it never goes to black: a
//             blue host stays a blue host, just a poisoned one.
//
// Tagged Universal2D. Without it the host simply keeps its full colour, which costs the withering
// read and nothing else.
Shader "ProjectBlock/ParasiteDrain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Drain ("How much life is being taken", Float) = 0.9
        _Desat ("Saturation lost at full drain", Float) = 0.78
        _Dim ("Brightness lost at full drain", Float) = 0.34
        _Kill ("How far the block's own highlight is put out", Float) = 0.7
        _Curve ("How sharply the drain follows coverage", Float) = 1.4
        _Dead ("What the drained colour leans toward", Color) = (0.28, 0.2, 0.28, 1)
        _Shape ("Contour reach, lobe strength, lobe turn, edge softness", Vector) = (0.86, 0.13, 0.7, 0.1)
        _Thick ("Thickness variation - the drain follows it", Float) = 0.5
        _FoldA ("A gather: direction (radians), offset across, bow, half width", Vector) = (0, 0, 0, 0)
        _FoldB ("A second gather", Vector) = (0, 0, 0, 0)
        _FoldC ("A third gather", Vector) = (0, 0, 0, 0)
        _WindowA ("A hole in the film - the block keeps its colour: uv centre, strength, radius", Vector) = (0.5, 0.5, 0, 0.1)
        _WindowB ("A second hole", Vector) = (0.5, 0.5, 0, 0.1)
        _Relief ("Where the cube is pushing back through: uv centre, strength, radius", Vector) = (0.5, 0.5, 0, 0.3)
        _Stain ("A region left extra drained after a struggle: uv centre, strength, radius", Vector) = (0.5, 0.5, 0, 0.3)
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
                float _Drain;
                float _Desat;
                float _Dim;
                float _Kill;
                float _Curve;
                float4 _Dead;
                float4 _Shape;
                float _Thick;
                float4 _FoldA;
                float4 _FoldB;
                float4 _FoldC;
                float4 _WindowA;
                float4 _WindowB;
                float4 _Relief;
                float4 _Stain;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.face = input.positionOS.xy * 2.0;
                return output;
            }

            // The membrane's own contour - the same lobes, so the two layers agree everywhere.
            float ContourRadius(float2 f)
            {
                float a = atan2(f.y, f.x);
                float lobes = sin(a * 3.0 + _Shape.z) * 0.6
                    + sin(a * 5.0 - _Shape.z * 1.7) * 0.3
                    + sin(a * 2.0 + _Shape.z * 0.5) * 0.4;
                return _Shape.x + lobes * _Shape.y;
            }

            // How much one of the film's gathers thickens it here - the membrane's own maths, so
            // the two layers agree about where the wrap is heaviest.
            float FoldSwell(float2 f, float4 fold)
            {
                if (fold.w <= 1e-4)
                {
                    return 0.0;
                }
                float2 dir = float2(cos(fold.x), sin(fold.x));
                float2 across = float2(-dir.y, dir.x);
                float along = dot(f, dir);
                float u = (dot(f, across) - fold.y - sin(along * 1.9) * fold.z) / fold.w;
                return 1.0 - smoothstep(0.0, 1.0, abs(u));
            }

            // WHERE THIS POINT SITS INSIDE ONE OF THE FILM'S HOLES, 0..strength. Its own lobes,
            // so the opening is torn rather than punched - a circle with the block's full colour
            // inside it is a status light, and two of them are a pair of them.
            float WindowAt(float4 win, float2 uv)
            {
                if (win.z <= 0.0)
                {
                    return 0.0;
                }
                float2 v = uv - win.xy;
                float a = atan2(v.y, v.x);
                // NO ONE HARMONIC MAY DOMINATE. A single strong three-lobe term makes a clover,
                // which is a recognisable symbol and worse than the circle it replaced; three weak
                // ones at different frequencies just make the opening irregular.
                float p = win.x * 21.0 + win.y * 13.0;
                float r = win.w * (1.0 + 0.15 * sin(a * 2.0 + p)
                    + 0.11 * sin(a * 3.0 - p * 1.7)
                    + 0.07 * sin(a * 5.0 + p * 0.6));
                float d = length(v) / max(r, 1e-4);
                float f = 1.0 - saturate(d);
                return f * f * (3.0 - 2.0 * f) * win.z;
            }

            float RegionAt(float4 region, float2 uv)
            {
                float d = length(uv - region.xy) / max(region.w, 1e-4);
                float f = 1.0 - saturate(d);
                return f * f * (3.0 - 2.0 * f) * region.z;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                if (tex.a < 0.01)
                {
                    return half4(0, 0, 0, 0);
                }
                float2 f = input.face;
                float square = max(abs(f.x), abs(f.y));
                float round = length(f);
                float d = lerp(round, square, 0.55);
                float contour = ContourRadius(f);
                float soft = max(_Shape.w, 1e-4);
                float covered = 1.0 - smoothstep(contour - soft, contour + soft * 0.5, d);
                if (covered <= 0.001)
                {
                    return half4(0, 0, 0, 0);
                }

                // THE DRAIN FOLLOWS THE FILM'S THICKNESS. Heavy where the wrap is thick, almost
                // nothing where it is thin - which is what makes it read as being pulled out of the
                // block rather than painted over it.
                float grain = sin(f.x * 4.1 + 1.3) * sin(f.y * 3.3 - 0.7);
                float thickness = 0.55 + 0.45 * grain * saturate(_Thick);
                // DEADEST UNDER THE GATHERS: the same three ridges the film heaps into.
                float swell = max(FoldSwell(f, _FoldA),
                    max(FoldSwell(f, _FoldB), FoldSwell(f, _FoldC)));
                thickness = saturate(thickness * (1.0 + swell * 0.55));
                // TIERED BY COVERAGE. A curve above 1 keeps the thin edges honest while making
                // the heavy middle genuinely dead - the difference between a colour filter and a
                // block being consumed.
                float coverage = pow(saturate(covered * thickness), max(_Curve, 0.2));
                float strength = saturate(_Drain) * coverage;
                // Where the cube is pushing the film thin, its colour comes BACK.
                strength *= 1.0 - saturate(RegionAt(_Relief, input.uv)) * 0.85;
                // And a region it has already struggled in is left a little more drained.
                strength = saturate(strength + RegionAt(_Stain, input.uv) * 0.35 * covered);
                // THE HOLES KEEP THEIR COLOUR. Nothing is on the block there, so nothing is being
                // taken out of it there - which is what makes the negative space read as the cube
                // showing THROUGH rather than as a lighter patch of film.
                float holes = max(WindowAt(_WindowA, input.uv), WindowAt(_WindowB, input.uv));
                // The SAME threshold the membrane cuts its own alpha at, so the film and the colour
                // it is taking end in the same place - but only MOST of the way. The block inside a
                // hole has been under this thing the whole time; the film is simply not on it any
                // more. Taken all the way back it is a bright saturated disc on a dead face, which
                // is a status light rather than a window.
                strength *= 1.0 - 0.72 * smoothstep(0.25, 0.75, saturate(holes));

                // Saturation and brightness out, and what is left leans toward the parasite's dead
                // mauve. NEVER to greyscale and never to black: a blue host stays a blue host.
                float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                float3 c = lerp(tex.rgb, lum.xxx, _Desat * strength);
                c *= 1.0 - _Dim * strength;
                c = lerp(c, _Dead.rgb * (0.55 + lum * 0.6), 0.35 * strength);
                // THE MATERIAL DIES TOO, not just the colour. A cube's life is in its highlight -
                // the bright band its bevel catches - so the drain puts that out and flattens what
                // is left toward its own mid tone. Without this the block is merely repainted; with
                // it, its surface has stopped being alive.
                float bright = saturate((lum - 0.55) / 0.45);
                c = lerp(c, c * (1.0 - 0.55 * bright), _Kill * strength);
                float mid = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(c, lerp(mid.xxx, c, 0.55), _Kill * strength * 0.5);

                // Drawn OVER the board's own cube, so the alpha is how much of the drained version
                // shows through - the sprite's own shape is kept exactly.
                return half4(c, tex.a * strength);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
