// PURPOSE: The "MIX" sound set - the one the listening settled on, and the default. It takes each
// family from the set that won it and reworks what was still wrong:
//   - PLACING A BLOCK (place, pickup, reject): NEW, unchanged.
//   - LINES: its own clear (MixExplode), in one of three CRUNCH candidates picked in the lab -
//     CRUNCH won over a chain of placing clacks (SNAP) and a run of tuned taps (TAP), and CRISP /
//     CHUNKY are its refinements; ZIP / POP / CHIP are three different approaches beside them
//     (an outward "shwip" with the beam, popping candy, a chiptune arpeggio). ZIP won that round,
//     so five ZIP variants follow it (TWIN, SPARK, GLISS, EDGE, SOFT). Two earlier takes
//     are buried here: NEW's with its thump halved was still too aggressive, and a soft take built
//     from low upward-gliding resonant "bloops" over a muffled thud read as a FART - anything that
//     glides upward in the low mids with a high Q does. Line clears stay short, bright-edged and
//     dry.
//   - THE EXPLOSION BED (MixBlast, MixBlastLevel): a soft, distant blast of muffled noise behind
//     line clears and the sweep's hit - a hint that the cubes really went off, kept subtle. It is
//     a short mid-band RUSH with no room: a bright snap with a big room read as a gunshot, and dark
//     low noise through the room was a boxy reverbed second thump.
//   - THE THUMP: NEW's, back under both, as its OWN LAYER at a level (MixThumpLevel, set in the
//     lab) - it was the right sound, only too dominant, and taking it out left the clears weightless.
//     Line clears also end on a short rising pluck motif, one note more per extra line.
//   - SWEEP: ONE hit (a second impact on the chord's landing was one too many) built as a phrase -
//     a clean noise swell into a big crunch over the thump, a swelling two-octave pluck run, a
//     strummed chord with its top octave shimmering in behind, dry sparkle and air.
//   - SHUFFLE: V3's riffle, which won the listen over MIX's own resonant whirr.
//   - CARDS & MARKET: V3's direction (soft, warm, nothing metallic) but away from the stock
//     timbres - V3 read as "stereotypical" (a marimba, a generic riffle). Two instruments carry it
//     instead, both unusual enough to have a voice of their own:
//       * a RESONANT GLIDE (Resonate - a state-variable band-pass swept while it rings): a click
//         into it is a "bloop", noise into it a pitched whirr. Buying is two bloops rising; vanishing
//         is a "fwip" that swells and cuts off.
//       * a MUTED PLUCKED STRING (Pluck, Karplus-Strong with a heavy damping filter in the loop): a
//         soft nylon "plink" for chimes, fusions and the sweep's run - pitched, warm, and it dies
//         on its own instead of ringing.
//   - FLAME and FUSE, which no earlier set touched: the flame is a flickering broadband roar that
//     flares on ignition, with dry crackle snaps - its first take was built on resonant filters and
//     sounded WET (resonance bubbles; fire is broadband and dry). The fuse is a thin sputter of
//     gated hiss and dry snaps, still pitched by how full the charge is.
// Shared helpers (PSine, PNoise, Room, PFinish, HighPass...) live in SoundFx.Polished.cs / .V3.cs.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private const int MixSet = 3;

        private AudioClip[][][] mixExplodeStyles; // [style][tier][variant]

        private const string MixLinePrefKey = "sfx.mixline3"; // reset when ZIP GLISS was locked in

        /// <summary>The line-clear candidates MIX can play, in the order the lab shows them.</summary>
        public static readonly string[] MixLineStyleNames = { "CRUNCH", "CRISP", "CHUNKY", "ZIP", "POP", "CHIP",
            "ZIP TWIN", "ZIP SPARK", "ZIP GLISS", "ZIP EDGE", "ZIP SOFT" };

        private int mixLineStyle;

        /// <summary>Which line-clear candidate MIX plays. Persisted; switched from the sound lab.
        /// </summary>
        public int MixLineStyle
        {
            get { return mixLineStyle; }
            set
            {
                mixLineStyle = Mathf.Clamp(value, 0, MixLineStyleNames.Length - 1);
                try { PlayerPrefs.SetInt(MixLinePrefKey, mixLineStyle); } catch { }
            }
        }

        /// <summary>ZIP GLISS - the line clear the listening LOCKED IN (2026-09-28). The other
        /// styles are kept as an archive the sound lab can still play.</summary>
        public const int LockedLineStyle = 8;

        /// <summary>Plays one line-clear STYLE through MIX (all its layers), without changing
        /// which style the game uses - the sound lab's archive of candidates.</summary>
        public void AuditionLineStyle(int style, int lines, int combo)
        {
            int was = mixLineStyle;
            mixLineStyle = Mathf.Clamp(style, 0, MixLineStyleNames.Length - 1);
            Audition(MixSet, s => s.Explode(lines, combo));
            mixLineStyle = was;
        }

        /// <summary>Plays ONE layer of MIX's line clear alone: 0 the top (the style), 1 the thump,
        /// 2 the explosion bed - so each can be judged on its own.</summary>
        public void AuditionLineLayer(int layer, int lines)
        {
            int tier = lines >= 3 ? 2 : (lines == 2 ? 1 : 0);
            if (layer == 0)
            {
                PlayPolished(MixExplodeTakes(tier), Detune(0.3f), 0.95f + 0.05f * tier);
            }
            else if (layer == 1)
            {
                PlayPolished(mixThump[tier], Detune(0.2f), Mathf.Max(0.3f, MixThumpVolume));
            }
            else
            {
                PlayPolished(mixBlast[tier], Detune(0.4f), Mathf.Max(0.3f, mixBlastLevel));
            }
        }

        /// <summary>MIX's line clear for a tier, in the chosen style.</summary>
        private AudioClip[] MixExplodeTakes(int tier)
        {
            return mixExplodeStyles[mixLineStyle][tier];
        }
        private AudioClip[] mixSweep;
        private AudioClip[][] mixThump;     // [tier][variant] - NEW's thump, its own layer
        private AudioClip[] mixSweepThump;

        private const string MixThumpPrefKey = "sfx.mixthump";

        /// <summary>How loud MIX's thump layer plays under line clears and sweeps, 0..1.5. NEW's
        /// thump was the right sound but too DOMINANT, so it came back as a layer with a level of
        /// its own. Persisted; set from the sound lab.</summary>
        public float MixThumpLevel
        {
            get { return mixThumpLevel; }
            set
            {
                mixThumpLevel = Mathf.Clamp(value, 0f, 1.5f);
                try { PlayerPrefs.SetFloat(MixThumpPrefKey, mixThumpLevel); } catch { }
            }
        }

        private float mixThumpLevel = DefaultThumpLevel;
        private const float DefaultThumpLevel = 0.5f;

        private float MixThumpVolume
        {
            get { return mixThumpLevel; }
        }

        private AudioClip[][] mixBlast;     // [tier][variant] - the distant explosion bed
        private AudioClip[] mixSweepBlast;

        private const string MixBlastPrefKey = "sfx.mixblast";

        /// <summary>How loud MIX's EXPLOSION BED plays behind line clears and sweeps, 0..1.5: a
        /// soft, distant blast of noise that hints the cubes actually went off, without the
        /// crack and grit that made NEW's clears aggressive. Subtle by default. Persisted; set
        /// from the sound lab.</summary>
        public float MixBlastLevel
        {
            get { return mixBlastLevel; }
            set
            {
                mixBlastLevel = Mathf.Clamp(value, 0f, 1.5f);
                try { PlayerPrefs.SetFloat(MixBlastPrefKey, mixBlastLevel); } catch { }
            }
        }

        private float mixBlastLevel = DefaultBlastLevel;
        private const float DefaultBlastLevel = 0.3f;

        // The sweep's timing: the hit (where the thump layer lands too) and the chord.
        private const float SweepHit = 0.12f;
        private const float SweepStep = 0.036f;
        private const int SweepRunNotes = 7;
        private const float SweepChord = SweepHit + 0.05f + SweepStep * SweepRunNotes + 0.02f;
        private AudioClip[] mixBell;
        private AudioClip[] mixShuffle;
        private AudioClip[] mixBuy;
        private AudioClip[] mixVanish;
        private AudioClip[] mixFlame;
        private AudioClip[] mixFuse;

        private void BuildMix()
        {
            try { mixLineStyle = Mathf.Clamp(PlayerPrefs.GetInt(MixLinePrefKey, LockedLineStyle), 0, MixLineStyleNames.Length - 1); }
            catch { mixLineStyle = LockedLineStyle; }
            mixExplodeStyles = new AudioClip[MixLineStyleNames.Length][][];
            for (int style = 0; style < MixLineStyleNames.Length; style++)
            {
                int st = style;
                mixExplodeStyles[style] = new[]
                {
                    Variants(3, s => MixExplode(st, 0, s)),
                    Variants(3, s => MixExplode(st, 1, s)),
                    Variants(3, s => MixExplode(st, 2, s)),
                };
            }
            mixSweep = Variants(2, MixSweep);
            try { mixThumpLevel = Mathf.Clamp(PlayerPrefs.GetFloat(MixThumpPrefKey, DefaultThumpLevel), 0f, 1.5f); }
            catch { mixThumpLevel = DefaultThumpLevel; }
            mixThump = new[]
            {
                Variants(3, s => MixThump(0, s)),
                Variants(3, s => MixThump(1, s)),
                Variants(3, s => MixThump(2, s)),
            };
            mixSweepThump = new[] { MixSweepThump() };
            try { mixBlastLevel = Mathf.Clamp(PlayerPrefs.GetFloat(MixBlastPrefKey, DefaultBlastLevel), 0f, 1.5f); }
            catch { mixBlastLevel = DefaultBlastLevel; }
            mixBlast = new[]
            {
                Variants(3, s => MixBlast(0, s, 0f)),
                Variants(3, s => MixBlast(1, s, 0f)),
                Variants(3, s => MixBlast(2, s, 0f)),
            };
            mixSweepBlast = new[] { MixBlast(2, 7, SweepHit) };
            mixBell = Variants(3, MixBell);
            mixShuffle = v3Shuffle; // V3's riffle won the listen
            mixBuy = Variants(3, MixBuy);
            mixVanish = Variants(3, MixVanish);
            mixFlame = Variants(3, MixFlame);
            mixFuse = Variants(4, MixFuse);
        }

        /// <summary>MIX's flame, or false when another set is playing (the legacy clip then plays).
        /// </summary>
        private bool TryMixFlame()
        {
            if (activeSet != MixSet)
            {
                return false;
            }
            PlayPolished(mixFlame, Detune(0.6f), 0.95f);
            return true;
        }

        private bool TryMixFuse(float pitch)
        {
            if (activeSet != MixSet)
            {
                return false;
            }
            PlayPolished(mixFuse, pitch * Detune(0.3f), 0.5f);
            return true;
        }

        // ---- builders ----

        /// <summary>
        /// A row or column clearing. CRUNCH won the first listen (over a snap chain and a run of
        /// tuned taps), so the three candidates picked in the sound lab (MixLineStyle) are now all
        /// crunches: CRUNCH - the one that won, kept as the reference: a bright contact, a short mid
        /// punch, a debris band and a cloud of dry grains. CRISP - the grains ROLL along the line
        /// from one end to the other, tightening as they go, over a brighter contact: the line
        /// crumbling in order rather than all at once. CHUNKY - fewer, bigger, lower fragments and
        /// a short mid "thock": larger pieces breaking. Every one ends on a rising pluck motif (a
        /// note more per extra line) and sits on MIX's thump layer.
        /// Then three DIFFERENT approaches beside them: ZIP - a small crunch and a bright "shwip"
        /// racing outward, travelling with the beam; POP - popping candy, high dry pops that fall
        /// in pitch; CHIP - a fast soft square-wave arpeggio over a light crunch.
        /// </summary>
        private static AudioClip MixExplode(int style, int tier, int v)
        {
            var rng = new System.Random(20000 + style * 100 + tier * 10 + v);
            float[] b = Buffer(0.4f + 0.1f * tier);
            float j = 1f + 0.03f * (v - 1f);
            float run = 0.07f + 0.035f * tier;
            float zipLen = 0.07f + 0.025f * tier;
            if (style == 0)
            {
                // CRUNCH
                Crunch(b, 0f, 1f, tier, j, run, rng);
                Saturate(b, 1.5f);
            }
            else if (style == 1)
            {
                // CRISP
                PNoise(b, 0f, 0.012f, 0.6f, 0.0001f, 0.0022f, 3500f, 13000f, rng);
                PSine(b, 0f, 0.06f, 420f * j, 210f * j, 0.008f, 0.35f, 0.0004f, 0.02f);
                PNoiseSweep(b, 0.002f, 0.18f + 0.05f * tier, 0.28f, 0.001f, 0.03f + 0.01f * tier,
                    1800f, 9000f, 4500f, rng);
                int grains = 18 + 10 * tier;
                for (int g = 0; g < grains; g++)
                {
                    float u = g / (float)(grains - 1);
                    // Rolling front: spaced along the run, the gaps closing as it goes.
                    float at = 0.002f + (run + 0.03f) * Mathf.Pow(u, 0.8f) + 0.003f * (float)rng.NextDouble();
                    float lo = (2600f + 2400f * u) * (0.85f + 0.3f * (float)rng.NextDouble());
                    float amp = (0.18f + 0.2f * (float)rng.NextDouble()) * (1f - 0.35f * u);
                    PNoise(b, at, 0.005f, amp, 0.0001f, 0.001f, lo, lo * 2.2f, rng);
                }
                Saturate(b, 1.4f);
            }
            else if (style == 2)
            {
                // CHUNKY
                PNoise(b, 0f, 0.015f, 0.5f, 0.0002f, 0.003f, 2000f, 10000f, rng);
                PSine(b, 0f, 0.09f, 300f * j, 150f * j, 0.012f, 0.45f, 0.0006f, 0.03f);
                PNoiseSweep(b, 0.003f, 0.22f + 0.06f * tier, 0.3f, 0.002f, 0.04f + 0.012f * tier,
                    700f, 5000f, 2000f, rng);
                int chunks = 7 + 4 * tier;
                for (int g = 0; g < chunks; g++)
                {
                    float at = 0.004f + (run + 0.04f) * (float)rng.NextDouble();
                    float lo = 900f + 1500f * (float)rng.NextDouble();
                    PNoise(b, at, 0.014f, 0.25f + 0.25f * (float)rng.NextDouble(), 0.0002f, 0.003f,
                        lo, lo * 3f, rng);
                }
                Saturate(b, 1.5f);
            }
            else if (style == 3)
            {
                // ZIP: the sound travels with the picture - the beam leaves the middle for both
                // ends, so a small crunch lands and a bright "shwip" races outward from it, opening
                // as it goes and thinning out at the edge.
                Crunch(b, 0f, 0.55f, tier, j, run * 0.6f, rng);
                Zip(b, 0.004f, zipLen, 900f, 3500f, 2500f * j, 12000f * j, 0.15f, 0.9f, rng);
                Saturate(b, 1.3f);
            }
            else if (style >= 6)
            {
                // ZIP VARIANTS - each pushes one idea of ZIP further, same language.
                const float z0 = 0.004f;
                if (style == 6)
                {
                    // TWIN: two shwips at once, one a little higher, one lower - "both ends".
                    Crunch(b, 0f, 0.55f, tier, j, run * 0.6f, rng);
                    Zip(b, z0, zipLen, 1000f, 3800f, 2800f * j, 12500f * j, 0.15f, 0.6f, rng);
                    Zip(b, z0 + 0.003f, zipLen * 1.1f, 600f, 2200f, 1800f * j, 7000f * j, 0.15f, 0.6f, rng);
                }
                else if (style == 7)
                {
                    // SPARK: the shwip trails a line of tiny dry ticks - a sparkler along the line.
                    Crunch(b, 0f, 0.5f, tier, j, run * 0.6f, rng);
                    Zip(b, z0, zipLen, 900f, 3500f, 2500f * j, 12000f * j, 0.15f, 0.75f, rng);
                    int ticks = 10 + 5 * tier;
                    for (int k = 0; k < ticks; k++)
                    {
                        float u = k / (float)(ticks - 1);
                        float at = z0 + zipLen * 1.25f * u + 0.003f * (float)rng.NextDouble();
                        float lo = (4000f + 4000f * u) * (0.9f + 0.2f * (float)rng.NextDouble());
                        PNoise(b, at, 0.003f, (0.12f + 0.15f * (float)rng.NextDouble()) * (1f - 0.5f * u),
                            0.0001f, 0.0006f, lo, lo * 1.8f, rng);
                    }
                }
                else if (style == 8)
                {
                    // GLISS: quick plucks run up WITH the shwip - the sweep is the melody.
                    Crunch(b, 0f, 0.5f, tier, j, run * 0.6f, rng);
                    Zip(b, z0, zipLen, 900f, 3500f, 2500f * j, 12000f * j, 0.15f, 0.7f, rng);
                    float[] gliss = { 783.99f, 880f, 987.77f, 1174.66f, 1318.51f, 1567.98f };
                    int notes = 4 + tier;
                    for (int k = 0; k < notes; k++)
                    {
                        Pluck(b, z0 + zipLen * k / notes, gliss[k] * j, 0.13f + 0.03f * k, 0.99f, 0.45f, rng);
                    }
                }
                else if (style == 9)
                {
                    // EDGE: the shwip lands - two small crisp ticks as the beam reaches both ends.
                    Crunch(b, 0f, 0.55f, tier, j, run * 0.6f, rng);
                    Zip(b, z0, zipLen, 900f, 3500f, 2500f * j, 12000f * j, 0.15f, 0.8f, rng);
                    float end = z0 + zipLen * 0.85f;
                    for (int e = 0; e < 2; e++)
                    {
                        float at = end + 0.006f * e;
                        float amp = e == 0 ? 0.45f : 0.35f;
                        PNoise(b, at, 0.004f, amp, 0.0001f, 0.0008f, 3000f, 12000f, rng);
                        PSine(b, at, 0.02f, 2300f * j, 1500f * j, 0.003f, amp * 0.45f, 0.0002f, 0.005f);
                    }
                }
                else
                {
                    // SOFT: longer and airier, a smaller crunch - the gentlest of them.
                    Crunch(b, 0f, 0.35f, tier, j, run * 0.6f, rng);
                    Zip(b, z0, zipLen * 1.6f, 500f, 2000f, 1800f * j, 7000f * j, 0.3f, 0.8f, rng);
                }
                Saturate(b, style == 10 ? 1.1f : 1.3f);
            }
            else if (style == 4)
            {
                // POP: popping candy - a scatter of high, dry pops, each FALLING in pitch (a pop
                // that rises in the low mids is the fart; one that drops up high is a tick).
                PNoise(b, 0f, 0.012f, 0.45f, 0.0002f, 0.0025f, 3000f, 12000f, rng);
                PSine(b, 0f, 0.06f, 360f * j, 180f * j, 0.01f, 0.3f, 0.0005f, 0.022f);
                int pops = 8 + 5 * tier;
                for (int k = 0; k < pops; k++)
                {
                    float at = 0.004f + (run + 0.04f) * (float)rng.NextDouble();
                    float f = (1500f + 1500f * (float)rng.NextDouble()) * j;
                    float amp = 0.25f + 0.25f * (float)rng.NextDouble();
                    PNoise(b, at, 0.003f, amp * 0.6f, 0.0001f, 0.0006f, 3000f, 12000f, rng);
                    PSine(b, at, 0.03f, f * 1.6f, f, 0.003f, amp, 0.0002f, 0.007f);
                }
                PNoise(b, 0.004f, 0.15f, 0.08f, 0.003f, 0.04f, 2500f, 9000f, rng); // fizz
                Saturate(b, 1.3f);
            }
            else
            {
                // CHIP: a fast soft square-wave arpeggio - a tiny tonal "ta-da", one note more per
                // extra line - over a light crunch. The most Nintendo of them.
                Crunch(b, 0f, 0.45f, tier, j, run * 0.5f, rng);
                float[] arp = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.51f, 1567.98f };
                int notes = 3 + tier;
                for (int k = 0; k < notes; k++)
                {
                    bool last = k == notes - 1;
                    Square(b, 0.01f + 0.03f * k, arp[k + (tier == 0 ? 0 : 1)] * j,
                        last ? 0.2f : 0.16f, last ? 0.07f : 0.028f);
                }
                Saturate(b, 1.15f);
            }
            if (style < 3)
            {
                // The reward: a short rising pluck motif - two notes for a line, three for two,
                // four for more. The newer approaches carry their own reward.
                float[] motif = { 783.99f, 987.77f, 1174.66f, 1567.98f };
                for (int m = 0; m < 2 + tier; m++)
                {
                    Pluck(b, run * 0.5f + 0.045f * m, motif[m] * j, 0.22f, 0.992f, 0.45f, rng);
                }
            }
            else if (style != 5)
            {
                // ZIP and POP: one bright pluck at the end of the run - the "done".
                Pluck(b, run + 0.02f, (tier == 2 ? 1567.98f : 1174.66f) * j, 0.22f, 0.992f, 0.45f, rng);
            }
            HighPass(b, 140f);
            Room(b, 0.08f + 0.03f * tier, 0.6f);
            return PFinish("explodeM" + style + "+" + tier + "." + v, b, 0.88f);
        }

        /// <summary>The ZIP shwip: noise through a band whose high-pass (hp0 to hp1) and low-pass
        /// (lp0 to lp1) both climb across it, rising in over <paramref name="attackShare"/> of its
        /// length and thinning out toward the end - a bright rush travelling outward. No
        /// resonance, so it is air, never a whistle.</summary>
        private static void Zip(float[] b, float atSec, float len, float hp0, float hp1, float lp0,
            float lp1, float attackShare, float amp, System.Random rng)
        {
            int start = (int)(atSec * SampleRate);
            int n = (int)(len * SampleRate);
            float hp = 0f, l1 = 0f, l2 = 0f;
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float u = i / (float)n;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp += (x - hp) * OnePole(Mathf.Lerp(hp0, hp1, u));
                float a = OnePole(Mathf.Lerp(lp0, lp1, u));
                l1 += ((x - hp) - l1) * a;
                l2 += (l1 - l2) * a;
                float env = Mathf.Min(1f, u / attackShare) * Mathf.Pow(1f - u, 1.3f);
                b[start + i] += l2 * env * amp;
            }
        }

        /// <summary>A soft band-limited square note (odd harmonics, rolled off) with a quick
        /// attack and an exponential fade - a chiptune voice without the harshness of a raw square.
        /// </summary>
        private static void Square(float[] b, float atSec, float hz, float amp, float tau)
        {
            for (int h = 1; h <= 9; h += 2)
            {
                if (hz * h > SampleRate * 0.4f)
                {
                    break;
                }
                PSine(b, atSec, tau * 6f, hz * h, hz * h, 1f, amp / h * Mathf.Pow(0.8f, h - 1), 0.002f, tau);
            }
        }

        /// <summary>The CRUNCH that won: a bright contact, a short mid punch, a debris band that
        /// closes as it falls and a cloud of dry grains - shared by the line clear and the sweep's
        /// hit. Size scales the grain count and the debris length.</summary>
        private static void Crunch(float[] b, float at, float size, int tier, float j, float run,
            System.Random rng)
        {
            PNoise(b, at, 0.015f, 0.55f * size, 0.0002f, 0.003f, 2500f, 12000f, rng);
            PSine(b, at, 0.07f, 380f * j, 190f * j, 0.01f, 0.4f * size, 0.0005f, 0.025f);
            PNoiseSweep(b, at + 0.002f, 0.2f + 0.06f * tier, 0.35f * size, 0.001f, 0.035f + 0.012f * tier,
                1200f, 8000f, 3500f, rng);
            int grains = (int)((14 + 8 * tier) * size);
            for (int g = 0; g < grains; g++)
            {
                float t = at + 0.003f + (run + 0.05f) * (float)rng.NextDouble();
                float lo = 2000f + 3000f * (float)rng.NextDouble();
                PNoise(b, t, 0.006f, 0.15f + 0.2f * (float)rng.NextDouble(), 0.0001f, 0.0012f,
                    lo, lo * 2.5f, rng);
            }
        }

        /// <summary>
        /// The clean sweep - ONE hit, built as a phrase around it: a clean noise swell that opens up
        /// into the hit (no resonant whistle), the hit itself a big CRUNCH over MIX's thump, then a
        /// two-octave pluck run that swells as it climbs, a STRUMMED chord at the top with its high
        /// octave shimmering in a beat later, a sparkle of dry clicks and a breath of air. Its pitch
        /// still rises with every sweep in the round.
        /// </summary>
        private static AudioClip MixSweep(int v)
        {
            var rng = new System.Random(21000 + v);
            float[] b = Buffer(1.5f);
            const float hit = SweepHit;
            // Riser: noise whose level and low-pass both climb into the hit.
            PNoiseSweep(b, 0f, hit + 0.008f, 0.3f, hit, 0.006f, 300f, 700f, 7500f, rng);
            // The hit.
            Crunch(b, hit, 1.5f, 2, 1f, 0.1f, rng);
            // The run: two octaves of G major, swelling as it climbs.
            float[] run = { 392f, 493.88f, 587.33f, 783.99f, 987.77f, 1174.66f, 1567.98f };
            float d = v == 1 ? 1.003f : 1f;
            for (int i = 0; i < run.Length; i++)
            {
                float u = i / (float)(run.Length - 1);
                Pluck(b, hit + 0.05f + SweepStep * i, run[i] * d, 0.22f + 0.12f * u, 0.993f, 0.45f, rng);
            }
            // The chord, strummed upward, and its top octave shimmering in behind it.
            const float chordAt = SweepChord;
            float[] chord = { 392f, 783.99f, 987.77f, 1174.66f, 1567.98f };
            for (int i = 0; i < chord.Length; i++)
            {
                Pluck(b, chordAt + 0.012f * i, chord[i] * d, 0.3f, 0.995f, 0.4f, rng);
            }
            Pluck(b, chordAt + 0.09f, 1975.53f * d, 0.14f, 0.995f, 0.35f, rng);
            Pluck(b, chordAt + 0.13f, 2349.32f * d, 0.1f, 0.995f, 0.35f, rng);
            // Sparkle: tiny dry clicks scattered over the run and the chord.
            for (int s = 0; s < 22; s++)
            {
                float at = hit + 0.08f + 0.6f * (float)rng.NextDouble();
                float lo = 4000f + 4000f * (float)rng.NextDouble();
                PNoise(b, at, 0.004f, 0.08f + 0.08f * (float)rng.NextDouble(), 0.0001f, 0.0008f, lo, lo * 1.6f, rng);
            }
            PNoise(b, hit, 1.1f, 0.035f, 0.2f, 0.3f, 4000f, 11000f, rng); // air
            Saturate(b, 1.2f);
            HighPass(b, 110f);
            Room(b, 0.24f, 0.9f);
            return PFinish("sweepM+" + v, b, 0.9f);
        }

        /// <summary>NEW's thump on its own - the kick (and, for three lines or more, the
        /// aftershock) exactly as NEW builds it, with a small contact click so it is felt as a hit
        /// rather than heard as a hum. MIX plays it UNDER its line clear at MixThumpLevel.</summary>
        private static AudioClip MixThump(int tier, int v)
        {
            var rng = new System.Random(28000 + tier * 10 + v);
            float[] b = Buffer(0.45f + 0.15f * tier);
            float j = 1f + 0.035f * (v - 1f);
            PNoise(b, 0f, 0.006f, 0.15f, 0.0002f, 0.0015f, 1500f, 6000f, rng);
            PSine(b, 0f, 0.35f + 0.1f * tier, 155f * j, 46f - 4f * tier, 0.03f, 0.95f, 0.001f,
                0.12f + 0.05f * tier);
            if (tier == 2)
            {
                PSine(b, 0.06f, 0.4f, 70f, 38f, 0.1f, 0.35f, 0.01f, 0.18f);
            }
            Saturate(b, 1.6f);
            return PFinish("thumpM+" + tier + "." + v, b, 0.9f);
        }

        /// <summary>
        /// The explosion bed: the RUSH of a far-off blast - a band of mid noise (roughly 350 Hz to
        /// 2.4 kHz, the top closing as it fades) that swells in over ~20 ms, rolls a little and
        /// is gone. Two takes are buried here. The first snapped in bright with a big room and read
        /// as a GUNSHOT; the second was dark low noise with a little room, and a low rumble through
        /// the comb room is a boxy REVERBED THUMP - a second, worse thump under every style. So it
        /// now lives above the thump (which owns the low end), gets no room at all, and is short:
        /// a hint, not an event. A bigger clear is a slightly longer rush, never a louder one.
        /// </summary>
        private static AudioClip MixBlast(int tier, int v, float atSec)
        {
            var rng = new System.Random(30000 + tier * 10 + v);
            float len = 0.3f + 0.08f * tier;
            float[] b = Buffer(atSec + len + 0.02f);
            int start = (int)(atSec * SampleRate);
            int n = (int)(len * SampleRate);
            float hp = 0f, lp1 = 0f, lp2 = 0f, roll = 1f, rollTarget = 1f;
            int rollStep = (int)(0.025f * SampleRate);
            float tau = 0.07f + 0.025f * tier;
            float hpA = OnePole(350f);
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float t = i / (float)SampleRate;
                if (i % rollStep == 0)
                {
                    rollTarget = 0.7f + 0.3f * (float)rng.NextDouble();
                }
                roll += (rollTarget - roll) * 0.003f;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp += (x - hp) * hpA;
                float a = OnePole(Mathf.Lerp(2400f, 800f, Mathf.Clamp01(t / (tau * 3f))));
                lp1 += ((x - hp) - lp1) * a;
                lp2 += (lp1 - lp2) * a;
                float env = t < 0.02f ? Mathf.SmoothStep(0f, 1f, t / 0.02f) : Mathf.Exp(-(t - 0.02f) / tau);
                // The last few ms fade to nothing, so it never ends on a cut.
                float tail = Mathf.Clamp01((n - i) / (0.01f * SampleRate));
                b[start + i] += lp2 * env * roll * tail;
            }
            return PFinish("blastM+" + tier + "." + v, b, 0.85f);
        }

        /// <summary>The sweep's thump: ONE, under the hit.</summary>
        private static AudioClip MixSweepThump()
        {
            var rng = new System.Random(29000);
            float[] b = Buffer(0.7f);
            PNoise(b, SweepHit, 0.006f, 0.15f, 0.0002f, 0.0015f, 1500f, 6000f, rng);
            PSine(b, SweepHit, 0.5f, 160f, 42f, 0.035f, 0.95f, 0.001f, 0.17f);
            Saturate(b, 1.6f);
            return PFinish("sweepThumpM+", b, 0.9f);
        }

        /// <summary>Chimes and fusions: two muted plucks a fifth apart.</summary>
        private static AudioClip MixBell(int v)
        {
            var rng = new System.Random(22000 + v);
            float[] b = Buffer(0.7f);
            float j = 1f + 0.004f * (v - 1f);
            Pluck(b, 0f, 587.33f * j, 0.45f, 0.993f, 0.5f, rng);
            Pluck(b, 0.055f, 880f * j, 0.32f, 0.992f, 0.5f, rng);
            Room(b, 0.2f, 0.8f);
            return PFinish("bellM+" + v, b, 0.8f);
        }

        /// <summary>Buying: a soft felt press and two rising resonant bloops - something dropped
        /// neatly into place rather than a till.</summary>
        private static AudioClip MixBuy(int v)
        {
            var rng = new System.Random(24000 + v);
            float[] b = Buffer(0.4f);
            float j = 1f + 0.03f * (v - 1f);
            PNoise(b, 0f, 0.03f, 0.25f, 0.001f, 0.01f, 150f, 900f, rng); // felt
            ResonantClick(b, 0.005f, 420f * j, 1100f * j, 22f, 0.9f, 0.0015f, rng, 0.02f);
            ResonantClick(b, 0.075f, 640f * j, 1650f * j, 22f, 0.75f, 0.0015f, rng, 0.02f);
            Saturate(b, 1.3f);
            HighPass(b, 140f);
            Room(b, 0.12f, 0.55f);
            return PFinish("buyM+" + v, b, 0.8f);
        }

        /// <summary>A card vanishing: a "fwip" - a resonant sweep that swells upward and is cut off
        /// at its peak, as if the card were pulled out of the air.</summary>
        private static AudioClip MixVanish(int v)
        {
            var rng = new System.Random(25000 + v);
            float[] b = Buffer(0.3f);
            const float len = 0.14f;
            int n = (int)(len * SampleRate);
            var ex = new float[n];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                // Swell, then a hard stop at the top - the reverse of a hit.
                float env = Mathf.Pow(u, 1.8f) * (u > 0.92f ? (1f - u) / 0.08f : 1f);
                ex[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * env;
            }
            float jf = 1f + 0.06f * (v - 1f);
            Resonate(b, 0f, ex, u => Mathf.Lerp(500f, 3800f, u * u) * jf, 5f, 0.9f);
            HighPass(b, 250f);
            Room(b, 0.16f, 0.6f);
            return PFinish("vanishM+" + v, b, 0.75f);
        }

        /// <summary>
        /// The arena flames growing. FIRE IS BROADBAND AND DRY: the first take swept a resonant
        /// band-pass and popped resonant clicks, and resonance is exactly what makes a sound WET -
        /// it bubbled. Now nothing here rings. The roar is wide noise through a low-pass that
        /// flares open on ignition and settles, its level FLICKERING (smoothed random steps, the
        /// way a flame breathes); the crackle is sharp broadband snaps a fraction of a millisecond
        /// long, some in quick doublets like popping wood; a thin hiss sits on top.
        /// </summary>
        private static AudioClip MixFlame(int v)
        {
            var rng = new System.Random(26000 + v);
            float[] b = Buffer(0.95f);
            int n = (int)(0.85f * SampleRate);
            float lp1 = 0f, lp2 = 0f, sub = 0f;
            float flicker = 1f, flickerTarget = 1f;
            int flickerStep = (int)(0.018f * SampleRate);
            float jf = 1f + 0.05f * (v - 1f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                if (i % flickerStep == 0)
                {
                    flickerTarget = 0.55f + 0.45f * (float)rng.NextDouble();
                }
                flicker += (flickerTarget - flicker) * 0.004f;
                // Flare open on ignition, settle, and close as it dies.
                float cutoff = (t < 0.1f ? Mathf.Lerp(600f, 4200f, t / 0.1f)
                    : Mathf.Lerp(4200f, 1400f, Mathf.Clamp01((t - 0.1f) / 0.25f))
                      * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((t - 0.35f) / 0.5f))) * jf;
                float a = OnePole(cutoff);
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += (x - lp1) * a;
                lp2 += (lp1 - lp2) * a;
                sub += (x - sub) * OnePole(260f);
                float env = t < 0.06f ? t / 0.06f : Mathf.Exp(-(t - 0.06f) / 0.32f);
                b[i] += (lp2 * 1.6f + sub * 1.2f) * env * flicker * 0.55f;
            }
            // Crackle: dry broadband snaps, thinning out as the flame settles.
            for (int c = 0; c < 46; c++)
            {
                float at = 0.02f + 0.75f * (float)System.Math.Pow(rng.NextDouble(), 1.4);
                float amp = (0.25f + 0.55f * (float)rng.NextDouble()) * Mathf.Exp(-at / 0.45f);
                Crackle(b, at, amp, rng);
                if (rng.NextDouble() < 0.3)
                {
                    Crackle(b, at + 0.004f + 0.008f * (float)rng.NextDouble(), amp * 0.6f, rng);
                }
            }
            PNoise(b, 0.02f, 0.75f, 0.05f, 0.05f, 0.3f, 5000f, 12000f, rng); // hiss
            HighPass(b, 80f);
            Saturate(b, 1.3f);
            Room(b, 0.06f, 0.6f);
            return PFinish("flameM+" + v, b, 0.85f);
        }

        /// <summary>A fuse taking a charge: a thin sputter of gated hiss and a few dry snaps -
        /// short and light, because several can fire in one turn.</summary>
        private static AudioClip MixFuse(int v)
        {
            var rng = new System.Random(27000 + v);
            float[] b = Buffer(0.22f);
            int bursts = 7 + v;
            for (int k = 0; k < bursts; k++)
            {
                float at = 0.01f * k + 0.012f * (float)rng.NextDouble() * k;
                float amp = (0.12f + 0.14f * (float)rng.NextDouble()) * (1f - k / (float)(bursts + 2));
                PNoise(b, at, 0.02f, amp, 0.0005f, 0.006f, 3500f, 10000f, rng);
            }
            for (int p = 0; p < 5; p++)
            {
                Crackle(b, 0.015f + 0.14f * (float)rng.NextDouble(), 0.4f + 0.3f * (float)rng.NextDouble(), rng);
            }
            HighPass(b, 400f);
            Room(b, 0.04f, 0.35f);
            return PFinish("fuseM+" + v, b, 0.7f);
        }

        /// <summary>One crackle: a broadband snap a fraction of a millisecond long, with no
        /// resonance under it - a resonant pop is a bubble, a bare impulse is a spark.</summary>
        private static void Crackle(float[] b, float atSec, float amp, System.Random rng)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min(b.Length - start, 40);
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Exp(-i / 7f);
                b[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0) * amp * env;
            }
        }

        // ---- MIX's two instruments ----

        /// <summary>A resonant band-pass (Chamberlin state-variable filter) swept while it rings:
        /// <paramref name="excitation"/> is fed through it with the centre frequency
        /// <paramref name="freqAt"/>(0..1 through the excitation, held after), and the ring is let
        /// run out past the end of the excitation.</summary>
        private static void Resonate(float[] b, float atSec, float[] excitation,
            System.Func<float, float> freqAt, float q, float amp)
        {
            int start = (int)(atSec * SampleRate);
            float damp = 1f / Mathf.Max(0.5f, q);
            float low = 0f, band = 0f;
            int n = b.Length - start;
            float lastF = freqAt(1f);
            for (int i = 0; i < n; i++)
            {
                float x = i < excitation.Length ? excitation[i] : 0f;
                float hz = i < excitation.Length ? freqAt(i / (float)excitation.Length) : lastF;
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Clamp(hz, 20f, SampleRate * 0.16f) / SampleRate);
                low += f * band;
                float high = x - low - damp * band;
                band += f * high;
                b[start + i] += band * amp;
                if (i > excitation.Length && Mathf.Abs(band) < 1e-5f && Mathf.Abs(low) < 1e-5f)
                {
                    break;
                }
            }
        }

        /// <summary>A short click into a resonant glide from f0 to f1 (over glideSec, or over the
        /// click when zero) - a pop, a tap, or with a high Q and a wide glide a "bloop".</summary>
        private static void ResonantClick(float[] b, float atSec, float f0, float f1, float q,
            float amp, float clickSec, System.Random rng, float glideSec = 0f)
        {
            int n = Mathf.Max(8, (int)(Mathf.Max(clickSec, glideSec) * SampleRate));
            int clickN = Mathf.Max(4, (int)(clickSec * SampleRate));
            var ex = new float[n];
            for (int i = 0; i < clickN; i++)
            {
                ex[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - i / (float)clickN) * 2f;
            }
            if (atSec * SampleRate >= b.Length)
            {
                return;
            }
            // A resonator is normalised by its Q so a sharp one is not louder than a soft one.
            Resonate(b, atSec, ex, u => Mathf.Lerp(f0, f1, 1f - (1f - u) * (1f - u)), q,
                amp * 4f / Mathf.Sqrt(q));
        }

        /// <summary>A muted plucked string (Karplus-Strong): a burst of filtered noise circulating
        /// in a delay one period long, through a loop filter that both decays it
        /// (<paramref name="feedback"/>) and darkens it (<paramref name="mute"/> 0..1, higher is
        /// duller) - which is what keeps it warm and stops it ringing.</summary>
        private static void Pluck(float[] b, float atSec, float hz, float amp, float feedback,
            float mute, System.Random rng)
        {
            int start = (int)(atSec * SampleRate);
            int period = Mathf.Max(2, Mathf.RoundToInt(SampleRate / hz));
            var line = new float[period];
            float lp = 0f;
            for (int i = 0; i < period; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (x - lp) * 0.5f; // a softer pick than raw noise
                line[i] = lp;
            }
            int n = Mathf.Min(b.Length - start, (int)(1.2f * SampleRate));
            int p = 0;
            float prev = 0f, tone = 0f;
            float a = 1f - Mathf.Clamp01(mute) * 0.85f;
            for (int i = 0; i < n; i++)
            {
                float cur = line[p];
                float avg = 0.5f * (cur + prev);
                tone += (avg - tone) * a;
                prev = cur;
                line[p] = tone * feedback;
                p = (p + 1) % period;
                // A few ms of fade-in so the pick is not a click.
                float fade = i < 60 ? i / 60f : 1f;
                b[start + i] += cur * amp * fade;
            }
        }
    }
}
