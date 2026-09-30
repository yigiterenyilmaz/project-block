// PURPOSE: The animation lab's "KREDİ / BORÇ / HACİZ" section - every look of the BLACK LEDGER
// (DebtLedgerView), every level of the DEBT PRESSURE (DebtPressurePresentationController) and every
// beat of the foreclosure (ForeclosureView), on demand.
//
// The lab's rule holds: the scenes call the SAME view methods the game calls and fabricate only
// the ARGUMENTS. The ledger's states are built from the session's own arithmetic (MinimumPaymentFor,
// InterestFor, the configured rate and term), never from numbers written here; the round's own bar
// is the real round's (RoundEngine.OwnBar). The pressure scenes hand the controller a frame of
// their own through the SAME seam the game uses (FeedPressure), measured against the REAL score
// line and board, and print their books on the real score line (creditLabHud). The foreclosures are
// RUN BY CORE: each scene builds a scratch GameSession of its own (never the player's), gives it the
// jokers, powers and blocks the scene is about, and asks GameSession.ForecloseForLab for the
// statement - so the order things go in, their values, their half prices and where the debt ends
// are the rules' answers. Only "600 -> 300" is written by hand, because it is the design's own
// worked example. While a scene runs the lab owns the views (creditLabOwnsViews), and RESET
// hands them back to the game, which resyncs without replaying anything as an event.
//
// THE CORRECTIVE PASS'S SCENES (A-V) are the design's own acceptance list: the old strip beside
// the new one, the fit at 1920x1080 / 1366x768 / narrow windows (the placement solver asked about
// screens that are not the one in front of it), the four states as whole screens, the two
// side-by-sides (alternated - an atmosphere is a whole screen, so "side by side" is A then B on
// the same screen), relief, a large relief, interest, the minimum, the stamp, each pressure layer
// alone, the carry, the transition into the bailiff, and the whole escalation at half speed. And
// the MAIN acceptance test: the ledger hidden, debt-free and dangerous alternating - can the
// screen alone still tell them apart?

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private enum CredScene
        {
            Closed, Open, Debt500, Debt2000, DebtStress, Min0, Min40, Min99, MinSatisfied,
            InterestPreview, MaturityFull, MaturityHalf, Warning, FinalDue,
            EvOpened, EvCarried, EvPaySmall, EvPayLarge, EvPayCrossesMinimum, EvInterest,
            EvEarlyRepayment, EvEarlyBonus, EvNormalClear,
            DlSafe, DlPressure, DlWarning, DlFinal, DlFinalScreen,
            FcStampOnly, FcFullEntry, FcLedgerPanel, FcAppraisal, FcJoker, FcPower, FcElemental,
            FcBasic, Fc600, FcToDrawer, FcToDebt, FcThreeHigh, FcTenBasic, FcClearsMid, FcComplete,
            FcRemainsDebt, Fc1x, Fc05x, FcAppraisal025x,
            // the corrective pass
            PxOldHud, PxNewHud, PxFit1080, PxFit768, PxResponsive, PxSafe, PxPressure, PxWarning,
            PxFinal, PxFreeVsSafe, PxSafeVsFinal, PxRelief, PxLargeRelief, PxInterest, PxMinimum,
            PxStamp, PxBracketsOnly, PxVignetteOnly, PxHeaderOnly, PxCarry, PxToForeclosure,
            PxHalfSpeed, PxAcceptance
        }

        private Coroutine animCredit;
        private bool animCreditUsed;

        private void AddCreditLabAnims()
        {
            AddAnimSub("jokers", "kredi", "kredi / borç / haciz", "kredi / borç / haciz");
            // --- DEBT PRESSURE (the corrective pass, A-V) ---
            AddCred(CredScene.PxOldHud, "A. Old tiny HUD (the first pass)", "A. Eski küçük panel (ilk hali)");
            AddCred(CredScene.PxNewHud, "B. New enlarged HUD", "B. Yeni büyük panel");
            AddCred(CredScene.PxFit1080, "C. 1920x1080 fit", "C. 1920x1080 sığma");
            AddCred(CredScene.PxFit768, "D. 1366x768 fit", "D. 1366x768 sığma");
            AddCred(CredScene.PxResponsive, "E. Small-window responsive", "E. Küçük pencere düzeni");
            AddCred(CredScene.PxSafe, "F. SAFE full screen", "F. GÜVENLİ tam ekran");
            AddCred(CredScene.PxPressure, "G. PRESSURE full screen", "G. BASKI tam ekran");
            AddCred(CredScene.PxWarning, "H. WARNING full screen", "H. UYARI tam ekran");
            AddCred(CredScene.PxFinal, "I. FINAL full screen", "I. SON VADE tam ekran");
            AddCred(CredScene.PxFreeVsSafe, "J. Debt-free vs SAFE (alternating)", "J. Borçsuz / GÜVENLİ (dönüşümlü)");
            AddCred(CredScene.PxSafeVsFinal, "K. SAFE vs FINAL (alternating)", "K. GÜVENLİ / SON VADE (dönüşümlü)");
            AddCred(CredScene.PxRelief, "L. Payment relief", "L. Ödeme rahatlaması");
            AddCred(CredScene.PxLargeRelief, "M. Large payment relief (2000, pays 1500)", "M. Büyük ödeme (2000, 1500 ödendi)");
            AddCred(CredScene.PxInterest, "N. Interest pressure (+300 on 2000)", "N. Faiz baskısı (2000 üstüne +300)");
            AddCred(CredScene.PxMinimum, "O. Minimum satisfied relief", "O. Asgari tamam rahatlaması");
            AddCred(CredScene.PxStamp, "P. Final due stamp", "P. Son vade damgası");
            AddCred(CredScene.PxBracketsOnly, "Q. Board brackets only", "Q. Yalnız tahta köşebentleri");
            AddCred(CredScene.PxVignetteOnly, "R. Vignette only", "R. Yalnız vinyet");
            AddCred(CredScene.PxHeaderOnly, "S. Header debt burden only", "S. Yalnız başlık borç yükü");
            AddCred(CredScene.PxCarry, "T. Debt carry -> negative score", "T. Borç taşıma -> eksi skor");
            AddCred(CredScene.PxToForeclosure, "U. Final due -> foreclosure", "U. Son vade -> haciz");
            AddCred(CredScene.PxHalfSpeed, "V. Pressure transitions 0.5x", "V. Baskı geçişleri 0.5x");
            AddCred(CredScene.PxAcceptance, "ACCEPTANCE: ledger hidden, no debt vs danger",
                "KABUL: panel gizli, borçsuz / tehlike");
            // --- DEBT HUD ---
            AddCred(CredScene.Closed, "1. Debt panel closed", "1. Borç paneli kapalı");
            AddCred(CredScene.Open, "2. Debt panel open", "2. Borç paneli açık");
            AddCred(CredScene.Debt500, "3. Debt = 500", "3. Borç = 500");
            AddCred(CredScene.Debt2000, "4. Debt = 2000", "4. Borç = 2000");
            AddCred(CredScene.DebtStress, "5. Debt large value stress", "5. Büyük borç stres testi");
            AddCred(CredScene.Min0, "6. Minimum 0%", "6. Asgari %0");
            AddCred(CredScene.Min40, "7. Minimum 40%", "7. Asgari %40");
            AddCred(CredScene.Min99, "8. Minimum 99%", "8. Asgari %99");
            AddCred(CredScene.MinSatisfied, "9. Minimum satisfied", "9. Asgari tamam");
            AddCred(CredScene.InterestPreview, "10. Interest preview", "10. Faiz önizleme");
            AddCred(CredScene.MaturityFull, "11. Maturity full term", "11. Vade tam");
            AddCred(CredScene.MaturityHalf, "12. Maturity half", "12. Vade yarı");
            AddCred(CredScene.Warning, "13. Warning", "13. Uyarı");
            AddCred(CredScene.FinalDue, "14. Final due", "14. Son vade");
            // --- EVENTS ---
            AddCred(CredScene.EvOpened, "15. Debt opened", "15. Borç açıldı");
            AddCred(CredScene.EvCarried, "16. Debt carried into next round", "16. Borç sonraki raunta taşındı");
            AddCred(CredScene.EvPaySmall, "17. Payment small", "17. Küçük ödeme");
            AddCred(CredScene.EvPayLarge, "18. Payment large", "18. Büyük ödeme");
            AddCred(CredScene.EvPayCrossesMinimum, "19. Payment crosses minimum", "19. Ödeme asgariyi geçiyor");
            AddCred(CredScene.EvInterest, "20. Interest applied", "20. Faiz işledi");
            AddCred(CredScene.EvEarlyRepayment, "21. Early full repayment", "21. Erken tam ödeme");
            AddCred(CredScene.EvEarlyBonus, "22. Early repayment bonus", "22. Erken ödeme bonusu");
            AddCred(CredScene.EvNormalClear, "23. Normal debt clear", "23. Normal borç kapanışı");
            // --- DEADLINE ---
            AddCred(CredScene.DlSafe, "24. Safe stage", "24. Güvenli aşama");
            AddCred(CredScene.DlPressure, "25. Pressure stage", "25. Baskı aşaması");
            AddCred(CredScene.DlWarning, "26. Warning stage", "26. Uyarı aşaması");
            AddCred(CredScene.DlFinal, "27. Final due stage", "27. Son vade aşaması");
            AddCred(CredScene.DlFinalScreen, "28. Final due full-screen context", "28. Son vade ekran atmosferi");
            // --- FORECLOSURE ---
            AddCred(CredScene.FcStampOnly, "29. HACİZ stamp only", "29. Yalnız HACİZ damgası");
            AddCred(CredScene.FcFullEntry, "30. HACİZ full entry", "30. HACİZ tam giriş");
            AddCred(CredScene.FcLedgerPanel, "31. Foreclosure Ledger panel", "31. Haciz dosyası paneli");
            AddCred(CredScene.FcAppraisal, "32. Single joker appraisal", "32. Tek joker değerleme");
            AddCred(CredScene.FcJoker, "33. Joker seizure", "33. Joker haczi");
            AddCred(CredScene.FcPower, "34. Power seizure", "34. Güç haczi");
            AddCred(CredScene.FcElemental, "35. Elemental card seizure", "35. Elementli kart haczi");
            AddCred(CredScene.FcBasic, "36. Basic card seizure", "36. Düz kart haczi");
            AddCred(CredScene.Fc600, "37. 600 -> 300 example", "37. 600 -> 300 örneği");
            AddCred(CredScene.FcToDrawer, "38. Asset -> drawer", "38. Varlık -> çekmece");
            AddCred(CredScene.FcToDebt, "39. Seizure value -> debt", "39. Haciz değeri -> borç");
            AddCred(CredScene.FcThreeHigh, "40. Three high-value assets", "40. Üç değerli varlık");
            AddCred(CredScene.FcTenBasic, "41. Ten basic cards fast cadence", "41. On düz kart hızlı tempo");
            AddCred(CredScene.FcClearsMid, "42. Debt clears mid-seizure", "42. Borç haciz ortasında kapanıyor");
            AddCred(CredScene.FcComplete, "43. Foreclosure complete", "43. Haciz tamam");
            AddCred(CredScene.FcRemainsDebt, "44. Foreclosure remains debt", "44. Haciz sonrası borç kalıyor");
            // --- SPEED ---
            AddCred(CredScene.Fc1x, "45. Foreclosure 1x", "45. Haciz 1x");
            AddCred(CredScene.Fc05x, "46. Foreclosure 0.5x", "46. Haciz 0.5x");
            AddCred(CredScene.FcAppraisal025x, "47. Appraisal 0.25x", "47. Değerleme 0.25x");

            // the pressure's debug readouts and the ledger's (135)
            AddCredToggle("ShowDebtPressureState", "ShowDebtPressureState",
                delegate { return DebtPressurePresentationController.Layers.ShowDebtPressureState; },
                delegate (bool v) { DebtPressurePresentationController.Layers.ShowDebtPressureState = v; });
            AddCredToggle("ShowDebtPressure01", "ShowDebtPressure01",
                delegate { return DebtPressurePresentationController.Layers.ShowDebtPressure01; },
                delegate (bool v) { DebtPressurePresentationController.Layers.ShowDebtPressure01 = v; });
            AddCredToggle("ShowVignetteStrength", "ShowVignetteStrength",
                delegate { return DebtPressurePresentationController.Layers.ShowVignetteStrength; },
                delegate (bool v) { DebtPressurePresentationController.Layers.ShowVignetteStrength = v; });
            AddCredToggle("ShowBackgroundDesaturation", "ShowBackgroundDesaturation",
                delegate { return DebtPressurePresentationController.Layers.ShowBackgroundDesaturation; },
                delegate (bool v) { DebtPressurePresentationController.Layers.ShowBackgroundDesaturation = v; });
            AddCredToggle("ShowBoardBracketOffset", "ShowBoardBracketOffset",
                delegate { return DebtPressurePresentationController.Layers.ShowBoardBracketOffset; },
                delegate (bool v) { DebtPressurePresentationController.Layers.ShowBoardBracketOffset = v; });
            AddCredToggle("ShowMaturityProgress", "ShowMaturityProgress",
                delegate { return DebtLedgerView.Layers.ShowMaturityProgress; },
                delegate (bool v) { DebtLedgerView.Layers.ShowMaturityProgress = v; });
            AddCredToggle("ShowDebtBurdenHeader", "ShowDebtBurdenHeader",
                delegate { return DebtPressurePresentationController.Layers.ShowDebtBurdenHeader; },
                delegate (bool v) { DebtPressurePresentationController.Layers.ShowDebtBurdenHeader = v; });
            AddCredToggle("ShowDebtLedgerBounds", "ShowDebtLedgerBounds",
                delegate { return DebtLedgerView.Layers.ShowDebtPanelBounds; },
                delegate (bool v) { DebtLedgerView.Layers.ShowDebtPanelBounds = v; });
            AddCredToggle("ShowResponsiveLayout", "ShowResponsiveLayout",
                delegate { return DebtLedgerView.Layers.ShowResponsiveLayout; },
                delegate (bool v) { DebtLedgerView.Layers.ShowResponsiveLayout = v; });
            AddCredToggle("ShowMinimumProgress", "ShowMinimumProgress",
                delegate { return DebtLedgerView.Layers.ShowMinimumProgress; },
                delegate (bool v) { DebtLedgerView.Layers.ShowMinimumProgress = v; });
            // the pressure's own layers
            AddCredToggle("pressure: vignette + backdrop", "baskı: vinyet + arka plan",
                delegate { return DebtPressurePresentationController.Layers.Vignette; },
                delegate (bool v)
                {
                    DebtPressurePresentationController.Layers.Vignette = v;
                    DebtPressurePresentationController.Layers.Desaturation = v;
                });
            AddCredToggle("pressure: brackets + board shadow", "baskı: köşebentler + tahta gölgesi",
                delegate { return DebtPressurePresentationController.Layers.Brackets; },
                delegate (bool v)
                {
                    DebtPressurePresentationController.Layers.Brackets = v;
                    DebtPressurePresentationController.Layers.BoardShadow = v;
                });
            AddCredToggle("pressure: score + target burden", "baskı: skor + hedef yükü",
                delegate { return DebtPressurePresentationController.Layers.TargetBurden; },
                delegate (bool v)
                {
                    DebtPressurePresentationController.Layers.ScoreAccent = v;
                    DebtPressurePresentationController.Layers.TargetBurden = v;
                });
            AddCredToggle("minimum marker", "asgari işareti",
                delegate { return DebtLedgerView.Layers.ShowMinimumMarker; },
                delegate (bool v) { DebtLedgerView.Layers.ShowMinimumMarker = v; });
            AddCredToggle("maturity track", "vade izi",
                delegate { return DebtLedgerView.Layers.ShowMaturityTrack; },
                delegate (bool v) { DebtLedgerView.Layers.ShowMaturityTrack = v; });
            AddCredToggle("debt value (dev)", "borç değeri (geliştirici)",
                delegate { return DebtLedgerView.Layers.ShowDebtValue; },
                delegate (bool v) { DebtLedgerView.Layers.ShowDebtValue = v; });
            AddCredToggle("deadline state (dev)", "vade durumu (geliştirici)",
                delegate { return DebtLedgerView.Layers.ShowDeadlineState; },
                delegate (bool v) { DebtLedgerView.Layers.ShowDeadlineState = v; });
            AddCredToggle("interest preview (dev)", "faiz önizleme (geliştirici)",
                delegate { return DebtLedgerView.Layers.ShowInterestPreview; },
                delegate (bool v) { DebtLedgerView.Layers.ShowInterestPreview = v; });
            AddCredToggle("selected asset", "seçili varlık",
                delegate { return ForeclosureView.Layers.ShowForeclosureSelectedAsset; },
                delegate (bool v) { ForeclosureView.Layers.ShowForeclosureSelectedAsset = v; });
            AddCredToggle("asset normal value", "varlık normal değeri",
                delegate { return ForeclosureView.Layers.ShowAssetNormalValue; },
                delegate (bool v) { ForeclosureView.Layers.ShowAssetNormalValue = v; });
            AddCredToggle("asset seizure value", "varlık haciz değeri",
                delegate { return ForeclosureView.Layers.ShowAssetSeizureValue; },
                delegate (bool v) { ForeclosureView.Layers.ShowAssetSeizureValue = v; });
            AddCredToggle("asset source anchor", "varlık kaynak noktası",
                delegate { return ForeclosureView.Layers.ShowAssetSourceAnchor; },
                delegate (bool v) { ForeclosureView.Layers.ShowAssetSourceAnchor = v; });
            AddCredToggle("bank drawer anchor", "banka çekmecesi noktası",
                delegate { return ForeclosureView.Layers.ShowBankDrawerAnchor; },
                delegate (bool v) { ForeclosureView.Layers.ShowBankDrawerAnchor = v; });
            AddCredToggle("asset travel path", "varlık yolu",
                delegate { return ForeclosureView.Layers.ShowAssetTravelPath; },
                delegate (bool v) { ForeclosureView.Layers.ShowAssetTravelPath = v; });
            AddCredToggle("debt transfer path", "borç aktarım yolu",
                delegate { return ForeclosureView.Layers.ShowDebtTransferPath; },
                delegate (bool v) { ForeclosureView.Layers.ShowDebtTransferPath = v; });
            AddCredToggle("foreclosure queue (dev)", "haciz sırası (geliştirici)",
                delegate { return ForeclosureView.Layers.ShowForeclosureQueue; },
                delegate (bool v) { ForeclosureView.Layers.ShowForeclosureQueue = v; });
            AddCredToggle("remaining asset count (dev)", "kalan varlık sayısı (geliştirici)",
                delegate { return ForeclosureView.Layers.ShowRemainingAssetCount; },
                delegate (bool v) { ForeclosureView.Layers.ShowRemainingAssetCount = v; });
            AddAnim("kredi switch: ALL back to default", "kredi anahtarı: TÜMÜ varsayılana", delegate
            {
                DebtLedgerView.Layers.Reset();
                DebtPressurePresentationController.Layers.Reset();
                ForeclosureView.Layers.Reset();
                animLastLabel = Loc.Pick("every credit switch back to default", "tüm kredi anahtarları varsayılan");
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
            });
        }

        private void AddCred(CredScene scene, string en, string tr)
        {
            AddAnim("kredi " + en, "kredi " + tr, delegate { AnimCredit(scene); });
        }

        private void AddCredToggle(string en, string tr, System.Func<bool> get, System.Action<bool> set)
        {
            AddAnim("kredi switch: " + en, "kredi anahtarı: " + tr, delegate
            {
                bool on = !get();
                set(on);
                animLastLabel = Loc.Pick("credit " + en + ": ", "kredi " + tr + ": ") + OnOff(on);
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
            });
        }

        private void AnimCredit(CredScene scene)
        {
            StopAnimHost();
            StopAnimPress();
            StopAnimSnake();
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            if (session == null)
            {
                return;
            }
            EnsureCreditViews();
            animCreditUsed = true;
            creditLabOwnsViews = true;
            animCredit = StartCoroutine(CreditRoutine(scene));
        }

        /// <summary>Called from StopAnimHost: hands every view back to the game.</summary>
        private void StopAnimCredit()
        {
            if (animCredit != null)
            {
                StopCoroutine(animCredit);
                animCredit = null;
            }
            if (!animCreditUsed)
            {
                return;
            }
            animCreditUsed = false;
            creditLabOwnsViews = false;
            creditLabPressure = new PressureFacts();
            creditLabHud = null;
            // the "X only" scenes promise the other layers come back
            DebtPressurePresentationController.Layers.Vignette = true;
            DebtPressurePresentationController.Layers.Desaturation = true;
            DebtPressurePresentationController.Layers.Brackets = true;
            DebtPressurePresentationController.Layers.BoardShadow = true;
            DebtPressurePresentationController.Layers.ScoreAccent = true;
            DebtPressurePresentationController.Layers.TargetBurden = true;
            if (ledgerView != null)
            {
                ledgerView.PlaybackRate = 1f;
                ledgerView.Close(false);
            }
            if (debtPressure != null)
            {
                debtPressure.PlaybackRate = 1f;
                debtPressure.Settle();
            }
            if (foreclosureView != null)
            {
                foreclosureView.PlaybackRate = 1f;
                foreclosureView.Stop();
            }
            UpdateScoreHud();
            // Resync from the books without telling anything as an event.
            creditSessionSeen = null;
        }

        // ------------------------------------------------------------------ states

        /// <summary>A ledger state built with the SESSION's arithmetic: the minimum a stage that
        /// started with <paramref name="stageStart"/> owes, the interest on <paramref name="debt"/>,
        /// the configured rate and term.</summary>
        private DebtLedgerView.State LabState(long debt, long stageStart, long minimumPaid, int termLeft,
            CreditDeadline deadline, bool inRound)
        {
            long due = session.MinimumPaymentFor(inRound ? stageStart : debt);
            return new DebtLedgerView.State
            {
                Debt = debt,
                StageStartDebt = inRound ? stageStart : 0,
                MinimumDue = due,
                MinimumPaid = System.Math.Min(minimumPaid, due),
                MinimumSatisfied = inRound && minimumPaid >= due,
                InRound = inRound,
                InterestPermille = session.CreditInterestPermille,
                NextInterest = session.InterestFor(debt),
                TermLeft = termLeft,
                TermTotal = session.CreditTermStages,
                Deadline = deadline
            };
        }

        private int Term
        {
            get { return session.CreditTermStages; }
        }

        /// <summary>The round's own bar the lab's header splits: the real round's, so the line
        /// the player sees and the lab's books are the same round.</summary>
        private long LabOwnBar
        {
            get
            {
                RoundEngine round = session.CurrentRound;
                return round != null ? round.OwnBar : 900;
            }
        }

        /// <summary>
        /// A WHOLE SCREEN in debt, as the lab's books have it: the ledger's state, the pressure's
        /// facts (through FeedPressure, on the real board and score line) and the score line's
        /// numbers. <paramref name="loanBase"/> is what the loan opened at, so a paid-down loan
        /// presses less. <paramref name="settle"/> jumps the look to its state at once; without
        /// it the screen EASES there, which is what the transition scenes are for.
        /// </summary>
        private void LabScreen(CreditDeadline deadline, long debt, long loanBase, long stageStart,
            long minimumPaid, int termLeft, bool settle)
        {
            DebtLedgerView.State s = LabState(debt, stageStart, minimumPaid, termLeft, deadline, true);
            if (debt > 0)
            {
                ledgerView.ShowState(s);
            }
            long own = LabOwnBar;
            creditLabPressure = new PressureFacts
            {
                Active = debt > 0,
                Deadline = debt > 0 ? deadline : CreditDeadline.None,
                DebtShare = loanBase > 0 ? debt / (float)loanBase : 0f,
                MinimumOwed = debt > 0 && minimumPaid < s.MinimumDue,
                OwnBar = own,
                Installment = debt > 0 ? s.MinimumDue : 0,
                RoundScore = minimumPaid > 0 ? own + minimumPaid : own / 3,
                MinimumSatisfied = s.MinimumSatisfied,
                InRound = true
            };
            creditLabHud = new LabScoreLine
            {
                Balance = -debt,
                RoundScore = creditLabPressure.RoundScore,
                PassBar = own + creditLabPressure.Installment
            };
            UpdateScoreHud();
            FeedPressure(creditLabPressure);
            if (settle)
            {
                debtPressure.Settle();
            }
        }

        private void LabLabel(string en, string tr)
        {
            animLastLabel = Loc.Pick(en, tr);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>A wait in the SCENE's time, so a 0.5x scene waits twice as long.</summary>
        private IEnumerator LabWait(float seconds, float rate)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime * rate;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ the routine

        private IEnumerator CreditRoutine(CredScene scene)
        {
            DebtLedgerView ledger = ledgerView;
            ForeclosureView fc = foreclosureView;
            ledger.Close(false);
            fc.Stop();
            ledger.PlaybackRate = 1f;
            debtPressure.PlaybackRate = 1f;
            fc.PlaybackRate = 1f;
            creditLabPressure = new PressureFacts();
            creditLabHud = null;
            UpdateScoreHud();
            debtPressure.Settle();
            PlaceLedger();
            int term = Term;
            if (IsPressureScene(scene))
            {
                yield return PressureRoutine(scene);
                animCredit = null;
                yield break;
            }
            switch (scene)
            {
                // ---------------- DEBT HUD
                case CredScene.Closed:
                    LabLabel("ledger: closed", "defter: kapalı");
                    break;
                case CredScene.Open:
                case CredScene.Debt2000:
                    ledger.ShowState(LabState(2000, 2000, 0, term, CreditDeadline.Safe, true));
                    LabLabel("ledger: open, debt 2000", "defter: açık, borç 2000");
                    break;
                case CredScene.Debt500:
                    ledger.ShowState(LabState(500, 500, 0, term, CreditDeadline.Safe, true));
                    LabLabel("ledger: debt 500", "defter: borç 500");
                    break;
                case CredScene.DebtStress:
                    ledger.ShowState(LabState(12345678, 12345678, 0, term, CreditDeadline.Pressure, true));
                    LabLabel("ledger: 12 345 678 - does the hero number fit?",
                        "defter: 12 345 678 - ana sayı sığıyor mu?");
                    break;
                case CredScene.Min0:
                case CredScene.Min40:
                case CredScene.Min99:
                {
                    long due = session.MinimumPaymentFor(2000);
                    int pct = scene == CredScene.Min0 ? 0 : scene == CredScene.Min40 ? 40 : 99;
                    long paid = due * pct / 100;
                    ledger.ShowState(LabState(2000 - paid, 2000, paid, term, CreditDeadline.Safe, true));
                    LabLabel("minimum " + pct + "% paid (" + paid + " of " + due + ")",
                        "asgarinin %" + pct + "'i ödendi (" + paid + " / " + due + ")");
                    break;
                }
                case CredScene.MinSatisfied:
                {
                    long due = session.MinimumPaymentFor(2000);
                    ledger.ShowState(LabState(2000 - due, 2000, due - 1, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.4f);
                    ledger.ShowState(LabState(2000 - due, 2000, due, term, CreditDeadline.Safe, true));
                    ledger.PlayMinimumSatisfied();
                    LabLabel("minimum satisfied", "asgari tamam");
                    break;
                }
                case CredScene.InterestPreview:
                    DebtLedgerView.Layers.ShowInterestPreview = true;
                    ledger.ShowState(LabState(2000, 2000, 0, term, CreditDeadline.Safe, true));
                    LabLabel("interest preview on (switch)", "faiz önizleme açık (anahtar)");
                    break;
                case CredScene.MaturityFull:
                    ledger.ShowState(LabState(2000, 2000, 0, term, CreditDeadline.Safe, true));
                    LabLabel("maturity: the whole term ahead", "vade: tamamı önde");
                    break;
                case CredScene.MaturityHalf:
                    ledger.ShowState(LabState(2000, 2000, 0, Mathf.Max(1, term / 2), CreditDeadline.Warning, true));
                    LabLabel("maturity: half gone", "vade: yarısı gitti");
                    break;
                case CredScene.Warning:
                    ledger.ShowState(LabState(2000, 2000, 0, 2, CreditDeadline.Warning, true));
                    LabLabel("WARNING: ink walks up the trim once, the term swells now and then",
                        "UYARI: mürekkep kenara bir kez yürür, vade arada şişer");
                    break;
                case CredScene.FinalDue:
                    ledger.ShowState(LabState(2000, 2000, 0, 1, CreditDeadline.FinalDue, true));
                    LabLabel("FINAL DUE: seal on the border, a heavy beat", "SON VADE: kenarda mühür, ağır vuruş");
                    break;

                // ---------------- EVENTS
                case CredScene.EvOpened:
                    PlaceLedgerForMarket();
                    ledger.PlayOpen(LabState(2000, 0, 0, term, CreditDeadline.Safe, false));
                    LabLabel("contract opens", "sözleşme açılıyor");
                    break;
                case CredScene.EvCarried:
                    yield return LabCarry(2000, 2000, term - 1, CreditDeadline.Pressure);
                    break;
                case CredScene.EvPaySmall:
                case CredScene.EvPayLarge:
                {
                    long amount = scene == CredScene.EvPaySmall ? 120 : 900;
                    ledger.ShowState(LabState(2000, 2000, 0, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlayPayment(amount, CreditRoundAnchor(),
                        LabState(2000 - amount, 2000, amount, term, CreditDeadline.Safe, true));
                    LabLabel("payment -" + amount, "ödeme -" + amount);
                    break;
                }
                case CredScene.EvPayCrossesMinimum:
                {
                    long due = session.MinimumPaymentFor(2000);
                    ledger.ShowState(LabState(2000 - due / 2, 2000, due / 2, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlayPayment(due, CreditRoundAnchor(),
                        LabState(2000 - due / 2 - due, 2000, due + due / 2, term, CreditDeadline.Safe, true));
                    LabLabel("payment crosses the minimum", "ödeme asgariyi geçiyor");
                    break;
                }
                case CredScene.EvInterest:
                {
                    long debt = 1750;
                    long interest = session.InterestFor(debt);
                    ledger.ShowState(LabState(debt, 2000, 250, term - 1, CreditDeadline.Pressure, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlayInterest(debt, interest,
                        LabState(debt + interest, 0, 0, term - 1, CreditDeadline.Pressure, false));
                    LabLabel("interest +" + interest + " on " + debt, "faiz +" + interest + " (" + debt + " üzerine)");
                    break;
                }
                case CredScene.EvEarlyRepayment:
                    ledger.ShowState(LabState(300, 2000, 1700, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlayPayment(300, CreditRoundAnchor(), LabState(0, 2000, 2000, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.7f);
                    ledger.PlaySettled(0);
                    LabLabel("paid off in the first stage", "ilk aşamada kapandı");
                    break;
                case CredScene.EvEarlyBonus:
                {
                    long bonus = (2000L * session.Config.Market.CreditOnTimeBonusPercent + 99) / 100;
                    ledger.ShowState(LabState(300, 2000, 1700, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlaySettled(bonus);
                    LabLabel("the bank's thanks +" + bonus, "bankanın teşekkürü +" + bonus);
                    break;
                }
                case CredScene.EvNormalClear:
                    ledger.ShowState(LabState(400, 2000, 500, 2, CreditDeadline.Warning, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlaySettled(0);
                    LabLabel("cleared after interest - no thanks", "faizden sonra kapandı - ödül yok");
                    break;

                // ---------------- DEADLINE (whole screens now: the pressure answers the term)
                case CredScene.DlSafe:
                    LabScreen(CreditDeadline.Safe, 2000, 2000, 2000, 0, term, true);
                    LabLabel("SAFE: a rare sheen, the edges barely touched", "GÜVENLİ: seyrek parıltı, kenarlar zar zor");
                    break;
                case CredScene.DlPressure:
                    LabScreen(CreditDeadline.Pressure, 2000, 2000, 2000, 0, Mathf.Max(3, term - 1), true);
                    LabLabel("PRESSURE: the burgundy comes through, brackets appear",
                        "BASKI: bordo görünüyor, köşebentler beliriyor");
                    break;
                case CredScene.DlWarning:
                    LabScreen(CreditDeadline.Warning, 2000, 2000, 2000, 0, 2, true);
                    LabLabel("WARNING: two stages left", "UYARI: iki aşama kaldı");
                    break;
                case CredScene.DlFinal:
                    LabScreen(CreditDeadline.FinalDue, 2000, 2000, 2000, 0, 1, true);
                    yield return new WaitForSeconds(0.2f);
                    ledger.PlayFinalDueStamp();
                    LabLabel("FINAL DUE, with its stamp", "SON VADE, damgasıyla");
                    break;
                case CredScene.DlFinalScreen:
                    LabScreen(CreditDeadline.FinalDue, 2000, 2000, 2000, 0, 1, true);
                    LabLabel("FINAL DUE: the edges of the world get heavier, the board does not",
                        "SON VADE: dünyanın kenarları ağırlaşıyor, tahta değil");
                    break;

                // ---------------- FORECLOSURE
                case CredScene.FcStampOnly:
                    fc.PlayStampOnly(ForeclosureLedgerAt(), ForeclosureLedgerScale());
                    LabLabel("the HACİZ stamp", "HACİZ damgası");
                    break;
                case CredScene.FcFullEntry:
                    yield return LabForeclose(new[] { "hazine" }, null, 0, 0, 1, 1f, true);
                    break;
                case CredScene.FcLedgerPanel:
                    yield return LabForeclose(new string[0], null, 0, 0, 1, 1f, false);
                    break;
                case CredScene.FcAppraisal:
                    yield return LabForeclose(new[] { "kredi_karti" }, null, 0, 0, 1, 0.5f, true);
                    break;
                case CredScene.FcJoker:
                    yield return LabForeclose(new[] { "deprem" }, null, 0, 0, 1, 1f, true);
                    break;
                case CredScene.FcPower:
                    yield return LabForeclose(new string[0], new[] { "buyutec" }, 0, 0, 1, 1f, true);
                    break;
                case CredScene.FcElemental:
                    yield return LabForeclose(new string[0], null, 1, 0, 1, 1f, true);
                    break;
                case CredScene.FcBasic:
                    yield return LabForeclose(new string[0], null, 0, 1, 1, 1f, true);
                    break;
                case CredScene.Fc600:
                    yield return Lab600();
                    break;
                case CredScene.FcToDrawer:
                    ForeclosureView.Layers.ShowAssetTravelPath = true;
                    ForeclosureView.Layers.ShowBankDrawerAnchor = true;
                    yield return LabForeclose(new[] { "midas" }, null, 0, 0, 1, 0.5f, true);
                    break;
                case CredScene.FcToDebt:
                    ForeclosureView.Layers.ShowDebtTransferPath = true;
                    yield return LabForeclose(new[] { "midas" }, null, 0, 0, 1, 0.5f, true);
                    break;
                case CredScene.FcThreeHigh:
                    yield return LabForeclose(new[] { "hazine", "kredi_karti", "deprem" }, null, 0, 0, -1, 1f, true);
                    break;
                case CredScene.FcTenBasic:
                    yield return LabForeclose(new string[0], null, 0, 10, -1, 1f, true);
                    break;
                case CredScene.FcClearsMid:
                    yield return LabForeclose(new[] { "hazine", "deprem", "midas" }, new[] { "klon" }, 1, 3, 2, 1f, true);
                    break;
                case CredScene.FcComplete:
                    yield return LabForeclose(new[] { "altin_kumbara", "midas" }, null, 0, 0, 2, 1f, true);
                    break;
                case CredScene.FcRemainsDebt:
                    yield return LabForeclose(new[] { "midas" }, new[] { "cerceve" }, 1, 3, -1, 1f, true);
                    break;
                case CredScene.Fc1x:
                case CredScene.Fc05x:
                    yield return LabForeclose(new[] { "hazine", "deprem" }, new[] { "buyutec" }, 2, 4, -1,
                        scene == CredScene.Fc1x ? 1f : 0.5f, true);
                    break;
                case CredScene.FcAppraisal025x:
                    yield return LabForeclose(new[] { "hazine" }, null, 0, 0, 1, 0.25f, true);
                    break;
            }
            animCredit = null;
        }

        private static bool IsPressureScene(CredScene scene)
        {
            return scene >= CredScene.PxOldHud;
        }

        /// <summary>The corrective pass's scenes, A-V and the acceptance test.</summary>
        private IEnumerator PressureRoutine(CredScene scene)
        {
            DebtLedgerView ledger = ledgerView;
            int term = Term;
            const long debt = 2590;
            switch (scene)
            {
                case CredScene.PxOldHud:
                {
                    // The first pass, drawn by the same view at its old place and size, with
                    // nothing around it pressing at all.
                    float halfH = cam.orthographicSize;
                    float halfW = halfH * cam.aspect;
                    UiLayout layout = UiLayout.Active;
                    float column = (layout.JokerColumns * layout.JokerPanel.x
                        + (layout.JokerColumns - 1) * layout.JokerGap + layout.CornerInset) * CanvasToWorld;
                    float left = MainBoardCenter.x + MainBoardWorldSize * 0.5f + 0.15f;
                    float right = cam.transform.position.x + halfW - column - 0.15f;
                    float w = Mathf.Clamp(right - left, 1.5f, 2.6f);
                    ledger.ShowState(LabState(debt, debt, 0, term, CreditDeadline.Safe, true));
                    ledger.SetPlacement(DebtLedgerView.Form.Legacy, 0f, new Vector2((left + right) * 0.5f,
                        MainBoardCenter.y + MainBoardWorldSize * 0.5f - 0.55f), w / DebtLedgerView.Style.LegacyWidth);
                    creditLabHud = new LabScoreLine { Balance = -debt, RoundScore = 0,
                        PassBar = LabOwnBar + session.MinimumPaymentFor(debt) };
                    UpdateScoreHud();
                    LabLabel("A: the first pass - " + PanelPixels(ledger) + ", nothing else on screen knows about the debt",
                        "A: ilk hali - " + PanelPixels(ledger) + ", ekranın geri kalanı borçtan habersiz");
                    break;
                }
                case CredScene.PxNewHud:
                    LabScreen(CreditDeadline.Safe, debt, debt, debt, 0, term, true);
                    LabLabel("B: the new ledger - " + PanelPixels(ledger) + " (" + ledger.CurrentForm + ")",
                        "B: yeni defter - " + PanelPixels(ledger) + " (" + ledger.CurrentForm + ")");
                    break;
                case CredScene.PxFit1080:
                case CredScene.PxFit768:
                {
                    // 16:9 at any resolution is the same WORLD layout; what changes is how many
                    // pixels a unit gets. The solver is asked about a 16:9 screen, the panel is put
                    // where it says, and the label prints the real pixel sizes at that height.
                    int lines = scene == CredScene.PxFit1080 ? 1080 : 768;
                    LabScreen(CreditDeadline.Warning, debt, debt, debt, 312, 2, true);
                    LedgerPlacement p = SolveLedgerPlacement(5f * 16f / 9f, 5f, false);
                    ledger.SetPlacement(p.Form, p.Width, p.Centre, p.Scale);
                    DebtLedgerView.Layers.ShowDebtPanelBounds = true;
                    LabLabel(FitReport(p, lines, 5f), FitReport(p, lines, 5f));
                    break;
                }
                case CredScene.PxResponsive:
                {
                    LabScreen(CreditDeadline.Warning, debt, debt, debt, 312, 2, true);
                    DebtLedgerView.Layers.ShowDebtPanelBounds = true;
                    // the solver asked about three other windows, one after another
                    float[] aspects = { 16f / 10f, 4f / 3f, 21f / 9f, 16f / 9f };
                    string[] names = { "16:10", "4:3", "21:9", "16:9" };
                    for (int round = 0; round < 2; round++)
                    {
                        for (int i = 0; i < aspects.Length; i++)
                        {
                            LedgerPlacement p = SolveLedgerPlacement(5f * aspects[i], 5f, false);
                            ledger.SetPlacement(p.Form, p.Width, p.Centre, p.Scale);
                            LabLabel("E: " + names[i] + " -> " + p.Form + ", room " + p.Room.ToString("0.00")
                                    + " world, " + FitReport(p, 1080, 5f),
                                "E: " + names[i] + " -> " + p.Form + ", yer " + p.Room.ToString("0.00")
                                    + " birim, " + FitReport(p, 1080, 5f));
                            yield return new WaitForSeconds(2.2f);
                        }
                    }
                    PlaceLedger();
                    break;
                }
                case CredScene.PxSafe:
                    LabScreen(CreditDeadline.Safe, debt, debt, debt, 0, term, true);
                    LabLabel("F: SAFE - a debt, but time: the edges barely touched", "F: GÜVENLİ - borç var ama zaman var");
                    break;
                case CredScene.PxPressure:
                    LabScreen(CreditDeadline.Pressure, 2400, 2590, 2400, 0, Mathf.Max(3, term - 1), true);
                    LabLabel("G: PRESSURE - burgundy through the ledger, faint brackets, the edges heavier",
                        "G: BASKI - defterde bordo, silik köşebentler, kenarlar ağır");
                    break;
                case CredScene.PxWarning:
                    LabScreen(CreditDeadline.Warning, 2300, 2590, 2300, 0, 2, true);
                    LabLabel("H: WARNING - two stages, ink on the trim, brackets 2 px in",
                        "H: UYARI - iki aşama, kenarda mürekkep, köşebentler 2 px içeride");
                    break;
                case CredScene.PxFinal:
                    LabScreen(CreditDeadline.FinalDue, 1340, 2000, 1340, 0, 1, true);
                    LabLabel("I: FINAL DUE - one warm segment, brackets 5 px in, the world a shade duller",
                        "I: SON VADE - tek sıcak segment, köşebentler 5 px içeride, dünya bir tık cansız");
                    break;
                case CredScene.PxFreeVsSafe:
                case CredScene.PxSafeVsFinal:
                {
                    bool freeFirst = scene == CredScene.PxFreeVsSafe;
                    for (int i = 0; i < 8; i++)
                    {
                        bool a = i % 2 == 0;
                        if (freeFirst && a)
                        {
                            ledger.Close(false);
                            LabScreen(CreditDeadline.None, 0, 0, 0, 0, 0, true);
                            LabLabel("J: A - no debt", "J: A - borçsuz");
                        }
                        else if (freeFirst || a)
                        {
                            LabScreen(CreditDeadline.Safe, debt, debt, debt, 0, term, true);
                            LabLabel((freeFirst ? "J" : "K") + ": " + (freeFirst ? "B" : "A") + " - SAFE",
                                (freeFirst ? "J" : "K") + ": " + (freeFirst ? "B" : "A") + " - GÜVENLİ");
                        }
                        else
                        {
                            LabScreen(CreditDeadline.FinalDue, debt, debt, debt, 0, 1, true);
                            LabLabel("K: B - FINAL DUE", "K: B - SON VADE");
                        }
                        yield return new WaitForSeconds(1.8f);
                    }
                    break;
                }
                case CredScene.PxRelief:
                    yield return LabRelief(2000, 600, CreditDeadline.Warning, 2, 1f);
                    break;
                case CredScene.PxLargeRelief:
                    yield return LabRelief(2000, 1500, CreditDeadline.Warning, 2, 1f);
                    break;
                case CredScene.PxInterest:
                    yield return LabInterest(2000, 300, 1f);
                    break;
                case CredScene.PxMinimum:
                {
                    long due = session.MinimumPaymentFor(2000);
                    LabScreen(CreditDeadline.Warning, 2000 - due / 2, 2000, 2000, due / 2, 2, true);
                    yield return new WaitForSeconds(0.6f);
                    ledger.PlayPayment(due, CreditRoundAnchor(),
                        LabState(2000 - due / 2 - due, 2000, due + due / 2, 2, CreditDeadline.Warning, true));
                    yield return new WaitForSeconds(DebtLedgerView.Style.Coalesce + DebtLedgerView.Style.PaymentTravel);
                    LabScreen(CreditDeadline.Warning, 2000 - due / 2 - due, 2000, 2000, due + due / 2, 2, false);
                    LabLabel("O: the minimum paid - a click, a little breath, the burden secured",
                        "O: asgari ödendi - klik, küçük bir nefes, yük güvende");
                    break;
                }
                case CredScene.PxStamp:
                    LabScreen(CreditDeadline.FinalDue, 1340, 2000, 1340, 0, 1, true);
                    yield return new WaitForSeconds(0.4f);
                    ledger.PlayFinalDueStamp();
                    LabLabel("P: SON VADE - forward, TOK, back", "P: SON VADE - öne, TOK, geri");
                    break;
                case CredScene.PxBracketsOnly:
                case CredScene.PxVignetteOnly:
                case CredScene.PxHeaderOnly:
                {
                    string only = scene == CredScene.PxBracketsOnly ? "brackets"
                        : scene == CredScene.PxVignetteOnly ? "vignette" : "header";
                    DebtPressurePresentationController.Layers.Only(only);
                    if (scene == CredScene.PxHeaderOnly)
                    {
                        long due = session.MinimumPaymentFor(debt);
                        LabScreen(CreditDeadline.Warning, debt - due / 3, debt, debt, due / 3, 2, true);
                    }
                    else
                    {
                        LabScreen(CreditDeadline.FinalDue, debt, debt, debt, 0, 1, true);
                    }
                    ledger.Close(false);
                    LabLabel("only the " + only + " (the ledger is off)", "yalnız " + only + " (defter kapalı)");
                    break;
                }
                case CredScene.PxCarry:
                    yield return LabCarry(debt, debt, term - 1, CreditDeadline.Pressure);
                    break;
                case CredScene.PxToForeclosure:
                {
                    LabScreen(CreditDeadline.FinalDue, 900, 2000, 900, 0, 1, true);
                    LabLabel("U: the last stage ends with 900 owed", "U: son aşama 900 borçla bitiyor");
                    yield return new WaitForSeconds(0.8f);
                    ledger.PlayContractLock(1);
                    while (ledger.Busy)
                    {
                        yield return null;
                    }
                    LabLabel("U: the contract closed - now the bailiff", "U: sözleşme kapandı - şimdi haciz");
                    yield return LabForeclose(new[] { "midas", "deprem" }, null, 1, 3, -1, 1f, true);
                    break;
                }
                case CredScene.PxHalfSpeed:
                    yield return LabEscalation(0.5f);
                    break;
                case CredScene.PxAcceptance:
                {
                    // THE MAIN ACCEPTANCE TEST: the ledger hidden; A is debt-free, B is a large
                    // debt with the minimum unpaid and one stage left. The screen alone has to say
                    // which is dangerous - without looking like a boss attack.
                    for (int i = 0; i < 6; i++)
                    {
                        bool b = i % 2 == 1;
                        if (b)
                        {
                            LabScreen(CreditDeadline.FinalDue, 4800, 4800, 4800, 0, 1, true);
                        }
                        else
                        {
                            ledger.Close(false);
                            LabScreen(CreditDeadline.None, 0, 0, 0, 0, 0, true);
                        }
                        ledger.transform.localScale = new Vector3(0.0001f, 0.0001f, 1f);
                        LabLabel(b ? "ACCEPTANCE B: 4800 owed, minimum unpaid, last stage - ledger hidden"
                                : "ACCEPTANCE A: no debt - ledger hidden",
                            b ? "KABUL B: 4800 borç, asgari ödenmedi, son aşama - defter gizli"
                                : "KABUL A: borçsuz - defter gizli");
                        yield return new WaitForSeconds(2f);
                    }
                    LabScreen(CreditDeadline.FinalDue, 4800, 4800, 4800, 0, 1, true);
                    PlaceLedger();
                    LabLabel("ACCEPTANCE: B again, with the ledger", "KABUL: yine B, defterle");
                    break;
                }
            }
        }

        /// <summary>A payment on the lab's screen: the ledger's chip, then - as it lands - the
        /// screen's steady look moves to the smaller debt, with the relief on top.</summary>
        private IEnumerator LabRelief(long debt, long amount, CreditDeadline deadline, int termLeft, float rate)
        {
            ledgerView.PlaybackRate = rate;
            debtPressure.PlaybackRate = rate;
            LabScreen(deadline, debt, debt, debt, 0, termLeft, true);
            LabLabel("relief: " + debt + " owed, " + amount + " about to be paid",
                "rahatlama: " + debt + " borç, " + amount + " ödenecek");
            yield return LabWait(0.8f, rate);
            ledgerView.PlayPayment(amount, CreditRoundAnchor(),
                LabState(debt - amount, debt, amount, termLeft, deadline, true));
            // the chip's travel (coalesce + flight), in the scene's time
            yield return LabWait(DebtLedgerView.Style.Coalesce + DebtLedgerView.Style.PaymentTravel, rate);
            LabScreen(deadline, debt - amount, debt, debt, amount, termLeft, false);
            LabLabel("relief: " + debt + " -> " + (debt - amount) + " - lighter, not free",
                "rahatlama: " + debt + " -> " + (debt - amount) + " - hafifledi, bitmedi");
        }

        /// <summary>Interest on the lab's screen: the ledger's ink and roll, and the screen a
        /// shade heavier - steady, with the pain on top.</summary>
        private IEnumerator LabInterest(long debt, long interest, float rate)
        {
            ledgerView.PlaybackRate = rate;
            debtPressure.PlaybackRate = rate;
            LabScreen(CreditDeadline.Pressure, debt, debt, debt, 0, 3, true);
            LabLabel("interest: " + debt + " owed", "faiz: " + debt + " borç");
            yield return LabWait(0.8f, rate);
            DebtLedgerView.State after = LabState(debt + interest, debt, 0, 3, CreditDeadline.Pressure, true);
            ledgerView.PlayInterest(debt, interest, after);
            yield return LabWait(DebtLedgerView.Style.InterestCreep * 0.6f, rate);
            LabScreen(CreditDeadline.Pressure, debt + interest, debt, debt, 0, 3, false);
            LabLabel("interest +" + interest + ": the debt grew by itself, the edges close a little",
                "faiz +" + interest + ": borç kendi kendine büyüdü, kenarlar biraz kapandı");
        }

        /// <summary>The carry on the lab's screen: the purse, the slip, and the TOTAL rolling down
        /// under the debt when it lands - the lab's own roll over the same seam and timing the
        /// game uses.</summary>
        private IEnumerator LabCarry(long debt, long loanBase, int termLeft, CreditDeadline deadline)
        {
            LabScreen(deadline, debt, loanBase, debt, 0, termLeft, true);
            long passBar = creditLabHud.Value.PassBar;
            creditLabHud = new LabScoreLine { Balance = 0, RoundScore = 0, PassBar = passBar };
            UpdateScoreHud();
            yield return new WaitForSeconds(0.4f);
            creditCarryLanded = false;
            ledgerView.PlayCarry(LabState(debt, debt, 0, termLeft, deadline, true));
            LabLabel("T: the stage starts at the purse - the debt is on its way",
                "T: aşama keseden başlıyor - borç yolda");
            float guard = 0f;
            while (!creditCarryLanded && guard < 2f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.35f);
                float eased = 1f - (1f - u) * (1f - u) * (1f - u);
                creditLabHud = new LabScoreLine { Balance = -(long)Mathf.Round(debt * eased), RoundScore = 0,
                    PassBar = passBar };
                UpdateScoreHud();
                yield return null;
            }
            LabLabel("T: the debt dragged the TOTAL down", "T: borç TOPLAM'ı aşağı çekti");
        }

        /// <summary>The whole escalation, stage by stage, at the given rate: SAFE, a tick into
        /// PRESSURE, into WARNING, into the final stage with its stamp - and a payment at the end
        /// to show the relief against it.</summary>
        private IEnumerator LabEscalation(float rate)
        {
            ledgerView.PlaybackRate = rate;
            debtPressure.PlaybackRate = rate;
            int term = Term;
            long debt = 2590;
            LabScreen(CreditDeadline.Safe, debt, debt, debt, 0, term, true);
            LabLabel("V: SAFE", "V: GÜVENLİ");
            yield return LabWait(2.2f, rate);
            CreditDeadline[] steps = { CreditDeadline.Pressure, CreditDeadline.Warning, CreditDeadline.FinalDue };
            int left = term;
            for (int i = 0; i < steps.Length && left > 1; i++)
            {
                int next = steps[i] == CreditDeadline.FinalDue ? 1 : steps[i] == CreditDeadline.Warning ? 2 : left - 1;
                if (next >= left)
                {
                    continue;
                }
                ledgerView.PlayTermTick(left, next);
                yield return LabWait(DebtLedgerView.Style.TickDuration + 0.1f, rate);
                left = next;
                debt += session.InterestFor(debt);
                LabScreen(steps[i], debt, 2590, debt, 0, left, false);
                if (steps[i] == CreditDeadline.FinalDue)
                {
                    ledgerView.PlayFinalDueStamp();
                }
                LabLabel("V: " + steps[i] + " (" + left + " left)", "V: " + steps[i] + " (" + left + " kaldı)");
                yield return LabWait(2.6f, rate);
            }
            long pay = debt * 3 / 5;
            ledgerView.PlayPayment(pay, CreditRoundAnchor(), LabState(debt - pay, debt, pay, left, CreditDeadline.FinalDue, true));
            yield return LabWait(DebtLedgerView.Style.Coalesce + DebtLedgerView.Style.PaymentTravel, rate);
            LabScreen(CreditDeadline.FinalDue, debt - pay, 2590, debt, pay, left, false);
            LabLabel("V: paid " + pay + " in the last stage - the screen breathes, the term does not move",
                "V: son aşamada " + pay + " ödendi - ekran nefes alıyor, vade kıpırdamıyor");
        }

        /// <summary>The panel as the player gets it, in pixels on this screen.</summary>
        private string PanelPixels(DebtLedgerView ledger)
        {
            float perWorld = cam.pixelHeight / (2f * cam.orthographicSize);
            Vector2 size = ledger.WorldSize * perWorld;
            return Mathf.RoundToInt(size.x) + " x " + Mathf.RoundToInt(size.y) + " px";
        }

        /// <summary>What a placement comes to at a screen height of <paramref name="lines"/>: the
        /// panel and its three hero numbers in pixels.</summary>
        private static string FitReport(LedgerPlacement p, int lines, float orthoSize)
        {
            float perWorld = lines / (2f * orthoSize);
            Vector2 size = DebtLedgerView.LocalSize(p.Form, p.Width) * p.Scale * perWorld;
            float unit = p.Scale * perWorld;
            return lines + "p: " + p.Form + " " + Mathf.RoundToInt(size.x) + " x " + Mathf.RoundToInt(size.y)
                + " px, digits " + Mathf.RoundToInt(DebtLedgerView.DigitHeight(DebtLedgerView.Style.DebtSize) * unit)
                + " / " + Mathf.RoundToInt(DebtLedgerView.DigitHeight(DebtLedgerView.Style.MinimumSize) * unit)
                + " / " + Mathf.RoundToInt(DebtLedgerView.DigitHeight(DebtLedgerView.Style.TermSize) * unit)
                + " px (debt / minimum / term)";
        }

        private void PlaceLedgerForMarket()
        {
            if (session.Phase != GamePhase.Market)
            {
                return; // in a round the round's own place is the right one
            }
            float halfH = cam.orthographicSize;
            LedgerPlacement p = SolveLedgerPlacement(halfH * cam.aspect, halfH, true);
            ledgerView.SetPlacement(p.Form, p.Width, p.Centre, p.Scale);
        }

        // ------------------------------------------------------------------ foreclosures

        /// <summary>
        /// A foreclosure RUN BY CORE on a scratch session of the lab's own: these jokers and powers,
        /// <paramref name="elemental"/> elemental blocks and <paramref name="plain"/> seizable
        /// plain blocks (the deck keeps its hand-size floor on top). <paramref name="takeCount"/>
        /// sets the debt so Core stops after that many things (asked of Core itself, from a
        /// dry run); -1 means a debt nothing covers.
        /// </summary>
        private IEnumerator LabForeclose(string[] jokers, string[] powers, int elemental, int plain,
            int takeCount, float rate, bool withAssets)
        {
            CreditStatement dry = ScratchForeclosure(jokers, powers, elemental, plain, long.MaxValue / 4);
            // "Nothing covers it" is everything Core would take, plus a remainder to write off -
            // a readable number rather than an astronomical one.
            long everything = 0;
            foreach (SeizedItem item in dry.Seized)
            {
                everything += item.Credited;
            }
            long debt = everything + 1500;
            if (takeCount > 0 && dry.Seized.Count > 0)
            {
                long sum = 0;
                for (int i = 0; i < dry.Seized.Count && i < takeCount; i++)
                {
                    sum += dry.Seized[i].Credited;
                }
                // stops inside the last one: its value covers what is left
                long last = dry.Seized[Mathf.Min(takeCount, dry.Seized.Count) - 1].Credited;
                debt = System.Math.Max(1L, sum - last / 2);
            }
            CreditStatement statement = withAssets
                ? ScratchForeclosure(jokers, powers, elemental, plain, debt)
                : new CreditStatement { DebtBeforeForeclosure = 2000, DebtAfter = 2000 };
            var assets = new List<ForeclosureView.Asset>();
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            int ji = 0, pi = 0;
            foreach (SeizedItem item in statement.Seized)
            {
                Vector2 source;
                if (item.Kind == SeizedKind.Joker)
                {
                    source = c + new Vector2(halfW - 0.9f - (ji % 2) * 1.5f, halfH - 1.2f - (ji / 2) * 2.1f);
                    ji++;
                }
                else if (item.Kind == SeizedKind.Power)
                {
                    source = c + new Vector2(-halfW + 0.9f, halfH - 1.0f - pi * 1.7f);
                    pi++;
                }
                else
                {
                    source = CardLayerView.DrawPilePos;
                }
                assets.Add(new ForeclosureView.Asset { Item = item, Source = source });
            }
            if (!ledgerView.IsOpen)
            {
                ledgerView.ShowState(LabState(statement.DebtBeforeForeclosure, 0, 0, 0, CreditDeadline.FinalDue, false));
            }
            else
            {
                ledgerView.RollDebtTo(statement.DebtBeforeForeclosure);
            }
            foreclosureView.PlaybackRate = rate;
            ledgerView.PlaybackRate = rate;
            foreclosureView.Play(statement, assets, ForeclosureLedgerAt(), ForeclosureLedgerScale());
            LabLabel("foreclosure: " + statement.Seized.Count + " taken, debt "
                    + statement.DebtBeforeForeclosure + " -> " + statement.DebtAfter
                    + (statement.WrittenOff > 0 ? ", " + statement.WrittenOff + " written off" : "")
                    + (rate < 0.99f ? "  " + rate + "x" : ""),
                "haciz: " + statement.Seized.Count + " alındı, borç "
                    + statement.DebtBeforeForeclosure + " -> " + statement.DebtAfter
                    + (statement.WrittenOff > 0 ? ", " + statement.WrittenOff + " silindi" : "")
                    + (rate < 0.99f ? "  " + rate + "x" : ""));
            while (foreclosureView.Playing)
            {
                yield return null;
            }
            if (ledgerView.IsOpen)
            {
                ledgerView.PlaySettled(0);
            }
            creditLabPressure = new PressureFacts();
        }

        /// <summary>The design's own worked example, by hand: a joker worth 600 taken for 300.</summary>
        private IEnumerator Lab600()
        {
            var statement = new CreditStatement { DebtBeforeForeclosure = 300, DebtAfter = 0 };
            statement.Seized.Add(new SeizedItem
            {
                Kind = SeizedKind.Joker,
                Name = "Hazine",
                DefId = "hazine",
                Rarity = Rarity.Rare,
                Value = 600,
                Credited = 300,
                DebtBefore = 300,
                DebtAfter = 0
            });
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            var assets = new List<ForeclosureView.Asset>
            {
                new ForeclosureView.Asset { Item = statement.Seized[0], Source = c + new Vector2(halfW - 0.9f, halfH - 1.2f) }
            };
            ledgerView.ShowState(LabState(300, 0, 0, 0, CreditDeadline.FinalDue, false));
            foreclosureView.Play(statement, assets, ForeclosureLedgerAt(), ForeclosureLedgerScale());
            LabLabel("600 -> 300: the worked example", "600 -> 300: tasarımın örneği");
            while (foreclosureView.Playing)
            {
                yield return null;
            }
            if (ledgerView.IsOpen)
            {
                ledgerView.PlaySettled(0);
            }
        }

        /// <summary>A scratch session of the lab's own, with exactly these things in it, foreclosed
        /// by Core for this debt. Never the player's session.</summary>
        private CreditStatement ScratchForeclosure(string[] jokers, string[] powers, int elemental, int plain, long debt)
        {
            var shapes = new List<BlockShape>();
            int floor = session.Config.Rules.HandSize;
            for (int i = 0; i < plain + floor; i++)
            {
                shapes.Add(LabShape(i));
            }
            var config = new GameConfig { RngSeed = 4242 };
            config.Deck = new DeckDefinition("lab", shapes, DeckLibrary.Classic.ShapeGenerator);
            var scratch = new GameSession(config);
            if (jokers != null)
            {
                foreach (string id in jokers)
                {
                    Joker j = JokerRegistry.Create(id);
                    if (j != null)
                    {
                        scratch.Jokers.Add(j);
                    }
                }
            }
            if (powers != null)
            {
                foreach (string id in powers)
                {
                    Power p = PowerRegistry.Create(id);
                    if (p != null)
                    {
                        scratch.Powers.Add(p);
                    }
                }
            }
            var extra = new List<BlockCard>();
            BlockElement[] kinds = { BlockElement.Fire, BlockElement.Gold, BlockElement.Water, BlockElement.Obsidian };
            for (int i = 0; i < elemental; i++)
            {
                extra.Add(scratch.CreateCard(LabShape(i + 1), new[] { kinds[i % kinds.Length] }));
            }
            return scratch.ForecloseForLab(debt, extra);
        }

        private static BlockShape LabShape(int i)
        {
            var cells = new List<GridPos>();
            switch (i % 3)
            {
                case 0:
                    cells.Add(new GridPos(0, 0));
                    cells.Add(new GridPos(1, 0));
                    cells.Add(new GridPos(2, 0));
                    break;
                case 1:
                    cells.Add(new GridPos(0, 0));
                    cells.Add(new GridPos(1, 0));
                    break;
                default:
                    cells.Add(new GridPos(0, 0));
                    cells.Add(new GridPos(0, 1));
                    cells.Add(new GridPos(1, 0));
                    break;
            }
            return BlockShape.FromCells(cells);
        }
    }
}
