// PURPOSE: The three powers that need machinery of their own: Kum saati (rewinds the
// board), Olta (fishes a marked card out of the piles) and Tılsım (turns ghost traces into
// real play area).
//
// All numbers are BALANCE PLACEHOLDERS.

using System;
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
    /// (including the hand) into the draw pile and deals a fresh hand, and lets the round
    /// continue - a fresh overtime attempt.</summary>
    public sealed class IkinciSansPower : Power
    {
        private bool usedThisRound;

        public IkinciSansPower()
            : base("ikinci_sans", "İkinci Şans")
        {
            SetDescription(
                "Overtime only, once per round: clears the board (no score, no sweep), pulls "
                    + "your score back to the threshold, reshuffles the deck into the draw pile, "
                    + "deals a fresh hand and plays on.",
                "Sadece uzatmada, raunt başına bir kez: oyun alanını temizler (puan yok, "
                    + "temizlik sayılmaz), puanını eşiğe çeker, desteyi karıp çekme destesine "
                    + "koyar, yeni bir el dağıtır ve oyun devam eder.");
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
            // RedrawHand discards the current hand and shuffles the whole discard (now holding
            // the dumped draw pile AND the hand) back into the draw pile, then deals a fresh
            // hand - a clean overtime restart. This intentional recycle is fine: the power is
            // overtime-only and once per round (unlike the free-recycle gating on İade).
            round.RedrawHand();
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
    /// "Tılsım" - blows up the ghost cubes hanging off the edge of the board (15 points each)
    /// and turns the space they occupied into BONUS play area on the board IN PLAY, for the
    /// rest of this round. It used to deliver the ground only on the NEXT round's board, which
    /// on the round it was used looked like nothing had happened at all.
    ///
    /// The board grows in any direction to reach the cells (GameBoard.CreateWithBonusGround -
    /// coordinates never move, the origin does, exactly as an inflation), but only those cells
    /// become playable. A new round builds a fresh arena, so the gift simply ends with its round.
    /// </summary>
    public sealed class TilsimPower : Power
    {
        /// <summary>Each ghost cube blown up pays this percent of the round's threshold.</summary>
        public int GhostSharePercent = 2;

        /// <summary>A line or sweep that takes cubes off the BONUS ground: this percent of the
        /// clear's base value per such cube, added flat.</summary>
        public int BonusGroundPercentPerCube = 20;

        /// <summary>What one ghost pays in this round: a share of the bar, never less than 1.
        /// </summary>
        public int PointsPerGhostCube(RoundEngine round)
        {
            return Math.Max(1, round.ScoreThreshold * GhostSharePercent / 100);
        }

        /// <summary>
        /// WHETHER GROUND CAN BE RECLAIMED FROM A GHOST AT THIS CELL. Since the ground is granted
        /// on the live board, which can grow on any side, every ghost can. Kept as the ONE place
        /// that is decided, because the report and the animation lab both ask it.
        /// </summary>
        public static bool CanReclaim(GridPos cell)
        {
            return true;
        }

        private readonly List<GridPos> convertedCells = new List<GridPos>();

        /// <summary>WHAT THE HARVEST TOOK, for the View: every ghost with its own face, which of
        /// them the rules could reclaim ground from, and what it all paid. Written before the
        /// ghosts are removed, because an animation that begins from a block's own colour cannot
        /// begin from a block that has already been deleted. Reporting only, [NotSaved].</summary>
        [field: NotSaved]
        public TalismanActivationVisuals LastActivation { get; private set; }

        /// <summary>THE GROUND AS THE BOARD ACTUALLY GOT IT. Written the moment it is granted and
        /// dropped when the round ends, which is what the View reads as the gift being recalled.
        /// </summary>
        [field: NotSaved]
        public TalismanGroundVisuals LastGround { get; private set; }

        public TilsimPower()
            : base("tilsim", "Tılsım")
        {
            SetDescription(
                "Blows up ghost blocks (each cube pays 2% of the threshold) and turns the space "
                    + "they covered outside the map into BONUS play area for the rest of the "
                    + "round. A row, column or sweep that clears cubes standing on it scores "
                    + "+20% per such cube.",
                "Hayalet blokları patlatır (her küp eşiğin %2'si kadar puan) ve harita dışında "
                    + "kapladıkları yeri raunt sonuna kadar BONUS oyun alanına katar. Bu alandaki "
                    + "küpleri de patlatan her satır, sütun ya da temizlik küp başına +%20 puan "
                    + "kazandırır.");
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

        /// <summary>The gift lasts one round: the fresh arena does not carry it, and the empty
        /// ground report is what tells the View to take it back.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            convertedCells.Clear();
            LastGround = null;
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
            // A NEW report per use: views match reports by identity (see CLAUDE.md).
            LastActivation = new TalismanActivationVisuals();
            LastActivation.Serial = (LastGround != null ? LastGround.Serial : 0) + 1;
            int perGhost = PointsPerGhostCube(ctx.Round);
            LastActivation.ScorePerGhost = perGhost;
            LastActivation.TotalScore = ghosts * perGhost;
            // SNAPSHOT FIRST. The ghosts are about to stop existing, and the View's harvest starts
            // from each one's own face - so it is taken here rather than reconstructed after.
            foreach (KeyValuePair<GridPos, Cube> ghost in ctx.Round.Board.OutsideCubes)
            {
                LastActivation.Ghosts.Add(new HarvestedGhost
                {
                    Cell = ghost.Key,
                    Cube = ghost.Value,
                    Reclaimable = CanReclaim(ghost.Key)
                });
            }
            List<GridPos> taken = ctx.Round.Board.TakeOutsideCellsForConversion();
            var wanted = new List<GridPos>();
            foreach (GridPos cell in taken)
            {
                if (CanReclaim(cell))
                {
                    wanted.Add(cell);
                }
            }
            List<GridPos> granted = ctx.Round.GrantBonusGround(wanted);
            foreach (GridPos cell in granted)
            {
                if (!convertedCells.Contains(cell))
                {
                    convertedCells.Add(cell);
                }
                LastActivation.Reclaimed.Add(cell);
            }
            // The View unwraps the WHOLE gift standing on the board, earlier uses included.
            var ground = new TalismanGroundVisuals();
            ground.Serial = LastActivation.Serial;
            ground.Cells.AddRange(convertedCells);
            LastGround = ground;
            ctx.Round.AddScoreOutsideTurn(ghosts * perGhost);
            return true;
        }

        /// <summary>Lines and sweeps that clear cubes off the bonus ground pay +20% of the clear's
        /// BASE value per such cube, added flat (it does not compound with joker multipliers).
        /// </summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            TurnReport report = turn.Report;
            if (convertedCells.Count == 0 || report == null)
            {
                return;
            }
            bool lines = (report.ExplodedRows != null && report.ExplodedRows.Count > 0)
                || (report.ExplodedColumns != null && report.ExplodedColumns.Count > 0);
            if (!lines && !report.CleanSweep)
            {
                return;
            }
            // ONLY the bonus-ground cubes of the rows and columns that broke (a cube where a
            // broken row and column cross counts once), plus, on a sweep, the ones it took.
            // Anything else destroyed on that ground this turn (a fire chain, a power) pays nothing.
            // ExplodedRows / ExplodedColumns are indices into the board's STORE, not coordinates:
            // once the ground grew left or down the origin is negative, so they are offset back.
            GameBoard board = turn.Round.Board;
            int onBonus = 0;
            foreach (DestroyedCube gone in report.DestroyedCubes)
            {
                if (!convertedCells.Contains(gone.Pos))
                {
                    continue;
                }
                bool inLine = (report.ExplodedRows != null && Contains(report.ExplodedRows, gone.Pos.Y - board.MinY))
                    || (report.ExplodedColumns != null && Contains(report.ExplodedColumns, gone.Pos.X - board.MinX));
                if (inLine || report.CleanSweep)
                {
                    onBonus++;
                }
            }
            // ADDITIVE, off the BASE value of the clear: the lines' and the sweep's own points,
            // before any joker touched them, in logical units (AddLateTurnScore scales once).
            // Taking it off the finished total compounded it with every multiplier and applied
            // the score scale twice.
            int baseClear = turn.Score.BaseLines + turn.Score.BaseSweep;
            if (onBonus == 0 || baseClear <= 0)
            {
                return;
            }
            LastBonusGroundPayout = baseClear * BonusGroundPercentPerCube * onBonus / 100;
            turn.Round.AddLateTurnScore(LastBonusGroundPayout, "Tılsım");
        }

        private static bool Contains(IReadOnlyList<int> lines, int index)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == index)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>What the last bonus-ground clear paid, for the View. [NotSaved].</summary>
        [field: NotSaved]
        public int LastBonusGroundPayout { get; private set; }
    }

    /// <summary>"Halüsinasyon" - a power with no fixed identity: it appears as a random power
    /// and, whenever used, instantly recharges and morphs into a DIFFERENT random one. It only
    /// ever becomes an instant, self-contained power (one clean Run) - never a legendary /
    /// reality-bending one, never one that needs special routing (Olta's marking, Eko's
    /// arm-and-replay), and never one that carries state across turns
    /// (inflation, Bükülme). Everything the current form needs - its targeting, preview, run
    /// and can-run - is delegated to that inner power, so from the engine's side it behaves
    /// exactly like whatever it currently is.</summary>
    public sealed class HalusinasyonPower : Power
    {
        /// <summary>The pool it can morph into: instant, single-Run, standard-routed powers
        /// with no per-round/per-turn lifetime. Ids missing from the registry are skipped.</summary>
        private static readonly string[] Pool =
        {
            "caprazlama", "cerceve", "bardagin_bos_tarafi", "mayin", "cimbiz", "klon",
            "transfer", "hologram", "hizli_cekim_sarjoru", "asirma", "yedekleme",
            "soguk_fuzyon", "kum_saati"
        };

        private Power current;

        public HalusinasyonPower()
            : base("halusinasyon", "Halüsinasyon")
        {
            SetDescription(
                "Appears as a random power. USE it to run that power, or RIGHT-CLICK to skip to a "
                    + "different one (that spends the charge). At the end of EVERY turn it becomes a "
                    + "new random power and refills. Never becomes a legendary power.",
                "Rastgele bir güç olarak görünür. KULLANINCA o gücü çalıştırır, SAĞ TIKLAYINCA "
                    + "başka bir güce atlar (bu şarjı harcar). HER turun sonunda yeni bir rastgele "
                    + "güce dönüşür ve yeniden dolar. Asla efsanevi bir güce dönüşmez.");
        }

        public override string Description
        {
            get
            {
                if (current == null)
                {
                    return base.Description;
                }
                return Loc.Pick(
                    "Random power - now " + current.DisplayName + ": " + current.Description,
                    "Rastgele güç - şu an " + current.DisplayName + ": " + current.Description);
            }
        }

        public override string StatusText
        {
            get
            {
                return current == null
                    ? Loc.Pick("rolling...", "değişiyor...")
                    : Loc.Pick("now: " + current.DisplayName, "şu an: " + current.DisplayName);
            }
        }

        public override ActivationTargeting Targeting
        {
            get { return current != null ? current.Targeting : ActivationTargeting.None; }
        }

        public override System.Collections.Generic.IReadOnlyList<GridPos> PreviewCells(
            ActivationTarget target)
        {
            return current != null ? current.PreviewCells(target) : base.PreviewCells(target);
        }

        public override void OnAcquired(SessionContext ctx)
        {
            if (current == null)
            {
                Reroll(ctx.Rng);
            }
        }

        /// <summary>
        /// A new turn, a new hallucination: at the end of EVERY turn it RENEWS - it becomes a
        /// different random power AND refills its charge, whether or not it was used or skipped
        /// during the turn. The one-power-per-turn rule still holds; what it gives up is being
        /// able to hold on to a roll it liked. A boss that forbids refills ("Tükenmişlik") still
        /// forbids this one - it changes form, but stays empty.
        /// </summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            Reroll(turn.Rng);
            if (turn.Round == null || !turn.Round.PowerRechargeBlocked)
            {
                Recharge();
            }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return current != null && current.CanRun(ctx, target);
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (current == null)
            {
                Reroll(ctx.Rng);
            }
            if (current == null || !current.Run(ctx, target))
            {
                return false;
            }
            // Using it runs the shown power and spends the charge like any power (it refills next
            // round). It still morphs into a new random power for the next time it comes up.
            Reroll(ctx.Rng);
            return true;
        }

        /// <summary>The "skip" action: morph into a different random power WITHOUT running the
        /// current one. The caller (PowerInventory.TrySkip) spends the charge, so skipping costs
        /// the round's use exactly like using it does - both refill next round.</summary>
        public void SkipToNextPower(IRandomSource rng)
        {
            Reroll(rng);
        }

        private void Reroll(IRandomSource rng)
        {
            string previous = current != null ? current.DefId : null;
            Power picked = null;
            for (int attempt = 0; attempt < 8 && picked == null; attempt++)
            {
                string id = Pool[rng.NextInt(0, Pool.Length)];
                if (id == previous && Pool.Length > 1)
                {
                    continue; // avoid morphing into the same power twice in a row
                }
                if (RarityTable.For(id) == Rarity.Legendary)
                {
                    continue; // never a legendary power, even if the pool is re-graded one day
                }
                picked = PowerRegistry.Create(id); // null if unknown; the loop tries again
            }
            if (picked == null)
            {
                picked = PowerRegistry.Create(Pool[0]);
            }
            current = picked;
        }
    }

    /// <summary>"Karakter oluşturma" - a maker's power. Using it opens a designer (in the UI)
    /// where the player draws any shape and picks an element; the custom block is baked into
    /// the owned deck and shuffles in from the next round. The whole effect - and spending the
    /// charge - is done in one place, GameSession.CreateDesignedBlock, once the designer is
    /// confirmed. This class is therefore just a marker: it never runs through the normal use
    /// path (Run returns false), it only tells the UI "open the designer".</summary>
    public sealed class KarakterOlusturmaPower : Power
    {
        public KarakterOlusturmaPower()
            : base("karakter_olusturma", "Karakter Oluşturma")
        {
            SetDescription(
                "Design a custom block - any shape, any element - and bake it into your deck. "
                    + "It shuffles in from the next round.",
                "İstediğin şekil ve elementte özel bir blok tasarla ve destene ekle. Sonraki "
                    + "raunttan itibaren desteye karışır.");
        }

        /// <summary>Usable whenever the standard rules allow (the UI opens the designer then);
        /// the actual make + charge-spend happens in GameSession.CreateDesignedBlock.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return true;
        }

        /// <summary>Never taken through TryUse - the designer flow spends the charge itself.</summary>
        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            return false;
        }
    }

    /// <summary>"Retro" - a toggle, not a spend. Using it flips tetris placement mode on or off
    /// (RoundRules.RetroMode); it never consumes its charge and never needs to recharge, so it
    /// can be switched any turn. While on, blocks fall from the top and ANY block can rotate,
    /// and every placement pays ScoringConfig.RetroPlacementBonus. The falling/steering is the
    /// View's job; the engine reads the one flag. Using it again turns it back off.</summary>
    public sealed class RetroPower : Power
    {
        /// <summary>Rows grown on TOP of the game area as the retro overflow "dead zone".</summary>
        public const int DeadZoneHeight = 4;

        private bool on;

        public RetroPower()
            : base("retro", "Retro")
        {
            SetDescription(
                "Toggle tetris mode: blocks fall from the top and can be rotated, and each "
                    + "placement scores a bonus. Use again to switch back. Never runs out.",
                "Tetris modunu aç/kapat: bloklar yukarıdan düşer ve döndürülebilir, her koyuş "
                    + "bonus puan verir. Kapatmak için tekrar kullan. Tükenmez.");
        }

        public override string StatusText
        {
            get { return on ? Loc.Pick("ON", "AÇIK") : Loc.Pick("off", "kapalı"); }
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            RoundEngine round = ctx.Round;
            if (on)
            {
                // Turning OFF is refused while the dead zone still holds cubes (clear it first),
                // and otherwise shrinks the overflow rows back off the top of the board.
                if (round != null && round.DeadZoneOccupied)
                {
                    return false; // no spend, no toggle - retro stays on
                }
                if (round != null && ctx.Rules.DeadZoneRows > 0)
                {
                    round.ReshapeBoard(0, 0, 0, -ctx.Rules.DeadZoneRows);
                }
                ctx.Rules.DeadZoneRows = 0;
                on = false;
            }
            else
            {
                // Turning ON grows an empty dead zone on top of the game area.
                if (round != null && round.ReshapeBoard(0, 0, 0, DeadZoneHeight))
                {
                    ctx.Rules.DeadZoneRows = DeadZoneHeight;
                }
                on = true;
            }
            ctx.Rules.RetroMode = on;
            KeepChargeAfterUse = true; // a toggle: never spends, never needs recharging
            return true;
        }

        /// <summary>Each round is built fresh, so bake the dead zone into the round's board size
        /// here (BEFORE the board exists) when retro is on - taller than normal by DeadZoneHeight,
        /// with those top rows marked dead. Runs early enough that the View never sees a mismatched
        /// board. DeadZoneRows is reset to match, so it always tracks the CURRENT board.</summary>
        public override RoundConfig FilterRoundConfig(SessionContext ctx, RoundConfig config)
        {
            if (!on)
            {
                ctx.Rules.DeadZoneRows = 0;
                return config;
            }
            ctx.Rules.DeadZoneRows = DeadZoneHeight;
            return config.WithBoard(config.BoardWidth, config.BoardHeight + DeadZoneHeight,
                config.ExtraPlayableCells);
        }

        public override void OnRemoved(SessionContext ctx)
        {
            // Selling/removing the toggle must never leave the game stuck in tetris mode.
            if (on)
            {
                ctx.Rules.RetroMode = false;
                ctx.Rules.DeadZoneRows = 0;
                on = false;
            }
        }
    }

    /// <summary>"Batak" - a betting power (was a joker; moved here 2026-07-19). Using it opens a
    /// picker in the UI to bet that the next clean sweep comes within a chosen number of turns;
    /// placing the bet spends the charge. Miss the deadline and the round is lost; make it and
    /// the score earned since the bet is paid again, scaled by how bold the call was. Any clean
    /// sweep resolves the bet AND recharges the power (the standard power economy), so you can
    /// bet again. The bet number comes from the UI, so Run is never taken through TryUse -
    /// GameSession.PlaceBatakBet places the bet and spends the charge.
    ///
    /// PAYOUT CURVE: a bet of N turns is worth MaxMultiplier * (1 - (N-1)/(ZeroAtTurns-1)); "1
    /// turn" pays the most, a bet at/beyond ZeroAtTurns pays nothing, and clearing EARLY pays
    /// pro rata (bet 7, cleared in 3 -> 3/7 of the 7-turn reward).</summary>
    public sealed class BatakPower : Power
    {
        /// <summary>
        /// THE BET TABLE: the bonus a bet of N turns pays, in per cent of the points scored from
        /// the bet to the sweep, when the sweep lands on the LAST turn of the bet. Index 0 is a
        /// 1-turn bet. The bolder the call, the bigger the bonus; past the table's end a bet is
        /// not offered. Sweeping sooner pays pro rata (confirmed rule - see PayoutFor).
        /// BALANCE PLACEHOLDERS.
        /// </summary>
        private static readonly int[] BonusTable =
        {
            500, 340, 230, 160, 110, 75, 50, 35, 25, 15, 10, 5
        };

        /// <summary>The longest bet on offer.</summary>
        public static int MaxBetTurns
        {
            get { return BonusTable.Length; }
        }

        /// <summary>The full bonus (per cent) of a bet of <paramref name="turns"/> turns, or 0
        /// for a length the table does not offer. The one definition - the bet menu lists it and
        /// PayoutFor pays it.</summary>
        public static int BonusPercentFor(int turns)
        {
            return turns >= 1 && turns <= BonusTable.Length ? BonusTable[turns - 1] : 0;
        }

        /// <summary>A payout the sweep earned, waiting for the turn's score to be final - it is
        /// paid in AfterTurnScored, so no multiplier of the turn is applied to it a second time.
        /// Unscaled points.</summary>
        private int pendingPayout;

        /// <summary>Turns bet on, or 0 when no bet is running.</summary>
        public int BetTurns { get; private set; }

        /// <summary>Turns already spent against the active bet.</summary>
        public int TurnsElapsed { get; private set; }

        private int scoreAtBet;

        public BatakPower()
            : base("batak", "Batak")
        {
            SetDescription(
                "Only on an EMPTY board: bet that you will sweep it again within 1 to "
                    + MaxBetTurns + " turns. Make "
                    + "it and you get a bonus on the points scored since the bet - up to +"
                    + BonusTable[0] + "% for a 1-turn call; sweeping early pays a share of it. "
                    + "Miss it and the run is LOST.",
                "Sadece BOŞ tahtada: tahtayı 1-" + MaxBetTurns + " tur içinde yeniden "
                    + "temizleyeceğine bahse gir. Tutturursan "
                    + "bahisten beri kazandığın puana bonus alırsın - 1 turluk bahiste +%"
                    + BonusTable[0] + "'e kadar; erken temizlersen bir payını alırsın. "
                    + "Tutturamazsan oyunu KAYBEDERSİN.");
        }

        public bool HasActiveBet
        {
            get { return BetTurns > 0; }
        }

        public override string StatusText
        {
            get
            {
                return HasActiveBet
                    ? Loc.Pick((BetTurns - TurnsElapsed) + " turns left, +" + BonusPercentFor(BetTurns) + "%",
                        (BetTurns - TurnsElapsed) + " tur kaldı, +%" + BonusPercentFor(BetTurns))
                    : Loc.Pick("empty board only", "sadece boş tahtada");
            }
        }

        /// <summary>Usable (to open the picker) only when no bet is already running, a round is in
        /// progress, and the ARENA IS EMPTY (<see cref="ArenaEmpty"/>) - the bet is always "from a
        /// clean board, back to a clean board". The spent charge also blocks re-betting until a
        /// sweep/new round.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return !HasActiveBet && ctx.Round.Status == RoundStatus.InProgress
                && ArenaEmpty(ctx.Round);
        }

        /// <summary>No cube anywhere in the arena. Only the board's own cells count - a ghost
        /// block's cubes hanging OUTSIDE it (GameBoard.OutsideCubes) are not on the play area.
        /// </summary>
        public static bool ArenaEmpty(RoundEngine round)
        {
            return round != null && round.Board.OccupiedCount == 0;
        }

        /// <summary>Never taken through TryUse - the bet picker calls PlaceBet + spends the
        /// charge via GameSession.PlaceBatakBet, because it needs a number.</summary>
        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            return false;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            ClearBet();
            pendingPayout = 0;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ClearBet();
        }

        /// <summary>Places a bet. Legal only while a round runs and no bet is open.</summary>
        public bool PlaceBet(RoundContext ctx, int turns)
        {
            if (HasActiveBet || turns < 1 || turns > MaxBetTurns
                || ctx.Round.Status != RoundStatus.InProgress || !ArenaEmpty(ctx.Round))
            {
                return false;
            }
            BetTurns = turns;
            TurnsElapsed = 0;
            scoreAtBet = ctx.Round.RoundScore;
            return true;
        }

        /// <summary>Reward for clearing in <paramref name="usedTurns"/> against a bet of
        /// <paramref name="betTurns"/>, applied to the score gained in between: the table's bonus
        /// for that bet, pro rata to the turns used (confirmed rule - bet 7, clear in 3, get 3/7
        /// of it).</summary>
        public int PayoutFor(int betTurns, int usedTurns, int scoreGained)
        {
            int percent = BonusPercentFor(betTurns);
            if (percent <= 0 || scoreGained <= 0 || usedTurns <= 0)
            {
                return 0;
            }
            double share = System.Math.Min(1.0, usedTurns / (double)betTurns);
            return (int)System.Math.Floor(scoreGained * percent / 100.0 * share);
        }

        /// <summary>The bet is WON. The payout is worked out now, while the bet is known, and
        /// PAID once the turn's score is final (AfterTurnScored): paid here it would be a flat
        /// bonus inside a breakdown whose multipliers had already counted the points it is a share
        /// of. It is kept in UNSCALED points, because a flat bonus is scaled when it is added.
        /// </summary>
        public override void AfterCleanSweep(TurnContext turn)
        {
            if (!HasActiveBet)
            {
                return;
            }
            int usedTurns = TurnsElapsed + 1; // this turn closes the window
            int gained = turn.Round.RoundScore + turn.Score.Total - scoreAtBet;
            int payout = PayoutFor(BetTurns, usedTurns, gained);
            int scale = turn.Score.ScoreScale > 0 ? turn.Score.ScoreScale : 1;
            ClearBet();
            pendingPayout += payout / scale;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (pendingPayout > 0)
            {
                int payout = pendingPayout;
                pendingPayout = 0;
                turn.AddFlatScore(payout, DefId);
            }
            if (!HasActiveBet)
            {
                return;
            }
            TurnsElapsed++;
            if (TurnsElapsed < BetTurns)
            {
                return;
            }
            ClearBet();
            // A pending advance offer still wins, exactly like every other same-turn loss.
            turn.Round.DeclareLoss(LossReason.BetFailed);
        }

        private void ClearBet()
        {
            BetTurns = 0;
            TurnsElapsed = 0;
            scoreAtBet = 0;
        }
    }

    /// <summary>
    /// "Kentsel Dönüşüm" - the rescue power. It only comes out at the moment the board has
    /// filled up and nothing in hand fits: the player swaps two whole rows, or two whole
    /// columns, hoping the rearrangement opens a gap their block can use.
    ///
    /// It is deliberately a REARRANGEMENT, not a demolition - no cube is destroyed, so it
    /// scores nothing and can never hand out a clean sweep. Whether it saves the round is up
    /// to the player reading the board correctly; a swap that does not help still spends the
    /// charge and the round ends.
    ///
    /// The engine drives the flow: RoundEngine pauses in AwaitingRescue when this is held,
    /// and PowerInventory calls ResumeAfterRescue once the swap is done.
    /// </summary>
    public sealed class KentselDonusumPower : Power
    {
        public KentselDonusumPower()
            : base("kentsel_donusum", "Kentsel Dönüşüm")
        {
            SetDescription(
                "Swap two rows or two columns of the board, whenever you like. And when the board "
                    + "fills up and nothing fits, it saves the round: the swap opens a gap and you "
                    + "play on. Destroys nothing, scores nothing.",
                "Oyun alanındaki iki satırın ya da iki sütunun yerini istediğin zaman değiştir. "
                    + "Alan dolup koyacak yer kalmazsa da raundu kurtarır: değişim bir boşluk açar "
                    + "ve oyuna devam edersin. Hiçbir küpü yok etmez, puan vermez.");
        }

        /// <summary>Still a rescue: a dead end pauses the round for it.</summary>
        public override bool IsDeadEndRescue
        {
            get { return true; }
        }

        /// <summary>...but no longer ONLY a rescue - it can be used in normal play too.</summary>
        public override bool AlsoUsableInPlay
        {
            get { return true; }
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.LineSwap; }
        }

        /// <summary>Without a pick this reports whether a swap COULD help, which is what the
        /// engine asks before pausing the round; with a pick it validates that pick.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            if (!target.Axis.HasValue)
            {
                // A board with fewer than two lines on either axis has nothing to exchange.
                return ctx.Round.Board.Width >= 2 || ctx.Round.Board.Height >= 2;
            }
            return target.LineA.HasValue && target.LineB.HasValue
                && target.LineA.Value != target.LineB.Value;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (!target.Axis.HasValue || !target.LineA.HasValue || !target.LineB.HasValue)
            {
                return false;
            }
            return ctx.Round.Board.SwapLines(target.Axis.Value, target.LineA.Value,
                target.LineB.Value);
        }
    }
}
