// PURPOSE: "Kara Delik" - MATTER UNDER A HOLE'S GRAVITY. One sprite shader for three jobs:
//   - LENSING: a board cube in a hole's ring 1 or 2 is drawn a little stretched toward the hole,
//     squeezed across it, dragged a pixel or two that way, its near edge bent in and its near side
//     darkened. The cube never leaves its cell: this is the picture only.
//   - THE PULL: a cube the rules moved a cell in is drawn travelling with the same deformation.
//   - THE SWALLOW: a cube (or a card) going in is SPAGHETTIFIED - long along the line to the hole,
//     thin across it - darkened and drained as it nears, and whatever part of it has crossed the
//     event horizon is simply not drawn (_Hole, per renderer: a SpriteMask would hide every other
//     proxy in its range too). It does not fade out; it goes behind the horizon.
//
// The deformation is in WORLD space about _Pivot along _Dir (unit, toward the hole), so it is the
// same whatever the sprite's own rotation - a turning cube is still stretched toward the hole, not
// along its own axis. It keeps the tile's own motion: the BlockWarp warp is here too, fed the tile's
// own numbers, so a lensed water cube goes on swirling.
Shader "ProjectBlock/BlackHoleMatter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _WarpAmp ("Warp amplitude (UV)", Range(0, 0.15)) = 0
        _WarpFreq ("Warp frequency", Range(0.5, 24)) = 6
        _WarpSpeed ("Warp speed", Range(0, 4)) = 0.7
        _WarpDrift ("Drift along the flow", Range(0, 1)) = 0
        _EdgeHold ("Edge hold", Range(0, 1)) = 1
        _EdgeSoft ("Edge mask width (UV)", Range(0.01, 0.4)) = 0.16
        _Swirl ("Stir", Range(-3, 3)) = 0

        _Pivot ("Pivot (world xy, z = half size)", Vector) = (0, 0, 0.5, 0)
        _Dir ("Toward the hole (world, unit)", Vector) = (1, 0, 0, 0)
        _Radial ("Radial stretch", Float) = 1
        _Tangent ("Tangential squeeze", Float) = 1
        _Offset ("Drag (world xy)", Vector) = (0, 0, 0, 0)
        _EdgeBend ("Near-edge pull (world)", Float) = 0
        _UVDrag ("Texture drag (uv)", Float) = 0
        _Darken ("Darkening", Range(0, 1)) = 0
        _Desat ("Desaturation", Range(0, 1)) = 0
        _Hole ("Hole (world xy, z = horizon radius)", Vector) = (0, 0, 0, 0)
        _HoleSoft ("Horizon softness (world)", Float) = 0.01
        _Occlude ("Occlude by the horizon", Range(0, 1)) = 0
        _Streak ("Rim streak", Range(0, 1)) = 0
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
                float2 worldPos : TEXCOORD2;
                float2 dirOS : TEXCOORD3;
                float nearSide : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _WarpAmp;
                float _WarpFreq;
                float _WarpSpeed;
                float _WarpDrift;
                float _EdgeHold;
                float _EdgeSoft;
                float _Swirl;
                float4 _Pivot;
                float4 _Dir;
                float _Radial;
                float _Tangent;
                float4 _Offset;
                float _EdgeBend;
                float _UVDrag;
                float _Darken;
                float _Desat;
                float4 _Hole;
                float _HoleSoft;
                float _Occlude;
                float _Streak;
            CBUFFER_END

            float4 _BlockFlowDir;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 ws = TransformObjectToWorld(input.positionOS);
                float2 dir = _Dir.xy;
                float len = length(dir);
                dir = len > 0.0001 ? dir / len : float2(1, 0);
                float2 d = ws.xy - _Pivot.xy;
                float along = dot(d, dir);
                float2 perp = d - along * dir;
                float halfSize = max(_Pivot.z, 0.0001);
                float nearT = saturate(along / halfSize);
                // Stretched along the pull, squeezed across it, and the side facing the hole
                // pulled in a little further than the rest (the near edge bends toward it).
                float2 deformed = dir * along * _Radial + perp * _Tangent + dir * (_EdgeBend * nearT * nearT);
                ws.xy = _Pivot.xy + deformed + _Offset.xy;
                output.positionCS = TransformWorldToHClip(ws);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                float3 originWS = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                output.phase = originWS.x * 2.17 + originWS.y * 3.41;
                output.worldPos = ws.xy;
                float2 dOS = mul((float3x3)GetWorldToObjectMatrix(), float3(dir, 0)).xy;
                float dl = length(dOS);
                output.dirOS = dl > 0.00001 ? dOS / dl : float2(1, 0);
                output.nearSide = along / halfSize;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _WarpSpeed + input.phase;
                if (abs(_Swirl) > 0.0001)
                {
                    float2 fromCentre = uv - 0.5;
                    float radius = saturate(length(fromCentre) * 2.0);
                    float falloff = (1.0 - radius) * (1.0 - radius);
                    float angle = _Swirl * falloff * (_Time.y + input.phase);
                    float sinA = sin(angle);
                    float cosA = cos(angle);
                    fromCentre = float2(fromCentre.x * cosA - fromCentre.y * sinA,
                                        fromCentre.x * sinA + fromCentre.y * cosA);
                    uv = fromCentre + 0.5;
                }
                float2 offset = 0;
                if (_WarpAmp > 0.00001)
                {
                    float2 w1 = float2(sin(uv.y * _WarpFreq + t), cos(uv.x * _WarpFreq * 0.9 - t * 1.1));
                    float2 w2 = float2(sin((uv.y + w1.y * 0.62) * _WarpFreq * 1.9 - t * 0.7),
                                       cos((uv.x + w1.x * 0.62) * _WarpFreq * 1.5 + t * 0.9));
                    offset = (w1 * 0.45 + w2 * 0.55) * _WarpAmp;
                    offset += _BlockFlowDir.xy * _WarpAmp * _WarpDrift * (0.5 + 0.5 * sin(t * 0.6));
                    float2 toEdge = min(uv, 1.0 - uv);
                    float mask = smoothstep(0.0, _EdgeSoft, min(toEdge.x, toEdge.y));
                    offset *= lerp(1.0, mask, _EdgeHold);
                }
                // The texture is dragged toward the hole: sample from the far side.
                float2 warped = uv + offset - input.dirOS * _UVDrag;
                float inset = _EdgeHold > 0.5 ? _EdgeSoft * 0.5 : 0.0;
                float2 clamped = clamp(warped, inset, 1.0 - inset);
                warped = lerp(warped, clamped, max(_EdgeHold, step(0.00001, abs(_UVDrag))));

                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped);
                half4 c = texel * input.color * _Color;

                // Light goes out on the side facing the hole first, then everywhere.
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, lum.xxx * float3(0.92, 0.9, 1.0), _Desat);
                float nearShade = saturate(0.55 + 0.45 * input.nearSide);
                c.rgb *= 1.0 - _Darken * nearShade;

                if (_Occlude > 0.001 && _Hole.z > 0.0)
                {
                    float dh = length(input.worldPos - _Hole.xy);
                    float outside = smoothstep(_Hole.z - _HoleSoft, _Hole.z + _HoleSoft, dh);
                    // Crushed toward black as it nears the horizon, then gone behind it.
                    float nearHorizon = 1.0 - smoothstep(_Hole.z, _Hole.z * 2.4, dh);
                    c.rgb *= 1.0 - nearHorizon * 0.75 * _Occlude;
                    // The last of it smears along the rim as a thin lit streak.
                    float rimD = (dh - _Hole.z * 1.05) / max(_HoleSoft * 1.5, 0.0001);
                    float rimBand = exp(-rimD * rimD);
                    c.rgb += float3(0.55, 0.52, 0.80) * rimBand * _Streak * c.a * 0.8;
                    c.a *= lerp(1.0, outside, _Occlude);
                }
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
