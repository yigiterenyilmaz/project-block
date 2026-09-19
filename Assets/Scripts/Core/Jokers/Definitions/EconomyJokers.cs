// PURPOSE: The jokers wired into the run economy and the market: Kapalı Ekonomi,
// ihale, Kara delik, Enfeksiyon.
//
// ("Powerbank" used to live here; it is a power now - Powers/Definitions/PowerbankPower.cs.)
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Kapalı Ekonomi" - skip the market and your score grows.
    ///
    /// (designer's call, 2026-09-19) Every market left WITHOUT buying adds a percentage to a
    /// score bonus that stays for the rest of the run: 5% for a skip, and 2.5% more per skip for
    /// every boss defeated before it. Skipping after round 1 (no boss yet) and after round 7 (two
    /// bosses down) is 5% + 10% = 15%. The bonus is a MULTIPLIER on every turn's score, so it
    /// lifts whatever the turn earned - base values and the other jokers' flat bonuses alike.
    /// Buying something does not take the bonus away; it simply adds nothing that market.</summary>
    public sealed class KapaliEkonomiJoker : Joker
    {
        /// <summary>Percent added by a skip with no boss beaten yet.</summary>
        public double PercentPerSkip = 5.0;

        /// <summary>Extra percent per skip for every boss already defeated.</summary>
        public double PercentPerBossDefeated = 2.5;

        /// <summary>The accumulated bonus, in percent.</summary>
        public double BonusPercent { get; private set; }

        /// <summary>Markets skipped over the run.</summary>
        public int MarketsSkipped { get; private set; }

        public KapaliEkonomiJoker()
            : base("kapali_ekonomi", "Kapalı Ekonomi")
        {
            SetDescription(
                "Every market you leave without buying anything adds +5% to all the points you "
                    + "score for the rest of the run - and +2.5% more per skip for every boss "
                    + "you have defeated.",
                "Hiçbir şey almadan çıktığın her market, oyunun kalanında kazandığın tüm "
                    + "puanlara +%5 ekler - yendiğin her patron için her atlamada +%2,5 daha.");
        }

        public override string StatusText
        {
            get
            {
                string pct = BonusPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                return Loc.Pick("+" + pct + "% score", "puan +%" + pct);
            }
        }

        /// <summary>Statistics: one proc per skip, and the tooltip's points are what the bonus
        /// has actually added.</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public override void OnMarketLeft(SessionContext ctx, bool anythingPurchased)
        {
            if (anythingPurchased)
            {
                return;
            }
            MarketsSkipped++;
            BonusPercent += PercentPerSkip + PercentPerBossDefeated * BossesDefeated(ctx.Session);
            NoteProc(0);
        }

        /// <summary>Boss stages already survived. A boss stage carries the number of the round
        /// it follows, so every round up to this one whose boss has been played counts - and this
        /// round's own only once we are past its boss (InBossStage).</summary>
        private static int BossesDefeated(GameSession session)
        {
            int beaten = 0;
            for (int r = 1; r <= session.RoundNumber; r++)
            {
                if (!session.Config.Progression.HasBossStageAfter(r))
                {
                    continue;
                }
                if (r < session.RoundNumber || session.InBossStage)
                {
                    beaten++;
                }
            }
            return beaten;
        }

        public override void ModifyScore(TurnContext turn)
        {
            if (BonusPercent <= 0)
            {
                return;
            }
            int before = turn.Score.Total;
            turn.Score.AddMultiplier(1.0 + BonusPercent / 100.0, DefId);
            int scale = turn.Score.ScoreScale < 1 ? 1 : turn.Score.ScoreScale;
            int gained = (turn.Score.Total - before) / scale;
            if (gained > 0)
            {
                NoteProc(gained, turn);
            }
        }
    }

    /// <summary>"ihale" - opens an auction on one random joker: it puts a premium of 20% of
    /// your CURRENT total points on that joker's sell price. The price is set once, when the
    /// auction opens, and only a new auction - after that joker is sold or destroyed - sets
    /// another.</summary>
    public sealed class IhaleJoker : Joker
    {
        /// <summary>Share of the run's total points the premium is worth, in percent.</summary>
        public double PremiumPercent = 20.0;

        public IhaleJoker()
            : base("ihale", "İhale")
        {
            SetDescription(
                "At a round start it picks a random joker and adds 20% of your current total "
                    + "points to its sell price. No new auction opens until that joker is sold; "
                    + "the price is fixed when the auction opens.",
                "Raunt başında rastgele bir joker seçer ve o anki toplam puanının %20'sini "
                    + "satış fiyatına ekler. O joker satılana kadar yeni ihale açılmaz; fiyat "
                    + "ihale açılınca sabitlenir.");
        }

        public override string StatusText
        {
            get
            {
                if (auctionedName == null)
                {
                    return Loc.Pick("no auction", "ihale yok");
                }
                return auctionedName + "  +" + auctionedPremiumShown;
            }
        }

        private string auctionedName;

        /// <summary>The premium as the market shows it (screen points).</summary>
        private long auctionedPremiumShown;

        /// <summary>Statistics: one proc per auction opened.</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

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
            // The premium is written in LOGICAL points (the sale multiplies by ScoreScale), and
            // the total is in screen points, so the share is divided back down.
            int scale = ctx.Scoring.ScoreScale < 1 ? 1 : ctx.Scoring.ScoreScale;
            long share = (long)System.Math.Round(ctx.Session.TotalScore * PremiumPercent / 100.0);
            int premium = (int)System.Math.Min(int.MaxValue, share / scale);
            if (premium <= 0)
            {
                return; // nothing to auction yet - an empty auction would only lock the next one
            }
            inventory.SetAuctionPremium(target, premium);
            auctionedName = target.DisplayName;
            auctionedPremiumShown = (long)premium * scale;
            NoteProc(0);
        }

        /// <summary>The auctioned joker left the inventory (sold or destroyed): the lock
        /// opens again. The premium the buyer already paid is theirs to keep.</summary>
        public override void OnJokerRemoved(SessionContext ctx, Joker other)
        {
            if (!ctx.Session.Jokers.ActiveAuctionInstanceId.HasValue)
            {
                auctionedName = null;
                auctionedPremiumShown = 0;
            }
        }
    }

    /// <summary>"Kara delik" - every clean sweep hands the player a void block, cut to the shape
    /// of a random card of the deck (designer's call, 2026-09-19 - it used to be a 1x1). A void
    /// block can be dropped onto occupied cells; each cube that lands on one of its cubes is
    /// swallowed and that void cube is used up. The cards are round-scoped and never join the
    /// owned deck.</summary>
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
                "Every clean sweep adds a void block to your discard, shaped like a random "
                    + "block of your deck. A void block can be placed over filled cells, and "
                    + "each of its cubes swallows whatever lands on it.",
                "Her temizlikte ıskartana, destendeki rastgele bir bloğun şeklinde bir boşluk "
                    + "bloğu ekler. Boşluk bloğu dolu hücrelerin üstüne konabilir ve her küpü "
                    + "üstüne geleni yutar.");
        }

        /// <summary>Statistics: one proc per void block handed out.</summary>
        public override bool TracksProcs
        {
            get { return true; }
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
            BlockCard card = MakeVoidCard(turn.Session, turn.Rng);
            liveVoidCardIds.Add(card.Id);
            GrantedThisRound++;
            NoteProc(0, turn);
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

        private static BlockCard MakeVoidCard(GameSession session, IRandomSource rng)
        {
            return session.CreateCard(VoidShape(session, rng), new[] { BlockElement.Void });
        }

        /// <summary>THE void block's shape: a random card of the owned deck, or a single cube
        /// when there is no deck to draw from. Public so the debug gallery deals the same thing
        /// the joker does.</summary>
        public static BlockShape VoidShape(GameSession session, IRandomSource rng)
        {
            IReadOnlyList<BlockCard> deck = session.OwnedCards;
            if (deck.Count == 0 || rng == null)
            {
                return BlockShape.FromCells(new[] { new GridPos(0, 0) });
            }
            return deck[rng.NextInt(0, deck.Count)].Shape;
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
    /// cube ON THAT CELL detonates (only that cube - the rest of the block stays). Only the FIRST detonation spreads the infection, once, into a
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

        /// <summary>Cells the detonation took on the most recent turn, for the view's blast.
        /// The detonation happens in AfterTurnScored, so those cubes are in the destruction log
        /// but in no exploded row or column - without this the view has no way to know they
        /// died and the block just vanished. Mirrors RobotSupurgeJoker.LastSweptCells; the
        /// list is reused, so the view copies what it needs.</summary>
        private readonly List<GridPos> lastDetonated = new List<GridPos>();

        public IReadOnlyList<GridPos> LastDetonatedCells
        {
            get { return lastDetonated; }
        }

        /// <summary>
        /// Cells the ONE spread actually infected this turn, and the cell it went out from.
        ///
        /// FOR THE VIEW, and it has to be reported rather than inferred: the plus is only four
        /// neighbours ON PAPER. AddInfection turns down a cell that is off the board or already
        /// infected, so an animation that drew all four arms would be showing infections that do
        /// not exist. Empty when nothing spread - which is every detonation after the first.
        /// </summary>
        public IReadOnlyList<GridPos> LastSpreadCells
        {
            get { return lastSpread; }
        }

        /// <summary>Where that spread came from. Null when nothing spread this turn.</summary>
        public GridPos? LastSpreadCentre
        {
            get { return spreadFrom; }
        }

        private readonly List<GridPos> lastSpread = new List<GridPos>();

        private GridPos? spreadFrom;

        public EnfeksiyonJoker()
            : base("enfeksiyon", "Enfeksiyon")
        {
            SetDescription(
                "Infect one cube. When the same block has sat on it for 3 turns, the cube on "
                    + "that cell detonates - only that cube, not the rest of its block. The first "
                    + "detonation spreads the infection once into a 3x3 plus - and no further.",
                "Bir küpü enfekte edersin. Aynı blok o karede 3 tur durursa o karedeki küp "
                    + "patlar - yalnızca o küp, bloğun geri kalanı değil. İlk patlama "
                    + "enfeksiyonu bir kez 3x3 artı şeklinde yayar - daha fazla değil.");
            ChargesPerRound = 1;
        }

        /// <summary>Statistics: one proc per detonation.</summary>
        public override bool TracksProcs
        {
            get { return true; }
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
            // Cleared every turn, before the early-out, so the view can never replay a blast
            // from an earlier detonation.
            lastDetonated.Clear();
            lastSpread.Clear();
            spreadFrom = null;
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
                // ONLY THE INFECTED CUBE (designer's call, 2026-09-19). It used to take every
                // cube of the block sitting on the cell, which read as the infection wiping a
                // block it had barely touched.
                Cube? here = board.GetCube(cell);
                if (!here.HasValue || here.Value.SourceCardId != cardId)
                {
                    continue; // gone already this turn (overlapping infection)
                }
                IReadOnlyList<GridPos> blown = turn.Round.DestroyCubes(new List<GridPos> { cell }, true);
                if (blown.Count > 0)
                {
                    lastDetonated.AddRange(blown);
                    NoteProc(turn.Round.ExternalDestructionScores
                        ? blown.Count * PointsPerInfectedCube : 0, turn);
                    // The detonation is the joker's destruction: it pays only under "Genel
                    // temizlik".
                    if (turn.Round.ExternalDestructionScores)
                    {
                        int paid = blown.Count * PointsPerInfectedCube;
                        turn.AddFlatScore(paid, DefId);
                        // This payment exists only because "Genel temizlik" is held, so it is
                        // part of what that joker has been worth - see
                        // RoundEngine.ExternalScoreCredited. Reporting only.
                        turn.Round.CreditExternalScore(paid);
                    }
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
                spreadFrom = spreadCentre;
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

        /// <summary>Infects a cell if it can be. Records the ones that TOOK, so the view shows
        /// the infections that exist rather than the four it assumed - see LastSpreadCells.</summary>
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
            lastSpread.Add(cell);
        }
    }
}
