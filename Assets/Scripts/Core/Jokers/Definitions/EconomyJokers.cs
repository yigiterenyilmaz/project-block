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
            SetDescription(
                "Buy nothing at the market and the next round pays a score bonus every turn.",
                "Marketten bir şey almazsan sonraki raunt her tur puan bonusu alırsın.");
            BaseSellValue = 45;
        }

        public override string StatusText
        {
            get
            {
                return ActiveBonus > 0
                    ? Loc.Pick("+" + ActiveBonus + "/turn", "+" + ActiveBonus + "/tur")
                    : Loc.Pick("saving " + SavedStreak, "biriktir " + SavedStreak);
            }
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
            SetDescription(
                "Once per round, recharges a spent power without waiting for a clean sweep.",
                "Raunt başına 1 kez, harcanmış bir gücü temizlik beklemeden doldurur.");
            ChargesPerRound = 1;
            BaseSellValue = 55;
        }

        public override string StatusText
        {
            get { return ChargesLeft > 0 ? Loc.Pick("ready", "hazır") : Loc.Pick("used", "kullanıldı"); }
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
            SetDescription(
                "At every round start it puts a premium on a random joker's sell price. "
                    + "No new auction opens until that joker is sold.",
                "Her raunt başında rastgele bir jokere ek satış fiyatı biçer. "
                    + "O joker satılana kadar yeni ihale açılmaz.");
            BaseSellValue = 40;
        }

        public override string StatusText
        {
            get { return auctionedName ?? Loc.Pick("no auction", "ihale yok"); }
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
            SetDescription(
                "Every clean sweep adds a 1x1 void block to your discard. A void block "
                    + "can be placed on a filled cell and swallows whatever lands on it.",
                "Her temizlikte ıskartana 1x1 boşluk bloğu ekler. Boşluk bloğu "
                    + "dolu hücreye konabilir ve üstüne geleni yutar.");
            BaseSellValue = 70;
        }

        public override string StatusText
        {
            get { return Loc.Pick(GrantedThisRound + " voids", GrantedThisRound + " boşluk"); }
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

    /// <summary>One infected cell exposed for the UI: where it is and how far its buildup
    /// has progressed toward detonation.</summary>
    public readonly struct InfectedCell
    {
        public readonly GridPos Cell;
        public readonly int Turns;
        public readonly int Threshold;

        public InfectedCell(GridPos cell, int turns, int threshold)
        {
            Cell = cell;
            Turns = turns;
            Threshold = threshold;
        }
    }

    /// <summary>
    /// "Enfeksiyon" - the player infects ONE cell. It does not spread on its own; it watches
    /// whatever block sits on it, and once that same block has held the cell for 3 turns the
    /// whole block detonates. Only the FIRST detonation spreads the infection, once, into a
    /// 3x3 plus around the cell - that is the most it ever grows. Activated once per round.
    /// </summary>
    public sealed class EnfeksiyonJoker : Joker
    {
        /// <summary>Turns the SAME block must hold an infected cell before it detonates.</summary>
        public int TurnsToDetonate = 3;

        /// <summary>Points per cube the detonation takes.</summary>
        public int PointsPerInfectedCube = 6;

        private sealed class Infection
        {
            public int CardId = -1; // block currently building up on this cell, or -1
            public int Turns;
        }

        private readonly Dictionary<GridPos, Infection> infected = new Dictionary<GridPos, Infection>();
        private readonly List<InfectedCell> markerCache = new List<InfectedCell>();
        private bool hasSpread;

        public EnfeksiyonJoker()
            : base("enfeksiyon", "Enfeksiyon")
        {
            SetDescription(
                "Infect one cube. When the same block has sat on it for 3 turns the whole "
                    + "block detonates. The first detonation spreads the infection once into "
                    + "a 3x3 plus - and no further.",
                "Bir küpü enfekte edersin. Aynı blok o karede 3 tur durursa blok tümüyle "
                    + "patlar. İlk patlama enfeksiyonu bir kez 3x3 artı şeklinde yayar - "
                    + "daha fazla değil.");
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
            get
            {
                return infected.Count > 0
                    ? Loc.Pick(infected.Count + " infected", infected.Count + " enfekte")
                    : Loc.Pick("ready", "hazır");
            }
        }

        /// <summary>Current infection markers, for the board view (buildup visualisation).</summary>
        public IReadOnlyList<InfectedCell> InfectedCells
        {
            get
            {
                markerCache.Clear();
                foreach (KeyValuePair<GridPos, Infection> entry in infected)
                {
                    markerCache.Add(new InfectedCell(entry.Key, entry.Value.Turns, TurnsToDetonate));
                }
                return markerCache;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            infected.Clear();
            hasSpread = false;
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
            Cube? cube = ctx.Round.Board.GetCube(cell);
            if (!cube.HasValue || !TrySpendCharge())
            {
                return false;
            }
            infected[cell] = new Infection { CardId = cube.Value.SourceCardId, Turns = 0 };
            return true;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (infected.Count == 0)
            {
                return;
            }
            GameBoard board = turn.Round.Board;

            // Age each cell against the block sitting on it: same block -> tick up; a different
            // block (or an empty cell) restarts the count. Collect the cells that ripened.
            var ripe = new List<GridPos>();
            foreach (GridPos cell in new List<GridPos>(infected.Keys))
            {
                Infection inf = infected[cell];
                Cube? cube = board.GetCube(cell);
                if (!cube.HasValue)
                {
                    inf.CardId = -1;
                    inf.Turns = 0;
                    continue;
                }
                if (cube.Value.SourceCardId == inf.CardId)
                {
                    inf.Turns++;
                }
                else
                {
                    inf.CardId = cube.Value.SourceCardId;
                    inf.Turns = 1; // the block is on the cell as of this turn
                }
                if (inf.Turns >= TurnsToDetonate)
                {
                    ripe.Add(cell);
                }
            }

            bool spreadNow = false;
            GridPos spreadCentre = default;
            foreach (GridPos cell in ripe)
            {
                Infection inf = infected[cell];
                int cardId = inf.CardId;
                inf.CardId = -1;
                inf.Turns = 0;
                if (cardId < 0)
                {
                    continue;
                }
                List<GridPos> blockCells = CellsOfCard(board, cardId);
                if (blockCells.Count == 0)
                {
                    continue; // block already gone this turn (overlapping infection)
                }
                IReadOnlyList<GridPos> blown = turn.Round.DestroyCubes(blockCells, true);
                if (blown.Count > 0)
                {
                    turn.AddFlatScore(blown.Count * PointsPerInfectedCube, DefId);
                    turn.Round.TryResolveCleanSweep();
                    if (!hasSpread)
                    {
                        spreadNow = true;
                        spreadCentre = cell;
                    }
                }
            }

            if (spreadNow)
            {
                hasSpread = true;
                SpreadPlus(board, spreadCentre);
            }
        }

        /// <summary>The one-time spread: the centre and its four orthogonal neighbours become
        /// infected too (a 3x3 plus). Cells already infected keep their progress.</summary>
        private void SpreadPlus(GameBoard board, GridPos centre)
        {
            AddInfection(board, centre);
            AddInfection(board, new GridPos(centre.X + 1, centre.Y));
            AddInfection(board, new GridPos(centre.X - 1, centre.Y));
            AddInfection(board, new GridPos(centre.X, centre.Y + 1));
            AddInfection(board, new GridPos(centre.X, centre.Y - 1));
        }

        private void AddInfection(GameBoard board, GridPos cell)
        {
            if (!board.IsInside(cell) || infected.ContainsKey(cell))
            {
                return;
            }
            Cube? cube = board.GetCube(cell);
            infected[cell] = new Infection
            {
                CardId = cube.HasValue ? cube.Value.SourceCardId : -1,
                Turns = 0
            };
        }

        private static List<GridPos> CellsOfCard(GameBoard board, int cardId)
        {
            var cells = new List<GridPos>();
            foreach (GridPos cell in board.GetOccupiedCells())
            {
                Cube? cube = board.GetCube(cell);
                if (cube.HasValue && cube.Value.SourceCardId == cardId)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }
    }
}
