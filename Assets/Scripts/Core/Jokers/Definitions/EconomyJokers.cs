// PURPOSE: The jokers wired into the run economy and the market: Damlaya Damlaya Göl Olur,
// ihale, Kara delik, Enfeksiyon.
//
// "Powerbank" lives here too, now that powers exist: it is the only joker that reaches into
// the power inventory.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Damlaya Damlaya Göl Olur" - skip the market and the next round pays for it.
    /// The bonus lands on every turn of that round, so it counts toward the threshold and
    /// cannot hand the player a free advance offer before a single block is placed.</summary>
    public sealed class DamlayaJoker : Joker
    {
        public int PointsPerTurnWhenSaving = 8;

        /// <summary>Consecutive markets left without buying. The bonus scales with it.</summary>
        public int SavedStreak { get; private set; }

        /// <summary>Bonus active during the current round (frozen at round start).</summary>
        public int ActiveBonus { get; private set; }

        public DamlayaJoker()
            : base("damlaya", "Damlaya Damlaya Göl Olur")
        {
            Description = "Marketten bir şey almazsan sonraki raunt her tur puan bonusu alırsın.";
            BaseSellValue = 45;
        }

        public override string StatusText
        {
            get { return ActiveBonus > 0 ? "+" + ActiveBonus + "/tur" : "biriktir " + SavedStreak; }
        }

        public override void OnMarketLeft(SessionContext ctx, bool anythingPurchased)
        {
            SavedStreak = anythingPurchased ? 0 : SavedStreak + 1;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            ActiveBonus = SavedStreak * PointsPerTurnWhenSaving;
        }

        public override void ModifyScore(TurnContext turn)
        {
            if (ActiveBonus > 0)
            {
                turn.Score.AddFlat(ActiveBonus, DefId);
            }
        }
    }

    /// <summary>"Powerbank" - once per round, puts one spent power back on charge without
    /// needing a clean sweep. Player-activated so the timing is a real decision.
    /// It refuses to fire when every power is already charged, so the charge is not wasted.</summary>
    public sealed class PowerbankJoker : Joker
    {
        public PowerbankJoker()
            : base("powerbank", "Powerbank")
        {
            Description = "Raunt başına 1 kez, harcanmış bir gücü temizlik beklemeden doldurur.";
            ChargesPerRound = 1;
            BaseSellValue = 55;
        }

        public override string StatusText
        {
            get { return ChargesLeft > 0 ? "hazır" : "kullanıldı"; }
        }

        public override bool CanActivate(RoundContext ctx)
        {
            if (ChargesLeft <= 0 || ctx.Round.Status != RoundStatus.InProgress)
            {
                return false;
            }
            IReadOnlyList<Power> powers = ctx.Session.Powers.Powers;
            for (int i = 0; i < powers.Count; i++)
            {
                if (!powers[i].Charged)
                {
                    return true;
                }
            }
            return false;
        }

        public override bool Activate(RoundContext ctx, ActivationTarget target)
        {
            if (!CanActivate(ctx) || !TrySpendCharge())
            {
                return false;
            }
            return ctx.Session.Powers.RechargeOne();
        }
    }

    /// <summary>"ihale" - every round it puts an extra price on one random joker (itself
    /// included). No new auction opens until that one is sold, so the player is nudged to
    /// actually let go of something.</summary>
    public sealed class IhaleJoker : Joker
    {
        public int BasePremium = 40;
        public int PremiumPerRound = 15;

        public IhaleJoker()
            : base("ihale", "İhale")
        {
            Description = "Her raunt başında rastgele bir jokere ek satış fiyatı biçer. "
                + "O joker satılana kadar yeni ihale açılmaz.";
            BaseSellValue = 40;
        }

        public override string StatusText
        {
            get { return auctionedName ?? "ihale yok"; }
        }

        private string auctionedName;

        public override void OnRoundStarted(RoundContext ctx)
        {
            JokerInventory inventory = ctx.Session.Jokers;
            if (inventory.ActiveAuctionInstanceId.HasValue)
            {
                return; // an auction is still open - see the file header
            }
            IReadOnlyList<Joker> all = inventory.Jokers;
            if (all.Count == 0)
            {
                return;
            }
            Joker target = all[ctx.Rng.NextInt(0, all.Count)];
            int premium = BasePremium + PremiumPerRound * (ctx.Round.Config.RoundNumber - 1);
            inventory.SetAuctionPremium(target, premium);
            auctionedName = target.DisplayName;
        }

        /// <summary>The auctioned joker left the inventory (sold or destroyed): the lock
        /// opens again. The premium the buyer already paid is theirs to keep.</summary>
        public override void OnJokerRemoved(SessionContext ctx, Joker other)
        {
            if (!ctx.Session.Jokers.ActiveAuctionInstanceId.HasValue)
            {
                auctionedName = null;
            }
        }
    }

    /// <summary>"Kara delik" - every clean sweep hands the player a 1x1 void block. A void
    /// block can be dropped onto an occupied cell; the cube that lands on it is swallowed
    /// and the void is used up. The cards are round-scoped and never join the owned deck.</summary>
    public sealed class KaraDelikJoker : Joker
    {
        /// <summary>How many void blocks may exist at the same time within one round.</summary>
        public int MaxLiveVoidBlocks = 2;

        /// <summary>Void blocks handed out this round.</summary>
        public int GrantedThisRound { get; private set; }

        private readonly List<int> liveVoidCardIds = new List<int>();

        public KaraDelikJoker()
            : base("kara_delik", "Kara Delik")
        {
            Description = "Her temizlikte ıskartana 1x1 boşluk bloğu ekler. Boşluk bloğu "
                + "dolu hücreye konabilir ve üstüne geleni yutar.";
            BaseSellValue = 70;
        }

        public override string StatusText
        {
            get { return GrantedThisRound + " boşluk"; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            GrantedThisRound = 0;
            liveVoidCardIds.Clear();
        }

        public override void AfterCleanSweep(TurnContext turn)
        {
            PruneSpent(turn.Round);
            if (liveVoidCardIds.Count >= MaxLiveVoidBlocks)
            {
                return;
            }
            BlockCard card = MakeVoidCard(turn.Session);
            liveVoidCardIds.Add(card.Id);
            GrantedThisRound++;
            // Into the discard, so it joins the pile economy and can be drawn later. In
            // overtime the sweep reshuffles the discard right after this, which is the
            // deliberate reward: the void block goes straight into the fresh draw pile.
            turn.Round.Deck.Discard(card);
        }

        /// <summary>Forgets void blocks that are no longer anywhere in the round's piles or
        /// hand, so the cap counts blocks that still exist rather than blocks ever made.</summary>
        private void PruneSpent(RoundEngine round)
        {
            for (int i = liveVoidCardIds.Count - 1; i >= 0; i--)
            {
                if (!IsStillAround(round, liveVoidCardIds[i]))
                {
                    liveVoidCardIds.RemoveAt(i);
                }
            }
        }

        private static bool IsStillAround(RoundEngine round, int cardId)
        {
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == cardId)
                {
                    return true;
                }
            }
            foreach (BlockCard card in round.Deck.DrawPile)
            {
                if (card.Id == cardId)
                {
                    return true;
                }
            }
            foreach (BlockCard card in round.Deck.DiscardPile)
            {
                if (card.Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }

        private static BlockCard MakeVoidCard(GameSession session)
        {
            BlockShape single = BlockShape.FromCells(new[] { new GridPos(0, 0) });
            return session.CreateCard(single, new[] { BlockElement.Void });
        }
    }

    /// <summary>"Enfeksiyon" - the player infects one cube; the infection creeps outward every
    /// turn and detonates a cube once it has been infected long enough. Activated with a
    /// board target, once per round.</summary>
    public sealed class EnfeksiyonJoker : Joker
    {
        /// <summary>Turns an infected cube survives before it blows.</summary>
        public int TurnsToDetonate = 3;

        /// <summary>Points per cube the infection takes.</summary>
        public int PointsPerInfectedCube = 6;

        /// <summary>Cell -> turns infected so far.</summary>
        private readonly Dictionary<GridPos, int> infected = new Dictionary<GridPos, int>();

        public EnfeksiyonJoker()
            : base("enfeksiyon", "Enfeksiyon")
        {
            Description = "Seçtiğin küpte enfeksiyon başlatır. Her tur çevresine yayılır ve "
                + "yeterince beklemiş küpleri patlatır.";
            ChargesPerRound = 1;
            BaseSellValue = 60;
        }

        /// <summary>The player points at the cube to infect.</summary>
        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.BoardCell; }
        }

        public override string StatusText
        {
            get { return infected.Count > 0 ? infected.Count + " enfekte" : "hazır"; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            infected.Clear();
        }

        public override bool CanActivate(RoundContext ctx)
        {
            return ChargesLeft > 0 && ctx.Round.Status == RoundStatus.InProgress;
        }

        public override bool Activate(RoundContext ctx, ActivationTarget target)
        {
            if (!CanActivate(ctx) || !target.Cell.HasValue)
            {
                return false;
            }
            GridPos cell = target.Cell.Value;
            if (!ctx.Round.Board.GetCube(cell).HasValue || !TrySpendCharge())
            {
                return false;
            }
            infected[cell] = 0;
            return true;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (infected.Count == 0)
            {
                return;
            }
            GameBoard board = turn.Round.Board;

            // Drop cells whose cube is already gone, and age the rest.
            var stale = new List<GridPos>();
            var ripe = new List<GridPos>();
            var keys = new List<GridPos>(infected.Keys);
            foreach (GridPos cell in keys)
            {
                if (!board.GetCube(cell).HasValue)
                {
                    stale.Add(cell);
                    continue;
                }
                int age = infected[cell] + 1;
                infected[cell] = age;
                if (age >= TurnsToDetonate)
                {
                    ripe.Add(cell);
                }
            }
            foreach (GridPos cell in stale)
            {
                infected.Remove(cell);
            }

            // Spread one ring from the cells still standing, before the ripe ones blow.
            var newlyInfected = new List<GridPos>();
            foreach (GridPos cell in new List<GridPos>(infected.Keys))
            {
                foreach (GridPos neighbour in board.Neighbours(cell))
                {
                    if (!infected.ContainsKey(neighbour) && board.GetCube(neighbour).HasValue
                        && !newlyInfected.Contains(neighbour))
                    {
                        newlyInfected.Add(neighbour);
                    }
                }
            }

            if (ripe.Count > 0)
            {
                IReadOnlyList<GridPos> blown = turn.Round.DestroyCubes(ripe, true);
                foreach (GridPos cell in ripe)
                {
                    infected.Remove(cell);
                }
                if (blown.Count > 0)
                {
                    turn.AddFlatScore(blown.Count * PointsPerInfectedCube, DefId);
                    turn.Round.TryResolveCleanSweep();
                }
            }
            foreach (GridPos cell in newlyInfected)
            {
                infected[cell] = 0;
            }
        }
    }
}
