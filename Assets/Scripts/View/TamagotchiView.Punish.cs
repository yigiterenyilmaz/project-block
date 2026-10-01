// PURPOSE: "Tamagotchi" FURIOUS - eating what Core's punish planner chose (PetRampageVisuals): a
// bite of the BOARD, a JOKER or a POWER for good, or a meal off the DRAW or DISCARD pile - and the
// smug moment after each.
//
// CORE HAS ALREADY DONE IT. The cells are dead, the joker is out of the bar, the cards are out of
// the run. So the repaint that follows the turn is met by PreparePunish, on the same frame: a
// ground proxy over every bitten cell (the board looks whole until the bite lands), the eaten joker
// or power standing where its panel was (the controller remembers every panel's place each frame,
// for exactly this) while the rest of its bar steps back, and the pile drawn as it WAS
// (CardLayerView.SetPileOverride) so the count can fall card by card as they are eaten. The punish
// itself then plays from the queue - after the fury when there is one - and gives everything back.
//
// THE BOARD BITE (the hero punish, start values .24/.15/.11/.18/.22/.35): it locks onto the region
// with wicked excitement; the cells about to go darken at their corners, a soft dark-pink bite
// pressure runs along the region's OUTER edge (tooth marks pointing in) and the region pulls in a
// pixel - no red square anywhere; it lunges out of its corner over the board (a body proxy above
// everything, mouth 1.45x its normal widest, still a cute monster); the cells give (1 -> 0.96) and
// fold in under a short crunch; the ground comes away as 2-4 big charcoal chunks - the board's own
// floor, never fifteen little squares - that are drawn a few pixels toward the mouth and then into
// it; the jaw shuts; the board shows what Core made of it; a soft scalloped pink-dark residue sits
// on the new edge for ~0.4 s and fades; it chews twice going home with its cheeks full of board,
// gulps, wipes its mouth, and the arena settles a pixel or two. No camera.
//
// THE JOKER / POWER: the others dim; the target swells to 1.05 and wobbles nervously; a valuable
// one is taken by a long stretchy PAW, a cheaper one by a quick sticky TONGUE tap (never wrapped,
// never gross); it leaves its slot fast on a curve, turning a little, and is eaten in bites (35%,
// 35%, the last strip, with a one-frame look at that last strip) so it stays recognisable to the
// end; a berry bite-shadow marks the empty slot for ~0.3 s.
//
// THE PILE ("snack attack"): the pile wiggles, the eyes go wide, a tiny lick. Cheap cards come
// one by one on quick arcs, 70-110 ms apart, nom-nom-nom with a squash per bite and the stack
// visibly thinning; ten or more is three heroes, a quick stream and three more. One valuable card
// is a different thing: it lifts slowly off the pile with a little shine, the eyes grow, a paw grabs
// it, two bites, a big gulp. Cards travel; nothing teleports.
// EXTENSION POINT: a new punish kind is a case in PlayPunish and a routine here.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        /// <summary>The hand and piles, for the pile meal (the pile's count and its wiggle).</summary>
        public CardLayerView Cards;

        private readonly List<SpriteRenderer> groundProxies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> punishMarks = new List<SpriteRenderer>();
        private TamagotchiFoodProxy itemProxy;
        private PetRampageVisuals prepared;
        private Rect itemRect;
        private bool pileOverridden;
        private bool pileOverrideDraw;
        private readonly List<BlockCard> pileShown = new List<BlockCard>();

        public PetRampageVisuals LastPunish { get; private set; }

        /// <summary>
        /// Called on the repaint that follows the punish, before anything is drawn wrong: covers
        /// what Core has already taken so the screen shows it until the pet takes it. The real
        /// panel rects and pile contents are the controller's (Anchors, Cards).
        /// </summary>
        public void PreparePunish(PetRampageVisuals r, IReadOnlyList<BlockCard> pileNow)
        {
            if (r == null || !shown)
            {
                return;
            }
            ClearPunishLeftovers();
            prepared = r;
            LastPunish = r;
            switch (r.Kind)
            {
                case PetPunishKind.Board:
                    foreach (GridPos cell in r.Cells)
                    {
                        groundProxies.Add(GroundProxy(cell));
                    }
                    break;
                case PetPunishKind.Joker:
                case PetPunishKind.Power:
                    PrepareItem(r);
                    break;
                default:
                    PreparePile(r, pileNow);
                    break;
            }
        }

        /// <summary>Plays a punish (queued behind the fury when the two land together).</summary>
        public void PlayPunish(PetRampageVisuals r)
        {
            if (r == null || !shown)
            {
                return;
            }
            if (!ReferenceEquals(prepared, r))
            {
                PreparePunish(r, null);
            }
            PetRampageVisuals rr = r;
            Enqueue(PetPresentationPriority.Punish, "punish: " + r.Kind, () => PunishRoutine(rr), false, 0.35f,
                ClearPunishLeftovers);
        }

        private IEnumerator PunishRoutine(PetRampageVisuals r)
        {
            act.Presence = 1f;
            ClearDrag();
            switch (r.Kind)
            {
                case PetPunishKind.Board:
                    yield return BoardEat(r);
                    break;
                case PetPunishKind.Joker:
                case PetPunishKind.Power:
                    yield return AssetEat(r);
                    break;
                default:
                    yield return PileEat(r);
                    break;
            }
            ClearPunishLeftovers();
        }

        private void ClearPunishLeftovers()
        {
            foreach (SpriteRenderer g in groundProxies)
            {
                if (g != null)
                {
                    Destroy(g.gameObject);
                }
            }
            groundProxies.Clear();
            foreach (SpriteRenderer m in punishMarks)
            {
                if (m != null)
                {
                    Destroy(m.gameObject);
                }
            }
            punishMarks.Clear();
            TamagotchiFoodProxy.Recycle(itemProxy);
            itemProxy = null;
            if (pileOverridden && Cards != null)
            {
                Cards.SetPileOverride(pileOverrideDraw, null);
            }
            pileOverridden = false;
            if (DimBar != null)
            {
                DimBar(false, true);
                DimBar(false, false);
            }
            if (rig != null)
            {
                SetPetOrder(PetOrder);
            }
            if (BoardImpulse != null)
            {
                BoardImpulse(Vector2.zero);
            }
            prepared = null;
        }

        private void SetPetOrder(int order)
        {
            var group = rig.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group != null)
            {
                group.sortingOrder = order;
            }
        }

        // ================================================================== the board

        private Vector2 CellWorld(GridPos cell)
        {
            return Anchors.CellToWorld != null ? Anchors.CellToWorld(cell) : Vector2.zero;
        }

        private SpriteRenderer GroundProxy(GridPos cell)
        {
            var go = new GameObject("Ground_" + cell.X + "_" + cell.Y);
            go.transform.SetParent(decor, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ViewUtil.WhiteSprite;
            r.color = Anchors.GroundColour;
            r.sortingOrder = 1;
            go.transform.position = CellWorld(cell);
            go.transform.localScale = new Vector3(Anchors.EmptySlot, Anchors.EmptySlot, 1f);
            return r;
        }

        private SpriteRenderer Mark(string sprite, Vector2 at, float rotation, Vector2 scale, Color colour, int order)
        {
            var go = new GameObject("Mark_" + sprite);
            go.transform.SetParent(decor, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = TamagotchiArt.Get(sprite);
            r.color = colour;
            r.sortingOrder = order;
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            punishMarks.Add(r);
            return r;
        }

        /// <summary>Every outer edge of the bitten region: the cell, and the side (as a unit
        /// vector from the cell's middle) that has no bitten neighbour.</summary>
        private static List<KeyValuePair<GridPos, Vector2>> OuterEdges(IReadOnlyList<GridPos> cells)
        {
            var set = new HashSet<GridPos>(cells);
            var edges = new List<KeyValuePair<GridPos, Vector2>>();
            Vector2[] sides = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            foreach (GridPos c in cells)
            {
                foreach (Vector2 s in sides)
                {
                    var n = new GridPos(c.X + (int)s.x, c.Y + (int)s.y);
                    if (!set.Contains(n))
                    {
                        edges.Add(new KeyValuePair<GridPos, Vector2>(c, s));
                    }
                }
            }
            return edges;
        }

        private Vector2 Centroid(IReadOnlyList<GridPos> cells)
        {
            Vector2 sum = Vector2.zero;
            foreach (GridPos c in cells)
            {
                sum += CellWorld(c);
            }
            return cells.Count > 0 ? sum / cells.Count : Anchors.BoardCentre;
        }

        /// <summary>Where the pet's mouth goes to bite: just off the region, on the side facing
        /// its home, so its head hangs over the bite rather than covering it.</summary>
        private Vector2 BiteMouthPoint(IReadOnlyList<GridPos> cells, Vector2 centroid)
        {
            Vector2 toHome = (Home.Base + new Vector2(0f, 0.5f * S)) - centroid;
            Vector2 n = toHome.sqrMagnitude > 0.0001f ? toHome.normalized : Vector2.right;
            return centroid + n * Anchors.CellSize * 0.35f;
        }

        private IEnumerator BoardEat(PetRampageVisuals r)
        {
            SetState(PetViewState.PunishTelegraph);
            SetParticleBudget(20);
            List<GridPos> cells = r.Cells;
            if (cells.Count == 0)
            {
                yield break;
            }
            Vector2 centroid = Centroid(cells);
            float cell = Anchors.CellSize;
            Color ground = Anchors.GroundColour;

            // ---- A: TARGET LOCK - wicked excitement
            Beat("lock");
            act.LookAt = centroid;
            HeadToward(centroid, 1f);
            act.Emotion = PetEmotion.Excited;
            act.EmotionWeight = 0.7f;
            act.Mouth = "smug";
            act.Lick = 0.7f;

            // ---- TELEGRAPH: corners darken, a dark-pink bite pressure on the OUTER edge, the
            // region pulls in a pixel. No red square overlay.
            yield return Wait(0.08f);
            Beat("telegraph");
            var corners = new List<SpriteRenderer>();
            foreach (GridPos c in cells)
            {
                Vector2 at = CellWorld(c);
                float half = Anchors.EmptySlot * 0.5f;
                for (int q = 0; q < 4; q++)
                {
                    float sx = q % 2 == 0 ? -1f : 1f;
                    float sy = q < 2 ? -1f : 1f;
                    float rot = sx < 0 ? (sy < 0 ? 0f : -90f) : (sy < 0 ? 90f : 180f);
                    corners.Add(Mark("corner_shade", at + new Vector2(sx * half, sy * half), rot,
                        Vector2.one * (Anchors.EmptySlot * 1.6f), new Color(0.03f, 0.02f, 0.05f, 0f), 2));
                }
            }
            var pressure = new List<SpriteRenderer>();
            foreach (KeyValuePair<GridPos, Vector2> e in OuterEdges(cells))
            {
                Vector2 at = CellWorld(e.Key) + e.Value * (cell * 0.5f);
                // the strip's teeth point along its +y: turn them in, toward the cell
                float rot = Mathf.Atan2(-e.Value.y, -e.Value.x) * Mathf.Rad2Deg - 90f;
                pressure.Add(Mark("fx_scallop", at, rot, new Vector2(cell / 0.4f, cell / 0.4f * 0.75f),
                    new Color(0.62f, 0.16f, 0.36f, 0f), 3));
            }
            yield return Tween(Tuning.BoardTelegraphDuration, t =>
            {
                float k = Smooth(t);
                foreach (SpriteRenderer c in corners)
                {
                    c.color = new Color(0.03f, 0.02f, 0.05f, 0.55f * k);
                }
                foreach (SpriteRenderer p in pressure)
                {
                    p.color = new Color(0.62f, 0.16f, 0.36f, 0.42f * k + 0.08f * Mathf.Sin(t * 30f));
                }
                for (int i = 0; i < groundProxies.Count; i++)
                {
                    Vector2 home = CellWorld(cells[i]);
                    groundProxies[i].transform.position = Vector2.Lerp(home, centroid, 0.015f * k);
                    float s = Anchors.EmptySlot * (1f - 0.03f * k);
                    groundProxies[i].transform.localScale = new Vector3(s, s, 1f);
                }
            });

            if (StopAfter("telegraph"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- B: THE LUNGE - out of its corner, over the board, mouth wide
            Beat("lunge");
            SetState(PetViewState.PunishExecute);
            SetPetOrder(LungeOrder);
            act.Lick = 0f;
            act.Emotion = PetEmotion.Furious;
            act.EmotionWeight = 1f;
            act.Mouth = "furious";
            Vector2 mouthTo = BiteMouthPoint(cells, centroid);
            Vector2 restMouth = Home.Base + new Vector2(0f, 0.5f * S);
            // it grows a little as it lunges (act.Scale), which lifts its mouth by the same share
            Vector2 travel = mouthTo - restMouth - new Vector2(0f, 0.06f * S);
            float tilt = Mathf.Clamp(-travel.x * 2f, -10f, 10f);
            yield return Tween(Tuning.BoardLungeDuration, t =>
            {
                float k = EaseIn(t);
                act.Offset = travel * k;
                act.Scale = 1f + 0.12f * k;
                act.Rot = tilt * k;
                act.Squash = new Vector2(1f - 0.06f * Bell(t), 1f + 0.08f * Bell(t));
                act.MouthScale = Mathf.Lerp(1f, 1.45f, k);
                act.MouthOpen = Mathf.Lerp(1f, 1.15f, k);
                act.LookAt = centroid;
            });

            if (StopAfter("lunge"))
            {
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }
            // ---- C: THE BITE - the cells give and fold in, a short crunch
            Beat("bite");
            Sound(PetSound.BoardBite);
            Haptic(PetHaptic.BoardBitePulse);
            yield return Tween(Tuning.BoardBiteDuration, t =>
            {
                float k = Smooth(t);
                for (int i = 0; i < groundProxies.Count; i++)
                {
                    Vector2 home = CellWorld(cells[i]);
                    Vector2 toward = (centroid - home);
                    groundProxies[i].transform.position = home + toward * 0.04f * k
                        + ((Vector2)rig.MouthWorld - home).normalized * Px(3f) * k;
                    float s = Anchors.EmptySlot * Mathf.Lerp(0.97f, 0.96f, k);
                    // the edge folds inward: thinner across the pull
                    groundProxies[i].transform.localScale = new Vector3(s * (1f - 0.05f * k), s, 1f);
                }
                act.Mouth = t < 0.6f ? "furious" : "chew_b";
                act.Squash = new Vector2(1f + 0.04f * Bell(t), 1f - 0.05f * Bell(t));
            });

            if (StopAfter("bite"))
            {
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }
            // ---- D: THE CHUNKS - the floor comes away in 2-4 pieces, pulled toward the mouth,
            // then into it
            Beat("chunks");
            List<TamagotchiFoodProxy> chunks = MakeChunks(cells, centroid, ground);
            foreach (SpriteRenderer g in groundProxies)
            {
                g.enabled = false;
            }
            foreach (SpriteRenderer c in corners)
            {
                c.enabled = false;
            }
            foreach (SpriteRenderer p in pressure)
            {
                p.enabled = false;
            }
            Vector3[] starts = new Vector3[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                starts[i] = chunks[i].transform.position;
            }
            act.Mouth = "furious";
            // It draws back a little so the pieces are SEEN coming, and it sorts under them: a
            // chunk that slid behind its cheek would be a chunk that vanished, not one it ate.
            SetPetOrder(FlightOrder - 1);
            Vector2 biteOffset = act.Offset;
            yield return Tween(Tuning.BoardChunkPullDuration, t =>
            {
                act.Offset = Vector2.Lerp(biteOffset, biteOffset * 0.76f, Smooth(Mathf.Clamp01(t / 0.45f)));
                Vector3 mouth = rig.MouthWorld;
                for (int i = 0; i < chunks.Count; i++)
                {
                    TamagotchiFoodProxy c = chunks[i];
                    if (c == null)
                    {
                        continue;
                    }
                    float lag = i * 0.12f;
                    float u = Mathf.Clamp01((t - lag) / (1f - lag));
                    // first drawn a few pixels toward the mouth, then compressed into it
                    Vector3 nudge = starts[i] + (mouth - starts[i]).normalized * Px(6f) * Smooth(Mathf.Clamp01(u / 0.3f));
                    float into = EaseIn(Mathf.Clamp01((u - 0.3f) / 0.7f));
                    c.transform.position = Vector3.Lerp(nudge, mouth + new Vector3(0f, 0.03f * S, 0f), into);
                    float s = Mathf.Lerp(1f, 0.45f, into);
                    c.transform.localScale = new Vector3(s, s, 1f);
                    c.transform.rotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 25f : -30f) * into);
                    // the bite line faces the way the piece is coming from: what has passed the
                    // mouth is gone, whichever side it arrived on
                    c.SetBite(mouth, BiteAngleFrom(starts[i], mouth), true);
                }
            });
            foreach (TamagotchiFoodProxy c in chunks)
            {
                TamagotchiFoodProxy.Recycle(c);
            }
            // the jaw shuts; the board shows what Core made of it
            act.Mouth = "chew_a";
            act.Squash = new Vector2(1.04f, 0.96f);
            BiteFlecks(rig.MouthWorld, Color.Lerp(ground, Color.white, 0.25f), 3);
            Say("Tahtandan " + cells.Count + " hücre ısırdı - o satır ve sütunlar artık ölü.",
                "It bit " + cells.Count + " cell(s) out of your board - their rows and columns are dead.");
            // the new edge: a soft scalloped pink-dark residue, gone in ~0.4 s
            var residue = new List<SpriteRenderer>();
            foreach (KeyValuePair<GridPos, Vector2> e in OuterEdges(cells))
            {
                Vector2 at = CellWorld(e.Key) + e.Value * (cell * 0.5f);
                float rot = Mathf.Atan2(-e.Value.y, -e.Value.x) * Mathf.Rad2Deg - 90f;
                residue.Add(Mark("fx_scallop", at, rot, new Vector2(cell / 0.4f, cell / 0.4f * 0.6f),
                    new Color(0.5f, 0.12f, 0.3f, 0.55f), 3));
            }
            StartCoroutine(FadeMarks(residue, 0.42f));

            if (StopAfter("chunks"))
            {
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }
            // ---- E: CHEW going home - twice, heavy, cheeks full of board
            Beat("chew");
            SetState(PetViewState.PunishRecover);
            Color boardCheeks = Color.Lerp(ground, TamagotchiArt.Plum, 0.35f);
            Vector2 lungeAt = act.Offset;
            float lungeScale = act.Scale;
            float lungeRot = act.Rot;
            act.MouthScale = 1f;
            act.MouthOpen = 1f;
            int chew = 0;
            yield return Tween(Tuning.BoardChewDuration + 0.12f, t =>
            {
                float k = Smooth(t);
                act.Offset = Vector2.Lerp(lungeAt, Vector2.zero, k);
                act.Scale = Mathf.Lerp(lungeScale, 1f, k);
                act.Rot = Mathf.Lerp(lungeRot, 0f, k);
                float c = Mathf.Repeat(t * 2f, 1f);
                int which = (int)(t * 2f);
                if (which != chew && which < 2)
                {
                    chew = which;
                    Sound(PetSound.Chew);
                }
                act.Mouth = c < 0.5f ? "chew_a" : "chew_b";
                act.PuffL = (which == 0 ? 0.9f : 0.5f) * Bell(c);
                act.PuffR = (which == 0 ? 0.5f : 0.9f) * Bell(c);
                act.PuffTint = boardCheeks;
                act.Squash = new Vector2(1f + 0.03f * Bell(c), 1f - 0.03f * Bell(c));
            });
            SetPetOrder(PetOrder);
            act.Offset = Vector2.zero;
            act.Scale = 1f;
            act.Rot = 0f;
            yield return Gulp(Tuning.GulpDuration * 1.2f, true);
            act.PuffL = 0f;
            act.PuffR = 0f;

            if (StopAfter("chew"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- AFTERMATH: the arena settles a pixel or two; it wipes its mouth, smug
            Beat("aftermath");
            StartCoroutine(BoardSettle(Tuning.BoardAftermathDuration));
            yield return PostAttack(PetPunishKind.Board);
        }

        /// <summary>The bite line's angle for food arriving at the mouth from
        /// <paramref name="from"/>: the shader takes everything on the line's far side, so the far
        /// side has to be the one BEYOND the mouth along the way the food is travelling.</summary>
        private static float BiteAngleFrom(Vector3 from, Vector3 mouth)
        {
            Vector2 dir = (Vector2)(mouth - from);
            if (dir.sqrMagnitude < 0.000001f)
            {
                return 0f;
            }
            dir.Normalize();
            return Mathf.Atan2(-dir.x, dir.y);
        }

        private List<TamagotchiFoodProxy> MakeChunks(List<GridPos> cells, Vector2 centroid, Color ground)
        {
            var chunks = new List<TamagotchiFoodProxy>();
            int count = Mathf.Clamp(cells.Count + (cells.Count >= 3 ? 0 : 1), 2, 4);
            string[] art = { "chunk_a", "chunk_b", "chunk_c" };
            bool bites = Lod != PetLod.Low;
            for (int i = 0; i < count; i++)
            {
                // spread the chunks over the region's cells
                GridPos at = cells[(i * cells.Count) / count];
                Vector2 pos = Vector2.Lerp(CellWorld(at), centroid, cells.Count == 1 ? 0.35f + 0.3f * i : 0.25f);
                if (cells.Count == 1)
                {
                    pos += new Vector2((i % 2 == 0 ? -1f : 1f) * 0.18f, (i < 2 ? 0.12f : -0.12f)) * Anchors.CellSize;
                }
                float size = Anchors.EmptySlot * (cells.Count == 1 ? 0.62f : 0.95f);
                Color tint = Color.Lerp(ground, new Color(0.10f, 0.11f, 0.16f), 0.35f + 0.15f * (i % 2));
                TamagotchiFoodProxy c = TamagotchiFoodProxy.ForChunk(flight, TamagotchiArt.Get(art[i % 3]), tint,
                    size, 2 + i, bites);
                c.transform.position = pos;
                c.transform.rotation = Quaternion.Euler(0f, 0f, i * 37f);
                chunks.Add(c);
            }
            return chunks;
        }

        private IEnumerator FadeMarks(List<SpriteRenderer> marks, float seconds)
        {
            float t = 0f;
            var start = new List<Color>();
            foreach (SpriteRenderer m in marks)
            {
                start.Add(m != null ? m.color : Color.clear);
            }
            while (t < seconds)
            {
                t += Dt;
                float k = 1f - Smooth(t / seconds);
                for (int i = 0; i < marks.Count; i++)
                {
                    if (marks[i] != null)
                    {
                        Color c = start[i];
                        c.a *= k;
                        marks[i].color = c;
                    }
                }
                yield return null;
            }
            foreach (SpriteRenderer m in marks)
            {
                if (m != null)
                {
                    m.enabled = false;
                }
            }
        }

        /// <summary>The arena's 1-2 px settle after a bite - through the board's own impulse term,
        /// never the camera.</summary>
        private IEnumerator BoardSettle(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Dt;
                float k = Mathf.Clamp01(t / seconds);
                float y = -Px(1.6f) * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.4f);
                if (BoardImpulse != null)
                {
                    BoardImpulse(new Vector2(0f, y));
                }
                yield return null;
            }
            if (BoardImpulse != null)
            {
                BoardImpulse(Vector2.zero);
            }
        }

        // ================================================================== a joker or a power

        private void PrepareItem(PetRampageVisuals r)
        {
            bool joker = r.Kind == PetPunishKind.Joker;
            Rect rect;
            Dictionary<int, Rect> panels = joker ? Anchors.JokerPanels : Anchors.PowerPanels;
            if (!panels.TryGetValue(r.InstanceId, out rect))
            {
                Vector2 c = joker ? Anchors.JokerBar : Anchors.PowerBar;
                float w = 1.3f;
                rect = new Rect(c.x - w * 0.5f, c.y - w * 0.7f, w, w * (joker ? 1.4f : 1.1f));
            }
            itemRect = rect;
            itemProxy = TamagotchiFoodProxy.ForItem(flight, joker, r.DefId, rect.size * 0.96f, 4, Lod != PetLod.Low);
            itemProxy.transform.position = rect.center;
            itemProxy.Tier = r.Tier;
            itemProxy.SourceId = r.InstanceId;
            itemProxy.InitialRect = rect;
            if (DimBar != null)
            {
                DimBar(true, joker);
            }
        }

        private IEnumerator AssetEat(PetRampageVisuals r)
        {
            SetState(PetViewState.PunishTelegraph);
            SetParticleBudget(12);
            bool joker = r.Kind == PetPunishKind.Joker;
            if (itemProxy == null)
            {
                PrepareItem(r);
            }
            TamagotchiFoodProxy item = itemProxy;
            bool high = r.Tier == CardValueTier.High;
            bool paw = high;
            Vector3 slot = itemRect.center;

            // ---- A: LOCK - it looks; the others are dim already; the target swells and wobbles
            Beat("lock");
            act.LookAt = slot;
            HeadToward(slot, 1f);
            act.Emotion = PetEmotion.Furious;
            act.Mouth = "smug";
            yield return Tween(0.3f, t =>
            {
                float s = Mathf.Lerp(1f, Tuning.AssetTargetScale, Smooth(Mathf.Clamp01(t * 2f)));
                item.transform.localScale = new Vector3(s, s, 1f);
                item.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 70f) * 3.2f * (0.4f + 0.6f * t));
                item.transform.position = slot + new Vector3(Mathf.Sin(t * 53f) * Px(1.2f), 0f, 0f);
            });

            if (StopAfter("lock"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- B: THE TONGUE OR THE PAW reaches it
            Beat("reach");
            SetState(PetViewState.PunishExecute);
            act.Mouth = paw ? "furious" : "wide";
            act.Reach = true;
            act.ReachTongue = !paw;
            act.ReachRight = BoardPawIsRight;
            act.ReachTarget = slot;
            Sound(PetSound.AssetSnatch);
            float reachTime = paw ? Tuning.PawSnatchDuration : Tuning.TongueDuration;
            yield return Tween(reachTime, t =>
            {
                act.Reach01 = EaseOut(t);
                act.ReachTarget = item.transform.position;
                act.Offset = (Vector2)(slot - rig.MouthWorld).normalized * Px(6f) * t;
            });
            // the sticky tap / the grab
            yield return Tween(0.06f, t => item.transform.localScale = Vector3.one * (Tuning.AssetTargetScale - 0.05f * Bell(t)));

            if (StopAfter("reach"))
            {
                act.Reach = false;
                yield return LabEnd(null);
                yield break;
            }
            // ---- C: THE SNATCH - out of the slot fast, on a curve, turning a little
            Beat("snatch");
            Vector3 from = item.transform.position;
            Vector3 side = Vector3.Cross((rig.MouthWorld - from).normalized, Vector3.forward) * 0.6f;
            float spin = joker ? 18f : -14f;
            float travel = Tuning.AssetTravelDuration;
            act.Mouth = "furious";
            yield return Tween(travel, t =>
            {
                float k = EaseIn(t) * 0.7f + Smooth(t) * 0.3f;
                Vector3 to = HoldPoint(ItemHoldScale(item));
                Vector3 mid = Vector3.Lerp(from, to, 0.5f) + side * S;
                Vector3 a = Vector3.Lerp(from, mid, k);
                Vector3 b = Vector3.Lerp(mid, to, k);
                item.transform.position = Vector3.Lerp(a, b, k);
                item.transform.rotation = Quaternion.Euler(0f, 0f, spin * Bell(t));
                float s = Mathf.Lerp(Tuning.AssetTargetScale, ItemHoldScale(item), Smooth(t));
                item.transform.localScale = new Vector3(s, s, 1f);
                act.ReachTarget = item.transform.position;
                act.Reach01 = 1f;
                act.Offset = Vector2.Lerp(act.Offset, Vector2.zero, t);
                act.LookAt = item.transform.position;
            });
            act.Reach = false;
            item.transform.SetParent(rig.FoodRoot, true);
            item.SetOrder(TamagotchiRig.FoodOrder);
            Haptic(PetHaptic.JokerLossTap);

            if (StopAfter("snatch"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- D: THE BITES - recognisable until the last one
            // a valuable JOKER is three bites (35%, 35%, the last strip); a valuable POWER two; a
            // cheap power is a quick snack, one mouthful - never a single-frame disappearance
            int bites = joker ? (high ? Mathf.Max(3, Tuning.AssetBiteCount) : 2)
                : (r.Tier == CardValueTier.Low ? 1 : 2);
            float[] steps = bites >= 3 ? new[] { 0.35f, 0.7f, 1f } : bites == 2 ? new[] { 0.5f, 1f } : new[] { 1f };
            float last = 0f;
            for (int i = 0; i < steps.Length; i++)
            {
                if (i == steps.Length - 1 && steps.Length > 1)
                {
                    // one frame of looking at the last little piece
                    act.LookAt = item.transform.position;
                    act.Mouth = "smug";
                    yield return Wait(0.09f);
                }
                string biteBeat = i == 0 ? "first bite" : i == steps.Length - 1 ? "final bite" : "second bite";
                Beat(biteBeat);
                yield return ItemChomp(item, last, steps[i], i == 0);
                last = steps[i];
                if (StopAfter(biteBeat))
                {
                    yield return LabEnd(null);
                    yield break;
                }
                if (i < steps.Length - 1)
                {
                    yield return Chew(item, 1, Tuning.ChewDuration, null);
                }
            }
            TamagotchiFoodProxy.Recycle(item);
            itemProxy = null;
            Beat("gulp");
            yield return Gulp(Tuning.GulpDuration, high);
            Beat("residue");
            // ---- the loss residue: a bite-shaped berry shadow where the slot was
            SpriteRenderer shadow = Mark("bite_mask", slot, 0f, Vector2.one * (itemRect.width / 0.4f) * 0.7f,
                new Color(0.4f, 0.1f, 0.24f, 0.55f), 6);
            StartCoroutine(FadeMarks(new List<SpriteRenderer> { shadow }, Tuning.AssetLossResidueDuration));
            string name = r.EatenName ?? r.DefId;
            Say(joker ? name + " jokerini kalıcı olarak yedi." : name + " gücünü kalıcı olarak yedi.",
                joker ? "It ate your joker " + name + " - for good." : "It ate your power " + name + " - for good.");
            if (DimBar != null)
            {
                DimBar(false, joker);
            }
            if (StopAfter("residue"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- a valuable one is savoured: sparkling eyes, a smug grin, eyes shut, belly rub
            Beat("reaction");
            if (high)
            {
                act.Emotion = PetEmotion.Smug;
                yield return Tween(0.3f, t => { act.SparkleL = Bell(t); act.SparkleR = Bell(t); });
                act.Emotion = PetEmotion.Happy;
                yield return IdleBellyPat(2, true);
            }
            yield return PostAttack(r.Kind);
        }

        private float ItemHoldScale(TamagotchiFoodProxy item)
        {
            // held between the paws and above the floor: no wider than its grip, no taller than
            // the mouth is high
            float w = Mathf.Max(0.001f, item.Size.x);
            float h = Mathf.Max(0.001f, item.Size.y);
            return Mathf.Min(1f, Mathf.Min(0.62f * S / w, HoldHeight / h));
        }

        private IEnumerator ItemChomp(TamagotchiFoodProxy item, float from, float to, bool hero)
        {
            Sound(hero ? PetSound.AssetBite : PetSound.ChompLight);
            bool flecked = false;
            float s = ItemHoldScale(item);
            float h = item.Size.y;
            yield return Tween(Tuning.BiteDuration, t =>
            {
                float open = t < 0.4f ? EaseOut(t / 0.4f) : 1f - EaseIn((t - 0.4f) / 0.6f);
                act.Mouth = t < 0.55f ? "furious" : "chew_b";
                act.MouthOpen = 0.85f + 0.4f * open;
                act.Head = new Vector2(0f, -0.02f * open);
                float bite = t < 0.55f ? from : Mathf.Lerp(from, to, EaseOut((t - 0.55f) / 0.25f));
                Vector3 c = HoldPoint(s * h / CardVisual.BodyHeight) + new Vector3(0f, bite * h * s, 0f);
                item.transform.position = c;
                item.transform.rotation = Quaternion.identity;
                FoodScale(item, s, s);
                if (item.ShaderBites)
                {
                    BiteAtMouth(item);
                }
                else
                {
                    float k = Mathf.Lerp(1f, 0.2f, bite);
                    FoodScale(item, s * k, s * k);
                }
                float half = item.Size.x * s * 0.5f;
                act.PawL = PawToward(false, c + new Vector3(-half, -0.05f * S, 0f), 0.05f);
                act.PawR = PawToward(true, c + new Vector3(half, -0.05f * S, 0f), 0.05f);
                if (!flecked && t >= 0.55f)
                {
                    flecked = true;
                    BiteFlecks(rig.MouthWorld, item.Colour, 3);
                }
            });
        }

        // ================================================================== a pile

        private void PreparePile(PetRampageVisuals r, IReadOnlyList<BlockCard> pileNow)
        {
            if (Cards == null)
            {
                return;
            }
            bool draw = r.Kind == PetPunishKind.DrawPile;
            pileShown.Clear();
            if (pileNow != null)
            {
                pileShown.AddRange(pileNow);
            }
            // the pile as it was: what is left, with what it ate on top
            for (int i = r.Cards.Count - 1; i >= 0; i--)
            {
                pileShown.Add(r.Cards[i]);
            }
            pileOverridden = true;
            pileOverrideDraw = draw;
            Cards.SetPileOverride(draw, pileShown);
        }

        private Vector2 PileWorld(bool draw)
        {
            if (Cards != null)
            {
                return Cards.PileTopWorld(draw);
            }
            return draw ? Anchors.DrawPile : Anchors.DiscardPile;
        }

        private void PileTakeOne(bool draw)
        {
            if (pileOverridden && Cards != null && pileShown.Count > 0)
            {
                pileShown.RemoveAt(pileShown.Count - 1);
                Cards.SetPileOverride(draw, pileShown);
            }
        }

        private IEnumerator PileEat(PetRampageVisuals r)
        {
            SetState(PetViewState.PunishTelegraph);
            SetParticleBudget(12);
            bool draw = r.Kind == PetPunishKind.DrawPile;
            Vector2 pile = PileWorld(draw);
            int n = r.Cards.Count;
            bool single = n == 1 && r.CardTiers.Count > 0 && r.CardTiers[0] == CardValueTier.High;

            // the pile wiggles; the eyes go wide; a tiny lick
            Beat("notice");
            if (Cards != null)
            {
                Cards.WigglePile(draw);
            }
            act.LookAt = pile;
            HeadToward(pile, 1f);
            act.Emotion = PetEmotion.Excited;
            act.IrisMul = 1.12f;
            act.Mouth = "nom";
            yield return Tween(0.32f, t => act.Lick = Bell(t));
            if (StopAfter("notice"))
            {
                yield return LabEnd(null);
                yield break;
            }
            SetState(PetViewState.PunishExecute);
            Beat("eat");

            if (single)
            {
                yield return PileFeast(r.Cards[0], draw, pile);
            }
            else
            {
                yield return PileSnack(r, draw, pile);
            }
            Haptic(PetHaptic.PileAggregate);
            Say((draw ? "Çekme destenden " : "Iskartandan ") + n + " kart yedi.",
                "It ate " + n + " card(s) from your " + (draw ? "draw pile." : "discard."));
            yield return PostAttack(r.Kind);
        }

        /// <summary>One valuable card: it lifts slowly off the pile with a little shine, the eyes
        /// grow, a paw grabs it, two bites, a big gulp - one expensive mouthful.</summary>
        private IEnumerator PileFeast(BlockCard card, bool draw, Vector2 pile)
        {
            TamagotchiFoodProxy food = TamagotchiFoodProxy.ForCard(flight, card, 1, Lod != PetLod.Low);
            food.Tier = CardValueTier.High;
            float pileScale = CardLayerView.CardScale * UiLayout.Active.PileScale;
            food.transform.position = pile;
            FoodScale(food, pileScale, pileScale);
            PileTakeOne(draw);
            Vector3 lift = pile + new Vector2(0f, 0.75f * S);
            act.IrisMul = 1.22f;
            act.Emotion = PetEmotion.Excited;
            act.Mouth = "wide";
            yield return Tween(0.4f, t =>
            {
                food.transform.position = Vector3.Lerp(pile, lift, Smooth(t));
                act.LookAt = food.transform.position;
            });
            // the value: a short shine
            Spawn("fx_sparkle", (Vector2)lift + new Vector2(0.25f, 0.35f) * S, Vector2.zero, 0.4f, 0.9f * S, 1.4f * S,
                TamagotchiArt.Cream, 0f, 90f, true);
            yield return Wait(0.18f);
            // the paw grabs it
            act.Reach = true;
            act.ReachTongue = false;
            act.ReachRight = (pile.x - rig.MouthWorld.x) >= 0f;
            act.ReachTarget = lift;
            yield return Tween(Tuning.PawSnatchDuration, t => act.Reach01 = EaseOut(t));
            Vector3 from = food.transform.position;
            yield return Tween(Tuning.PileCardTravelDuration + 0.06f, t =>
            {
                float k = Smooth(t);
                Vector3 to = HoldPoint(HoldScale);
                food.transform.position = Vector3.Lerp(from, to, k);
                FoodScale(food, Mathf.Lerp(pileScale, HoldScale, k), Mathf.Lerp(pileScale, HoldScale, k));
                act.ReachTarget = food.transform.position;
            });
            act.Reach = false;
            food.transform.SetParent(rig.FoodRoot, true);
            food.SetOrder(TamagotchiRig.FoodOrder);
            // two bites: a big one and the rest
            yield return Chomp(food, 0f, 0.55f, 0.9f, Tuning.BiteDuration * 1.15f, true);
            yield return Chew(food, 2, Tuning.ChewDuration, null);
            yield return Chomp(food, 0.55f, 1f, 0.6f, Tuning.BiteDuration, false);
            DropFood(food);
            yield return Gulp(Tuning.PileFinalGulpDuration, true);
            act.Emotion = PetEmotion.Happy;
            yield return Tween(0.3f, t => { act.SparkleL = Bell(t); act.SparkleR = Bell(t); });
        }

        /// <summary>Cheap cards: one by one on quick arcs, 70-110 ms apart, nom-nom-nom. Ten or more
        /// is three heroes, a quick stream for the middle, and three more.</summary>
        private IEnumerator PileSnack(PetRampageVisuals r, bool draw, Vector2 pile)
        {
            int n = r.Cards.Count;
            var flights = new List<KeyValuePair<TamagotchiFoodProxy, float>>();
            bool stream = n >= 10;
            // ten or more: a handful of hero cards (Tuning.PileHeroCardCount, split front and back)
            // and one quick stream standing for the middle, the count running down under it
            int heroes = Mathf.Clamp(Tuning.PileHeroCardCount, 2, 6);
            int heroesFront = stream ? (heroes + 1) / 2 : n;
            int heroesBack = stream ? heroes / 2 : 0;
            int middle = n - heroesFront - heroesBack;
            float interval = Mathf.Clamp(Tuning.PileCardInterval, 0.07f, 0.11f);
            float travel = Tuning.PileCardTravelDuration;
            float pileScale = CardLayerView.CardScale * UiLayout.Active.PileScale;
            int launched = 0;
            act.Mouth = "wide";
            act.Emotion = PetEmotion.Excited;
            int eaten = 0;
            // launch, and keep every card in the air moving, until all are in
            int toLaunch = heroesFront + (middle > 0 ? 1 : 0) + heroesBack;
            float nextLaunch = 0f;
            float t0 = clock;
            int streamLeft = middle;
            while (eaten < toLaunch)
            {
                float now = clock - t0;
                if (launched < toLaunch && now >= nextLaunch)
                {
                    bool isStream = middle > 0 && launched == heroesFront;
                    int cardIndex = isStream ? heroesFront : launched < heroesFront ? launched
                        : heroesFront + middle + (launched - heroesFront - 1);
                    BlockCard card = r.Cards[Mathf.Clamp(cardIndex, 0, n - 1)];
                    TamagotchiFoodProxy f = TamagotchiFoodProxy.ForCard(flight, card, 1 + launched * 6 % 30, Lod != PetLod.Low);
                    f.transform.position = pile;
                    FoodScale(f, pileScale, pileScale);
                    flights.Add(new KeyValuePair<TamagotchiFoodProxy, float>(f, now));
                    if (isStream)
                    {
                        // the middle of a long meal: one proxy stands for the stream, and the count
                        // runs down under it
                        for (int k = 0; k < middle; k++)
                        {
                            PileTakeOne(draw);
                        }
                        streamLeft = 0;
                    }
                    else
                    {
                        PileTakeOne(draw);
                    }
                    launched++;
                    nextLaunch = now + (isStream ? interval * 2.2f : interval);
                }
                for (int i = flights.Count - 1; i >= 0; i--)
                {
                    TamagotchiFoodProxy f = flights[i].Key;
                    float u = (now - flights[i].Value) / travel;
                    Vector3 mouth = rig.MouthWorld;
                    if (u >= 1f)
                    {
                        // chomp: the first one is the hero, the rest lighter
                        Sound(eaten == 0 ? PetSound.PileSnack : PetSound.PileSnackLight);
                        BiteFlecks(mouth, f.Colour, eaten == 0 ? 3 : 1);
                        TamagotchiFoodProxy.Recycle(f);
                        flights.RemoveAt(i);
                        eaten++;
                        StartCoroutine(SnackSquash());
                        continue;
                    }
                    Vector3 mid = Vector3.Lerp(pile, mouth, 0.5f) + new Vector3(0f, 0.9f * S, 0f);
                    float k = Smooth(u);
                    Vector3 a = Vector3.Lerp(pile, mid, k);
                    Vector3 b = Vector3.Lerp(mid, mouth, k);
                    f.transform.position = Vector3.Lerp(a, b, k);
                    float s = Mathf.Lerp(pileScale, HoldScale * 0.55f, k);
                    FoodScale(f, s, s);
                    f.transform.rotation = Quaternion.Euler(0f, 0f, 30f * k * (i % 2 == 0 ? 1f : -1f));
                    // identifiable only briefly: bitten as it crosses the mouth, from whichever
                    // side its arc brings it in
                    f.SetBite(mouth, BiteAngleFrom(f.transform.position, mouth), u > 0.5f);
                }
                act.LookAt = flights.Count > 0 ? flights[0].Key.transform.position : rig.MouthWorld;
                yield return null;
            }
            yield return Chew(null, 1, Tuning.ChewDuration, null);
            yield return Gulp(Tuning.PileFinalGulpDuration, true);
        }

        private IEnumerator SnackSquash()
        {
            float t = 0f;
            while (t < 0.09f)
            {
                t += Dt;
                float k = Bell(t / 0.09f);
                act.Squash = new Vector2(1f + 0.03f * k, 1f - 0.035f * k);
                act.MouthOpen = 1f - 0.4f * k;
                yield return null;
            }
            act.Squash = Vector2.one;
            act.MouthOpen = 1f;
        }

        // ================================================================== after an attack

        /// <summary>
        /// Never straight back to idle: cheeks full after the board, a tongue wipe after a joker or
        /// power, a belly pat after a pile - then the smug look (half-shut eyes, a tiny grin) for
        /// 250-450 ms, and only then the furious idle.
        /// </summary>
        private IEnumerator PostAttack(PetPunishKind kind)
        {
            SetState(PetViewState.PunishRecover);
            act.LookAt = Anchors.HandCentre;
            act.IrisMul = 1f;
            switch (kind)
            {
                case PetPunishKind.Board:
                    yield return Tween(0.22f, t =>
                    {
                        act.PuffL = 0.55f * (1f - t);
                        act.PuffR = 0.55f * (1f - t);
                        act.PuffTint = TamagotchiArt.Plum;
                    });
                    yield return IdleMouthWipe();
                    break;
                case PetPunishKind.Joker:
                case PetPunishKind.Power:
                    act.Mouth = "small";
                    yield return Tween(0.4f, t => act.Lick = Bell(t));
                    break;
                default:
                    yield return IdleBellyPat(2, false);
                    break;
            }
            act.Mouth = null;
            act.PuffL = 0f;
            act.PuffR = 0f;
            act.Emotion = PetEmotion.Smug;
            act.EmotionWeight = 1f;
            Sound(PetSound.Smug);
            yield return Wait(0.36f);
        }
    }
}
