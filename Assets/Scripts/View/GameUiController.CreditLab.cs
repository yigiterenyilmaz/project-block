// PURPOSE: The animation lab's "KREDİ / BORÇ / HACİZ" section - every look of the BLACK LEDGER
// (DebtLedgerView) and every beat of the foreclosure (ForeclosureView), on demand.
//
// The lab's rule holds: the scenes call the SAME view methods the game calls and fabricate only
// the ARGUMENTS. The ledger's states are built from the session's own arithmetic (MinimumPaymentFor,
// InterestFor, the configured rate and term), never from numbers written here. The foreclosures are
// RUN BY CORE: each scene builds a scratch GameSession of its own (never the player's), gives it the
// jokers, powers and blocks the scene is about, and asks GameSession.ForecloseForLab for the
// statement - so the order things go in, their values, their half prices and where the debt ends
// are the rules' answers. Only "600 -> 300" is written by hand, because it is the design's own
// worked example. While a scene runs the lab owns both views (creditLabOwnsViews), and RESET
// hands them back to the game, which resyncs without replaying anything as an event.

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
            FcRemainsDebt, Fc1x, Fc05x, FcAppraisal025x
        }

        private Coroutine animCredit;
        private bool animCreditUsed;

        private void AddCreditLabAnims()
        {
            AddAnimSub("jokers", "kredi", "kredi / borç / haciz", "kredi / borç / haciz");
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

            AddCredToggle("panel bounds", "panel sınırları",
                delegate { return DebtLedgerView.Layers.ShowDebtPanelBounds; },
                delegate (bool v) { DebtLedgerView.Layers.ShowDebtPanelBounds = v; });
            AddCredToggle("debt value (dev)", "borç değeri (geliştirici)",
                delegate { return DebtLedgerView.Layers.ShowDebtValue; },
                delegate (bool v) { DebtLedgerView.Layers.ShowDebtValue = v; });
            AddCredToggle("minimum marker", "asgari işareti",
                delegate { return DebtLedgerView.Layers.ShowMinimumMarker; },
                delegate (bool v) { DebtLedgerView.Layers.ShowMinimumMarker = v; });
            AddCredToggle("minimum progress", "asgari ilerlemesi",
                delegate { return DebtLedgerView.Layers.ShowMinimumProgress; },
                delegate (bool v) { DebtLedgerView.Layers.ShowMinimumProgress = v; });
            AddCredToggle("maturity track", "vade izi",
                delegate { return DebtLedgerView.Layers.ShowMaturityTrack; },
                delegate (bool v) { DebtLedgerView.Layers.ShowMaturityTrack = v; });
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

        /// <summary>Called from StopAnimHost: hands both views back to the game.</summary>
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
            if (ledgerView != null)
            {
                ledgerView.PlaybackRate = 1f;
                ledgerView.Close(false);
                ledgerView.SetTargetChip(null, 0);
            }
            if (foreclosureView != null)
            {
                foreclosureView.PlaybackRate = 1f;
                foreclosureView.Stop();
                foreclosureView.SetFinalDueAtmosphere(0f);
            }
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

        private void LabLabel(string en, string tr)
        {
            animLastLabel = Loc.Pick(en, tr);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        // ------------------------------------------------------------------ the routine

        private IEnumerator CreditRoutine(CredScene scene)
        {
            DebtLedgerView ledger = ledgerView;
            ForeclosureView fc = foreclosureView;
            ledger.Close(false);
            fc.Stop();
            fc.SetFinalDueAtmosphere(0f);
            ledger.PlaybackRate = 1f;
            fc.PlaybackRate = 1f;
            PlaceLedger();
            int term = Term;
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
                case CredScene.DlWarning:
                    ledger.ShowState(LabState(2000, 2000, 0, 2, CreditDeadline.Warning, true));
                    LabLabel("WARNING: ink up the rim, a slow trim pulse", "UYARI: kenarda mürekkep, yavaş nabız");
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
                    ledger.ShowState(LabState(2000, 2000, 0, term - 1, CreditDeadline.Pressure, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlayCarry(LabState(2000, 2000, 0, term - 1, CreditDeadline.Pressure, true));
                    LabLabel("debt carried to the TOTAL", "borç TOPLAM'a taşındı");
                    break;
                case CredScene.EvPaySmall:
                case CredScene.EvPayLarge:
                {
                    long amount = scene == CredScene.EvPaySmall ? 120 : 900;
                    ledger.ShowState(LabState(2000, 2000, 0, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlayPayment(amount, ScoreWorldAnchor(),
                        LabState(2000 - amount, 2000, amount, term, CreditDeadline.Safe, true));
                    LabLabel("payment -" + amount, "ödeme -" + amount);
                    break;
                }
                case CredScene.EvPayCrossesMinimum:
                {
                    long due = session.MinimumPaymentFor(2000);
                    ledger.ShowState(LabState(2000 - due / 2, 2000, due / 2, term, CreditDeadline.Safe, true));
                    yield return new WaitForSeconds(0.3f);
                    ledger.PlayPayment(due, ScoreWorldAnchor(),
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
                    ledger.PlayPayment(300, ScoreWorldAnchor(), LabState(0, 2000, 2000, term, CreditDeadline.Safe, true));
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

                // ---------------- DEADLINE
                case CredScene.DlSafe:
                    ledger.ShowState(LabState(2000, 2000, 0, term, CreditDeadline.Safe, true));
                    LabLabel("SAFE: still", "GÜVENLİ: durgun");
                    break;
                case CredScene.DlPressure:
                    ledger.ShowState(LabState(2000, 2000, 0, Mathf.Max(3, term - 1), CreditDeadline.Pressure, true));
                    LabLabel("PRESSURE: the burgundy comes through", "BASKI: bordo görünüyor");
                    break;
                case CredScene.DlFinal:
                    ledger.ShowState(LabState(2000, 2000, 0, 1, CreditDeadline.FinalDue, true));
                    yield return new WaitForSeconds(0.2f);
                    ledger.PlayFinalDueStamp();
                    LabLabel("FINAL DUE, with its stamp", "SON VADE, damgasıyla");
                    break;
                case CredScene.DlFinalScreen:
                    ledger.ShowState(LabState(2000, 2000, 0, 1, CreditDeadline.FinalDue, true));
                    fc.SetFinalDueAtmosphere(1f);
                    LabLabel("FINAL DUE: the screen closes in a few percent", "SON VADE: ekran birkaç yüzde kapanıyor");
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

        private void PlaceLedgerForMarket()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            if (session.Phase != GamePhase.Market)
            {
                return; // in a round the round's own place is the right one
            }
            ledgerView.SetPlacement(new Vector2(c.x, c.y - halfH + 0.44f), Mathf.Min(2.5f, halfW * 1.1f));
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
            ledgerView.ShowState(LabState(statement.DebtBeforeForeclosure, 0, 0, 0, CreditDeadline.FinalDue, false));
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
