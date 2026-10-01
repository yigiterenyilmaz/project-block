// PURPOSE: "Tamagotchi" running out of patience - the HUNGER ESCALATION acted out (not a bar), the
// last-chance PREWARNING, and the FURIOUS TRANSITION, the boss's second big payoff.
//
// HUNGER is Core's: TamagotchiBoss.Stage and HungerProgress, reported when they move
// (TamagotchiHungerChange). The mood face, the idle set, the plates' tint, the aura and the
// patience ring all follow it on their own (.Idle, .Plates); a stage CHANGE also gets a short beat
// so the player sees the moment it happened - a belly pat and "Acıktım..." going hungry, a stomp
// and "Hadi ama!" going impatient, a hard head snap at the plates and "Bana onu ver." going angry.
//
// THE PREWARNING reads TamagotchiBoss.DeadlineNext - the rules' own "the next draw is the deadline"
// - never a guess off the progress: the pet ducks behind its edge, and 200-350 ms later two eyes
// come up over it glowing dark pink. Then it comes back with the glow still in its eyes.
//
// THE FURY (the corrective pass; ~1.55 s, one clock, the brief's table 152). The first version was
// a face change, a squash and a red puff - "it frowned, went red, and something was deleted". This
// one is the pet's whole character giving way, and the screen going with it:
//   A  0.00  DEAD SILENCE. It looks at the plate it never got. Nothing moves, it does not blink,
//            and the rest of the game's audio steps back a third. The small silence is the point.
//   B  0.15  DISAPPOINTMENT. The head sinks 4 px, the eyes narrow slowly, the mouth is a small line
//            turned down, the paws hang at its sides - "are you serious". The breath goes out of it.
//   C  0.30  THE GROWL starts - low, from the throat - and the body is pulled in (0.95), trembling
//            with it. Nothing on the screen has changed yet.
//   D  0.38  THE FACE BREAKS: the furious skin comes up through the cute one (TamagotchiRig's Fury)
//            - eyes shrink into slits, the brows snap down, the blush goes to a dark tension, the
//            mouth opens slowly and the teeth show - and it leans forward over what it wants.
//   E  0.55  THE HATRED FIELD leaves it: a dark berry pressure front crosses the whole screen in a
//            quarter of a second (.Hatred) - the background a little darker and greyer, a thin
//            magenta split at the screen's edge, the arena's shadow heavier.
//   F  0.72  THE BREAK: 0.95 -> 1.10 -> 1.03, the mouth huge, the paws out and tense, a snarl, a
//            dozen short berry streaks and plum specks (no sparkles, no hearts), the plates cracking
//            and falling away.
//   G  0.90  THE IMPACT: the edge closes in for a moment and the arena takes ONE 2 px impulse; a
//            low whump under the growl's tail; and it SAYS it - "BEN ALIRIM.".
//   H  1.05  THE TARGET: the eyes snap, small and fast, to what Core's planner chose - the board's
//            chokepoint, that joker, that pile - the head snaps after them, and the mouth sets in a
//            crooked hungry grin. Then it HOLDS, for a quarter of a second, before anything is
//            taken (the predatory hold): the punish never starts on the fury's own beat.
// Afterwards the screen settles into the furious AMBIENT state (never the peak) and stays there.
// The first version is kept as FuryLegacy for the lab's side-by-side only.
// EXTENSION POINT: every length is in Tuning (FurySilence ... FuryTargetHold).

using System.Collections;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        /// <summary>The stage moved (Core's report). Calm -> Hungry -> Impatient -> Angry get a
        /// beat of their own; Satisfied and Furious are told by the feed and the fury.</summary>
        public void PlayHungerChange(TamagotchiHungerChange change)
        {
            if (change == null || !shown || exiting)
            {
                return;
            }
            PetHungerStage stage = change.Stage;
            if (stage == PetHungerStage.Hungry || stage == PetHungerStage.Impatient || stage == PetHungerStage.Angry)
            {
                Enqueue(PetPresentationPriority.Request, "hunger: " + stage, () => StageBeat(stage), true, 0.25f, null);
            }
        }

        private IEnumerator StageBeat(PetHungerStage stage)
        {
            // it comes out for these, wherever it was peeking from
            act.Presence = 1f;
            switch (stage)
            {
                case PetHungerStage.Hungry:
                    Sound(PetSound.Hungry);
                    Speak("Acıktım...", "I'm hungry...", true);
                    yield return IdleBellyPat(2, true);
                    yield return IdleRequestGlance(false);
                    break;
                case PetHungerStage.Impatient:
                    Sound(PetSound.Impatient);
                    Speak("Hadi ama!", "Come on!", true);
                    yield return IdleStomp();
                    yield return IdleEdgeTap(2);
                    break;
                default:
                    // a paw at the plate: "that one"
                    int want = FirstPending();
                    Speak("Bana onu ver.", "Give me that.", true);
                    if (plates.Count > 0)
                    {
                        Vector3 at = PlateWorld(want);
                        act.LookAt = at;
                        act.Emotion = PetEmotion.Angry;
                        HeadToward(at, 1.2f);
                        yield return Tween(0.14f, t => SetPaw(BoardPawIsRight, BlendPaw(Paw(0f, 0f, -18f), PawToward(BoardPawIsRight, at, 0.07f), Smooth(t))));
                        yield return Wait(0.34f);
                    }
                    act.LookAt = Anchors.HandCentre;
                    act.Emotion = PetEmotion.Angry;
                    yield return Wait(0.24f);
                    if (plates.Count > 0)
                    {
                        act.LookAt = PlateWorld(want);
                        yield return Wait(0.18f);
                    }
                    break;
            }
        }

        /// <summary>The next draw is the deadline (TamagotchiBoss.DeadlineNext turned true).</summary>
        public void PlayPrewarning()
        {
            if (!shown || exiting || queue.Has("prewarning"))
            {
                return;
            }
            Enqueue(PetPresentationPriority.Request, "prewarning", Prewarning, false, 0.3f, null);
        }

        private IEnumerator Prewarning()
        {
            // the RULE goes in the bar; what the pet says about it comes out of the pet
            Say("Çok az kaldı: bir kart daha çekilirse çıldıracak.",
                "Almost out of time: one more draw and it snaps.");
            DismissSpeech();
            float rest = RestPresence;
            // gone behind the edge...
            Sound(PetSound.Hide);
            yield return Tween(0.2f, t => act.Presence = Mathf.Lerp(rest, 0f, EaseIn(t)));
            act.Presence = 0f;
            yield return Wait(0.28f);
            // ... and then two eyes over it, glowing dark pink
            act.Emotion = PetEmotion.Angry;
            act.Glow = 1f;
            act.LookAt = Anchors.HandCentre;
            float eyes = Mathf.Min(rest, PresenceShowing(0.56f));
            yield return Tween(0.18f, t => act.Presence = Mathf.Lerp(0f, eyes, EaseOut(t)));
            Sound(PetSound.GrowlIdle);
            yield return Tween(0.85f, t =>
            {
                act.Glow = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 3f);
                act.LookAt = t < 0.5f ? (Vector3)Anchors.HandCentre : PlateWorld(FirstPending());
            });
            yield return Tween(0.22f, t =>
            {
                act.Presence = Mathf.Lerp(eyes, 1f, EaseOut(t));
                act.Glow = Mathf.Lerp(1f, 0.3f, t);
            });
            Speak("Son şansın...", "Last chance...", true);
            yield return Wait(0.5f);
        }

        /// <summary>The deadline was missed (Core's report). The punish that comes in the same
        /// breath is queued separately, behind this.</summary>
        public void PlayFury(TamagotchiFuryVisuals fury)
        {
            if (fury == null || !shown)
            {
                return;
            }
            TamagotchiFuryVisuals f = fury;
            Enqueue(PetPresentationPriority.FuriousTransition, "fury", () => FuryRoutine(f), false, 0.35f, null);
        }

        /// <summary>Which parts of the fury the lab asked for (all of them in the game).</summary>
        [System.Flags]
        public enum FuryParts
        {
            Face = 1,
            Body = 2,
            Screen = 4,
            Voice = 8,
            All = 15
        }

        public FuryParts LabFuryParts = FuryParts.All;

        /// <summary>The punishes this fury has shown so far: the first is the full cinematic, the
        /// ones after it run a little quicker (Tuning.RepeatPunishPace).</summary>
        private int punishesThisFury;

        private IEnumerator FuryRoutine(TamagotchiFuryVisuals fury)
        {
            punishesThisFury = 0;
            if (LabLegacy)
            {
                yield return FuryLegacy(fury);
                yield break;
            }
            yield return GroundForFury();
            SetState(PetViewState.FuriousTransition);
            SetParticleBudget(16);
            ClearDrag();
            DismissSpeech();
            bool face = (LabFuryParts & FuryParts.Face) != 0;
            bool body = (LabFuryParts & FuryParts.Body) != 0;
            bool screen = (LabFuryParts & FuryParts.Screen) != 0;
            bool voice = (LabFuryParts & FuryParts.Voice) != 0;

            // ---- the timeline (seconds at 1x)
            float tSilence = Tuning.FurySilence;
            float tDisEnd = tSilence + Tuning.FuryDisappointment;
            float tGrowl = Mathf.Max(tSilence, tDisEnd - Tuning.FuryGrowlLead);
            float tFaceEnd = tDisEnd + Tuning.FuryFaceMorph;
            float tWave = tDisEnd + Tuning.FuryFaceMorph * 0.63f;
            float tBurst = tFaceEnd + 0.07f;
            float tBurstEnd = tBurst + Tuning.FuryBurstDuration;
            float tImpact = tBurst + Tuning.FuryBurstDuration * 0.69f;
            float tSnap = tImpact + 0.15f;
            float tEnd = tSnap + 0.25f;
            float tHoldEnd = tEnd + Tuning.FuryTargetHold;

            int missing = FirstPending();
            Vector3 plateAt = plates.Count > 0 ? PlateWorld(missing) : (Vector3)Anchors.HandCentre;
            Vector3 target = PunishTargetPoint(fury.PunishKind);
            float downUnits = Px(4f) / Mathf.Max(0.01f, S);
            float compress = Mathf.Clamp(Tuning.FuryBodyCompression, 0.85f, 1f);
            bool didDisappoint = false, didGrowl = false, didWave = false, didBurst = false, didImpact = false, didSnap = false;
            string beat = null;
            float t = 0f;
            act.Presence = 1f;
            act.Fury = 0f;
            Duck(1.2f, tHoldEnd + 0.2f);

            while (t < tHoldEnd)
            {
                string now = t < tSilence ? "silence" : t < tDisEnd ? "disappointment" : t < tBurst ? "face break"
                    : t < tImpact ? "burst" : t < tSnap ? "impact" : t < tEnd ? "target" : "hold";
                if (now != beat)
                {
                    if (beat != null && StopAfter(beat))
                    {
                        yield return LabEnd(null);
                        if (act.Fury.HasValue && act.Fury.Value < 0.99f)
                        {
                            act.Fury = null;
                        }
                        yield break;
                    }
                    beat = now;
                    Beat(now);
                }

                // ---- A: DEAD SILENCE - it looks at the plate it never got; nothing moves
                act.Presence = 1f;
                act.Blink = 0f;
                act.EarWobble = 0f;
                act.Still = t < tGrowl;
                if (t < tSilence)
                {
                    act.LookAt = plateAt;
                    act.Emotion = PetEmotion.Neutral;
                    act.Mouth = "closed";
                    act.MouthScale = 0.8f;
                }

                // ---- B: DISAPPOINTMENT - head down, eyes narrowing, a small line of a mouth
                float b = Smooth((t - tSilence) / Mathf.Max(0.01f, tDisEnd - tSilence));
                if (t >= tSilence && t < tBurst)
                {
                    if (!didDisappoint)
                    {
                        didDisappoint = true;
                        if (voice)
                        {
                            Sound(PetSound.Disappointed);
                        }
                    }
                    act.LookAt = plateAt;
                    act.Emotion = PetEmotion.Impatient;
                    act.EmotionWeight = 0.7f * b;
                    act.Mouth = "frown";
                    act.MouthScale = 0.8f;
                    act.Lid = 0.48f * b;
                    act.Head = new Vector2(0f, -downUnits * b);
                    act.PawL = Paw(0.03f * b, -0.04f * b, -18f - 16f * b);
                    act.PawR = Paw(-0.03f * b, -0.04f * b, -18f - 16f * b);
                }

                // ---- C: THE GROWL - the body pulled in, trembling with it
                float c = Smooth((t - tGrowl) / Mathf.Max(0.01f, tBurst - tGrowl));
                if (t >= tGrowl && t < tBurst)
                {
                    if (!didGrowl)
                    {
                        didGrowl = true;
                        if (voice)
                        {
                            Sound(PetSound.GrowlStart);
                        }
                    }
                    if (body)
                    {
                        float tremble = Mathf.Sin(t * 150f) * 0.004f * c;
                        act.Squash = new Vector2(1f - 0.02f * c + tremble, Mathf.Lerp(1f, compress, c) - tremble);
                        act.Belly = -0.05f * c;
                        act.Ear = Mathf.Lerp(0f, -6f, c);
                    }
                }

                // ---- D: THE FACE BREAKS - the other skin comes up through this one; it leans in
                float d = Smooth((t - tDisEnd) / Mathf.Max(0.01f, tFaceEnd - tDisEnd));
                if (t >= tDisEnd && t < tBurst)
                {
                    if (face)
                    {
                        act.Fury = d;
                        act.Emotion = PetEmotion.Furious;
                        act.EmotionWeight = d;
                        act.Lid = Mathf.Lerp(0.48f, 0f, d);
                        act.MouthScale = Mathf.Lerp(0.8f, 1f, d);
                        // the mouth opens slowly: the teeth are seen for the first time here
                        act.MouthOpen = Mathf.Lerp(0.7f, 1.12f, d);
                        act.IrisMul = Mathf.Lerp(1f, 0.86f, d);
                    }
                    if (body)
                    {
                        act.Offset = new Vector2(F * Px(5f) * d, 0f);
                        act.Rot = -F * 3.2f * d;
                        act.Head = new Vector2(F * 0.012f * d, -downUnits * (1f - 0.4f * d));
                    }
                    if (d > 0.2f && plates.Count > 0)
                    {
                        ShakePlates(0.1f);
                    }
                }

                // ---- E: THE HATRED FIELD leaves it
                if (t >= tWave && !didWave)
                {
                    didWave = true;
                    if (screen)
                    {
                        StartHatredWave();
                    }
                }

                // ---- F: THE BREAK - 0.95 -> 1.10 -> 1.03, mouth huge, paws out, a snarl
                if (t >= tBurst && t < tEnd)
                {
                    if (!didBurst)
                    {
                        didBurst = true;
                        if (voice)
                        {
                            Sound(PetSound.FuryBreak);
                        }
                        Haptic(PetHaptic.FuryPulse);
                        if (body)
                        {
                            PuffAura(TamagotchiArt.DeepRaspberry, 0.7f);
                            FuryStreaks(rig.BellyWorld + new Vector3(0f, 0.25f * S, 0f), 13);
                        }
                        BitePendingPlates();
                    }
                    float u = Mathf.Clamp01((t - tBurst) / Mathf.Max(0.01f, tBurstEnd - tBurst));
                    if (face)
                    {
                        act.Fury = 1f;
                        act.Emotion = PetEmotion.Furious;
                        act.EmotionWeight = 1f;
                        act.Lid = -1f;
                    }
                    if (t < tSnap)
                    {
                        act.Mouth = "furious";
                        act.MouthScale = 1f + 0.28f * Bell(Mathf.Clamp01(u * 0.8f + 0.2f));
                        act.MouthOpen = 1.2f;
                        act.IrisMul = 0.82f;
                        act.LookAt = Anchors.HandCentre;
                    }
                    if (body)
                    {
                        // the furious body is 3% the bigger for good (ComposePose); this is the
                        // overshoot on the way there: 0.95 pulled in, 1.10 at the top, 1.03 after
                        float s = u < 0.35f ? Mathf.Lerp(compress, 1.068f, EaseOut(u / 0.35f)) : Mathf.Lerp(1.068f, 1f, Smooth((u - 0.35f) / 0.65f));
                        act.Squash = Vector2.one;
                        act.Scale = s;
                        act.Belly = 0f;
                        float open = Bell(Mathf.Clamp01(u * 1.15f));
                        act.PawL = Paw(-0.06f * open, 0.07f * open, -30f + 62f * open, 1.04f + 0.1f * open);
                        act.PawR = Paw(0.06f * open, 0.07f * open, -30f + 62f * open, 1.04f + 0.1f * open);
                        act.Offset = new Vector2(F * Px(5f + 4f * open), Px(3f) * open);
                        act.Rot = -F * (3.2f + 2f * open);
                        act.Head = new Vector2(F * 0.012f, 0.012f * open);
                        act.EarWobble = -5f * open;
                    }
                }

                // ---- G: THE IMPACT on the screen, and what it says
                if (t >= tImpact && !didImpact)
                {
                    didImpact = true;
                    if (screen)
                    {
                        HatredImpact();
                    }
                    if (voice)
                    {
                        Sound(PetSound.FuryImpact);
                    }
                    Speak("BEN ALIRIM.", "I'LL TAKE IT.", true, 1.5f);
                    Say("Artık senden istemiyor - kendisi alacak.", "It is not asking any more - it will take it itself.");
                }

                // ---- H: THE TARGET - eyes first, small and fast; the head snaps after them
                if (t >= tSnap)
                {
                    if (!didSnap)
                    {
                        didSnap = true;
                        target = PunishTargetPoint(fury.PunishKind);
                    }
                    float h = Mathf.Clamp01((t - tSnap) / 0.09f);
                    act.LookAt = target;
                    act.IrisMul = 0.8f;
                    HeadToward(target, 1.35f * EaseOut(h));
                    // head down, eyes up from under the brows; a crooked hungry grin
                    act.Head += new Vector2(0f, -0.014f * h);
                    act.Mouth = "smug";
                    act.MouthScale = 1f;
                    act.MouthOpen = 1f;
                    act.Scale = 1f;
                    if (body)
                    {
                        float settle = Smooth(Mathf.Clamp01((t - tSnap) / 0.2f));
                        Vector2 toward = ((Vector2)target - (Vector2)rig.BellyWorld).normalized;
                        act.Offset = new Vector2(F * Px(5f), 0f) + toward * Px(3f) * settle;
                        act.Rot = -F * 3.2f;
                        act.PawL = null;
                        act.PawR = null;
                    }
                    if (t >= tEnd)
                    {
                        // the predatory hold: it does not move, it does not blink
                        act.Still = true;
                        act.SquintL = F > 0 ? 0f : 0.35f;
                        act.SquintR = F > 0 ? 0.35f : 0f;
                    }
                }

                yield return null;
                t += Dt;
            }
            act.Fury = null;
        }

        /// <summary>The fury's particles: short berry streaks thrown outward, dark plum specks, a
        /// few distorted flecks - 100-220 ms each. No sparkles, no hearts.</summary>
        private void FuryStreaks(Vector2 at, int count)
        {
            int n = (int)LodCount(count);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.15f).normalized;
                float life = Random.Range(0.1f, 0.22f);
                if (i % 3 == 2)
                {
                    // a plum speck
                    Spawn("fx_crumb", at + dir * 0.3f * S, dir * Random.Range(2.2f, 3.4f) * S, life, 0.42f * S, 0.2f * S,
                        new Color(0.27f, 0.08f, 0.19f, 0.95f), 1.5f * S, Random.Range(-500f, 500f), false, 3f);
                }
                else
                {
                    SpawnStreak(at + dir * 0.32f * S, dir, Random.Range(3.2f, 5.2f) * S, life,
                        i % 2 == 0 ? new Color(0.72f, 0.16f, 0.38f, 0.95f) : new Color(0.46f, 0.09f, 0.26f, 0.95f));
                }
            }
        }

        /// <summary>The point Core's chosen punish is about: the middle of the cells it will bite,
        /// the very panel it will take, the pile - or the class's own place when the report is not
        /// in yet.</summary>
        private Vector3 PunishTargetPoint(PetPunishKind kind)
        {
            PetRampageVisuals r = prepared;
            if (r != null)
            {
                switch (r.Kind)
                {
                    case PetPunishKind.Board:
                        if (r.Cells.Count > 0)
                        {
                            return Centroid(r.Cells);
                        }
                        break;
                    case PetPunishKind.Joker:
                    case PetPunishKind.Power:
                        if (itemProxy != null)
                        {
                            return itemRect.center;
                        }
                        break;
                    case PetPunishKind.DrawPile: return PileWorld(true);
                    case PetPunishKind.DiscardPile: return PileWorld(false);
                }
            }
            return PunishLookTarget(kind);
        }

        /// <summary>Where it looks to foreshadow a punish Core has already chosen.</summary>
        private Vector3 PunishLookTarget(PetPunishKind kind)
        {
            switch (kind)
            {
                case PetPunishKind.Joker: return Anchors.JokerBar;
                case PetPunishKind.Power: return Anchors.PowerBar;
                case PetPunishKind.DrawPile: return Anchors.DrawPile;
                case PetPunishKind.DiscardPile: return Anchors.DiscardPile;
                default: return Anchors.BoardCentre;
            }
        }

        // ================================================================== the first version

        /// <summary>
        /// THE FURY AS IT FIRST SHIPPED, for the lab's comparison only: a blank stare, the eyes
        /// change, the body is pressed down, a burst with a red puff, one bite at the plates, a look
        /// round. No second skin, no growl, nothing on the screen - which is what was wrong with it.
        /// </summary>
        private IEnumerator FuryLegacy(TamagotchiFuryVisuals fury)
        {
            SetState(PetViewState.FuriousTransition);
            SetParticleBudget(16);
            ClearDrag();
            float k = Tuning.FuryTransitionDuration / 0.92f;
            act.Presence = 1f;
            Beat("realization");
            int missing = FirstPending();
            act.LookAt = plates.Count > 0 ? PlateWorld(missing) : (Vector3)Anchors.HandCentre;
            act.Emotion = PetEmotion.Neutral;
            act.Mouth = "closed";
            act.MouthScale = 0.75f;
            act.Lid = 0f;
            act.Blink = 0f;
            act.EarWobble = 0f;
            yield return Wait(0.1f);
            act.Still = true;
            yield return Wait(0.1f * k);
            Beat("eye change");
            yield return Tween(0.08f * k, t =>
            {
                float e = Smooth(t);
                act.Emotion = PetEmotion.Furious;
                act.EmotionWeight = e;
                act.Mouth = "frown";
                act.CatchMul = Mathf.Lerp(1f, 0.6f, e);
                act.IrisMul = Mathf.Lerp(1f, 0.92f, e);
                act.Cheek = Color.Lerp(TamagotchiArt.CheekRose, TamagotchiArt.CheekBerry, e);
                act.CheekA = Mathf.Lerp(0.55f, 0.85f, e);
            });
            Beat("compression");
            yield return Tween(0.15f * k, t =>
            {
                float e = Smooth(t);
                act.Squash = new Vector2(1f + 0.03f * e, 1f - 0.06f * e);
                act.PawL = Paw(0.05f * e, -0.04f * e, -30f * e, 1f - 0.08f * e);
                act.PawR = Paw(-0.05f * e, -0.04f * e, -30f * e, 1f - 0.08f * e);
                act.Ear = -14f * e;
            });
            Beat("burst");
            act.Still = false;
            Sound(PetSound.Furious);
            act.Mouth = "furious";
            PuffAura(TamagotchiArt.DeepRaspberry, 0.65f);
            vignetteBoost = 0.34f;
            FuryPuff(rig.BellyWorld + new Vector3(0f, 0.2f * S, 0f), 7);
            ShakePlates(0.32f);
            yield return Tween(0.18f * k, t =>
            {
                float s = t < 0.45f ? Mathf.Lerp(0.94f, 1.08f, EaseOut(t / 0.45f))
                    : Mathf.Lerp(1.08f, 1f, Smooth((t - 0.45f) / 0.55f));
                act.Squash = new Vector2(1f + (1f - s) * 0.6f, s);
                act.MouthScale = 1f + 0.2f * Bell(t);
                act.PawL = Paw(-0.02f, 0.06f * Bell(t), 40f * Bell(t));
                act.PawR = Paw(0.02f, 0.06f * Bell(t), 40f * Bell(t));
                act.EarWobble = -4f * Bell(t);
            });
            act.Squash = Vector2.one;
            Beat("request destruction");
            Vector3 plateAt = plates.Count > 0 ? PlateWorld(missing) : rig.MouthWorld;
            Vector2 dir = ((Vector2)(plateAt - rig.MouthWorld)).normalized;
            bool bitten = false;
            yield return Tween(0.15f * k, t =>
            {
                float lunge = t < 0.5f ? EaseIn(t / 0.5f) : 1f - Smooth((t - 0.5f) / 0.5f);
                act.Offset = dir * Px(14f) * lunge;
                act.Head = dir * 0.03f * lunge;
                act.Mouth = t < 0.45f ? "furious" : "chew_b";
                act.MouthScale = t < 0.45f ? 1.25f : 1f;
                if (t >= 0.45f && !bitten)
                {
                    bitten = true;
                    Sound(PetSound.Chomp);
                    BitePendingPlates();
                    BiteFlecks(Vector2.Lerp(rig.MouthWorld, plateAt, 0.6f), new Color(0.98f, 0.92f, 0.9f), 3);
                }
            });
            if (!bitten)
            {
                BitePendingPlates();
            }
            act.Offset = Vector2.zero;
            act.Head = Vector2.zero;
            Beat("settle");
            act.Mouth = "smug";
            act.Emotion = PetEmotion.Furious;
            act.EmotionWeight = 1f;
            Vector3[] looks = { Anchors.BoardCentre, Anchors.JokerBar, Anchors.DrawPile };
            for (int i = 0; i < looks.Length; i++)
            {
                act.LookAt = looks[i];
                HeadToward(looks[i], 0.8f);
                yield return Wait(0.11f * k);
            }
            Vector3 target = PunishLookTarget(fury.PunishKind);
            act.LookAt = target;
            HeadToward(target, 1f);
            act.Emotion = PetEmotion.Smug;
            act.EmotionWeight = 0.55f;
            act.Lick = 0.5f;
            yield return Wait(0.22f * k);
            act.Lick = 0f;
        }
    }
}
