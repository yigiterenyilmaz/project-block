// PURPOSE: "Tamagotchi" running out of patience - the HUNGER ESCALATION acted out (not a bar), the
// last-chance PREWARNING, and the FURIOUS TRANSITION, the boss's second big payoff.
//
// HUNGER is Core's: TamagotchiBoss.Stage and HungerProgress, reported when they move
// (TamagotchiHungerChange). The mood face, the idle set, the plates' tint, the aura and the
// patience ring all follow it on their own (.Idle, .Plates); a stage CHANGE also gets a short beat
// so the player sees the moment it happened - a belly pat and a lick going hungry, crossed arms and
// a stomp going impatient, a hard head snap at the plates going angry.
//
// THE PREWARNING reads TamagotchiBoss.DeadlineNext - the rules' own "the next draw is the deadline"
// - never a guess off the progress: the pet ducks behind its edge, and 200-350 ms later two eyes
// come up over it glowing dark pink. Then it comes back with the glow still in its eyes.
//
// THE FURY, ~0.8-1.15 s: it looks at the plate it never got and stares, blank, for 100 ms of
// nothing; the catchlights shrink, the pupils go small, the brows come down and the cheeks go from
// rose to deep raspberry; the body is pressed down to 0.94 with the paws clenched and the ears
// stiff; then it BURSTS (0.94 -> 1.08 -> 1) with a short dark raspberry puff, the mouth wide, a
// squeal, the plates shaking - one fast bite at the plates cracks them and they drop away - and it
// looks at the board, the jokers, the deck, and ends on what it is about to take (the planner has
// already chosen it). Local only: a dark-magenta vignette round the pet, no fire, no lightning, no
// camera, the board as readable as ever. Same creature, a completely different energy.
// EXTENSION POINT: the beat lengths come from Tuning (start values 0.10/0.15/0.18/0.15/0.22).

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
            switch (stage)
            {
                case PetHungerStage.Hungry:
                    yield return IdleBellyPat(1, false);
                    yield return IdleLick();
                    yield return IdleRequestGlance(false);
                    break;
                case PetHungerStage.Impatient:
                    Say("Sabırsızlanıyor...", "It is getting impatient...");
                    Sound(PetSound.Impatient);
                    yield return IdleStomp();
                    yield return IdleEdgeTap(2);
                    break;
                default:
                    Say("\"Bana onu ver.\"", "\"Give me that.\"");
                    yield return IdleRequestGlance(true);
                    act.LookAt = Anchors.HandCentre;
                    act.Emotion = PetEmotion.Angry;
                    yield return Wait(0.2f);
                    if (plates.Count > 0)
                    {
                        act.LookAt = PlateWorld(FirstPending());
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
            Say("Çok az kaldı: bir kart daha çekilirse çıldıracak.",
                "Almost out of time: one more draw and it snaps.");
            // gone behind the edge...
            yield return Tween(0.2f, t => act.Presence = Mathf.Lerp(1f, 0f, EaseIn(t)));
            act.Presence = 0f;
            yield return Wait(0.28f);
            // ... and then two eyes over it, glowing dark pink
            act.Emotion = PetEmotion.Angry;
            act.Glow = 1f;
            act.LookAt = Anchors.HandCentre;
            yield return Tween(0.18f, t => act.Presence = Mathf.Lerp(0f, PresenceShowing(0.56f), EaseOut(t)));
            yield return Tween(0.85f, t =>
            {
                act.Glow = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 3f);
                act.LookAt = t < 0.5f ? (Vector3)Anchors.HandCentre : PlateWorld(FirstPending());
            });
            float from = PresenceShowing(0.56f);
            yield return Tween(0.22f, t =>
            {
                act.Presence = Mathf.Lerp(from, 1f, EaseOut(t));
                act.Glow = Mathf.Lerp(1f, 0.3f, t);
            });
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

        private IEnumerator FuryRoutine(TamagotchiFuryVisuals fury)
        {
            SetState(PetViewState.FuriousTransition);
            SetParticleBudget(16);
            ClearDrag();
            float k = Tuning.FuryTransitionDuration / 0.92f;
            act.Presence = 1f;

            // ---- A: REALIZATION - it looks at the plate it never got. Blank. Nothing for 100 ms.
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

            if (StopAfter("realization"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- B: the eyes change - catchlights shrink, pupils small, brows down, cheeks berry
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
            if (StopAfter("eye change"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- C: the body is pressed down, the paws clench, the ears go stiff
            Beat("compression");
            yield return Tween(0.15f * k, t =>
            {
                float e = Smooth(t);
                // the eyes keep what B gave them; the body takes the pressure
                act.Emotion = PetEmotion.Furious;
                act.EmotionWeight = 1f;
                act.CatchMul = 0.6f;
                act.IrisMul = 0.92f;
                act.Cheek = TamagotchiArt.CheekBerry;
                act.CheekA = 0.85f;
                act.Squash = new Vector2(1f + 0.03f * e, 1f - 0.06f * e);
                act.PawL = Paw(0.05f * e, -0.04f * e, -30f * e, 1f - 0.08f * e);
                act.PawR = Paw(-0.05f * e, -0.04f * e, -30f * e, 1f - 0.08f * e);
                act.Ear = -14f * e;
            });

            if (StopAfter("compression"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- D: THE BURST - 0.94 -> 1.08 -> 1, a dark raspberry puff, the mouth wide, a squeal
            Beat("burst");
            act.Still = false;
            Sound(PetSound.Furious);
            Haptic(PetHaptic.FuryPulse);
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

            if (StopAfter("burst"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- E: one fast bite at the plates - they crack and drop away
            Beat("request destruction");
            Vector3 plateAt = plates.Count > 0 ? PlateWorld(missing) : rig.MouthWorld;
            Vector2 dir = ((Vector2)(plateAt - rig.MouthWorld)).normalized;
            Say("Artık senden istemiyor - kendisi alacak.", "It is not asking any more - it will take it itself.");
            yield return Tween(0.15f * k, t =>
            {
                float lunge = t < 0.5f ? EaseIn(t / 0.5f) : 1f - Smooth((t - 0.5f) / 0.5f);
                act.Offset = dir * Px(14f) * lunge;
                act.Head = dir * 0.03f * lunge;
                act.Mouth = t < 0.45f ? "furious" : "chew_b";
                act.MouthScale = t < 0.45f ? 1.25f : 1f;
                if (t >= 0.45f && t - Dt / (0.15f * k) < 0.45f)
                {
                    Sound(PetSound.Chomp);
                    BitePendingPlates();
                    BiteFlecks(Vector2.Lerp(rig.MouthWorld, plateAt, 0.6f), new Color(0.98f, 0.92f, 0.9f), 3);
                }
            });
            if (!AnyPlateDropping())
            {
                BitePendingPlates();
            }
            act.Offset = Vector2.zero;
            act.Head = Vector2.zero;

            if (StopAfter("request destruction"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- F: SETTLE - the board, the jokers, the deck... and what it is going to take
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

        private bool AnyPlateDropping()
        {
            foreach (PlateView p in plates)
            {
                if (p.DropAt >= 0f)
                {
                    return true;
                }
            }
            return false;
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
    }
}
