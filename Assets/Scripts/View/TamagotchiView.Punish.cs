// PURPOSE: "Tamagotchi" FURIOUS - eating what Core's punish planner chose (PetRampageVisuals): a
// bite of the BOARD, a JOKER or a POWER for good, or a meal off the DRAW or DISCARD pile - and the
// smug moment after each.
//
// CORE HAS ALREADY DONE IT. The cells are dead, the joker is out of the bar, the cards are out of
// the run. So the repaint that follows the turn is met by PreparePunish, on the same frame: a
// ground proxy over every bitten cell (the board looks whole until the bite lands), the eaten joker
// or power standing where its panel was with its SLOT HELD OPEN in the bar (HoldBarSlot - the
// controller remembers every panel's place and index each frame, for exactly this) while the rest
// of the bar steps back a fifth, and the pile drawn as it WAS (CardLayerView.SetPileOverride) so
// the count can fall card by card as they are eaten. The punish then plays from the queue - after
// the fury when there is one - and gives everything back.
//
// THE CORRECTIVE PASS. The first version of all this was too FAST: it frowned, went red, and
// something was gone. Every punish is now read in three stages - TARGET, CAPTURE, EAT - and none of
// it is hurried (a second punish in the same fury runs at ~86%, never instantly):
//
// A JOKER / A POWER (~1.3 s, a valuable one ~1.5): LOCK - its eyes snap to it, the target swells to
// 1.06, a low growl and a dull sting; THREAT - the mouth opens slowly, the card wobbles, a faint
// raspberry shadow gathers behind it; GRAB - a long stretchy paw (a quick tongue for a cheap power)
// REACHES it in 120-180 ms, never an instant snatch, and it gives a little under the contact;
// STRUGGLE - it is pulled and RESISTS: two short tugs, the frame creaking, the pet leaning back
// against it (this is the beat that tells the player "it is taking my joker"); PULL - out of its
// slot along a curve to the mouth; FIRST BITE - a third of it, a deep crunch with a glassy snap in
// it (these are not paper); two strong CHEWS with the cheeks bulging unevenly and the rest still in
// its paws; SECOND BITE - another third (a valuable one gets a third bite); GULP - the last
// recognisable piece goes in, the mouth shuts, the throat and belly move. ONLY THEN does the bar
// close up over the empty slot. It licks its mouth and gives a short, unpleasant, satisfied grunt.
//
// THE BOARD (~1.6 s): TELEGRAPH - it stares; the cells about to go darken at their corners, a soft
// dark-pink bite pressure with tooth marks runs along the region's OUTER edge and the region pulls
// in a pixel; PRE-BITE SUCTION - the region is drawn 1-3 px toward the pet, nearest cells most, and
// small motes of the board's own floor run to its mouth: "it is going to eat THAT"; LUNGE - out of
// its edge over the board, body stretched, mouth 1.65x; BITE - the cells compress under one heavy
// crunch and the arena takes a single 2 px impact; the floor comes away as 2-5 SLABS that are still
// recognisably the cells (rounded, one side fractured - never square confetti) and the hole they
// leave is a raw dark wound; CHUNK PULL - the slabs are drawn to the mouth (1 -> 0.7, turning a
// little) and STAY there; CHEW - two or three heavy bites, a slab gone with each, a cheek full of
// board; GULP - one big one, the belly out; and only THEN does the board settle into the shape Core
// made of it, a dark raspberry-charcoal bite stress standing on the new edge for half a second.
//
// THE PILE: cheap cards come one at a time and each is SEEN - lifted off the stack (100 ms), flown
// to the mouth (130 ms), chomped; the next a tenth of a second behind, the later ones a little
// quicker, the last one a hero bite and a gulp - with the stack thinner after every card. One
// valuable card is a different thing: it lifts slowly, the eyes lock, a paw takes it, two large
// bites, a gulp. Cards travel; nothing teleports and nothing blurs into a stream of five.
// EXTENSION POINT: a new punish kind is a case in PunishRoutine and a routine here; every length
// is in Tuning (Asset*, Board*, Pile*).

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
        private int itemSlot = -1;
        private bool itemIsJoker;
        private bool pileOverridden;
        private bool pileOverrideDraw;
        private readonly List<BlockCard> pileShown = new List<BlockCard>();

        public PetRampageVisuals LastPunish { get; private set; }

        /// <summary>How long the last punish took from its first beat to the end of its gulp, at
        /// 1x (the lab prints it against the brief's targets).</summary>
        public float LastPunishSeconds { get; private set; }

        /// <summary>The path the eaten asset took and the chunks' ways in (debug overlays).</summary>
        public readonly List<Vector3> DebugPullPath = new List<Vector3>();
        public readonly List<Vector3> DebugChunkPaths = new List<Vector3>();

        /// <summary>How much of what it is eating has gone in, 0..1 (debug).</summary>
        public float BiteProgressNow { get; private set; }

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

        /// <summary>This punish's pace: the first of a fury is the full cinematic, the ones after
        /// it ~86% - never instant.</summary>
        private float pace = 1f;

        /// <summary>A length of the corrective choreography at this punish's pace - or, for the
        /// lab's "old" side, the length the first version gave it (zero skips the beat: it did not
        /// exist then).</summary>
        private float D(float seconds, float legacy)
        {
            return LabLegacy ? legacy : seconds * pace;
        }

        private IEnumerator PunishRoutine(PetRampageVisuals r)
        {
            act.Presence = 1f;
            ClearDrag();
            DismissSpeech();
            pace = punishesThisFury == 0 ? 1f : Mathf.Clamp(Tuning.RepeatPunishPace, 0.5f, 1f);
            punishesThisFury++;
            DebugPullPath.Clear();
            DebugChunkPaths.Clear();
            BiteProgressNow = 0f;
            Duck(0.8f, 1.6f);
            // it goes to what it wants - seen to leave, seen to arrive - when another edge is
            // really nearer (the first punish of a fury only: it does not pace about between bites)
            if (!LabLegacy && punishesThisFury == 1)
            {
                Beat("stalk");
                SetState(PetViewState.PunishTelegraph);
                yield return StalkToward(PunishTargetPoint(r.Kind));
                act.Presence = 1f;
            }
            float started = clock;
            switch (r.Kind)
            {
                case PetPunishKind.Board:
                    yield return BoardEat(r, started);
                    break;
                case PetPunishKind.Joker:
                case PetPunishKind.Power:
                    yield return AssetEat(r, started);
                    break;
                default:
                    yield return PileEat(r, started);
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
            ReleaseBarSlot();
            if (rig != null)
            {
                SetPetOrder(PetOrder);
            }
            if (BoardImpulse != null && !boardImpulseMine)
            {
                BoardImpulse(Vector2.zero);
            }
            prepared = null;
        }

        private void ReleaseBarSlot()
        {
            if (itemSlot >= 0 && HoldBarSlot != null)
            {
                HoldBarSlot(itemIsJoker, -1);
            }
            itemSlot = -1;
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
            Vector2 toHome = (Home.Base + HomeUp * (0.5f * S)) - centroid;
            Vector2 n = toHome.sqrMagnitude > 0.0001f ? toHome.normalized : Vector2.right;
            return centroid + n * Anchors.CellSize * 0.35f;
        }

        private IEnumerator BoardEat(PetRampageVisuals r, float started)
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
            Color wound = new Color(0.085f, 0.045f, 0.08f);

            // ---- A: TELEGRAPH - it stares; the corners darken, bite pressure on the OUTER edge
            Beat("telegraph");
            act.LookAt = centroid;
            HeadToward(centroid, 1.2f);
            act.Emotion = PetEmotion.Furious;
            act.Mouth = "smug";
            act.Still = true;
            Sound(PetSound.GrowlIdle);
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
            yield return Tween(D(Tuning.BoardTelegraph, 0.32f), t =>
            {
                float k = Smooth(t);
                foreach (SpriteRenderer c in corners)
                {
                    c.color = new Color(0.03f, 0.02f, 0.05f, 0.6f * k);
                }
                foreach (SpriteRenderer p in pressure)
                {
                    p.color = new Color(0.62f, 0.16f, 0.36f, 0.46f * k + 0.08f * Mathf.Sin(t * 30f));
                }
                for (int i = 0; i < groundProxies.Count; i++)
                {
                    Vector2 home = CellWorld(cells[i]);
                    groundProxies[i].transform.position = Vector2.Lerp(home, centroid, 0.015f * k);
                    float s = Anchors.EmptySlot * (1f - 0.03f * k);
                    groundProxies[i].transform.localScale = new Vector3(s, s, 1f);
                }
                // the lick of something that has decided
                act.Lick = 0.7f * Bell(Mathf.Clamp01(t * 1.4f));
            });
            act.Lick = 0f;
            act.Still = false;
            if (StopAfter("telegraph"))
            {
                yield return LabEnd(null);
                yield break;
            }

            // ---- B: PRE-BITE SUCTION - the region is drawn toward it, motes run to its mouth
            Beat("suction");
            float suction = D(Tuning.BoardPreSuction, 0f);
            if (suction > 0f)
            {
                Sound(PetSound.BoardSuction);
                act.Mouth = "medium";
                float nextMote = 0f;
                int motes = 0;
                yield return Tween(suction, t =>
                {
                    float k = Smooth(t);
                    Vector2 mouth = rig.MouthWorld;
                    for (int i = 0; i < groundProxies.Count; i++)
                    {
                        Vector2 home = CellWorld(cells[i]);
                        Vector2 toMouth = mouth - home;
                        // the nearer the cell, the more it gives: the grid seems to bend
                        float near = Mathf.Clamp01(1f - toMouth.magnitude / (cell * 7f));
                        Vector2 pull = toMouth.normalized * Px(Mathf.Lerp(1f, 3f, near)) * k;
                        groundProxies[i].transform.position = Vector2.Lerp(home, centroid, 0.015f) + pull;
                    }
                    foreach (SpriteRenderer p in pressure)
                    {
                        p.color = new Color(0.62f, 0.16f, 0.36f, 0.5f + 0.1f * Mathf.Sin(t * 40f));
                    }
                    // it draws breath: the body fills, the mouth opens toward it
                    act.Squash = new Vector2(1f - 0.02f * k, 1f + 0.035f * k);
                    act.MouthOpen = Mathf.Lerp(0.7f, 1.1f, k);
                    act.Offset = ((Vector2)centroid - (Vector2)rig.BellyWorld).normalized * Px(3f) * k;
                    if (t >= nextMote && motes < 7)
                    {
                        motes++;
                        nextMote = t + 0.13f;
                        GridPos from = cells[motes % cells.Count];
                        Vector2 at = CellWorld(from) + Random.insideUnitCircle * cell * 0.3f;
                        SpawnPulled("fx_crumb", at, mouth, Mathf.Max(0.12f, suction * (1f - t) + 0.1f), 0.42f * S,
                            Color.Lerp(ground, Color.white, 0.3f));
                    }
                });
                if (StopAfter("suction"))
                {
                    yield return LabEnd(null);
                    yield break;
                }
            }

            // ---- C: THE LUNGE - out of its edge, over the board, body stretched, mouth huge
            Beat("lunge");
            SetState(PetViewState.PunishExecute);
            SetPetOrder(LungeOrder);
            act.Emotion = PetEmotion.Furious;
            act.EmotionWeight = 1f;
            act.Mouth = "furious";
            Vector2 mouthTo = BiteMouthPoint(cells, centroid);
            Vector2 restMouth = rig.MouthWorld - (Vector3)act.Offset;
            // it grows a little as it lunges (act.Scale), which lifts its mouth by the same share
            Vector2 travel = mouthTo - restMouth - HomeUp * (0.06f * S);
            Vector2 startOffset = act.Offset;
            float tilt = Mathf.Clamp(-travel.x * 2f, -10f, 10f);
            float mouthWide = LabLegacy ? 1.45f : 1.65f;
            yield return Tween(D(Tuning.BoardLunge, 0.15f), t =>
            {
                float k = EaseIn(t);
                act.Offset = Vector2.Lerp(startOffset, travel, k);
                act.Scale = 1f + 0.12f * k;
                act.Rot = tilt * k;
                // stretched along the way it goes
                act.Squash = new Vector2(1f - 0.07f * Bell(t), 1f + 0.1f * Bell(t));
                act.MouthScale = Mathf.Lerp(1f, mouthWide, k);
                act.MouthOpen = Mathf.Lerp(1f, 1.2f, k);
                act.LookAt = centroid;
            });
            if (StopAfter("lunge"))
            {
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }

            // ---- D: THE BITE - the cells compress, one heavy crunch, the arena takes 2 px
            Beat("bite");
            Sound(PetSound.BoardBite);
            Haptic(PetHaptic.BoardBitePulse);
            if (!LabLegacy)
            {
                Vector2 into = (centroid - (Vector2)rig.BellyWorld);
                StartCoroutine(ArenaImpulse(into.sqrMagnitude > 0.0001f ? into.normalized : Vector2.down, 2f, 0.18f));
            }
            yield return Tween(D(Tuning.BoardBite, 0.11f), t =>
            {
                float k = Smooth(t);
                for (int i = 0; i < groundProxies.Count; i++)
                {
                    Vector2 home = CellWorld(cells[i]);
                    Vector2 toward = (centroid - home);
                    groundProxies[i].transform.position = home + toward * 0.05f * k
                        + ((Vector2)rig.MouthWorld - home).normalized * Px(3f) * k;
                    float s = Anchors.EmptySlot * Mathf.Lerp(0.97f, 0.93f, k);
                    // the edge folds inward: thinner across the pull
                    groundProxies[i].transform.localScale = new Vector3(s * (1f - 0.06f * k), s, 1f);
                }
                act.Mouth = t < 0.55f ? "furious" : "chew_b";
                act.MouthScale = t < 0.55f ? mouthWide : 1.1f;
                act.Squash = new Vector2(1f + 0.05f * Bell(t), 1f - 0.06f * Bell(t));
            });
            if (StopAfter("bite"))
            {
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }

            // ---- THE TEAR: the floor comes away as 2-5 slabs that are still the cells; what is
            // left is a raw wound until the meal is over
            Beat("chunks");
            List<TamagotchiFoodProxy> chunks = MakeChunks(cells, centroid, ground);
            foreach (SpriteRenderer g in groundProxies)
            {
                if (LabLegacy)
                {
                    g.enabled = false;
                }
                else
                {
                    g.color = wound;
                    g.transform.position = CellWorld(cells[groundProxies.IndexOf(g)]);
                    g.transform.localScale = new Vector3(Anchors.EmptySlot * 1.04f, Anchors.EmptySlot * 1.04f, 1f);
                }
            }
            foreach (SpriteRenderer c in corners)
            {
                c.enabled = false;
            }
            foreach (SpriteRenderer p in pressure)
            {
                p.enabled = false;
            }
            // the fresh edge: torn, dark raspberry over charcoal
            var scar = new List<SpriteRenderer>();
            foreach (KeyValuePair<GridPos, Vector2> e in OuterEdges(cells))
            {
                Vector2 at = CellWorld(e.Key) + e.Value * (cell * 0.5f);
                float rot = Mathf.Atan2(-e.Value.y, -e.Value.x) * Mathf.Rad2Deg - 90f;
                scar.Add(Mark("fx_scallop", at, rot, new Vector2(cell / 0.4f, cell / 0.4f * 0.62f),
                    new Color(0.46f, 0.1f, 0.26f, 0.7f), 3));
            }
            BiteFlecks(centroid, Color.Lerp(ground, Color.white, 0.25f), 4);

            // ---- E: CHUNK PULL - drawn to the mouth, 1 -> 0.7, turning a little; they STAY there
            Vector3[] starts = new Vector3[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                starts[i] = chunks[i].transform.position;
                DebugChunkPaths.Add(starts[i]);
            }
            act.Mouth = "furious";
            act.MouthScale = 1.25f;
            // It draws back a little so the pieces are SEEN coming, and it sorts under them: a
            // chunk that slid behind its cheek would be a chunk that vanished, not one it ate.
            SetPetOrder(FlightOrder - 1);
            Vector2 biteOffset = act.Offset;
            float endScale = LabLegacy ? 0.45f : 0.7f;
            yield return Tween(D(Tuning.BoardChunkPull, 0.18f), t =>
            {
                act.Offset = Vector2.Lerp(biteOffset, biteOffset * 0.8f, Smooth(Mathf.Clamp01(t / 0.45f)));
                Vector3 mouth = rig.MouthWorld;
                for (int i = 0; i < chunks.Count; i++)
                {
                    TamagotchiFoodProxy c = chunks[i];
                    float lag = i * 0.09f;
                    float u = Mathf.Clamp01((t - lag) / (1f - lag));
                    // first drawn a few pixels toward the mouth, then pulled to it
                    Vector3 nudge = starts[i] + (mouth - starts[i]).normalized * Px(6f) * Smooth(Mathf.Clamp01(u / 0.3f));
                    float into = EaseIn(Mathf.Clamp01((u - 0.3f) / 0.7f));
                    // held in front of the mouth, side by side: they have not been eaten yet
                    Vector3 held = mouth + new Vector3((i - (chunks.Count - 1) * 0.5f) * 0.11f * S, -0.02f * S, 0f);
                    c.transform.position = Vector3.Lerp(nudge, LabLegacy ? mouth + new Vector3(0f, 0.03f * S, 0f) : held, into);
                    float s = Mathf.Lerp(1f, endScale, into);
                    c.transform.localScale = new Vector3(s, s, 1f);
                    c.transform.rotation = Quaternion.Euler(0f, 0f, i * 37f + (i % 2 == 0 ? 22f : -26f) * into);
                    if (LabLegacy)
                    {
                        c.SetBite(mouth, BiteAngleFrom(starts[i], mouth), true);
                    }
                }
            });
            foreach (Vector3 s in starts)
            {
                DebugChunkPaths.Add(rig.MouthWorld);
            }
            if (StopAfter("chunks"))
            {
                foreach (TamagotchiFoodProxy c in chunks)
                {
                    TamagotchiFoodProxy.Recycle(c);
                }
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }

            // ---- F: CHEW - heavy bites, a slab gone with each, a cheek full of board
            Beat("chew");
            SetState(PetViewState.PunishRecover);
            Color boardCheeks = Color.Lerp(ground, TamagotchiArt.Plum, 0.35f);
            act.MouthOpen = 1f;
            if (LabLegacy)
            {
                foreach (TamagotchiFoodProxy c in chunks)
                {
                    TamagotchiFoodProxy.Recycle(c);
                }
                chunks.Clear();
            }
            int chews = LabLegacy ? 2 : Mathf.Clamp(chunks.Count, 2, 3);
            for (int n = 0; n < chews; n++)
            {
                bool left = n % 2 == 0;
                // the slabs this chew takes: all that are left on the last one
                int take = n == chews - 1 ? chunks.Count : Mathf.CeilToInt(chunks.Count / (float)(chews - n));
                var eaten = chunks.GetRange(0, Mathf.Min(take, chunks.Count));
                chunks.RemoveRange(0, eaten.Count);
                Vector3[] from = new Vector3[eaten.Count];
                for (int i = 0; i < eaten.Count; i++)
                {
                    from[i] = eaten[i].transform.position;
                }
                bool crunched = false;
                yield return Tween(D(Tuning.BoardChew, 0.17f), t =>
                {
                    Vector3 mouth = rig.MouthWorld;
                    // the jaw opens over the first third, shuts on the slab, works it
                    float open = t < 0.3f ? EaseOut(t / 0.3f) : 1f - EaseIn(Mathf.Clamp01((t - 0.3f) / 0.25f));
                    act.Mouth = t < 0.45f ? "furious" : (t < 0.75f ? "chew_b" : "chew_a");
                    act.MouthScale = Mathf.Lerp(1f, 1.3f, open);
                    act.Head = new Vector2(0f, -0.02f * open);
                    for (int i = 0; i < eaten.Count; i++)
                    {
                        float into = EaseIn(Mathf.Clamp01(t / 0.45f));
                        eaten[i].transform.position = Vector3.Lerp(from[i], mouth + new Vector3(0f, 0.02f * S, 0f), into);
                        float s = Mathf.Lerp(endScale, endScale * 0.55f, into);
                        eaten[i].transform.localScale = new Vector3(s, s, 1f);
                        eaten[i].SetBite(mouth, BiteAngleFrom(from[i] + new Vector3(0f, -0.3f * S, 0f), mouth), t > 0.2f);
                    }
                    if (t >= 0.45f && !crunched)
                    {
                        crunched = true;
                        Sound(LabLegacy ? PetSound.Chew : PetSound.BoardGrind);
                        BiteFlecks(mouth, Color.Lerp(ground, Color.white, 0.25f), 2);
                        foreach (TamagotchiFoodProxy c in eaten)
                        {
                            TamagotchiFoodProxy.Recycle(c);
                        }
                        eaten.Clear();
                    }
                    float bulge = Bell(Mathf.Clamp01((t - 0.4f) / 0.6f));
                    act.PuffL = (left ? 0.95f : 0.35f) * bulge;
                    act.PuffR = (left ? 0.35f : 0.95f) * bulge;
                    act.PuffTint = boardCheeks;
                    act.Squash = new Vector2(1f + 0.035f * bulge, 1f - 0.035f * bulge);
                    BiteProgressNow = (n + t) / chews;
                });
                foreach (TamagotchiFoodProxy c in eaten)
                {
                    TamagotchiFoodProxy.Recycle(c);
                }
            }
            act.MouthScale = 1f;
            act.Head = Vector2.zero;
            if (StopAfter("chew"))
            {
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }

            // ---- G: THE GULP - one big one, still over the board
            Beat("gulp");
            Sound(PetSound.GulpDeep);
            yield return GulpBody(D(Tuning.BoardGulp, 0.14f), true);
            act.PuffL = 0f;
            act.PuffR = 0f;
            LastPunishSeconds = clock - started;
            BiteProgressNow = 1f;
            if (StopAfter("gulp"))
            {
                SetPetOrder(PetOrder);
                yield return LabEnd(null);
                yield break;
            }

            // ---- H: THE NEW BOARD - only now does it settle into the shape Core made of it; the
            // bite stress stands on the fresh edge and fades
            Beat("reveal");
            Say("Tahtandan " + cells.Count + " hücre ısırdı - o satır ve sütunlar artık ölü.",
                "It bit " + cells.Count + " cell(s) out of your board - their rows and columns are dead.");
            StartCoroutine(FadeGround(0.24f));
            StartCoroutine(FadeMarks(scar, LabLegacy ? 0.42f : Tuning.BoardScar, LabLegacy ? 0f : 0.2f));
            Vector2 lungeAt = act.Offset;
            float lungeScale = act.Scale;
            float lungeRot = act.Rot;
            yield return Tween(0.26f, t =>
            {
                float k = Smooth(t);
                act.Offset = Vector2.Lerp(lungeAt, Vector2.zero, k);
                act.Scale = Mathf.Lerp(lungeScale, 1f, k);
                act.Rot = Mathf.Lerp(lungeRot, 0f, k);
                act.Mouth = "chew_a";
            });
            SetPetOrder(PetOrder);
            act.Offset = Vector2.zero;
            act.Scale = 1f;
            act.Rot = 0f;
            if (StopAfter("reveal"))
            {
                yield return LabEnd(null);
                yield break;
            }

            // ---- AFTERMATH: the arena settles a pixel or two; it wipes its mouth, smug
            Beat("aftermath");
            StartCoroutine(BoardSettle(Tuning.BoardAftermathDuration));
            yield return PostAttack(PetPunishKind.Board);
        }

        /// <summary>The wound closes into what Core made of the board: the covers fade out.</summary>
        private IEnumerator FadeGround(float seconds)
        {
            var covers = new List<SpriteRenderer>(groundProxies);
            float t = 0f;
            while (t < seconds)
            {
                t += Dt;
                float a = 1f - Smooth(t / seconds);
                foreach (SpriteRenderer g in covers)
                {
                    if (g != null)
                    {
                        Color c = g.color;
                        c.a = a;
                        g.color = c;
                    }
                }
                yield return null;
            }
            foreach (SpriteRenderer g in covers)
            {
                if (g != null)
                {
                    g.enabled = false;
                }
            }
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

        /// <summary>
        /// The floor, torn out: 2-5 SLABS that still read as the cells they were - a rounded piece
        /// of the board's own ground with one fractured side - each lifted from where its cell
        /// stood. A single cell breaks in two; more than five cells are five bigger pieces.
        /// </summary>
        private List<TamagotchiFoodProxy> MakeChunks(List<GridPos> cells, Vector2 centroid, Color ground)
        {
            var chunks = new List<TamagotchiFoodProxy>();
            bool bites = Lod != PetLod.Low;
            if (LabLegacy)
            {
                // the first version: 2-4 loose lumps
                int was = Mathf.Clamp(cells.Count + (cells.Count >= 3 ? 0 : 1), 2, 4);
                string[] lumps = { "chunk_a", "chunk_b", "chunk_c" };
                for (int i = 0; i < was; i++)
                {
                    GridPos at = cells[(i * cells.Count) / was];
                    Vector2 pos = Vector2.Lerp(CellWorld(at), centroid, cells.Count == 1 ? 0.35f + 0.3f * i : 0.25f);
                    Color lump = Color.Lerp(ground, new Color(0.10f, 0.11f, 0.16f), 0.35f + 0.15f * (i % 2));
                    TamagotchiFoodProxy c = TamagotchiFoodProxy.ForChunk(flight, TamagotchiArt.Get(lumps[i % 3]), lump,
                        Anchors.EmptySlot * (cells.Count == 1 ? 0.62f : 0.95f), 2 + i, bites);
                    c.transform.position = pos;
                    c.transform.rotation = Quaternion.Euler(0f, 0f, i * 37f);
                    chunks.Add(c);
                }
                return chunks;
            }
            string[] art = { "slab_a", "slab_b", "slab_c" };
            int count = cells.Count == 1 ? 2 : Mathf.Min(5, cells.Count);
            // the board's own ground, a little lifted so a slab is seen against the wound it left
            Color tint = Color.Lerp(ground, Color.white, 0.2f);
            for (int i = 0; i < count; i++)
            {
                Vector2 pos;
                float size;
                if (cells.Count == 1)
                {
                    // one cell breaks in two halves
                    pos = CellWorld(cells[0]) + new Vector2((i == 0 ? -1f : 1f) * 0.2f, (i == 0 ? 0.08f : -0.08f)) * Anchors.CellSize;
                    size = Anchors.EmptySlot * 0.66f;
                }
                else if (cells.Count <= 5)
                {
                    pos = CellWorld(cells[i]);
                    size = Anchors.EmptySlot * 0.98f;
                }
                else
                {
                    // five bigger pieces over the region
                    int a = (i * cells.Count) / count;
                    int b = Mathf.Min(cells.Count - 1, ((i + 1) * cells.Count) / count - 1);
                    pos = (CellWorld(cells[a]) + CellWorld(cells[b])) * 0.5f;
                    size = Anchors.EmptySlot * 1.3f;
                }
                TamagotchiFoodProxy c = TamagotchiFoodProxy.ForChunk(flight, TamagotchiArt.Get(art[i % 3]),
                    Color.Lerp(tint, new Color(0.3f, 0.32f, 0.4f), 0.12f * (i % 2)), size, 2 + i, bites);
                c.transform.position = pos;
                c.transform.rotation = Quaternion.Euler(0f, 0f, (i % 4) * 90f);
                chunks.Add(c);
            }
            return chunks;
        }

        private IEnumerator FadeMarks(List<SpriteRenderer> marks, float seconds, float hold = 0f)
        {
            if (hold > 0f)
            {
                yield return Wait(hold);
            }
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

        /// <summary>The gulp while its body is somewhere else (over the board): the same wave
        /// through the face and the belly, without letting go of where it is.</summary>
        private IEnumerator GulpBody(float seconds, bool big)
        {
            act.Mouth = "closed";
            float belly = Tuning.BellyGulpScale * (big ? 1.6f : 1f);
            yield return Tween(seconds, t =>
            {
                act.Head = new Vector2(0f, 0.02f * Bell(Mathf.Clamp01(t * 1.6f)));
                act.HeadSquash = 1f - 0.08f * Bell(Mathf.Clamp01(t * 1.8f));
                act.Belly = belly * Bell(Mathf.Clamp01((t - 0.25f) / 0.75f));
                act.Squash = new Vector2(1f + 0.03f * Bell(Mathf.Clamp01((t - 0.25f) / 0.75f)), 1f);
                act.MouthOpen = 1f;
            });
            act.HeadSquash = 1f;
            act.Head = Vector2.zero;
            act.Belly = 0f;
            act.Squash = Vector2.one;
        }

        // ================================================================== a joker or a power

        private void PrepareItem(PetRampageVisuals r)
        {
            bool joker = r.Kind == PetPunishKind.Joker;
            int key = joker ? r.InstanceId : r.InstanceId + PowerMemoryKey;
            Rect rect;
            if (!Anchors.PanelMemory.TryGetValue(key, out rect))
            {
                Dictionary<int, Rect> panels = joker ? Anchors.JokerPanels : Anchors.PowerPanels;
                if (!panels.TryGetValue(r.InstanceId, out rect))
                {
                    Vector2 c = joker ? Anchors.JokerBar : Anchors.PowerBar;
                    float w = 1.3f;
                    rect = new Rect(c.x - w * 0.5f, c.y - w * 0.7f, w, w * (joker ? 1.4f : 1.1f));
                }
            }
            itemRect = rect;
            itemProxy = TamagotchiFoodProxy.ForItem(flight, joker, r.DefId, rect.size * 0.96f, 4, Lod != PetLod.Low);
            itemProxy.transform.position = rect.center;
            itemProxy.Tier = r.Tier;
            itemProxy.SourceId = r.InstanceId;
            itemProxy.InitialRect = rect;
            // its slot stays OPEN in the bar until the last of it has been swallowed
            int index;
            itemIsJoker = joker;
            itemSlot = Anchors.PanelIndexMemory.TryGetValue(key, out index) ? index : -1;
            if (itemSlot >= 0 && HoldBarSlot != null)
            {
                HoldBarSlot(joker, itemSlot);
            }
            if (DimBar != null)
            {
                DimBar(true, joker);
            }
        }

        private IEnumerator AssetEat(PetRampageVisuals r, float started)
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
            bool snack = !joker && r.Tier == CardValueTier.Low;
            bool paw = joker || high;
            Vector3 slot = itemRect.center;
            float targetScale = LabLegacy ? Tuning.AssetTargetScale : 1.06f;

            // ---- A: LOCK - its eyes snap to it; the target swells; a low growl and a dull sting
            Beat("lock");
            act.LookAt = slot;
            HeadToward(slot, 1.3f);
            act.Emotion = PetEmotion.Furious;
            act.Mouth = "smug";
            act.IrisMul = 0.82f;
            Sound(PetSound.TargetAsset);
            yield return Tween(D(Tuning.AssetLockDuration, 0.3f), t =>
            {
                float s = Mathf.Lerp(1f, targetScale, Back(Mathf.Clamp01(t * 1.3f), 2f));
                item.transform.localScale = new Vector3(s, s, 1f);
                if (LabLegacy)
                {
                    item.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 70f) * 3.2f * (0.4f + 0.6f * t));
                    item.transform.position = slot + new Vector3(Mathf.Sin(t * 53f) * Px(1.2f), 0f, 0f);
                }
            });
            if (StopAfter("lock"))
            {
                yield return LabEnd(null);
                yield break;
            }

            // ---- B: THREAT - the mouth opens slowly; the card wobbles; a faint raspberry shadow
            Beat("threat");
            float threat = D(Tuning.AssetThreatDuration * (snack ? 0.7f : 1f), 0f);
            SpriteRenderer dread = null;
            if (threat > 0f)
            {
                dread = Mark("fx_soft", slot, 0f, Vector2.one * (Mathf.Max(itemRect.width, itemRect.height) * 1.9f / 0.32f),
                    new Color(0.5f, 0.1f, 0.28f, 0f), 5);
                act.Mouth = "furious";
                yield return Tween(threat, t =>
                {
                    float k = Smooth(t);
                    act.MouthScale = Mathf.Lerp(0.55f, 1f, k);
                    act.MouthOpen = Mathf.Lerp(0.6f, 1.12f, k);
                    // a small nervous wobble: it knows
                    item.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 46f) * 3f * (0.3f + 0.7f * t));
                    item.transform.position = slot + new Vector3(Mathf.Sin(t * 37f) * Px(1.2f), 0f, 0f);
                    dread.color = new Color(0.5f, 0.1f, 0.28f, 0.24f * k);
                    act.Offset = ((Vector2)slot - (Vector2)rig.BellyWorld).normalized * Px(3f) * k;
                });
                if (StopAfter("threat"))
                {
                    yield return LabEnd(null);
                    yield break;
                }
            }

            // ---- C: GRAB - the paw (or the tongue) REACHES it; it gives under the contact
            Beat("grab");
            SetState(PetViewState.PunishExecute);
            act.Mouth = paw ? "furious" : "wide";
            act.Reach = true;
            act.ReachTongue = !paw;
            act.ReachRight = slot.x >= rig.MouthWorld.x;
            act.ReachTarget = slot;
            if (LabLegacy)
            {
                Sound(PetSound.AssetSnatch);
            }
            yield return Tween(D(Tuning.AssetGrabDuration, paw ? Tuning.PawSnatchDuration : Tuning.TongueDuration), t =>
            {
                act.Reach01 = EaseOut(t);
                act.ReachTarget = item.transform.position;
                act.Offset = (Vector2)(slot - rig.MouthWorld).normalized * Px(6f) * t;
            });
            // contact
            Sound(PetSound.GrabAsset);
            yield return Tween(0.06f, t =>
            {
                float sq = targetScale - 0.06f * Bell(t);
                item.transform.localScale = new Vector3(sq * (1f + 0.03f * Bell(t)), sq, 1f);
                item.transform.rotation = Quaternion.identity;
                item.transform.position = slot;
            });
            if (StopAfter("grab"))
            {
                act.Reach = false;
                yield return LabEnd(null);
                yield break;
            }

            // ---- D: STRUGGLE - it is pulled and it RESISTS: short tugs, the pet leaning back
            Beat("struggle");
            float struggle = D(Tuning.AssetStruggleDuration * (snack ? 0.55f : 1f), 0f);
            Vector2 toPet = ((Vector2)rig.MouthWorld - (Vector2)slot).normalized;
            if (struggle > 0f)
            {
                int tugs = snack ? 1 : 2;
                Vector2 held = act.Offset;
                for (int n = 0; n < tugs; n++)
                {
                    Sound(PetSound.AssetStrain);
                    float reach = n == 0 ? 7f : 10f;
                    yield return Tween(struggle / tugs, t =>
                    {
                        // out with the pull, snapped most of the way back by the slot
                        float pull = t < 0.55f ? EaseOut(t / 0.55f) : 1f - 0.72f * Smooth((t - 0.55f) / 0.45f);
                        item.transform.position = slot + (Vector3)(toPet * Px(reach) * pull);
                        item.transform.rotation = Quaternion.Euler(0f, 0f, (toPet.x > 0f ? -1f : 1f) * 5f * pull);
                        float stretch = 1f + 0.035f * pull;
                        item.transform.localScale = new Vector3(targetScale * stretch, targetScale * (2f - stretch), 1f);
                        act.ReachTarget = item.transform.position;
                        act.Reach01 = 1f;
                        // it leans back against it
                        act.Offset = held + toPet * Px(6f) * pull;
                        act.Rot = (toPet.x > 0f ? -1f : 1f) * 3.5f * pull;
                        act.Squash = new Vector2(1f + 0.02f * pull, 1f - 0.025f * pull);
                        act.LookAt = item.transform.position;
                    });
                }
                if (StopAfter("struggle"))
                {
                    act.Reach = false;
                    yield return LabEnd(null);
                    yield break;
                }
            }

            // ---- THE PULL - out of its slot, on a curve, to the mouth
            Beat("pull");
            Vector3 from = item.transform.position;
            Vector3 side = Vector3.Cross((rig.MouthWorld - from).normalized, Vector3.forward) * 0.6f;
            float spin = joker ? 18f : -14f;
            float startScale = item.transform.localScale.x;
            Vector2 leanStart = act.Offset;
            float rotStart = act.Rot;
            act.Mouth = "furious";
            act.Squash = Vector2.one;
            if (!LabLegacy)
            {
                Sound(PetSound.AssetPull);
            }
            DebugPullPath.Clear();
            yield return Tween(D(Tuning.AssetPullDuration, Tuning.AssetTravelDuration), t =>
            {
                float k = EaseIn(t) * 0.6f + Smooth(t) * 0.4f;
                Vector3 to = HoldPoint(ItemHoldScale(item));
                Vector3 mid = Vector3.Lerp(from, to, 0.5f) + side * S;
                Vector3 a = Vector3.Lerp(from, mid, k);
                Vector3 b = Vector3.Lerp(mid, to, k);
                item.transform.position = Vector3.Lerp(a, b, k);
                item.transform.rotation = Quaternion.Euler(0f, 0f, spin * Bell(t));
                float s = Mathf.Lerp(startScale, ItemHoldScale(item), Smooth(t));
                item.transform.localScale = new Vector3(s, s, 1f);
                act.ReachTarget = item.transform.position;
                act.Reach01 = 1f;
                act.Offset = Vector2.Lerp(leanStart, Vector2.zero, t);
                act.Rot = Mathf.Lerp(rotStart, 0f, t);
                act.LookAt = item.transform.position;
                if (DebugPullPath.Count < 64)
                {
                    DebugPullPath.Add(item.transform.position);
                }
            });
            act.Reach = false;
            act.Rot = 0f;
            item.transform.SetParent(rig.FoodRoot, true);
            item.SetOrder(TamagotchiRig.FoodOrder);
            Haptic(PetHaptic.JokerLossTap);
            if (dread != null)
            {
                StartCoroutine(FadeMarks(new List<SpriteRenderer> { dread }, 0.2f));
            }
            if (StopAfter("pull"))
            {
                yield return LabEnd(null);
                yield break;
            }

            // ---- THE BITES - it is recognisable until the last one
            // a joker: a third, another third, and the rest goes in with the gulp; a valuable one
            // gets a third bite of its own; a cheap power is a snack - one big bite and the rest
            float first = Mathf.Clamp(Tuning.AssetFirstBite, 0.2f, 0.4f);
            float second = Mathf.Clamp(Tuning.AssetSecondBite, 0.25f, 0.45f);
            float[] steps;
            if (LabLegacy)
            {
                steps = joker ? (high ? new[] { 0.35f, 0.7f, 1f } : new[] { 0.5f, 1f }) : (snack ? new[] { 1f } : new[] { 0.5f, 1f });
            }
            else if (snack)
            {
                steps = new[] { 0.55f };
            }
            else if (high)
            {
                steps = new[] { first, first + second, 0.88f };
            }
            else
            {
                steps = new[] { first, first + second };
            }
            float last = 0f;
            for (int i = 0; i < steps.Length; i++)
            {
                bool final = i == steps.Length - 1;
                string biteBeat = i == 0 ? "first bite" : i == 1 ? "second bite" : "third bite";
                Beat(biteBeat);
                if (final && steps.Length > 1 && LabLegacy)
                {
                    act.LookAt = item.transform.position;
                    act.Mouth = "smug";
                    yield return Wait(0.09f);
                }
                yield return ItemChomp(item, last, steps[i], i == 0);
                last = steps[i];
                BiteProgressNow = last;
                if (StopAfter(biteBeat))
                {
                    yield return LabEnd(null);
                    yield break;
                }
                if (!final || (!LabLegacy && i == 0))
                {
                    // two strong chews after the first bite, one between the later ones
                    Beat("chew");
                    int chews = LabLegacy ? 1 : (i == 0 ? 2 : 1);
                    yield return Chew(item, chews, D(Tuning.AssetChewDuration, Tuning.ChewDuration), null);
                    if (StopAfter("chew"))
                    {
                        yield return LabEnd(null);
                        yield break;
                    }
                }
            }

            // ---- THE GULP - the last recognisable piece goes in, the mouth shuts over it, and
            // the throat and the belly move
            Beat("gulp");
            if (last < 0.999f)
            {
                yield return ItemSwallowLast(item, last);
            }
            TamagotchiFoodProxy.Recycle(item);
            itemProxy = null;
            BiteProgressNow = 1f;
            yield return Gulp(D(Tuning.AssetGulpDuration, Tuning.GulpDuration), true);
            LastPunishSeconds = clock - started;

            // ---- THE LOSS SETTLES - only now does the bar close up over the empty slot
            Beat("residue");
            ReleaseBarSlot();
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
            // ---- a valuable one is savoured before the smug moment
            Beat("reaction");
            if (high)
            {
                act.Emotion = PetEmotion.Smug;
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

        /// <summary>One bite of a joker or power: the jaw opens wide, comes down, and the piece
        /// crosses the bite line. The last one takes what is left IN - the mouth closes over it.</summary>
        private IEnumerator ItemChomp(TamagotchiFoodProxy item, float from, float to, bool hero)
        {
            bool swallowIn = false;
            Sound(hero || !LabLegacy ? PetSound.AssetBite : PetSound.ChompLight);
            bool flecked = false;
            float s = ItemHoldScale(item);
            float h = item.Size.y;
            float seconds = (LabLegacy ? Tuning.BiteDuration : Tuning.BiteDuration * (hero ? 1.1f : 0.95f)) * (LabLegacy ? 1f : pace);
            yield return Tween(seconds, t =>
            {
                float open = t < 0.4f ? EaseOut(t / 0.4f) : 1f - EaseIn((t - 0.4f) / 0.6f);
                act.Mouth = t < 0.55f ? "furious" : "chew_b";
                act.MouthScale = 1f + 0.18f * open;
                act.MouthOpen = 0.85f + 0.4f * open;
                act.Head = new Vector2(0f, -0.024f * open);
                float bite = t < 0.55f ? from : Mathf.Lerp(from, to, EaseOut((t - 0.55f) / 0.25f));
                float shrink = swallowIn ? Mathf.Lerp(1f, 0.7f, Mathf.Clamp01((t - 0.55f) / 0.45f)) : 1f;
                Vector3 c = HoldPoint(s * h / CardVisual.BodyHeight) + (Vector3)(PetUp * (bite * h * s));
                item.transform.position = c;
                item.transform.rotation = Quaternion.identity;
                FoodScale(item, s * shrink, s * shrink);
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
                    BiteFlecks(rig.MouthWorld, item.Colour, hero ? 4 : 2);
                    act.Squash = new Vector2(1.035f, 0.965f);
                }
            });
            act.MouthScale = 1f;
        }

        /// <summary>What is left of it goes in: the piece rides up across the bite line as the
        /// jaw closes over it. No bite of its own - the gulp that follows is its sound.</summary>
        private IEnumerator ItemSwallowLast(TamagotchiFoodProxy item, float from)
        {
            float s = ItemHoldScale(item);
            float h = item.Size.y;
            act.Mouth = "furious";
            yield return Tween(0.07f * pace, t =>
            {
                float bite = Mathf.Lerp(from, 1f, EaseIn(t));
                float shrink = Mathf.Lerp(1f, 0.75f, t);
                Vector3 c = HoldPoint(s * h / CardVisual.BodyHeight) + (Vector3)(PetUp * (bite * h * s));
                item.transform.position = c;
                FoodScale(item, s * shrink, s * shrink);
                if (item.ShaderBites)
                {
                    BiteAtMouth(item);
                }
                act.MouthOpen = Mathf.Lerp(1.2f, 0.7f, t);
                act.Head = new Vector2(0f, 0.01f * t);
                BiteProgressNow = bite;
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

        /// <summary>The stack is one card thinner (the count on it too).</summary>
        private void PileTakeOne(bool draw)
        {
            if (pileOverridden && Cards != null && pileShown.Count > 0)
            {
                pileShown.RemoveAt(pileShown.Count - 1);
                Cards.SetPileOverride(draw, pileShown);
            }
        }

        private IEnumerator PileEat(PetRampageVisuals r, float started)
        {
            SetState(PetViewState.PunishTelegraph);
            SetParticleBudget(12);
            bool draw = r.Kind == PetPunishKind.DrawPile;
            Vector2 pile = PileWorld(draw);
            int n = r.Cards.Count;
            bool single = n == 1 && r.CardTiers.Count > 0 && r.CardTiers[0] == CardValueTier.High;

            // the pile wiggles; the eyes lock; a lick
            Beat("notice");
            if (Cards != null)
            {
                Cards.WigglePile(draw);
            }
            act.LookAt = pile;
            HeadToward(pile, 1.2f);
            act.Emotion = PetEmotion.Furious;
            act.IrisMul = 0.86f;
            act.Mouth = "smug";
            if (!LabLegacy)
            {
                Sound(PetSound.GrowlIdle);
            }
            yield return Tween(D(0.26f, 0.32f), t => act.Lick = Bell(t));
            act.Lick = 0f;
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
            LastPunishSeconds = clock - started;
            BiteProgressNow = 1f;
            Haptic(PetHaptic.PileAggregate);
            Say((draw ? "Çekme destenden " : "Iskartandan ") + n + " kart yedi.",
                "It ate " + n + " card(s) from your " + (draw ? "draw pile." : "discard."));
            yield return PostAttack(r.Kind);
        }

        /// <summary>One valuable card (~1 s): it lifts slowly off the pile, the eyes lock on it, a
        /// paw takes it, two large bites, a gulp - one expensive mouthful.</summary>
        private IEnumerator PileFeast(BlockCard card, bool draw, Vector2 pile)
        {
            TamagotchiFoodProxy food = TamagotchiFoodProxy.ForCard(flight, card, 1, Lod != PetLod.Low);
            food.Tier = CardValueTier.High;
            float pileScale = CardLayerView.CardScale * UiLayout.Active.PileScale;
            food.transform.position = pile;
            FoodScale(food, pileScale, pileScale);
            PileTakeOne(draw);
            Vector3 lift = pile + new Vector2(0f, 0.75f * S);
            act.IrisMul = 0.86f;
            act.Emotion = PetEmotion.Furious;
            act.Mouth = "medium";
            if (!LabLegacy)
            {
                Sound(PetSound.PileHero);
            }
            // lift
            yield return Tween(D(0.2f, 0.4f), t =>
            {
                food.transform.position = Vector3.Lerp(pile, lift, Smooth(t));
                food.transform.rotation = Quaternion.Euler(0f, 0f, 4f * Bell(t));
                act.LookAt = food.transform.position;
            });
            // the eyes lock: it knows what this one is worth
            Spawn("fx_sparkle", (Vector2)lift + new Vector2(0.25f, 0.35f) * S, Vector2.zero, 0.3f, 0.8f * S, 1.2f * S,
                TamagotchiArt.Cream, 0f, 90f, true);
            HeadToward(lift, 1.3f);
            yield return Wait(D(0.12f, 0.18f));
            // the paw takes it
            act.Reach = true;
            act.ReachTongue = false;
            act.ReachRight = (pile.x - rig.MouthWorld.x) >= 0f;
            act.ReachTarget = lift;
            yield return Tween(D(0.14f, Tuning.PawSnatchDuration), t => act.Reach01 = EaseOut(t));
            Sound(PetSound.GrabAsset);
            Vector3 from = food.transform.position;
            yield return Tween(D(0.16f, Tuning.PileCardTravelDuration + 0.06f), t =>
            {
                float k = Smooth(t);
                Vector3 to = HoldPoint(HoldScale);
                food.transform.position = Vector3.Lerp(from, to, k) + new Vector3(0f, 0.2f * S * Bell(t), 0f);
                FoodScale(food, Mathf.Lerp(pileScale, HoldScale, k), Mathf.Lerp(pileScale, HoldScale, k));
                act.ReachTarget = food.transform.position;
            });
            act.Reach = false;
            act.Head = Vector2.zero;
            act.HeadTilt = 0f;
            food.transform.SetParent(rig.FoodRoot, true);
            food.SetOrder(TamagotchiRig.FoodOrder);
            // two large bites
            yield return Chomp(food, 0f, 0.55f, 0.9f, Tuning.BiteDuration * 1.15f, true);
            yield return Chew(food, 1, D(0.085f, Tuning.ChewDuration * 2f), null);
            yield return Chomp(food, 0.55f, 1f, 0.6f, Tuning.BiteDuration, true);
            DropFood(food);
            yield return Gulp(D(0.15f, Tuning.PileFinalGulpDuration), true);
        }

        /// <summary>
        /// Cheap cards, ONE AT A TIME and each of them seen: lifted off the stack, flown to the
        /// mouth, chomped - the next a tenth of a second behind, the later ones a little quicker,
        /// the last a hero bite. Ten or more is three heroes, a short run standing for the middle
        /// (the count falling under it), and two more.
        /// </summary>
        private IEnumerator PileSnack(PetRampageVisuals r, bool draw, Vector2 pile)
        {
            if (LabLegacy)
            {
                yield return PileSnackLegacy(r, draw, pile);
                yield break;
            }
            int n = r.Cards.Count;
            bool stream = n >= 10;
            int heroesFront = stream ? 3 : n;
            int heroesBack = stream ? 2 : 0;
            int middle = n - heroesFront - heroesBack;
            int shownCards = heroesFront + heroesBack;
            float pileScale = CardLayerView.CardScale * UiLayout.Active.PileScale;
            act.Emotion = PetEmotion.Furious;
            int index = 0;
            for (int i = 0; i < shownCards; i++)
            {
                bool lastCard = i == shownCards - 1;
                bool firstCard = i == 0;
                int cardIndex = i < heroesFront ? i : heroesFront + middle + (i - heroesFront);
                // the middle of a long meal: the count runs down while a few cards stand for it
                if (stream && i == heroesFront)
                {
                    float each = 0.5f / Mathf.Max(1, middle);
                    for (int k = 0; k < middle; k++)
                    {
                        PileTakeOne(draw);
                        if (k % 3 == 0)
                        {
                            Sound(PetSound.PileSnackLight);
                            StartCoroutine(SnackSquash());
                        }
                        yield return Wait(each * pace);
                    }
                }
                // a little quicker as it goes
                float quick = Mathf.Lerp(1f, 0.82f, shownCards <= 1 ? 0f : i / (float)(shownCards - 1)) * pace;
                BlockCard card = r.Cards[Mathf.Clamp(cardIndex, 0, n - 1)];
                TamagotchiFoodProxy f = TamagotchiFoodProxy.ForCard(flight, card, 1 + index * 6 % 30, Lod != PetLod.Low);
                index++;
                f.transform.position = pile;
                FoodScale(f, pileScale, pileScale);
                PileTakeOne(draw);
                Vector3 lifted = pile + new Vector2(0f, 0.3f * S);
                float turn = i % 2 == 0 ? 1f : -1f;
                // LIFT: off the stack, plainly
                yield return Tween(Tuning.PileLift * quick, t =>
                {
                    f.transform.position = Vector3.Lerp(pile, lifted, EaseOut(t));
                    f.transform.rotation = Quaternion.Euler(0f, 0f, 6f * turn * t);
                    act.LookAt = f.transform.position;
                    act.Mouth = "furious";
                    act.MouthOpen = Mathf.Lerp(0.8f, 1.15f, t);
                });
                // TRAVEL: an arc to the mouth, still a card all the way
                yield return Tween(Tuning.PileTravel * quick, t =>
                {
                    Vector3 mouth = rig.MouthWorld;
                    Vector3 mid = Vector3.Lerp(lifted, mouth, 0.5f) + new Vector3(0f, 0.55f * S, 0f);
                    float k = Smooth(t);
                    Vector3 a = Vector3.Lerp(lifted, mid, k);
                    Vector3 b = Vector3.Lerp(mid, mouth, k);
                    f.transform.position = Vector3.Lerp(a, b, k);
                    float s = Mathf.Lerp(pileScale, HoldScale * 0.62f, k);
                    FoodScale(f, s, s);
                    f.transform.rotation = Quaternion.Euler(0f, 0f, turn * (6f + 22f * k));
                    f.SetBite(mouth, BiteAngleFrom(f.transform.position, mouth), t > 0.72f);
                    act.LookAt = f.transform.position;
                });
                // CHOMP: the first and the last are full, the ones between lighter
                Sound(firstCard || lastCard ? PetSound.PileSnack : PetSound.PileSnackLight);
                BiteFlecks(rig.MouthWorld, f.Colour, firstCard || lastCard ? 3 : 1);
                TamagotchiFoodProxy.Recycle(f);
                BiteProgressNow = (i + 1f) / shownCards;
                if (lastCard)
                {
                    // the hero bite
                    yield return Tween(0.11f * pace, t =>
                    {
                        float k = Bell(t);
                        act.Mouth = t < 0.45f ? "furious" : "chew_b";
                        act.Squash = new Vector2(1f + 0.05f * k, 1f - 0.055f * k);
                        act.Head = new Vector2(0f, -0.02f * k);
                    });
                }
                else
                {
                    StartCoroutine(SnackSquash());
                    yield return Wait(Mathf.Max(0f, Tuning.PileGap * quick - 0.06f));
                }
            }
            act.Head = Vector2.zero;
            yield return Chew(null, 1, 0.09f * pace, null);
            yield return Gulp(Tuning.PileFinalGulpDuration * pace, true);
        }

        /// <summary>The first version's snack (the lab's comparison): everything in the air at
        /// once, 70-110 ms apart.</summary>
        private IEnumerator PileSnackLegacy(PetRampageVisuals r, bool draw, Vector2 pile)
        {
            int n = r.Cards.Count;
            var flights = new List<KeyValuePair<TamagotchiFoodProxy, float>>();
            float interval = Mathf.Clamp(Tuning.PileCardInterval, 0.07f, 0.11f);
            float travel = Tuning.PileCardTravelDuration;
            float pileScale = CardLayerView.CardScale * UiLayout.Active.PileScale;
            int launched = 0;
            act.Mouth = "wide";
            int eaten = 0;
            int toLaunch = Mathf.Min(n, 6);
            float nextLaunch = 0f;
            float t0 = clock;
            while (eaten < toLaunch)
            {
                float now = clock - t0;
                if (launched < toLaunch && now >= nextLaunch)
                {
                    TamagotchiFoodProxy f = TamagotchiFoodProxy.ForCard(flight, r.Cards[Mathf.Clamp(launched, 0, n - 1)],
                        1 + launched * 6 % 30, Lod != PetLod.Low);
                    f.transform.position = pile;
                    FoodScale(f, pileScale, pileScale);
                    flights.Add(new KeyValuePair<TamagotchiFoodProxy, float>(f, now));
                    PileTakeOne(draw);
                    launched++;
                    nextLaunch = now + interval;
                }
                for (int i = flights.Count - 1; i >= 0; i--)
                {
                    TamagotchiFoodProxy f = flights[i].Key;
                    float u = (now - flights[i].Value) / travel;
                    Vector3 mouth = rig.MouthWorld;
                    if (u >= 1f)
                    {
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
                    f.SetBite(mouth, BiteAngleFrom(f.transform.position, mouth), u > 0.5f);
                }
                act.LookAt = flights.Count > 0 ? flights[0].Key.transform.position : rig.MouthWorld;
                yield return null;
            }
            while (pileShown.Count > 0 && launched < n)
            {
                PileTakeOne(draw);
                launched++;
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
                act.Mouth = k > 0.5f ? "chew_b" : "furious";
                yield return null;
            }
            act.Squash = Vector2.one;
            act.MouthOpen = 1f;
        }

        // ================================================================== after an attack

        /// <summary>
        /// Never straight back to idle: cheeks full after the board, a slow lick after a joker or
        /// power, a belly pat after a pile - then the smug look (a crooked grin, half-shut eyes) and
        /// a short, low, satisfied grunt, and only then the furious idle.
        /// </summary>
        private IEnumerator PostAttack(PetPunishKind kind)
        {
            SetState(PetViewState.PunishRecover);
            Beat("smug");
            act.LookAt = Anchors.HandCentre;
            act.IrisMul = 1f;
            act.Still = false;
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
                    act.Mouth = "smug";
                    yield return Tween(0.42f, t => act.Lick = Bell(t));
                    break;
                default:
                    yield return IdleBellyPat(2, false);
                    break;
            }
            act.Mouth = null;
            act.Lick = 0f;
            act.PuffL = 0f;
            act.PuffR = 0f;
            act.Emotion = PetEmotion.Smug;
            act.EmotionWeight = 1f;
            Sound(PetSound.Smug);
            yield return Wait(0.36f);
        }
    }
}
