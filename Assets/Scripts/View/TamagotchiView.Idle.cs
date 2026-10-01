// PURPOSE: "Tamagotchi"'s life when nothing is happening to it - the ENTRANCE (antennae, then eyes,
// a scan of the board, a pop out of the edge, the plates, a paw to each, "nom?"), the IDLE LIBRARY
// (fourteen small behaviours, five for a satisfied pet, four more for an impatient or angry one,
// eight for a furious one), and the EXITS.
//
// THE IDLE IS CHOSEN, NOT LOOPED. Every 4-6.5 s (shorter when impatient, longer when content) a
// behaviour is drawn by weight from the set the MOOD calls for - with the last three excluded, so
// nothing repeats back to back - on a generator seeded from the round, so a replay or a debug run
// sees the same sequence. Two pending requests is a normal hungry pet, one is an eager one (it
// glances at its plate and opens its mouth more), none is a content one. Any gameplay event cancels
// an idle cleanly: idles are the lowest priority in the queue and the only interruptible kind.
//
// A peek is a real hide: the pet sinks behind the edge of its nest (TamagotchiRig's clip) until
// only its eyes are over it, watches the cards, and comes back up.
// EXTENSION POINT: a new idle is a case in IdleKind, a weight in IdleWeights and a routine.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        public enum IdleKind
        {
            Peek, HandWave, Yawn, Think, RequestGlance, Lick, BellyPat, CheekSquish, DoubleBlink,
            LittleHop, CardTrack, SnackDream, EdgeTap, MouthOpenWait,
            SleepySmile, BellyRub, SmallYawn, ContentBlink, NapWobble,
            CheekPuff, ArmsCross, Stomp, HeadSnap,
            AngryPant, SideEye, PawFlex, ToeTap, BoardGlance, JokerGlance, DeckGlance, MouthWipe
        }

        private readonly List<IdleKind> idleHistory = new List<IdleKind>();
        private float nextIdleAt;
        private System.Random idleRng = new System.Random(7);

        /// <summary>Idles off while a lab scene holds the pet in a pose.</summary>
        public bool IdlesEnabled = true;

        public IdleKind? LastIdle { get; private set; }

        private void ResetIdleHistory()
        {
            idleHistory.Clear();
            idleRng = new System.Random((int)((lifeSeed * 2654435761u) & 0x7fffffff));
            nextIdleAt = clock + 2.4f;
        }

        private void ScheduleNextIdle(float extra)
        {
            float min = Tuning.IdleIntervalMin;
            float max = Tuning.IdleIntervalMax;
            float k = 1f;
            switch (State.Stage)
            {
                case PetHungerStage.Impatient: k = Tuning.ImpatientIdleRate; break;
                case PetHungerStage.Angry: k = 0.75f; break;
                case PetHungerStage.Satisfied: k = 1.3f; break;
                case PetHungerStage.Furious: min = 2.6f; max = 4.4f; break;
                default: k = State.Pending == 1 ? 0.85f : 1f; break;
            }
            nextIdleAt = clock + extra + Mathf.Lerp(min, max, (float)idleRng.NextDouble()) * k;
        }

        private void TickIdleScheduler()
        {
            if (!IdlesEnabled || exiting || queue.Busy || drag.Active || clock < nextIdleAt)
            {
                return;
            }
            IdleKind? kind = PickIdle();
            ScheduleNextIdle(0f);
            if (kind.HasValue)
            {
                PlayIdle(kind.Value);
            }
        }

        /// <summary>Plays one idle now (the lab's buttons, and the scheduler).</summary>
        public void PlayIdle(IdleKind kind)
        {
            LastIdle = kind;
            idleHistory.Add(kind);
            if (idleHistory.Count > 3)
            {
                idleHistory.RemoveAt(0);
            }
            IdleKind k = kind;
            Enqueue(PetPresentationPriority.Idle, "idle: " + kind, () => IdleRoutine(k), true, 0f, null);
        }

        private IdleKind? PickIdle()
        {
            var kinds = new List<IdleKind>();
            var weights = new List<float>();
            IdleWeights(kinds, weights);
            float total = 0f;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (idleHistory.Contains(kinds[i]))
                {
                    weights[i] = 0f;
                }
                total += weights[i];
            }
            if (total <= 0f)
            {
                return null;
            }
            float roll = (float)idleRng.NextDouble() * total;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (roll < weights[i])
                {
                    return kinds[i];
                }
                roll -= weights[i];
            }
            return kinds[kinds.Count - 1];
        }

        /// <summary>The set the mood calls for, with weights.</summary>
        private void IdleWeights(List<IdleKind> kinds, List<float> weights)
        {
            System.Action<IdleKind, float> add = (k, w) => { kinds.Add(k); weights.Add(w); };
            bool hand = Anchors.HandFocus.HasValue;
            switch (State.Stage)
            {
                case PetHungerStage.Satisfied:
                    add(IdleKind.SleepySmile, 3f); add(IdleKind.BellyRub, 3f); add(IdleKind.SmallYawn, 2f);
                    add(IdleKind.ContentBlink, 3f); add(IdleKind.NapWobble, 2f); add(IdleKind.Peek, 0.7f);
                    return;
                case PetHungerStage.Furious:
                    add(IdleKind.AngryPant, 3f); add(IdleKind.SideEye, 2.5f); add(IdleKind.PawFlex, 2.5f);
                    add(IdleKind.ToeTap, 2.5f); add(IdleKind.BoardGlance, 2f); add(IdleKind.JokerGlance, 1.5f);
                    add(IdleKind.DeckGlance, 1.5f); add(IdleKind.MouthWipe, 2f);
                    return;
                case PetHungerStage.Impatient:
                    add(IdleKind.EdgeTap, 3f); add(IdleKind.CheekPuff, 3f); add(IdleKind.ArmsCross, 3f);
                    add(IdleKind.Stomp, 2f); add(IdleKind.RequestGlance, 3f); add(IdleKind.BellyPat, 1f);
                    add(IdleKind.Lick, 1f); add(IdleKind.Peek, 0.6f);
                    return;
                case PetHungerStage.Angry:
                    add(IdleKind.HeadSnap, 3f); add(IdleKind.ArmsCross, 2.5f); add(IdleKind.Stomp, 2f);
                    add(IdleKind.EdgeTap, 2f); add(IdleKind.CheekPuff, 2f); add(IdleKind.RequestGlance, 2f);
                    return;
                case PetHungerStage.Hungry:
                    add(IdleKind.RequestGlance, 4f); add(IdleKind.BellyPat, 3f); add(IdleKind.Lick, 3f);
                    add(IdleKind.MouthOpenWait, 2f); add(IdleKind.Peek, 1.5f); add(IdleKind.Think, 1f);
                    add(IdleKind.DoubleBlink, 1.5f); add(IdleKind.EdgeTap, 1f); add(IdleKind.HandWave, 0.5f);
                    if (hand)
                    {
                        add(IdleKind.CardTrack, 1f);
                    }
                    return;
            }
            bool eager = State.Pending == 1;
            add(IdleKind.Peek, 3f); add(IdleKind.HandWave, 2f); add(IdleKind.Yawn, 1.5f);
            add(IdleKind.Think, 1.5f); add(IdleKind.RequestGlance, eager ? 4f : 2.5f);
            add(IdleKind.Lick, eager ? 2f : 1.5f); add(IdleKind.BellyPat, eager ? 2f : 1.5f);
            add(IdleKind.CheekSquish, 1f); add(IdleKind.DoubleBlink, 2f); add(IdleKind.LittleHop, 1.5f);
            add(IdleKind.SnackDream, 0.35f); add(IdleKind.EdgeTap, 1f);
            add(IdleKind.MouthOpenWait, eager ? 2.5f : 1f);
            if (hand)
            {
                add(IdleKind.CardTrack, 1.2f);
            }
        }

        private IEnumerator IdleRoutine(IdleKind kind)
        {
            switch (kind)
            {
                case IdleKind.Peek: return IdlePeek();
                case IdleKind.HandWave: return IdleWave(3);
                case IdleKind.Yawn: return IdleYawn(1f);
                case IdleKind.Think: return IdleThink();
                case IdleKind.RequestGlance: return IdleRequestGlance(false);
                case IdleKind.Lick: return IdleLick();
                case IdleKind.BellyPat: return IdleBellyPat(2, false);
                case IdleKind.CheekSquish: return IdleCheekSquish();
                case IdleKind.DoubleBlink: return IdleDoubleBlink(false);
                case IdleKind.LittleHop: return IdleHop();
                case IdleKind.CardTrack: return IdleCardTrack();
                case IdleKind.SnackDream: return IdleSnackDream();
                case IdleKind.EdgeTap: return IdleEdgeTap(State.Stage == PetHungerStage.Impatient ? 3 : 2);
                case IdleKind.MouthOpenWait: return IdleMouthWait();
                case IdleKind.SleepySmile: return IdleSleepySmile();
                case IdleKind.BellyRub: return IdleBellyPat(3, true);
                case IdleKind.SmallYawn: return IdleYawn(0.6f);
                case IdleKind.ContentBlink: return IdleDoubleBlink(true);
                case IdleKind.NapWobble: return IdleNap();
                case IdleKind.CheekPuff: return IdleCheekPuff();
                case IdleKind.ArmsCross: return IdleArmsCross();
                case IdleKind.Stomp: return IdleStomp();
                case IdleKind.HeadSnap: return IdleRequestGlance(true);
                case IdleKind.AngryPant: return IdlePant();
                case IdleKind.SideEye: return IdleSideEye();
                case IdleKind.PawFlex: return IdlePawFlex();
                case IdleKind.ToeTap: return IdleToeTap();
                case IdleKind.BoardGlance: return IdleGlanceAt(Anchors.BoardCentre, true);
                case IdleKind.JokerGlance: return IdleGlanceAt(Anchors.JokerBar, true);
                case IdleKind.DeckGlance: return IdleGlanceAt(Anchors.DrawPile, true);
                default: return IdleMouthWipe();
            }
        }

        // ================================================================== geometry helpers

        /// <summary>The top of the ear tips, in body units.</summary>
        private const float TopY = 1.1f;

        /// <summary>The presence at which the top <paramref name="units"/> of the character are
        /// over the edge of its nest (0 = hidden, 1 = fully out).</summary>
        private float PresenceShowing(float units)
        {
            float clipFloor = Home.Clip.yMin;
            float hideBy = Home.Base.y + (TopY - units) * S - clipFloor;
            return Mathf.Clamp01(1f - hideBy / Mathf.Max(0.01f, Home.HideDepth));
        }

        /// <summary>The paw on the side of the screen edge (the one that taps it).</summary>
        private bool EdgePawIsRight
        {
            get { return Home.Facing < 0; }
        }

        /// <summary>The paw on the board's side (the one that reaches for things).</summary>
        private bool BoardPawIsRight
        {
            get { return Home.Facing > 0; }
        }

        private void SetPaw(bool right, PetPaw paw)
        {
            if (right)
            {
                act.PawR = paw;
            }
            else
            {
                act.PawL = paw;
            }
        }

        /// <summary>A paw pointing at a world point from its shoulder: the angle the rig wants and a
        /// little reach toward it.</summary>
        private PetPaw PawToward(bool right, Vector3 world, float reach)
        {
            Vector3 shoulder = rig.ShoulderWorld(right);
            Vector2 d = (Vector2)(world - shoulder);
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            // the rig's angle is "raised outward": the right paw points along +x at 0, the left
            // along -x
            float angle = right ? a : 180f - a;
            angle = Mathf.DeltaAngle(0f, angle);
            Vector2 n = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.zero;
            return Paw(n.x * reach, n.y * reach, angle);
        }

        // ================================================================== the entrance

        private void SkipEntrance()
        {
            presence = 1f;
            RevealPlatesNow();
        }

        /// <summary>
        /// The brief's choreography, ~0.9 s: two antennae come up over the edge, then the eyes, the
        /// head a little more; the eyes scan the board left, centre, the cards (~150 ms); then it
        /// pops out with a small hop and lands with a 1.06/0.94 squash; the plates pop one, then
        /// the other 70 ms later, and it reaches a paw to each; it looks at the player's cards,
        /// makes a small "o", and pats its belly with a tiny inward squeeze.
        /// </summary>
        private IEnumerator Entrance()
        {
            SetState(PetViewState.Entering);
            HidePlatesNow();
            act.Presence = 0f;
            presence = 0f;
            yield return null;
            // A: the ears
            act.EarWobble = 9f;
            act.Presence = PresenceShowing(0.16f);
            yield return Wait(0.13f);
            // ... then the eyes
            act.EarWobble = 3f;
            act.Presence = PresenceShowing(0.58f);
            act.LookAt = Anchors.BoardCentre;
            yield return Wait(0.1f);
            // ... the head a bit more
            act.Presence = PresenceShowing(0.7f);
            yield return Wait(0.06f);
            // the scan: left -> centre -> the cards
            Rect b = Anchors.Board;
            act.LookAt = new Vector3(F > 0 ? b.xMax : b.xMin, b.center.y, 0f);
            yield return Wait(0.05f);
            act.LookAt = Anchors.BoardCentre;
            yield return Wait(0.05f);
            act.LookAt = Anchors.HandCentre;
            yield return Wait(0.05f);
            // B: the body pops out with a small hop
            Sound(PetSound.Enter);
            act.Presence = 1f;
            presence = Mathf.Max(presence, 0.86f);
            yield return Tween(0.12f, t =>
            {
                act.Offset = new Vector2(0f, Bell(t) * Px(18f));
                act.Squash = new Vector2(1f - 0.05f * Bell(t), 1f + 0.06f * Bell(t));
            });
            // landing: 1.06 / 0.94, then settle
            yield return Tween(0.13f, t =>
            {
                float k = 1f - Back(t, 2.2f);
                act.Offset = Vector2.zero;
                act.Squash = new Vector2(1f + 0.06f * k, 1f - 0.06f * k);
            });
            act.Squash = Vector2.one;
            act.EarWobble = null;
            // C: the request reveal
            SetState(PetViewState.Requesting);
            Sound(PetSound.Request);
            RevealPlates();
            for (int i = 0; i < Mathf.Min(2, plates.Count); i++)
            {
                Vector3 at = PlateWorld(i);
                act.LookAt = at;
                SetPaw(BoardPawIsRight, PawToward(BoardPawIsRight, at, 0.05f));
                HeadToward(at, 0.6f);
                yield return Wait(0.14f);
            }
            SetPaw(BoardPawIsRight, Paw(0f, 0f, -18f));
            act.Head = Vector2.zero;
            act.HeadTilt = 0f;
            // D: the player's cards, a small "o", a paw on the belly
            act.LookAt = Anchors.HandCentre;
            act.Mouth = "nom";
            act.Emotion = PetEmotion.Hungry;
            SetPaw(!BoardPawIsRight, BellyPaw(!BoardPawIsRight));
            yield return Tween(0.2f, t => act.Squash = new Vector2(1f - 0.03f * Bell(t), 1f + 0.01f * Bell(t)));
            yield return Wait(0.12f);
        }

        /// <summary>A paw resting on the belly.</summary>
        private static PetPaw BellyPaw(bool right)
        {
            return Paw(right ? -0.13f : 0.13f, -0.07f, -158f);
        }

        // ================================================================== the exits

        /// <summary>
        /// Leaving. Fed: a happy little wave and down behind the edge (~0.45 s). Furious: a smug
        /// grin, held, and then it sinks slowly away - no redemption. Neither: a small wave.
        /// </summary>
        private IEnumerator Exit()
        {
            SetState(PetViewState.Exiting);
            ClearDrag();
            bool fed = State.Stage == PetHungerStage.Satisfied;
            bool furious = State.Stage == PetHungerStage.Furious;
            FoldPlates();
            if (furious)
            {
                act.Emotion = PetEmotion.Smug;
                act.LookAt = Anchors.HandCentre;
                Sound(PetSound.Smug);
                yield return Wait(0.3f);
                yield return Tween(0.65f, t =>
                {
                    act.Presence = Mathf.Lerp(1f, 0f, EaseIn(t));
                    act.Alpha = Mathf.Lerp(1f, 0.25f, Smooth((t - 0.4f) / 0.6f));
                });
            }
            else
            {
                act.Emotion = fed ? PetEmotion.Happy : PetEmotion.Neutral;
                act.LookAt = Anchors.HandCentre;
                bool right = BoardPawIsRight;
                yield return Tween(0.26f, t =>
                {
                    float w = Mathf.Sin(t * Mathf.PI * 4f);
                    SetPaw(right, Paw(right ? 0.02f : -0.02f, 0.12f, 72f + 22f * w));
                });
                yield return Tween(0.2f, t => act.Presence = Mathf.Lerp(1f, 0f, EaseIn(t)));
            }
            act.Presence = 0f;
            presence = 0f;
            yield return Wait(0.05f);
            HideNow();
        }

        // ================================================================== the idle library

        private IEnumerator IdlePeek()
        {
            // how far: just the eyes most of the time, the head or half the body sometimes
            double r = idleRng.NextDouble();
            float showing = r < 0.6 ? 0.56f : r < 0.85 ? 0.74f : 0.86f;
            yield return Tween(0.45f, t => act.Presence = Mathf.Lerp(1f, PresenceShowing(showing), Smooth(t)));
            float hold = 1.2f + (float)idleRng.NextDouble() * 0.6f;
            yield return Tween(hold, t =>
            {
                act.LookAt = Anchors.HandFocus ?? Anchors.HandCentre;
            });
            // sometimes it hides all the way for a moment before coming back
            if (idleRng.NextDouble() < 0.2)
            {
                yield return Tween(0.22f, t => act.Presence = Mathf.Lerp(PresenceShowing(showing), 0f, Smooth(t)));
                yield return Wait(0.4f);
            }
            act.LookAt = null;
            float from = act.Presence ?? 1f;
            yield return Tween(0.18f, t => act.Presence = Mathf.Lerp(from, 1f, EaseOut(t)));
            yield return Tween(0.14f, t =>
            {
                float k = 1f - Back(t, 2f);
                act.Squash = new Vector2(1f + 0.04f * k, 1f - 0.04f * k);
            });
        }

        private IEnumerator IdleWave(int waves)
        {
            Sound(PetSound.Idle);
            bool right = BoardPawIsRight;
            act.LookAt = Anchors.HandCentre;
            act.Emotion = PetEmotion.Happy;
            act.EmotionWeight = 0.5f;
            yield return Tween(0.12f, t => SetPaw(right, Paw(0f, 0.1f * t, Mathf.Lerp(-18f, 70f, EaseOut(t)))));
            float dur = 0.16f * waves;
            yield return Tween(dur, t =>
            {
                float w = Mathf.Sin(t * Mathf.PI * 2f * waves);
                SetPaw(right, Paw(right ? 0.015f * w : -0.015f * w, 0.1f, 70f + 24f * w));
                act.HeadTilt = -F * 3f * Bell(t);
            });
            yield return Tween(0.12f, t => SetPaw(right, Paw(0f, 0.1f * (1f - t), Mathf.Lerp(70f, -18f, t))));
        }

        private IEnumerator IdleYawn(float size)
        {
            // mouth wide, eyes half shut, the body stretches - then a small "pop" back
            act.Lid = 0.55f;
            act.Mouth = "wide";
            act.EarWobble = 0f;
            yield return Tween(0.35f * size + 0.15f, t =>
            {
                float k = Smooth(t);
                act.MouthOpen = Mathf.Lerp(0.3f, 1.1f, k);
                act.MouthScale = Mathf.Lerp(0.8f, 1f + 0.15f * size, k);
                act.Squash = new Vector2(1f - 0.03f * k * size, 1f + 0.05f * k * size);
                act.Lid = Mathf.Lerp(0.2f, 0.62f, k);
                act.Head = new Vector2(0f, 0.012f * k);
                act.PawL = Paw(0.02f * k, 0.05f * k, 30f * k);
                act.PawR = Paw(-0.02f * k, 0.05f * k, 30f * k);
            });
            yield return Wait(0.35f * size);
            act.Mouth = "closed";
            yield return Tween(0.16f, t =>
            {
                float k = 1f - Back(t, 2.4f);
                act.Squash = new Vector2(1f + 0.04f * k, 1f - 0.05f * k);
                act.MouthOpen = 1f;
                act.MouthScale = 1f;
                act.Lid = Mathf.Lerp(0.62f, 0.1f, t);
                act.Head = Vector2.zero;
                act.PawL = null;
                act.PawR = null;
            });
        }

        private IEnumerator IdleThink()
        {
            bool right = !BoardPawIsRight;
            act.Look = new Vector2(-0.8f, 0.85f);
            yield return Tween(0.2f, t =>
            {
                SetPaw(right, Paw(right ? -0.24f * t : 0.24f * t, 0.07f * t, Mathf.Lerp(-18f, 112f, Smooth(t))));
                act.HeadTilt = 6f * Smooth(t) * (right ? 1f : -1f);
            });
            act.Mouth = "chew_a";
            yield return Wait(0.9f);
            act.Mouth = null;
            act.Look = null;
            yield return Tween(0.2f, t =>
            {
                SetPaw(right, Paw(right ? -0.24f * (1f - t) : 0.24f * (1f - t), 0.07f * (1f - t),
                    Mathf.Lerp(112f, -18f, Smooth(t))));
                act.HeadTilt = 6f * (1f - Smooth(t)) * (right ? 1f : -1f);
            });
        }

        /// <summary>Plate 1 -> plate 2 -> the cards. Snapped (the angry "head snap") it is quick
        /// and hard, with the head turning hard toward the plates first.</summary>
        private IEnumerator IdleRequestGlance(bool snap)
        {
            float step = snap ? 0.18f : 0.34f;
            for (int i = 0; i < plates.Count; i++)
            {
                if (plates[i].Fed)
                {
                    continue;
                }
                Vector3 at = PlateWorld(i);
                act.LookAt = at;
                HeadToward(at, snap ? 1.4f : 0.7f);
                if (snap)
                {
                    act.Emotion = PetEmotion.Angry;
                    act.Squash = new Vector2(0.98f, 1.02f);
                }
                yield return Wait(step);
            }
            act.Squash = Vector2.one;
            act.LookAt = Anchors.HandCentre;
            HeadToward(Anchors.HandCentre, 0.5f);
            yield return Wait(step + 0.08f);
            if (snap && plates.Count > 0)
            {
                act.LookAt = PlateWorld(0);
                yield return Wait(0.15f);
            }
        }

        private IEnumerator IdleLick()
        {
            act.Mouth = "small";
            yield return Tween(0.55f, t =>
            {
                act.Lick = Bell(t);
                act.Head = new Vector2(0f, 0.006f * Bell(t));
            });
            act.Mouth = null;
        }

        private IEnumerator IdleBellyPat(int pats, bool rub)
        {
            bool right = !BoardPawIsRight;
            if (rub)
            {
                act.Emotion = PetEmotion.Happy;
                act.EmotionWeight = 0.7f;
            }
            yield return Tween(0.14f, t => SetPaw(right, BlendPaw(Paw(0f, 0f, -18f), BellyPaw(right), Smooth(t))));
            for (int i = 0; i < pats; i++)
            {
                if (rub)
                {
                    // both paws, small circles
                    yield return Tween(0.36f, t =>
                    {
                        float a = t * Mathf.PI * 2f;
                        PetPaw p = BellyPaw(right);
                        p.Offset += new Vector2(Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.02f);
                        SetPaw(right, p);
                        PetPaw q = BellyPaw(!right);
                        q.Offset += new Vector2(-Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.02f);
                        SetPaw(!right, q);
                        act.Belly = 0.015f * Mathf.Sin(a);
                    });
                }
                else
                {
                    yield return Tween(0.16f, t =>
                    {
                        PetPaw p = BellyPaw(right);
                        p.Offset += new Vector2(0f, 0.04f * Bell(t));
                        SetPaw(right, p);
                        act.Belly = -0.02f * Bell(Mathf.Clamp01(t * 2f - 1f));
                        act.Squash = new Vector2(1f + 0.01f * Bell(t), 1f - 0.01f * Bell(t));
                    });
                }
            }
            yield return Tween(0.14f, t => SetPaw(right, BlendPaw(BellyPaw(right), Paw(0f, 0f, -18f), Smooth(t))));
        }

        private static PetPaw BlendPaw(PetPaw a, PetPaw b, float t)
        {
            return new PetPaw
            {
                Offset = Vector2.Lerp(a.Offset, b.Offset, t),
                Angle = Mathf.Lerp(a.Angle, b.Angle, t),
                Scale = Mathf.Lerp(a.Scale <= 0f ? 1f : a.Scale, b.Scale <= 0f ? 1f : b.Scale, t)
            };
        }

        private IEnumerator IdleCheekSquish()
        {
            bool right = BoardPawIsRight;
            // the paw pushes its own side's cheek; the other cheek bulges and that eye squints
            yield return Tween(0.18f, t => SetPaw(right, Paw(right ? -0.13f * t : 0.13f * t, 0.15f * t,
                Mathf.Lerp(-18f, 128f, Smooth(t)))));
            yield return Tween(0.4f, t =>
            {
                float k = Bell(t);
                if (right)
                {
                    act.PuffL = 0.7f * k;
                }
                else
                {
                    act.PuffR = 0.7f * k;
                }
                act.HeadTilt = (right ? 7f : -7f) * k;
                act.Lid = 0.35f * k;
                act.Mouth = "smug";
            });
            act.Mouth = null;
            act.Lid = -1f;
            yield return Tween(0.16f, t => SetPaw(right, Paw(right ? -0.13f * (1f - t) : 0.13f * (1f - t),
                0.15f * (1f - t), Mathf.Lerp(128f, -18f, Smooth(t)))));
        }

        private IEnumerator IdleDoubleBlink(bool content)
        {
            if (content)
            {
                act.Emotion = PetEmotion.Happy;
                act.EmotionWeight = 0.45f;
            }
            yield return Tween(0.1f, t => act.Blink = BlinkCurve(t * 0.12f));
            act.Blink = 0f;
            yield return Wait(0.12f);
            // the slow one
            yield return Tween(0.36f, t => act.Blink = Bell(t));
            act.Blink = 0f;
            yield return Wait(0.1f);
        }

        private IEnumerator IdleHop()
        {
            Sound(PetSound.Idle);
            float h = Px(3f);
            yield return Tween(0.08f, t => act.Squash = new Vector2(1f + 0.03f * t, 1f - 0.04f * t));
            yield return Tween(0.2f, t =>
            {
                act.Offset = new Vector2(0f, Bell(t) * h * 2.2f);
                act.Squash = new Vector2(1f - 0.02f * Bell(t), 1f + 0.03f * Bell(t));
                act.EarWobble = -6f * Bell(t);
            });
            yield return Tween(0.14f, t =>
            {
                float k = 1f - Back(t, 2.2f);
                act.Offset = Vector2.zero;
                act.Squash = new Vector2(1f + 0.035f * k, 1f - 0.04f * k);
            });
        }

        private IEnumerator IdleCardTrack()
        {
            yield return Tween(1.3f, t =>
            {
                act.LookAt = Anchors.HandFocus ?? Anchors.HandCentre;
                HeadToward(act.LookAt.Value, 0.35f);
            });
        }

        private IEnumerator IdleSnackDream()
        {
            act.Lid = 0.38f;
            act.Look = new Vector2(0.1f, 1f);
            act.Mouth = "small";
            Vector3 head = rig.EyeWorld(true) + new Vector3(0.15f * S * (F > 0 ? 1f : -1f), 0.45f * S, 0f);
            SpawnThought(head, 1f);
            yield return Wait(1.05f);
            act.Look = null;
        }

        private IEnumerator IdleEdgeTap(int taps)
        {
            bool right = EdgePawIsRight;
            act.LookAt = Anchors.HandCentre;
            yield return Tween(0.1f, t => SetPaw(right, Paw(right ? 0.06f * t : -0.06f * t, 0.02f * t,
                Mathf.Lerp(-18f, 10f, t))));
            for (int i = 0; i < taps; i++)
            {
                yield return Tween(0.13f, t =>
                {
                    float k = Bell(t);
                    SetPaw(right, Paw(right ? 0.06f + 0.03f * k : -0.06f - 0.03f * k, 0.02f, 10f + 18f * k));
                    if (t > 0.5f && t < 0.6f)
                    {
                        Sound(PetSound.Tap);
                    }
                });
            }
            yield return Tween(0.12f, t => SetPaw(right, Paw(right ? 0.06f * (1f - t) : -0.06f * (1f - t),
                0.02f * (1f - t), Mathf.Lerp(10f, -18f, t))));
        }

        private IEnumerator IdleMouthWait()
        {
            // "I'm ready": the mouth opens for a moment toward the cards, then shuts
            act.LookAt = Anchors.HandCentre;
            act.Mouth = "medium";
            act.Emotion = PetEmotion.Curious;
            act.EmotionWeight = 0.6f;
            yield return Tween(0.5f, t =>
            {
                act.MouthOpen = Mathf.Lerp(0.6f, 1.05f, Bell(t));
                act.Offset = new Vector2(F * Px(2f) * Bell(t), 0f);
            });
            act.Mouth = "closed";
            yield return Wait(0.12f);
        }

        // ---- satisfied

        private IEnumerator IdleSleepySmile()
        {
            act.Emotion = PetEmotion.Sleepy;
            act.Mouth = "closed";
            yield return Tween(1.8f, t =>
            {
                act.Lid = Mathf.Lerp(0.45f, 0.7f, Bell(t));
                act.Rot = 1.2f * Mathf.Sin(t * Mathf.PI * 2f) * Bell(t);
            });
        }

        private IEnumerator IdleNap()
        {
            act.Emotion = PetEmotion.Sleepy;
            yield return Tween(0.5f, t => act.Lid = Mathf.Lerp(0.5f, 0.95f, Smooth(t)));
            yield return Tween(0.9f, t =>
            {
                act.Lid = 0.95f;
                act.Rot = 2.2f * Mathf.Sin(t * Mathf.PI * 2f);
                act.Head = new Vector2(0f, -0.015f * Smooth(t));
            });
            // a little nod wakes it
            yield return Tween(0.18f, t =>
            {
                act.Head = new Vector2(0f, -0.015f - 0.02f * Bell(t));
                act.Lid = Mathf.Lerp(0.95f, 0.35f, t);
            });
            act.Head = Vector2.zero;
            yield return Wait(0.2f);
        }

        // ---- impatient / angry

        private IEnumerator IdleCheekPuff()
        {
            Sound(PetSound.Impatient);
            act.Emotion = PetEmotion.Impatient;
            act.Mouth = "closed";
            yield return Tween(0.18f, t => { act.PuffL = t; act.PuffR = t; act.MouthScale = 1f - 0.3f * t; });
            yield return Wait(0.45f);
            yield return Tween(0.14f, t =>
            {
                act.PuffL = 1f - t;
                act.PuffR = 1f - t;
                act.MouthScale = 0.7f + 0.3f * t;
                act.Squash = new Vector2(1f - 0.02f * Bell(t), 1f + 0.02f * Bell(t));
            });
        }

        private IEnumerator IdleArmsCross()
        {
            act.Emotion = State.Stage == PetHungerStage.Angry ? PetEmotion.Angry : PetEmotion.Impatient;
            yield return Tween(0.18f, t =>
            {
                act.PawL = BlendPaw(Paw(0f, 0f, -18f), Paw(0.26f, -0.02f, -165f), Smooth(t));
                act.PawR = BlendPaw(Paw(0f, 0f, -18f), Paw(-0.26f, 0.03f, -168f), Smooth(t));
            });
            yield return Tween(1.1f, t =>
            {
                act.LookAt = (int)(t * 3f) % 2 == 0 ? PlateWorld(FirstPending()) : (Vector3)Anchors.HandCentre;
                act.HeadTilt = -F * 4f;
            });
            yield return Tween(0.16f, t =>
            {
                act.PawL = BlendPaw(Paw(0.26f, -0.02f, -165f), Paw(0f, 0f, -18f), Smooth(t));
                act.PawR = BlendPaw(Paw(-0.26f, 0.03f, -168f), Paw(0f, 0f, -18f), Smooth(t));
                act.HeadTilt = -F * 4f * (1f - t);
            });
        }

        /// <summary>A small stomp: two pixels down and a squash. No screen shake, ever.</summary>
        private IEnumerator IdleStomp()
        {
            act.Emotion = PetEmotion.Impatient;
            yield return Tween(0.1f, t => act.Offset = new Vector2(0f, Px(3f) * EaseOut(t)));
            Sound(PetSound.Stomp);
            yield return Tween(0.08f, t =>
            {
                act.Offset = new Vector2(0f, Mathf.Lerp(Px(3f), -Px(2f), EaseIn(t)));
                act.Squash = new Vector2(1f + 0.05f * t, 1f - 0.06f * t);
            });
            yield return Tween(0.18f, t =>
            {
                act.Offset = new Vector2(0f, -Px(2f) * (1f - t));
                float k = 1f - Back(t, 2f);
                act.Squash = new Vector2(1f + 0.05f * k, 1f - 0.06f * k);
            });
        }

        // ---- furious

        private IEnumerator IdlePant()
        {
            act.Mouth = "small";
            for (int i = 0; i < 3; i++)
            {
                yield return Tween(0.17f, t =>
                {
                    act.MouthOpen = Mathf.Lerp(0.7f, 1.25f, Bell(t));
                    act.Squash = new Vector2(1f - 0.015f * Bell(t), 1f + 0.025f * Bell(t));
                });
            }
        }

        private IEnumerator IdleSideEye()
        {
            act.Lid = 0.42f;
            act.Look = new Vector2(-F * 1f, -0.2f);
            yield return Wait(0.5f);
            act.LookAt = Anchors.HandCentre;
            act.Look = null;
            yield return Wait(0.6f);
        }

        private IEnumerator IdlePawFlex()
        {
            for (int i = 0; i < 2; i++)
            {
                yield return Tween(0.16f, t =>
                {
                    float k = Bell(t);
                    act.PawL = Paw(0.02f, 0.03f * k, -22f + 40f * k, 1f + 0.08f * k);
                    act.PawR = Paw(-0.02f, 0.03f * k, -22f + 40f * k, 1f + 0.08f * k);
                });
            }
        }

        private IEnumerator IdleToeTap()
        {
            for (int i = 0; i < 4; i++)
            {
                yield return Tween(0.12f, t =>
                {
                    act.Offset = new Vector2(0f, Px(1.2f) * Bell(t));
                    act.Rot = -F * 1.2f * Bell(t);
                });
            }
        }

        /// <summary>A sudden look at something it could eat - the board, the joker bar, the deck.</summary>
        private IEnumerator IdleGlanceAt(Vector2 at, bool hungry)
        {
            act.LookAt = at;
            HeadToward(at, 1f);
            if (hungry)
            {
                act.Lick = 0.6f;
            }
            yield return Wait(0.45f);
            act.Lick = 0f;
            act.Emotion = PetEmotion.Smug;
            act.EmotionWeight = 0.5f;
            yield return Wait(0.3f);
        }

        private IEnumerator IdleMouthWipe()
        {
            bool right = BoardPawIsRight;
            yield return Tween(0.4f, t =>
            {
                float sweep = Mathf.Sin(t * Mathf.PI);
                SetPaw(right, Paw(right ? -0.28f * sweep : 0.28f * sweep, 0.12f * sweep, 140f * sweep - 18f * (1f - sweep)));
                act.Mouth = "chew_a";
            });
            act.Mouth = null;
            yield return Tween(0.3f, t => act.Lick = Bell(t));
        }

        private int FirstPending()
        {
            for (int i = 0; i < plates.Count; i++)
            {
                if (!plates[i].Fed)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
