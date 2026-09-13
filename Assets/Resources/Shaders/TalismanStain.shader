// PURPOSE: "Tılsım"'s CURSE STAIN - the dark ground the claim lies on, and the only layer whose
// job is to take the light out of the outside space rather than to draw something on it.
//
// IT IS TWO MATERIALS OFF ONE SHADER, and that is the whole point of the file:
//
//   THE DRAIN   Blend DstColor OneMinusSrcAlpha, output premultiplied, so the background is
//               genuinely MULTIPLIED (dst * lerp(1, _Drain, a)). This is the half an alpha
//               overlay cannot do. The board renders in LINEAR colour, and that is what defeated
//               every previous attempt at this layer: a near-black quad at alpha 0.34 over the
//               backdrop lands at 0.87 of its sRGB luminance - a drop you have to be told about.
//               The design asks for 0.65-0.72, which needs the LINEAR value at about 0.43, and no
//               alpha in the range anybody would write by hand gets there.
//   THE STAIN   Blend SrcAlpha OneMinusSrcAlpha over the top. The drain darkens; this is what
//               pulls the hue and the saturation, so the area comes out colder and dirtier rather
//               than just dimmer.
//
// Both read ONE baked field (TalismanStain.cs) whose channels carry the whole animation:
//
//   A   coverage - the union of the patches, bridges, corner merges and edge tongues, combined
//       with MAX so two overlapping parts never blend twice and leave a lump.
//   R   depth - how far inside the MASS a texel is, from a blur of the finished union. Never
//       per-part: per-part depth is what made the first pass read as lumps, with the bridge ovals
//       and the cell circles visible through the tone.
//   G   growth - WHEN this texel belongs to the claim, normalised over the claim's span. One
//       float (_Front) then plays the whole thing: patches open from their middles, bridges close
//       from both ends at once, tongues arrive last. Run _Front backwards and the unseal is the
//       same map in reverse - the tongues retract first, the bridges open from the middle, the
//       patches shrink to their centres. Neither direction is coded for twice.
//   B   which part won the texel, for the lab's debug colours only.
//
// Nothing here decides anything. The cells, and which of them were reclaimed, are the rules'.
Shader "ProjectBlock/TalismanStain"
{
    Properties
    {
        [PerRendererData] _MainTex ("The baked field", 2D) = "white" {}
        // VECTORS, NOT COLOURS, and that is deliberate. A Color property is converted from
        // sRGB to linear on its way in, which is right for a swatch somebody picked by eye and
        // wrong for these two: they are LINEAR coefficients, measured against the backdrop's own
        // linear values, and a conversion would put the drain at (0.18, 0.32, 0.29) - roughly
        // twice the darkening that was actually calibrated.
        _Stain ("The stain's own colour, LINEAR", Vector) = (0.020, 0.052, 0.050, 1)
        _Drain ("What the ground is multiplied toward, LINEAR", Vector) = (0.46, 0.60, 0.58, 1)
        _Centre ("Opacity through the mass", Float) = 0.42
        _Edge ("Opacity at its rim", Float) = 0.17
        _DrainStrength ("How hard the colour is pulled out", Float) = 0.92
        _Front ("How far the growth has got", Float) = 1
        _Band ("How softly the front arrives", Float) = 0.09
        _Contract ("The seal pulse tightening the mass", Float) = 0
        _Sweep ("Where the seal energy is", Float) = -1
        _SweepWidth ("How wide that pass is", Float) = 0.12
        _SweepGain ("How much darker it leaves what it passed", Float) = 0
        _Mode ("0 stain, 1 drain", Float) = 0
        _Debug ("Colour the parts instead", Float) = 0
        _SrcBlend ("", Float) = 5
        _DstBlend ("", Float) = 10
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
        Blend [_SrcBlend] [_DstBlend]

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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Stain;
                float4 _Drain;
                float _Centre;
                float _Edge;
                float _DrainStrength;
                float _Front;
                float _Band;
                float _Contract;
                float _Sweep;
                float _SweepWidth;
                float _SweepGain;
                float _Mode;
                float _Debug;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float field = src.a;
                float depth = src.r;
                float growth = src.g;

                // THE FRONT. A texel belongs to the claim once the growth has reached its own
                // moment, and it arrives over a short band rather than switching on - which is
                // also why running _Front backwards is a retraction and not a fade.
                float on = saturate((_Front - growth) / max(_Band, 1e-4));
                on = on * on * (3.0 - 2.0 * on);
                field *= on;

                // The seal closing: the whole mass tightens a couple of per cent from its rim.
                field = saturate((field - _Contract) / max(1.0 - _Contract, 1e-4));

                // The talisman's energy running the network once, read off the same growth
                // coordinate - so what it passes is what the vines passed, in the same order.
                float s = (growth - _Sweep) / max(_SweepWidth, 1e-4);
                float passing = exp(-s * s) * _SweepGain * step(0.0, _Sweep);

                float a = (lerp(_Edge, _Centre, depth) + passing) * field * input.color.a;

                float3 tint = _Stain.rgb;
                if (_Debug > 0.5)
                {
                    // The lab's part colours: patch red, bridge blue, merge yellow, tongue green.
                    int cls = (int)(src.b * 8.0 + 0.5);
                    tint = cls == 1 ? float3(0.9, 0.15, 0.15)
                        : cls == 2 ? float3(0.15, 0.35, 0.95)
                        : cls == 3 ? float3(0.95, 0.85, 0.15)
                        : float3(0.2, 0.85, 0.3);
                    a = field * input.color.a * 0.75;
                }

                if (_Mode > 0.5)
                {
                    // THE DRAIN, premultiplied for Blend DstColor OneMinusSrcAlpha: the result is
                    // dst * lerp(1, _Drain, a), a real multiply of what is underneath. It follows
                    // the FIELD rather than the depth ramp - the colour goes out of the whole
                    // claim at once, and the ramp is the stain's business.
                    float k = saturate(field * _DrainStrength * input.color.a);
                    return half4(_Drain.rgb * k, k);
                }
                return half4(tint, saturate(a));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
