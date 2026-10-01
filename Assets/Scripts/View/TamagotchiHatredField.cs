// PURPOSE: "Tamagotchi"'s HATRED FIELD - the furious pet's anger leaving the character and getting
// into the screen. The corrective brief's point: fury that stays on the character is a face and a
// red tint; fury the whole game takes on is an event. This draws it and decides nothing - how
// strong, how far the wave has run and where the pet stands are TamagotchiView's (.Hatred).
//
// THREE RENDERERS, ONE MATERIAL (Resources/Shaders/TamagotchiHatred, a property block each - no
// clones, no RenderTexture, no grab pass):
//   FIELD   a full-screen quad UNDER the board (order -6: over the backdrop, the boss atmosphere and
//           the debt vignette, under the board's plate at -3), so cells, cubes and cards keep all
//           their colour. The berry vignette, the irregular pressure front and the desaturating veil.
//   RIM     a full-screen quad over everything, lit only at the very edge of the screen: the two
//           thin magenta / violet lines of a screen under strain. Zero most of the time.
//   SHADOW  the board's own outer shadow deepened (a soft frame round the arena): the hatred
//           pressing on the play area without the grid ever being resized.
//
// "Kredi kartı"'s vignette lives on the same edges. The two are never added into a black mess: the
// debt's strength is taken OUT of this one (Apply's debtVignette), so the screen shows the heavier
// of the two.
// Without the shader nothing is drawn at all (a stand-in sprite shader on a full-screen quad would
// paint the screen); the character's own acting and the background's mood still carry the fury.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class TamagotchiHatredField : MonoBehaviour
    {
        public const int FieldOrder = -6;
        public const int ShadowOrder = -5;
        public const int RimOrder = 58;

        /// <summary>The lab's switches: each part alone.</summary>
        public static class Layers
        {
            public static bool Vignette = true;
            public static bool Wave = true;
            public static bool Chromatic = true;
            public static bool BoardShadow = true;
            public static bool Desaturation = true;

            public static void AllOn()
            {
                Vignette = Wave = Chromatic = BoardShadow = Desaturation = true;
            }
        }

        private SpriteRenderer field;
        private SpriteRenderer rim;
        private SpriteRenderer shadow;
        private Material material;
        private MaterialPropertyBlock block;
        private bool supported;

        private static readonly int StrengthId = Shader.PropertyToID("_HatredStrength");
        private static readonly int TintId = Shader.PropertyToID("_BerryTint");
        private static readonly int DesatId = Shader.PropertyToID("_Desaturation");
        private static readonly int ChromaId = Shader.PropertyToID("_EdgeChromaticOffset");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int NoiseId = Shader.PropertyToID("_OrganicNoise");
        private static readonly int WaveId = Shader.PropertyToID("_WaveProgress");
        private static readonly int ScreenId = Shader.PropertyToID("_Screen");
        private static readonly int SourceId = Shader.PropertyToID("_Source");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int ClockId = Shader.PropertyToID("_Clock");

        /// <summary>What the field shows this frame.</summary>
        public struct Frame
        {
            public Rect Screen;
            public Rect Board;
            public Vector2 Source;
            public float Strength;      // 0..1, the whole thing
            public float Vignette;      // perceptual opacity at the far edge
            public float Desaturation;  // 0..1
            public float Chromatic;     // 0..1, the rim lines
            public float ChromaticOffset; // world units (1-2 px)
            public float Wave;          // 0..1.1, how far the pressure front has run
            public float BoardShadow;   // perceptual opacity of the arena's deepened shadow
            public float DebtVignette;  // what the debt's own vignette already shows
            public float Clock;
        }

        public static TamagotchiHatredField Create(Transform parent)
        {
            var go = new GameObject("TamagotchiHatredField");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<TamagotchiHatredField>();
            f.Build();
            return f;
        }

        private void Build()
        {
            Shader shader = Shader.Find("ProjectBlock/TamagotchiHatred");
            if (shader == null)
            {
                shader = Resources.Load<Shader>("Shaders/TamagotchiHatred");
            }
            supported = shader != null && shader.isSupported;
            block = new MaterialPropertyBlock();
            if (supported)
            {
                material = new Material(shader) { name = "TamagotchiHatred (shared)" };
                field = Quad("Field", FieldOrder);
                rim = Quad("Rim", RimOrder);
            }
            var sgo = new GameObject("BoardShadow");
            sgo.transform.SetParent(transform, false);
            shadow = sgo.AddComponent<SpriteRenderer>();
            shadow.sprite = TamagotchiArt.Get("soft_frame");
            shadow.sortingOrder = ShadowOrder;
            shadow.color = new Color(0f, 0f, 0f, 0f);
        }

        private SpriteRenderer Quad(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ViewUtil.WhiteSprite;
            r.sharedMaterial = material;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        /// <summary>True when the field can be drawn (the shader is there and runs).</summary>
        public bool Supported
        {
            get { return supported; }
        }

        public void Apply(Frame f)
        {
            float strength = Mathf.Clamp01(f.Strength);
            // the debt's vignette is on the same edges: show the heavier of the two, never the sum
            float vignette = Layers.Vignette ? Mathf.Max(0f, f.Vignette - f.DebtVignette * 0.8f) : 0f;
            bool anyField = strength > 0.002f && (vignette > 0.002f || (Layers.Wave && f.Wave > 0f && f.Wave < 1.08f)
                || (Layers.Desaturation && f.Desaturation > 0.002f));
            if (supported)
            {
                field.enabled = anyField;
                if (anyField)
                {
                    Place(field, f.Screen, 0.5f);
                    Write(field, f, strength, vignette, 0f);
                }
                bool anyRim = Layers.Chromatic && strength * f.Chromatic > 0.01f;
                rim.enabled = anyRim;
                if (anyRim)
                {
                    Place(rim, f.Screen, 0f);
                    Write(rim, f, strength * Mathf.Clamp01(f.Chromatic), vignette, 1f);
                }
            }
            // the arena's shadow, deepened: a soft frame scaled to the board (its inner edge is 0.25
            // of a unit from its middle)
            float sa = Layers.BoardShadow ? Mathf.Clamp01(f.BoardShadow * strength) : 0f;
            shadow.enabled = sa > 0.004f && f.Board.width > 0.01f;
            if (shadow.enabled)
            {
                shadow.transform.position = new Vector3(f.Board.center.x, f.Board.center.y, 0f);
                shadow.transform.localScale = new Vector3(f.Board.width / 0.5f, f.Board.height / 0.5f, 1f);
                // perceptual -> linear, like the shader
                shadow.color = new Color(0.07f, 0.01f, 0.05f, 1f - Mathf.Pow(1f - sa, 2.2f));
            }
        }

        private static void Place(SpriteRenderer r, Rect screen, float pad)
        {
            r.transform.position = new Vector3(screen.center.x, screen.center.y, 0f);
            r.transform.localScale = new Vector3(screen.width + pad, screen.height + pad, 1f);
        }

        private void Write(SpriteRenderer r, Frame f, float strength, float vignette, float mode)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(StrengthId, strength);
            block.SetColor(TintId, new Color(0.20f, 0.03f, 0.12f, 1f));
            block.SetFloat(DesatId, Layers.Desaturation ? Mathf.Clamp01(f.Desaturation) : 0f);
            block.SetFloat(ChromaId, Mathf.Max(0.0005f, f.ChromaticOffset));
            block.SetFloat(VignetteId, vignette);
            block.SetFloat(NoiseId, 0.12f);
            block.SetFloat(WaveId, Layers.Wave ? f.Wave : 0f);
            block.SetVector(ScreenId, new Vector4(f.Screen.center.x, f.Screen.center.y, f.Screen.width * 0.5f, f.Screen.height * 0.5f));
            // far enough for the front to cross the whole screen from wherever the pet is
            float reach = 0f;
            foreach (Vector2 corner in new[] { new Vector2(f.Screen.xMin, f.Screen.yMin), new Vector2(f.Screen.xMax, f.Screen.yMin),
                new Vector2(f.Screen.xMin, f.Screen.yMax), new Vector2(f.Screen.xMax, f.Screen.yMax) })
            {
                reach = Mathf.Max(reach, Vector2.Distance(corner, f.Source));
            }
            block.SetVector(SourceId, new Vector4(f.Source.x, f.Source.y, reach * 1.08f, 0f));
            block.SetFloat(ModeId, mode);
            block.SetFloat(ClockId, f.Clock);
            r.SetPropertyBlock(block);
        }

        public void Hide()
        {
            if (field != null)
            {
                field.enabled = false;
                rim.enabled = false;
            }
            if (shadow != null)
            {
                shadow.enabled = false;
            }
        }
    }
}
