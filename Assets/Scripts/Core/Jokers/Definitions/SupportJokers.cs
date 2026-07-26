// PURPOSE: The two jokers that keep the REST of your kit running - "Şifacı" heals spent
// jokers, "Yer altı kaynakları" refuels spent powers until it runs itself dry.
//
// Both act from AfterTurnScored, so they tick with the turn rather than with anything the
// player does, and both go through the inventories' own primitives (Joker.GrantCharge,
// PowerInventory.Recharge) - which means a boss that forbids refills ("Tükenmişlik") stops
// them for free, without either joker knowing that boss exists.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// "Şifacı" - every few turns it gives one use back to a random SPENT joker.
    ///
    /// The clock does not run down while there is nothing to heal: if no joker is empty when it
    /// comes due, it stays ready and heals the moment one empties, then goes back to sleep. So
    /// the wait is never wasted - it is a promise, not a window you can miss.
    /// </summary>
    public sealed class SifaciJoker : Joker
    {
        /// <summary>Turns between heals.</summary>
        public int TurnsBetweenHeals = 5;

        /// <summary>Turns counted since the last heal. Stops counting once it is due.</summary>
        private int turnsWaited;

        /// <summary>Heals given this round, for the UI.</summary>
        private int healsGiven;

        public SifaciJoker()
            : base("sifaci", "Şifacı")
        {
            SetDescription(
                "Every 5 turns it gives one use back to a random spent joker. If nothing is "
                    + "spent it stays ready and heals the moment something is.",
                "Her 5 turda bir, hakkı bitmiş rastgele bir jokerine bir hak geri verir. "
                    + "Bitmiş joker yoksa hazır bekler ve biri biter bitmez iyileştirir.");
        }

        /// <summary>True when the clock is up and it is only waiting for something to heal.</summary>
        public bool IsReadyToHeal
        {
            get { return turnsWaited >= TurnsBetweenHeals; }
        }

        public override string StatusText
        {
            get
            {
                if (IsReadyToHeal)
                {
                    return Loc.Pick("ready", "hazır");
                }
                int left = TurnsBetweenHeals - turnsWaited;
                return left + Loc.Pick("t to heal", "t sonra");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // The clock restarts with the round, but a heal owed is not carried over: every
            // joker comes back fully charged at round start anyway, so there is nothing owed.
            turnsWaited = 0;
            healsGiven = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (turnsWaited < TurnsBetweenHeals)
            {
                turnsWaited++;
            }
            if (turnsWaited < TurnsBetweenHeals)
            {
                return;
            }
            // Due. Heal a spent joker if there is one; otherwise stay ready and try again next
            // turn - the clock does NOT restart on an empty search.
            IReadOnlyList<Joker> jokers = turn.Session.Jokers.Jokers;
            var spent = new List<Joker>();
            for (int i = 0; i < jokers.Count; i++)
            {
                Joker other = jokers[i];
                if (other != this && other.ChargesPerRound > 0 && other.ChargesLeft <= 0)
                {
                    spent.Add(other);
                }
            }
            if (spent.Count == 0)
            {
                return;
            }
            Joker patient = spent[turn.Rng.NextInt(0, spent.Count)];
            if (patient.GrantCharge())
            {
                healsGiven++;
                turnsWaited = 0;
            }
        }
    }

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
                }
            }
        }
    }
}
