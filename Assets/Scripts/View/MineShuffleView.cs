// PURPOSE: The shell game on screen ("Mayın eşeği"). It shows you the mine, lays a cover over it,
// dances the covers across the board, and lifts everything again.
//
// IT ANIMATES WHAT THE RULES DID - nothing else. The path comes from the boss
// (MayinEsegiBoss.ShufflePath), which computed it off the round's own rng, so the cover the player
// follows really is where the mine went. A View that made up its own dance, or added a swap because
// one looked better, would be lying to them. Twelve steps in, twelve steps out, in order.
//
// ONCE THE COVER IS DOWN, NO COVER MAY BE TOLD APART FROM ANY OTHER. That is the whole game, and it
// is the thing this file has had wrong twice:
//   - the marker used to be re-sorted ON TOP of the covers, so a player just watched a red square;
//   - and then the mine's cover always arced UP while the one it swapped with arced DOWN, which is
//     a tell a careful player would find inside one round.
// So every visual difference between covers now comes from the STEP or from the DIRECTION OF
// TRAVEL, and never from which cover is carrying anything. See ShuffleStep.
//
// THE CUBES NEVER MOVE. Not one, not for one frame. This view never touches the board's own
// renderers - it lays covers over them and takes them away again, and the arrangement underneath is
// identical before and after. The only thing that changed is where the mine is.
//
// PREMIUM HERE IS SLEIGHT OF HAND, NOT SPECTACLE. No particles, no shake, no flash. What makes it
// physical is weight: covers that lift before they travel, shadows that separate and soften as they
// do, easing that leaves under acceleration and sets down under control, and a beat of silence
// before the dance so the player knows to watch.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The cover-the-board-and-shuffle animation.</summary>
    public sealed class MineShuffleView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ------------------------------------------------------------------ the reveal
            /// <summary>
            /// How far the cover rises off the mine, in cells, and how much of it fades as it
            /// goes. Both, and neither alone: a cover that only faded would not read as being
            /// LIFTED, and one that only rose would have to clear a whole cell and end up looking
            /// like a plate hovering over the board.
            /// </summary>
            public static float RevealLift = 0.30f;

            /// <summary>How far the raised cover fades out. 1 takes it away completely.</summary>
            public static float RevealFade = 1f;

            // ------------------------------------------------------------------ the mine
            /// <summary>How much of the cell the mine takes up. It is an OBJECT SITTING IN the
            /// slot, not the slot itself - the whole point of this pass. Too small and it cannot
            /// be read at gameplay zoom; too big and it is a red cell again.</summary>
            public static float MineSize = 0.46f;

            /// <summary>The warm light the mine throws onto the slot around it, as a multiple of
            /// its own size. It stays well inside the cell: the light comes FROM the mine, and a
            /// glow that reached the cell's edges would be back to colouring the slot.</summary>
            public static float MineGlowSize = 1.5f;

            public static float MineGlowOpacity = 0.42f;

            public static float RevealSeconds = 0.16f;

            /// <summary>How long the mine is left plainly visible. This is the ONE window the
            /// player gets to commit the cell to memory, so it is generous.</summary>
            public static float RevealHold = 0.62f;

            public static float RevealPulse = 0.55f;

            /// <summary>A second look, shorter: by then the player knows the ritual.</summary>
            public static float RerevealHoldScale = 0.75f;

            // ------------------------------------------------------------------ closing
            public static float CloseSeconds = 0.17f;

            /// <summary>How hard a cover settles as it lands - a per cent or two of squash and the
            /// shadow tightening under it. Enough to say "shut", never a bounce.</summary>
            public static float CloseSettle = 0.035f;

            /// <summary>The silence between the cover closing and the first hop. Short, and it is
            /// what turns the dance into something the player was WARNED about.</summary>
            public static float AnticipationHold = 0.14f;

            // ------------------------------------------------------------------ a hop
            /// <summary>How far a cover rises before it travels, in cells. Small - it is sliding
            /// across a table, not flying over it.</summary>
            public static float PreLift = 0.055f;

            public static float PreLiftSeconds = 0.05f;

            /// <summary>How much bigger a lifted cover is drawn. The other half of the lift.</summary>
            public static float LiftScale = 0.045f;

            /// <summary>One hop, before the tempo below.</summary>
            public static float StepSeconds = 0.23f;

            /// <summary>Tempo across the twelve. Not one metronome: the first few are laid out
            /// clearly, the middle picks up, the last few come back down so the final position can
            /// be read. Driven by the STEP INDEX and nothing else.</summary>
            public static float EarlyStepScale = 1.16f;

            public static float MiddleStepScale = 0.84f;

            public static float LateStepScale = 1.10f;

            /// <summary>Per-step jitter, so twelve hops are not twelve identical hops. Seeded from
            /// the step index - never from anything that knows where the mine is.</summary>
            public static float StepJitter = 0.06f;

            /// <summary>How far the shadow drops away from a lifted cover, in cells, and how much
            /// it spreads and thins while it is up. This is most of the weight.</summary>
            public static float ShadowDrop = 0.045f;

            public static float ShadowSpread = 0.16f;

            public static float ShadowOpacity = 0.55f;

            /// <summary>A cover in motion tilts by up to this many degrees. Tiny: the board is a
            /// grid, and a cover that really rotated would look like it had come loose.</summary>
            public static float MoveTilt = 1.1f;

            /// <summary>A short neutral afterimage behind a moving cover. Identical for every
            /// cover - see the header.</summary>
            public static float TrailOpacity = 0.22f;

            public static float TrailSeconds = 0.09f;

            // ------------------------------------------------------------------ the end
            /// <summary>After the last landing, before the covers lift away.</summary>
            public static float FinalHold = 0.22f;

            public static float LiftAwaySeconds = 0.20f;

            // ------------------------------------------------------------------ focus
            /// <summary>How much the edges of the screen are drawn down while this runs. TINY: the
            /// job is to pull the eye to the board, and anything a player would describe as "the
            /// screen went dark" is both wrong and in the way of following a cover.</summary>
            public static float FocusStrength = 0.16f;

            public static float FocusFadeIn = 0.30f;

            public static float FocusFadeOut = 0.32f;

            // ------------------------------------------------------------------ palette
            /// <summary>
            /// Graphite, and clearly LIGHTER than the arena underneath.
            ///
            /// This was 0.105,0.112,0.137 - and the board's own empty cell is 0.112,0.121,0.147.
            /// They were the same colour, so a fully covered board looked like an ordinary empty
            /// one and the whole trick was invisible. A cover has to read as a plate laid ON the
            /// board, which means it cannot be the board's own value.
            /// </summary>
            public static Color CoverColor = new Color(0.205f, 0.220f, 0.265f);

            /// <summary>The lit top edge. Cool and desaturated - this is not chrome, and it is
            /// deliberately weaker than it was: a crisp bright rim on every cover, identical on
            /// all of them, is what turned the board into a grid of UI buttons.</summary>
            public static Color CoverEdge = new Color(0.34f, 0.375f, 0.445f);

            /// <summary>How hard that rim is drawn, and how wide. Lower and wider is more matte -
            /// a thin plate lying on a table rather than something waiting to be clicked.</summary>
            public static float CoverRim = 0.52f;

            public static float CoverRimWidth = 0.075f;

            public static Color CoverBase = new Color(0.098f, 0.105f, 0.130f);

            /// <summary>The shell: charcoal with crimson in it, not red. What makes the core
            /// look hot is having something dark around it.</summary>
            public static Color MineShell = new Color(0.165f, 0.052f, 0.055f);

            public static Color MineShellLit = new Color(0.30f, 0.10f, 0.095f);

            /// <summary>The core, and its peak at the very centre. Deep hot red rather than neon:
            /// this is a dangerous object with heat inside it, not a light.</summary>
            public static Color MineCore = new Color(0.80f, 0.17f, 0.09f);

            public static Color MineHot = new Color(1f, 0.62f, 0.32f);

            /// <summary>What it throws on the slot around it. Muted - a reflection, not a lamp.</summary>
            public static Color MineGlow = new Color(0.45f, 0.10f, 0.07f);
        }

        // =================================================================== orders
        // Everything here sits ABOVE the cubes, because it is covering them.

        private const int ShadowOrder = 26;

        private const int MineOrder = 27;

        private const int CoverOrder = 29;

        private const int TrailOrder = 31;

        /// <summary>A cover in motion draws over the ones that are not. Which of a travelling PAIR
        /// is in front is decided by direction of travel, never by identity.</summary>
        private const int MovingOrder = 32;

        private const int FocusOrder = 40;

        /// <summary>The mine's red. Public because the DETONATION is drawn elsewhere (see
        /// GameUiController.Feedback) and the blast has to be the same red as the mine's own
        /// core, or the thing that went off does not read as the thing you were following.</summary>
        public static Color MineColor
        {
            get { return Style.MineCore; }
        }

        /// <summary>True while the dance is running. The controller blocks play meanwhile - a
        /// player cannot be asked to watch and act at the same time.</summary>
        public bool IsRunning { get; private set; }

        // =================================================================== a cover

        /// <summary>One cover: the plate the player sees and the shadow that gives it weight. They
        /// move together, and every cover on the board is built and moved by the same code.</summary>
        private sealed class Cover
        {
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public Vector2 Rest;
        }

        private readonly Dictionary<GridPos, Cover> covers = new Dictionary<GridPos, Cover>();

        private SpriteRenderer mineMarker;

        private SpriteRenderer mineCore;

        private SpriteRenderer mineGlow;

        private SpriteRenderer mineShadow;

        private SpriteRenderer focus;

        private Coroutine running;

        private float cellSize = 1f;

        // =================================================================== entry points

        /// <summary>Runs the whole reveal-cover-shuffle-lift on the given path.</summary>
        public void Play(BoardView board, GameBoard model, IReadOnlyList<GridPos> path)
        {
            Play(board, model, path, false);
        }

        /// <summary>
        /// The same sequence, with <paramref name="again"/> for the ten-turn re-reveal: the same
        /// visual language, held a little shorter because the player knows the ritual by then.
        ///
        /// <paramref name="delaySeconds"/> holds it back before the first beat, which is what a
        /// DETONATION needs: setting the mine off arms a fresh one at once, so the blast and the
        /// new mine's reveal would otherwise land on the same frame and neither would read.
        /// </summary>
        public void Play(BoardView board, GameBoard model, IReadOnlyList<GridPos> path, bool again,
            float delaySeconds = 0f)
        {
            Stop();
            if (board == null || model == null || path == null || path.Count == 0)
            {
                return;
            }
            running = StartCoroutine(Dance(board, model, new List<GridPos>(path), again,
                delaySeconds));
        }

        /// <summary>
        /// Covers the board, lifts the one cover, and STOPS there with the mine showing.
        ///
        /// For the animation lab only, and it runs the real thing: the same covers, the same
        /// lift, the same mine, the same pulse. It exists because the mine's own look is the part
        /// that most needs staring at, and in the game it is visible for well under a second.
        /// </summary>
        public void PreviewMine(BoardView board, GameBoard model, GridPos at)
        {
            Stop();
            if (board == null || model == null)
            {
                return;
            }
            running = StartCoroutine(HoldOpen(board, model, at));
        }

        private IEnumerator HoldOpen(BoardView board, GameBoard model, GridPos at)
        {
            IsRunning = true;
            cellSize = board.CellWorldSize;
            BuildFocus();
            BuildCovers(board, model);
            BuildMine(board, at);
            yield return Fade(focus, 0f, Style.FocusStrength, Style.FocusFadeIn);
            yield return LiftCover(covers.ContainsKey(at) ? covers[at] : null,
                Style.RevealLift, Style.RevealSeconds);
            // Beats on, forever, until something else is played. Nothing closes.
            while (true)
            {
                yield return PulseMine(Style.RevealHold * 2f);
            }
        }

        public void Stop()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            IsRunning = false;
            covers.Clear();
            mineMarker = null;
            mineCore = null;
            mineGlow = null;
            mineShadow = null;
            focus = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        // =================================================================== the sequence

        private IEnumerator Dance(BoardView board, GameBoard model, List<GridPos> path, bool again,
            float delaySeconds)
        {
            IsRunning = true;
            // Running (so play stays blocked) but showing nothing yet - the blast has the screen.
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }
            cellSize = board.CellWorldSize;
            GridPos at = path[0];

            BuildFocus();
            BuildCovers(board, model);
            Cover mineCover = covers.ContainsKey(at) ? covers[at] : null;

            // ---- 1. THE REVEAL. The cover over the mine is lifted the way a hand lifts a cup: it
            // rises, its shadow drops away and softens under it, and what is beneath is plain.
            BuildMine(board, at);
            yield return Fade(focus, 0f, Style.FocusStrength, Style.FocusFadeIn);
            yield return LiftCover(mineCover, Style.RevealLift, Style.RevealSeconds);

            // ---- 2. LOOK AT IT. Two beats, the first stronger, then still - "remember this". A
            // window rather than a flash: this is the only look the player gets.
            yield return PulseMine(Style.RevealHold * (again ? Style.RerevealHoldScale : 1f));

            // ---- 3. THE COVER COMES DOWN. It falls, meets the surface and settles - it does not
            // fade out. The instant it lands the mine is hidden, and from here nothing on this
            // board says where it is.
            yield return DropCover(mineCover, Style.CloseSeconds);
            HideMine();

            // ---- 4. A BEAT OF NOTHING. The dance does not begin on the frame the cover shut:
            // this pause is what tells the player to watch.
            yield return new WaitForSeconds(Style.AnticipationHold);

            // ---- 5. THE DANCE, exactly as the rules ran it: one hop per step in the boss's own
            // path, in the boss's own order, and nothing in between.
            for (int step = 1; step < path.Count; step++)
            {
                GridPos from = at;
                GridPos to = path[step];
                yield return ShuffleStep(board, from, to, step, path.Count - 1);
                at = to;
            }

            // ---- 6. SETTLE, then take the table away.
            yield return new WaitForSeconds(Style.FinalHold);
            yield return Fade(focus, Style.FocusStrength, 0f, Style.FocusFadeOut);
            yield return LiftAway();
            Stop(); // the covers are gone and the cubes are exactly where they always were
        }

        // =================================================================== one hop

        /// <summary>
        /// ONE STEP OF THE DANCE, and the only place a cover is ever moved.
        ///
        /// Both covers go through the same code with the same numbers. Everything that could tell
        /// them apart is decided by something that cannot know which is which:
        ///   - the DURATION comes from the step index (the tempo across the twelve, plus a jitter
        ///     seeded from that index);
        ///   - which one passes IN FRONT comes from the direction it is travelling.
        /// Neither correlates with where the mine is, and that is the point: a player who watches
        /// closely can follow the cover, and a player who hunts for a tell will not find one,
        /// because there is not one.
        /// </summary>
        private IEnumerator ShuffleStep(BoardView board, GridPos from, GridPos to,
            int step, int total)
        {
            Cover a;
            Cover b;
            covers.TryGetValue(from, out a);
            covers.TryGetValue(to, out b);
            if (a == null || b == null)
            {
                yield break;
            }
            Vector2 pa = board.CellToWorld(from);
            Vector2 pb = board.CellToWorld(to);

            // Tempo: laid out at the start, quicker through the middle, clear again at the end.
            float tempo = step <= 3 ? Style.EarlyStepScale
                : step > total - 3 ? Style.LateStepScale : Style.MiddleStepScale;
            // Deterministic on the STEP, so it is the same jitter every time this step is played
            // and it can never line up with the mine.
            float jitter = 1f + (Frac(step * 0.6180339f) - 0.5f) * 2f * Style.StepJitter;
            float seconds = Mathf.Max(0.05f, Style.StepSeconds * tempo * jitter);

            // Which passes in front: the one travelling right, or on a vertical move the one
            // travelling up. Positional, and which way a step goes is the rules' business.
            bool aInFront = !Mathf.Approximately(pb.x, pa.x) ? pb.x > pa.x : pb.y > pa.y;
            SetOrder(a, aInFront ? MovingOrder + 1 : MovingOrder);
            SetOrder(b, aInFront ? MovingOrder : MovingOrder + 1);

            // ---- the lift, together
            float t = 0f;
            while (t < Style.PreLiftSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Style.PreLiftSeconds);
                Place(a, pa, Style.PreLift * k, k);
                Place(b, pb, Style.PreLift * k, k);
                yield return null;
            }

            // ---- the slide. Out under acceleration, across at pace, down under control.
            float tilt = Style.MoveTilt * (Frac(step * 0.3819660f) - 0.5f) * 2f;
            t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float e = SlideEase(k);
                // Comes back down over the last quarter, so it lands rather than drops.
                float up = Style.PreLift
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (k - 0.72f) / 0.28f)));
                float lifted = up / Mathf.Max(Style.PreLift, 0.0001f);
                Place(a, Vector2.Lerp(pa, pb, e), up, lifted, tilt);
                Place(b, Vector2.Lerp(pb, pa, e), up, lifted, -tilt);
                bool moving = e > 0.05f && e < 0.95f;
                Smear(a, moving);
                Smear(b, moving);
                yield return null;
            }

            // ---- the landing: a per cent of squash, the shadow tightening back underneath.
            SetOrder(a, CoverOrder);
            SetOrder(b, CoverOrder);
            yield return Settle(a, pb, b, pa);

            // The two covers have changed places, so the map has to agree.
            covers[to] = a;
            covers[from] = b;
            a.Rest = pb;
            b.Rest = pa;
        }

        /// <summary>
        /// Out fast, in slow. Accelerating at the start so a cover leaves with intent, and
        /// decelerating into the target so it arrives rather than stops. No overshoot anywhere:
        /// this is a hand on a table, not a spring.
        /// </summary>
        private static float SlideEase(float k)
        {
            float accelerate = k * k;
            float decelerate = 1f - (1f - k) * (1f - k) * (1f - k);
            return Mathf.Lerp(accelerate, decelerate, Mathf.SmoothStep(0f, 1f, k));
        }

        private IEnumerator Settle(Cover a, Vector2 restA, Cover b, Vector2 restB)
        {
            const float seconds = 0.07f;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float squash = Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI)
                    * Style.CloseSettle;
                Place(a, restA, 0f, 0f, 0f, squash);
                Place(b, restB, 0f, 0f, 0f, squash);
                yield return null;
            }
            Place(a, restA, 0f, 0f);
            Place(b, restB, 0f, 0f);
        }

        // =================================================================== the cover itself

        /// <summary>Puts a cover where it belongs. <paramref name="lift"/> is in cells, and it is
        /// what drives the whole illusion: the plate grows a little while its shadow drops away,
        /// spreads and thins under it.</summary>
        private void Place(Cover cover, Vector2 at, float lift, float lifted,
            float tilt = 0f, float squash = 0f)
        {
            if (cover == null)
            {
                return;
            }
            float size = cellSize * 0.94f;
            float grow = 1f + Style.LiftScale * lifted;
            cover.Body.transform.localPosition = new Vector3(at.x, at.y + lift * cellSize, 0f);
            cover.Body.transform.localScale =
                new Vector3(size * grow * (1f + squash), size * grow * (1f - squash), 1f);
            cover.Body.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            if (cover.Shadow == null)
            {
                return;
            }
            float spread = 1f + Style.ShadowSpread * lifted;
            cover.Shadow.transform.localPosition =
                new Vector3(at.x, at.y - Style.ShadowDrop * cellSize * lifted, 0f);
            cover.Shadow.transform.localScale = new Vector3(size * spread, size * spread, 1f);
            var c = Color.black;
            c.a = Style.ShadowOpacity * (1f - 0.45f * lifted);
            cover.Shadow.color = c;
        }

        private void SetOrder(Cover cover, int order)
        {
            if (cover == null)
            {
                return;
            }
            cover.Body.sortingOrder = order;
            if (cover.Shadow != null)
            {
                cover.Shadow.sortingOrder = order - 3;
            }
        }

        /// <summary>A short neutral afterimage behind a moving cover. Every cover gets exactly
        /// this, so it can never say which one is worth watching.</summary>
        private void Smear(Cover cover, bool on)
        {
            if (cover == null || !on || Style.TrailOpacity <= 0.001f)
            {
                return;
            }
            var go = new GameObject("Smear");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = cover.Body.transform.localPosition;
            go.transform.localScale = cover.Body.transform.localScale;
            go.transform.localRotation = cover.Body.transform.localRotation;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = CoverSprite();
            r.sortingOrder = TrailOrder;
            r.color = new Color(1f, 1f, 1f, Style.TrailOpacity);
            StartCoroutine(FadeSmear(r));
        }

        private IEnumerator FadeSmear(SpriteRenderer r)
        {
            float t = 0f;
            while (t < Style.TrailSeconds && r != null)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / Style.TrailSeconds);
                r.color = new Color(1f, 1f, 1f, Style.TrailOpacity * k * k);
                yield return null;
            }
            if (r != null)
            {
                Destroy(r.gameObject);
            }
        }

        // =================================================================== reveal and close

        /// <summary>
        /// Takes the cover off the mine: it RISES and thins away as it goes.
        ///
        /// Both, and neither on its own. A cover that only faded would not read as being lifted -
        /// it would read as being switched off. One that only rose would have to clear an entire
        /// cell to uncover anything, and a plate hanging a full cell above the board looks like
        /// it has come loose rather than like it is being held.
        /// </summary>
        private IEnumerator LiftCover(Cover cover, float lift, float seconds)
        {
            Vector2 rest = cover != null ? cover.Rest : Vector2.zero;
            SetOrder(cover, MovingOrder);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                Place(cover, rest, lift * k, k);
                SetCoverAlpha(cover, 1f - Style.RevealFade * k);
                yield return null;
            }
            Place(cover, rest, lift, 1f);
            SetCoverAlpha(cover, 1f - Style.RevealFade);
        }

        /// <summary>Fades a cover and the shadow that belongs to it together, so a half-raised
        /// cover never leaves its shadow behind on the board.</summary>
        private void SetCoverAlpha(Cover cover, float alpha)
        {
            if (cover == null)
            {
                return;
            }
            if (cover.Body != null)
            {
                cover.Body.color = new Color(1f, 1f, 1f, alpha);
            }
            if (cover.Shadow != null)
            {
                var c = Color.black;
                c.a = Style.ShadowOpacity * alpha;
                cover.Shadow.color = c;
            }
        }

        private IEnumerator DropCover(Cover cover, float seconds)
        {
            Vector2 rest = cover != null ? cover.Rest : Vector2.zero;
            float from = Style.RevealLift;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                // Falls rather than eases: it is being let go of. It comes back in as it comes
                // down, so the mine is covered PROGRESSIVELY - the cover arrives over it.
                Place(cover, rest, from * (1f - k * k), 1f - k);
                SetCoverAlpha(cover, 1f - Style.RevealFade * (1f - k));
                yield return null;
            }
            SetCoverAlpha(cover, 1f);
            SetOrder(cover, CoverOrder);
            const float settle = 0.09f;
            float s = 0f;
            while (s < settle)
            {
                s += Time.deltaTime;
                float k = Mathf.Clamp01(s / settle);
                Place(cover, rest, 0f, 0f, 0f, Mathf.Sin(k * Mathf.PI) * Style.CloseSettle);
                yield return null;
            }
            Place(cover, rest, 0f, 0f);
        }

        /// <summary>Two beats on the mine, the first stronger, then still. An emphasis, not an
        /// alarm - the player is being asked to remember a place, not warned of an impact.</summary>
        private IEnumerator PulseMine(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = t / Mathf.Max(seconds, 0.01f);
                float beat = k < 0.30f ? Mathf.Sin(k / 0.30f * Mathf.PI)
                    : k < 0.60f ? Mathf.Sin((k - 0.30f) / 0.30f * Mathf.PI) * 0.55f : 0f;
                // Only the OBJECT beats - never the cell. The core brightens, the whole mine
                // grows by a few per cent, and the light it throws swells with it.
                float size = cellSize * Style.MineSize;
                float grow = 1f + 0.035f * Style.RevealPulse * beat / 0.55f;
                if (mineCore != null)
                {
                    mineCore.transform.localScale = new Vector3(size * grow, size * grow, 1f);
                    mineCore.color = Color.Lerp(Color.white, Style.MineHot,
                        beat * Style.RevealPulse * 0.55f);
                }
                if (mineMarker != null)
                {
                    mineMarker.transform.localScale = new Vector3(size * grow, size * grow, 1f);
                }
                if (mineGlow != null)
                {
                    float g = size * Style.MineGlowSize * (1f + 0.10f * beat);
                    mineGlow.transform.localScale = new Vector3(g, g, 1f);
                    mineGlow.color = Alpha(Style.MineGlow,
                        Style.MineGlowOpacity * (1f + 0.5f * beat));
                }
                yield return null;
            }
        }

        /// <summary>Every trace of it, in one place. No edge, no glow, no tint left behind: after
        /// this frame the board carries no information about where the mine is.</summary>
        private void HideMine()
        {
            if (mineMarker != null)
            {
                mineMarker.enabled = false;
            }
            if (mineCore != null)
            {
                mineCore.enabled = false;
            }
            if (mineGlow != null)
            {
                mineGlow.enabled = false;
            }
            if (mineShadow != null)
            {
                mineShadow.enabled = false;
            }
        }

        private IEnumerator LiftAway()
        {
            float t = 0f;
            while (t < Style.LiftAwaySeconds)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / Style.LiftAwaySeconds);
                foreach (Cover cover in covers.Values)
                {
                    if (cover.Body != null)
                    {
                        cover.Body.color = new Color(1f, 1f, 1f, k);
                    }
                    if (cover.Shadow != null)
                    {
                        var c = Color.black;
                        c.a = Style.ShadowOpacity * k;
                        cover.Shadow.color = c;
                    }
                }
                yield return null;
            }
        }

        // =================================================================== building

        private void BuildCovers(BoardView board, GameBoard model)
        {
            for (int x = model.MinX; x < model.MinX + model.Width; x++)
            {
                for (int y = model.MinY; y < model.MinY + model.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (!model.IsInside(cell))
                    {
                        continue;
                    }
                    Vector2 at = board.CellToWorld(cell);
                    var cover = new Cover
                    {
                        Shadow = Make("Shadow_" + x + "_" + y, ShadowSprite(), ShadowOrder),
                        Body = Make("Cover_" + x + "_" + y, CoverSprite(), CoverOrder),
                        Rest = at
                    };
                    Place(cover, at, 0f, 0f);
                    covers[cell] = cover;
                }
            }
        }

        /// <summary>
        /// THE MINE, as an object standing in the slot - not as a coloured slot.
        ///
        /// It used to be two red squares filling most of the cell, and that is exactly what it
        /// read as: "this cell is red". A player has to see a small, heavy, dangerous THING that
        /// happens to be sitting under this cover, with the board's own slot plainly visible
        /// around it. So it is three pieces, and the cell underneath is left alone:
        ///   GLOW    the warm light it throws on the slot immediately around it
        ///   SHELL   a dark crimson body with a lit shoulder and a few machined notches
        ///   CORE    the heat inside it, which is the only bright thing here
        /// </summary>
        private void BuildMine(BoardView board, GridPos at)
        {
            Vector2 p = board.CellToWorld(at);
            float size = cellSize * Style.MineSize;

            mineGlow = Make("MineGlow", GlowSprite(), MineOrder - 1);
            mineGlow.transform.localPosition = new Vector3(p.x, p.y, 0f);
            mineGlow.transform.localScale =
                new Vector3(size * Style.MineGlowSize, size * Style.MineGlowSize, 1f);
            mineGlow.color = Alpha(Style.MineGlow, Style.MineGlowOpacity);

            // Its own contact shadow, so it sits IN the slot rather than on top of it.
            mineShadow = Make("MineShadow", ShadowSprite(), MineOrder - 2);
            mineShadow.transform.localPosition =
                new Vector3(p.x, p.y - size * 0.10f, 0f);
            mineShadow.transform.localScale = new Vector3(size * 0.92f, size * 0.92f, 1f);
            mineShadow.color = Alpha(Color.black, 0.55f);

            mineMarker = Make("MineShell", MineShellSprite(), MineOrder);
            mineMarker.transform.localPosition = new Vector3(p.x, p.y, 0f);
            mineMarker.transform.localScale = new Vector3(size, size, 1f);

            mineCore = Make("MineCore", MineCoreSprite(), MineOrder + 1);
            mineCore.transform.localPosition = new Vector3(p.x, p.y, 0f);
            mineCore.transform.localScale = new Vector3(size, size, 1f);
        }

        private static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private void BuildFocus()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic || Style.FocusStrength <= 0.001f)
            {
                return;
            }
            focus = Make("Focus", FocusSprite(), FocusOrder);
            float h = cam.orthographicSize * 2.2f;
            focus.transform.position =
                new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            focus.transform.localScale = new Vector3(h * cam.aspect * 1.1f, h, 1f);
            focus.color = new Color(0f, 0f, 0f, 0f);
        }

        private IEnumerator Fade(SpriteRenderer r, float from, float to, float seconds)
        {
            if (r == null)
            {
                yield break;
            }
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                r.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, k));
                yield return null;
            }
            r.color = new Color(0f, 0f, 0f, to);
        }

        private SpriteRenderer Make(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private static float Frac(float v)
        {
            return v - Mathf.Floor(v);
        }

        // =================================================================== the art

        private static Sprite coverSprite;

        private static Sprite shadowSprite;

        private static Sprite focusSprite;

        private static Sprite mineShellSprite;

        private static Sprite mineCoreSprite;

        private static Sprite glowSprite;

        /// <summary>
        /// The cover: a rounded plate, lit along its top edge and dark at its foot. Generated
        /// rather than imported like everything else under View/ - and drawn from a distance
        /// field, because that is what keeps the edge from crawling when the plate slides by a
        /// fraction of a pixel.
        /// </summary>
        private static Sprite CoverSprite()
        {
            if (coverSprite != null)
            {
                return coverSprite;
            }
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = RoundedBox(u, v, 0.94f, 0.22f);
                    float inside = Mathf.Clamp01(-d / (2.2f / n));
                    // Lit along the top, falling away to a dark foot.
                    float lit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v * 0.5f + 0.5f));
                    Color body = Color.Lerp(Style.CoverBase, Style.CoverColor, lit);
                    // A highlight just inside the top edge and a darker line at the foot. Both
                    // are WIDER and WEAKER than they were: a narrow bright rim, identical on every
                    // cover, is what made a covered board look like a grid of buttons.
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d + 0.055f) / Style.CoverRimWidth);
                    rim = Mathf.SmoothStep(0f, 1f, rim);
                    body = Color.Lerp(body, Style.CoverEdge,
                        rim * Mathf.Clamp01(v) * Style.CoverRim);
                    body = Color.Lerp(body, Style.CoverBase,
                        rim * Mathf.Clamp01(-v) * Style.CoverRim * 0.8f);
                    px[y * n + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(body.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(body.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(body.b) * 255f),
                        (byte)Mathf.RoundToInt(inside * 255f));
                }
            }
            coverSprite = Bake(px, n);
            return coverSprite;
        }

        /// <summary>The contact shadow. Soft-edged and resolution independent, so a shadow that
        /// spreads under a lifted cover never turns into a pixel blob.</summary>
        private static Sprite ShadowSprite()
        {
            if (shadowSprite != null)
            {
                return shadowSprite;
            }
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = RoundedBox(u, v, 0.86f, 0.26f);
                    float a = Mathf.Exp(-Mathf.Max(d, 0f) * 14f)
                        * Mathf.Clamp01(1f - d * 3f);
                    px[y * n + x] = new Color32(0, 0, 0,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            shadowSprite = Bake(px, n);
            return shadowSprite;
        }

        /// <summary>The focus frame: clear in the middle, drawn down at the edges. Very slight -
        /// see Style.FocusStrength for why.</summary>
        private static Sprite FocusSprite()
        {
            if (focusSprite != null)
            {
                return focusSprite;
            }
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.45f) / 0.55f));
                    px[y * n + x] = new Color32(0, 0, 0,
                        (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            focusSprite = Bake(px, n);
            return focusSprite;
        }

        /// <summary>
        /// THE MINE'S SHELL. A compact dark body with a lit shoulder, an armoured inset just
        /// inside its rim, and four small machined notches on the diagonals.
        ///
        /// Deliberately restrained: this is drawn at about forty pixels on a phone, so what has
        /// to survive is the SILHOUETTE and the fact that it is dark. Detail beyond that is
        /// detail nobody sees, and a cartoon bomb or a warning glyph would belong to a different
        /// game entirely.
        /// </summary>
        private static Sprite MineShellSprite()
        {
            if (mineShellSprite != null)
            {
                return mineShellSprite;
            }
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = RoundedBox(u, v, 0.74f, 0.34f);
                    float inside = Mathf.Clamp01(-d / (2.4f / n));

                    // Lit from the upper left, like everything else on this board.
                    float lit = Mathf.Clamp01((u * -0.5f + v * 0.5f) * 0.5f + 0.5f);
                    Color body = Color.Lerp(Style.MineShell, Style.MineShellLit,
                        Mathf.SmoothStep(0f, 1f, lit) * 0.85f);

                    // The armoured inset: a darker groove a little way in from the edge, which is
                    // what makes it read as a shell around something rather than a solid lump.
                    float groove = Mathf.Clamp01(1f - Mathf.Abs(d + 0.16f) / 0.07f);
                    body = Color.Lerp(body, Style.MineShell * 0.55f, groove * 0.8f);

                    // Four notches on the diagonals - the only "mechanical" detail there is room
                    // for, and enough to say the thing was built rather than grown.
                    float ang = Mathf.Atan2(v, u);
                    float notch = Mathf.Cos(ang * 4f);
                    float r = Mathf.Sqrt(u * u + v * v);
                    float cut = Mathf.Clamp01((notch - 0.93f) / 0.07f)
                        * Mathf.Clamp01((r - 0.42f) / 0.18f)
                        * Mathf.Clamp01((0.80f - r) / 0.12f);
                    body = Color.Lerp(body, Style.MineShell * 0.4f, cut);

                    px[y * n + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(body.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(body.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(body.b) * 255f),
                        (byte)Mathf.RoundToInt(inside * 255f));
                }
            }
            mineShellSprite = Bake(px, n);
            return mineShellSprite;
        }

        /// <summary>The heat inside the shell. Small, and the only bright thing in the whole
        /// animation - it is what a player remembers the cell by.</summary>
        private static Sprite MineCoreSprite()
        {
            if (mineCoreSprite != null)
            {
                return mineCoreSprite;
            }
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    // Amber at the very centre, falling to hot red, gone by the shell's groove.
                    float k = Mathf.Clamp01(r / 0.34f);
                    Color c = Color.Lerp(Style.MineHot, Style.MineCore,
                        Mathf.SmoothStep(0f, 1f, k));
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.14f) / 0.22f));
                    px[y * n + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            mineCoreSprite = Bake(px, n);
            return mineCoreSprite;
        }

        /// <summary>The light the mine throws on the slot around it. A soft falloff and nothing
        /// more - the point is that the red has a SOURCE, and the source is the object.</summary>
        private static Sprite GlowSprite()
        {
            if (glowSprite != null)
            {
                return glowSprite;
            }
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Exp(-r * r * 6.5f) * Mathf.Clamp01(1f - r);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            glowSprite = Bake(px, n);
            return glowSprite;
        }

        private static float RoundedBox(float u, float v, float half, float radius)
        {
            float dx = Mathf.Abs(u) - (half - radius);
            float dy = Mathf.Abs(v) - (half - radius);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }

        private static Sprite Bake(Color32[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
