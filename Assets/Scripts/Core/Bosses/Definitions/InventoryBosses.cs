// PURPOSE: The four bosses that attack what the player OWNS rather than the board -
// "Tükenmişlik" stops powers refilling, "Anarşi" silences everything rare, "Oburluk" punishes
// a full inventory by switching one item off, and "Terslik" turns every joker against you.
//
// None of them touches the inventories: silencing and inversion are QUERIES the inventories ask
// every time (RoundEngine.IsSilencedByBoss / InvertsJokerScore), so nothing is removed, nothing
// is re-added, and no permanent effect (a joker's hand-size bonus, say) is ever undone and redone.

namespace ProjectBlock.Core
{
    /// <summary>"Tükenmişlik" - burnout: for the rest of the round no power ever refills. You
    /// start the round charged as usual, so every power is worth exactly one use.</summary>
    public sealed class TukenmislikBoss : BossRound
    {
        public TukenmislikBoss()
            : base("tukenmislik", "Tükenmişlik")
        {
            SetDescription(
                "Powers never refill for the rest of the round - not from a clean sweep, not "
                    + "from anything. Each one is good for a single use.",
                "Bu raunt boyunca güçler bir daha dolmaz - ne temizlikle, ne başka bir şeyle. "
                    + "Her güç tek kullanımlık.");
        }

        public override bool BlocksPowerRecharge
        {
            get { return true; }
        }
    }

    /// <summary>"Anarşi" - anything above common is switched off: every rare and legendary
    /// joker and power sits silent for the round, leaving the player with their basics.</summary>
    public sealed class AnarsiBoss : BossRound
    {
        public AnarsiBoss()
            : base("anarsi", "Anarşi")
        {
            SetDescription(
                "Rare and legendary jokers and powers are switched off for the round. Only your "
                    + "common ones still work.",
                "Nadir ve efsanevi jokerler ile güçler bu raunt boyunca devre dışı. Sadece "
                    + "sıradan olanlar çalışır.");
        }

        public override bool DisablesJoker(Joker joker)
        {
            return RarityTable.For(joker.DefId) != Rarity.Common;
        }

        public override bool DisablesPower(Power power)
        {
            return RarityTable.For(power.DefId) != Rarity.Common;
        }
    }

    /// <summary>"Oburluk" - gluttony punishes hoarding: if the joker inventory is FULL one
    /// random joker goes silent for the round, and the same happens to the powers. Keep a slot
    /// free and it has nothing to bite.</summary>
    public sealed class OburlukBoss : BossRound
    {
        private int silencedJoker;
        private int silencedPower;

        public OburlukBoss()
            : base("oburluk", "Oburluk")
        {
            SetDescription(
                "If your joker slots are full, one random joker is switched off for the round - "
                    + "and the same for your powers. A free slot keeps everything working.",
                "Joker yuvaların doluysa rastgele bir jokerin bu raunt boyunca devre dışı "
                    + "kalır - güçler için de aynısı geçerli. Boş yuva bırakırsan hiçbiri kapanmaz.");
        }

        /// <summary>Instance ids it ate, for the UI. 0 when that inventory was not full.</summary>
        public int SilencedJokerId
        {
            get { return silencedJoker; }
        }

        public int SilencedPowerId
        {
            get { return silencedPower; }
        }

        public override string StatusText
        {
            get
            {
                int eaten = (silencedJoker != 0 ? 1 : 0) + (silencedPower != 0 ? 1 : 0);
                return eaten > 0 ? eaten + Loc.Pick(" switched off", " kapatıldı") : null;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // Re-picked every time this boss comes up, so a run never suffers the same loss twice.
            silencedJoker = 0;
            silencedPower = 0;
            JokerInventory jokers = ctx.Session.Jokers;
            if (jokers.IsFull && jokers.Count > 0)
            {
                silencedJoker = jokers.Jokers[ctx.Rng.NextInt(0, jokers.Count)].InstanceId;
            }
            PowerInventory powers = ctx.Session.Powers;
            if (powers.IsFull && powers.Count > 0)
            {
                silencedPower = powers.Powers[ctx.Rng.NextInt(0, powers.Count)].InstanceId;
            }
        }

        public override bool DisablesJoker(Joker joker)
        {
            return silencedJoker != 0 && joker.InstanceId == silencedJoker;
        }

        public override bool DisablesPower(Power power)
        {
            return silencedPower != 0 && power.InstanceId == silencedPower;
        }
    }

    /// <summary>
    /// "Terslik" - your jokers turn on you. Everything a joker GIVES this round is taken
    /// instead: the points it would add are subtracted by the same amount, and a piggy bank
    /// leaks value rather than filling.
    ///
    /// WHAT IT DOES NOT DO. Only the giving is reversed, not the joker. A joker that hands out
    /// no points and no value has no opposite to turn into - "Insider" still reveals the top
    /// card, "Seri tetik" still holds the hand open, "Deprem" still saves you from a dead end.
    /// They keep working exactly as they do on any other round. That keeps the boss honest (it
    /// punishes what you built your score on) and keeps it working for every joker added later,
    /// with no per-joker opposite to invent.
    ///
    /// A turn can never pay less than nothing, so the worst this boss can do is make a turn
    /// worthless - your round score never goes backwards. And a drained piggy bank stops at
    /// zero; it cannot go into debt.
    ///
    /// The whole thing is one flag: the inversion window in JokerInventory does the work, so
    /// this class has nothing of its own to run.
    /// </summary>
    public sealed class TerslikBoss : BossRound
    {
        public TerslikBoss()
            : base("terslik", "Terslik")
        {
            SetDescription(
                "Your jokers work against you this round: the points they would give you are "
                    + "taken from you instead, and piggy banks leak value. Jokers that give "
                    + "neither still do their job. A turn can never pay less than nothing.",
                "Bu raunt jokerlerin sana karşı çalışır: kazandıracakları puanı kaybettirirler, "
                    + "kumbaralar da dolmak yerine boşalır. Puan ya da değer vermeyen jokerler "
                    + "normal çalışmaya devam eder. Bir tur hiçbir zaman eksiye düşürmez.");
        }

        public override bool InvertsJokerScore
        {
            get { return true; }
        }
    }
}
