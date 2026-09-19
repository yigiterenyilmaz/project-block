// PURPOSE: The joker that keeps the REST of your kit running - "Yer altı kaynakları" refuels
// spent powers until it runs itself dry. ("Şifacı", which healed spent JOKERS on the same
// pattern, was cut: only a handful of jokers have charges at all, so a rare joker spent most
// runs doing nothing.)
//
// It acts from AfterTurnScored, so it ticks with the turn rather than with anything the player
// does, and it goes through the inventory's own primitive (PowerInventory.Recharge) - which
// means a boss that forbids refills ("Tükenmişlik") stops it for free, without the joker
// knowing that boss exists.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// "Yer altı kaynakları" - a seam of fuel for your POWERS. Every few turns it refills the
    /// spent common powers, and on a slower clock the spent rare ones.
    ///
    /// It is a finite resource: every power it refills costs CAPACITY - one for a common, two
    /// for a rare - out of a seam that is never replenished, not even by a new round. When the
    /// seam runs out the joker goes quiet: it keeps its slot and does nothing.
    ///
    /// The compensation is the sale. A worked-out seam sells for exactly what you PAID for it,
    /// so the joker is a loan of fuel rather than a purchase - you get your money back and the
    /// slot with it. (A joker that was never bought has no price to refund and sells normally.)
    ///
    /// Legendary powers are outside the seam entirely: it fuels the everyday kit.
    /// </summary>
    public sealed class YerAltiKaynaklariJoker : Joker
    {
        /// <summary>Total refills the seam is worth, in capacity points.</summary>
        public int Capacity = 10;

        public int CommonEveryTurns = 3;
        public int RareEveryTurns = 5;

        /// <summary>Capacity spent per power refilled, by rarity.</summary>
        public int CommonCost = 1;
        public int RareCost = 2;

        private int capacityLeft = -1; // -1 = not started; set on the first round
        private int commonTimer;
        private int rareTimer;

        /// <summary>
        /// The last delivery of fuel, for the VIEW to draw. Reporting only, and [NotSaved]: it is
        /// what just happened rather than state, it is rebuilt by the next delivery, and it is
        /// meaningless across a load - a restored run has no animation waiting to play.
        ///
        /// A turn due for BOTH clocks overwrites this with the second tier's delivery, which is
        /// correct: the View is asked on the repaint that follows the turn, and both tiers landing
        /// at once is one event to the player. The common tier's own powers are already charged by
        /// then, so nothing is lost but a stagger.
        /// </summary>
        [NotSaved]
        public SeamRefuelVisuals LastRefuel;

        /// <summary>
        /// It keeps statistics, so the tooltip prints its count from zero. What that count means
        /// here is POWERS REFUELLED, not ticks: the effect lands once per power (that is where the
        /// seam is actually spent), and counting it that way makes the number directly comparable
        /// with the capacity the card is already showing.
        /// </summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public YerAltiKaynaklariJoker()
            : base("yer_alti_kaynaklari", "Yer Altı Kaynakları")
        {
            SetDescription(
                "Every 3 turns it refills your spent common powers, every 5 turns your rare "
                    + "ones. Each refill costs the seam: 1 for a common power, 2 for a rare. "
                    + "When the seam is worked out the joker does nothing - but it sells for "
                    + "exactly what you paid for it.",
                "3 turda bir boşalmış sıradan güçlerini, 5 turda bir nadir güçlerini doldurur. "
                    + "Her doldurma damardan düşer: sıradan güç 1, nadir güç 2. Damar tükenince "
                    + "joker hiçbir şey yapmaz - ama ona verdiğin parayla satarsın.");
        }

        /// <summary>Capacity still in the seam.</summary>
        public int CapacityLeft
        {
            get { return capacityLeft < 0 ? Capacity : capacityLeft; }
        }

        /// <summary>True once the seam is worked out and the joker does nothing.</summary>
        public bool IsExhausted
        {
            get { return CapacityLeft <= 0; }
        }

        public override string StatusText
        {
            get
            {
                return IsExhausted
                    ? Loc.Pick("worked out", "tükendi")
                    : CapacityLeft + "/" + Capacity;
            }
        }

        /// <summary>
        /// A worked-out seam refunds EXACTLY the purchase price instead of the market's formula -
        /// the joker was a loan of fuel, not a purchase. A joker that was never bought (a starting
        /// one, or a debug grant) has no price to refund and is sold normally.
        ///
        /// PurchasePrice is in the SCALED economy while this hook works in market units, so it is
        /// brought back down; the caller scales the answer again.
        /// </summary>
        public override int OverrideSellValue(int marketValue)
        {
            if (!IsExhausted || PurchasePrice <= 0)
            {
                return marketValue;
            }
            int scale = ScoreScaleForRefund;
            return scale > 1 ? (int)(PurchasePrice / scale) : (int)PurchasePrice;
        }

        /// <summary>The economy scale the purchase price was recorded in. A joker has no session
        /// to ask, so the one number it needs is stamped on it when it is bought.</summary>
        internal int ScoreScaleForRefund { get; set; } = 1;

        public override void OnRoundStarted(RoundContext ctx)
        {
            if (capacityLeft < 0)
            {
                capacityLeft = Capacity;
            }
            // The seam itself is a RUN resource and never refills - only the clocks restart,
            // because turn numbers do.
            commonTimer = 0;
            rareTimer = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (capacityLeft < 0)
            {
                capacityLeft = Capacity;
            }
            if (capacityLeft <= 0)
            {
                return; // worked out: no clocks, no effect
            }
            // The two clocks run INDEPENDENTLY, so a turn that is due for both pays for both.
            commonTimer++;
            if (commonTimer >= CommonEveryTurns)
            {
                commonTimer = 0;
                RefillTier(turn, Rarity.Common, CommonCost);
            }
            rareTimer++;
            if (rareTimer >= RareEveryTurns)
            {
                rareTimer = 0;
                RefillTier(turn, Rarity.Rare, RareCost);
            }
        }

        /// <summary>Refills every SPENT power of one rarity, in inventory order, paying for each
        /// out of the seam and stopping the moment it cannot afford the next one. A power that is
        /// already charged costs nothing - the seam is only spent on fuel actually delivered.</summary>
        private void RefillTier(TurnContext turn, Rarity rarity, int cost)
        {
            if (cost <= 0)
            {
                return;
            }
            PowerInventory powers = turn.Session.Powers;
            IReadOnlyList<Power> all = powers.Powers;
            SeamRefuelVisuals delivered = null;
            for (int i = 0; i < all.Count && capacityLeft >= cost; i++)
            {
                Power power = all[i];
                if (power.Charged || RarityTable.For(power.DefId) != rarity)
                {
                    continue;
                }
                // Through the inventory, so a boss that forbids refills ("Tükenmişlik") stops
                // this without costing the seam a thing.
                if (powers.Recharge(power.InstanceId))
                {
                    capacityLeft -= cost;
                    // ONE FIRING PER POWER REFILLED. That is where the effect actually lands and
                    // where the seam is actually spent - a tick that was due but found every
                    // power charged has not fired at all, and one that fuelled three powers has
                    // fired three times. Worth 0 points: this joker's effect is not score.
                    NoteProc(0, turn);
                    // Written HERE rather than before the loop, so a tick that delivers nothing
                    // leaves no report for the View to draw.
                    if (delivered == null)
                    {
                        delivered = new SeamRefuelVisuals();
                        delivered.Tier = rarity;
                        delivered.Capacity = Capacity;
                    }
                    delivered.PowerInstanceIds.Add(power.InstanceId);
                    delivered.CapacitySpent += cost;
                }
            }
            if (delivered != null)
            {
                delivered.CapacityLeft = capacityLeft;
                LastRefuel = delivered;
            }
        }
    }
}
