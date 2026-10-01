// PURPOSE: "Tamagotchi"'s voice and its eating - one cue per audio hook the brief names
// (TamagotchiView.Sounded raises them; PetSound is the list). The creature is CUTE, not a baby:
// soft chirps and squeaks built from short pitch glides with a little vibrato and two quiet
// harmonics, never a voice. Eating is STYLIZED: a soft chomp (a low "thock" under a short crunch of
// band noise), a nom (a closed-mouth glide), a tiny rounded gulp (a pitch-dropping bloop). Furious
// it is the SAME creature: the chirp goes higher and raspy. The board bite is a low chunky crunch,
// a joker or power a cardboard crunch with a glint of magic in it, a pile snack rapid little paper
// snaps.
//
// SPAM CONTROL. A five-card snack is not five full chomps: the first is the hero, the rest are the
// LIGHT takes at a lower level, and the meal ends on one gulp (the view chooses which). The same
// cue inside 35 ms is dropped here as well. Every cue plays through the pooled sources
// (PlayPolished), so a re-pitched chirp never bends one still ringing.
//
// Built on first use, like the rest of the late cues. The haptic beats the brief lists (a small
// happy tap on a feed, a medium pulse for the fury and the board bite, a sharp tap for a lost
// joker, one aggregated pulse for a pile meal) are announced by the view (TamagotchiView.Haptics)
// and have no layer to go to yet - the game has no haptics.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private readonly Dictionary<PetSound, AudioClip> petClips = new Dictionary<PetSound, AudioClip>();
        private readonly Dictionary<PetSound, float> petLastPlayed = new Dictionary<PetSound, float>();

        /// <summary>Plays one of the pet's cues.</summary>
        public void Tamagotchi(PetSound cue)
        {
            float last;
            if (petLastPlayed.TryGetValue(cue, out last) && Time.unscaledTime - last < 0.035f)
            {
                return;
            }
            petLastPlayed[cue] = Time.unscaledTime;
            AudioClip clip;
            if (!petClips.TryGetValue(cue, out clip) || clip == null)
            {
                clip = BuildPet(cue);
                petClips[cue] = clip;
            }
            if (clip == null)
            {
                return;
            }
            float volume;
            float detune;
            switch (cue)
            {
                case PetSound.ChompLight:
                case PetSound.PileSnackLight:
                    volume = 0.38f; detune = 1.2f; break;
                case PetSound.Chew:
                    volume = 0.42f; detune = 1.0f; break;
                case PetSound.Idle:
                case PetSound.Tap:
                    volume = 0.32f; detune = 0.8f; break;
                case PetSound.BoardBite:
                case PetSound.Furious:
                    volume = 0.85f; detune = 0.3f; break;
                default:
                    volume = 0.6f; detune = 0.6f; break;
            }
            if (pool != null)
            {
                PlayPolished(clip, Detune(detune), volume);
            }
            else
            {
                PlayWithPitch(clip, 0.98f, 1.02f, volume);
            }
        }

        private static AudioClip BuildPet(PetSound cue)
        {
            var rng = new System.Random(52000 + (int)cue);
            float[] b;
            switch (cue)
            {
                case PetSound.Enter:
                    // "pip-pip!" and a soft pop as it lands
                    b = Buffer(0.42f);
                    PetChirp(b, 0.00f, 0.09f, 880f, 1320f, 0.32f, 18f, 0.02f, 0f);
                    PetChirp(b, 0.11f, 0.11f, 990f, 1560f, 0.30f, 20f, 0.02f, 0.06f);
                    Thump(b, 0.22f, 0.35f, 220f, 120f, 0.03f);
                    PNoise(b, 0.22f, 0.04f, 0.1f, 0.001f, 0.012f, 600f, 2400f, rng);
                    Room(b, 0.16f, 0.45f);
                    return PFinish("petEnter", b, 0.55f);
                case PetSound.Request:
                    // two soft plate pops, 70 ms apart
                    b = Buffer(0.34f);
                    Marimba(b, 0.00f, 783.99f, 0.32f, 0.06f);
                    Marimba(b, 0.07f, 987.77f, 0.30f, 0.06f);
                    PNoise(b, 0.0f, 0.02f, 0.06f, 0.0005f, 0.006f, 1500f, 5000f, rng);
                    PNoise(b, 0.07f, 0.02f, 0.06f, 0.0005f, 0.006f, 1500f, 5000f, rng);
                    Room(b, 0.15f, 0.4f);
                    return PFinish("petRequest", b, 0.45f);
                case PetSound.Idle:
                    b = Buffer(0.18f);
                    PetChirp(b, 0f, 0.12f, 1480f, 1180f, 0.26f, 22f, 0.025f, 0.05f);
                    Room(b, 0.12f, 0.35f);
                    return PFinish("petIdle", b, 0.35f);
                case PetSound.NoticeDraggedCard:
                    // "hm?" - a curious rising chirp with a bend
                    b = Buffer(0.22f);
                    PetChirp(b, 0f, 0.16f, 720f, 1150f, 0.3f, 14f, 0.02f, 0.08f);
                    Room(b, 0.12f, 0.35f);
                    return PFinish("petNotice", b, 0.42f);
                case PetSound.ValidFoodNear:
                    // an excited little trill
                    b = Buffer(0.3f);
                    for (int i = 0; i < 4; i++)
                    {
                        PetChirp(b, i * 0.055f, 0.05f, 1050f + 90f * i, 1350f + 120f * i, 0.24f, 0f, 0f, 0f);
                    }
                    Room(b, 0.12f, 0.35f);
                    return PFinish("petValidNear", b, 0.45f);
                case PetSound.WrongFoodNear:
                    // "mm-mm": two low closed-mouth glides
                    b = Buffer(0.36f);
                    PetHum(b, 0f, 0.13f, 420f, 380f, 0.3f);
                    PetHum(b, 0.17f, 0.15f, 400f, 330f, 0.3f);
                    Room(b, 0.1f, 0.3f);
                    return PFinish("petWrongNear", b, 0.4f);
                case PetSound.GrabFood:
                    b = Buffer(0.14f);
                    PNoise(b, 0f, 0.06f, 0.22f, 0.002f, 0.02f, 400f, 2200f, rng);
                    Thump(b, 0f, 0.2f, 260f, 160f, 0.02f);
                    return PFinish("petGrab", b, 0.35f);
                case PetSound.Chomp:
                case PetSound.ChompLight:
                    // a soft chomp: a low thock under a short crunch, a tiny click on top
                    bool light = cue == PetSound.ChompLight;
                    b = Buffer(0.2f);
                    Thump(b, 0f, light ? 0.3f : 0.45f, 210f, 95f, 0.025f);
                    PNoise(b, 0.004f, 0.06f, light ? 0.18f : 0.3f, 0.001f, 0.015f, 900f, 3800f, rng);
                    PNoise(b, 0.018f, 0.04f, 0.12f, 0.001f, 0.01f, 1600f, 5200f, rng);
                    ResonantClick(b, 0f, 2600f, 2100f, 10f, 0.12f, 0.0008f, rng);
                    HighPass(b, 60f);
                    Room(b, 0.08f, 0.25f);
                    return PFinish(light ? "petChompLight" : "petChomp", b, light ? 0.4f : 0.6f);
                case PetSound.Chew:
                    // "nom": a closed-mouth glide with a soft squish
                    b = Buffer(0.14f);
                    PetHum(b, 0f, 0.1f, 330f, 270f, 0.32f);
                    PNoise(b, 0.01f, 0.05f, 0.06f, 0.004f, 0.02f, 300f, 1400f, rng);
                    return PFinish("petChew", b, 0.4f);
                case PetSound.Gulp:
                case PetSound.GulpBig:
                    // a tiny rounded gulp: a bloop dropping in pitch, a bubble behind it
                    bool big = cue == PetSound.GulpBig;
                    b = Buffer(big ? 0.3f : 0.2f);
                    PSine(b, 0f, big ? 0.16f : 0.11f, big ? 420f : 520f, big ? 140f : 190f, 0.04f, 0.42f, 0.003f, big ? 0.06f : 0.04f);
                    PSine(b, big ? 0.07f : 0.05f, 0.05f, 700f, 900f, 0.02f, 0.08f, 0.002f, 0.015f);
                    Room(b, 0.1f, 0.3f);
                    return PFinish(big ? "petGulpBig" : "petGulp", b, big ? 0.55f : 0.45f);
                case PetSound.Satisfied:
                    // a happy coo, and a breath of sparkle
                    b = Buffer(0.5f);
                    PetChirp(b, 0f, 0.28f, 700f, 820f, 0.28f, 7f, 0.03f, 0.18f);
                    Marimba(b, 0.05f, 1046.5f, 0.14f, 0.1f);
                    Sparkle(b, 0.08f, 0.2f, 4, 0.04f, rng);
                    Room(b, 0.2f, 0.5f);
                    return PFinish("petSatisfied", b, 0.45f);
                case PetSound.Impatient:
                    // a huff: a puff of breath and a short low grunt
                    b = Buffer(0.3f);
                    PNoise(b, 0f, 0.16f, 0.22f, 0.01f, 0.06f, 300f, 2400f, rng);
                    PetHum(b, 0.02f, 0.12f, 300f, 250f, 0.22f);
                    Room(b, 0.08f, 0.3f);
                    return PFinish("petImpatient", b, 0.45f);
                case PetSound.Furious:
                    // a short furious squeal - higher, raspy, still the same small creature
                    b = Buffer(0.55f);
                    PetChirp(b, 0f, 0.36f, 980f, 1250f, 0.4f, 34f, 0.05f, 0.35f);
                    PetRasp(b, 0f, 0.36f, 0.22f, rng);
                    Thump(b, 0f, 0.25f, 160f, 80f, 0.05f);
                    Saturate(b, 1.6f);
                    HighPass(b, 90f);
                    Room(b, 0.16f, 0.5f);
                    return PFinish("petFurious", b, 0.75f);
                case PetSound.BoardBite:
                    // a low chunky crunch and the chomp: the ground of the board coming away
                    b = Buffer(0.5f);
                    Thump(b, 0f, 0.8f, 110f, 42f, 0.07f);
                    for (int i = 0; i < 4; i++)
                    {
                        PNoise(b, 0.006f + i * 0.028f, 0.05f, 0.32f - i * 0.05f, 0.001f, 0.018f, 140f, 1500f, rng);
                    }
                    PNoise(b, 0.0f, 0.08f, 0.16f, 0.001f, 0.02f, 1500f, 4200f, rng);
                    ResonantClick(b, 0f, 900f, 520f, 8f, 0.25f, 0.002f, rng);
                    Saturate(b, 1.3f);
                    HighPass(b, 32f);
                    Room(b, 0.14f, 0.55f);
                    return PFinish("petBoardBite", b, 0.85f);
                case PetSound.AssetSnatch:
                    // "thwip" - the tongue or the paw shooting out - then a sticky tap
                    b = Buffer(0.24f);
                    PNoiseSweep(b, 0f, 0.1f, 0.25f, 0.002f, 0.04f, 600f, 1800f, 7000f, rng);
                    PSine(b, 0f, 0.08f, 500f, 1400f, 0.03f, 0.12f, 0.001f, 0.03f);
                    ResonantClick(b, 0.1f, 1800f, 1500f, 12f, 0.16f, 0.001f, rng);
                    Room(b, 0.1f, 0.3f);
                    return PFinish("petSnatch", b, 0.45f);
                case PetSound.AssetBite:
                    // cardboard and a glint of magic: a papery crunch with a sparkle in it
                    b = Buffer(0.3f);
                    Thump(b, 0f, 0.35f, 190f, 90f, 0.03f);
                    PNoise(b, 0.003f, 0.08f, 0.3f, 0.001f, 0.02f, 700f, 3200f, rng);
                    PNoise(b, 0.02f, 0.05f, 0.14f, 0.001f, 0.012f, 2500f, 6500f, rng);
                    Sparkle(b, 0.02f, 0.12f, 5, 0.06f, rng);
                    PSine(b, 0.01f, 0.18f, 1760f, 1975f, 0.05f, 0.05f, 0.002f, 0.06f);
                    Room(b, 0.12f, 0.4f);
                    return PFinish("petAssetBite", b, 0.55f);
                case PetSound.PileSnack:
                case PetSound.PileSnackLight:
                    // a rapid little paper snap
                    bool soft = cue == PetSound.PileSnackLight;
                    b = Buffer(0.1f);
                    PNoise(b, 0f, 0.03f, soft ? 0.2f : 0.32f, 0.0005f, 0.008f, 2000f, 7500f, rng);
                    ResonantClick(b, 0f, 3200f, 2600f, 9f, soft ? 0.1f : 0.16f, 0.0006f, rng);
                    Thump(b, 0f, soft ? 0.12f : 0.22f, 240f, 150f, 0.015f);
                    return PFinish(soft ? "petSnackLight" : "petSnack", b, soft ? 0.35f : 0.5f);
                case PetSound.Smug:
                    // "heh": two short low-mid chirps
                    b = Buffer(0.26f);
                    PetChirp(b, 0f, 0.07f, 600f, 540f, 0.24f, 0f, 0f, 0f);
                    PetChirp(b, 0.1f, 0.08f, 640f, 520f, 0.22f, 0f, 0f, 0f);
                    Room(b, 0.1f, 0.3f);
                    return PFinish("petSmug", b, 0.38f);
                case PetSound.Tap:
                    b = Buffer(0.08f);
                    ResonantClick(b, 0f, 1300f, 1100f, 14f, 0.22f, 0.001f, rng);
                    PNoise(b, 0f, 0.015f, 0.08f, 0.0005f, 0.004f, 800f, 3000f, rng);
                    return PFinish("petTap", b, 0.3f);
                case PetSound.Stomp:
                    // a soft low thud - never a shake
                    b = Buffer(0.2f);
                    Thump(b, 0f, 0.5f, 140f, 60f, 0.04f);
                    PNoise(b, 0f, 0.05f, 0.08f, 0.001f, 0.02f, 80f, 600f, rng);
                    return PFinish("petStomp", b, 0.45f);
                default:
                    return null;
            }
        }

        /// <summary>The creature's voice: a sine gliding f0 -> f1 (eased), bent up through its
        /// middle by <paramref name="bend"/>, with vibrato and two quiet harmonics - small and
        /// round, never formant-shaped enough to read as a baby talking.</summary>
        private static void PetChirp(float[] b, float atSec, float seconds, float f0, float f1, float amp,
            float vibHz, float vibDepth, float bend)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min((int)(seconds * SampleRate), b.Length - start);
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / seconds;
                float glide = u * u * (3f - 2f * u);
                float f = Mathf.Lerp(f0, f1, glide) * (1f + bend * Mathf.Sin(Mathf.PI * u))
                    * (1f + vibDepth * Mathf.Sin(2f * Mathf.PI * vibHz * t));
                phase += 2.0 * Mathf.PI * f / SampleRate;
                float env = Mathf.Clamp01(t / 0.008f) * Mathf.Pow(1f - u, 1.3f);
                float s = Mathf.Sin((float)phase) + 0.26f * Mathf.Sin((float)(2.0 * phase))
                    + 0.07f * Mathf.Sin((float)(3.0 * phase));
                b[start + i] += s * amp * env;
            }
        }

        /// <summary>A closed-mouth hum (the "nom", the "mm-mm"): a low glide, rounded off by a
        /// one-pole low-pass so it has no edge.</summary>
        private static void PetHum(float[] b, float atSec, float seconds, float f0, float f1, float amp)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min((int)(seconds * SampleRate), b.Length - start);
            double phase = 0.0;
            float lp = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 900f / SampleRate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / seconds;
                float f = Mathf.Lerp(f0, f1, u);
                phase += 2.0 * Mathf.PI * f / SampleRate;
                float s = Mathf.Sin((float)phase) + 0.5f * Mathf.Sin((float)(2.0 * phase)) + 0.3f * Mathf.Sin((float)(3.0 * phase));
                lp += (s - lp) * a;
                float env = Mathf.Clamp01(t / 0.012f) * Mathf.Clamp01((1f - u) / 0.3f);
                b[start + i] += lp * amp * env;
            }
        }

        /// <summary>The rasp of the furious squeal: band noise that flutters at a rough rate.</summary>
        private static void PetRasp(float[] b, float atSec, float seconds, float amp, System.Random rng)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min((int)(seconds * SampleRate), b.Length - start);
            float lp = 0f;
            float hp = 0f;
            float aLo = 1f - Mathf.Exp(-2f * Mathf.PI * 3800f / SampleRate);
            float aHi = 1f - Mathf.Exp(-2f * Mathf.PI * 900f / SampleRate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / seconds;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * aLo;
                hp += (lp - hp) * aHi;
                float band = lp - hp;
                float flutter = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 46f * t);
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Pow(1f - u, 1.4f);
                b[start + i] += band * amp * flutter * env;
            }
        }
    }
}
