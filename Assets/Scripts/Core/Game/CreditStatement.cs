// PURPOSE: "Kredi kartı" - the statement of one stage's credit, written by the session at the end
// of every stage the run was in debt for (GameSession.Credit.cs) and read by the View. Reporting
// only: nothing in the rules reads it back, a NEW object is written per statement so the View
// matches it by identity, and it is never saved (it describes a moment that has already passed).

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>What the bank gave for a debt paid off on time.</summary>
    public enum BankRewardKind
    {
        None = 0,
        /// <summary>Points straight into the purse.</summary>
        Points = 1,
        /// <summary>A campaign: one offer in the next market is cheaper bought on the card.</summary>
        Campaign = 2
    }

    /// <summary>What the bailiff took, and what it was credited at.</summary>
    public enum SeizedKind
    {
        Joker = 0,
        Power = 1,
        ElementalBlock = 2,
        PlainBlock = 3
    }

    /// <summary>One thing taken in a foreclosure ("haciz").</summary>
    public sealed class SeizedItem
    {
        public SeizedKind Kind;

        /// <summary>A joker's or power's name, or the block's own description.</summary>
        public string Name;

        /// <summary>The joker/power def id, or null for a block.</summary>
        public string DefId;

        /// <summary>The block's card id, or 0 for a joker/power.</summary>
        public int CardId;

        /// <summary>The block itself, so the View can draw the card that went (it is already out
        /// of the deck). Null for a joker/power.</summary>
        public BlockCard Card;

        /// <summary>The instance that went, so the View can find where its panel stood. 0 when
        /// it was not a joker / not a power.</summary>
        public int JokerInstanceId;
        public int PowerInstanceId;

        /// <summary>A joker's or power's rarity - a rare one is seized with more weight.</summary>
        public Rarity Rarity;

        /// <summary>What it would cost on the shelf, in the scaled economy.</summary>
        public long Value;

        /// <summary>What the bailiff credited it at against the debt.</summary>
        public long Credited;

        /// <summary>The debt just before this seizure and just after it.</summary>
        public long DebtBefore;
        public long DebtAfter;

        /// <summary>How many cards the deck had left once this was taken.</summary>
        public int DeckCountAfter;
    }

    /// <summary>How close a loan's term is to running out (GameSession.CreditDeadline).</summary>
    public enum CreditDeadline
    {
        None = 0,
        /// <summary>The first stage the loan is carried into.</summary>
        Safe = 1,
        /// <summary>Carried a while; more than two stages still left.</summary>
        Pressure = 2,
        /// <summary>Two stages left.</summary>
        Warning = 3,
        /// <summary>The last stage: at its end, the bailiff.</summary>
        FinalDue = 4
    }

    /// <summary>One stage's credit, as it was settled at the stage's end.</summary>
    public sealed class CreditStatement
    {
        public int RoundNumber;
        public bool BossStage;

        /// <summary>The debt the stage started with.</summary>
        public long DebtAtStart;

        /// <summary>The minimum payment the stage owed on top of its bar (scaled).</summary>
        public long Installment;

        /// <summary>What the stage's earnings paid off.</summary>
        public long Repaid;

        /// <summary>Interest charged on what was left at the end, on what it was charged on, and
        /// at what rate (tenths of a percent).</summary>
        public long Interest;
        public long DebtBeforeInterest;
        public int InterestPermille;

        /// <summary>The term, and how many stages of it had been used before this one.</summary>
        public int TermStages;
        public int CarriedBefore;

        /// <summary>A foreclosure's starting point: the debt the bailiff came for, and the deck
        /// it started taking from.</summary>
        public long DebtBeforeForeclosure;
        public int DeckCountBeforeForeclosure;

        /// <summary>The debt carried into the market.</summary>
        public long DebtAfter;

        /// <summary>Stages left on the term, 0 when debt-free.</summary>
        public int TermLeft;

        public BankRewardKind Reward;

        /// <summary>The points the bank paid (Reward == Points).</summary>
        public long RewardPoints;

        /// <summary>The offer the campaign is on, and how much off (Reward == Campaign).</summary>
        public MarketOffer CampaignOffer;
        public int CampaignPercent;

        /// <summary>What the bailiff took, most valuable first, in the order it was taken.</summary>
        public readonly List<SeizedItem> Seized = new List<SeizedItem>();

        /// <summary>What was still owed with nothing left to take - written off.</summary>
        public long WrittenOff;

        public bool Foreclosed
        {
            get { return Seized.Count > 0 || WrittenOff > 0; }
        }
    }
}
