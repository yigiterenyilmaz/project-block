// PURPOSE: "Tamagotchi" HEARD - the playing half of the pet's audio. The sounds themselves are
// synthesized in TamagotchiVoice (a throat, grains of material, staged layers - see there); this
// file bakes them into AudioClips once, plays them, and mixes the rest of the game under them.
//
//   VARIANTS. A cue heard in runs has 3-5 takes; the same one is never played twice running, and
//   each play is moved a few percent in pitch and level (chomps +-3-6%, the voice +-5-9%). The takes
//   already differ by a few milliseconds of lead, so a run of bites never lines up like a machine.
//   STEMS. A growl is two clips played together - the low throat and the rasp - so their balance
//   (PetAudio.GrowlLowLayer / GrowlRaspLayer) is a live knob in the lab instead of a rebake.
//   A POOL OF ITS OWN. The pet plays on its own sources: the game's other effects are DUCKED under
//   a major pet event (DuckForPet: 15-30%, back over 180-350 ms) by lowering the sources they are
//   ringing on - which is only possible because the pet is not on them. Nothing clips: every take is
//   normalised below full scale and the categories are levelled here.
//   SPAM CONTROL. The same cue inside 35 ms is one cue. A five-card snack is not five full chomps:
//   the view asks for the hero take first and the light ones after.
//
// The clips are built in the background when the pet first appears (WarmTamagotchi, a few a frame),
// and on demand before that - never per frame. The haptic beats the brief lists are announced by the
// view (TamagotchiView.Haptics) and have no layer to go to yet: the game has no haptics.
// EXTENSION POINT: a new cue needs a recipe in TamagotchiVoice and a line in PetLevels.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        /// <summary>The brief's audio tuning (168). Live: every one is read when a cue plays.</summary>
        public static class PetAudio
        {
            public static float VoiceVolume = 0.62f;
            public static float GrowlVolume = 0.8f;
            public static float GrowlLowLayer = 1f;
            public static float GrowlRaspLayer = 0.85f;
            public static float ChompVolume = 0.72f;
            public static float BoardCrunchVolume = 0.95f;
            public static float FuryImpactVolume = 0.95f;
            public static float MusicDuckAmount = 0.28f;
            public static float MusicDuckAttack = 0.06f;
            public static float MusicDuckRelease = 0.26f;
        }

        private sealed class PetTake
        {
            public AudioClip[] Clips;
            public TamagotchiVoice.StemKind[] Kinds;
            public string[] Layers;
        }

        private enum PetGroup
        {
            Voice,
            Growl,
            Chomp,
            Board,
            Impact
        }

        private readonly Dictionary<PetSound, PetTake[]> petTakes = new Dictionary<PetSound, PetTake[]>();
        private readonly Dictionary<PetSound, int> petLastTake = new Dictionary<PetSound, int>();
        private readonly Dictionary<PetSound, float> petLastPlayed = new Dictionary<PetSound, float>();
        private AudioSource[] petPool;
        private int petPoolNext;
        private float petDuck;
        private float petDuckTarget;
        private float petDuckUntil;
        private Coroutine petWarm;

        /// <summary>The last pet cue played, and what it is made of (the lab's readouts).</summary>
        public string PetLastCue { get; private set; }

        public string PetLastLayers { get; private set; }

        /// <summary>How far the rest of the game is ducked right now, 0..1.</summary>
        public float PetDuckNow
        {
            get { return petDuck; }
        }

        /// <summary>Plays one of the pet's cues.</summary>
        public void Tamagotchi(PetSound cue)
        {
            Tamagotchi(cue, 1f);
        }

        /// <summary>Plays one of the pet's cues at a share of its own level.</summary>
        public void Tamagotchi(PetSound cue, float level)
        {
            float last;
            if (petLastPlayed.TryGetValue(cue, out last) && Time.unscaledTime - last < 0.035f)
            {
                return;
            }
            petLastPlayed[cue] = Time.unscaledTime;
            PetTake[] takes = PetTakes(cue);
            if (takes == null || takes.Length == 0)
            {
                return;
            }
            // never the same take twice running
            int was;
            int pick = 0;
            if (takes.Length > 1)
            {
                pick = Random.Range(0, takes.Length - 1);
                if (petLastTake.TryGetValue(cue, out was) && pick >= was)
                {
                    pick++;
                }
            }
            petLastTake[cue] = pick;
            PetTake take = takes[pick];
            float volume;
            float semis;
            PetGroup group;
            PetLevels(cue, out volume, out semis, out group);
            float groupGain = group == PetGroup.Voice ? PetAudio.VoiceVolume
                : group == PetGroup.Growl ? PetAudio.GrowlVolume
                : group == PetGroup.Board ? PetAudio.BoardCrunchVolume
                : group == PetGroup.Impact ? PetAudio.FuryImpactVolume : PetAudio.ChompVolume;
            float pitch = Detune(semis);
            float gain = volume * groupGain * level * Random.Range(0.95f, 1.05f);
            EnsurePetPool();
            for (int i = 0; i < take.Clips.Length; i++)
            {
                float stem = take.Kinds[i] == TamagotchiVoice.StemKind.GrowlLow ? PetAudio.GrowlLowLayer * PetAudio.GrowlVolume / Mathf.Max(0.01f, groupGain)
                    : take.Kinds[i] == TamagotchiVoice.StemKind.GrowlRasp ? PetAudio.GrowlRaspLayer * PetAudio.GrowlVolume / Mathf.Max(0.01f, groupGain) : 1f;
                AudioSource s = petPool[petPoolNext];
                petPoolNext = (petPoolNext + 1) % petPool.Length;
                s.pitch = pitch;
                s.PlayOneShot(take.Clips[i], Mathf.Clamp01(FullVolume * 2f * masterVolume * gain * stem));
            }
            PetLastCue = cue + (takes.Length > 1 ? " #" + (pick + 1) + "/" + takes.Length : "");
            PetLastLayers = string.Join(" + ", take.Layers);
        }

        /// <summary>What a cue is made of, without playing it (the lab's layer readout).</summary>
        public string PetLayersOf(PetSound cue)
        {
            PetTake[] takes = PetTakes(cue);
            return takes != null && takes.Length > 0 ? string.Join(" + ", takes[0].Layers) : "-";
        }

        /// <summary>Level (before the group's own knob), pitch spread in semitones, and the group.</summary>
        private static void PetLevels(PetSound cue, out float volume, out float semis, out PetGroup group)
        {
            switch (cue)
            {
                // ---- the voice: +-5-9%
                case PetSound.Enter: volume = 0.95f; semis = 0.6f; group = PetGroup.Voice; break;
                case PetSound.Peek: volume = 0.7f; semis = 1.2f; group = PetGroup.Voice; break;
                case PetSound.Idle: volume = 0.6f; semis = 1.4f; group = PetGroup.Voice; break;
                case PetSound.Hungry: volume = 0.8f; semis = 1.2f; group = PetGroup.Voice; break;
                case PetSound.NoticeDraggedCard: volume = 0.8f; semis = 1.2f; group = PetGroup.Voice; break;
                case PetSound.ValidFoodNear: volume = 0.85f; semis = 1f; group = PetGroup.Voice; break;
                case PetSound.WrongFoodNear: volume = 0.8f; semis = 0.9f; group = PetGroup.Voice; break;
                case PetSound.Request: volume = 0.8f; semis = 0.5f; group = PetGroup.Voice; break;
                case PetSound.Satisfied: volume = 0.9f; semis = 0.9f; group = PetGroup.Voice; break;
                case PetSound.Impatient: volume = 0.8f; semis = 0.9f; group = PetGroup.Voice; break;
                case PetSound.Smug: volume = 0.7f; semis = 0.9f; group = PetGroup.Voice; break;
                case PetSound.Disappointed: volume = 0.75f; semis = 0.4f; group = PetGroup.Voice; break;
                case PetSound.PileHero: volume = 0.7f; semis = 0.4f; group = PetGroup.Voice; break;
                case PetSound.Bubble: volume = 0.6f; semis = 1.2f; group = PetGroup.Voice; break;
                case PetSound.BubbleFurious: volume = 0.7f; semis = 0.8f; group = PetGroup.Voice; break;
                // ---- the pushed throat
                case PetSound.GrowlStart: volume = 0.9f; semis = 0.3f; group = PetGroup.Growl; break;
                case PetSound.GrowlIdle: volume = 0.42f; semis = 0.7f; group = PetGroup.Growl; break;
                case PetSound.Furious:
                case PetSound.FuryBreak: volume = 1f; semis = 0.25f; group = PetGroup.Growl; break;
                case PetSound.Grunt: volume = 0.7f; semis = 0.7f; group = PetGroup.Growl; break;
                case PetSound.FuriousBreath: volume = 0.42f; semis = 0.7f; group = PetGroup.Growl; break;
                case PetSound.MoveFurious: volume = 0.5f; semis = 0.6f; group = PetGroup.Growl; break;
                case PetSound.TargetAsset: volume = 0.8f; semis = 0.3f; group = PetGroup.Growl; break;
                // ---- the screen
                case PetSound.HatredWave: volume = 0.85f; semis = 0.15f; group = PetGroup.Impact; break;
                case PetSound.FuryImpact: volume = 1f; semis = 0.15f; group = PetGroup.Impact; break;
                // ---- the board
                case PetSound.BoardSuction: volume = 0.7f; semis = 0.3f; group = PetGroup.Board; break;
                case PetSound.BoardBite: volume = 1f; semis = 0.5f; group = PetGroup.Board; break;
                case PetSound.BoardGrind: volume = 0.75f; semis = 0.8f; group = PetGroup.Board; break;
                // ---- eating and moving: +-3-6%
                case PetSound.ChompLight:
                case PetSound.PileSnackLight: volume = 0.62f; semis = 1f; group = PetGroup.Chomp; break;
                case PetSound.Chew:
                case PetSound.ChewFurious: volume = 0.66f; semis = 1f; group = PetGroup.Chomp; break;
                case PetSound.Tap:
                case PetSound.TeethClack: volume = 0.5f; semis = 0.9f; group = PetGroup.Chomp; break;
                case PetSound.Move:
                case PetSound.Hide: volume = 0.55f; semis = 0.9f; group = PetGroup.Chomp; break;
                case PetSound.AssetBite:
                case PetSound.ChompFurious: volume = 1f; semis = 0.7f; group = PetGroup.Chomp; break;
                case PetSound.AssetStrain: volume = 0.75f; semis = 0.8f; group = PetGroup.Chomp; break;
                case PetSound.AssetPull: volume = 0.7f; semis = 0.4f; group = PetGroup.Chomp; break;
                case PetSound.GulpDeep: volume = 0.95f; semis = 0.5f; group = PetGroup.Chomp; break;
                default: volume = 0.85f; semis = 0.8f; group = PetGroup.Chomp; break;
            }
        }

        private PetTake[] PetTakes(PetSound cue)
        {
            PetTake[] takes;
            if (petTakes.TryGetValue(cue, out takes))
            {
                return takes;
            }
            int n = TamagotchiVoice.Variants(cue);
            takes = new PetTake[n];
            for (int v = 0; v < n; v++)
            {
                takes[v] = BakePet(cue, v);
            }
            petTakes[cue] = takes;
            return takes;
        }

        private static PetTake BakePet(PetSound cue, int variant)
        {
            TamagotchiVoice.Take take = TamagotchiVoice.Build(cue, variant);
            if (take == null)
            {
                return new PetTake { Clips = new AudioClip[0], Kinds = new TamagotchiVoice.StemKind[0], Layers = new string[0] };
            }
            var baked = new PetTake
            {
                Clips = new AudioClip[take.Stems.Length],
                Kinds = new TamagotchiVoice.StemKind[take.Stems.Length],
                Layers = take.Layers
            };
            for (int i = 0; i < take.Stems.Length; i++)
            {
                float[] samples = take.Stems[i].Samples;
                AudioClip clip = AudioClip.Create("pet" + cue + variant + "_" + i, samples.Length, 1, TamagotchiVoice.Rate, false);
                clip.SetData(samples, 0);
                baked.Clips[i] = clip;
                baked.Kinds[i] = take.Stems[i].Kind;
            }
            return baked;
        }

        /// <summary>Builds every pet clip in the background, a few a frame (called when the pet
        /// appears, so the first growl of a round is never built on the frame it is needed).</summary>
        public void WarmTamagotchi()
        {
            if (petWarm == null && isActiveAndEnabled)
            {
                petWarm = StartCoroutine(WarmPet());
            }
        }

        private IEnumerator WarmPet()
        {
            foreach (PetSound cue in System.Enum.GetValues(typeof(PetSound)))
            {
                if (!petTakes.ContainsKey(cue))
                {
                    PetTakes(cue);
                    yield return null;
                }
            }
        }

        private void EnsurePetPool()
        {
            if (petPool != null)
            {
                return;
            }
            petPool = new AudioSource[10];
            for (int i = 0; i < petPool.Length; i++)
            {
                petPool[i] = gameObject.AddComponent<AudioSource>();
                petPool[i].playOnAwake = false;
            }
        }

        // ================================================================== the mix

        /// <summary>
        /// A major pet event takes the foreground: everything else is lowered by
        /// <paramref name="amount"/> (0..1 of PetAudio.MusicDuckAmount's scale - pass 1 for the
        /// tuned amount) for <paramref name="seconds"/>, then comes back over the release.
        /// </summary>
        public void DuckForPet(float amount, float seconds)
        {
            petDuckTarget = Mathf.Clamp01(PetAudio.MusicDuckAmount * amount);
            petDuckUntil = Mathf.Max(petDuckUntil, Time.unscaledTime + seconds);
        }

        private void Update()
        {
            float want = Time.unscaledTime < petDuckUntil ? petDuckTarget : 0f;
            if (Mathf.Approximately(petDuck, want))
            {
                return;
            }
            float speed = want > petDuck ? 1f / Mathf.Max(0.01f, PetAudio.MusicDuckAttack) : 1f / Mathf.Max(0.01f, PetAudio.MusicDuckRelease);
            petDuck = Mathf.MoveTowards(petDuck, want, Mathf.Max(0.05f, PetAudio.MusicDuckAmount) * speed * Time.unscaledDeltaTime);
            float gain = 1f - petDuck;
            if (source != null)
            {
                source.volume = gain;
            }
            if (pool != null)
            {
                for (int i = 0; i < pool.Length; i++)
                {
                    pool[i].volume = gain;
                }
            }
            if (humSource != null)
            {
                humSource.volume = HumVolume * masterVolume * gain;
            }
        }
    }
}
