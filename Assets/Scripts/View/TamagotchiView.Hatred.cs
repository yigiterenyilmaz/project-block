// PURPOSE: "Tamagotchi" FURIOUS - the hatred that leaves the character and gets into the WHOLE
// SCREEN, driven here and drawn by TamagotchiHatredField. The corrective brief's point is the reason
// this file exists: an angry face with a red tint is a face; the fury only reads as an EVENT when the
// game's own atmosphere goes with it.
//
// THREE LEVELS, ONE NUMBER EACH, and they are never a flash:
//   AMBIENT  what stays for as long as it is furious: a slightly heavier berry vignette (+0.06-0.10),
//            a background a few percent greyer and darker, the arena's shadow a little deeper. No
//            wave, no distortion - a screen that stays at its peak is a filter, not a mood.
//   SURGE    the transition's own: a soft irregular PRESSURE FRONT runs out from the pet across the
//            whole screen in a quarter of a second (StartHatredWave), the vignette, the veil and the
//            edge's magenta split rise behind it, hold for a breath and settle into the ambient.
//   IMPACT   one short spike on the fury's break (HatredImpact): the edge closes in for a fifth of a
//            second and the ARENA takes a single 2 px impulse - through the board's own transform
//            term (BoardImpulse), never the camera and never a random shake.
// Rarely, while it stands furious, the edge splits again for a moment (a micro pulse every 6-11 s).
//
// THE BACKGROUND IS NOT REPLACED. Its own controller is sent multipliers (HatredMood: brightness
// 0.94-0.97, saturation 0.90-0.95, a little plum, warmth out) by the game's controller, which also
// hands in what the debt's vignette already shows - the two share the screen's edges and the field
// takes the heavier of them, never the sum (TamagotchiHatredField.Apply).
// Cells, cubes and cards are never tinted: the field is UNDER the board's plate.
// EXTENSION POINT: another screen-wide answer is a term in TickHatred and a field in Frame.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        /// <summary>What the background is asked for while the pet is furious (multipliers; 1 = rest).</summary>
        public struct HatredMoodValues
        {
            public bool Active;
            public float Brightness;
            public float Saturation;
            public float Plum;
            public float Warmth;
            public float Vignette;
            public float Motes;
        }

        private TamagotchiHatredField hatred;
        private float hatredAmbient;
        private float hatredSurge;
        private float hatredSurgeHold;
        private float hatredWave = -1f;
        private float hatredImpact;
        private float hatredPulse;
        private float nextHatredPulseAt;
        private Vector2 hatredSource;
        private bool boardImpulseMine;

        /// <summary>The lab's switch: no screen response at all (the "old fury" comparison).</summary>
        public bool LabNoHatred;

        /// <summary>The lab holds the field at a level of its own (-1 = the game's).</summary>
        public float LabHatredHold = -1f;

        /// <summary>How strong the whole field is right now, 0..1 (debug).</summary>
        public float HatredLevel
        {
            get { return Mathf.Clamp01(Mathf.Max(hatredAmbient * 0.4f, hatredSurge)); }
        }

        /// <summary>How far the pressure front has run, 0..1 (-1 = no wave) (debug).</summary>
        public float HatredWaveProgress
        {
            get { return hatredWave; }
        }

        /// <summary>The perceptual opacity the berry vignette has at the far edge right now (debug).</summary>
        public float HatredVignetteNow { get; private set; }

        public float HatredChromaticNow { get; private set; }

        /// <summary>The multipliers for the background's own controller, this frame.</summary>
        public HatredMoodValues HatredMood
        {
            get
            {
                float a = Mathf.Clamp01(hatredAmbient);
                float s = Mathf.Clamp01(hatredSurge);
                if (!shown || LabNoHatred || (a < 0.004f && s < 0.004f))
                {
                    return new HatredMoodValues { Active = false, Brightness = 1f, Saturation = 1f, Plum = 1f, Warmth = 1f, Vignette = 1f, Motes = 1f };
                }
                // ambient: saturation -4.5%, brightness -2.5%; the surge takes it to -11% and -6%
                float sat = Mathf.Lerp(Tuning.HatredDesaturation * 0.4f * a, Tuning.HatredDesaturation, s);
                float dim = Mathf.Lerp(0.025f * a, 0.06f, s);
                return new HatredMoodValues
                {
                    Active = true,
                    Brightness = 1f - dim,
                    Saturation = 1f - sat,
                    Plum = 1f + 0.9f * Mathf.Max(a * 0.45f, s),
                    Warmth = 1f - 0.5f * Mathf.Max(a * 0.5f, s),
                    Vignette = 1f + 0.25f * Mathf.Max(a * 0.5f, s),
                    Motes = 1f - 0.6f * Mathf.Max(a * 0.5f, s)
                };
            }
        }

        private void BuildHatred()
        {
            hatred = TamagotchiHatredField.Create(transform);
        }

        /// <summary>The pressure front leaves the pet (the fury's phase E).</summary>
        private void StartHatredWave()
        {
            hatredWave = 0f;
            hatredSource = rig.BellyWorld;
            hatredSurgeHold = Tuning.HatredWaveDuration + 0.34f;
            Sound(PetSound.HatredWave);
        }

        /// <summary>The fury's break landing on the screen (phase G): a short spike at the edge and
        /// ONE 2 px impulse of the arena, away from the pet.</summary>
        private void HatredImpact()
        {
            hatredImpact = 1f;
            hatredSurgeHold = Mathf.Max(hatredSurgeHold, 0.3f);
            hatredSurge = Mathf.Max(hatredSurge, 0.85f);
            Vector2 away = Anchors.BoardCentre - (Vector2)rig.BellyWorld;
            StartCoroutine(ArenaImpulse(away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up, 2f, 0.2f));
        }

        /// <summary>One impulse of the arena: out and back, no ringing.</summary>
        private System.Collections.IEnumerator ArenaImpulse(Vector2 dir, float pixels, float seconds)
        {
            if (LabNoHatred)
            {
                yield break;
            }
            boardImpulseMine = true;
            float t = 0f;
            while (t < seconds)
            {
                t += Dt;
                float k = Mathf.Clamp01(t / seconds);
                // out in the first fifth, back over the rest
                float e = k < 0.2f ? EaseOut(k / 0.2f) : 1f - Smooth((k - 0.2f) / 0.8f);
                if (BoardImpulse != null)
                {
                    BoardImpulse(dir * Px(pixels) * e);
                }
                yield return null;
            }
            if (BoardImpulse != null)
            {
                BoardImpulse(Vector2.zero);
            }
            boardImpulseMine = false;
        }

        private void HideHatred()
        {
            hatredAmbient = 0f;
            hatredSurge = 0f;
            hatredSurgeHold = 0f;
            hatredWave = -1f;
            hatredImpact = 0f;
            hatredPulse = 0f;
            HatredVignetteNow = 0f;
            HatredChromaticNow = 0f;
            if (hatred != null)
            {
                hatred.Hide();
            }
            if (boardImpulseMine && BoardImpulse != null)
            {
                BoardImpulse(Vector2.zero);
            }
            boardImpulseMine = false;
        }

        private void TickHatred()
        {
            if (hatred == null)
            {
                return;
            }
            float dt = Dt;
            bool furious = State.Furious && furyNow > 0.5f && !exiting;
            hatredAmbient = Damp(hatredAmbient, furious ? 1f : 0f, furious ? 0.22f : 0.4f);

            // THE WAVE: a quarter of a second across the whole screen; the surge rises with it
            if (hatredWave >= 0f)
            {
                hatredWave += dt / Mathf.Max(0.05f, Tuning.HatredWaveDuration);
                hatredSurge = Mathf.Max(hatredSurge, Smooth(Mathf.Clamp01(hatredWave / 0.8f)));
                if (hatredWave >= 1.1f)
                {
                    hatredWave = -1f;
                }
            }
            // ... holds a breath at its peak, then settles into the ambient
            if (hatredSurgeHold > 0f)
            {
                hatredSurgeHold -= dt;
            }
            else
            {
                hatredSurge = Damp(hatredSurge, 0f, 0.28f);
            }
            hatredImpact = Mathf.MoveTowards(hatredImpact, 0f, dt / 0.22f);

            // the rare micro pulse of a screen still under strain
            if (furious && queue.Current == null && clock >= nextHatredPulseAt)
            {
                if (nextHatredPulseAt > 0f)
                {
                    hatredPulse = 1f;
                }
                nextHatredPulseAt = clock + Rand(6f, 11f);
            }
            hatredPulse = Mathf.MoveTowards(hatredPulse, 0f, dt / 0.32f);

            float ambient = hatredAmbient;
            float surge = hatredSurge;
            if (LabHatredHold >= 0f)
            {
                ambient = 0f;
                surge = LabHatredHold;
            }
            if (LabNoHatred || !shown)
            {
                hatred.Hide();
                HatredVignetteNow = 0f;
                HatredChromaticNow = 0f;
                return;
            }
            float peak = Tuning.HatredVignette;
            float vignette = Mathf.Lerp(Tuning.HatredAmbientVignette * ambient, peak, surge) + 0.07f * hatredImpact;
            float desat = Mathf.Lerp(Tuning.HatredDesaturation * 0.4f * ambient, Tuning.HatredDesaturation, surge);
            float chroma = Mathf.Clamp01(Mathf.Max(Mathf.Max(surge, hatredImpact * 0.8f), hatredPulse * 0.4f));
            float shadow = Mathf.Lerp(0.16f * ambient, 0.34f, surge) + 0.08f * hatredImpact;
            HatredVignetteNow = vignette;
            HatredChromaticNow = chroma;
            if (hatredWave < 0f)
            {
                hatredSource = rig.BellyWorld;
            }
            var frame = new TamagotchiHatredField.Frame
            {
                Screen = Anchors.Screen,
                Board = Anchors.Board,
                Source = hatredSource,
                Strength = Mathf.Clamp01(Mathf.Max(ambient, surge) * 4f) * Mathf.Clamp01(pose.Alpha),
                Vignette = vignette,
                Desaturation = desat,
                Chromatic = chroma,
                ChromaticOffset = Px(Tuning.HatredChromaticOffset),
                Wave = hatredWave >= 0f ? hatredWave : 0f,
                BoardShadow = shadow,
                DebtVignette = Anchors.DebtVignette,
                Clock = clock
            };
            hatred.Apply(frame);
        }
    }
}
