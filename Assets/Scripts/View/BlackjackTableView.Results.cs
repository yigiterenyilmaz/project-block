// PURPOSE: BlackjackTableView's SEQUENCES - the beats between the plays: the stake going onto the
// table, the deck cut in two, the hand's end and its result, the reset into the next hand, the
// stage won and the bank run dry. Each is one coroutine the controller runs (Run) and waits for.
//
// THE RESULT IS TOLD IN ORDER: a breath of stillness (the music ducks), both scores pulse, the
// difference is read - and only then the verdict and the MONEY. A won hand doubles the stake ON
// THE PLATE (200 -> 400) before it travels to the bank; a push slides it back; a lost hand slides
// it to the house's side and is taken there, and the bank does not move a second time (it already
// paid when the bet was placed). The bank's numbers are Core's (DuelHandResult.PurseAfter).
// Nothing celebrates loudly: a win is warm and restrained, a loss quiet and burgundy, a stage won
// is light opening up rather than confetti, and bankruptcy is the light going out - no red flash,
// no siren.

using System;
using System.Collections;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView
    {
        private SpriteRenderer[] splitStack;

        /// <summary>The stake leaves the bank for the plate: the bank counts down as the tokens arc
        /// across, the plate takes the amount, the house's side answers with a breath of light.</summary>
        public IEnumerator PlayBetPlaced(long bankBefore, long bankAfter, long bet, bool allIn)
        {
            if (Sfx != null)
            {
                Sfx.Casino(allIn ? BlackjackCue.ChipStack : BlackjackCue.ChipTap);
            }
            StartCoroutine(FoldBetTray());
            yield return Wait(T(BlackjackFx.Tuning.betConfirmPulse));
            SetWager(0, true);
            SetBank(bankBefore, false);
            SetBank(bankAfter, true, T(BlackjackFx.Tuning.betBankCountDuration));
            float share = bankBefore > 0 ? bet / (float)bankBefore : 0f;
            int tokens = Mathf.Clamp(2 + Mathf.RoundToInt(share * 5f), 2, 7);
            yield return FlyTokens(BankAnchor, L.Wager + new Vector2(0f, 0.2f * L.Type), tokens,
                T(BlackjackFx.Tuning.betTransferDuration + 0.08f), BlackjackCue.ChipTap);
            wagerShown = 0;
            CountWager(bet, T(0.16f));
            PulseWager(allIn ? 1.14f : 1.08f);
            if (Sfx != null)
            {
                Sfx.Casino(allIn ? BlackjackCue.AllInThud : BlackjackCue.WarmTone, 1f, allIn ? 0.8f : 0.6f);
            }
            yield return Wait(T(BlackjackFx.Tuning.betSettle));
            // the house's side wakes
            yield return PulseSide(Side.Dealer, 0.8f, T(0.35f));
        }

        /// <summary>A breath of warm light on one board's edge.</summary>
        public IEnumerator PulseSide(Side side, float strength, float seconds)
        {
            int i = side == Side.Player ? 0 : 1;
            warmTarget[i] = strength;
            yield return Wait(seconds * 0.45f);
            warmTarget[i] = 0f;
            yield return Wait(seconds * 0.55f);
        }

        /// <summary>The cut: one stack of backs in the middle of the table splits, half to the
        /// player's side, half to the house's - every card face down, nothing telling which went
        /// where. Then the house's closed hand is dealt in.</summary>
        public IEnumerator PlayDealSplit(int houseHand)
        {
            const int Layers = 8;
            if (splitStack == null)
            {
                splitStack = new SpriteRenderer[Layers];
                for (int i = 0; i < Layers; i++)
                {
                    splitStack[i] = Sprite("SplitCard" + i, BlackjackShapes.CardBackSprite, Color.white, FlightOrder + i);
                }
            }
            float s = L.DealerCardScale * 1.35f * 1.15f;
            Vector2 centre = L.GroupCenter + new Vector2(0f, 0.15f * L.Type);
            for (int i = 0; i < Layers; i++)
            {
                splitStack[i].transform.localPosition = centre + new Vector2(0f, i * 0.012f);
                splitStack[i].transform.localScale = new Vector3(s, s, 1f);
                splitStack[i].transform.localRotation = Quaternion.identity;
                splitStack[i].color = new Color(1f, 1f, 1f, 0f);
                splitStack[i].enabled = true;
            }
            // the stack arrives
            float t0 = Time.time;
            float a = T(0.14f);
            while (Time.time - t0 < a)
            {
                float k = (Time.time - t0) / a;
                for (int i = 0; i < Layers; i++)
                {
                    splitStack[i].color = new Color(1f, 1f, 1f, k);
                }
                yield return null;
            }
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.DeckSplit);
            }
            // it cuts: alternate cards go left and right, the halves leave for their sides
            float dur = T(BlackjackFx.Tuning.dealSplitDuration);
            t0 = Time.time;
            Vector2 playerTo = new Vector2(L.PlayerHand.x, Mathf.Lerp(L.PlayerHand.y, L.Player.y, 0.25f));
            Vector2 houseTo = L.DealerHand;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / dur);
                for (int i = 0; i < Layers; i++)
                {
                    bool toHouse = i % 2 == 1;
                    float stagger = Mathf.Clamp01(k * 1.4f - (i / 2) * 0.05f);
                    float part = BlackjackFx.EaseOut(Mathf.Clamp01(stagger / 0.35f));
                    float leave = BlackjackFx.EaseInOut(Mathf.Clamp01((stagger - 0.35f) / 0.65f));
                    Vector2 split = centre + new Vector2((toHouse ? 1f : -1f) * 0.55f * L.Type * part, i * 0.012f);
                    Vector2 pos = Vector2.Lerp(split, toHouse ? houseTo : playerTo, leave);
                    splitStack[i].transform.localPosition = pos;
                    splitStack[i].transform.localRotation = Quaternion.Euler(0f, 0f, (toHouse ? -1f : 1f) * 6f * part * (1f - leave));
                    splitStack[i].color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((leave - 0.7f) / 0.3f));
                }
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
            for (int i = 0; i < Layers; i++)
            {
                splitStack[i].enabled = false;
            }
            yield return DealDealerCards(houseHand);
        }

        /// <summary>
        /// The hand is over: a held breath, both scores pulse, the gap is read - then the verdict and
        /// the money, Core's numbers throughout. <paramref name="bankBefore"/> is what the bank
        /// SHOWED while the hand was played (the stake already out of it).
        /// </summary>
        public IEnumerator PlayHandResult(DuelHandResult r, long bankBefore)
        {
            SetActiveSide(Side.None);
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.HandEnd);
                Sfx.DuckCasino(0.45f, 1.8f);
            }
            SetScore(Side.Player, r.PlayerScore);
            SetScore(Side.Dealer, r.AiScore);
            yield return Wait(T(BlackjackFx.Tuning.handEndSettle));
            PulseScores();
            int gap = Math.Abs(r.PlayerScore - r.AiScore);
            bool close = gap > 0 && gap <= Math.Max(10, Math.Max(r.PlayerScore, r.AiScore) / 10);
            // a close hand breathes a little longer before it is called
            yield return Wait(T(BlackjackFx.Tuning.finalComparisonHold + (close ? 0.25f : 0f)));

            string sub;
            if (r.Outcome == DuelHandOutcome.Win)
            {
                warmTarget[0] = 0.8f;
                dimTarget[1] = 0.45f;
                sub = "+" + r.Payout;
                ShowBanner(Loc.Pick("HAND WON", "EL KAZANILDI"), sub, WinInk, T(BlackjackFx.Tuning.handResultHold + 0.4f));
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.WinChime);
                }
                yield return Wait(T(BlackjackFx.Tuning.handResultIntro));
                // the stake doubles in place, then goes home
                CountWager(r.Payout, T(0.28f));
                yield return Wait(T(0.34f));
                SetBank(r.PurseAfter, true, T(BlackjackFx.Tuning.payoutDuration));
                yield return FlyTokens(WagerAnchor, BankAnchor, 6, T(BlackjackFx.Tuning.payoutDuration), BlackjackCue.ChipTap);
                SetWager(0, false);
                PulseBank(1.08f);
            }
            else if (r.Outcome == DuelHandOutcome.Push)
            {
                sub = Loc.Pick("bet returned", "bahis geri");
                ShowBanner(Loc.Pick("PUSH", "BERABERE"), sub, TieInk, T(BlackjackFx.Tuning.handResultHold));
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.TieSuspend);
                }
                yield return Wait(T(BlackjackFx.Tuning.handResultIntro));
                SetBank(r.PurseAfter, true, T(BlackjackFx.Tuning.wagerReturnDuration));
                yield return FlyTokens(WagerAnchor, BankAnchor, 3, T(BlackjackFx.Tuning.wagerReturnDuration), BlackjackCue.ChipTap);
                SetWager(0, false);
                PulseBank(1.04f);
            }
            else
            {
                dimTarget[0] = 0.45f;
                warmTarget[1] = 0.25f;
                sub = "-" + r.Bet;
                ShowBanner(Loc.Pick("HAND LOST", "EL KAYBEDİLDİ"), sub, LossInk, T(BlackjackFx.Tuning.handResultHold));
                yield return Wait(T(BlackjackFx.Tuning.handResultIntro * 0.6f));
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.LossClack);
                }
                yield return SlideWagerToHouse(T(BlackjackFx.Tuning.wagerLossDuration));
                // the bank already paid when the bet went down: it does not move again
                SetBank(r.PurseAfter, ShownBank != r.PurseAfter);
            }
            yield return Wait(T(BlackjackFx.Tuning.handResultHold * 0.6f));
        }

        /// <summary>Into the next hand: a felt wipe crosses both arenas (the boards are emptied
        /// under it - <paramref name="onCovered"/>), the house's cards slide off into the dark, the
        /// scores fold to zero, and a sweep of light opens the table again.</summary>
        public IEnumerator PlayNextHand(Action onCovered)
        {
            StartCoroutine(SlideDealerCardsAway());
            FoldScores();
            SetWager(0, false);
            float half = T(BlackjackFx.Tuning.nextHandResetDuration * 0.5f);
            float t0 = Time.time;
            while (Time.time - t0 < half)
            {
                wipe = BlackjackFx.EaseInOut((Time.time - t0) / half);
                yield return null;
            }
            wipe = 1f;
            if (onCovered != null)
            {
                onCovered();
            }
            ClearSides();
            SetScoresVisible(false);
            lightTarget = 1.18f;
            t0 = Time.time;
            while (Time.time - t0 < half)
            {
                wipe = 1f - BlackjackFx.EaseInOut((Time.time - t0) / half);
                yield return null;
            }
            wipe = 0f;
            lightTarget = 1f;
        }

        /// <summary>The target reached: the bank's marker crosses it in gold, the warm light opens
        /// up over the dimmed arenas, one line sweeps under HEDEF TAMAMLANDI, a few motes rise - and
        /// then the lights go down for the market.</summary>
        public IEnumerator PlayBossWin(long bank)
        {
            PulseTarget();
            PulseBank(1.12f);
            winTarget = 1f;
            dimTarget[0] = dimTarget[1] = 0.5f;
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.BossWin);
                Sfx.SetCasinoMood(CasinoMusicMood.Win);
            }
            ShowBanner(Loc.Pick("TARGET REACHED", "HEDEF TAMAMLANDI"),
                Loc.Pick("bank ", "kasan ") + bank, WinInk, T(BlackjackFx.Tuning.bossWinHold));
            yield return Wait(T(BlackjackFx.Tuning.bossWinHold + BlackjackFx.Tuning.handResultIntro));
            lightTarget = 0.55f;
            winTarget = 0.3f;
            yield return Wait(T(0.5f));
        }

        /// <summary>The bank run dry: the amber light goes down, the burgundy closes in, the
        /// arenas lose their colour, the empty bank pulses once, KASAN BOŞALDI - and the music is
        /// taken away a stem at a time.</summary>
        public IEnumerator PlayBankrupt()
        {
            lightTarget = 0.35f;
            burgundyTarget = 1f;
            greyTarget[0] = greyTarget[1] = 1f;
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.Bankrupt);
                Sfx.SetCasinoMood(CasinoMusicMood.Bankrupt);
            }
            yield return Wait(T(0.3f));
            PulseBank(1.1f);
            ShowBanner(Loc.Pick("BANK EMPTY", "KASAN BOŞALDI"),
                Loc.Pick("the house keeps the table", "masa kasada kaldı"), LossInk, T(BlackjackFx.Tuning.bankruptHold));
            yield return Wait(T(0.6f));
            PulseBank(1.06f);
            yield return Wait(T(BlackjackFx.Tuning.bankruptHold));
        }
    }
}
