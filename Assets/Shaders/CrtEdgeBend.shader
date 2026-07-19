// PURPOSE: Fullscreen CRT "edge bend" (barrel distortion) for retro mode. Written for URP's
// built-in Full Screen Pass Renderer Feature (Unity 6): it samples _BlitTexture through the
// standard fullscreen Vert/Varyings and warps the UVs outward, with a soft vignette.
//
// The game drives it with ONE global float, _CrtBend (0 = off / passthrough, 1 = full), set via
// Shader.SetGlobalFloat from the view whenever RoundRules.RetroMode changes. The per-material
// _BarrelAmount / _Vignette are the tunable strengths, multiplied by _CrtBend, so at bend 0 the
// pass is an exact passthrough (no warp, no darkening) and costs only a blit.
Shader "ProjectBlock/CrtEdgeBend"
{
    Properties
    {
        _BarrelAmount ("Barrel Amount", Range(0, 1)) = 0.28
        _Vignette ("Vignette", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "CrtEdgeBend"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BarrelAmount;
            float _Vignette;
            float _CrtBend; // global, set by the game (0 = off, 1 = on)

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float bend = saturate(_CrtBend);
                float2 c = uv - 0.5;
                float r2 = dot(c, c);

                // Push the image outward from the center by r^2: the classic barrel bulge.
                float2 warped = uv + c * r2 * (_BarrelAmount * bend);

                // Warped samples that fall off the frame are the curved black screen edge.
                if (warped.x < 0.0 || warped.x > 1.0 || warped.y < 0.0 || warped.y > 1.0)
                {
                    return half4(0.0, 0.0, 0.0, 1.0);
                }

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warped);
                col.rgb *= saturate(1.0 - r2 * _Vignette * bend); // soft corner vignette
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
