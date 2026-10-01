// PURPOSE: "Tamagotchi" EATING something - a card it was fed, a joker or power it snatched, a card
// off a pile, a chunk of the board's ground. Everything it eats is a PROXY drawn with this shader,
// and the shader's one job is the BITE: a world-space line at the pet's mouth with three rounded
// tooth marks hanging off it, everything beyond the line gone. The proxy is fed INTO the mouth by
// moving it across the line a step per chomp, so what is left always carries the scalloped edge of
// the last bite - it is chomped, never wiped and never faded (the brief: "scale 0 + fade = FAIL").
//
// WHY NOT A SpriteMask: a mask cuts with an alpha threshold, and its stair-stepped edge is exactly
// the "pixelated cut" the brief forbids; it also reveals EVERY masked sprite in its range, so two
// proxies in the mouth at once (a pile snack) would see through each other's bites. A line carried
// in a MaterialPropertyBlock belongs to one renderer, anti-aliases for free, and the material is
// SHARED - no clone per proxy.
//
// _Bite  xy = the world point at the middle of the bite line, z = the line's angle (radians, 0 = the
//        mouth is ABOVE the line and the bite takes everything above it), w = tooth radius (world)
// _Soft  world units of feather on the cut
// _On    1 = bite, 0 = draw everything (before the first chomp)
// _Edge  how dark the freshly bitten edge goes (a crust, only where the proxy actually is)
//
// Without this shader the proxy is drawn by Sprites/Default and the view falls back to a stepped
// shrink - the "simple bite proxy" the LOW quality setting asks for anyway.
Shader "ProjectBlock/TamagotchiBite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Bite ("Bite line (x, y, angle, tooth radius)", Vector) = (0, 0, 0, 0.05)
        _Soft ("Feather (world)", Float) = 0.004
        _On ("On", Float) = 0
        _Edge ("Bitten edge darkening", Range(0, 1)) = 0.45
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
                float2 world : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _Bite;
                float _Soft;
                float _On;
                float _Edge;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.world = TransformObjectToWorld(input.positionOS).xy;
                return output;
            }

            // Signed distance to what the bite took (negative inside the bite): the half-plane
            // beyond the line united with three round tooth marks hanging below it - the middle one
            // a little bigger and lower, so the edge reads as a mouthful and not as a saw.
            float BiteDistance(float2 world)
            {
                float2 d = world - _Bite.xy;
                float c = cos(_Bite.z);
                float s = sin(_Bite.z);
                float2 p = float2(c * d.x + s * d.y, -s * d.x + c * d.y);
                float r = _Bite.w;
                float plane = -p.y;
                float t1 = length(p - float2(-1.55 * r, -0.05 * r)) - 0.95 * r;
                float t2 = length(p - float2(0.0, -0.32 * r)) - 1.08 * r;
                float t3 = length(p - float2(1.55 * r, -0.05 * r)) - 0.95 * r;
                return min(plane, min(t1, min(t2, t3)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv)
                    * input.color * _Color;
                if (_On > 0.5)
                {
                    float dist = BiteDistance(input.world);
                    float soft = max(_Soft, 0.0001);
                    texel.a *= saturate(dist / soft + 0.5);
                    // the crust: the bitten edge darkens over a few pixels, on the proxy only
                    float crust = 1.0 - saturate(dist / (soft * 5.0));
                    texel.rgb *= 1.0 - _Edge * crust;
                }
                return texel;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
