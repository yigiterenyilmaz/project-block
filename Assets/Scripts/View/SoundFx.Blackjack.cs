// PURPOSE: "Blackjack" HEARD - the table's cues (BlackjackSound, baked on first use and played on
// the polished pool) and the stage's MUSIC: five looping stems started on one DSP clock so they
// never drift apart, whose levels follow the table's mood (MusicMood) a little every frame.
// THE MIX: the music sits well under the cues - a room, not a soundtrack - and the cues keep the
// table's hierarchy: the hand's score ticks are the lightest thing on it, the bank's chips and its
// count the weightiest, and a result is the only cue that is allowed to be heard over the rest.
// The stems are synthesized on a WORKER THREAD the first time the table asks for music, so forty
// seconds of audio never cost a frame; until they are ready the table is simply quiet.
// EXTENSION POINT: a new cue is a BlackjackCue, a recipe in BlackjackSound and a level here; a new
// mood is a MusicMood and a row in MoodLevels.

using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>What the table's music is doing.</summary>
    public enum CasinoMusicMood
    {
        Off,
        Betting,
        Hand,
        AllIn,
        Win,
        Bankrupt
    }

    public sealed partial class SoundFx
    {
        private readonly Dictionary<int, AudioClip> casinoClips = new Dictionary<int, AudioClip>();
        private readonly Dictionary<BlackjackCue, int> casinoLastTake = new Dictionary<BlackjackCue, int>();

        private AudioSource[] casinoStems;
        private float[][] casinoStemData;
        private volatile bool casinoStemsReady;
        private bool casinoStemsBuilding;
        private bool casinoStemsStarted;
        private readonly float[] casinoLevel = new float[5];
        private CasinoMusicMood casinoMood = CasinoMusicMood.Off;
        private float casinoDuck;
        private float casinoDuckUntil;
        private float casinoDuckAmount;

        /// <summary>The table's cue level and the music's (lab knobs).</summary>
        public static float CasinoVolume = 1f;
        public static float CasinoMusicVolume = 1f;

        /// <summary>The music's mood right now (the lab's debug readout).</summary>
        public CasinoMusicMood CasinoMood
        {
            get { return casinoMood; }
        }

        /// <summary>How far the music is ducked right now, 0..1 (debug).</summary>
        public float CasinoDuckNow
        {
            get { return casinoDuck; }
        }

        /// <summary>Plays one beat of the table. <paramref name="pitch"/> 1 is the recipe's own.</summary>
        public void Casino(BlackjackCue cue, float pitch = 1f, float level = 1f)
        {
            int takes = cue == BlackjackCue.ChipTap || cue == BlackjackCue.ScoreTick
                || cue == BlackjackCue.CardLand || cue == BlackjackCue.BankTick ? 3 : 1;
            int last;
            casinoLastTake.TryGetValue(cue, out last);
            int take = takes == 1 ? 0 : (last + 1 + Random.Range(0, takes - 1)) % takes;
            casinoLastTake[cue] = take;
            int key = (int)cue * 8 + take;
            AudioClip clip;
            if (!casinoClips.TryGetValue(key, out clip))
            {
                float[] samples = BlackjackSound.Build(cue, take);
                if (samples == null)
                {
                    return;
                }
                clip = AudioClip.Create("casino" + cue + take, samples.Length, 1, BlackjackSound.Rate, false);
                clip.SetData(samples, 0);
                casinoClips[key] = clip;
            }
            float volume;
            switch (cue)
            {
                case BlackjackCue.ScoreTick: volume = 0.32f; break;
                case BlackjackCue.BankTick: volume = 0.42f; break;
                case BlackjackCue.DealerThink: volume = 0.3f; break;
                case BlackjackCue.CardSlide: volume = 0.42f; break;
                case BlackjackCue.CardFlick: volume = 0.4f; break;
                case BlackjackCue.CardLand: volume = 0.5f; break;
                case BlackjackCue.ChipTap: volume = 0.5f; break;
                case BlackjackCue.ChipStack: volume = 0.6f; break;
                case BlackjackCue.TableTap: volume = 0.55f; break;
                case BlackjackCue.LeadChange: volume = 0.38f; break;
                case BlackjackCue.InvalidBet: volume = 0.45f; break;
                case BlackjackCue.PowerBlocked: volume = 0.5f; break;
                case BlackjackCue.WinChime: volume = 0.62f; break;
                case BlackjackCue.LossClack: volume = 0.6f; break;
                case BlackjackCue.TieSuspend: volume = 0.5f; break;
                case BlackjackCue.Bankrupt: volume = 0.7f; break;
                case BlackjackCue.BossWin: volume = 0.72f; break;
                case BlackjackCue.AllInThud: volume = 0.75f; break;
                default: volume = 0.5f; break;
            }
            volume *= CasinoVolume * level;
            if (pool != null)
            {
                PlayPolished(clip, pitch * (takes > 1 ? Detune(0.25f) : 1f), volume);
            }
            else
            {
                PlayWithPitch(clip, pitch, pitch, volume);
            }
        }

        /// <summary>Sets what the music is doing; the levels move toward it over the next frames.
        /// The first call that wants music starts the stems' build on a worker.</summary>
        public void SetCasinoMood(CasinoMusicMood mood)
        {
            casinoMood = mood;
            if (mood != CasinoMusicMood.Off)
            {
                EnsureCasinoStems();
            }
        }

        /// <summary>Lowers the music by <paramref name="amount"/> (0..1) for a while - a result is
        /// being told, an all-in is being placed.</summary>
        public void DuckCasino(float amount, float seconds)
        {
            casinoDuckAmount = Mathf.Max(Time.unscaledTime < casinoDuckUntil ? casinoDuckAmount : 0f, Mathf.Clamp01(amount));
            casinoDuckUntil = Mathf.Max(casinoDuckUntil, Time.unscaledTime + seconds);
        }

        /// <summary>Called every frame by the controller: the stems' levels follow the mood.</summary>
        public void TickCasino(float dt)
        {
            if (casinoStemsReady && casinoStems == null)
            {
                CreateCasinoSources();
            }
            float wantDuck = Time.unscaledTime < casinoDuckUntil ? casinoDuckAmount : 0f;
            casinoDuck = Mathf.MoveTowards(casinoDuck, wantDuck, dt * (wantDuck > casinoDuck ? 4f : 1.4f));
            if (casinoStems == null)
            {
                return;
            }
            float[] want = MoodLevels(casinoMood);
            bool anyOn = false;
            for (int i = 0; i < casinoStems.Length; i++)
            {
                // a strip (bankruptcy) takes the stems away one at a time, the bass last
                float rate = casinoMood == CasinoMusicMood.Bankrupt ? 0.25f + 0.2f * (4 - i) : 0.7f;
                if (casinoMood == CasinoMusicMood.Bankrupt && i == (int)BlackjackStem.Bass)
                {
                    rate = 0.18f;
                }
                casinoLevel[i] = Mathf.MoveTowards(casinoLevel[i], want[i], dt * rate);
                anyOn |= casinoLevel[i] > 0.001f;
                casinoStems[i].volume = Mathf.Clamp01(FullVolume * masterVolume * CasinoMusicVolume
                    * 0.42f * casinoLevel[i] * (1f - casinoDuck) * (1f - petDuck));
            }
            if (anyOn && !casinoStemsStarted)
            {
                double at = AudioSettings.dspTime + 0.08;
                for (int i = 0; i < casinoStems.Length; i++)
                {
                    casinoStems[i].PlayScheduled(at);
                }
                casinoStemsStarted = true;
            }
            else if (!anyOn && casinoStemsStarted && casinoMood == CasinoMusicMood.Off)
            {
                for (int i = 0; i < casinoStems.Length; i++)
                {
                    casinoStems[i].Stop();
                }
                casinoStemsStarted = false;
            }
        }

        /// <summary>Each mood's stem levels: piano, bass, brush, vibes, pulse.</summary>
        private static float[] MoodLevels(CasinoMusicMood mood)
        {
            switch (mood)
            {
                case CasinoMusicMood.Betting: return new[] { 0.85f, 0.42f, 0.0f, 0.55f, 0.0f };
                case CasinoMusicMood.Hand: return new[] { 0.55f, 0.9f, 0.75f, 0.55f, 0.0f };
                case CasinoMusicMood.AllIn: return new[] { 0.45f, 0.95f, 0.9f, 0.4f, 0.85f };
                case CasinoMusicMood.Win: return new[] { 0.95f, 0.55f, 0.25f, 0.9f, 0.0f };
                default: return new[] { 0f, 0f, 0f, 0f, 0f };
            }
        }

        private void EnsureCasinoStems()
        {
            if (casinoStemsReady || casinoStemsBuilding)
            {
                return;
            }
            casinoStemsBuilding = true;
            var thread = new Thread(delegate ()
            {
                var data = new float[5][];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = BlackjackSound.BuildStem((BlackjackStem)i);
                }
                casinoStemData = data;
                casinoStemsReady = true;
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void CreateCasinoSources()
        {
            casinoStems = new AudioSource[casinoStemData.Length];
            for (int i = 0; i < casinoStems.Length; i++)
            {
                AudioClip clip = AudioClip.Create("casinoStem" + (BlackjackStem)i, casinoStemData[i].Length, 1,
                    BlackjackSound.MusicRate, false);
                clip.SetData(casinoStemData[i], 0);
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.clip = clip;
                s.volume = 0f;
                casinoStems[i] = s;
            }
            casinoStemData = null;
        }
    }
}
