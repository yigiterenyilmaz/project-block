// PURPOSE: The Mexican wave under the game's name on the title screen - each letter lifts,
// hangs, and drops as a bump travels left to right along the word. Built and fed by
// MenuScreenView.MakeWaveTitle; nothing else in the game waves, and no other menu heading
// does either (a bouncing "PAUSED" is a joke, a bouncing logo is a logo).
//
// IT IS A TRAVELLING BUMP, NOT A SINE. A shared sine with a per-letter phase was the obvious
// first shape and it reads as a WOBBLE: every letter is always moving, so the word never sits
// still and the eye finds no line to read. A stadium wave is mostly stillness - one letter is
// up, its neighbours are on their way, everyone else is resting - so a letter here is flat for
// most of the cycle and only lifts inside its own window (LiftFraction).
//
// The lift curve is sin^2, whose slope is ZERO at both ends: a letter leaves the baseline and
// returns to it without a kink. Plain sin(pi*u) has the same shape but arrives at a slope, and
// at this amplitude that shows up as a tick at the bottom of every letter's travel.
//
// UNSCALED TIME on purpose. This runs on the menu layer, which is exactly where Time.timeScale
// is liable to be zero one day; a title screen that freezes because the game behind it is
// paused would be a bug nobody thinks to look for.
//
// NOT IN THE ANIMATION LAB. The lab plays animations over the live board (see
// GameUiController.AnimationLab), and this one only exists while a menu is covering the whole
// screen - there is nothing for it to play on top of. Open the title screen to see it.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Lifts a row of letters in sequence. One per title, destroyed with it.</summary>
    public sealed class MenuTitleWave : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the wave MOVES, in one place. Timing rather
        /// than skinning, so it lives here and not in MenuSkin.</summary>
        public static class Style
        {
            /// <summary>How far a letter rises at the top of its bump, in canvas units against
            /// the 1920x1080 reference. Read against TitleFontSize (68): about a quarter of the
            /// cap height, which is enough to see from across the room and still little enough
            /// that the word stays a word.</summary>
            public static float Amplitude = 17f;

            /// <summary>Seconds for one full pass, INCLUDING the rest at the end. This is the
            /// dial to turn for "faster" or "slower" - the rest shrinks and grows with it.</summary>
            public static float Period = 2.7f;

            /// <summary>How far behind its left neighbour a letter starts, as a fraction of the
            /// cycle. This is the SPEED OF THE BUMP along the word rather than of a letter:
            /// smaller and the wave whips across, larger and the letters go one at a time.</summary>
            public static float Stagger = 0.05f;

            /// <summary>The share of the cycle a single letter spends off the baseline. The
            /// last letter finishes at Stagger * (letters - 1) + LiftFraction, so keep that
            /// under 1 or the wave laps itself and the word never rests. At 10 letters that is
            /// 0.45 + 0.34, leaving a fifth of the cycle still.</summary>
            public static float LiftFraction = 0.34f;
        }

        // =================================================================== internals

        /// <summary>The letters, left to right. Their rest position is y = 0 in the title's own
        /// rect, so the wave writes anchoredPosition.y outright rather than tracking an
        /// origin - MenuScreenView lays them out on that baseline and never moves them again.
        /// A whitespace character keeps its slot here with no glyph to show, which is what
        /// carries the bump across the gap in a name of two words.</summary>
        private RectTransform[] letters;

        private float clock;

        /// <summary>Takes the letters MenuScreenView just laid out. Passing none simply leaves
        /// the component idle.</summary>
        public void Setup(RectTransform[] laidOut)
        {
            letters = laidOut;
            clock = 0f;
        }

        private void Update()
        {
            if (letters == null)
            {
                return;
            }
            clock += Time.unscaledDeltaTime;
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null)
                {
                    continue;
                }
                float t = Mathf.Repeat(clock / Style.Period - i * Style.Stagger, 1f);
                float lift = 0f;
                if (t < Style.LiftFraction)
                {
                    float s = Mathf.Sin(Mathf.PI * (t / Style.LiftFraction));
                    lift = s * s;
                }
                Vector2 p = letters[i].anchoredPosition;
                p.y = lift * Style.Amplitude;
                letters[i].anchoredPosition = p;
            }
        }
    }
}
