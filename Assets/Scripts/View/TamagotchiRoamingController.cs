// PURPOSE: "Tamagotchi" LIVES ON THE SCREEN, it does not stand in one corner of it. This is the
// part that decides WHEN the pet moves house and WHERE to - and nothing else: it draws nothing,
// reads no rule, and has no effect on the game (the brief: presentation only, no advantage).
//
// WHEN. Every 2-4 TURNS (a number drawn per move, so it is not a metronome), and ONCE if the player
// sits idle for 12-20 seconds. Never while it is eating, being fed, being dragged at, or in its fury
// - the view asks only when it is otherwise idle, and a move simply waits its turn. Every turn would
// be a hyperactive pet and the same place all round a dead one; both are failures of this file.
//
// WHERE. From the places the game's controller says are SAFE right now (it alone can see the board,
// the hand, the piles, the bars and the debt ledger): the two bottom corners (standing, or sitting
// with its paws on its belly), behind either pile with only a head showing, leaning in round the
// left or right edge, and - rarely, and never when furious - hanging head-down over the top edge.
// The last two places it lived are not taken again, and a place of the same FAMILY as the current
// one is less likely, so eight or ten turns show several different edges and poses.
//
// HOW it gets there is chosen from the two places' geometry (StyleFor): a short run along the same
// edge is a SCAMPER or a SNEAK, a drop from a side into the corner under it is a HOP, anything else
// is the long way - it sinks behind its edge and peeks out somewhere else. Furious, it never plays:
// it is gone at once and its eyes come up elsewhere.
// EXTENSION POINT: another kind of place is a TamagotchiHome.Kind the controller offers and a weight here.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public enum PetMoveStyle
    {
        HideAndPeek,   // A: sinks away, only the eyes, gone - and antennae, eyes, head somewhere else
        Scamper,       // B: a few quick little steps along the same edge
        Hop,           // C: one small arc to a corner perch, a squash on landing
        Sneak,         // D: half-hidden, the top of its head sliding along the edge
        FuriousSnap    // furious: gone at once, eyes elsewhere, a head snap, a low growl
    }

    public sealed class TamagotchiRoamingController
    {
        private readonly List<string> history = new List<string>();
        private System.Random rng = new System.Random(11);
        private int turnsSinceMove;
        private int nextMoveInTurns = 3;
        private float lastInputAt;
        private float idleAfter = 15f;
        private bool idleMoveUsed;

        public int TurnsSinceMove
        {
            get { return turnsSinceMove; }
        }

        public int NextMoveInTurns
        {
            get { return nextMoveInTurns; }
        }

        public IReadOnlyList<string> History
        {
            get { return history; }
        }

        /// <summary>A new round: a new sequence (seeded, so a replay roams the same way).</summary>
        public void Reset(uint seed, string startName, float now)
        {
            rng = new System.Random((int)((seed * 40503u + 977u) & 0x7fffffff));
            history.Clear();
            if (!string.IsNullOrEmpty(startName))
            {
                history.Add(startName);
            }
            turnsSinceMove = 0;
            idleMoveUsed = false;
            lastInputAt = now;
            DrawInterval();
        }

        private void DrawInterval()
        {
            int min = Mathf.Max(1, TamagotchiView.Tuning.MoveTurnIntervalMin);
            int max = Mathf.Max(min, TamagotchiView.Tuning.MoveTurnIntervalMax);
            nextMoveInTurns = rng.Next(min, max + 1);
            // 12-20 s about the tuned middle
            idleAfter = TamagotchiView.Tuning.IdleRelocateSeconds * Mathf.Lerp(0.8f, 1.33f, (float)rng.NextDouble());
        }

        /// <summary>A turn was played.</summary>
        public void NoteTurn(float now)
        {
            turnsSinceMove++;
            lastInputAt = now;
            idleMoveUsed = false;
        }

        /// <summary>The player touched something.</summary>
        public void NoteInput(float now)
        {
            lastInputAt = now;
        }

        /// <summary>True when a move is owed: the turns have passed, or the player has sat idle
        /// long enough (that one happens once, until the next turn).</summary>
        public bool Due(float now)
        {
            if (turnsSinceMove >= nextMoveInTurns)
            {
                return true;
            }
            return !idleMoveUsed && now - lastInputAt >= idleAfter;
        }

        /// <summary>
        /// The next place to live, or null when there is nowhere else safe. Never the current one
        /// and never one of the last few (Tuning.PositionCooldown).
        /// </summary>
        public TamagotchiHome Choose(IList<TamagotchiHome> candidates, TamagotchiHome current, bool furious)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }
            var pool = new List<TamagotchiHome>();
            var weights = new List<float>();
            int cooldown = Mathf.Max(0, TamagotchiView.Tuning.PositionCooldown);
            float total = 0f;
            foreach (TamagotchiHome h in candidates)
            {
                if (h == null || (current != null && h.Name == current.Name))
                {
                    continue;
                }
                bool recent = false;
                for (int i = Mathf.Max(0, history.Count - cooldown); i < history.Count; i++)
                {
                    if (history[i] == h.Name)
                    {
                        recent = true;
                    }
                }
                if (recent)
                {
                    continue;
                }
                float w = Weight(h, current, furious);
                if (w <= 0f)
                {
                    continue;
                }
                pool.Add(h);
                weights.Add(w);
                total += w;
            }
            if (pool.Count == 0)
            {
                return null;
            }
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                if (roll < weights[i])
                {
                    return pool[i];
                }
                roll -= weights[i];
            }
            return pool[pool.Count - 1];
        }

        private static float Weight(TamagotchiHome h, TamagotchiHome current, bool furious)
        {
            if (furious && (h.Inverted || h.Rest < 0.95f || h.Sit > 0.01f))
            {
                // nothing playful when it is furious: no hanging upside down, no peeking, no sitting
                return 0f;
            }
            float w;
            switch (h.Kind)
            {
                case "top": w = 0.4f; break;              // rare: one in many
                case "side": w = 2.2f; break;
                case "pile": w = h.Rest < 0.95f ? 1.6f : 1f; break;
                default: w = h.Sit > 0.01f ? 2f : 1.5f; break;
            }
            // the very same spot in another pose is hardly a move at all
            if (current != null && Vector2.Distance(current.Base, h.Base) < 0.3f)
            {
                w *= 0.3f;
            }
            // the same family again is less of a change
            if (current != null && current.Kind == h.Kind)
            {
                w *= 0.55f;
            }
            // the other half of the screen is more of one
            if (current != null && current.Side != 0 && h.Side != 0 && current.Side != h.Side)
            {
                w *= 1.35f;
            }
            return w;
        }

        /// <summary>How it gets from one place to the other.</summary>
        public PetMoveStyle StyleFor(TamagotchiHome from, TamagotchiHome to, bool furious)
        {
            if (furious)
            {
                return PetMoveStyle.FuriousSnap;
            }
            if (from == null || to == null || from.Inverted || to.Inverted)
            {
                return PetMoveStyle.HideAndPeek;
            }
            bool sameSide = from.Side == to.Side && from.Side != 0;
            float distance = Vector2.Distance(from.Base, to.Base);
            // the same spot, another pose (sitting down, ducking to a peek): a little hop on the
            // spot, or it just sinks
            if (distance < 0.3f)
            {
                return to.Rest < 0.95f || from.Rest < 0.95f ? PetMoveStyle.Sneak : PetMoveStyle.Hop;
            }
            // along the same edge, a short way, nothing of the game between them
            if (sameSide && from.Edge == 0 && to.Edge == 0 && distance < 3.2f)
            {
                // a head-only place is reached (or left) with the head only
                if (from.Rest < 0.95f || to.Rest < 0.95f)
                {
                    return PetMoveStyle.Sneak;
                }
                return rng.NextDouble() < 0.6 ? PetMoveStyle.Scamper : PetMoveStyle.Sneak;
            }
            // down from the side into the corner under it (or back up onto it)
            if (sameSide && ((from.Kind == "side" && to.Kind == "corner") || (from.Kind == "corner" && to.Kind == "side"))
                && distance < 4.2f)
            {
                return PetMoveStyle.Hop;
            }
            return PetMoveStyle.HideAndPeek;
        }

        /// <summary>It has moved in.</summary>
        public void Moved(TamagotchiHome to, float now)
        {
            if (to != null)
            {
                history.Add(to.Name);
                if (history.Count > 8)
                {
                    history.RemoveAt(0);
                }
            }
            turnsSinceMove = 0;
            idleMoveUsed = true;
            lastInputAt = now;
            DrawInterval();
        }

        /// <summary>A random gap inside a range (the pause between vanishing and reappearing).</summary>
        public float Between(float a, float b)
        {
            return Mathf.Lerp(a, b, (float)rng.NextDouble());
        }
    }
}
