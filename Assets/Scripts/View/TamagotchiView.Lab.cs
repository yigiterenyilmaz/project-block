// PURPOSE: What the ANIMATION LAB (F3) needs from the pet to show any one part of it on demand -
// without a second copy of a single beat. The lab drives the very routines the game does and only
// fabricates their arguments (GameUiController.TamagotchiLab); this file gives it the handles:
//
//   - BEAT ISOLATION. Every long sequence marks its beats (Beat("grab"), Beat("lunge")...). The lab
//     can ask for one beat ALONE: everything before it runs at forty times speed (the body arrives
//     where that beat starts, which is the only honest way to start a beat mid-sequence) and the
//     sequence stops after it. A retimed beat shows its new timing here for free.
//   - SHOW NOW: the pet out of its nest with its plates up, no entrance (most scenes start there).
//   - LIFE MASKS: breathing, bob, sway and blinks switched off one at a time, and a still pose,
//     so "weight shift" and "breathing" can each be seen alone.
//   - THE CORRECTIVE PASS: the first fury and the first, fast punishes (LabLegacy) for a side by
//     side with the new ones; the fury's parts one at a time (LabFuryParts); the hatred field held
//     at a level with its layers switched off one by one; a line said on demand; a move to a named
//     place by a named style.
// Nothing here is used by the game.

using System.Collections;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        /// <summary>Beat isolation: run fast until this beat starts...</summary>
        public string LabFastUntil;

        /// <summary>... and stop the sequence once this one has played.</summary>
        public string LabStopAfter;

        private bool labFast;
        private float labFastSince;

        /// <summary>The lab's life masks. All true in the game.</summary>
        public bool LabBreath = true;
        public bool LabBob = true;
        public bool LabSway = true;
        public bool LabBlinks = true;
        public bool LabStill;

        /// <summary>THE OLD LOOK, for the lab's side-by-sides: the first fury (no second skin, no
        /// growl, nothing on the screen) and the first, fast punishes. The game never sets it.</summary>
        public bool LabLegacy;

        /// <summary>A sequence is entering the named beat.</summary>
        private void Beat(string name)
        {
            CurrentBeat = name;
            if (labFast && LabFastUntil == name)
            {
                labFast = false;
            }
        }

        /// <summary>The beat the sequence is in (debug, the lab's status line).</summary>
        public string CurrentBeat { get; private set; }

        /// <summary>The end of an isolated beat: hold what it ended on for a moment, then let go of
        /// whatever it was holding.</summary>
        private IEnumerator LabEnd(TamagotchiFoodProxy food)
        {
            LabStopAfter = null;
            yield return Wait(0.45f);
            if (food != null)
            {
                DropFood(food);
            }
        }

        /// <summary>True when the lab asked the sequence to end after this beat.</summary>
        private bool StopAfter(string name)
        {
            return !string.IsNullOrEmpty(LabStopAfter) && LabStopAfter == name;
        }

        /// <summary>Plays only <paramref name="beat"/> of whatever sequence is started next
        /// (null = the whole of it).</summary>
        public void LabIsolate(string beat)
        {
            LabFastUntil = beat;
            LabStopAfter = beat;
            labFast = !string.IsNullOrEmpty(beat);
            labFastSince = Time.unscaledTime;
        }

        /// <summary>Plays the next sequence up to and including <paramref name="beat"/>.</summary>
        public void LabUpTo(string beat)
        {
            LabFastUntil = null;
            LabStopAfter = beat;
            labFast = false;
        }

        public void LabClear()
        {
            LabFastUntil = null;
            LabStopAfter = null;
            labFast = false;
            LabBreath = LabBob = LabSway = LabBlinks = true;
            LabStill = false;
            LabLegacy = false;
            LabNoHatred = false;
            LabNoSpeech = false;
            LabHatredHold = -1f;
            LabFuryParts = FuryParts.All;
            LabNoRoam = false;
            IdlesEnabled = true;
            PlaybackRate = 1f;
        }

        /// <summary>The pet out, plates up, no entrance - where most lab scenes start.</summary>
        public void LabShowNow(TamagotchiRoundVisualState state)
        {
            queue.Clear();
            act.Clear();
            ClearFoodAndFlights();
            State = state ?? new TamagotchiRoundVisualState();
            shown = true;
            exiting = false;
            rig.SetVisible(true);
            ResetLife(State.Seed);
            presence = 1f;
            RebuildPlates();
            RevealPlatesNow();
            nextIdleAt = clock + 1.5f;
            // a scene starts clean: the skin it should be wearing, no field, no bubble, at home
            furyNow = State.Furious && !LabLegacy ? 1f : 0f;
            HideHatred();
            HideSpeech();
            lastBubbleAt = -100f;
            DropMove();
            platesPlaced = false;
            ResetRoaming();
            punishesThisFury = 0;
        }

        /// <summary>The pressure front alone, from where the pet stands.</summary>
        public void LabHatredWave()
        {
            StartHatredWave();
        }

        /// <summary>The impact alone: the edge's spike and the arena's one impulse.</summary>
        public void LabHatredImpact()
        {
            HatredImpact();
        }

        /// <summary>A line out of its mouth, now (a scripted bubble).</summary>
        public void LabSpeak(string tr, string en, float hold)
        {
            Speak(tr, en, true, hold);
        }

        /// <summary>Counts the next punish as a repeat (the 86% pace) or as the first again.</summary>
        public void LabSetPunishCount(int played)
        {
            punishesThisFury = Mathf.Max(0, played);
        }

        /// <summary>The pet in from nothing, with its entrance.</summary>
        public void LabEnter(TamagotchiRoundVisualState state)
        {
            HideNow();
            Sync(state);
        }

        /// <summary>Changes what the pet knows (a stage, the progress) without rebuilding it.</summary>
        public void LabSetState(TamagotchiRoundVisualState state)
        {
            State = state ?? State;
        }

        /// <summary>Marks a plate fed, as the meal does (the lab's "slot completion" alone).</summary>
        public void LabCollapsePlate(int slotId)
        {
            CollapsePlate(slotId);
        }

        /// <summary>One of the small lab-only beats that are not idles: a single blink.</summary>
        public void LabBlink()
        {
            Enqueue(PetPresentationPriority.Request, "lab: blink", BlinkOnce, true, 0f, null);
        }

        private IEnumerator BlinkOnce()
        {
            yield return Tween(0.12f, t => act.Blink = BlinkCurve(t * 0.12f));
            act.Blink = 0f;
            yield return Wait(0.3f);
        }

        /// <summary>The post-attack moments on their own.</summary>
        public void LabPostAttack(ProjectBlock.Core.PetPunishKind kind)
        {
            ProjectBlock.Core.PetPunishKind k = kind;
            Enqueue(PetPresentationPriority.Request, "lab: post attack", () => PostAttack(k), true, 0f, null);
        }

        /// <summary>A smug hold (the "smug idle").</summary>
        public void LabSmug(float seconds)
        {
            float s = seconds;
            Enqueue(PetPresentationPriority.Request, "lab: smug", () => SmugHold(s), true, 0f, null);
        }

        private IEnumerator SmugHold(float seconds)
        {
            act.Emotion = PetEmotion.Smug;
            act.LookAt = Anchors.HandCentre;
            yield return Wait(seconds);
        }

        /// <summary>Holds the pet in a face for a while (the hunger scenes' static looks).</summary>
        public void LabFace(PetEmotion emotion, float seconds)
        {
            PetEmotion e = emotion;
            float s = seconds;
            Enqueue(PetPresentationPriority.Request, "lab: face " + emotion, () => FaceHold(e, s), true, 0f, null);
        }

        private IEnumerator FaceHold(PetEmotion emotion, float seconds)
        {
            act.Emotion = emotion;
            yield return Wait(seconds);
        }

        /// <summary>The idle scheduler's next pick, played now.</summary>
        public void LabNextIdle()
        {
            IdleKind? kind = PickIdle();
            if (kind.HasValue)
            {
                PlayIdle(kind.Value);
            }
        }

        public bool LabBusy
        {
            get { return queue.Busy; }
        }
    }
}
