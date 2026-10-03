// PURPOSE: The BLACKJACK table's sound, synthesized and baked once (SoundFx.Blackjack plays it):
// the table's cues and the stage's own MUSIC. Plain C# on float arrays - no UnityEngine - so it
// can be rendered and measured outside the editor, and built on a worker thread so the forty
// seconds of music never cost a frame.
//
// THE CUES are a casino table, not a slot machine: ceramic chips (a short bright click over a
// small body, never a coin), felt (a soft low knock), paper (a card's slide, flick and tap),
// a glassy tick for the HAND's score that is deliberately higher and lighter than the deeper chip
// tick of the BANK - the ear must never confuse points with money - and three results that are
// told by direction: a win rises, a loss falls, a tie hangs. No bells, no coin shower, no jingle.
//
// THE MUSIC is a small late-night jazz room in D minor at 88 BPM, eight bars (21.8 s) on a loop,
// built as FIVE STEMS of identical length so the mix can move with the table: an upright-bass
// walk, brushes and a ride tick, a felt piano, sparse vibraphone, and a low pulse kept for an
// all-in hand. The bet phase is sparse (piano, a soft bass, a vibe), a hand in play brings the
// rhythm in, an all-in adds the pulse; a result ducks it, a stage won resolves it, bankruptcy
// strips it away one stem at a time (SoundFx.Blackjack sets the levels).
// EXTENSION POINT: a new cue is a BlackjackCue and a case in Build.

using System;

namespace ProjectBlock.View
{
    public enum BlackjackCue
    {
        ChipTap,
        ChipStack,
        TableTap,
        WarmTone,
        BankTick,
        ScoreTick,
        LeadChange,
        DealerThink,
        CardSlide,
        CardFlick,
        CardLand,
        DeckSplit,
        WinChime,
        LossClack,
        TieSuspend,
        Bankrupt,
        BossWin,
        InvalidBet,
        PowerBlocked,
        IntroOpen,
        HouseStake,
        AllInThud,
        HandEnd
    }

    /// <summary>The music's five stems, in the order SoundFx keeps their sources.</summary>
    public enum BlackjackStem
    {
        Piano,
        Bass,
        Brush,
        Vibes,
        Pulse
    }

    public static class BlackjackSound
    {
        public const int Rate = 44100;
        public const int MusicRate = 22050;
        public const float Bpm = 88f;
        public const int Bars = 8;

        public static float BeatSeconds
        {
            get { return 60f / Bpm; }
        }

        public static float LoopSeconds
        {
            get { return BeatSeconds * 4f * Bars; }
        }

        // ================================================================== cues

        public static float[] Build(BlackjackCue cue, int variant)
        {
            var rng = new Random(9001 + (int)cue * 31 + variant * 7);
            float[] b;
            switch (cue)
            {
                case BlackjackCue.ChipTap:
                    b = Buf(0.14f);
                    Mix(b, Chip(rng, 1f + variant * 0.04f), 0f, 0.8f);
                    break;
                case BlackjackCue.ChipStack:
                    b = Buf(0.36f);
                    for (int i = 0; i < 4; i++)
                    {
                        Mix(b, Chip(rng, 1.06f - i * 0.05f), 0.035f + i * (0.045f + 0.01f * i), 0.72f - i * 0.08f);
                    }
                    break;
                case BlackjackCue.TableTap:
                    b = Buf(0.2f);
                    Mix(b, Thump(0.18f, 150f, 85f, 0.04f), 0f, 0.8f);
                    Mix(b, LowPass(Burst(rng, 0.03f, 0.002f, 0.008f), 900f), 0f, 0.35f);
                    break;
                case BlackjackCue.WarmTone:
                    b = Buf(0.8f);
                    Mix(b, Mallet(62, 0.7f, 0.35f), 0f, 0.5f);
                    Mix(b, Mallet(69, 0.7f, 0.3f), 0.03f, 0.42f);
                    Mix(b, Mallet(50, 0.8f, 0.5f), 0f, 0.36f);
                    break;
                case BlackjackCue.BankTick:
                    b = Buf(0.08f);
                    Mix(b, BandPass(Burst(rng, 0.03f, 0.001f, 0.006f), 1700f, 3f), 0f, 0.6f);
                    Mix(b, Thump(0.06f, 620f, 480f, 0.012f), 0f, 0.5f);
                    break;
                case BlackjackCue.ScoreTick:
                    b = Buf(0.1f);
                    Mix(b, Tone(0.09f, 2640f + variant * 90f, 0.018f, 0.0015f), 0f, 0.5f);
                    Mix(b, Tone(0.06f, 5280f + variant * 180f, 0.01f, 0.001f), 0f, 0.18f);
                    break;
                case BlackjackCue.LeadChange:
                    b = Buf(0.42f);
                    Mix(b, Glass(88, 0.3f), 0f, 0.42f);
                    Mix(b, Glass(93, 0.3f), 0.07f, 0.46f);
                    break;
                case BlackjackCue.DealerThink:
                    b = Buf(0.34f);
                    for (int i = 0; i < 3; i++)
                    {
                        Mix(b, BandPass(Burst(rng, 0.02f, 0.001f, 0.004f), 4200f - i * 300f, 2.5f), i * 0.09f, 0.28f);
                    }
                    break;
                case BlackjackCue.CardSlide:
                    b = Buf(0.26f);
                    Mix(b, Swept(rng, 0.24f, 1100f, 2600f, 1.6f), 0f, 0.55f);
                    break;
                case BlackjackCue.CardFlick:
                    b = Buf(0.1f);
                    Mix(b, BandPass(Burst(rng, 0.03f, 0.0005f, 0.004f), 4800f, 1.8f), 0f, 0.7f);
                    Mix(b, BandPass(Burst(rng, 0.06f, 0.004f, 0.02f), 2200f, 1.2f), 0.004f, 0.25f);
                    break;
                case BlackjackCue.CardLand:
                    b = Buf(0.14f);
                    Mix(b, LowPass(Burst(rng, 0.03f, 0.0008f, 0.006f), 2400f), 0f, 0.6f);
                    Mix(b, Thump(0.1f, 230f, 160f, 0.022f), 0f, 0.45f);
                    break;
                case BlackjackCue.DeckSplit:
                    b = Buf(0.55f);
                    {
                        float at = 0f;
                        for (int i = 0; i < 14; i++)
                        {
                            Mix(b, BandPass(Burst(rng, 0.018f, 0.0005f, 0.003f), 3600f + (float)rng.NextDouble() * 900f, 2f),
                                at, 0.22f + 0.02f * (i % 3));
                            at += 0.03f - i * 0.0012f;
                        }
                        Mix(b, LowPass(Burst(rng, 0.04f, 0.001f, 0.01f), 1400f), at + 0.04f, 0.45f);
                    }
                    break;
                case BlackjackCue.WinChime:
                    b = Buf(1.1f);
                    Mix(b, Mallet(74, 0.8f, 0.4f), 0f, 0.36f);
                    Mix(b, Mallet(78, 0.8f, 0.4f), 0.075f, 0.36f);
                    Mix(b, Mallet(81, 0.9f, 0.45f), 0.15f, 0.40f);
                    Mix(b, Pad(new[] { 50, 57, 62, 66 }, 1.0f, 0.12f), 0.05f, 0.18f);
                    Mix(b, ChipRun(rng, 5), 0.2f, 0.3f);
                    break;
                case BlackjackCue.LossClack:
                    b = Buf(0.85f);
                    Mix(b, Swept(rng, 0.3f, 2400f, 900f, 1.4f), 0f, 0.32f);
                    Mix(b, Chip(rng, 0.82f), 0.28f, 0.6f);
                    Mix(b, Chip(rng, 0.76f), 0.32f, 0.5f);
                    Mix(b, Glide(0.5f, 220f, 174.6f, 0.25f), 0.3f, 0.3f);
                    break;
                case BlackjackCue.TieSuspend:
                    b = Buf(0.95f);
                    Mix(b, Pad(new[] { 62, 67, 69 }, 0.9f, 0.18f), 0f, 0.34f);
                    Mix(b, Mallet(79, 0.9f, 0.5f), 0.08f, 0.2f);
                    break;
                case BlackjackCue.Bankrupt:
                    b = Buf(2.2f);
                    Mix(b, Glide(2.1f, 110f, 52f, 0.9f), 0f, 0.55f);
                    Mix(b, Glide(2.0f, 165f, 78f, 0.7f), 0.05f, 0.2f);
                    Mix(b, LowPass(Breath(rng, 1.8f), 500f), 0.1f, 0.18f);
                    break;
                case BlackjackCue.BossWin:
                    b = Buf(2.6f);
                    int[] roll = { 62, 66, 69, 76, 81 };
                    for (int i = 0; i < roll.Length; i++)
                    {
                        Mix(b, Mallet(roll[i], 1.4f, 0.6f), i * 0.08f, 0.32f);
                    }
                    Mix(b, Pad(new[] { 50, 57, 62, 66, 69 }, 2.3f, 0.5f), 0.1f, 0.3f);
                    break;
                case BlackjackCue.InvalidBet:
                    b = Buf(0.12f);
                    Mix(b, Thump(0.1f, 320f, 260f, 0.02f), 0f, 0.4f);
                    Mix(b, LowPass(Burst(rng, 0.02f, 0.0005f, 0.004f), 1500f), 0f, 0.2f);
                    break;
                case BlackjackCue.PowerBlocked:
                    b = Buf(0.16f);
                    Mix(b, Thump(0.13f, 200f, 150f, 0.03f), 0f, 0.5f);
                    Mix(b, LowPass(Burst(rng, 0.02f, 0.0005f, 0.005f), 1200f), 0.002f, 0.28f);
                    break;
                case BlackjackCue.IntroOpen:
                    b = Buf(1.1f);
                    Mix(b, LowPass(Swell(rng, 0.7f), 1200f), 0f, 0.3f);
                    Mix(b, Pad(new[] { 38, 45, 53, 57 }, 1.0f, 0.3f), 0.15f, 0.36f);
                    break;
                case BlackjackCue.HouseStake:
                    b = Buf(0.5f);
                    Mix(b, Swept(rng, 0.28f, 900f, 1800f, 1.4f), 0f, 0.3f);
                    Mix(b, Chip(rng, 0.94f), 0.27f, 0.55f);
                    Mix(b, Chip(rng, 0.9f), 0.33f, 0.45f);
                    break;
                case BlackjackCue.AllInThud:
                    b = Buf(0.7f);
                    Mix(b, Thump(0.5f, 72f, 44f, 0.16f), 0f, 0.9f);
                    Mix(b, ChipRun(rng, 4), 0.06f, 0.45f);
                    break;
                case BlackjackCue.HandEnd:
                    b = Buf(0.5f);
                    Mix(b, Thump(0.3f, 120f, 90f, 0.08f), 0f, 0.5f);
                    Mix(b, Pad(new[] { 50, 57 }, 0.45f, 0.05f), 0f, 0.16f);
                    break;
                default:
                    return null;
            }
            Limit(b, 0.92f);
            Declick(b);
            return b;
        }

        // ================================================================== music

        private static readonly int[][] Chords =
        {
            new[] { 53, 57, 60, 64 },   // Dm9
            new[] { 50, 53, 57, 62 },   // Bbmaj7
            new[] { 53, 57, 58, 62 },   // Gm9
            new[] { 55, 61, 64 },       // A7
            new[] { 53, 57, 60, 64 },   // Dm9
            new[] { 53, 57, 60, 64 },   // Fmaj7 (shares Dm9's voicing - a colour, not a turn)
            new[] { 53, 57, 58, 62 },   // Gm9
            new[] { 55, 58, 61, 64 }    // A7b9
        };

        private static readonly int[][] Walk =
        {
            new[] { 38, 45, 41, 35 },
            new[] { 34, 41, 38, 42 },
            new[] { 43, 38, 41, 44 },
            new[] { 45, 40, 43, 37 },
            new[] { 38, 45, 41, 40 },
            new[] { 41, 45, 36, 42 },
            new[] { 43, 46, 38, 44 },
            new[] { 45, 40, 43, 37 }
        };

        /// <summary>One stem of the loop, MusicRate mono, exactly LoopSeconds long.</summary>
        public static float[] BuildStem(BlackjackStem stem)
        {
            int n = (int)(LoopSeconds * MusicRate);
            var b = new float[n];
            float beat = BeatSeconds;
            var rng = new Random(4242 + (int)stem);
            switch (stem)
            {
                case BlackjackStem.Piano:
                    for (int bar = 0; bar < Bars; bar++)
                    {
                        float t0 = bar * beat * 4f;
                        int[] chord = Chords[bar];
                        for (int k = 0; k < chord.Length; k++)
                        {
                            // a rolled chord, the felt taking the edge off every note
                            AddLooped(b, Piano(chord[k], 2.6f, 0.62f), t0 + k * 0.018f, 0.16f);
                        }
                        if (bar % 2 == 1)
                        {
                            // a soft answer on the and of three
                            AddLooped(b, Piano(chord[chord.Length - 1] + 12, 1.2f, 0.4f), t0 + beat * 2.5f, 0.08f);
                        }
                    }
                    break;
                case BlackjackStem.Bass:
                    for (int bar = 0; bar < Bars; bar++)
                    {
                        for (int q = 0; q < 4; q++)
                        {
                            float t = bar * beat * 4f + q * beat;
                            float vel = q == 0 ? 1f : q == 2 ? 0.86f : 0.74f;
                            AddLooped(b, Upright(Walk[bar][q], beat * 1.05f), t, 0.42f * vel);
                        }
                    }
                    break;
                case BlackjackStem.Brush:
                    {
                        // the swirl: a breath of band noise rising into every beat
                        float[] noise = BandPassM(NoiseBuf(rng, n), 2600f, 0.7f);
                        for (int i = 0; i < n; i++)
                        {
                            float t = (float)i / MusicRate;
                            float ph = (t / beat) % 1f;
                            float env = 0.35f + 0.65f * (float)Math.Sin(Math.PI * ph);
                            b[i] = noise[i] * 0.07f * env;
                        }
                        int beats = Bars * 4;
                        for (int q = 0; q < beats; q++)
                        {
                            float t = q * beat;
                            if (q % 2 == 1)
                            {
                                AddLooped(b, Slap(rng), t, 0.26f);
                            }
                            // the ride's tick, swung: on the beat and the swung and
                            AddLooped(b, Tick(rng), t, 0.06f);
                            if (q % 2 == 1)
                            {
                                AddLooped(b, Tick(rng), t + beat * 0.66f, 0.045f);
                            }
                        }
                    }
                    break;
                case BlackjackStem.Vibes:
                    {
                        // sparse, a phrase every other bar, never a melody that asks to be followed
                        float[][] phrase =
                        {
                            new[] { 0f, 2f, 81f }, new[] { 0f, 3f, 77f },
                            new[] { 2f, 1.5f, 76f }, new[] { 2f, 2.5f, 84f }, new[] { 2f, 3.5f, 81f },
                            new[] { 4f, 0.5f, 74f }, new[] { 4f, 1.5f, 77f }, new[] { 4f, 2.5f, 81f },
                            new[] { 6f, 2f, 79f }, new[] { 6f, 3f, 76f }, new[] { 7f, 2f, 73f }
                        };
                        for (int i = 0; i < phrase.Length; i++)
                        {
                            float t = phrase[i][0] * beat * 4f + phrase[i][1] * beat;
                            AddLooped(b, Vibe((int)phrase[i][2], 1.6f), t, 0.22f);
                        }
                    }
                    break;
                case BlackjackStem.Pulse:
                    for (int bar = 0; bar < Bars; bar++)
                    {
                        for (int q = 0; q < 4; q++)
                        {
                            float t = bar * beat * 4f + q * beat;
                            float[] thump = ThumpM(beat * 0.6f, 62f, 46f, 0.12f);
                            AddLooped(b, thump, t, q == 0 ? 0.5f : 0.34f);
                            if (q % 2 == 0)
                            {
                                AddLooped(b, thump, t + beat * 0.22f, 0.18f);
                            }
                        }
                    }
                    break;
            }
            Limit(b, 0.9f);
            return b;
        }

        // ================================================================== instruments (cue rate)

        /// <summary>A ceramic chip: a short bright click with a small resonant body. Never a coin.</summary>
        private static float[] Chip(Random rng, float pitch)
        {
            float[] b = Buf(0.1f);
            Mix(b, BandPass(Burst(rng, 0.012f, 0.0003f, 0.002f), 5200f * pitch, 2.2f), 0f, 0.8f);
            Mix(b, BandPass(Burst(rng, 0.012f, 0.0003f, 0.0025f), 3300f * pitch, 3f), 0.0015f, 0.6f);
            Mix(b, Tone(0.06f, 2150f * pitch, 0.011f, 0.0004f), 0f, 0.3f);
            Mix(b, Thump(0.05f, 700f * pitch, 520f * pitch, 0.008f), 0f, 0.25f);
            return b;
        }

        private static float[] ChipRun(Random rng, int count)
        {
            float[] b = Buf(0.05f * count + 0.12f);
            for (int i = 0; i < count; i++)
            {
                Mix(b, Chip(rng, 1.04f - i * 0.03f), i * (0.04f + 0.006f * i), 0.75f - i * 0.08f);
            }
            return b;
        }

        /// <summary>A soft mallet (marimba-ish): fundamental and a quiet fourth partial.</summary>
        private static float[] Mallet(int midi, float dur, float tau)
        {
            float f = Hz(midi);
            float[] b = Buf(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = (1f - (float)Math.Exp(-t / 0.003f)) * (float)Math.Exp(-t / tau);
                b[i] = env * ((float)Math.Sin(2 * Math.PI * f * t)
                    + 0.18f * (float)Math.Sin(2 * Math.PI * f * 3.98f * t) * (float)Math.Exp(-t / (tau * 0.25f)));
            }
            return b;
        }

        /// <summary>A glassy note - the score's language, lighter than any chip.</summary>
        private static float[] Glass(int midi, float dur)
        {
            float f = Hz(midi);
            float[] b = Buf(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = (1f - (float)Math.Exp(-t / 0.002f)) * (float)Math.Exp(-t / 0.09f);
                b[i] = env * ((float)Math.Sin(2 * Math.PI * f * t) + 0.3f * (float)Math.Sin(2 * Math.PI * f * 2.76f * t));
            }
            return b;
        }

        /// <summary>A soft sustained chord.</summary>
        private static float[] Pad(int[] notes, float dur, float attack)
        {
            float[] b = Buf(dur);
            for (int k = 0; k < notes.Length; k++)
            {
                float f = Hz(notes[k]);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = (float)i / Rate;
                    float env = Math.Min(1f, t / Math.Max(0.01f, attack)) * Math.Min(1f, (dur - t) / (dur * 0.5f));
                    b[i] += env * ((float)Math.Sin(2 * Math.PI * f * t) + 0.25f * (float)Math.Sin(4 * Math.PI * f * t)) / notes.Length;
                }
            }
            return b;
        }

        private static float[] Glide(float dur, float fromHz, float toHz, float tau)
        {
            float[] b = Buf(dur);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float k = t / dur;
                float f = fromHz * (float)Math.Pow(toHz / fromHz, k);
                ph += 2 * Math.PI * f / Rate;
                float env = (1f - (float)Math.Exp(-t / 0.02f)) * (float)Math.Exp(-t / Math.Max(0.05f, tau * dur));
                b[i] = env * ((float)Math.Sin(ph) + 0.3f * (float)Math.Sin(2 * ph));
            }
            return b;
        }

        private static float[] Thump(float dur, float fromHz, float toHz, float tau)
        {
            return ThumpAt(dur, fromHz, toHz, tau, Rate);
        }

        private static float[] ThumpM(float dur, float fromHz, float toHz, float tau)
        {
            return ThumpAt(dur, fromHz, toHz, tau, MusicRate);
        }

        private static float[] ThumpAt(float dur, float fromHz, float toHz, float tau, int rate)
        {
            float[] b = new float[Math.Max(1, (int)(dur * rate))];
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / rate;
                float f = toHz + (fromHz - toHz) * (float)Math.Exp(-t / (tau * 0.5f));
                ph += 2 * Math.PI * f / rate;
                float env = (1f - (float)Math.Exp(-t / 0.002f)) * (float)Math.Exp(-t / tau);
                b[i] = env * (float)Math.Sin(ph);
            }
            return b;
        }

        private static float[] Tone(float dur, float hz, float tau, float attack)
        {
            float[] b = Buf(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                b[i] = (1f - (float)Math.Exp(-t / attack)) * (float)Math.Exp(-t / tau) * (float)Math.Sin(2 * Math.PI * hz * t);
            }
            return b;
        }

        private static float[] Burst(Random rng, float dur, float attack, float tau)
        {
            float[] b = Buf(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                b[i] = ((float)rng.NextDouble() * 2f - 1f) * (1f - (float)Math.Exp(-t / attack)) * (float)Math.Exp(-t / tau);
            }
            return b;
        }

        private static float[] Swell(Random rng, float dur)
        {
            float[] b = Buf(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float k = (float)i / b.Length;
                b[i] = ((float)rng.NextDouble() * 2f - 1f) * (float)Math.Sin(Math.PI * k) * (float)Math.Sin(Math.PI * k);
            }
            return b;
        }

        private static float[] Breath(Random rng, float dur)
        {
            float[] b = Buf(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float k = (float)i / b.Length;
                b[i] = ((float)rng.NextDouble() * 2f - 1f) * (float)Math.Sin(Math.PI * Math.Min(1f, k * 1.6f)) * (1f - k);
            }
            return b;
        }

        /// <summary>Paper over felt: band noise whose centre glides, swelling and settling.</summary>
        private static float[] Swept(Random rng, float dur, float fromHz, float toHz, float q)
        {
            float[] raw = Swell(rng, dur);
            float[] b = new float[raw.Length];
            float z1 = 0f, z2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float k = (float)i / raw.Length;
                float f = fromHz + (toHz - fromHz) * k;
                Biquad(raw[i], f, q, Rate, ref z1, ref z2, out b[i]);
            }
            return b;
        }

        // ================================================================== instruments (music rate)

        /// <summary>A felt piano note: partials that die faster the higher they are, a soft hammer.</summary>
        private static float[] Piano(int midi, float dur, float bright)
        {
            float f = Hz(midi);
            float[] b = new float[(int)(dur * MusicRate)];
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / MusicRate;
                float s = 0f;
                for (int k = 1; k <= 5; k++)
                {
                    float amp = (float)Math.Pow(k, -1.6) * (k == 1 ? 1f : bright);
                    float tau = 1.4f / (float)Math.Pow(k, 0.8);
                    s += amp * (float)Math.Exp(-t / tau) * (float)Math.Sin(2 * Math.PI * f * k * 1.0008 * t);
                }
                float attack = 1f - (float)Math.Exp(-t / 0.006f);
                float release = Math.Min(1f, (dur - t) / 0.15f);
                b[i] = s * attack * release;
            }
            return b;
        }

        /// <summary>An upright bass pluck: a round fundamental, a little second, a finger thump, a
        /// hair of pitch falling into the note.</summary>
        private static float[] Upright(int midi, float dur)
        {
            float f = Hz(midi);
            float[] b = new float[(int)(dur * MusicRate)];
            double ph = 0;
            var rng = new Random(midi * 17);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / MusicRate;
                float bend = 1f + 0.012f * (float)Math.Exp(-t / 0.03f);
                ph += 2 * Math.PI * f * bend / MusicRate;
                float env = (1f - (float)Math.Exp(-t / 0.006f)) * (float)Math.Exp(-t / 0.45f);
                float release = Math.Min(1f, (dur - t) / 0.06f);
                float body = (float)Math.Sin(ph) + 0.38f * (float)Math.Sin(2 * ph) + 0.12f * (float)Math.Sin(3 * ph);
                float thumb = ((float)rng.NextDouble() * 2f - 1f) * (float)Math.Exp(-t / 0.004f) * 0.4f;
                b[i] = (body * env + thumb) * release;
            }
            return b;
        }

        private static float[] Vibe(int midi, float dur)
        {
            float f = Hz(midi);
            float[] b = new float[(int)(dur * MusicRate)];
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / MusicRate;
                float trem = 1f - 0.22f * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * 5.4f * t));
                float env = (1f - (float)Math.Exp(-t / 0.004f)) * (float)Math.Exp(-t / 0.9f) * Math.Min(1f, (dur - t) / 0.2f);
                b[i] = env * trem * ((float)Math.Sin(2 * Math.PI * f * t)
                    + 0.1f * (float)Math.Sin(2 * Math.PI * f * 4f * t) * (float)Math.Exp(-t / 0.15f));
            }
            return b;
        }

        private static float[] Slap(Random rng)
        {
            float[] raw = new float[(int)(0.09f * MusicRate)];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / MusicRate;
                raw[i] = ((float)rng.NextDouble() * 2f - 1f) * (1f - (float)Math.Exp(-t / 0.004f)) * (float)Math.Exp(-t / 0.035f);
            }
            return BandPassM(raw, 3000f, 0.8f);
        }

        private static float[] Tick(Random rng)
        {
            float[] raw = new float[(int)(0.05f * MusicRate)];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / MusicRate;
                raw[i] = ((float)rng.NextDouble() * 2f - 1f) * (float)Math.Exp(-t / 0.012f);
            }
            return BandPassM(raw, 7000f, 1.5f);
        }

        // ================================================================== plumbing

        private static float Hz(int midi)
        {
            return 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);
        }

        private static float[] Buf(float seconds)
        {
            return new float[Math.Max(1, (int)(seconds * Rate))];
        }

        private static float[] NoiseBuf(Random rng, int n)
        {
            var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                b[i] = (float)rng.NextDouble() * 2f - 1f;
            }
            return b;
        }

        private static void Biquad(float x, float hz, float q, int rate, ref float z1, ref float z2, out float y)
        {
            // a state-variable band-pass (stable while the centre moves)
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(hz, rate * 0.24f) / rate);
            float damp = 1f / Math.Max(0.3f, q);
            float high = x - z1 * damp - z2;
            float band = f * high + z1;
            float low = f * band + z2;
            z1 = band;
            z2 = low;
            y = band;
        }

        private static float[] BandPass(float[] input, float hz, float q)
        {
            var b = new float[input.Length];
            float z1 = 0f, z2 = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                Biquad(input[i], hz, q, Rate, ref z1, ref z2, out b[i]);
            }
            return b;
        }

        private static float[] BandPassM(float[] input, float hz, float q)
        {
            var b = new float[input.Length];
            float z1 = 0f, z2 = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                Biquad(input[i], hz, q, MusicRate, ref z1, ref z2, out b[i]);
            }
            return b;
        }

        private static float[] LowPass(float[] input, float hz)
        {
            var b = new float[input.Length];
            float a = 1f - (float)Math.Exp(-2 * Math.PI * hz / Rate);
            float z = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                z += (input[i] - z) * a;
                b[i] = z;
            }
            return b;
        }

        /// <summary>Brings a layer to a stated peak and adds it at a time (seconds) - so nothing
        /// mixed here can clip by being louder than it was asked to be.</summary>
        private static void Mix(float[] into, float[] layer, float at, float peak)
        {
            float max = 0f;
            for (int i = 0; i < layer.Length; i++)
            {
                max = Math.Max(max, Math.Abs(layer[i]));
            }
            if (max <= 0f)
            {
                return;
            }
            float g = peak / max;
            int start = (int)(at * Rate);
            for (int i = 0; i < layer.Length && start + i < into.Length; i++)
            {
                if (start + i >= 0)
                {
                    into[start + i] += layer[i] * g;
                }
            }
        }

        /// <summary>Adds a music-rate layer at a time, WRAPPING round the loop's end so a note held
        /// over the last bar line rings into the first one (the loop has no seam).</summary>
        private static void AddLooped(float[] into, float[] layer, float at, float gain)
        {
            int start = (int)(at * MusicRate);
            for (int i = 0; i < layer.Length; i++)
            {
                int k = (start + i) % into.Length;
                into[k] += layer[i] * gain;
            }
        }

        private static void Limit(float[] b, float peak)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                max = Math.Max(max, Math.Abs(b[i]));
            }
            if (max > peak)
            {
                float g = peak / max;
                for (int i = 0; i < b.Length; i++)
                {
                    b[i] *= g;
                }
            }
        }

        private static void Declick(float[] b)
        {
            int n = Math.Min(64, b.Length / 4);
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                b[b.Length - 1 - i] *= k;
            }
        }
    }
}
