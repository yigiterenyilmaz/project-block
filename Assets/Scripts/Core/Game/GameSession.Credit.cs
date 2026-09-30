// PURPOSE: GameSession market credit ("Kredi kartı", designer's call 2026-09-30) - a LOAN rather
// than a tab. The joker is only the switch that lets the player borrow (CreditAvailable); every
// rule below is the session's, because debt is an economy rule and the economy is the session's.
//
//   THE PURSE GOES NEGATIVE. Buying past what you have borrows the shortfall, without a limit.
//     Balance (TotalScore - Debt) is what the player sees, so 2000 borrowed is a purse of -2000.
//   A ROUND PAYS IN ORDER (BankRoundScore, designer's call 2026-09-30): what the round earns up
//     to its OWN threshold is the player's and goes to the purse; what it earns above that - the
//     minimum payment, then overtime - pays the debt first. Money from outside a round (a sale,
//     an effect's grant) pays the debt first too (Receive). There is no manual repayment.
//   THE MINIMUM PAYMENT ("asgari"): a stage that STARTS in debt has to earn a share of that debt
//     (CreditMinimumPaymentPercent, 25%) ON TOP of its own bar before it can be passed
//     (RoundEngine.CreditInstallment - the bar rises, the threshold the jokers scale off does
//     not). The threshold first, then the minimum; past both is overtime's to earn, and every
//     point of overtime goes to the debt.
//   INTEREST at the end of every stage on what is still owed (CreditInterestPermille, 12.5%).
//   THE TERM ("vade"): a debt may be carried through CreditTermStages stages (4), counted from
//     the first one it was carried into; borrowing more on an open loan does not reset it.
//   FORECLOSURE ("haciz") when the term runs out with money still owed: jokers, powers and
//     elemental blocks are taken MOST VALUABLE FIRST at half their shelf price; with those gone,
//     plain blocks one by one for next to nothing - never below a playable deck (the hand size,
//     the floor the tax bosses keep too). What is still owed with nothing left to take is
//     written off. The FINAL stage is the last term there is: a debt the bailiff cannot cover
//     there loses the run (LossReason.DebtNotRepaid) - the one place that loss is still used.
//   THE BANK'S THANKS: a debt paid off in the FIRST stage it was carried into, before any
//     interest, earns a reward - points (CreditOnTimeBonusPercent of the loan) or a CAMPAIGN,
//     one offer in the next market cheaper bought on the card (CreditCampaignDiscountPercent).
//     Which one is drawn from the bank's OWN rng, so the main stream never moves.
//
// All numbers are MarketConfig balance placeholders. A run that never borrows is byte-identical
// to one without any of this (Receive is TotalScore += amount when nothing is owed).

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class GameSession
    {
        /// <summary>What the player owes, in the scaled run economy. 0 when debt-free.</summary>
        public long Debt { get; private set; }

        /// <summary>The purse as the player sees it: negative while in debt.</summary>
        public long Balance
        {
            get { return TotalScore - Debt; }
        }

        /// <summary>True while a held joker lets the player buy with points they do not have.</summary>
        public bool CreditAvailable
        {
            get { return Jokers.GrantsMarketCredit; }
        }

        /// <summary>True if this price is payable at all - out of the purse, or on credit.</summary>
        public bool CanAfford(long price)
        {
            return TotalScore >= price || CreditAvailable;
        }

        /// <summary>Stages the current debt has been carried through - interest charged at the
        /// end of each. What the term counts.</summary>
        public int CreditStagesCarried { get; private set; }

        /// <summary>The debt the stage in progress started with: what its minimum payment and the
        /// bank's thanks are measured on.</summary>
        public long DebtAtStageStart { get; private set; }

        /// <summary>What the stage in progress has paid off so far.</summary>
        public long DebtRepaidThisStage { get; private set; }

        /// <summary>Stages left before the bailiff comes, while in debt; 0 when debt-free.</summary>
        public int CreditTermLeft
        {
            get
            {
                return Debt > 0
                    ? Math.Max(0, Config.Market.CreditTermStages - CreditStagesCarried)
                    : 0;
            }
        }

        /// <summary>The last stage's statement - a NEW object whenever a stage settled any credit,
        /// so the View matches it by identity. Not saved.</summary>
        public CreditStatement LastCreditStatement { get; private set; }

        // ------------------------------------------------------------------- reporting
        // What the View reads to draw the loan. Queries over the state above, answered with the
        // SAME arithmetic the rules use (MinimumPaymentFor / InterestFor are what BeginCreditStage
        // and SettleCreditAtStageEnd call), so the ledger on screen can never disagree with the
        // books - the View computes none of it.

        /// <summary>The minimum payment a stage starting with this debt owes (rounded up).</summary>
        public long MinimumPaymentFor(long debt)
        {
            return debt > 0 ? (debt * Config.Market.CreditMinimumPaymentPercent + 99) / 100 : 0;
        }

        /// <summary>The interest this debt would draw at a stage's end (rounded up).</summary>
        public long InterestFor(long debt)
        {
            return debt > 0 ? (debt * Config.Market.CreditInterestPermille + 999) / 1000 : 0;
        }

        /// <summary>What the NEXT stage will owe as its minimum, as the debt stands now.</summary>
        public long NextStageMinimumPayment
        {
            get { return MinimumPaymentFor(Debt); }
        }

        /// <summary>The interest the debt would draw if the stage ended now.</summary>
        public long NextInterest
        {
            get { return InterestFor(Debt); }
        }

        /// <summary>How much of THIS stage's minimum payment has been paid so far.</summary>
        public long MinimumPaidThisStage
        {
            get
            {
                return CurrentRound != null
                    ? Math.Min(DebtRepaidThisStage, CurrentRound.CreditInstallment)
                    : 0;
            }
        }

        /// <summary>True once this stage has paid its minimum.</summary>
        public bool MinimumSatisfied
        {
            get
            {
                return CurrentRound != null && CurrentRound.CreditInstallment > 0
                    && DebtRepaidThisStage >= CurrentRound.CreditInstallment;
            }
        }

        /// <summary>The whole term, in stages.</summary>
        public int CreditTermStages
        {
            get { return Config.Market.CreditTermStages; }
        }

        /// <summary>The interest rate, in tenths of a percent.</summary>
        public int CreditInterestPermille
        {
            get { return Config.Market.CreditInterestPermille; }
        }

        /// <summary>How close the term is to running out - the one definition of "safe" through
        /// "final due" the ledger's tension follows. Safe is the first stage a loan is carried
        /// into; Warning is two stages left; FinalDue is the last.</summary>
        public CreditDeadline CreditDeadline
        {
            get
            {
                if (Debt <= 0)
                {
                    return CreditDeadline.None;
                }
                int left = CreditTermLeft;
                if (left <= 1)
                {
                    return CreditDeadline.FinalDue;
                }
                if (left == 2)
                {
                    return CreditDeadline.Warning;
                }
                return CreditStagesCarried == 0 ? CreditDeadline.Safe : CreditDeadline.Pressure;
            }
        }

        /// <summary>Pays a market price: the purse first, the rest borrowed. Only ever called
        /// after CanAfford said yes.</summary>
        private void Spend(long price)
        {
            if (TotalScore >= price)
            {
                TotalScore -= price;
                return;
            }
            if (Debt <= 0)
            {
                // A NEW loan opens a new term. Borrowing more on an open one does not - otherwise
                // the term could be rolled forward forever one purchase at a time.
                CreditStagesCarried = 0;
            }
            Debt += price - TotalScore;
            TotalScore = 0;
        }

        /// <summary>
        /// THE ANIMATION LAB'S SEAM - only ever called on a scratch session the lab builds for
        /// itself, never on a real run. Puts <paramref name="extraCards"/> into its deck, borrows
        /// <paramref name="debt"/> on an empty purse, and forecloses AT ONCE exactly as the end of
        /// a term does - including writing off what nothing could cover, as any stage but the final
        /// one would. Returns Core's own statement, so the order, every value and every half price
        /// the lab shows are the rules' answers rather than a drawing of them.
        /// </summary>
        public CreditStatement ForecloseForLab(long debt, IEnumerable<BlockCard> extraCards)
        {
            if (extraCards != null)
            {
                ownedCards.AddRange(extraCards);
            }
            TotalScore = 0;
            Spend(Math.Max(1L, debt));
            var statement = new CreditStatement
            {
                RoundNumber = RoundNumber,
                BossStage = InBossStage,
                DebtAtStart = Debt,
                InterestPermille = Config.Market.CreditInterestPermille,
                TermStages = Config.Market.CreditTermStages,
                CarriedBefore = Config.Market.CreditTermStages - 1,
                DebtBeforeForeclosure = Debt,
                DeckCountBeforeForeclosure = ownedCards.Count
            };
            Foreclose(statement);
            if (Debt > 0)
            {
                statement.WrittenOff = Debt;
                Debt = 0;
            }
            statement.DebtAfter = Debt;
            return statement;
        }

        /// <summary>TEST SEAM: spends the whole purse and borrows <paramref name="amount"/> on top
        /// of it - exactly what buying something that dear would do, without a shelf to buy it on.</summary>
        internal void BorrowForTest(long amount)
        {
            Spend(TotalScore + amount);
        }

        /// <summary>Money coming IN - a turn, a sale, a grant. It pays the debt first and only
        /// what is left reaches the purse. With nothing owed it is exactly TotalScore += amount.</summary>
        private void Receive(long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            if (Debt > 0)
            {
                long paid = Math.Min(Debt, amount);
                Debt -= paid;
                DebtRepaidThisStage += paid;
                amount -= paid;
            }
            TotalScore += amount;
        }

        /// <summary>Money an effect takes BACK (the overtime cap clawing banked score). Out of the
        /// purse first; whatever the purse cannot cover had gone to paying the debt, so it goes
        /// back onto the debt. Anything beyond that is the old behaviour, unchanged.</summary>
        private void TakeBack(long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            long fromPurse = Math.Min(Math.Max(TotalScore, 0), amount);
            TotalScore -= fromPurse;
            long rest = amount - fromPurse;
            if (rest > 0 && DebtRepaidThisStage > 0)
            {
                long back = Math.Min(rest, DebtRepaidThisStage);
                Debt += back;
                DebtRepaidThisStage -= back;
                rest -= back;
            }
            TotalScore -= rest;
        }

        /// <summary>
        /// A ROUND'S EARNINGS, split by where they land on the round's meter (designer's call,
        /// 2026-09-30): whatever lands under the round's own threshold is the PLAYER'S and goes to
        /// the purse, debt or no debt - that is what the round is for. What lands above it - the
        /// minimum payment between the threshold and the bar, and everything overtime earns past
        /// the bar - goes through Receive, so it pays the debt first. With nothing owed both halves
        /// reach the purse and this is exactly TotalScore += amount.
        /// </summary>
        /// <param name="roundScoreAfter">The round's meter once this amount is on it.</param>
        internal void BankRoundScore(long amount, long roundScoreAfter)
        {
            if (amount < 0)
            {
                TakeBack(-amount);
                return;
            }
            if (amount == 0)
            {
                return;
            }
            long own = amount;
            if (CurrentRound != null)
            {
                long threshold = (long)CurrentRound.ScoreThreshold * Config.Scoring.ScoreScale;
                long before = roundScoreAfter - amount;
                own = Math.Min(roundScoreAfter, threshold) - Math.Max(before, 0L);
                own = Math.Max(0L, Math.Min(amount, own));
            }
            TotalScore += own;
            Receive(amount - own);
        }

        /// <summary>
        /// Round score taken back FROM THE TOP of the meter - the excess over the bar on the turn
        /// that crosses it between turns, an overtime excess pulled back to the threshold. The top
        /// is where the debt's share lands, so it goes back onto the debt first and only then
        /// comes out of the purse. Booked as taken by an effect, like any negative AddCurrency.
        /// </summary>
        internal void UnbankRoundScoreFromTop(long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            long back = Math.Min(amount, DebtRepaidThisStage);
            if (back > 0)
            {
                Debt += back;
                DebtRepaidThisStage -= back;
            }
            TotalScore -= amount - back;
            CurrencyTakenByEffects += amount;
        }

        // ------------------------------------------------------------------- the stage

        /// <summary>At the start of every stage: remember what it started owing, and put the
        /// minimum payment on its bar. Called once the round engine and its boss exist.</summary>
        private void BeginCreditStage()
        {
            DebtAtStageStart = Debt;
            DebtRepaidThisStage = 0;
            if (Debt <= 0)
            {
                CreditStagesCarried = 0;
                return;
            }
            long installment = MinimumPaymentFor(Debt);
            CurrentRound.SetCreditInstallment((int)Math.Min(installment, int.MaxValue / 4));
        }

        /// <summary>
        /// At the end of every stage that was passed: interest on what is still owed, the term,
        /// the bailiff, the bank's thanks. Returns true when the run is lost to the debt - the
        /// final stage, with more owed than there was anything left to take.
        /// </summary>
        private bool SettleCreditAtStageEnd()
        {
            if (DebtAtStageStart <= 0 && Debt <= 0)
            {
                CreditStagesCarried = 0;
                return false;
            }
            var statement = new CreditStatement
            {
                RoundNumber = RoundNumber,
                BossStage = InBossStage,
                DebtAtStart = DebtAtStageStart,
                Installment = CurrentRound != null ? CurrentRound.CreditInstallment : 0,
                Repaid = DebtRepaidThisStage,
                InterestPermille = Config.Market.CreditInterestPermille,
                TermStages = Config.Market.CreditTermStages,
                CarriedBefore = CreditStagesCarried
            };
            bool lost = false;
            if (Debt <= 0)
            {
                // Paid off. In the FIRST stage it was carried into - no interest ever charged on
                // it - and the bank says thank you.
                if (DebtAtStageStart > 0 && CreditStagesCarried == 0)
                {
                    GrantBankReward(statement);
                }
                CreditStagesCarried = 0;
            }
            else
            {
                long interest = InterestFor(Debt);
                statement.DebtBeforeInterest = Debt;
                Debt += interest;
                statement.Interest = interest;
                CreditStagesCarried++;
                if (CreditStagesCarried >= Config.Market.CreditTermStages || IsFinalRound)
                {
                    statement.DebtBeforeForeclosure = Debt;
                    statement.DeckCountBeforeForeclosure = ownedCards.Count;
                    Foreclose(statement);
                    if (Debt > 0)
                    {
                        if (IsFinalRound)
                        {
                            lost = true;
                        }
                        else
                        {
                            // Nothing left worth taking: the rest is written off. The player has
                            // already paid for it with everything they had.
                            statement.WrittenOff = Debt;
                            Debt = 0;
                        }
                    }
                    if (Debt <= 0)
                    {
                        CreditStagesCarried = 0;
                    }
                }
            }
            statement.DebtAfter = Debt;
            statement.TermLeft = CreditTermLeft;
            LastCreditStatement = statement;
            DebtAtStageStart = 0;
            DebtRepaidThisStage = 0;
            return lost;
        }

        // ------------------------------------------------------------------- the bailiff

        private struct Seizable
        {
            public SeizedKind Kind;
            public Joker Joker;
            public Power Power;
            public BlockCard Card;
            public long Value;
            public int Order;
        }

        /// <summary>
        /// FORECLOSURE ("haciz"): takes what the player holds, most valuable first, at a share of
        /// its shelf price, until the debt is covered - jokers, powers and elemental blocks at
        /// CreditSeizurePercent, then plain blocks at CreditPlainSeizurePercent. The deck never
        /// goes below the hand size. A seizure worth more than what is owed hands the rest back.
        /// Internal for the test suite.
        /// </summary>
        internal void Foreclose(CreditStatement statement)
        {
            MarketConfig market = Config.Market;
            long scale = Config.Scoring.ScoreScale;
            var valuables = new List<Seizable>();
            int order = 0;
            foreach (Joker joker in Jokers.Jokers)
            {
                valuables.Add(new Seizable
                {
                    Kind = SeizedKind.Joker,
                    Joker = joker,
                    Value = market.JokerBuyPrice(RarityTable.For(joker.DefId)) * scale,
                    Order = order++
                });
            }
            foreach (Power power in Powers.Powers)
            {
                valuables.Add(new Seizable
                {
                    Kind = SeizedKind.Power,
                    Power = power,
                    Value = market.PowerBuyPrice(RarityTable.For(power.DefId)) * scale,
                    Order = order++
                });
            }
            var plain = new List<Seizable>();
            foreach (BlockCard card in ownedCards)
            {
                var item = new Seizable
                {
                    Kind = card.Elements.Count > 0 ? SeizedKind.ElementalBlock : SeizedKind.PlainBlock,
                    Card = card,
                    Value = market.BuyPrice(card) * scale,
                    Order = order++
                };
                (item.Kind == SeizedKind.ElementalBlock ? valuables : plain).Add(item);
            }
            // Most valuable first; a tie goes in the order things were held, so the same books
            // always lose the same things.
            Comparison<Seizable> byValue = delegate (Seizable a, Seizable b)
            {
                return a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Order.CompareTo(b.Order);
            };
            valuables.Sort(byValue);
            plain.Sort(byValue);

            int floor = Config.Rules.HandSize;
            bool deckChanged = false;
            for (int i = 0; i < valuables.Count && Debt > 0; i++)
            {
                deckChanged |= Seize(valuables[i], market.CreditSeizurePercent, floor, statement);
            }
            for (int i = 0; i < plain.Count && Debt > 0; i++)
            {
                deckChanged |= Seize(plain[i], market.CreditPlainSeizurePercent, floor, statement);
            }
            if (deckChanged)
            {
                NoteDeckChanged();
            }
        }

        /// <summary>Takes one thing. Returns true when it was a card (the deck changed).</summary>
        private bool Seize(Seizable item, int percent, int deckFloor, CreditStatement statement)
        {
            string name;
            if (item.Joker != null)
            {
                if (!Jokers.Remove(item.Joker))
                {
                    return false;
                }
                name = item.Joker.DisplayName;
            }
            else if (item.Power != null)
            {
                if (!Powers.Remove(item.Power))
                {
                    return false;
                }
                name = item.Power.DisplayName;
            }
            else
            {
                if (ownedCards.Count <= deckFloor || !ownedCards.Remove(item.Card))
                {
                    return false;
                }
                Jokers.DispatchCardLost(null, item.Card);
                name = item.Card.ToString();
            }
            long credited = Math.Max(1L, item.Value * percent / 100);
            long debtBefore = Debt;
            Receive(credited);
            string defId = item.Joker != null ? item.Joker.DefId
                : item.Power != null ? item.Power.DefId : null;
            statement.Seized.Add(new SeizedItem
            {
                Kind = item.Kind,
                Name = name,
                DefId = defId,
                CardId = item.Card != null ? item.Card.Id : 0,
                Card = item.Card,
                JokerInstanceId = item.Joker != null ? item.Joker.InstanceId : 0,
                PowerInstanceId = item.Power != null ? item.Power.InstanceId : 0,
                Rarity = defId != null ? RarityTable.For(defId) : Rarity.Common,
                Value = item.Value,
                Credited = credited,
                DebtBefore = debtBefore,
                DebtAfter = Debt,
                DeckCountAfter = ownedCards.Count
            });
            return item.Card != null;
        }

        // ------------------------------------------------------------------- the bank's thanks

        /// <summary>The campaign waiting for the market this stage is about to open.</summary>
        private CreditStatement pendingCampaign;

        private void GrantBankReward(CreditStatement statement)
        {
            // The bank's own stream, seeded from the run and the stage: drawing from the main rng
            // would shift every shuffle after it for a run that happened to pay a debt on time.
            var bank = new SeededRandom(unchecked(resolvedSeed * 1103515245
                + RoundNumber * 7919 + (InBossStage ? 104729 : 0)));
            bool campaign = !IsFinalRound && bank.NextInt(0, 2) == 1;
            if (campaign)
            {
                statement.Reward = BankRewardKind.Campaign;
                statement.CampaignPercent = Config.Market.CreditCampaignDiscountPercent;
                pendingCampaign = statement;
                pendingCampaignPick = bank.NextInt(0, 1 << 20);
                return;
            }
            long bonus = (DebtAtStageStart * Config.Market.CreditOnTimeBonusPercent + 99) / 100;
            statement.Reward = BankRewardKind.Points;
            statement.RewardPoints = bonus;
            GrantCurrency(bonus);
        }

        private int pendingCampaignPick;

        /// <summary>Puts a waiting campaign on one offer of the market just stocked. A market with
        /// nothing unsold on it pays the reward as points instead - a campaign on nothing is no
        /// reward at all.</summary>
        private void ApplyPendingCampaign()
        {
            CreditStatement statement = pendingCampaign;
            pendingCampaign = null;
            if (statement == null)
            {
                return;
            }
            var open = new List<MarketOffer>();
            foreach (MarketOffer offer in Market.Offers)
            {
                if (!offer.Sold)
                {
                    open.Add(offer);
                }
            }
            if (open.Count == 0)
            {
                long bonus = (statement.DebtAtStart * Config.Market.CreditOnTimeBonusPercent + 99) / 100;
                statement.Reward = BankRewardKind.Points;
                statement.RewardPoints = bonus;
                GrantCurrency(bonus);
                return;
            }
            MarketOffer picked = open[pendingCampaignPick % open.Count];
            picked.CampaignPercent = statement.CampaignPercent;
            statement.CampaignOffer = picked;
        }
    }
}
