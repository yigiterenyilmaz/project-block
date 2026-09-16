// PURPOSE: "Buzluk" - an ICE CRUST GROWING over water, and the frozen cube it leaves.
//
// THE FIRST PASS OF THIS SHADER WAS A TINT WIPE and it is worth recording why, because it looked
// reasonable in every still: one progress value, one ragged front, and the whole face lerped
// toward one ice colour behind it. On the board it read as the water being RECOLOURED. Water and
// ice came out as two brightnesses of one sprite.
//
// What replaced it is not a better colour. It is a different claim: frost buds on the wall,
// CRYSTAL FINGERS reaching into the water, a thin FILM closing the gaps between them, and the
// last liquid trapped in a shrinking irregular POCKET until it goes too. So the progress is not
// one number - it is five, and they overlap:
//
//   _Slow    1 -> 0     the water's own flow dying, and only where it is still liquid
//   _Seeds   0 -> 1     the buds on the wall
//   _Fingers 0 -> 1     the crystals reaching in
//   _Film    0 -> 1.16  the sheet closing between them (past 1, or the last pocket never seals)
//   _Thick   0 -> 1     the shell thickening after the surface is closed
//
// THE LIQUID POCKET IS NOT DRAWN. It is whatever the film has not reached, which is why it comes
// out irregular and off-centre rather than a shrinking disc: nobody chose its shape. The water
// also only MOVES there - the warp is scaled by the same mask - so the last swirl in the cube is
// in the last liquid in the cube, for free.
//
// THE SHAPE COMES FROM A BAKED FIELD (IceGrowth): R finger arrival, G film arrival, B cloud,
// A rim. Baked for a wall on the LEFT; every other wall is a uv swizzle here, and a CORNER
// samples two tiles and takes MIN of both arrivals, so two crystal fields grow and meet in the
// middle without either being coded for.
//
// THE FINISHED CUBE IS THREE PHYSICAL LAYERS, not a colour:
//   1. the water, BURIED - contrast and saturation taken right down, ~0.25 visible, still
//   2. the MILKY ICE BODY - with its own thickness variation, so it is not a flat plate
//   3. the FROSTED CRYSTAL RIM - irregular, and thicker on the side the cold came from
// plus the fingers themselves, which stay in it as crystal veins.
//
// Written for the 2D renderer: the pass is tagged LightMode = Universal2D or Renderer2D will
// not draw it at all.
Shader "ProjectBlock/CryoFreeze"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Growth ("Growth field (RGBA = finger/film/cloud/rim)", 2D) = "white" {}

        // ---- the timeline, per cube ----
        _Slow ("Water motion left", Range(0, 1)) = 0
        _Seeds ("Frost seeds", Range(0, 1)) = 1
        _Fingers ("Crystal fingers", Range(0, 1)) = 1
        _Film ("Ice film", Range(0, 1.4)) = 1.16
        _Thick ("Shell thickness", Range(0, 1)) = 1
        _Glint ("Interior glint", Range(-0.3, 1.3)) = -0.3

        // Which tile of the field, and which way the wall is. xy = atlas u offset/scale,
        // z = wall (0 left, 1 right, 2 down, 3 up), w = 1 when this wall is in use.
        _TileA ("Wall A tile", Vector) = (0, 0.25, 2, 1)
        _TileB ("Wall B tile", Vector) = (0, 0.25, 0, 0)

        // ---- the look ----
        _ColdTint ("Ice base (cool blue, NOT cyan)", Color) = (0.55, 0.74, 0.90, 1)
        _FrostColour ("Frost / rim", Color) = (0.90, 0.95, 0.99, 1)
        _CloudColour ("Milky ice body", Color) = (0.76, 0.84, 0.90, 1)
        _VeinColour ("Crystal finger core", Color) = (0.72, 0.86, 0.97, 1)

        _InnerWater ("How much of the water survives", Range(0, 0.6)) = 0.30
        _InnerFade ("How much further it is buried as the shell thickens", Range(0, 0.5)) = 0.10
        _IceBody ("Milky body strength", Range(0, 1)) = 0.30
        _IceBodyVar ("...and how much its own clouding adds", Range(0, 1)) = 0.45
        _FingerAmount ("Crystal fingers", Range(0, 1)) = 0.68
        _RimAmount ("Frosted rim", Range(0, 1)) = 0.80
        _EdgeBand ("Finger soft edge (must match IceGrowth)", Range(0.01, 0.2)) = 0.055
        _FilmBand ("Film soft edge", Range(0.01, 0.4)) = 0.13
        _SeedShare ("Seed share (must match IceGrowth)", Range(0.02, 0.4)) = 0.14

        // ---- the water underneath, copied from BlockWarp so _Slow 1 IS the water ----
        _WarpAmp ("Warp amplitude (UV)", Range(0, 0.15)) = 0.036
        _WarpFreq ("Warp frequency", Range(0.5, 24)) = 3.8
        _WarpSpeed ("Warp speed", Range(0, 4)) = 0.55
        _WarpDrift ("Drift along the flow", Range(0, 1)) = 0.35
        _EdgeSoft ("Edge mask width (UV)", Range(0.01, 0.4)) = 0.16

        // DEV. 1 = paint the wall sides (L red, R blue, D green, U yellow). 2 = the finger
        // field. 3 = the film field. 4 = the liquid pocket. So "is it coming off the side Core
        // said" and "is the pocket really the last unsealed region" stop being arguments.
        _Debug ("DEV: show a field", Range(0, 4)) = 0
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
            TEXTURE2D(_Growth);
            SAMPLER(sampler_Growth);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Slow;
                float _Seeds;
                float _Fingers;
                float _Film;
                float _Thick;
                float _Glint;
                float4 _TileA;
                float4 _TileB;
                float4 _ColdTint;
                float4 _FrostColour;
                float4 _CloudColour;
                float4 _VeinColour;
                float _InnerWater;
                float _InnerFade;
                float _IceBody;
                float _IceBodyVar;
                float _FingerAmount;
                float _RimAmount;
                float _EdgeBand;
                float _FilmBand;
                float _SeedShare;
                float _WarpAmp;
                float _WarpFreq;
                float _WarpSpeed;
                float _WarpDrift;
                float _EdgeSoft;
                float _Debug;
            CBUFFER_END

            float4 _BlockFlowDir;

            float Lum(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            // The field is baked for a wall on the LEFT. Turn the cube's uv into that tile's
            // local space for whichever wall this is, so one bake serves all four.
            float2 WallSpace(float2 uv, float wall)
            {
                if (wall < 0.5) { return uv; }                       // left
                if (wall < 1.5) { return float2(1.0 - uv.x, uv.y); } // right
                if (wall < 2.5) { return float2(uv.y, uv.x); }       // down
                return float2(1.0 - uv.y, uv.x);                     // up
            }

            // DEV only: left red, right blue, down green, up yellow.
            float3 WallColour(float wall)
            {
                if (wall < 0.5) { return float3(1.0, 0.15, 0.15); }
                if (wall < 1.5) { return float3(0.2, 0.4, 1.0); }
                if (wall < 2.5) { return float3(0.2, 1.0, 0.3); }
                return float3(1.0, 0.95, 0.2);
            }

            // One tile of the atlas, clamped half a texel inside so the next variant never
            // bleeds in along the seam.
            float4 SampleField(float2 uv, float4 tile)
            {
                float2 p = saturate(WallSpace(uv, tile.z));
                float inset = 0.5 * tile.y / 96.0;
                float u = tile.x + clamp(p.x * tile.y, inset, tile.y - inset);
                return SAMPLE_TEXTURE2D(_Growth, sampler_Growth, float2(u, p.y));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                float3 originWS = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                output.phase = originWS.x * 2.17 + originWS.y * 3.41;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // ---- 1. THE FIELD. A corner has two walls on, and MIN of the two arrivals is
                // two crystal fields growing toward each other and meeting in the middle.
                float4 F = SampleField(uv, _TileA);
                if (_TileB.w > 0.5)
                {
                    float4 B = SampleField(uv, _TileB);
                    F = float4(min(F.x, B.x), min(F.y, B.y), max(F.z, B.z), max(F.w, B.w));
                }

                // ---- 2. WHERE THE CRUST HAS GOT TO. The seeds run the first _SeedShare of the
                // finger field - the swelling at each finger's root IS its bud - and the fingers
                // carry it the rest of the way.
                float reach = max(_Seeds * _SeedShare,
                                  _SeedShare + _Fingers * (1.0 - _SeedShare));
                float fingerOn = saturate((reach - F.x) / max(_EdgeBand, 0.0001));
                float filmOn = saturate((_Film - F.y) / max(_FilmBand, 0.0001));
                // A finger is a partial seal: the film still has to close over it.
                float crust = saturate(max(fingerOn * 0.55, filmOn));
                float liquid = 1.0 - crust;

                // ---- 3. THE WATER, and it only MOVES WHERE IT IS STILL LIQUID. The warp is
                // BlockWarp's, scaled by the pocket - so the last swirl in the cube is in the
                // last liquid in the cube, and nothing had to be written to make that true.
                float motion = _Slow * liquid;
                float t = _Time.y * _WarpSpeed + input.phase;
                float2 w1 = float2(sin(uv.y * _WarpFreq + t),
                                   cos(uv.x * _WarpFreq * 0.9 - t * 1.1));
                float2 w2 = float2(sin((uv.y + w1.y * 0.62) * _WarpFreq * 1.9 - t * 0.7),
                                   cos((uv.x + w1.x * 0.62) * _WarpFreq * 1.5 + t * 0.9));
                float2 offset = (w1 * 0.45 + w2 * 0.55) * _WarpAmp;
                offset += _BlockFlowDir.xy * _WarpAmp * _WarpDrift * (0.5 + 0.5 * sin(t * 0.6));
                float2 toEdge = min(uv, 1.0 - uv);
                offset *= smoothstep(0.0, _EdgeSoft, min(toEdge.x, toEdge.y)) * motion;
                float inset = _EdgeSoft * 0.5;
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    clamp(uv + offset, inset, 1.0 - inset));
                float3 rgb = texel.rgb;

                if (_Debug > 0.5)
                {
                    float3 mark = rgb * 0.3;
                    if (_Debug < 1.5)
                    {
                        float wa = _TileA.z;
                        float wb = _TileB.w > 0.5 ? _TileB.z : -1.0;
                        mark += WallColour(wa) * saturate(1.0 - WallSpace(uv, wa).x * 4.0);
                        if (wb >= 0.0)
                        {
                            mark += WallColour(wb) * saturate(1.0 - WallSpace(uv, wb).x * 4.0);
                        }
                    }
                    else if (_Debug < 2.5) { mark = 1.0 - F.xxx; }
                    else if (_Debug < 3.5) { mark = 1.0 - F.yyy; }
                    else { mark = float3(liquid, liquid * 0.4, 0.1); }
                    return half4(saturate(mark), texel.a) * input.color * _Color;
                }

                // ---- 4. LAYER ONE: the water BURIED. Contrast and saturation taken right down
                // and leaned cold, with only _InnerWater of the real face left - and less of it
                // as the shell thickens. Built from the face's own LUMINANCE rather than lerped
                // toward a blue, because luminance is where the tile's frame, its bevel and its
                // lit top edge live and a lerp to a flat colour throws all three away.
                float lum = Lum(rgb);
                float keep = max(_InnerWater - _InnerFade * _Thick, 0.0);
                float3 inner = _ColdTint.rgb * (lum * 0.34 + 0.46);
                inner = lerp(inner, rgb, keep);

                // ---- 5. LAYER TWO: the MILKY ICE BODY, with its own thickness variation so it
                // is never a flat plate - some of it more translucent, some of it whiter.
                float milk = saturate(F.z * 1.25 - 0.12);
                float3 body = lerp(inner, _CloudColour.rgb,
                    (_IceBody + _IceBodyVar * milk) * (0.78 + 0.22 * _Thick));

                rgb = lerp(rgb, body, crust);

                // ---- 6. THE FINGERS THEMSELVES, which stay in the finished cube as veins.
                // Core pale blue, edge almost white: the depth into the finger is how far the
                // reach has got past this texel's own arrival, which IS its cross-section.
                float depth = saturate((reach - F.x) / max(_EdgeBand, 0.0001));
                float3 tone = lerp(_FrostColour.rgb, _VeinColour.rgb, depth);
                float nearTip = saturate(1.0 - (reach - F.x) / 0.26);
                float fresh = fingerOn * (0.48 + 0.52 * nearTip);
                rgb = lerp(rgb, tone, fresh * (_FingerAmount - 0.24 * _Thick));

                // ---- 7. LAYER THREE: the FROSTED CRYSTAL RIM, over everything, growing with
                // the shell. This is the layer that still says which way the cold came from -
                // the field bakes it thicker on the wall side.
                float rim = F.w * (0.55 + 0.45 * saturate(_Film)) * (0.80 + 0.20 * _Thick);
                rgb = lerp(rgb, _FrostColour.rgb, rim * _RimAmount);

                // ---- 8. THE FROZEN IDLE: one pale band drifting through, seconds apart, driven
                // from the view so a field of ice never shimmers in unison.
                if (_Glint > -0.2)
                {
                    float g = saturate(1.0 - abs((uv.x + uv.y) * 0.5 - _Glint) / 0.13);
                    rgb = lerp(rgb, _FrostColour.rgb, g * g * crust * 0.18);
                }

                return half4(rgb, texel.a) * input.color * _Color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
