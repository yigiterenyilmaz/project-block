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

    /// <summary>"Halüsinasyon" - a power with no fixed identity: it appears as a random power
    /// and, whenever used, instantly recharges and morphs into a DIFFERENT random one. It only
    /// ever becomes an instant, self-contained power (one clean Run) - never a legendary /
    /// reality-bending one, never one that needs special routing (Olta's marking, Hileli zar's
    /// market picker, Eko's arm-and-replay), and never one that carries state across turns
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
                "Appears as a random power. Using it never spends it - it instantly refills and "
                    + "morphs into a different random power. Never becomes a legendary power.",
                "Rastgele bir güç olarak görünür. Kullanmak onu tüketmez - anında dolar ve başka "
                    + "bir rastgele güce dönüşür. Asla efsanevi bir güce dönüşmez.");
            BaseSellValue = 55;
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
            // The whole point: the use is "free" - it keeps its charge and becomes something new.
            KeepChargeAfterUse = true;
            Reroll(ctx.Rng);
            return true;
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
            BaseSellValue = 55;
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
        private bool on;

        public RetroPower()
            : base("retro", "Retro")
        {
            SetDescription(
                "Toggle tetris mode: blocks fall from the top and can be rotated, and each "
                    + "placement scores a bonus. Use again to switch back. Never runs out.",
                "Tetris modunu aç/kapat: bloklar yukarıdan düşer ve döndürülebilir, her koyuş "
                    + "bonus puan verir. Kapatmak için tekrar kullan. Tükenmez.");
            BaseSellValue = 50;
        }

        public override string StatusText
        {
            get { return on ? Loc.Pick("ON", "AÇIK") : Loc.Pick("off", "kapalı"); }
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            on = !on;
            ctx.Rules.RetroMode = on;
            KeepChargeAfterUse = true; // a toggle: never spends, never needs recharging
            return true;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            // Selling/removing the toggle must never leave the game stuck in tetris mode.
            if (on)
            {
                ctx.Rules.RetroMode = false;
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
        /// <summary>Multiplier for the boldest possible call (1 turn).</summary>
        public double MaxMultiplier = 3.0;

        /// <summary>Bets this long or longer are worth nothing.</summary>
        public int ZeroAtTurns = 100;

        /// <summary>Turns bet on, or 0 when no bet is running.</summary>
        public int BetTurns { get; private set; }

        /// <summary>Turns already spent against the active bet.</summary>
        public int TurnsElapsed { get; private set; }

        private int scoreAtBet;

        public BatakPower()
            : base("batak", "Batak")
        {
            SetDescription(
                "Bet that you will sweep the board within a chosen number of turns. Miss it and "
                    + "the round ends; make it and the payout multiplies.",
                "İstersen 'şu kadar turda temizlerim' diye bahse girersin. Tutturamazsan raunt "
                    + "biter, tutturursan aradaki puanı katlayarak alırsın.");
            BaseSellValue = 65;
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
                    ? Loc.Pick("bet: " + (BetTurns - TurnsElapsed) + " turns",
                        "bahis " + (BetTurns - TurnsElapsed) + " tur")
                    : Loc.Pick("no bet", "bahis yok");
            }
        }

        /// <summary>Usable (to open the picker) only when no bet is already running and a round
        /// is in progress; the spent charge also blocks re-betting until a sweep/new round.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return !HasActiveBet && ctx.Round.Status == RoundStatus.InProgress;
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
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ClearBet();
        }

        /// <summary>Places a bet. Legal only while a round runs and no bet is open.</summary>
        public bool PlaceBet(RoundContext ctx, int turns)
        {
            if (HasActiveBet || turns < 1 || ctx.Round.Status != RoundStatus.InProgress)
            {
                return false;
            }
            BetTurns = turns;
            TurnsElapsed = 0;
            scoreAtBet = ctx.Round.RoundScore;
            return true;
        }

        /// <summary>Reward for clearing in <paramref name="usedTurns"/> against a bet of
        /// <paramref name="betTurns"/>, applied to the score gained in between.</summary>
        public int PayoutFor(int betTurns, int usedTurns, int scoreGained)
        {
            if (betTurns <= 0 || scoreGained <= 0)
            {
                return 0;
            }
            double boldness = ZeroAtTurns > 1
                ? 1.0 - (betTurns - 1) / (double)(ZeroAtTurns - 1)
                : 1.0;
            if (boldness <= 0.0)
            {
                return 0;
            }
            double earliness = usedTurns / (double)betTurns;
            return (int)System.Math.Floor(scoreGained * MaxMultiplier * boldness * earliness);
        }

        public override void AfterCleanSweep(TurnContext turn)
        {
            if (!HasActiveBet)
            {
                return;
            }
            int usedTurns = TurnsElapsed + 1; // this turn closes the window
            int gained = turn.Round.RoundScore + turn.Score.Total - scoreAtBet;
            int payout = PayoutFor(BetTurns, usedTurns, gained);
            ClearBet();
            if (payout > 0)
            {
                turn.AddFlatScore(payout, DefId);
            }
        }

        public override void AfterTurnScored(TurnContext turn)
        {
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
}
