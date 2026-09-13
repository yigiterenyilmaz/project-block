// PURPOSE: The parasite's palette, ADAPTED TO THE BLOCK IT IS ON.
//
// This file exists because of one failure that no amount of tuning fixes: a dark plum parasite on an
// OBSIDIAN cube is dark-on-dark, and the whole organism disappears. The membrane, the folds, the
// necrotic crust and the nest are all deep plum by design - which reads beautifully over cyan, red
// or gold and vanishes over a near-black block. Picking one palette and living with it means the
// mechanic is invisible on one of the two cubes in the game that most need to be read.
//
// So the palette is a FUNCTION of the host's own material. The parasite keeps its identity - it is
// always a bruised plum organism, never a neon outline and never a bright rim - but where the base
// is dark it opens its EDGES up (ash-lilac contours, a lighter separation rim, a paler crust edge)
// and where the base is bright it deepens them. Only the contrast moves; the hue family does not.
//
// The drain moves with it too: a near-black cube has almost no saturation left to take, so draining
// it hard says nothing and only makes the cell muddier. A bright saturated cube has everything to
// lose, so the drain bites.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The parasite's colours for one host, derived from what the host is made of.</summary>
    public struct ParasiteContrast
    {
        /// <summary>The translucent film.</summary>
        public Color Skin;

        /// <summary>Its contour, and the separation rim that keeps it off a dark background.
        /// </summary>
        public Color Edge;

        /// <summary>The thick wrap folds.</summary>
        public Color Fold;

        /// <summary>The dried necrotic crust, and the light on its curled edge - which is what
        /// makes a dead patch visible on a near-black cube at all.</summary>
        public Color Patch;

        public Color PatchEdge;

        public Color Vein;

        /// <summary>The nest's own rim.</summary>
        public Color NestRim;

        /// <summary>How hard the film pulls the colour out of this particular block, 0..1 of the
        /// configured strength.</summary>
        public float DrainScale;

        /// <summary>How strongly the contour rim is drawn. Near zero on a light block (the film's
        /// own darkness separates it) and real on a dark one.</summary>
        public float RimStrength;

        /// <summary>0 on a bright saturated host, 1 on a near-black one.</summary>
        public float Darkness;
    }

    /// <summary>Builds a host's palette from the block it is riding.</summary>
    public static class ParasiteContrastProfile
    {
        /// <summary>The parasite's own family, at its normal weight - what it looks like over a
        /// mid-bright block.</summary>
        private static readonly Color BaseSkin = new Color(0.26f, 0.13f, 0.28f);

        private static readonly Color BaseEdge = new Color(0.42f, 0.26f, 0.44f);

        private static readonly Color BaseFold = new Color(0.19f, 0.09f, 0.21f);

        private static readonly Color BasePatch = new Color(0.11f, 0.05f, 0.12f);

        private static readonly Color BasePatchEdge = new Color(0.38f, 0.28f, 0.40f);

        private static readonly Color BaseVein = new Color(0.56f, 0.26f, 0.38f);

        /// <summary>Where a DARK host's colours go: everything opens up toward ash and lilac, so the
        /// organism separates from a near-black block without ever becoming a neon outline.</summary>
        private static readonly Color AshSkin = new Color(0.40f, 0.31f, 0.44f);

        private static readonly Color AshEdge = new Color(0.62f, 0.54f, 0.66f);

        private static readonly Color AshFold = new Color(0.34f, 0.26f, 0.38f);

        private static readonly Color AshPatch = new Color(0.20f, 0.14f, 0.24f);

        private static readonly Color AshPatchEdge = new Color(0.58f, 0.51f, 0.62f);

        private static readonly Color AshVein = new Color(0.66f, 0.46f, 0.54f);

        /// <summary>
        /// The palette for a host made of <paramref name="paint"/> - the block's own MATERIAL colour
        /// (ViewUtil.CubeMaterialColor), never its renderer tint, which is white on a painted tile.
        /// </summary>
        public static ParasiteContrast For(Color paint)
        {
            float lum = 0.299f * paint.r + 0.587f * paint.g + 0.114f * paint.b;
            float max = Mathf.Max(paint.r, Mathf.Max(paint.g, paint.b));
            float min = Mathf.Min(paint.r, Mathf.Min(paint.g, paint.b));
            float sat = max > 0.0001f ? (max - min) / max : 0f;

            // How much this block is going to swallow a dark parasite. Obsidian sits near 1; cyan,
            // gold and red near 0. Squared, so only genuinely dark blocks get the lifted palette
            // and a mid-tone one is left alone.
            float darkness = Mathf.Clamp01(1f - lum / 0.42f);
            darkness *= darkness;

            var c = new ParasiteContrast
            {
                Darkness = darkness,
                Skin = Color.Lerp(BaseSkin, AshSkin, darkness),
                Edge = Color.Lerp(BaseEdge, AshEdge, darkness),
                Fold = Color.Lerp(BaseFold, AshFold, darkness),
                Patch = Color.Lerp(BasePatch, AshPatch, darkness),
                PatchEdge = Color.Lerp(BasePatchEdge, AshPatchEdge, darkness),
                Vein = Color.Lerp(BaseVein, AshVein, darkness),
                // The rim only earns its place on a dark block. On a bright one the film's own
                // darkness is the separation, and a rim would read as an outline.
                RimStrength = darkness * 0.85f
            };
            c.NestRim = Color.Lerp(c.Edge, Color.white, 0.08f * darkness);

            // A near-black block has almost no colour left to take, so draining it hard says
            // nothing and only muddies the cell. A bright saturated one has everything to lose.
            c.DrainScale = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(lum * 0.8f + sat * 0.5f));
            return c;
        }

        /// <summary>The palette for a host cube, straight from the cube itself.</summary>
        public static ParasiteContrast For(Cube cube)
        {
            return For(ViewUtil.CubeMaterialColor(cube));
        }
    }
}
