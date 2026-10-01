// PURPOSE: "Tamagotchi"'s ASSET CONTRACT - the names of its layered art, where each layer is
// pinned on the character, and its palette. The art is baked by Tools/ArtPrep/bake_tamagotchi.js
// into Resources/Art/Tamagotchi (one PNG per layer, pivot and 400 px per unit in its .meta), and
// THIS file is the only place the animator learns anything about it: replace a PNG with a painted
// one of the same name, pivot and scale and nothing else has to change (the brief: "if art later
// replaced, the animator should remain").
//
// UNITS. One unit is the BODY's width. Every position below is in those units from the body's
// base centre - the pivot the whole character squashes, sways and hops about. TamagotchiRig
// scales the lot to the board's cell size.
//
// THE PIVOT TABLE mirrors the baker. It is only used when a PNG has been imported as a plain
// texture rather than a sprite (a fresh clone before Unity has read the .meta): the sprite is then
// made at runtime with the same pivot, so the rig never has to care which happened.
// EXTENSION POINT: a new layer is a new PNG, a line in Pivots and a field in TamagotchiRig.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Sprites, placements and colours of the "Tamagotchi" character.</summary>
    public static class TamagotchiArt
    {
        public const string Folder = "Art/Tamagotchi/";
        public const float PixelsPerUnit = 400f;

        // ------------------------------------------------------------------ where layers sit
        public static readonly Vector2 Belly = new Vector2(0f, 0.34f);
        public static readonly Vector2 EarL = new Vector2(-0.18f, 0.92f);
        public static readonly Vector2 EarR = new Vector2(0.18f, 0.92f);
        public const float EarSplay = 16f;
        public static readonly Vector2 PawL = new Vector2(-0.43f, 0.36f);
        public static readonly Vector2 PawR = new Vector2(0.43f, 0.36f);
        public static readonly Vector2 FootL = new Vector2(-0.2f, 0.03f);
        public static readonly Vector2 FootR = new Vector2(0.2f, 0.03f);
        public static readonly Vector2 EyeL = new Vector2(-0.155f, 0.64f);
        public static readonly Vector2 EyeR = new Vector2(0.155f, 0.64f);
        public static readonly Vector2 BrowL = new Vector2(-0.16f, 0.8f);
        public static readonly Vector2 BrowR = new Vector2(0.16f, 0.8f);
        public static readonly Vector2 CheekL = new Vector2(-0.27f, 0.52f);
        public static readonly Vector2 CheekR = new Vector2(0.27f, 0.52f);
        public static readonly Vector2 Mouth = new Vector2(0f, 0.5f);

        /// <summary>The middle of the face - what the head group turns about.</summary>
        public static readonly Vector2 FaceCentre = new Vector2(0f, 0.62f);

        /// <summary>The catchlight's rest offset from its eye's centre, and how far the eyes can
        /// look in each axis (the iris moves a third of that, the catchlight the rest).</summary>
        public static readonly Vector2 CatchOffset = new Vector2(-0.025f, 0.035f);
        public const float LookRange = 0.04f;

        /// <summary>The height of the top of the head, in units - what a peek has to clear.</summary>
        public const float HeadTop = 1.04f;

        /// <summary>The eyes' height: a peek that shows "just the eyes" stops here.</summary>
        public const float EyeLine = 0.56f;

        /// <summary>The body sprite's width in units including the paws at rest - what the
        /// brief's "1.4-2.0 cell widths of total character width" is measured against.</summary>
        public const float TotalWidth = 1.26f;

        /// <summary>The upper lid has six baked closures, 0.2 .. 1.0.</summary>
        public const int LidFrames = 6;

        // ------------------------------------------------------------------ palette
        public static readonly Color CheekRose = new Color(0.957f, 0.471f, 0.573f);
        public static readonly Color CheekBerry = new Color(0.745f, 0.18f, 0.36f);
        public static readonly Color CheekBright = new Color(1f, 0.56f, 0.64f);
        public static readonly Color Raspberry = new Color(0.80f, 0.36f, 0.49f);
        public static readonly Color DeepRaspberry = new Color(0.55f, 0.13f, 0.30f);
        public static readonly Color Plum = new Color(0.27f, 0.10f, 0.20f);
        public static readonly Color Berry = new Color(0.59f, 0.23f, 0.38f);
        public static readonly Color WarmPink = new Color(1f, 0.70f, 0.76f);
        public static readonly Color Cream = new Color(1f, 0.965f, 0.90f);
        public static readonly Color GlowPink = new Color(0.84f, 0.24f, 0.47f);
        public static readonly Color DarkMagenta = new Color(0.36f, 0.05f, 0.24f);
        public static readonly Color BoardChunk = new Color(0.15f, 0.17f, 0.23f);

        // ------------------------------------------------------------------ loading
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The pivot of every layer in pixels from its bottom-left, and its size - the
        /// baker's own numbers, used only when a PNG was imported as a texture.</summary>
        private static readonly Dictionary<string, Vector4> Pivots = new Dictionary<string, Vector4>
        {
            { "body", new Vector4(480, 480, 240, 24) },
            { "belly", new Vector4(280, 220, 140, 110) },
            { "ear", new Vector4(96, 112, 48, 26) },
            { "paw", new Vector4(120, 96, 24, 48) },
            { "foot", new Vector4(120, 72, 60, 36) },
            { "eye_iris", new Vector4(96, 112, 48, 56) },
            { "eye_catch", new Vector4(64, 64, 32, 32) },
            { "eye_glow", new Vector4(160, 176, 80, 88) },
            { "eye_happy", new Vector4(112, 72, 56, 30) },
            { "brow", new Vector4(80, 40, 40, 20) },
            { "cheek", new Vector4(96, 72, 48, 36) },
            { "tongue_tip", new Vector4(72, 56, 36, 40) },
            { "tongue_strip", new Vector4(400, 40, 0, 20) },
            { "tongue_end", new Vector4(64, 64, 16, 32) },
            { "arm_strip", new Vector4(400, 64, 0, 32) },
            { "shadow", new Vector4(240, 72, 120, 36) },
            { "plate", new Vector4(220, 260, 110, 130) },
            { "plate_bitten", new Vector4(220, 260, 110, 130) },
            { "corner_shade", new Vector4(96, 96, 0, 0) },
            { "body_furious", new Vector4(480, 480, 240, 24) },
            { "paw_furious", new Vector4(136, 104, 24, 52) },
            { "ear_furious", new Vector4(96, 112, 48, 26) },
            { "foot_furious", new Vector4(120, 72, 60, 36) },
            { "bubble_tail", new Vector4(80, 80, 40, 62) },
            { "bubble_tail_furious", new Vector4(80, 96, 40, 76) }
        };

        /// <summary>The nine-slice borders of the two speech bubbles, in pixels (left, bottom,
        /// right, top): the corner's radius plus the soft shadow round it.</summary>
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "bubble", new Vector4(90, 94, 90, 94) },
            { "bubble_furious", new Vector4(64, 68, 64, 68) }
        };

        private static readonly Dictionary<string, Sprite> sliced = new Dictionary<string, Sprite>();

        /// <summary>
        /// A layer as a NINE-SLICED sprite (a speech bubble stretched to its text keeps its corners
        /// and the weight of its line). Made at runtime over the same texture, because the borders
        /// are the animator's contract and a sliced SpriteRenderer needs a full-rect mesh.
        /// </summary>
        public static Sprite GetSliced(string name)
        {
            Sprite sprite;
            if (sliced.TryGetValue(name, out sprite))
            {
                return sprite;
            }
            Sprite plain = Get(name);
            Vector4 border;
            if (plain == null || !Borders.TryGetValue(name, out border))
            {
                sliced[name] = plain;
                return plain;
            }
            Texture2D tex = plain.texture;
            sprite = Sprite.Create(tex, plain.rect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = name + " (sliced)";
            sliced[name] = sprite;
            return sprite;
        }

        /// <summary>A layer by name, or null when the art is missing (that layer simply does not
        /// draw - the character must never throw over a missing PNG).</summary>
        public static Sprite Get(string name)
        {
            Sprite sprite;
            if (cache.TryGetValue(name, out sprite))
            {
                return sprite;
            }
            sprite = Resources.Load<Sprite>(Folder + name);
            if (sprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>(Folder + name);
                if (tex != null)
                {
                    Vector2 pivot = new Vector2(0.5f, 0.5f);
                    Vector4 p;
                    if (Pivots.TryGetValue(PivotKey(name), out p))
                    {
                        pivot = new Vector2(p.z / p.x, p.w / p.y);
                    }
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot,
                        PixelsPerUnit);
                    sprite.name = name;
                }
            }
            cache[name] = sprite;
            return sprite;
        }

        private static string PivotKey(string name)
        {
            if (name.StartsWith("eye_lid_") || name.StartsWith("eye_lowlid_") || name.StartsWith("eye_angry_"))
            {
                return "eye_iris";
            }
            if (name.StartsWith("mouth_") || name.StartsWith("chunk_") || name.StartsWith("fx_")
                || name.StartsWith("bite_") || name == "crack" || name == "ring_bead")
            {
                return "__centre";
            }
            return name;
        }

        /// <summary>The upper lid for one eye at a closure 0..1, or null when it is open.</summary>
        public static Sprite Lid(bool left, float closure)
        {
            if (closure <= 0.08f)
            {
                return null;
            }
            int frame = Mathf.Clamp(Mathf.CeilToInt(closure * LidFrames - 0.15f), 1, LidFrames);
            return Get("eye_lid_" + (left ? "L_" : "R_") + frame);
        }

        /// <summary>True when the baked art is present at all (the body is the one layer it
        /// cannot do without).</summary>
        public static bool Loaded
        {
            get { return Get("body") != null; }
        }
    }
}
