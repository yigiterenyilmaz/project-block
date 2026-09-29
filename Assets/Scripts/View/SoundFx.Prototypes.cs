// PURPOSE: PROTOTYPES for the sounds the game is MISSING or leans on a stand-in for - three takes
// each (A, B, C) in the MIX palette, for the sound lab's "Missing & weak sounds" group. None of
// these is wired into the game yet: they are here to be picked by ear first, and only a picked
// one gets hooked to its event.
//
// Four families:
//   - JOKER EFFECTS THAT ARE SILENT: nine views already announce their beats through a static
//     `Sounded` event (SnakeEatView, SnakeDefeatView, HazineRevealView, IceFreezeView,
//     IgnitionBurnView, QuakeCollapseView, QuarryBreakView, RebateView, ChallengeContractView) and
//     nothing listens. One prototype per effect's HERO moment here; the beats around it follow once
//     the voice is chosen.
//   - ROUND FLOW & FEEDBACK: the score ticking up, the bar reached, a round won or lost, overtime,
//     a joker firing, a power used or recharged, the dead-end warning, the dead zone growing.
//   - CARDS, DECK & MARKET: dealing the hand, a card freezing and thawing, the market opening and
//     closing.
//   - STAND-INS TO REPLACE: `Buy` answers 16 different events and `Shuffle` 13 - rerolling,
//     selling, confirming and hovering deserve voices of their own.
// Clips are built LAZILY on first play, so the prototypes cost nothing until the lab asks.
// Same rules as the rest of MIX: no inharmonic ting, no resonance on anything dry, no low
// upward-gliding pops, no big room on a low sound.
// EXTENSION POINT: a new prototype is one enum value, one row in ProtoInfo and one case in
// BuildProto.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The missing / weak sounds with prototypes, in the lab's order.</summary>
    public enum ProtoCue
    {
        // Joker effects that are silent
        SnakeBite, SnakeDefeat, HazineTreasure, HazineDynamite, IceFreeze, IgnitionRow, Quake,
        QuarryBreak, RebateStamp, ChallengeWon, ChallengeMissed,
        // Round flow & feedback
        ScoreTick, GoalReached, RoundWon, RoundLost, OvertimeStart, JokerProc, PowerUse,
        PowerRecharge, DeadEndWarning, DeadZoneGrows,
        // Cards, deck & market
        DealHand, CardFreeze, CardThaw, MarketOpen, MarketClose,
        // Stand-ins to replace
        Reroll, Sell, UiConfirm, UiHover,
    }

    public sealed partial class SoundFx
    {
        public static readonly string[] ProtoVersionNames = { "A", "B", "C" };

        /// <summary>The lab's family (0 joker effects, 1 round flow, 2 cards &amp; market, 3
        /// stand-ins), label and Turkish label for each prototype.</summary>
        public static void ProtoInfo(ProtoCue cue, out int family, out string en, out string tr)
        {
            switch (cue)
            {
                case ProtoCue.SnakeBite: family = 0; en = "Yılan: the bite"; tr = "Yılan: ısırık"; return;
                case ProtoCue.SnakeDefeat: family = 0; en = "Yılan: beaten, lets go"; tr = "Yılan: yenildi"; return;
                case ProtoCue.HazineTreasure: family = 0; en = "Hazine: treasure found"; tr = "Hazine: hazine"; return;
                case ProtoCue.HazineDynamite: family = 0; en = "Hazine: dynamite"; tr = "Hazine: dinamit"; return;
                case ProtoCue.IceFreeze: family = 0; en = "Buzluk: water freezes"; tr = "Buzluk: donma"; return;
                case ProtoCue.IgnitionRow: family = 0; en = "Tutuştur: a row burns"; tr = "Tutuştur: sıra yanar"; return;
                case ProtoCue.Quake: family = 0; en = "Deprem: the collapse"; tr = "Deprem: çöküş"; return;
                case ProtoCue.QuarryBreak: family = 0; en = "Elmas Kazma: stone breaks"; tr = "Elmas Kazma: taş kırılır"; return;
                case ProtoCue.RebateStamp: family = 0; en = "Harcama bonusu: stamped"; tr = "Harcama bonusu: damga"; return;
                case ProtoCue.ChallengeWon: family = 0; en = "Meydan Okuma: dare won"; tr = "Meydan Okuma: kazanıldı"; return;
                case ProtoCue.ChallengeMissed: family = 0; en = "Meydan Okuma: dare missed"; tr = "Meydan Okuma: kaçtı"; return;
                case ProtoCue.ScoreTick: family = 1; en = "score ticking up"; tr = "skor sayarken"; return;
                case ProtoCue.GoalReached: family = 1; en = "the bar reached"; tr = "eşiğe ulaşıldı"; return;
                case ProtoCue.RoundWon: family = 1; en = "round won"; tr = "raunt kazanıldı"; return;
                case ProtoCue.RoundLost: family = 1; en = "round lost"; tr = "raunt kaybedildi"; return;
                case ProtoCue.OvertimeStart: family = 1; en = "overtime begins"; tr = "uzatma başlar"; return;
                case ProtoCue.JokerProc: family = 1; en = "a joker fires"; tr = "joker tetiklenir"; return;
                case ProtoCue.PowerUse: family = 1; en = "a power used"; tr = "güç kullanıldı"; return;
                case ProtoCue.PowerRecharge: family = 1; en = "a power recharged"; tr = "güç doldu"; return;
                case ProtoCue.DeadEndWarning: family = 1; en = "no move left (warning)"; tr = "hamle kalmadı"; return;
                case ProtoCue.DeadZoneGrows: family = 1; en = "the dead zone grows"; tr = "ölü bölge büyür"; return;
                case ProtoCue.DealHand: family = 2; en = "dealing the hand"; tr = "el dağıtılır"; return;
                case ProtoCue.CardFreeze: family = 2; en = "a card freezes"; tr = "kart donar"; return;
                case ProtoCue.CardThaw: family = 2; en = "a card thaws"; tr = "kart çözülür"; return;
                case ProtoCue.MarketOpen: family = 2; en = "market opens"; tr = "market açılır"; return;
                case ProtoCue.MarketClose: family = 2; en = "market closes"; tr = "market kapanır"; return;
                case ProtoCue.Reroll: family = 3; en = "reroll (now: buy)"; tr = "yenile (şimdi: satın al)"; return;
                case ProtoCue.Sell: family = 3; en = "sell (now: buy)"; tr = "sat (şimdi: satın al)"; return;
                case ProtoCue.UiConfirm: family = 3; en = "confirm (now: buy/shuffle)"; tr = "onay"; return;
                default: family = 3; en = "hover tick (now: none)"; tr = "üzerine gelme"; return;
            }
        }

        private readonly Dictionary<int, AudioClip> protoClips = new Dictionary<int, AudioClip>();

        /// <summary>Plays one take (0..2) of a prototype. Built the first time it is asked for.
        /// </summary>
        public void AuditionProto(ProtoCue cue, int version)
        {
            int key = (int)cue * 10 + version;
            AudioClip clip;
            if (!protoClips.TryGetValue(key, out clip))
            {
                clip = BuildProto(cue, version, new System.Random(50000 + key), "proto" + cue + "+" + version);
                protoClips[key] = clip;
            }
            PlayPolished(clip, 1f, 0.9f);
        }

        // ---- two small shared shapes ----

        /// <summary>A run of notes as plucks (kind 0) or soft squares (kind 1).</summary>
        private static void Arp(float[] b, float at, float[] notes, float step, float amp, int kind,
            System.Random rng, float lastTau = 0f)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                bool last = i == notes.Length - 1;
                if (kind == 0)
                {
                    Pluck(b, at + step * i, notes[i], amp, last ? 0.995f : 0.992f, 0.45f, rng);
                }
                else
                {
                    Square(b, at + step * i, notes[i], amp * 0.6f, last && lastTau > 0f ? lastTau : Mathf.Min(0.05f, step));
                }
            }
        }

        /// <summary>A small cluster of dry high sparkle clicks.</summary>
        private static void Sparkle(float[] b, float at, float span, int count, float amp, System.Random rng)
        {
            for (int s = 0; s < count; s++)
            {
                float t = at + span * (float)rng.NextDouble();
                float lo = 4000f + 4000f * (float)rng.NextDouble();
                PNoise(b, t, 0.004f, amp * (0.5f + 0.5f * (float)rng.NextDouble()), 0.0001f, 0.0008f, lo, lo * 1.6f, rng);
            }
        }

        private static readonly float[] Up4 = { 523.25f, 659.25f, 783.99f, 1046.5f };
        private static readonly float[] Down3 = { 783.99f, 659.25f, 523.25f };

        private static AudioClip BuildProto(ProtoCue cue, int v, System.Random rng, string name)
        {
            float[] b;
            switch (cue)
            {
                // ---------------- joker effects ----------------
                case ProtoCue.SnakeBite:
                    b = Buffer(0.5f);
                    if (v == 0) { Zip(b, 0f, 0.07f, 800f, 3000f, 2500f, 9000f, 0.6f, 0.5f, rng); Crunch(b, 0.07f, 0.8f, 1, 1f, 0.05f, rng); Thump(b, 0.2f, 0.45f, 110f, 60f, 0.06f); }
                    else if (v == 1) { Square(b, 0f, 392f, 0.14f, 0.03f); Square(b, 0.05f, 523.25f, 0.18f, 0.04f); Knock(b, 0.05f, 500f, 0.7f, rng); Thump(b, 0.18f, 0.4f, 110f, 60f, 0.06f); }
                    else { Knock(b, 0f, 600f, 0.9f, rng); Knock(b, 0.06f, 420f, 0.7f, rng); Pluck(b, 0.2f, 196f, 0.4f, 0.99f, 0.6f, rng); }
                    break;
                case ProtoCue.SnakeDefeat:
                    b = Buffer(1.3f);
                    if (v == 0) { Arp(b, 0f, new[] { 1046.5f, 880f, 783.99f, 659.25f, 523.25f }, 0.09f, 0.3f, 0, rng); Rush(b, 0.3f, 0.8f, 0.2f, 0.3f, 0.5f, 1500f, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 1046.5f, 880f, 783.99f, 659.25f, 523.25f }, 0.07f, 0.3f, 1, rng, 0.2f); }
                    else { PassBy(b, 0.5f, rng); Arp(b, 0.35f, new[] { 392f, 523.25f, 659.25f, 783.99f }, 0.012f, 0.28f, 0, rng); }
                    break;
                case ProtoCue.HazineTreasure:
                    b = Buffer(0.9f);
                    if (v == 0) { ResonantClick(b, 0f, 900f, 1600f, 18f, 0.6f, 0.0015f, rng, 0.02f); ResonantClick(b, 0.07f, 1300f, 2300f, 18f, 0.5f, 0.0015f, rng, 0.02f); Arp(b, 0.14f, new[] { 783.99f, 987.77f, 1174.66f }, 0.012f, 0.28f, 0, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 783.99f, 987.77f, 1174.66f, 1567.98f, 1975.53f }, 0.05f, 0.3f, 1, rng, 0.2f); }
                    else { Sparkle(b, 0f, 0.4f, 24, 0.14f, rng); Arp(b, 0.03f, Up4, 0.05f, 0.3f, 0, rng); }
                    break;
                case ProtoCue.HazineDynamite:
                    b = Buffer(0.8f);
                    if (v == 0) { for (int k = 0; k < 6; k++) { Crackle(b, 0.02f * k, 0.4f, rng); } Crunch(b, 0.14f, 1.2f, 2, 1f, 0.08f, rng); Thump(b, 0.14f, 0.8f, 140f, 45f, 0.14f); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 392f, 261.63f }, 0.05f, 0.3f, 1, rng, 0.08f); Thump(b, 0.15f, 0.8f, 140f, 45f, 0.14f); Rush(b, 0.15f, 0.5f, 0.01f, 0.12f, 0.6f, 2200f, rng); }
                    else { Rush(b, 0f, 0.6f, 0.005f, 0.15f, 0.9f, 2400f, rng); Crunch(b, 0f, 1.2f, 2, 1f, 0.1f, rng); Thump(b, 0f, 0.7f, 140f, 45f, 0.14f); }
                    break;
                case ProtoCue.IceFreeze:
                    b = Buffer(0.9f);
                    if (v == 0) { PNoiseSweep(b, 0f, 0.7f, 0.18f, 0.25f, 0.2f, 3000f, 5000f, 11000f, rng); for (int k = 0; k < 30; k++) { Crackle(b, 0.05f + 0.6f * (float)rng.NextDouble(), 0.12f, rng); } }
                    else if (v == 1) { for (int k = 0; k < 7; k++) { Pluck(b, 0.05f * k + 0.01f * (float)rng.NextDouble(), 1567.98f * (1f + 0.25f * (k % 3)), 0.12f, 0.99f, 0.3f, rng); } PNoise(b, 0f, 0.5f, 0.04f, 0.2f, 0.2f, 5000f, 12000f, rng); }
                    else { Arp(b, 0f, new[] { 1567.98f, 2093f, 2637f }, 0.06f, 0.2f, 1, rng, 0.1f); Zip(b, 0f, 0.3f, 3000f, 6000f, 8000f, 14000f, 0.5f, 0.25f, rng); }
                    break;
                case ProtoCue.IgnitionRow:
                    b = Buffer(0.5f);
                    if (v == 0) { Rush(b, 0f, 0.45f, 0.03f, 0.1f, 0.8f, 2600f, rng); for (int k = 0; k < 12; k++) { Crackle(b, 0.02f + 0.3f * (float)rng.NextDouble(), 0.35f, rng); } }
                    else if (v == 1) { for (int k = 0; k < 18; k++) { Crackle(b, 0.25f * (float)System.Math.Pow(rng.NextDouble(), 1.5), 0.5f, rng); } PNoise(b, 0f, 0.3f, 0.08f, 0.02f, 0.08f, 3000f, 9000f, rng); }
                    else { Zip(b, 0f, 0.2f, 600f, 2500f, 1500f, 7000f, 0.3f, 0.6f, rng); Rush(b, 0.02f, 0.4f, 0.02f, 0.1f, 0.5f, 1800f, rng); }
                    break;
                case ProtoCue.Quake:
                    b = Buffer(0.9f);
                    if (v == 0) { Rush(b, 0f, 0.8f, 0.05f, 0.25f, 0.8f, 900f, rng); for (int k = 0; k < 4; k++) { Knock(b, 0.1f + 0.14f * k, 240f, 0.5f, rng); } }
                    else if (v == 1) { Thump(b, 0f, 0.7f, 110f, 45f, 0.16f); for (int k = 0; k < 40; k++) { float u = (float)rng.NextDouble(); PNoise(b, 0.03f + 0.6f * u, 0.006f, 0.2f * (1f - 0.6f * u), 0.0001f, 0.0015f, 1200f, 4000f, rng); } }
                    else { for (int k = 0; k < 4; k++) { Thump(b, 0.18f * k, 0.5f - 0.08f * k, 100f, 50f, 0.06f); Rush(b, 0.18f * k, 0.16f, 0.01f, 0.05f, 0.4f, 1200f, rng); } }
                    break;
                case ProtoCue.QuarryBreak:
                    b = Buffer(0.45f);
                    if (v == 0) { Knock(b, 0f, 700f, 1f, rng); Crunch(b, 0.01f, 0.9f, 1, 1.1f, 0.06f, rng); }
                    else if (v == 1) { Knock(b, 0f, 450f, 1f, rng); for (int k = 0; k < 9; k++) { float lo = 800f + 1500f * (float)rng.NextDouble(); PNoise(b, 0.01f + 0.1f * (float)rng.NextDouble(), 0.014f, 0.4f, 0.0002f, 0.003f, lo, lo * 3f, rng); } }
                    else { Knock(b, 0f, 800f, 0.9f, rng); Knock(b, 0.035f, 600f, 0.7f, rng); Sparkle(b, 0.03f, 0.12f, 12, 0.2f, rng); }
                    break;
                case ProtoCue.RebateStamp:
                    b = Buffer(0.6f);
                    if (v == 0) { Knock(b, 0f, 380f, 0.9f, rng); Arp(b, 0.06f, new[] { 783.99f, 1174.66f }, 0.07f, 0.3f, 0, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 1174.66f, 1567.98f }, 0.07f, 0.35f, 1, rng, 0.12f); }
                    else { PNoise(b, 0f, 0.03f, 0.3f, 0.001f, 0.01f, 150f, 900f, rng); ResonantClick(b, 0.01f, 700f, 1500f, 20f, 0.8f, 0.0015f, rng, 0.02f); }
                    break;
                case ProtoCue.ChallengeWon:
                    b = Buffer(0.9f);
                    if (v == 0) { Arp(b, 0f, Up4, 0.06f, 0.32f, 0, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f }, 0.06f, 0.3f, 1, rng, 0.25f); }
                    else { Zip(b, 0f, 0.12f, 900f, 3500f, 2500f, 12000f, 0.2f, 0.6f, rng); Arp(b, 0.1f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.01f, 0.28f, 0, rng); }
                    break;
                case ProtoCue.ChallengeMissed:
                    b = Buffer(0.7f);
                    if (v == 0) { Arp(b, 0f, new[] { 587.33f, 440f }, 0.12f, 0.3f, 0, rng); Knock(b, 0.24f, 300f, 0.5f, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 466.16f, 392f }, 0.1f, 0.3f, 1, rng, 0.15f); }
                    else { Knock(b, 0f, 250f, 0.8f, rng); Rush(b, 0.02f, 0.5f, 0.05f, 0.15f, 0.4f, 900f, rng); }
                    break;

                // ---------------- round flow & feedback ----------------
                case ProtoCue.ScoreTick:
                    b = Buffer(0.08f);
                    if (v == 0) { PNoise(b, 0f, 0.004f, 0.4f, 0.0001f, 0.0008f, 3000f, 12000f, rng); PSine(b, 0f, 0.02f, 2400f, 2000f, 0.005f, 0.12f, 0.0002f, 0.004f); }
                    else if (v == 1) { Square(b, 0f, 1567.98f, 0.18f, 0.012f); }
                    else { Pluck(b, 0f, 1567.98f, 0.3f, 0.97f, 0.5f, rng); }
                    break;
                case ProtoCue.GoalReached:
                    b = Buffer(1.1f);
                    if (v == 0) { Arp(b, 0f, new[] { 523.25f, 659.25f, 783.99f }, 0.015f, 0.34f, 0, rng); Arp(b, 0.18f, new[] { 1046.5f, 1318.51f }, 0.015f, 0.26f, 0, rng); Thump(b, 0f, 0.35f, 130f, 60f, 0.08f); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.07f, 0.34f, 1, rng, 0.3f); }
                    else { Zip(b, 0f, 0.15f, 700f, 3000f, 2000f, 11000f, 0.8f, 0.5f, rng); Arp(b, 0.15f, new[] { 783.99f, 1046.5f, 1318.51f }, 0.012f, 0.3f, 0, rng); Sparkle(b, 0.18f, 0.5f, 16, 0.1f, rng); }
                    break;
                case ProtoCue.RoundWon:
                    b = Buffer(1.5f);
                    if (v == 0) { Arp(b, 0f, new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f }, 0.08f, 0.32f, 0, rng); Arp(b, 0.45f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.012f, 0.3f, 0, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 523.25f, 523.25f, 659.25f, 783.99f, 1046.5f }, 0.1f, 0.34f, 1, rng, 0.4f); }
                    else { Crunch(b, 0f, 0.8f, 1, 1f, 0.06f, rng); Thump(b, 0f, 0.5f, 140f, 50f, 0.12f); Arp(b, 0.06f, new[] { 392f, 493.88f, 587.33f, 783.99f, 987.77f, 1174.66f }, 0.045f, 0.3f, 0, rng); Sparkle(b, 0.1f, 0.8f, 22, 0.1f, rng); }
                    break;
                case ProtoCue.RoundLost:
                    b = Buffer(1.4f);
                    if (v == 0) { Arp(b, 0f, new[] { 659.25f, 587.33f, 523.25f, 440f }, 0.2f, 0.3f, 0, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 493.88f, 466.16f, 440f }, 0.18f, 0.3f, 1, rng, 0.5f); }
                    else { Thump(b, 0f, 0.7f, 110f, 40f, 0.25f); Rush(b, 0f, 1.2f, 0.08f, 0.4f, 0.6f, 800f, rng); Pluck(b, 0.05f, 110f, 0.4f, 0.997f, 0.6f, rng); }
                    break;
                case ProtoCue.OvertimeStart:
                    b = Buffer(1.0f);
                    if (v == 0) { Zip(b, 0f, 0.5f, 200f, 1500f, 600f, 6000f, 0.95f, 0.6f, rng); Thump(b, 0.5f, 0.7f, 130f, 50f, 0.12f); Thump(b, 0.72f, 0.5f, 130f, 50f, 0.1f); }
                    else if (v == 1) { for (int k = 0; k < 4; k++) { Square(b, 0.2f * k, k % 2 == 0 ? 392f : 523.25f, 0.2f, 0.08f); } }
                    else { for (int k = 0; k < 3; k++) { float at = 0.3f * k; Thump(b, at, 0.6f, 120f, 55f, 0.05f); Thump(b, at + 0.12f, 0.4f, 120f, 55f, 0.05f); } }
                    break;
                case ProtoCue.JokerProc:
                    b = Buffer(0.3f);
                    if (v == 0) { Pluck(b, 0f, 1174.66f, 0.35f, 0.99f, 0.45f, rng); PNoise(b, 0f, 0.004f, 0.2f, 0.0001f, 0.001f, 3000f, 11000f, rng); }
                    else if (v == 1) { Square(b, 0f, 987.77f, 0.14f, 0.02f); Square(b, 0.035f, 1318.51f, 0.14f, 0.04f); }
                    else { ResonantClick(b, 0f, 900f, 1700f, 16f, 0.6f, 0.0015f, rng, 0.015f); }
                    break;
                case ProtoCue.PowerUse:
                    b = Buffer(0.6f);
                    if (v == 0) { Zip(b, 0f, 0.14f, 700f, 3500f, 2000f, 11000f, 0.8f, 0.6f, rng); Pluck(b, 0.13f, 1174.66f, 0.3f, 0.99f, 0.45f, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 783.99f, 1046.5f }, 0.04f, 0.3f, 1, rng, 0.1f); }
                    else { Rush(b, 0f, 0.3f, 0.12f, 0.06f, 0.6f, 2600f, rng); Knock(b, 0.13f, 700f, 0.6f, rng); }
                    break;
                case ProtoCue.PowerRecharge:
                    b = Buffer(0.7f);
                    if (v == 0) { Arp(b, 0f, new[] { 659.25f, 783.99f, 987.77f }, 0.06f, 0.3f, 0, rng); }
                    else if (v == 1) { Arp(b, 0f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.035f, 0.3f, 1, rng, 0.12f); }
                    else { Sparkle(b, 0f, 0.3f, 18, 0.14f, rng); Pluck(b, 0.2f, 1567.98f, 0.3f, 0.992f, 0.4f, rng); }
                    break;
                case ProtoCue.DeadEndWarning:
                    b = Buffer(0.8f);
                    if (v == 0) { for (int k = 0; k < 2; k++) { Knock(b, 0.25f * k, 220f, 0.8f, rng); Thump(b, 0.25f * k, 0.4f, 100f, 60f, 0.05f); } }
                    else if (v == 1) { Square(b, 0f, 220f, 0.25f, 0.1f); Square(b, 0.25f, 207.65f, 0.25f, 0.15f); }
                    else { for (int k = 0; k < 3; k++) { Rush(b, 0.22f * k, 0.18f, 0.05f, 0.06f, 0.6f, 900f, rng); } }
                    break;
                case ProtoCue.DeadZoneGrows:
                    b = Buffer(0.8f);
                    if (v == 0) { Crunch(b, 0f, 0.9f, 1, 0.8f, 0.08f, rng); Thump(b, 0f, 0.6f, 100f, 40f, 0.14f); }
                    else if (v == 1) { for (int k = 0; k < 45; k++) { float u = (float)rng.NextDouble(); PNoise(b, 0.6f * u, 0.006f, 0.25f * (1f - 0.5f * u), 0.0001f, 0.0015f, 900f, 3500f, rng); } Thump(b, 0f, 0.4f, 90f, 45f, 0.12f); }
                    else { Rush(b, 0f, 0.7f, 0.1f, 0.25f, 0.8f, 700f, rng); Pluck(b, 0.05f, 98f, 0.4f, 0.996f, 0.6f, rng); }
                    break;

                // ---------------- cards, deck & market ----------------
                case ProtoCue.DealHand:
                    b = Buffer(0.6f);
                    for (int k = 0; k < 5; k++)
                    {
                        float at = 0.085f * k;
                        if (v == 0) { Zip(b, at, 0.05f, 1500f, 4000f, 4000f, 11000f, 0.4f, 0.35f, rng); PNoise(b, at + 0.045f, 0.004f, 0.3f, 0.0001f, 0.001f, 2500f, 10000f, rng); }
                        else if (v == 1) { PNoise(b, at, 0.03f, 0.25f, 0.003f, 0.01f, 1500f, 7000f, rng); }
                        else { Knock(b, at, 900f + 60f * k, 0.5f, rng); }
                    }
                    break;
                case ProtoCue.CardFreeze:
                    b = Buffer(0.5f);
                    if (v == 0) { for (int k = 0; k < 20; k++) { Crackle(b, 0.3f * (float)rng.NextDouble(), 0.2f, rng); } PNoise(b, 0f, 0.35f, 0.06f, 0.08f, 0.1f, 5000f, 12000f, rng); }
                    else if (v == 1) { Square(b, 0f, 2093f, 0.12f, 0.03f); Square(b, 0.05f, 1567.98f, 0.12f, 0.06f); }
                    else { Zip(b, 0f, 0.25f, 6000f, 2500f, 14000f, 6000f, 0.1f, 0.5f, rng); }
                    break;
                case ProtoCue.CardThaw:
                    b = Buffer(0.5f);
                    if (v == 0) { Pluck(b, 0f, 1567.98f, 0.25f, 0.99f, 0.4f, rng); Pluck(b, 0.08f, 1174.66f, 0.22f, 0.99f, 0.45f, rng); }
                    else if (v == 1) { Rush(b, 0f, 0.4f, 0.06f, 0.12f, 0.5f, 3000f, rng); }
                    else { for (int k = 0; k < 10; k++) { Crackle(b, 0.25f * (float)rng.NextDouble(), 0.15f, rng); } Zip(b, 0.05f, 0.25f, 2500f, 800f, 8000f, 2500f, 0.2f, 0.3f, rng); }
                    break;
                case ProtoCue.MarketOpen:
                    b = Buffer(0.8f);
                    if (v == 0) { Arp(b, 0f, new[] { 392f, 493.88f, 587.33f, 783.99f }, 0.015f, 0.3f, 0, rng); }
                    else if (v == 1) { Zip(b, 0f, 0.2f, 500f, 2500f, 1500f, 8000f, 0.7f, 0.5f, rng); Knock(b, 0.2f, 450f, 0.7f, rng); }
                    else { Arp(b, 0f, new[] { 392f, 523.25f, 659.25f }, 0.05f, 0.3f, 1, rng, 0.15f); }
                    break;
                case ProtoCue.MarketClose:
                    b = Buffer(0.7f);
                    if (v == 0) { Knock(b, 0f, 400f, 0.8f, rng); Arp(b, 0.03f, new[] { 783.99f, 587.33f }, 0.08f, 0.26f, 0, rng); }
                    else if (v == 1) { Zip(b, 0f, 0.2f, 2500f, 500f, 8000f, 1500f, 0.1f, 0.5f, rng); Knock(b, 0.18f, 380f, 0.7f, rng); }
                    else { Arp(b, 0f, Down3, 0.05f, 0.3f, 1, rng, 0.12f); }
                    break;

                // ---------------- stand-ins to replace ----------------
                case ProtoCue.Reroll:
                    b = Buffer(0.45f);
                    if (v == 0) { for (int k = 0; k < 4; k++) { Knock(b, 0.05f * k + 0.01f * (float)rng.NextDouble(), 800f + 150f * (k % 2), 0.6f, rng); } }
                    else if (v == 1) { Zip(b, 0f, 0.1f, 1500f, 4000f, 4000f, 12000f, 0.5f, 0.5f, rng); Zip(b, 0.1f, 0.1f, 1500f, 4000f, 4000f, 12000f, 0.5f, 0.5f, rng); PNoise(b, 0.2f, 0.004f, 0.3f, 0.0001f, 0.001f, 2500f, 10000f, rng); }
                    else { Arp(b, 0f, new[] { 659.25f, 783.99f, 659.25f, 987.77f }, 0.04f, 0.28f, 1, rng, 0.06f); }
                    break;
                case ProtoCue.Sell:
                    b = Buffer(0.5f);
                    if (v == 0) { Arp(b, 0f, new[] { 1046.5f, 783.99f }, 0.07f, 0.3f, 0, rng); Knock(b, 0.14f, 500f, 0.5f, rng); }
                    else if (v == 1) { ResonantClick(b, 0f, 1700f, 900f, 18f, 0.6f, 0.0015f, rng, 0.02f); ResonantClick(b, 0.07f, 1300f, 700f, 18f, 0.5f, 0.0015f, rng, 0.02f); }
                    else { PNoise(b, 0f, 0.02f, 0.3f, 0.001f, 0.006f, 800f, 5000f, rng); Pluck(b, 0.01f, 783.99f, 0.3f, 0.99f, 0.5f, rng); }
                    break;
                case ProtoCue.UiConfirm:
                    b = Buffer(0.3f);
                    if (v == 0) { Pluck(b, 0f, 987.77f, 0.3f, 0.99f, 0.5f, rng); PNoise(b, 0f, 0.004f, 0.2f, 0.0001f, 0.001f, 3000f, 11000f, rng); }
                    else if (v == 1) { Square(b, 0f, 1318.51f, 0.14f, 0.035f); }
                    else { Knock(b, 0f, 900f, 0.6f, rng); }
                    break;
                default: // UiHover
                    b = Buffer(0.06f);
                    if (v == 0) { PNoise(b, 0f, 0.003f, 0.25f, 0.0001f, 0.0006f, 4000f, 12000f, rng); }
                    else if (v == 1) { PSine(b, 0f, 0.03f, 2600f, 2200f, 0.004f, 0.12f, 0.0005f, 0.006f); }
                    else { PNoise(b, 0f, 0.02f, 0.15f, 0.002f, 0.005f, 2000f, 7000f, rng); }
                    break;
            }
            // No room on any of them: several carry a thump, and a thump through the room is the
            // boxy reverbed sound the listening ruled out.
            Saturate(b, 1.2f);
            HighPass(b, 60f);
            return PFinish(name, b, 0.85f);
        }
    }
}
