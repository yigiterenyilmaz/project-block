// PURPOSE: The surface of "Hidrolik pres"'s compressed cube - the industrial slate shell that four
// cubes are shut inside. Drawn as ONE plate over the cell (CompressedCubeView), per renderer
// through a MaterialPropertyBlock, because everything about it is a surface rather than a sprite:
//
//   SEAMS      the 2x2 quadrant memory: a cross pressed into the face where the four cells met.
//              _Seams.x how deep, _Seams.y how wide. This is the cube's one piece of identity -
//              it says "four of something is in here" without showing their colours.
//   DIMPLE     a recessed pressure dimple in the middle, _Dimple tightening it. The idle cycle
//              breathes THIS, not the cube's scale: a compressed object does not wobble.
//   CORNERS    four small compression stresses, so the plate reads as pressed rather than cut.
//   LIGHT      a low-gloss matte steel response biased to the game's own upper-left light, in the
//              plate's face space. Never a specular dot, never a rim glow.
//   SCARS      the turn countdown, and it is a MATERIAL STATE rather than a counter. Four short
//              tapered PRESSURE SCARS run out of the central cross into the quadrants - creases
//              pressed into the shell, never circles and never lamps. _Scars xyzw is how loaded
//              each one is (0 barely there, 1 a locked mechanical crease); _Glints xyzw is a brief
//              burnt-amber strain travelling along one as it locks, which fades to nothing and
//              leaves the crease behind. Four orange dots is what this replaces: a player should
//              read "this capsule is under a lot of pressure now", not count lights.
//   LOAD       _Load (0..1 over the press's life) ages the whole shell with them: the dimple bites
//              deeper, the seams tighten and darken, the outer bevel takes compression, and each
//              quadrant's surface picks up a little concave stress. That progression - not the
//              scars alone - is what makes the four turns tell themselves apart.
//   MEMORY     the stored cubes' own colours under the seams - _Q0.._Q3 per quadrant, _Memory how
//              far up. Almost nothing in idle; it wakes for a moment on the release, which is what
//              tells the player the four cells really are still in there.
//   STRESS     _Stress takes the seams and corners toward burnt amber as the vessel overpressures.
//              Muted industrial amber, never a neon red alarm.
//   EDGES      _EdgeLead (xyzw = +x, +y, -x, -y, world units) pushes ONE side out a pixel or two,
//              which is how the release's direction is said; _Collapse pulls every edge toward the
//              middle while the centre holds, which is the failure's inward crush. Both are vertex
//              work, so the plate never scales as a whole - a cartoon scale on a metal object is
//              exactly the thing this replaces.
//
//
// THE FACE COORDINATE IS NOT ASSUMED. A sprite is one unit across only when its pixels-per-unit
// equals its pixel size, and the generated rounded plate is 64px at 128 PPU - HALF a unit. A shader
// that took `positionOS.xy * 2` as -1..1 was really getting -0.5..0.5, which put every corner
// feature outside the face: the countdown marks were never drawn at all, the corner stress could
// never trigger, and the dimple came out twice the size it was tuned to. So the half-extent is
// passed in (_FaceHalf, the sprite's own bounds) and the face is derived from it.
//
// It still assumes an UNROTATED renderer - which the plate always is. Tagged Universal2D, as the 2D renderer
// requires. Without this shader CompressedCubeView leaves the board's own slate cube alone and the
// press is a plain block again: readable, just not pressed.
Shader "ProjectBlock/PressShell"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Seams ("Seam depth, width, -, -", Vector) = (0.35, 0.035, 0, 0)
        _Dimple ("Dimple depth, radius, -, -", Vector) = (0.3, 0.17, 0, 0)
        _Corner ("Corner compression", Float) = 0.22
        _Gloss ("Matte steel response", Float) = 0.16
        _Scars ("How loaded each quadrant's pressure scar is", Vector) = (0, 0, 0, 0)
        _Glints ("Amber strain travelling along each scar, 0..1 position, <0 idle", Vector) = (-1, -1, -1, -1)
        _ScarGeom ("Inner start, outer end, half-width, depth (face units)", Vector) = (0.2, 0.62, 0.075, 0.5)
        _Load ("How far through its life the press is, 0..1", Float) = 0
        _Strain ("Uneven surface strain at the final turn's peak", Float) = 0
        _Amber ("Pressure amber", Color) = (0.72, 0.45, 0.16, 1)
        _Q0 ("Stored quadrant colour: anchor", Color) = (0, 0, 0, 0)
        _Q1 ("Stored quadrant colour: right", Color) = (0, 0, 0, 0)
        _Q2 ("Stored quadrant colour: up", Color) = (0, 0, 0, 0)
        _Q3 ("Stored quadrant colour: up-right", Color) = (0, 0, 0, 0)
        _Memory ("How far the stored colours come up", Float) = 0
        _Stress ("Overpressure stress", Float) = 0
        _EdgeLead ("Edge push: +x, +y, -x, -y (world)", Vector) = (0, 0, 0, 0)
        _Collapse ("Inward crush", Float) = 0
        _FaceHalf ("Half the sprite's object-space size", Float) = 0.5
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
                float4 _Seams;
                float4 _Dimple;
                float _Corner;
                float _Gloss;
                float4 _Scars;
                float4 _Glints;
                float4 _ScarGeom;
                float _Load;
                float _Strain;
                float4 _Amber;
                float4 _Q0;
                float4 _Q1;
                float4 _Q2;
                float4 _Q3;
                float _Memory;
                float _Stress;
                float4 _EdgeLead;
                float _Collapse;
                float _FaceHalf;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // -1..1 across the plate, before anything moves. Derived from the sprite's
                // OWN half-extent, never from an assumed one-unit sprite.
                float2 face = input.positionOS.xy / max(_FaceHalf, 1e-4);
                float3 posOS = input.positionOS;
                // ONE SIDE LEADS. Each edge is pushed on its own, so the plate is never uniformly
                // scaled: the side the press is about to open on draws ahead of the others.
                float2 lead = float2(
                    max(face.x, 0.0) * _EdgeLead.x - max(-face.x, 0.0) * _EdgeLead.z,
                    max(face.y, 0.0) * _EdgeLead.y - max(-face.y, 0.0) * _EdgeLead.w);
                // THE INWARD CRUSH. The edges travel, the middle holds - the opposite of a balloon.
                float2 crush = -face * _Collapse * saturate(abs(face));
                float3 world = TransformObjectToWorld(posOS);
                world.xy += lead + crush;
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.face = face;
                return output;
            }

            // A soft line at zero, `w` wide, 1 on the line.
            float Line1D(float v, float w)
            {
                return 1.0 - smoothstep(0.0, max(w, 1e-4), abs(v));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float2 f = input.face;

                // ---- the 2x2 quadrant memory seams ----
                float seam = max(Line1D(f.x, _Seams.y), Line1D(f.y, _Seams.y));
                // The stored colour of whichever quadrant this pixel is in, brought up UNDER the
                // seam: strongest beside it, gone by the quadrant's own middle. Mixed beneath the
                // slate, never laid over it, so the shell's own shading survives.
                float4 q = f.x >= 0.0
                    ? (f.y >= 0.0 ? _Q3 : _Q1)
                    : (f.y >= 0.0 ? _Q2 : _Q0);
                float nearSeam = saturate(max(Line1D(f.x, _Seams.y * 6.0),
                    Line1D(f.y, _Seams.y * 6.0)));
                c.rgb = lerp(c.rgb, c.rgb * 0.55 + q.rgb * 0.75,
                    saturate(_Memory) * nearSeam * q.a);
                // A GROOVE, not a painted line: the dark cut with a hairline of catch-light on
                // its upper lip. Four painted lines across a face make it read as four tiles.
                c.rgb *= 1.0 - _Seams.x * seam;
                float lipY = Line1D(f.y - _Seams.y * 1.8, _Seams.y * 0.9);
                float lipX = Line1D(f.x + _Seams.y * 1.8, _Seams.y * 0.9);
                c.rgb += c.rgb * 0.10 * max(lipY, lipX) * _Seams.x;

                // ---- the recessed pressure dimple in the middle ----
                float r = length(f);
                float dim = 1.0 - smoothstep(0.0, max(_Dimple.y, 1e-4), r);
                // A recess, so: darker in the well and a thin brighter lip around it.
                c.rgb *= 1.0 - _Dimple.x * dim;
                float lip = Line1D(r - _Dimple.y, _Dimple.y * 0.45);
                c.rgb += c.rgb * 0.18 * lip * _Dimple.x;

                // ---- four corner compressions ----
                float corner = saturate((abs(f.x) + abs(f.y)) - 1.25);
                c.rgb *= 1.0 - _Corner * corner;

                // ---- low-gloss matte steel, biased to the game's upper-left light ----
                float lit = saturate(0.5 + 0.5 * dot(normalize(float2(-0.6, 0.8)), f));
                c.rgb *= 1.0 + _Gloss * (lit - 0.5);

                // ---- QUADRANT STRESS: the shell taking load as its life runs on ----
                // Each quarter of the face is drawn in a little at its middle, so the surface
                // reads as pressed rather than flat. Shading only - nothing is deformed.
                float quadR = length(abs(f) - 0.5);
                float stressShade = (1.0 - smoothstep(0.0, 0.45, quadR)) * saturate(_Load);
                c.rgb *= 1.0 - 0.10 * stressShade;

                // ---- the four PRESSURE SCARS ----
                // Short tapered creases running out of the central cross into each quadrant. A
                // crease, not a dot: it is the shell's own material locked under load.
                float scarMark = 0.0;
                float scarGlint = 0.0;
                [unroll]
                for (int q = 0; q < 4; q++)
                {
                    float2 dir = normalize(float2(q == 1 || q == 3 ? 1.0 : -1.0,
                        q >= 2 ? 1.0 : -1.0));
                    float2 a = dir * _ScarGeom.x;
                    float2 b = dir * _ScarGeom.y;
                    float2 ab = b - a;
                    float t = saturate(dot(f - a, ab) / max(dot(ab, ab), 1e-5));
                    float dist = length(f - (a + ab * t));
                    // Tapered: widest where it leaves the seam, closing to a point.
                    float halfW = _ScarGeom.z * (1.0 - 0.8 * t);
                    float m = 1.0 - smoothstep(halfW * 0.35, halfW, dist);
                    float load = saturate(q == 0 ? _Scars.x : q == 1 ? _Scars.y
                        : q == 2 ? _Scars.z : _Scars.w);
                    scarMark += m * load;
                    // The moment it locks: a short burnt-amber strain running out along it.
                    float g = q == 0 ? _Glints.x : q == 1 ? _Glints.y
                        : q == 2 ? _Glints.z : _Glints.w;
                    if (g >= 0.0)
                    {
                        scarGlint += m * (1.0 - smoothstep(0.0, 0.3, abs(t - g)));
                    }
                }
                // The crease is CUT INTO the shell, so it is a recess first and a colour second.
                c.rgb *= 1.0 - _ScarGeom.w * saturate(scarMark);
                // And a hairline of catch-light on its upper lip, which is what makes a groove a
                // groove rather than a painted stroke.
                c.rgb += c.rgb * 0.12 * saturate(scarMark) * (1.0 - saturate(_Load) * 0.4);
                // Amber is an ACCENT on strain, never the countdown itself.
                c.rgb = lerp(c.rgb, _Amber.rgb, saturate(scarGlint) * 0.55);

                // ---- the final turn's uneven strain: the shell working under load ----
                float unevenness = sin(f.x * 5.3 + f.y * 3.1) * 0.5 + 0.5;
                c.rgb *= 1.0 + 0.09 * saturate(_Strain) * (unevenness - 0.5);

                // ---- overpressure: the seams and corners go to burnt amber ----
                float stressed = saturate(_Stress) * saturate(seam * 0.8 + corner * 1.2 + dim * 0.5);
                c.rgb = lerp(c.rgb, _Amber.rgb * 0.8, stressed * 0.7);
                c.rgb *= 1.0 - 0.25 * saturate(_Stress) * (1.0 - stressed);

                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
