// PURPOSE: "Kütleçekim Merkezi" - the board's pull, shown as a FIELD instead of as arrows.
//
// WHAT WAS WRONG WITH THE ARROWS. A row of pips outside one edge reads as a debug marker: it says
// which way, and nothing else. The power turns the physics of the whole arena for the rest of the
// round, and the interface has to carry that weight - so what replaces them is a current running
// across the board, denser where it is heading, gathering at the far edge. The direction should be
// felt before it is read.
//
// IT NEVER LIES ABOUT WHAT MOVES. Only WATER obeys this pull in the rules. So the field draws
// UNDER the cubes, thins over every cell seam, and nothing in it ever tugs at a block's outline -
// a player must never come away thinking their blocks are about to slide. What it animates is the
// space between them.
//
// DOWN DRAWS NOTHING. Every board falls downward, and a permanent marker for the ordinary case is
// just noise. The field only exists while gravity has been turned.
//
// GAMEPLAY IS THE ONLY SOURCE OF DIRECTION. This reads GameBoard.WaterFlow through
// BoardView.ShowGravity and keeps no truth of its own - it animates TOWARDS whatever it is told.
// Nothing here is ever read back by the rules.
//
// THREE INTENSITIES, deliberately separated: the ACTIVATION is a beat you notice, the IDLE is
// something you can sit next to for a whole round, and the water's own movement stays local and
// legible on top of both.
//
// EXTENSION POINT: a diagonal pull would need nothing here but a non-axis vector - everything is
// computed in lane space off the direction, and no direction is special-cased.

using UnityEngine;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>The directional gravity field, its motes, and the transitions between pulls.</summary>
    public sealed class GravityFieldView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ------------------------------------------------------------------ the field
            /// <summary>How much of the current is visible at rest. LOW: this sits on screen for a
            /// whole round, and the round is about the blocks, not about this.</summary>
            public static float FieldOpacity = 0.42f;

            public static float FlowSpeed = 0.42f;

            /// <summary>Features per cell across the pull. Higher is finer grain.</summary>
            public static float FlowScale = 0.85f;

            /// <summary>How far a feature is drawn out ALONG the pull. This is what turns a soft
            /// noise field into something with a direction.</summary>
            /// <summary>Long enough that a band crosses the WHOLE arena. At 4.2 a feature ran about
        /// three cells and the field broke into patches; the direction has to be one continuous
        /// thing or it is not a direction.</summary>
        public static float Stretch = 9.5f;

            /// <summary>How much denser the field is at the end it is heading for. A gradient,
            /// never a band.</summary>
            public static float DensityGradient = 0.80f;

            // ------------------------------------------------------------------ destination
            public static float EdgeStrength = 0.70f;

            /// <summary>In fractions of the board's length along the pull.</summary>
            public static float EdgeWidth = 0.13f;

            public static float EdgePulse = 0.10f;

            // ------------------------------------------------------------------ markers
            /// <summary>Chevrons across the destination edge. A SECONDARY readability layer - the
            /// motion is the primary one - so there are a few, and they are faint.</summary>
            public static float MarkerCount = 3f;

            public static float MarkerOpacity = 0.40f;

            public static float MarkerSize = 0.34f;

            // ------------------------------------------------------------------ refraction
            /// <summary>Only the FIELD bends. Bending the grid or the cubes would say the board
            /// itself is being moved, which is exactly the wrong thing to say.</summary>
            public static float DistortionStrength = 0.22f;

            public static float DistortionScale = 0.55f;

            // ------------------------------------------------------------------ motes
            /// <summary>How many drift across the board at once. Sparse: they are here to be
            /// felt at the edge of vision, not counted.</summary>
            public static int MoteCount = 26;

            public static float MoteSpeedMin = 0.85f;

            public static float MoteSpeedMax = 1.75f;

            /// <summary>Per second, as a fraction of the mote's own speed. The pull gets stronger
            /// the closer to the edge you are, which is what makes it read as weight rather than
            /// as wind.</summary>
            public static float MoteAcceleration = 1.5f;

            public static float MoteSize = 0.115f;

            public static float MoteOpacity = 0.50f;

            /// <summary>How far a mote is drawn out along its travel.</summary>
            public static float MoteStretch = 5.5f;

            /// <summary>A hair of sideways drift, as a fraction of the mote's own speed. Perfectly
            /// straight paths read as a machine; this is small enough that no mote ever wanders
            /// off the pull, which is the other half of the rule - a direction in doubt is a
            /// breeze, and this is meant to be weight.</summary>
            public static float MoteCrossDrift = 0.10f;

            // ------------------------------------------------------------------ transitions
            /// <summary>Turning the pull on from DOWN.</summary>
            public static float ActivationSeconds = 0.34f;

            /// <summary>How hard the field squeezes toward the middle as it recalibrates. Small:
            /// the power is called a gravity CENTRE, but a black hole would be a lie about what
            /// it does.</summary>
            public static float ActivationCompression = 0.85f;

            public static float ActivationImpulse = 1f;

            /// <summary>Turning from one non-down pull to another.</summary>
            public static float DirectionChangeSeconds = 0.30f;

            /// <summary>How far the field falls away while it turns, so the change reads as the
            /// old one collapsing and a new one arriving rather than as a rotation.</summary>
            public static float ChangeDip = 0.72f;

            /// <summary>Turning back to DOWN.</summary>
            public static float CollapseSeconds = 0.28f;

            // ------------------------------------------------------------------ shared
            /// <summary>How far the field steps out of a cell seam's way. It used to be 0.42, and
        /// that WAS the complaint: thinning hard at every seam stamps the cell pattern onto the
        /// field, so the current stopped reading as bands crossing the board and started reading
        /// as cyan light sitting on the grid. It is a faint secondary shimmer now - the field is
        /// low-contrast and draws UNDER the cubes, which is what actually protects readability.</summary>
        public static float GridStrength = 0.12f;

            public static Color DeepColor = new Color(0.06f, 0.12f, 0.16f);

            /// <summary>Pale, desaturated teal. Deliberately NOT the circuit's electric blue -
            /// that is a different power with a different register, and the two must never be
            /// mistaken for each other.</summary>
            public static Color FlowColor = new Color(0.34f, 0.62f, 0.72f);

            public static Color EdgeColor = new Color(0.62f, 0.86f, 0.92f);

            public static Color MoteColor = new Color(0.70f, 0.90f, 0.96f);
        }

        /// <summary>Under the cubes (1), over the board's plate. See BoardSurfaceView.</summary>
        private const int FieldOrder = -2;

        private const int MoteOrder = -1;

        // =================================================================== state

        private SpriteRenderer field;

        private Material material;

        private float clock;

        private bool built;

        private Rect area;                        // the board, in world units

        private float cellSize = 1f;

        private Vector2 sizeCells = Vector2.one;

        /// <summary>What gameplay says. Zero while gravity is DOWN - see Show.</summary>
        private Vector2 target;

        /// <summary>What is DRAWN. It turns towards the target rather than snapping, so a
        /// re-aim reads as the field being swung round.</summary>
        private float angle;

        private float targetAngle;

        private float strength;

        private float targetStrength;

        private float dip;                        // extra fall-off while turning

        private float compression;

        private float impulse;

        private float changeSpeed = 1f;

        private static readonly int TimeId = Shader.PropertyToID("_FieldTime");

        private static readonly int DirId = Shader.PropertyToID("_Dir");

        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private static readonly int CompressionId = Shader.PropertyToID("_Compression");

        private static readonly int ImpulseId = Shader.PropertyToID("_Impulse");

        // =================================================================== driving it

        /// <summary>
        /// Points the field along the board's pull.
        ///
        /// <paramref name="flow"/> is GameBoard.WaterFlow, straight from the rules - (0,-1) is an
        /// ordinary board and turns everything here off. The only decision made in here is how to
        /// get from what is drawn to what was asked for.
        /// </summary>
        public void Show(GridPos flow, Rect worldArea, float cell, int cellsWide, int cellsHigh)
        {
            bool down = flow.X == 0 && flow.Y == -1;
            area = worldArea;
            cellSize = Mathf.Max(cell, 0.0001f);
            sizeCells = new Vector2(Mathf.Max(cellsWide, 1), Mathf.Max(cellsHigh, 1));

            var wanted = down ? Vector2.zero : new Vector2(flow.X, flow.Y).normalized;
            bool wasOn = target.sqrMagnitude > 0.001f;
            bool turned = wanted != target;
            target = wanted;

            if (down)
            {
                if (wasOn)
                {
                    targetStrength = 0f;
                    changeSpeed = 1f / Mathf.Max(Style.CollapseSeconds, 0.01f);
                }
                Place();
                return;
            }

            EnsureField();
            Place();
            targetAngle = Mathf.Atan2(wanted.y, wanted.x);
            targetStrength = 1f;

            if (!wasOn)
            {
                // ARRIVING from an ordinary board: there is nothing to turn, so the field is born
                // already pointing the right way and the beat is all compression and shove.
                angle = targetAngle;
                strength = 0f;
                compression = Style.ActivationCompression;
                impulse = Style.ActivationImpulse;
                changeSpeed = 1f / Mathf.Max(Style.ActivationSeconds, 0.01f);
            }
            else if (turned)
            {
                // RE-AIMED: the old current has to be seen to let go before the new one takes, or
                // the change reads as a texture being rotated.
                dip = Style.ChangeDip;
                compression = Style.ActivationCompression * 0.7f;
                impulse = Style.ActivationImpulse * 0.8f;
                changeSpeed = 1f / Mathf.Max(Style.DirectionChangeSeconds, 0.01f);
            }
        }

        /// <summary>Tears everything down - a new round, or the board being rebuilt.</summary>
        public void Clear()
        {
            target = Vector2.zero;
            targetStrength = 0f;
            strength = 0f;
            dip = 0f;
            compression = 0f;
            impulse = 0f;
            if (field != null)
            {
                field.enabled = false;
            }
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i].Live = false;
                if (motes[i].R != null)
                {
                    motes[i].R.enabled = false;
                }
            }
        }

        // =================================================================== the field

        private void EnsureField()
        {
            if (field == null)
            {
                var go = new GameObject("Field");
                go.transform.SetParent(transform, false);
                field = go.AddComponent<SpriteRenderer>();
                field.sortingOrder = FieldOrder;
                field.sprite = ViewUtil.WhiteSprite;
                field.color = Color.white;
            }
            if (material == null)
            {
                Shader shader = Shader.Find("ProjectBlock/GravityField");
                material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                material.hideFlags = HideFlags.HideAndDontSave;
                field.sharedMaterial = material;
            }
            built = true;
            PushStyle();
        }

        /// <summary>Sits the quad on the board. One unit sprite scaled to the arena, so the
        /// shader gets clean 0..1 UVs whatever size the board is.</summary>
        private void Place()
        {
            if (field == null)
            {
                return;
            }
            field.transform.localPosition =
                new Vector3(area.center.x, area.center.y, 0f);
            field.transform.localScale = new Vector3(area.width, area.height, 1f);
        }

        private void PushStyle()
        {
            if (material == null)
            {
                return;
            }
            material.SetVector("_SizeCells", new Vector4(sizeCells.x, sizeCells.y, 0f, 0f));
            material.SetColor("_DeepColor", Style.DeepColor);
            material.SetColor("_FlowColor", Style.FlowColor);
            material.SetColor("_EdgeColor", Style.EdgeColor);
            material.SetFloat("_Opacity", Style.FieldOpacity);
            material.SetFloat("_FlowSpeed", Style.FlowSpeed);
            material.SetFloat("_FlowScale", Style.FlowScale);
            material.SetFloat("_Stretch", Style.Stretch);
            material.SetFloat("_DensityGradient", Style.DensityGradient);
            material.SetFloat("_EdgeStrength", Style.EdgeStrength);
            material.SetFloat("_EdgeWidth", Style.EdgeWidth);
            material.SetFloat("_EdgePulse", Style.EdgePulse);
            material.SetFloat("_Distortion", Style.DistortionStrength);
            material.SetFloat("_DistortionScale", Style.DistortionScale);
            material.SetFloat("_MarkerCount", Style.MarkerCount);
            material.SetFloat("_MarkerOpacity", Style.MarkerOpacity);
            material.SetFloat("_MarkerSize", Style.MarkerSize);
            material.SetFloat("_GridStrength", Style.GridStrength);
            material.SetFloat("_CellPhase", 0f);
        }

        // =================================================================== motes

        private struct Mote
        {
            public SpriteRenderer R;
            public Vector2 Pos;
            public float Speed;
            public float Life;
            public float Age;
            public float Size;
            public float Drift;
            public bool Live;
        }

        private Mote[] motes = new Mote[0];

        private static Sprite moteSprite;

        /// <summary>A soft blob, drawn out along its own travel by the transform - so one sprite
        /// serves every direction and nothing has to be re-rasterised when the pull turns.</summary>
        private static Sprite MoteSprite()
        {
            if (moteSprite != null)
            {
                return moteSprite;
            }
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Exp(-(u * u + v * v) * 3.2f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            moteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return moteSprite;
        }

        private void EnsureMotes()
        {
            if (motes.Length >= Style.MoteCount)
            {
                return;
            }
            var next = new Mote[Style.MoteCount];
            System.Array.Copy(motes, next, motes.Length);
            for (int i = motes.Length; i < next.Length; i++)
            {
                var go = new GameObject("Mote" + i);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = MoteSprite();
                r.sortingOrder = MoteOrder;
                r.enabled = false;
                next[i].R = r;
            }
            motes = next;
        }

        /// <summary>Puts a mote somewhere on the board, weighted toward the end the field comes
        /// FROM - they are meant to be seen crossing it, not appearing where it ends.</summary>
        private void Respawn(ref Mote mote, Vector2 dir)
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            float span = Mathf.Abs(Vector2.Dot(new Vector2(area.width, area.height),
                new Vector2(Mathf.Abs(dir.x), Mathf.Abs(dir.y))));
            float wide = Mathf.Abs(Vector2.Dot(new Vector2(area.width, area.height),
                new Vector2(Mathf.Abs(side.x), Mathf.Abs(side.y))));
            // Born ANYWHERE in the near two thirds, not just at the very edge: a mote has to be
            // seen crossing the board, and one that always appears at the same line reads as a
            // spawner rather than as a field.
            float along = Random.Range(-0.55f, 0.20f) * span;
            float across = Random.Range(-0.48f, 0.48f) * wide;
            mote.Pos = area.center + dir * along + side * across;
            mote.Speed = Random.Range(Style.MoteSpeedMin, Style.MoteSpeedMax) * cellSize;
            // Long enough to cover one to four cells before it is spent - the travel IS the
            // direction, and a mote that blinks out where it appeared says nothing.
            mote.Life = Random.Range(2.4f, 4.6f);
            mote.Age = 0f;
            mote.Size = Style.MoteSize * cellSize * Random.Range(0.7f, 1.35f);
            mote.Drift = Random.Range(-1f, 1f) * Style.MoteCrossDrift;
            mote.Live = true;
        }

        private void StepMotes(float dt, Vector2 dir, float show)
        {
            if (show <= 0.01f || dir.sqrMagnitude < 0.001f)
            {
                for (int i = 0; i < motes.Length; i++)
                {
                    if (motes[i].R != null)
                    {
                        motes[i].R.enabled = false;
                    }
                    motes[i].Live = false;
                }
                return;
            }
            EnsureMotes();
            Vector2 side = new Vector2(-dir.y, dir.x);
            float span = Mathf.Max(0.001f, Mathf.Abs(Vector2.Dot(
                new Vector2(area.width, area.height),
                new Vector2(Mathf.Abs(dir.x), Mathf.Abs(dir.y)))));
            float rotation = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            for (int i = 0; i < motes.Length; i++)
            {
                if (!motes[i].Live)
                {
                    Respawn(ref motes[i], dir);
                }
                motes[i].Age += dt;

                // How far across the board it has come, 0 at the near end and 1 at the far one.
                float t = Mathf.Clamp01(
                    Vector2.Dot(motes[i].Pos - area.center, dir) / span + 0.5f);
                // FASTER the closer it gets: a pull that strengthens is what separates gravity
                // from a breeze, and it is what makes the far edge read as somewhere things go.
                motes[i].Speed *= 1f + Style.MoteAcceleration * t * dt;
                motes[i].Pos += (dir + side * motes[i].Drift) * (motes[i].Speed * dt);

                bool spent = motes[i].Age >= motes[i].Life || t >= 0.995f;
                if (spent)
                {
                    motes[i].Live = false;
                    motes[i].R.enabled = false;
                    continue;
                }

                // Fades in as it arrives and dissolves into the far edge, so nothing pops.
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(motes[i].Age / 0.4f))
                    * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.18f));
                Transform tr = motes[i].R.transform;
                tr.localPosition = new Vector3(motes[i].Pos.x, motes[i].Pos.y, 0f);
                tr.localRotation = Quaternion.Euler(0f, 0f, rotation);
                // Stretched ALONG the travel, and more so the faster it is going.
                float stretch = Style.MoteStretch * (0.6f + 0.6f * t);
                tr.localScale = new Vector3(motes[i].Size * stretch, motes[i].Size, 1f);
                Color c = Style.MoteColor;
                c.a = fade * Style.MoteOpacity * show;
                motes[i].R.color = c;
                motes[i].R.enabled = true;
            }
        }

        // =================================================================== running

        /// <summary>True while a change is still settling, so a caller can wait it out.</summary>
        public bool Settling
        {
            get { return dip > 0.01f || compression > 0.01f || impulse > 0.01f; }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;

            strength = Mathf.MoveTowards(strength, targetStrength, changeSpeed * dt);
            dip = Mathf.MoveTowards(dip, 0f, dt / Mathf.Max(Style.DirectionChangeSeconds, 0.01f));
            compression = Mathf.MoveTowards(compression, 0f,
                dt / Mathf.Max(Style.ActivationSeconds, 0.01f));
            impulse = Mathf.MoveTowards(impulse, 0f,
                dt / Mathf.Max(Style.ActivationSeconds * 1.6f, 0.01f));

            // The pull SWINGS round, by the short way. Lerping the vector instead would take it
            // through zero on a reversal and the field would collapse and reinflate.
            if (target.sqrMagnitude > 0.001f)
            {
                angle = Mathf.MoveTowardsAngle(angle * Mathf.Rad2Deg,
                    targetAngle * Mathf.Rad2Deg,
                    360f * changeSpeed * dt) * Mathf.Deg2Rad;
            }

            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float show = Mathf.Clamp01(strength * (1f - dip));

            if (field != null)
            {
                bool on = show > 0.002f;
                field.enabled = on && built;
                if (on && material != null)
                {
                    material.SetFloat(TimeId, clock);
                    material.SetVector(DirId, new Vector4(dir.x, dir.y, 0f, 0f));
                    material.SetFloat(StrengthId, show);
                    material.SetFloat(CompressionId, compression);
                    material.SetFloat(ImpulseId, impulse);
                }
            }
            StepMotes(dt, target.sqrMagnitude > 0.001f ? dir : Vector2.zero, show);
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
