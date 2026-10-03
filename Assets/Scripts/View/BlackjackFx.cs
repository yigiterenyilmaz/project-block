// PURPOSE: The BLACKJACK table's numbers in one place - every length, distance and strength the
// casino presentation is tuned by (the brief's own names), the lab's debug switches, the easing
// curves and the one colour conversion every overlay on this table needs.
//
// Presentation only: nothing here is a rule. The bank, the target, the bet, the deal, the house's
// move, the scores and the result are all Core's (BlackjackBoss); these numbers only say how long
// the table takes to SHOW them.
//
// EXTENSION POINT: a new beat gets its length here, next to its neighbours, never as a literal in
// the view - the lab's speed knob multiplies every one of them.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class BlackjackFx
    {
        /// <summary>The brief's tuning, named as it names them. Seconds unless said otherwise;
        /// distances in world units.</summary>
        public static class Tuning
        {
            // ---- layout
            public static float blackjackArenaGap = 1.5f;
            public static float blackjackArenaGapCompact = 0.9f;
            public static float blackjackArenaScale = 1f;
            public static float blackjackGroupVerticalOffset = 0f;
            public static float blackjackHudTopSpacing = 1.95f;
            public static float blackjackDealerHandOffset = 0.42f;
            public static float blackjackPlayerHandOffset = 1f; // share of the way under the player's board

            // ---- the house's card
            // faster since the first pass (2026-10-03: "the house deals too slowly") - about 0.5 s a card
            public static float dealerThinkMin = 0.15f;
            public static float dealerThinkMax = 0.30f;
            public static float dealerChosenCardOffset = 0.07f;
            public static float dealerChosenDuration = 0.07f;
            public static float dealerRevealDuration = 0.04f;
            public static float dealerCardTravelDuration = 0.26f;
            public static float dealerCardFlipDuration = 0.12f;
            public static float dealerCardLandSquash = 0.04f;
            public static float dealerCardLandDuration = 0.09f;
            public static float dealerGapCloseDuration = 0.10f;
            public static float dealerRefillDuration = 0.14f;
            public static float dealerAfterPlace = 0.12f;

            // ---- the bet
            public static float betConfirmPulse = 0.10f;
            public static float betBankCountDuration = 0.32f;
            public static float betTransferDuration = 0.28f;
            public static float betSettle = 0.12f;
            public static float betChipTravelArc = 0.55f;
            public static float allInMoodStrength = 1f;
            public static float dealSplitDuration = 0.42f;

            // ---- the score duel
            public static float scoreCountDuration = 0.35f;
            public static float scorePulseScale = 1.06f;
            public static float leadChangePulse = 1.08f;
            public static float finalComparisonHold = 0.30f;

            // ---- the hand's end
            public static float handEndSettle = 0.30f;
            public static float handResultIntro = 0.35f;
            public static float handResultHold = 0.75f;
            public static float payoutDuration = 0.45f;
            public static float wagerReturnDuration = 0.40f;
            public static float wagerLossDuration = 0.42f;
            public static float nextHandResetDuration = 0.55f;
            public static float bossWinHold = 1.6f;
            public static float bankruptHold = 1.8f;

            // ---- the intro
            public static float introOpen = 0.40f;
            public static float introStepStagger = 0.09f;
            public static float introClose = 0.32f;
            public static float houseStakeDuration = 0.55f;

            /// <summary>The lab's slow motion: every length above is divided by it.</summary>
            public static float Speed = 1f;

            /// <summary>A length from the table, at the lab's speed.</summary>
            public static float T(float seconds)
            {
                return seconds / Mathf.Max(0.05f, Speed);
            }
        }

        /// <summary>The brief's debug views. All off in the game; the lab switches them.</summary>
        public static class Debug
        {
            public static bool ShowBlackjackLayoutBounds;
            public static bool ShowArenaGroupCenter;
            public static bool ShowPlayerBoardCenter;
            public static bool ShowDealerBoardCenter;
            public static bool ShowDealerHandSlots;
            public static bool ShowDealerChosenCard;
            public static bool ShowDealerCardTravelPath;
            public static bool ShowPlayerScoreAnchor;
            public static bool ShowDealerScoreAnchor;
            public static bool ShowBankAnchor;
            public static bool ShowWagerAnchor;
            public static bool ShowTargetAnchor;
            public static bool ShowBetInputState;
            public static bool ShowHandResult;
            public static bool ShowAudioDuck;
            public static bool ShowBackgroundMood;

            public static bool Any
            {
                get
                {
                    return ShowBlackjackLayoutBounds || ShowArenaGroupCenter || ShowPlayerBoardCenter
                        || ShowDealerBoardCenter || ShowDealerHandSlots || ShowDealerChosenCard
                        || ShowDealerCardTravelPath || ShowPlayerScoreAnchor || ShowDealerScoreAnchor
                        || ShowBankAnchor || ShowWagerAnchor || ShowTargetAnchor || ShowBetInputState
                        || ShowHandResult || ShowAudioDuck || ShowBackgroundMood;
                }
            }

            public static void AllOff()
            {
                ShowBlackjackLayoutBounds = ShowArenaGroupCenter = ShowPlayerBoardCenter = false;
                ShowDealerBoardCenter = ShowDealerHandSlots = ShowDealerChosenCard = false;
                ShowDealerCardTravelPath = ShowPlayerScoreAnchor = ShowDealerScoreAnchor = false;
                ShowBankAnchor = ShowWagerAnchor = ShowTargetAnchor = ShowBetInputState = false;
                ShowHandResult = ShowAudioDuck = ShowBackgroundMood = false;
            }
        }

        // ------------------------------------------------------------------ easing

        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        public static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>A small overshoot - 1 at both ends of its settling, a little past in between.</summary>
        public static float EaseOutBack(float t, float overshoot)
        {
            t = Mathf.Clamp01(t);
            float c3 = overshoot + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }

        /// <summary>A pulse through three values (from -> peak -> settle -> 1) over 0..1.</summary>
        public static float Punch(float t, float from, float peak, float dip)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.4f)
            {
                return Mathf.Lerp(from, peak, EaseOut(t / 0.4f));
            }
            if (t < 0.75f)
            {
                return Mathf.Lerp(peak, dip, EaseInOut((t - 0.4f) / 0.35f));
            }
            return Mathf.Lerp(dip, 1f, EaseInOut((t - 0.75f) / 0.25f));
        }

        /// <summary>
        /// The brief's opacities are what the EYE sees; this game blends in LINEAR colour, where a
        /// dark overlay lands far weaker than its alpha says. Converted, a 0.13 dim is a 13% dim.
        /// </summary>
        public static float DarkAlpha(float perceptual)
        {
            perceptual = Mathf.Clamp01(perceptual);
            return 1f - Mathf.Pow(1f - perceptual, 2.2f);
        }

        /// <summary>A pale overlay over a dark table lifts about twice what its alpha promises;
        /// this is the inverse, roughly - so a "0.10 warm lift" stays a lift and not a haze.</summary>
        public static float LightAlpha(float perceptual)
        {
            return Mathf.Clamp01(perceptual) * 0.5f;
        }

        /// <summary>A deterministic 0..1 from two integers - the table never rolls a die.</summary>
        public static float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ 0x9E3779B9u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>A point on a quadratic bezier.</summary>
        public static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }
    }
}
