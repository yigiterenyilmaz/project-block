// PURPOSE: "Tamagotchi"'s VOICE and everything it is heard to do - synthesized, layered and baked
// into buffers ONCE (SoundFx.Tamagotchi turns each into an AudioClip and plays it). The first pass
// was a set of sine glides with two harmonics: clean, cheap, and exactly the "beep / squeak / chomp"
// the corrective brief threw out. What replaces it is built the way the creature would make it:
//
//   A VOICE IS A THROAT, NOT AN OSCILLATOR. Voice() is a soft glottal pulse train (every pulse a
//   little different in length and weight - jitter and shimmer - and, for a growl, every other one
//   weaker, which is the subharmonic a throat makes when it is pushed) sent through three formant
//   resonators. Small creature, short throat: the formants sit high, so a growl at ~100 Hz still
//   reads as THIS animal and never as a dog or a monster. Normal it is round and breathy ("mm?",
//   "nhm", a chirrup); furious the same throat is pushed: low, raspy, the noise in it opened and shut
//   by the pulses themselves (Rasp), which is what makes a rasp belong to a voice.
//   EATING IS MATERIAL. A crunch is a handful of GRAINS - short bursts of band noise at irregular
//   gaps, never one burst - over a soft jaw thump, with the snap of the thing bitten on top: a dry
//   card snap for a block card, a glassy partial for a joker or power (they are not paper), a dense
//   low tear for the board's own floor.
//   EVERY LAYER IS GAIN-STAGED ON ITS OWN. Each layer is synthesized into a buffer of its own and
//   mixed in at a stated peak (Mix), so a recipe says what it means ("crunch 0.6, snap 0.25") and no
//   sum of layers can clip; the whole is normalised once more at the end.
//   NOTHING REPEATS. The cues heard in runs (chomps, chews, snaps, taps) come in 3-5 VARIANTS with
//   their grains, pitches and a few milliseconds of lead moved by the variant's seed; SoundFx adds
//   +-3-6% pitch and +-5% level on top and never plays one variant twice running.
//
// A growl is returned as TWO STEMS (low body + sub, rasp + breath) so their balance is a live knob
// rather than a rebake. No UnityEngine in this file: it is plain arithmetic on float arrays, which is
// what lets it be rendered and measured outside the editor.
// EXTENSION POINT: a new cue is a PetSound, a case in Build and (if it repeats) a count in Variants.

using System;

namespace ProjectBlock.View
{
    /// <summary>
    /// The pet's audio hooks. The corrective brief's own names map onto these: OnTamagotchiPeek ->
    /// Peek, Move -> Move / MoveFurious, Request -> Request, NoticeFood -> NoticeDraggedCard,
    /// FeedGrab -> GrabFood, Chomp / Chew / Gulp, Happy -> Satisfied, Disappointed, GrowlStart,
    /// FuryBreak, HatredWave, FuriousIdle -> GrowlIdle / FuriousBreath / TeethClack, TargetAsset,
    /// GrabAsset, EatAsset -> AssetBite, BoardSuction, BoardBite, PileSnack, Smug (Grunt when furious).
    /// </summary>
    public enum PetSound
    {
        Enter, Request, Idle, NoticeDraggedCard, ValidFoodNear, WrongFoodNear, GrabFood, Chomp,
        ChompLight, Chew, Gulp, GulpBig, Satisfied, Impatient, Furious, BoardBite, AssetSnatch, AssetBite,
        PileSnack, PileSnackLight, Smug, Tap, Stomp,
        // the corrective pass
        Peek, Move, Hide, MoveFurious, Hungry, Disappointed, GrowlStart, GrowlIdle, FuryBreak, HatredWave,
        FuryImpact, FuriousBreath, TeethClack, TargetAsset, GrabAsset, AssetStrain, AssetPull, ChompFurious,
        ChewFurious, BoardSuction, BoardGrind, GulpDeep, PileHero, Grunt, Bubble, BubbleFurious
    }

    public static class TamagotchiVoice
    {
        public const int Rate = 44100;

        /// <summary>Which knob a stem answers to (SoundFx.PetAudio).</summary>
        public enum StemKind
        {
            Mix,
            GrowlLow,
            GrowlRasp
        }

        public sealed class Stem
        {
            public string Name;
            public StemKind Kind;
            public float[] Samples;
        }

        public sealed class Take
        {
            public Stem[] Stems;
            public string[] Layers;
        }

        private struct Formant
        {
            public float Hz;
            public float Q;
            public float Gain;

            public Formant(float hz, float q, float gain)
            {
                Hz = hz;
                Q = q;
                Gain = gain;
            }
        }

        // The throat's shapes. Small creature: everything sits high.
        private static readonly Formant[] Uh = { new Formant(780f, 5f, 1f), new Formant(1750f, 7f, 0.62f), new Formant(3000f, 8f, 0.3f) };
        private static readonly Formant[] Ee = { new Formant(560f, 6f, 1f), new Formant(2300f, 8f, 0.42f), new Formant(3300f, 9f, 0.2f) };
        private static readonly Formant[] Mm = { new Formant(360f, 4f, 1f), new Formant(1100f, 5f, 0.22f), new Formant(2400f, 6f, 0.06f) };
        private static readonly Formant[] Ah = { new Formant(950f, 4f, 1f), new Formant(1550f, 5f, 0.7f), new Formant(2900f, 7f, 0.35f) };
        private static readonly Formant[] Rr = { new Formant(430f, 3f, 1f), new Formant(1150f, 4f, 0.55f), new Formant(2400f, 5f, 0.28f) };

        /// <summary>How many variants a cue has (the ones heard in runs get several).</summary>
        public static int Variants(PetSound cue)
        {
            switch (cue)
            {
                case PetSound.Chomp:
                case PetSound.ChompLight:
                case PetSound.Chew:
                case PetSound.ChompFurious:
                case PetSound.PileSnackLight:
                    return 4;
                case PetSound.ChewFurious:
                case PetSound.PileSnack:
                case PetSound.BoardBite:
                case PetSound.BoardGrind:
                case PetSound.AssetBite:
                case PetSound.AssetStrain:
                case PetSound.Idle:
                case PetSound.Move:
                case PetSound.Tap:
                case PetSound.GrowlIdle:
                    return 3;
                case PetSound.Hungry:
                case PetSound.NoticeDraggedCard:
                case PetSound.FuriousBreath:
                case PetSound.TeethClack:
                case PetSound.Grunt:
                case PetSound.Bubble:
                case PetSound.BubbleFurious:
                case PetSound.Smug:
                case PetSound.Peek:
                    return 2;
                default:
                    return 1;
            }
        }

        // ================================================================== the recipes

        public static Take Build(PetSound cue, int variant)
        {
            var rng = new Random(52000 + (int)cue * 97 + variant * 7919);
            // a few percent of shape per variant, and a few milliseconds of lead
            float v = Variants(cue) > 1 ? (float)(rng.NextDouble() * 2.0 - 1.0) : 0f;
            float lead = Variants(cue) > 1 ? (float)rng.NextDouble() * 0.008f : 0f;
            float k = 1f + 0.06f * v;
            float[] b;
            float[] l;
            switch (cue)
            {
                case PetSound.Enter:
                {
                    // "pi-yup!" and the soft pop of landing
                    b = Buf(0.46f);
                    l = Buf(0.46f);
                    Voice(l, 0.00f, 0.085f, u => Lerp(620f, 840f, Smooth(u)), u => Env(u, 0.12f, 0.45f), Ee, 0.48f, 0.01f, 0.05f, 0f, 0.12f, 0.25f, rng);
                    Voice(l, 0.105f, 0.12f, u => Lerp(760f, 600f, u) * (1f + 0.07f * Sin01(u)), u => Env(u, 0.1f, 0.5f), Uh, 0.48f, 0.01f, 0.05f, 0f, 0.12f, 0.25f, rng);
                    Mix(b, l, 0.6f);
                    l = Buf(0.46f);
                    Tone(l, 0.225f, 0.12f, 250f, 150f, 0.02f, 1f, 0.002f, 0.03f);
                    Noise(l, 0.225f, 0.035f, 500f, 2200f, 2200f, u => (1f - u) * (1f - u), 0.25f, rng);
                    Mix(b, l, 0.4f);
                    Room(b, 0.12f, 0.4f);
                    return One(cue, b, 0.6f, "chirp pair (throat, ee > uh)", "landing pop (body + air)");
                }
                case PetSound.Peek:
                {
                    // a small breath and a peep
                    b = Buf(0.2f);
                    l = Buf(0.2f);
                    Noise(l, lead, 0.08f, 1400f, 4200f, 5200f, u => Bell(u), 1f, rng);
                    Mix(b, l, 0.16f);
                    l = Buf(0.2f);
                    Voice(l, lead + 0.035f, 0.085f, u => Lerp(760f * k, 900f * k, Smooth(u)), u => Env(u, 0.2f, 0.5f), Ee, 0.5f, 0.01f, 0.05f, 0f, 0.2f, 0.2f, rng);
                    Mix(b, l, 0.42f);
                    return One(cue, b, 0.42f, "breath (airy inhale)", "peep (throat, ee)");
                }
                case PetSound.Idle:
                {
                    b = Buf(0.2f);
                    l = Buf(0.2f);
                    Voice(l, lead, 0.12f, u => Lerp(700f * k, 610f * k, u) * (1f + 0.05f * Sin01(u)), u => Env(u, 0.12f, 0.55f), Uh, 0.5f, 0.012f, 0.06f, 0f, 0.15f, 0.25f, rng);
                    Mix(b, l, 0.4f);
                    l = Buf(0.2f);
                    Noise(l, lead, 0.05f, 900f, 3600f, 3600f, u => (1f - u), 1f, rng);
                    Mix(b, l, 0.07f);
                    return One(cue, b, 0.4f, "chirp (throat, uh)", "breath puff");
                }
                case PetSound.Hungry:
                {
                    // "mm?": a rounded throat sound going up, a tiny airy inhale before it
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    Noise(l, lead, 0.07f, 1800f, 5200f, 6000f, u => Bell(u) * u, 1f, rng);
                    Mix(b, l, 0.1f);
                    l = Buf(0.3f);
                    Voice(l, lead + 0.05f, 0.17f, u => Lerp(400f * k, 560f * k, u * u), u => Env(u, 0.15f, 0.4f), Mm, 0.62f, 0.012f, 0.06f, 0f, 0.1f, 0.4f, rng);
                    Mix(b, l, 0.5f);
                    l = Buf(0.3f);
                    Tone(l, lead + 0.05f, 0.05f, 300f, 240f, 0.02f, 1f, 0.003f, 0.02f);
                    Mix(b, l, 0.1f);
                    return One(cue, b, 0.48f, "inhale (air)", "\"mm?\" (closed throat, rising)", "rubber body resonance");
                }
                case PetSound.NoticeDraggedCard:
                {
                    // "hm?" - curious, rising, with a bend
                    b = Buf(0.26f);
                    l = Buf(0.26f);
                    Voice(l, lead, 0.16f, u => Lerp(470f * k, 720f * k, Smooth(u)) * (1f + 0.06f * Sin01(u)), u => Env(u, 0.12f, 0.4f), Uh, 0.48f, 0.012f, 0.05f, 0f, 0.14f, 0.3f, rng);
                    Mix(b, l, 0.5f);
                    l = Buf(0.26f);
                    Noise(l, lead, 0.04f, 1200f, 4000f, 4000f, u => 1f - u, 1f, rng);
                    Mix(b, l, 0.06f);
                    return One(cue, b, 0.46f, "\"hm?\" (throat, rising)", "breath");
                }
                case PetSound.ValidFoodNear:
                {
                    // an excited little chirrup: three quick rising notes
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    for (int i = 0; i < 3; i++)
                    {
                        float f = 640f + 95f * i;
                        Voice(l, i * 0.062f, 0.055f, u => Lerp(f, f * 1.2f, u), u => Env(u, 0.15f, 0.5f), Ee, 0.48f, 0.01f, 0.05f, 0f, 0.12f, 0.25f, rng);
                    }
                    Mix(b, l, 0.5f);
                    Room(b, 0.1f, 0.35f);
                    return One(cue, b, 0.46f, "chirrup x3 (throat, ee)");
                }
                case PetSound.WrongFoodNear:
                {
                    // "mm-mm": two closed-mouth notes going down
                    b = Buf(0.4f);
                    l = Buf(0.4f);
                    Voice(l, 0f, 0.13f, u => Lerp(410f, 370f, u), u => Env(u, 0.12f, 0.35f), Mm, 0.62f, 0.012f, 0.06f, 0f, 0.05f, 0.45f, rng);
                    Voice(l, 0.17f, 0.16f, u => Lerp(390f, 300f, u), u => Env(u, 0.1f, 0.4f), Mm, 0.62f, 0.012f, 0.06f, 0f, 0.05f, 0.45f, rng);
                    LowPass(l, 1700f);
                    Mix(b, l, 0.5f);
                    return One(cue, b, 0.44f, "\"mm-mm\" (closed throat, falling)");
                }
                case PetSound.Request:
                {
                    // two soft plate pops, 70 ms apart
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    Tone(l, 0f, 0.09f, 340f, 250f, 0.02f, 1f, 0.002f, 0.028f);
                    Tone(l, 0.07f, 0.09f, 400f, 290f, 0.02f, 0.9f, 0.002f, 0.028f);
                    Mix(b, l, 0.45f);
                    l = Buf(0.3f);
                    Noise(l, 0f, 0.018f, 1500f, 5000f, 5000f, u => (1f - u) * (1f - u), 1f, rng);
                    Noise(l, 0.07f, 0.018f, 1500f, 5000f, 5000f, u => (1f - u) * (1f - u), 0.9f, rng);
                    Mix(b, l, 0.12f);
                    Room(b, 0.12f, 0.4f);
                    return One(cue, b, 0.46f, "plate pops (soft body)", "air ticks");
                }
                case PetSound.GrabFood:
                {
                    b = Buf(0.14f);
                    l = Buf(0.14f);
                    Noise(l, 0f, 0.05f, 450f, 2200f, 1600f, u => (1f - u) * (1f - u), 1f, rng);
                    Mix(b, l, 0.3f);
                    l = Buf(0.14f);
                    Tone(l, 0f, 0.06f, 300f, 200f, 0.016f, 1f, 0.002f, 0.014f);
                    Mix(b, l, 0.18f);
                    HighPass(b, 100f);
                    return One(cue, b, 0.36f, "paw pat (soft noise)", "body thud");
                }
                case PetSound.Chomp:
                case PetSound.ChompLight:
                {
                    bool light = cue == PetSound.ChompLight;
                    b = Buf(0.22f);
                    // A: the soft bite transient
                    l = Buf(0.22f);
                    Noise(l, lead, 0.006f, 1800f, 5500f, 5500f, u => 1f - u, 1f, rng);
                    Mix(b, l, light ? 0.2f : 0.3f);
                    // B: a very short low-mid crunch - grains, not one burst
                    l = Buf(0.22f);
                    Grains(l, lead + 0.003f, light ? 0.028f : 0.042f, light ? 3 : 5, 450f * k, 1900f * k, 0.011f, 0.72f, rng);
                    Mix(b, l, light ? 0.4f : 0.6f);
                    // C: the card's own snap
                    l = Buf(0.22f);
                    Click(l, lead + 0.004f, 2300f * k, 1700f * k, 9f, 0.02f, rng);
                    Mix(b, l, light ? 0.16f : 0.22f);
                    // D: mouth / body resonance
                    l = Buf(0.22f);
                    Tone(l, lead, 0.08f, 230f * k, 135f, 0.02f, 1f, 0.0015f, 0.017f);
                    Mix(b, l, light ? 0.14f : 0.26f);
                    HighPass(b, 90f);
                    Room(b, 0.07f, 0.25f);
                    return One(cue, b, light ? 0.42f : 0.62f, "bite transient", "low-mid crunch (grains)", "card snap", "mouth / body resonance");
                }
                case PetSound.Chew:
                {
                    // a closed mouth working: two or three soft grains, a hum under them
                    b = Buf(0.16f);
                    l = Buf(0.16f);
                    Grains(l, lead, 0.05f, 2 + (variant % 2), 260f * k, 1050f * k, 0.014f, 0.8f, rng);
                    Mix(b, l, 0.4f);
                    l = Buf(0.16f);
                    Voice(l, lead, 0.075f, u => Lerp(315f * k, 268f * k, u), u => Env(u, 0.2f, 0.5f), Mm, 0.62f, 0.02f, 0.1f, 0f, 0.05f, 0.5f, rng);
                    LowPass(l, 1300f);
                    Mix(b, l, 0.22f);
                    l = Buf(0.16f);
                    Noise(l, lead + 0.01f, 0.05f, 300f, 1200f, 800f, u => Bell(u), 1f, rng);
                    Mix(b, l, 0.1f);
                    return One(cue, b, 0.42f, "soft crunch (grains)", "closed-mouth hum", "squish");
                }
                case PetSound.Gulp:
                case PetSound.GulpBig:
                case PetSound.GulpDeep:
                {
                    bool big = cue != PetSound.Gulp;
                    bool deep = cue == PetSound.GulpDeep;
                    float len = deep ? 0.36f : big ? 0.3f : 0.22f;
                    b = Buf(len);
                    // a soft body tone dropping in pitch
                    l = Buf(len);
                    Tone(l, 0f, deep ? 0.2f : big ? 0.16f : 0.11f, deep ? 310f : big ? 420f : 500f, deep ? 92f : big ? 135f : 175f,
                        deep ? 0.06f : 0.04f, 1f, 0.004f, deep ? 0.075f : big ? 0.06f : 0.04f);
                    LowPass(l, deep ? 900f : 1400f);
                    Mix(b, l, 0.5f);
                    // the small belly thump behind it
                    l = Buf(len);
                    Tone(l, deep ? 0.09f : big ? 0.075f : 0.055f, 0.1f, deep ? 105f : 125f, deep ? 58f : 78f, 0.03f, 1f, 0.003f, deep ? 0.045f : 0.03f);
                    Mix(b, l, deep ? 0.3f : big ? 0.24f : 0.18f);
                    if (deep)
                    {
                        // a furious throat does not swallow cleanly
                        l = Buf(len);
                        Rasp(l, 0f, 0.14f, 500f, 1900f, 62f, 0.8f, u => Bell(u), 1f, rng);
                        Mix(b, l, 0.12f);
                    }
                    HighPass(b, 70f);
                    return One(cue, b, deep ? 0.6f : big ? 0.55f : 0.46f, "falling body tone", "belly thump", deep ? "throat rasp" : null);
                }
                case PetSound.Satisfied:
                {
                    // a soft happy chirrup and a tiny body pop. No coin, no bell.
                    b = Buf(0.36f);
                    l = Buf(0.36f);
                    Voice(l, 0f, 0.075f, u => Lerp(640f, 830f, Smooth(u)), u => Env(u, 0.15f, 0.45f), Ee, 0.5f, 0.01f, 0.05f, 0f, 0.14f, 0.25f, rng);
                    Voice(l, 0.085f, 0.15f, u => Lerp(790f, 700f, u) * (1f + 0.04f * (float)Math.Sin(u * 38f)), u => Env(u, 0.1f, 0.6f), Ee, 0.5f, 0.01f, 0.05f, 0f, 0.14f, 0.25f, rng);
                    Mix(b, l, 0.5f);
                    l = Buf(0.36f);
                    Tone(l, 0f, 0.06f, 270f, 175f, 0.02f, 1f, 0.002f, 0.02f);
                    Mix(b, l, 0.16f);
                    Room(b, 0.14f, 0.45f);
                    return One(cue, b, 0.48f, "happy chirrup (throat, ee)", "body pop");
                }
                case PetSound.Impatient:
                {
                    // a huff: a puff of breath over a short low grumble
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    Noise(l, 0f, 0.16f, 320f, 2400f, 1400f, u => Bell(u) * (1f - 0.5f * u), 1f, rng);
                    Mix(b, l, 0.34f);
                    l = Buf(0.3f);
                    Voice(l, 0.015f, 0.13f, u => Lerp(300f, 240f, u), u => Env(u, 0.15f, 0.5f), Mm, 0.58f, 0.03f, 0.12f, 0.1f, 0.05f, 0.4f, rng);
                    Mix(b, l, 0.34f);
                    return One(cue, b, 0.46f, "huff (breath)", "low grumble (closed throat)");
                }
                case PetSound.Smug:
                {
                    // "heh-heh": two short breathy notes
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    Voice(l, lead, 0.065f, u => Lerp(530f * k, 470f * k, u), u => Env(u, 0.15f, 0.5f), Uh, 0.48f, 0.015f, 0.08f, 0f, 0.3f, 0.25f, rng);
                    Voice(l, lead + 0.1f, 0.08f, u => Lerp(560f * k, 440f * k, u), u => Env(u, 0.15f, 0.55f), Uh, 0.48f, 0.015f, 0.08f, 0f, 0.3f, 0.25f, rng);
                    Mix(b, l, 0.44f);
                    return One(cue, b, 0.4f, "\"heh-heh\" (breathy throat)");
                }
                case PetSound.Tap:
                {
                    b = Buf(0.09f);
                    l = Buf(0.09f);
                    Click(l, lead, 1300f * k, 1080f * k, 12f, 0.012f, rng);
                    Mix(b, l, 0.3f);
                    l = Buf(0.09f);
                    Tone(l, lead, 0.04f, 360f, 260f, 0.01f, 1f, 0.001f, 0.012f);
                    Mix(b, l, 0.14f);
                    return One(cue, b, 0.32f, "claw tick", "pad thud");
                }
                case PetSound.Stomp:
                {
                    b = Buf(0.22f);
                    l = Buf(0.22f);
                    Tone(l, 0f, 0.16f, 145f, 62f, 0.03f, 1f, 0.002f, 0.04f);
                    Mix(b, l, 0.5f);
                    l = Buf(0.22f);
                    Noise(l, 0f, 0.05f, 90f, 700f, 500f, u => (1f - u) * (1f - u), 1f, rng);
                    Mix(b, l, 0.14f);
                    HighPass(b, 40f);
                    return One(cue, b, 0.46f, "low thud", "floor noise");
                }
                case PetSound.Move:
                {
                    // tiny soft foot / body taps
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    for (int i = 0; i < 3; i++)
                    {
                        float at = lead + i * (0.07f + 0.012f * v) + 0.006f * (float)rng.NextDouble();
                        Tone(l, at, 0.05f, 320f + 30f * i, 210f, 0.012f, 1f - 0.12f * i, 0.0015f, 0.011f);
                    }
                    Mix(b, l, 0.26f);
                    l = Buf(0.3f);
                    for (int i = 0; i < 3; i++)
                    {
                        Noise(l, lead + i * (0.07f + 0.012f * v), 0.022f, 600f, 2600f, 2000f, u => (1f - u) * (1f - u), 1f, rng);
                    }
                    Mix(b, l, 0.2f);
                    HighPass(b, 110f);
                    return One(cue, b, 0.36f, "foot taps (soft body)", "pad air");
                }
                case PetSound.Hide:
                {
                    // a soft cloth / rubber slide
                    b = Buf(0.26f);
                    l = Buf(0.26f);
                    Noise(l, 0f, 0.22f, 400f, 1900f, 700f, u => Bell(u) * (1f - 0.4f * u), 1f, rng);
                    Mix(b, l, 0.3f);
                    l = Buf(0.26f);
                    Tone(l, 0.02f, 0.16f, 300f, 180f, 0.09f, 1f, 0.03f, 0.06f);
                    Mix(b, l, 0.1f);
                    return One(cue, b, 0.32f, "cloth slide (falling band)", "rubber body");
                }
                case PetSound.MoveFurious:
                {
                    // a sharper scrape and a short growl - quiet
                    var body = Buf(0.36f);
                    l = Buf(0.36f);
                    Noise(l, 0f, 0.17f, 700f, 3800f, 2200f, u => Bell(u), 1f, rng);
                    Grit(l, 0f, 0.17f, 95f, 0.6f, rng);
                    Mix(body, l, 0.4f);
                    var low = Buf(0.36f);
                    Growl(low, 0.05f, 0.28f, 142f, 124f, 0.5f, rng);
                    var rasp = Buf(0.36f);
                    Rasp(rasp, 0.05f, 0.26f, 700f, 2500f, 138f, 0.85f, u => Env(u, 0.2f, 0.5f), 1f, rng);
                    return new Take
                    {
                        Stems = new[] { Finish("scrape", StemKind.Mix, body, 0.4f), Finish("growl low", StemKind.GrowlLow, low, 0.5f), Finish("growl rasp", StemKind.GrowlRasp, rasp, 0.3f) },
                        Layers = new[] { "scrape (gritty band noise)", "growl: low throat", "growl: rasp" }
                    };
                }
                case PetSound.Disappointed:
                {
                    // the breath going out of it: a falling closed note and a slow nasal exhale
                    b = Buf(0.36f);
                    l = Buf(0.36f);
                    Voice(l, 0f, 0.24f, u => Lerp(350f, 255f, Smooth(u)), u => Env(u, 0.12f, 0.6f), Mm, 0.6f, 0.02f, 0.1f, 0f, 0.05f, 0.45f, rng);
                    LowPass(l, 1300f);
                    Mix(b, l, 0.34f);
                    l = Buf(0.36f);
                    Noise(l, 0.03f, 0.3f, 400f, 1600f, 900f, u => Bell(u) * (1f - 0.5f * u), 1f, rng);
                    Mix(b, l, 0.14f);
                    return One(cue, b, 0.36f, "falling closed note", "slow exhale");
                }
                case PetSound.GrowlStart:
                case PetSound.GrowlIdle:
                {
                    bool idle = cue == PetSound.GrowlIdle;
                    float len = idle ? 0.42f : 0.74f;
                    float dur = idle ? 0.36f : 0.66f;
                    // LOW: the throat itself, and a subtle rumble under it
                    var low = Buf(len);
                    l = Buf(len);
                    Growl(l, lead, dur, 138f * k, idle ? 126f * k : 150f * k, idle ? 0.55f : 1f, rng);
                    Mix(low, l, 0.6f);
                    l = Buf(len);
                    Sine(l, lead, dur, 69f, u => Env(u, 0.4f, 0.3f) * (0.75f + 0.25f * (float)Math.Sin(u * 27f)));
                    Mix(low, l, idle ? 0.05f : 0.08f);
                    HighPass(low, 55f);
                    // RASP: the mid texture the pulses open and shut, and the breath it starts on
                    var rasp = Buf(len);
                    l = Buf(len);
                    Rasp(l, lead, dur, 700f, 2500f, 138f * k, 0.9f, u => idle ? Env(u, 0.3f, 0.45f) : Smooth(u / 0.5f) * Min(1f, (1f - u) / 0.25f), 1f, rng);
                    Mix(rasp, l, 0.5f);
                    l = Buf(len);
                    Noise(l, lead, 0.045f, 1500f, 5000f, 3000f, u => (1f - u), 1f, rng);
                    Mix(rasp, l, idle ? 0.08f : 0.16f);
                    return new Take
                    {
                        Stems = new[] { Finish("growl low", StemKind.GrowlLow, low, idle ? 0.5f : 0.7f), Finish("growl rasp", StemKind.GrowlRasp, rasp, idle ? 0.34f : 0.5f) },
                        Layers = new[] { "low throat (pushed pulse train, subharmonic)", "sub rumble (69 Hz, subtle)", "raspy mid (noise gated by the pulses)", "breath / snarl transient" }
                    };
                }
                case PetSound.Furious:
                case PetSound.FuryBreak:
                {
                    // the snarl: a raspy creature cry over its own low throat - never a scream
                    float len = 0.52f;
                    var low = Buf(len);
                    l = Buf(len);
                    Voice(l, 0f, 0.42f, u => Lerp(176f, 132f, u), u => Env(u, 0.05f, 0.5f), Rr, 0.4f, 0.07f, 0.3f, 0.42f, 0.1f, 0.12f, rng);
                    Mix(low, l, 0.6f);
                    HighPass(low, 70f);
                    var rasp = Buf(len);
                    l = Buf(len);
                    // up, over the top and down: a cry that breaks
                    Voice(l, 0f, 0.36f, u => (u < 0.3f ? Lerp(430f, 590f, Smooth(u / 0.3f)) : Lerp(590f, 320f, Smooth((u - 0.3f) / 0.7f))),
                        u => Env(u, 0.04f, 0.55f), Ah, 0.36f, 0.08f, 0.4f, 0.3f, 0.25f, 0.15f, rng);
                    Mix(rasp, l, 0.6f);
                    l = Buf(len);
                    Rasp(l, 0f, 0.36f, 1500f, 5200f, 66f, 0.9f, u => Env(u, 0.04f, 0.55f), 1f, rng);
                    Mix(rasp, l, 0.34f);
                    Saturate(rasp, 2.2f);
                    Room(rasp, 0.12f, 0.5f);
                    return new Take
                    {
                        Stems = new[] { Finish("snarl low", StemKind.GrowlLow, low, 0.62f), Finish("snarl cry", StemKind.GrowlRasp, rasp, 0.72f) },
                        Layers = new[] { "low throat layer", "raspy creature cry (pushed, breaking)", "rasp noise (gated)", "saturation" }
                    };
                }
                case PetSound.FuryImpact:
                {
                    // one short wide WHUMP and the growl's tail. No boom: it stops.
                    float len = 0.5f;
                    var body = Buf(len);
                    l = Buf(len);
                    Tone(l, 0f, 0.26f, 128f, 62f, 0.045f, 1f, 0.003f, 0.055f);
                    Mix(body, l, 0.56f);
                    l = Buf(len);
                    Noise(l, 0f, 0.08f, 180f, 1100f, 600f, u => (1f - u) * (1f - u), 1f, rng);
                    Mix(body, l, 0.42f);
                    l = Buf(len);
                    Noise(l, 0f, 0.012f, 900f, 3600f, 3600f, u => 1f - u, 1f, rng);
                    Mix(body, l, 0.14f);
                    Saturate(body, 1.5f);
                    HighPass(body, 52f);
                    Room(body, 0.14f, 0.6f);
                    var low = Buf(len);
                    Growl(low, 0.03f, 0.42f, 150f, 112f, 0.9f, rng);
                    for (int i = 0; i < low.Length; i++)
                    {
                        float u = i / (float)low.Length;
                        low[i] *= (1f - u) * (1f - u);
                    }
                    return new Take
                    {
                        Stems = new[] { Finish("whump", StemKind.Mix, body, 0.74f), Finish("growl tail", StemKind.GrowlLow, low, 0.5f) },
                        Layers = new[] { "low impact (128 > 62 Hz, short)", "mid pressure burst", "attack tick", "growl tail" }
                    };
                }
                case PetSound.HatredWave:
                {
                    // a pressure swell, a distorted mid sweep, a reverse suction that is cut off
                    float len = 0.56f;
                    b = Buf(len);
                    l = Buf(len);
                    Sine(l, 0f, 0.44f, 64f, u => (float)Math.Pow(Min(1f, u / 0.75f), 1.6) * Min(1f, (1f - u) / 0.12f));
                    Sine(l, 0f, 0.44f, 96.5f, u => 0.5f * (float)Math.Pow(Min(1f, u / 0.75f), 1.6) * Min(1f, (1f - u) / 0.12f));
                    Mix(b, l, 0.5f);
                    l = Buf(len);
                    SweepNoise(l, 0.02f, 0.46f, 260f, u => u < 0.6f ? Lerp(600f, 2300f, Smooth(u / 0.6f)) : Lerp(2300f, 800f, (u - 0.6f) / 0.4f), u => Bell(u) * Bell(u), rng);
                    Saturate(l, 3f);
                    Mix(b, l, 0.34f);
                    l = Buf(len);
                    SweepNoise(l, 0f, 0.32f, 900f, u => Lerp(1500f, 4200f, u), u => u * u * u, rng);
                    Mix(b, l, 0.2f);
                    HighPass(b, 38f);
                    Room(b, 0.16f, 0.7f);
                    return One(cue, b, 0.7f, "low pressure swell (64 + 96 Hz)", "distorted mid noise sweep", "reverse suction (cut)");
                }
                case PetSound.FuriousBreath:
                {
                    // two harsh breath cycles, in and out
                    b = Buf(0.98f);
                    l = Buf(0.98f);
                    for (int c = 0; c < 2; c++)
                    {
                        float at = lead + c * 0.46f;
                        Rasp(l, at, 0.17f, 800f, 2800f, 44f + 5f * v, 0.45f, u => Bell(u) * u, 0.8f, rng);
                        Rasp(l, at + 0.2f, 0.22f, 420f, 1900f, 38f + 5f * v, 0.55f, u => Bell(u) * (1f - 0.5f * u), 1f, rng);
                    }
                    Mix(b, l, 0.4f);
                    HighPass(b, 120f);
                    return One(cue, b, 0.36f, "breath in (rough, higher)", "breath out (rough, lower) x2");
                }
                case PetSound.TeethClack:
                {
                    b = Buf(0.16f);
                    l = Buf(0.16f);
                    Click(l, lead, 1950f * k, 1700f * k, 14f, 0.006f, rng);
                    Click(l, lead + 0.055f + 0.008f * v, 1750f * k, 1550f * k, 14f, 0.006f, rng);
                    Mix(b, l, 0.4f);
                    l = Buf(0.16f);
                    Tone(l, lead, 0.03f, 420f, 300f, 0.008f, 1f, 0.0008f, 0.008f);
                    Tone(l, lead + 0.055f + 0.008f * v, 0.03f, 400f, 290f, 0.008f, 0.8f, 0.0008f, 0.008f);
                    Mix(b, l, 0.16f);
                    return One(cue, b, 0.4f, "dry tooth clack x2", "jaw");
                }
                case PetSound.TargetAsset:
                {
                    // the lock: a dull, slightly sour sting over a short low growl. No beep.
                    float len = 0.42f;
                    var body = Buf(len);
                    l = Buf(len);
                    Pluck(l, 0f, 196f, 0.985f, 0.82f, rng);
                    Pluck(l, 0.012f, 207.65f, 0.985f, 0.82f, rng);
                    Mix(body, l, 0.4f);
                    l = Buf(len);
                    Noise(l, 0f, 0.02f, 800f, 3000f, 3000f, u => 1f - u, 1f, rng);
                    Mix(body, l, 0.08f);
                    HighPass(body, 110f);
                    Room(body, 0.14f, 0.5f);
                    var low = Buf(len);
                    Growl(low, 0.02f, 0.3f, 136f, 124f, 0.5f, rng);
                    var rasp = Buf(len);
                    Rasp(rasp, 0.02f, 0.28f, 700f, 2500f, 134f, 0.9f, u => Env(u, 0.25f, 0.5f), 1f, rng);
                    return new Take
                    {
                        Stems = new[] { Finish("sting", StemKind.Mix, body, 0.42f), Finish("growl low", StemKind.GrowlLow, low, 0.46f), Finish("growl rasp", StemKind.GrowlRasp, rasp, 0.26f) },
                        Layers = new[] { "target sting (two muted strings a semitone apart)", "low growl", "growl rasp" }
                    };
                }
                case PetSound.AssetSnatch:
                case PetSound.GrabAsset:
                {
                    // the paw / the tongue landing on it: a soft thud and a short rubbery squeak
                    b = Buf(0.2f);
                    l = Buf(0.2f);
                    Tone(l, 0f, 0.08f, 240f, 150f, 0.018f, 1f, 0.002f, 0.018f);
                    Mix(b, l, 0.28f);
                    l = Buf(0.2f);
                    SweepNoise(l, 0.004f, 0.04f, 1500f, u => Lerp(1900f, 2800f, u), u => Bell(u), rng);
                    Mix(b, l, 0.2f);
                    l = Buf(0.2f);
                    Noise(l, 0f, 0.035f, 500f, 2400f, 1600f, u => (1f - u) * (1f - u), 1f, rng);
                    Mix(b, l, 0.3f);
                    HighPass(b, 90f);
                    return One(cue, b, 0.46f, "contact thud", "rubber squeak", "pad noise");
                }
                case PetSound.AssetStrain:
                {
                    // the frame straining as it is tugged: a short creak (stick-slip into a resonance)
                    b = Buf(0.2f);
                    l = Buf(0.2f);
                    Creak(l, lead, 0.13f, 30f * k, 62f * k, 640f * k, 1480f * k, rng);
                    Mix(b, l, 0.42f);
                    l = Buf(0.2f);
                    Noise(l, lead, 0.1f, 700f, 2600f, 1800f, u => Bell(u), 1f, rng);
                    Grit(l, lead, 0.1f, 70f, 0.7f, rng);
                    Mix(b, l, 0.12f);
                    return One(cue, b, 0.4f, "frame creak (stick-slip)", "friction");
                }
                case PetSound.AssetPull:
                {
                    // dragged out of its slot: friction, and the faint glassy pull of the thing itself
                    b = Buf(0.4f);
                    l = Buf(0.4f);
                    SweepNoise(l, 0f, 0.32f, 500f, u => Lerp(1400f, 2800f, u), u => Bell(u), rng);
                    Grit(l, 0f, 0.32f, 55f, 0.45f, rng);
                    Mix(b, l, 0.3f);
                    l = Buf(0.4f);
                    foreach (float f in new[] { 1480f, 2217f, 2960f })
                    {
                        float ff = f;
                        Sine(l, 0.02f, 0.3f, ff, u => Bell(u) * Bell(u) * (ff > 2500f ? 0.4f : ff > 2000f ? 0.65f : 1f));
                    }
                    Mix(b, l, 0.12f);
                    Room(b, 0.12f, 0.5f);
                    return One(cue, b, 0.4f, "drag friction", "magical pull (glassy partials)");
                }
                case PetSound.ChompFurious:
                case PetSound.AssetBite:
                {
                    bool asset = cue == PetSound.AssetBite;
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    Noise(l, lead, 0.007f, 1500f, 5000f, 5000f, u => 1f - u, 1f, rng);
                    Mix(b, l, 0.3f);
                    // the deep crunch
                    l = Buf(0.3f);
                    Grains(l, lead + 0.003f, 0.058f, 6, 320f * k, 1600f * k, 0.013f, 0.7f, rng);
                    Mix(b, l, 0.62f);
                    l = Buf(0.3f);
                    Click(l, lead + 0.004f, (asset ? 1250f : 1500f) * k, (asset ? 820f : 1000f) * k, 7f, 0.03f, rng);
                    Mix(b, l, 0.26f);
                    l = Buf(0.3f);
                    Tone(l, lead, 0.11f, 190f * k, 100f, 0.025f, 1f, 0.0015f, 0.024f);
                    Mix(b, l, 0.32f);
                    Saturate(b, 1.5f);
                    if (asset)
                    {
                        // a joker or a power is not paper: a very small glassy snap in the bite
                        l = Buf(0.3f);
                        float[] partials = { 2900f, 4370f, 6200f };
                        for (int i = 0; i < partials.Length; i++)
                        {
                            Tone(l, lead + 0.005f, 0.2f, partials[i] * k, partials[i] * k, 1f, 1f - 0.25f * i, 0.0006f, 0.045f - 0.01f * i);
                        }
                        Mix(b, l, 0.13f);
                    }
                    HighPass(b, 75f);
                    Room(b, 0.1f, 0.35f);
                    return One(cue, b, asset ? 0.7f : 0.66f, "bite transient", "deep crunch (grains)", asset ? "frame crack" : "snap", "jaw / body", asset ? "glassy snap (magic)" : null);
                }
                case PetSound.ChewFurious:
                {
                    b = Buf(0.18f);
                    l = Buf(0.18f);
                    Grains(l, lead, 0.06f, 3, 200f * k, 900f * k, 0.016f, 0.8f, rng);
                    Mix(b, l, 0.46f);
                    l = Buf(0.18f);
                    Noise(l, lead + 0.008f, 0.06f, 200f, 800f, 500f, u => Bell(u), 1f, rng);
                    Mix(b, l, 0.14f);
                    l = Buf(0.18f);
                    Voice(l, lead, 0.08f, u => Lerp(150f * k, 128f * k, u), u => Env(u, 0.2f, 0.5f), Mm, 0.5f, 0.05f, 0.2f, 0.3f, 0.05f, 0.5f, rng);
                    LowPass(l, 1100f);
                    Mix(b, l, 0.2f);
                    HighPass(b, 60f);
                    return One(cue, b, 0.46f, "heavy crunch (grains)", "mouth", "throat");
                }
                case PetSound.BoardSuction:
                {
                    // the pull before the bite: rising, low, cut short
                    b = Buf(0.24f);
                    l = Buf(0.24f);
                    SweepNoise(l, 0f, 0.2f, 150f, u => Lerp(380f, 1150f, u), u => u * u, rng);
                    Mix(b, l, 0.4f);
                    l = Buf(0.24f);
                    Sine(l, 0f, 0.2f, 78f, u => u * u * Min(1f, (1f - u) / 0.08f));
                    Mix(b, l, 0.26f);
                    HighPass(b, 40f);
                    return One(cue, b, 0.44f, "low suction (rising band)", "pressure tone");
                }
                case PetSound.BoardBite:
                {
                    // the jaw snapping shut on dense material: a tear, never a stone explosion
                    b = Buf(0.56f);
                    // B: the mouth snap
                    l = Buf(0.56f);
                    Noise(l, lead, 0.008f, 1200f, 4500f, 4500f, u => 1f - u, 1f, rng);
                    Click(l, lead, 1100f * k, 700f * k, 6f, 0.02f, rng);
                    Mix(b, l, 0.34f);
                    // C: the board tearing - dense grains thinning out, and one deep crack
                    l = Buf(0.56f);
                    Grains(l, lead + 0.004f, 0.15f, 9, 230f * k, 1400f * k, 0.018f, 0.6f, rng);
                    Mix(b, l, 0.7f);
                    l = Buf(0.56f);
                    Click(l, lead + 0.012f, 620f * k, 340f * k, 6f, 0.06f, rng);
                    Mix(b, l, 0.4f);
                    l = Buf(0.56f);
                    Tone(l, lead, 0.2f, 130f, 66f, 0.04f, 1f, 0.002f, 0.042f);
                    Mix(b, l, 0.34f);
                    // small grit falling after it
                    l = Buf(0.56f);
                    Ticks(l, lead + 0.05f, 0.22f, 7, 3000f, 6200f, rng);
                    Mix(b, l, 0.1f);
                    Saturate(b, 1.4f);
                    HighPass(b, 58f);
                    Room(b, 0.12f, 0.55f);
                    return One(cue, b, 0.8f, "mouth snap", "board tear (dense grains)", "deep crack", "low body", "grit");
                }
                case PetSound.BoardGrind:
                {
                    // chunks of board between the teeth
                    b = Buf(0.26f);
                    l = Buf(0.26f);
                    Grains(l, lead, 0.1f, 4, 140f * k, 820f * k, 0.02f, 0.75f, rng);
                    Mix(b, l, 0.56f);
                    l = Buf(0.26f);
                    Ticks(l, lead + 0.01f, 0.12f, 4, 2800f, 6000f, rng);
                    Mix(b, l, 0.1f);
                    l = Buf(0.26f);
                    Tone(l, lead, 0.08f, 150f, 90f, 0.025f, 1f, 0.002f, 0.022f);
                    Mix(b, l, 0.22f);
                    Saturate(b, 1.3f);
                    HighPass(b, 65f);
                    return One(cue, b, 0.6f, "chunk grind (low grains)", "grit", "jaw");
                }
                case PetSound.PileSnack:
                {
                    // a whole card in one bite: the chomp with the card's snap on top
                    b = Buf(0.2f);
                    l = Buf(0.2f);
                    Noise(l, lead, 0.02f, 2000f, 7500f, 5000f, u => (1f - u) * (1f - u), 1f, rng);
                    Mix(b, l, 0.3f);
                    l = Buf(0.2f);
                    Click(l, lead, 3000f * k, 2400f * k, 9f, 0.012f, rng);
                    Mix(b, l, 0.2f);
                    l = Buf(0.2f);
                    Grains(l, lead + 0.003f, 0.03f, 3, 400f * k, 1600f * k, 0.01f, 0.75f, rng);
                    Mix(b, l, 0.4f);
                    l = Buf(0.2f);
                    Tone(l, lead, 0.07f, 235f, 145f, 0.018f, 1f, 0.0015f, 0.016f);
                    Mix(b, l, 0.22f);
                    HighPass(b, 90f);
                    return One(cue, b, 0.56f, "card snap (dry paper)", "snap click", "crunch (grains)", "jaw");
                }
                case PetSound.PileSnackLight:
                {
                    // the middle of a run: mouth snaps and card snaps taking turns
                    bool mouth = variant % 2 == 0;
                    b = Buf(0.12f);
                    if (mouth)
                    {
                        l = Buf(0.12f);
                        Tone(l, lead, 0.06f, 250f * k, 160f, 0.016f, 1f, 0.0015f, 0.013f);
                        Mix(b, l, 0.22f);
                        l = Buf(0.12f);
                        Grains(l, lead + 0.002f, 0.02f, 2, 380f * k, 1500f * k, 0.009f, 0.8f, rng);
                        Mix(b, l, 0.3f);
                    }
                    else
                    {
                        l = Buf(0.12f);
                        Noise(l, lead, 0.016f, 2200f, 7500f, 5200f, u => (1f - u) * (1f - u), 1f, rng);
                        Mix(b, l, 0.3f);
                        l = Buf(0.12f);
                        Click(l, lead, 3200f * k, 2600f * k, 9f, 0.01f, rng);
                        Mix(b, l, 0.2f);
                        l = Buf(0.12f);
                        Tone(l, lead, 0.05f, 250f, 170f, 0.014f, 1f, 0.0015f, 0.014f);
                        Mix(b, l, 0.14f);
                    }
                    HighPass(b, 70f);
                    return One(cue, b, 0.38f, mouth ? "mouth snap (jaw + small crunch)" : "card snap (paper + click)");
                }
                case PetSound.PileHero:
                {
                    // one valuable card coming off the pile: a low hum of wanting, a faint glint
                    b = Buf(0.44f);
                    l = Buf(0.44f);
                    Voice(l, 0f, 0.36f, u => Lerp(188f, 214f, u), u => Smooth(u / 0.7f) * Min(1f, (1f - u) / 0.2f), Mm, 0.6f, 0.02f, 0.1f, 0f, 0.05f, 0.5f, rng);
                    LowPass(l, 1200f);
                    Mix(b, l, 0.36f);
                    l = Buf(0.44f);
                    Sine(l, 0.08f, 0.3f, 2637f, u => Bell(u) * Bell(u));
                    Sine(l, 0.1f, 0.28f, 3951f, u => 0.5f * Bell(u) * Bell(u));
                    Mix(b, l, 0.07f);
                    Room(b, 0.14f, 0.5f);
                    return One(cue, b, 0.4f, "anticipation hum (closed throat, rising)", "glint (two soft partials)");
                }
                case PetSound.Grunt:
                {
                    // "hrm-hm": low, closed, pleased with itself - and unpleasant
                    float len = 0.36f;
                    var low = Buf(len);
                    l = Buf(len);
                    Voice(l, lead, 0.1f, u => Lerp(128f * k, 104f * k, u), u => Env(u, 0.15f, 0.45f), Mm, 0.48f, 0.05f, 0.25f, 0.35f, 0.04f, 0.5f, rng);
                    Voice(l, lead + 0.13f, 0.15f, u => Lerp(118f * k, 86f * k, u), u => Env(u, 0.12f, 0.55f), Mm, 0.48f, 0.05f, 0.25f, 0.4f, 0.04f, 0.5f, rng);
                    LowPass(l, 1500f);
                    Mix(low, l, 0.6f);
                    HighPass(low, 50f);
                    var rasp = Buf(len);
                    Rasp(rasp, lead, 0.1f, 700f, 2200f, 128f * k, 0.9f, u => Env(u, 0.15f, 0.45f), 1f, rng);
                    Rasp(rasp, lead + 0.13f, 0.15f, 700f, 2200f, 110f * k, 0.9f, u => Env(u, 0.12f, 0.55f), 1f, rng);
                    return new Take
                    {
                        Stems = new[] { Finish("grunt low", StemKind.GrowlLow, low, 0.56f), Finish("grunt rasp", StemKind.GrowlRasp, rasp, 0.2f) },
                        Layers = new[] { "low closed throat x2 (\"hrm-hm\")", "rasp" }
                    };
                }
                case PetSound.Bubble:
                {
                    // a speech bubble opening: a tiny soft mouth puff. Never a UI pop.
                    b = Buf(0.1f);
                    l = Buf(0.1f);
                    Noise(l, lead, 0.03f, 500f, 2600f, 1500f, u => (1f - u) * (1f - u), 1f, rng);
                    Mix(b, l, 0.22f);
                    l = Buf(0.1f);
                    Tone(l, lead, 0.04f, 430f * k, 300f * k, 0.012f, 1f, 0.002f, 0.013f);
                    Mix(b, l, 0.2f);
                    return One(cue, b, 0.26f, "mouth puff", "lip pop");
                }
                case PetSound.BubbleFurious:
                {
                    // a small dry "thk" and a low breath
                    b = Buf(0.2f);
                    l = Buf(0.2f);
                    Click(l, lead, 900f * k, 600f * k, 5f, 0.012f, rng);
                    Noise(l, lead, 0.008f, 1000f, 3000f, 3000f, u => 1f - u, 0.6f, rng);
                    Mix(b, l, 0.3f);
                    l = Buf(0.2f);
                    Rasp(l, lead + 0.01f, 0.14f, 300f, 1300f, 46f, 0.5f, u => Bell(u) * (1f - 0.5f * u), 1f, rng);
                    Mix(b, l, 0.14f);
                    return One(cue, b, 0.32f, "dry \"thk\"", "low breath");
                }
                default:
                    return null;
            }
        }

        private static Take One(PetSound cue, float[] b, float peak, params string[] layers)
        {
            int n = 0;
            foreach (string s in layers)
            {
                if (s != null)
                {
                    n++;
                }
            }
            var names = new string[n];
            n = 0;
            foreach (string s in layers)
            {
                if (s != null)
                {
                    names[n++] = s;
                }
            }
            return new Take { Stems = new[] { Finish(cue.ToString(), StemKind.Mix, b, peak) }, Layers = names };
        }

        // ================================================================== the throat

        /// <summary>
        /// A voiced sound: glottal pulses at <paramref name="hz"/> (a contour over 0..1), each one
        /// slightly off in length (<paramref name="jitter"/>) and weight (<paramref name="shimmer"/>),
        /// every other one weakened by <paramref name="sub"/> (the pushed throat's subharmonic),
        /// through three formants. <paramref name="open"/> is the share of a period the throat is
        /// open (smaller = harsher), <paramref name="breath"/> the air in it, <paramref name="dry"/>
        /// how much of the unshaped pulse is kept (the body of the note).
        /// </summary>
        private static void Voice(float[] b, float at, float dur, Func<float, float> hz, Func<float, float> env,
            Formant[] formants, float open, float jitter, float shimmer, float sub, float breath, float dry, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            if (n <= 0)
            {
                return;
            }
            var res = new Biquad[formants.Length];
            for (int i = 0; i < formants.Length; i++)
            {
                res[i] = Biquad.BandPass(formants[i].Hz, formants[i].Q);
            }
            float phase = 0f;
            float periodScale = 1f;
            float pulseAmp = 1f;
            int pulse = 0;
            float dc = 0f;
            float lp = 0f;
            float lpA = OnePole(1100f);
            float aspA = OnePole(2600f);
            float asp = 0f;
            float xPrev = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float f = hz(u);
                phase += f * periodScale / Rate;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    pulse++;
                    periodScale = 1f + jitter * ((float)rng.NextDouble() * 2f - 1f);
                    pulseAmp = 1f - shimmer * (float)rng.NextDouble();
                    if (pulse % 2 == 1)
                    {
                        pulseAmp *= 1f - sub;
                    }
                }
                float x = 0f;
                if (phase < open)
                {
                    float s = (float)Math.Sin(Math.PI * phase / open);
                    x = s * s * pulseAmp;
                }
                // the air: mostly while the throat is open
                float noise = (float)rng.NextDouble() * 2f - 1f;
                asp += (noise - asp) * aspA;
                float air = (noise - asp) * breath * (0.35f + 0.65f * x);
                dc += (x - dc) * 0.002f;
                // WHAT THE FORMANTS HEAR is the pulse's slope, not the pulse: a throat radiates the
                // change in flow, and that +6 dB an octave is the difference between a voice and a
                // soft whistle (the first render put 97% of a chirp's energy in its fundamental).
                float slope = (x - xPrev) * Rate / (6.2832f * Math.Max(40f, f));
                xPrev = x;
                float y = 0f;
                for (int k = 0; k < res.Length; k++)
                {
                    y += res[k].Step(slope + air) * formants[k].Gain;
                }
                lp += (x - dc - lp) * lpA;
                b[start + i] += (y * 1.6f + lp * dry) * env(u);
            }
        }

        /// <summary>The growl: the same throat pushed low - long open phase, heavy jitter, every
        /// other pulse dropped most of the way (the roughness sits at half the pitch).</summary>
        private static void Growl(float[] b, float at, float dur, float f0, float f1, float swell, Random rng)
        {
            Voice(b, at, dur, u => Lerp(f0, f1, u) * (1f + 0.03f * (float)Math.Sin(u * 19f)),
                u => (swell >= 0.99f ? Smooth(Min(1f, u / 0.45f)) : Min(1f, u / 0.2f)) * Min(1f, (1f - u) / 0.22f),
                Rr, 0.4f, 0.06f, 0.28f, 0.48f, 0.08f, 1.1f, rng);
        }

        /// <summary>Band noise opened and shut at a pulse rate - a rasp that belongs to a voice
        /// (depth 1) or a harsh breath (depth ~0.5).</summary>
        private static void Rasp(float[] b, float at, float dur, float lowHz, float highHz, float rateHz, float depth,
            Func<float, float> env, float amp, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            float hp = 0f, l1 = 0f, l2 = 0f;
            float hpA = OnePole(lowHz);
            float lpA = OnePole(highHz);
            float phase = 0f;
            float scale = 1f;
            float weight = 1f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                phase += rateHz * scale / Rate;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    scale = 1f + 0.09f * ((float)rng.NextDouble() * 2f - 1f);
                    weight = 0.6f + 0.4f * (float)rng.NextDouble();
                }
                float gate = phase < 0.5f ? (float)Math.Sin(Math.PI * phase / 0.5f) * weight : 0f;
                float x = (float)rng.NextDouble() * 2f - 1f;
                hp += (x - hp) * hpA;
                l1 += (x - hp - l1) * lpA;
                l2 += (l1 - l2) * lpA;
                b[start + i] += l2 * 1.8f * amp * (1f - depth + depth * gate) * env(u);
            }
        }

        // ================================================================== material

        /// <summary>A crunch: <paramref name="count"/> short bursts of band noise at irregular gaps
        /// across <paramref name="span"/>, each a little different in colour, thinning toward the end
        /// (<paramref name="keep"/> is what each keeps of the one before).</summary>
        private static void Grains(float[] b, float at, float span, int count, float lowHz, float highHz, float grainSec,
            float keep, Random rng)
        {
            float amp = 1f;
            for (int g = 0; g < count; g++)
            {
                float where = count <= 1 ? 0f : g / (float)(count - 1);
                // irregular: every grain is pushed off its even place
                float t = at + span * where * where * 0.4f + span * where * 0.6f + (g == 0 ? 0f : ((float)rng.NextDouble() - 0.5f) * span / count * 0.7f);
                float colour = 0.8f + 0.4f * (float)rng.NextDouble();
                float len = grainSec * (0.7f + 0.6f * (float)rng.NextDouble());
                Noise(b, Math.Max(0f, t), len, lowHz * colour, highHz * colour, highHz * colour * 0.6f, u => (1f - u) * (1f - u), amp, rng);
                amp *= keep * (0.85f + 0.3f * (float)rng.NextDouble());
            }
        }

        /// <summary>A few tiny bright ticks scattered over a span (grit falling).</summary>
        private static void Ticks(float[] b, float at, float span, int count, float lowHz, float highHz, Random rng)
        {
            for (int g = 0; g < count; g++)
            {
                float t = at + span * (float)rng.NextDouble();
                float fall = 1f - (t - at) / Math.Max(0.001f, span) * 0.6f;
                Noise(b, t, 0.004f + 0.004f * (float)rng.NextDouble(), lowHz, highHz, highHz, u => 1f - u, fall, rng);
            }
        }

        /// <summary>Stick-slip: pulses speeding up from r0 to r1 per second, ringing two resonances.</summary>
        private static void Creak(float[] b, float at, float dur, float r0, float r1, float f1, float f2, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            Biquad a = Biquad.BandPass(f1, 9f);
            Biquad c = Biquad.BandPass(f2, 11f);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                phase += Lerp(r0, r1, u * u) / Rate;
                float x = 0f;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    x = 0.6f + 0.4f * (float)rng.NextDouble();
                }
                float y = a.Step(x) + 0.6f * c.Step(x);
                b[start + i] += y * 6f * Bell(u);
            }
        }

        /// <summary>Roughens what is already in the buffer: its level stepped at random about
        /// <paramref name="rateHz"/> times a second (friction, a scrape).</summary>
        private static void Grit(float[] b, float at, float dur, float rateHz, float depth, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            int step = Math.Max(8, (int)(Rate / rateHz));
            float now = 1f, want = 1f;
            for (int i = 0; i < n; i++)
            {
                if (i % step == 0)
                {
                    want = 1f - depth * (float)rng.NextDouble();
                }
                now += (want - now) * 0.02f;
                b[start + i] *= now;
            }
        }

        /// <summary>A muted plucked string (Karplus-Strong).</summary>
        private static void Pluck(float[] b, float at, float hz, float feedback, float mute, Random rng)
        {
            int start = (int)(at * Rate);
            int period = Math.Max(2, (int)Math.Round(Rate / hz));
            var line = new float[period];
            float lp = 0f;
            for (int i = 0; i < period; i++)
            {
                float x = (float)rng.NextDouble() * 2f - 1f;
                lp += (x - lp) * 0.35f;
                line[i] = lp;
            }
            // a string holds no DC: left in, it circulates as a sub nobody asked for
            float mean = 0f;
            for (int i = 0; i < period; i++)
            {
                mean += line[i];
            }
            mean /= period;
            for (int i = 0; i < period; i++)
            {
                line[i] -= mean;
            }
            int n = Math.Min(b.Length - start, (int)(0.5f * Rate));
            int p = 0;
            float prev = 0f, tone = 0f;
            float a = 1f - Math.Min(1f, Math.Max(0f, mute)) * 0.85f;
            for (int i = 0; i < n; i++)
            {
                float cur = line[p];
                tone += (0.5f * (cur + prev) - tone) * a;
                prev = cur;
                line[p] = tone * feedback;
                p = (p + 1) % period;
                b[start + i] += cur * (i < 60 ? i / 60f : 1f);
            }
        }

        // ================================================================== primitives

        private static float[] Buf(float seconds)
        {
            return new float[(int)(Rate * seconds)];
        }

        private static float OnePole(float hz)
        {
            return 1f - (float)Math.Exp(-2.0 * Math.PI * hz / Rate);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * (t < 0f ? 0f : t > 1f ? 1f : t);
        }

        private static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        private static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        private static float Bell(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return (float)Math.Sin(t * Math.PI);
        }

        private static float Sin01(float t)
        {
            return (float)Math.Sin(t * Math.PI);
        }

        /// <summary>In over the first <paramref name="attack"/> of it, out over the last
        /// <paramref name="release"/>.</summary>
        private static float Env(float u, float attack, float release)
        {
            float a = attack <= 0f ? 1f : Min(1f, u / attack);
            float r = release <= 0f ? 1f : Min(1f, (1f - u) / release);
            return Math.Max(0f, a * r);
        }

        /// <summary>A sine falling (or rising) exponentially from f0 toward f1, under a linear
        /// attack and an exponential decay.</summary>
        private static void Tone(float[] b, float at, float dur, float f0, float f1, float pitchTau, float amp,
            float attack, float tau)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = f1 + (f0 - f1) * (float)Math.Exp(-t / pitchTau);
                phase += 2.0 * Math.PI * f / Rate;
                float e = t < attack ? t / attack : (float)Math.Exp(-(t - attack) / tau);
                // the last few ms always close, so a cut tone cannot click
                float close = Min(1f, (n - i) / (0.004f * Rate));
                b[start + i] += (float)Math.Sin(phase) * amp * e * close;
            }
        }

        /// <summary>A steady sine under an envelope over 0..1.</summary>
        private static void Sine(float[] b, float at, float dur, float hz, Func<float, float> env)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            double w = 2.0 * Math.PI * hz / Rate;
            for (int i = 0; i < n; i++)
            {
                b[start + i] += (float)Math.Sin(w * i) * env(i / (float)Math.Max(1, n));
            }
        }

        /// <summary>Band noise: a high-pass at lowHz, a two-pole low-pass sliding from high0 to
        /// high1, under an envelope over 0..1.</summary>
        private static void Noise(float[] b, float at, float dur, float lowHz, float high0, float high1,
            Func<float, float> env, float amp, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            float hp = 0f, l1 = 0f, l2 = 0f;
            float hpA = OnePole(lowHz);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                float x = (float)rng.NextDouble() * 2f - 1f;
                hp += (x - hp) * hpA;
                float a = OnePole(Lerp(high0, high1, u));
                l1 += (x - hp - l1) * a;
                l2 += (l1 - l2) * a;
                b[start + i] += l2 * 1.8f * amp * env(u);
            }
        }

        /// <summary>Band noise whose top follows a contour.</summary>
        private static void SweepNoise(float[] b, float at, float dur, float lowHz, Func<float, float> high,
            Func<float, float> env, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)(dur * Rate), b.Length - start);
            float hp = 0f, l1 = 0f, l2 = 0f;
            float hpA = OnePole(lowHz);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                float x = (float)rng.NextDouble() * 2f - 1f;
                hp += (x - hp) * hpA;
                float a = OnePole(high(u));
                l1 += (x - hp - l1) * a;
                l2 += (l1 - l2) * a;
                b[start + i] += l2 * 1.8f * env(u);
            }
        }

        /// <summary>A burst of noise into a resonance that glides from f0 to f1: a snap, a clack,
        /// a crack.</summary>
        private static void Click(float[] b, float at, float f0, float f1, float q, float ringSec, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min((int)((ringSec * 6f + 0.004f) * Rate), b.Length - start);
            int burst = (int)(0.0012f * Rate);
            Biquad r = Biquad.BandPass(f0, q);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                float x = i < burst ? ((float)rng.NextDouble() * 2f - 1f) * (1f - i / (float)burst) : 0f;
                if ((i & 31) == 0)
                {
                    r.Retune(Lerp(f0, f1, 1f - (1f - u) * (1f - u)), q);
                }
                float t = i / (float)Rate;
                b[start + i] += r.Step(x) * 5f * (float)Math.Exp(-t / Math.Max(0.002f, ringSec));
            }
        }

        private struct Biquad
        {
            private float b0, b2, a1, a2, x1, x2, y1, y2;

            /// <summary>A band-pass with a peak gain of one at its centre.</summary>
            public static Biquad BandPass(float hz, float q)
            {
                var f = new Biquad();
                f.Retune(hz, q);
                return f;
            }

            public void Retune(float hz, float q)
            {
                double w = 2.0 * Math.PI * Math.Min(hz, Rate * 0.45f) / Rate;
                double alpha = Math.Sin(w) / (2.0 * Math.Max(0.3, q));
                double a0 = 1.0 + alpha;
                b0 = (float)(alpha / a0);
                b2 = -b0;
                a1 = (float)(-2.0 * Math.Cos(w) / a0);
                a2 = (float)((1.0 - alpha) / a0);
            }

            public float Step(float x)
            {
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
        }

        private static void HighPass(float[] b, float hz)
        {
            float a = OnePole(hz);
            float s1 = 0f, s2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                s1 += (b[i] - s1) * a;
                float h1 = b[i] - s1;
                s2 += (h1 - s2) * a;
                b[i] = h1 - s2;
            }
        }

        private static void LowPass(float[] b, float hz)
        {
            float a = OnePole(hz);
            float s1 = 0f, s2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                s1 += (b[i] - s1) * a;
                s2 += (s1 - s2) * a;
                b[i] = s2;
            }
        }

        /// <summary>Soft saturation about the buffer's own peak: rounds the top of the loudest
        /// layers (and adds the grit of a pushed throat) without changing how loud it is asked to be.</summary>
        private static void Saturate(float[] b, float drive)
        {
            float peak = Peak(b);
            if (peak < 1e-6f)
            {
                return;
            }
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (float)Math.Tanh(b[i] / peak * drive) / norm * peak;
            }
        }

        /// <summary>A small room (four damped combs into two all-passes) under the dry sound.</summary>
        private static void Room(float[] b, float mix, float size)
        {
            int[] combs = { 1116, 1188, 1277, 1356 };
            int[] allpasses = { 556, 441 };
            var wet = new float[b.Length];
            for (int c = 0; c < combs.Length; c++)
            {
                int d = Math.Max(8, (int)(combs[c] * size));
                var line = new float[d];
                int p = 0;
                float damp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float y = line[p];
                    damp += (y - damp) * 0.6f;
                    line[p] = b[i] + damp * 0.74f;
                    p = (p + 1) % d;
                    wet[i] += y * 0.25f;
                }
            }
            foreach (int ap in allpasses)
            {
                int d = Math.Max(4, (int)(ap * size));
                var line = new float[d];
                int p = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float buf = line[p];
                    float y = -wet[i] + buf;
                    line[p] = wet[i] + buf * 0.5f;
                    p = (p + 1) % d;
                    wet[i] = y;
                }
            }
            for (int i = 0; i < b.Length; i++)
            {
                b[i] += wet[i] * mix;
            }
        }

        private static float Peak(float[] b)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float a = b[i] < 0f ? -b[i] : b[i];
                if (a > max)
                {
                    max = a;
                }
            }
            return max;
        }

        /// <summary>GAIN STAGING: the layer is brought to <paramref name="peak"/> on its own and
        /// then added - a recipe states each layer's level instead of hoping the sum works out.</summary>
        private static void Mix(float[] dst, float[] layer, float peak)
        {
            float max = Peak(layer);
            if (max < 1e-6f)
            {
                return;
            }
            float g = peak / max;
            int n = Math.Min(dst.Length, layer.Length);
            for (int i = 0; i < n; i++)
            {
                dst[i] += layer[i] * g;
            }
        }

        /// <summary>The whole brought to its peak, its last few ms closed so nothing clicks.</summary>
        private static Stem Finish(string name, StemKind kind, float[] b, float peak)
        {
            float max = Peak(b);
            float g = max > 1e-6f ? peak / max : 1f;
            int fade = Math.Min(b.Length, (int)(0.006f * Rate));
            for (int i = 0; i < b.Length; i++)
            {
                float f = i >= b.Length - fade ? (b.Length - i) / (float)fade : 1f;
                b[i] *= g * f;
            }
            return new Stem { Name = name, Kind = kind, Samples = b };
        }
    }
}
