// PURPOSE: The three powers that need machinery of their own: Kum saati (rewinds the
// board), Olta (fishes a marked card out of the piles) and Tılsım (turns ghost traces into
// real play area).
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// "Kum saati" - puts the BOARD back to where it stood two turns ago. The hand, the piles
    /// and the score deliberately do not move: only the grid rewinds, which is what makes it
    /// a rescue rather than a full undo.
    /// </summary>
    public sealed class KumSaatiPower : Power
    {
        public int TurnsBack = 2;

        public KumSaatiPower()
            : base("kum_saati", "Kum Saati")
        {
            SetDescription(
                "Rewinds the board 2 turns. The hand, the piles and the score stay put.",
                "Oyun alanını 2 tur geriye sarar. El, deste ve ıskarta olduğu gibi kalır.");
            BaseSellValue = 60;
        }

        public override string StatusText
        {
            get { return Loc.Pick(TurnsBack + " turns back", TurnsBack + " tur geri"); }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.BoardHistoryCount >= TurnsBack;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.RewindBoard(TurnsBack);
        }
    }

    /// <summary>
    /// "Olta" - mark one held card per round, then reel it back in from wherever it drifted.
    ///  - already in hand : nothing happens, the cast is wasted
    ///  - in the draw pile: it lands in the bonus hand for free
    ///  - in the discard  : it lands in the bonus hand too, but the rod is stuck until the
    ///                      next clean sweep
    /// A card fished up goes to the DISCARD when played rather than expiring, and if it is
    /// played the same turn it was fished and that turn sweeps the board, it pays a bonus.
    ///
    /// Marking is free and separate from the charge: it is a per-round setup action, so the
    /// UI calls TryMark, not the generic use path.
    /// </summary>
    public sealed class OltaPower : Power
    {
        public int SweepBonus = 120;

        /// <summary>Card the rod is set for, or null.</summary>
        public int? MarkedCardId { get; private set; }

        /// <summary>True once a discard-pull locked the rod until the next sweep.</summary>
        public bool StuckUntilSweep { get; private set; }

        private bool markedThisRound;
        private int fishedCardId = -1;
        private int fishedOnTurn = -1;

        public OltaPower()
            : base("olta", "Olta")
        {
            SetDescription(
                "Mark one held card per round; reel it back into your bonus hand. Pulling it "
                    + "from the draw pile is free (the rod stays ready); pulling it from the "
                    + "discard locks the rod until the next clean sweep.",
                "Raunt başına bir kart işaretlersin; kartı bonus eline çekersin. Çekme "
                    + "destesinden çekmek bedavadır (olta hazır kalır); ıskartadan çekersen "
                    + "olta bir sonraki temizliğe kadar kilitlenir.");
            BaseSellValue = 55;
        }

        public override string StatusText
        {
            get
            {
                if (StuckUntilSweep)
                {
                    return Loc.Pick("stuck", "takıldı");
                }
                return MarkedCardId.HasValue
                    ? Loc.Pick("marked", "işaretli")
                    : Loc.Pick("idle", "boşta");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            MarkedCardId = null;
            markedThisRound = false;
            StuckUntilSweep = false;
            fishedCardId = -1;
            fishedOnTurn = -1;
        }

        /// <summary>Marks a held card. Free, once per round, and NOT the power's charge.</summary>
        public bool TryMark(RoundContext ctx, int handIndex)
        {
            if (markedThisRound || ctx.Round.Status != RoundStatus.InProgress)
            {
                return false;
            }
            if (handIndex < 0 || handIndex >= ctx.Round.Hand.Count)
            {
                return false;
            }
            MarkedCardId = ctx.Round.Hand[handIndex].Id;
            markedThisRound = true;
            return true;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return MarkedCardId.HasValue && !StuckUntilSweep;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            int cardId = MarkedCardId.Value;
            RoundEngine round = ctx.Round;

            // Already in hand: nothing to reel in, so the cast is free (charge kept).
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == cardId)
                {
                    KeepChargeAfterUse = true;
                    return true;
                }
            }

            bool fromDiscard = IsInDiscard(round, cardId);
            BlockCard card = round.TakeCardFromPiles(cardId);
            if (card == null)
            {
                KeepChargeAfterUse = true; // on the board / out of the round: nothing happened
                return true;
            }
            // ToDiscard: a fished card rejoins the pile economy instead of expiring.
            round.AddBonusCard(card, BonusPlayOutcome.ToDiscard);
            fishedCardId = cardId;
            fishedOnTurn = round.TurnNumber;
            if (fromDiscard)
            {
                // Recovering a spent card is the costly pull: the rod stays stuck (and the
                // charge is spent) until the next clean sweep.
                StuckUntilSweep = true;
            }
            else
            {
                // Pulling a card that was still in the draw pile is free - Olta keeps its
                // charge and is ready to use again.
                KeepChargeAfterUse = true;
            }
            return true;
        }

        /// <summary>Playing the fished card on the turn it was fished, and sweeping the board
        /// with it, is the pay-off this power is built around.</summary>
        public override void AfterCleanSweep(TurnContext turn)
        {
            if (fishedCardId < 0 || turn.Report.Card == null)
            {
                return;
            }
            if (turn.Report.Card.Id == fishedCardId && turn.Report.TurnNumber == fishedOnTurn + 1)
            {
                turn.AddFlatScore(SweepBonus, DefId);
            }
            fishedCardId = -1;
            // The sweep also frees a rod stuck by a discard pull. The central recharge in
            // PowerInventory has already put the charge back.
            StuckUntilSweep = false;
        }

        private static bool IsInDiscard(RoundEngine round, int cardId)
        {
            foreach (BlockCard card in round.Deck.DiscardPile)
            {
                if (card.Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>"İkinci şans" - overtime only, once per round: clears the board (no score, no
    /// sweep), pulls the earned score back down to the threshold, reshuffles the whole deck
    /// into the draw pile, and lets the round continue - a fresh overtime attempt.</summary>
    public sealed class IkinciSansPower : Power
    {
        private bool usedThisRound;

        public IkinciSansPower()
            : base("ikinci_sans", "İkinci Şans")
        {
            SetDescription(
                "Overtime only, once per round: clears the board (no score, no sweep), pulls "
                    + "your score back to the threshold, reshuffles the deck into the draw pile "
                    + "and plays on.",
                "Sadece uzatmada, raunt başına bir kez: oyun alanını temizler (puan yok, "
                    + "temizlik sayılmaz), puanını eşiğe çeker, desteyi karıp çekme destesine "
                    + "koyar ve oyun devam eder.");
            BaseSellValue = 60;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            usedThisRound = false;
        }

        public override string StatusText
        {
            get
            {
                return usedThisRound
                    ? Loc.Pick("used", "kullanıldı")
                    : Loc.Pick("overtime only", "sadece uzatma");
            }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Overtime && !usedThisRound;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            RoundEngine round = ctx.Round;
            round.ClearBoardScoreless();
            round.CapRoundScoreAtThreshold();
            round.Deck.DumpDrawPileIntoDiscard();
            round.Deck.ShuffleDiscardIntoDraw();
            usedThisRound = true;
            return true;
        }
    }

    /// <summary>"Totem" - overtime only: pulls the earned score down to the threshold, sends
    /// the run to the market, and is consumed.</summary>
    public sealed class TotemPower : Power
    {
        public TotemPower()
            : base("totem", "Totem")
        {
            SetDescription(
                "Overtime only: pulls your score to the threshold, sends you to the market, "
                    + "and is destroyed.",
                "Sadece uzatmada: puanını eşiğe çeker, seni market fazına geçirir ve yok olur.");
            BaseSellValue = 70;
        }

        public override string StatusText
        {
            get { return Loc.Pick("overtime only", "sadece uzatma"); }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Overtime;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            ctx.Round.CapRoundScoreAtThreshold();
            ctx.Round.ForceAdvanceToMarket();
            ctx.Session.Powers.Remove(this); // self-destruct
            return true;
        }
    }

    /// <summary>
    /// "Tılsım" - blows up the ghost cubes hanging off the edge of the board and turns the
    /// space they occupied into real play area for the rest of the RUN... except the design
    /// says it resets when the round ends, so the cells are granted for the current round
    /// only and handed back at round end.
    ///
    /// It leans on the same seam "Kentsel Dönüşüm" uses: extra playable cells travel to the
    /// next board through RoundConfig. Because the board of the CURRENT round already exists,
    /// the conversion takes effect from the next round - the ghosts are cleared immediately,
    /// the ground they leave behind opens next round.
    /// </summary>
    public sealed class TilsimPower : Power
    {
        /// <summary>Points per ghost cube blown up.</summary>
        public int PointsPerGhostCube = 15;

        private readonly List<GridPos> convertedCells = new List<GridPos>();

        public TilsimPower()
            : base("tilsim", "Tılsım")
        {
            SetDescription(
                "Blows up ghost blocks and turns the space they covered outside the map "
                    + "into play area. Resets when the round ends.",
                "Hayalet blokları patlatır ve harita dışında kapladıkları yeri "
                    + "oyun alanına katar. Raunt bitince sıfırlanır.");
            BaseSellValue = 65;
        }

        /// <summary>Cells this power is currently granting to the board.</summary>
        public int ConvertedCellCount
        {
            get { return convertedCells.Count; }
        }

        public override string StatusText
        {
            get
            {
                return convertedCells.Count > 0
                    ? Loc.Pick("+" + convertedCells.Count + " cells", "+" + convertedCells.Count + " kare")
                    : Loc.Pick("ready", "hazır");
            }
        }

        /// <summary>Confirmed: the conversion lasts for the round only.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            convertedCells.Clear();
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Board.OutsideCubes.Count > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            int ghosts = ctx.Round.Board.OutsideCubes.Count;
            if (ghosts == 0)
            {
                return false;
            }
            List<GridPos> converted = ctx.Round.Board.TakeOutsideCellsForConversion();
            foreach (GridPos cell in converted)
            {
                // Only cells the board can actually grow into: it never grows left or down.
                if (cell.X >= 0 && cell.Y >= 0 && !convertedCells.Contains(cell))
                {
                    convertedCells.Add(cell);
                }
            }
            ctx.Round.AddScoreOutsideTurn(ghosts * PointsPerGhostCube);
            return true;
        }

        /// <summary>Hands the converted ground to the board being built.</summary>
        public override RoundConfig FilterRoundConfig(SessionContext ctx, RoundConfig config)
        {
            if (convertedCells.Count == 0)
            {
                return config;
            }
            var cells = new List<GridPos>(config.ExtraPlayableCells);
            cells.AddRange(convertedCells);
            return new RoundConfig(config.RoundNumber, config.BoardWidth, config.BoardHeight,
                config.ScoreThreshold, cells);
        }
    }
}
