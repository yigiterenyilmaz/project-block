// PURPOSE: The four jokers that pay you for a SHAPE OF PLAY rather than a kind of block -
// symmetry on the board, dynamite left to mature, antimatter annihilating a whole element, and a
// round won without touching a power.
//
// What they have in common: none of them changes a rule. They watch what the turn did and pay for
// it. The one that needs a real rule - Antimadde's "it only goes where it fits exactly" - pushed
// that rule into the engine (BlockCard.AntimatterOf, RoundEngine.CanPlaceCard and the turn
// resolver) and kept only the price here, which is where balance numbers belong.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// "Simetri" - it pays you for leaving the board a mirror image of itself. One axis pays; BOTH
    /// axes pay triple.
    ///
    /// It sleeps for the first turns of a round and, crucially, GOES BACK TO SLEEP after every clean
    /// sweep (confirmed design). That is what stops it being a sweep joker: an empty board is
    /// perfectly symmetric on both axes, so without the reset every sweep would hand out the triple
    /// for free. With it, symmetry is something you have to build again, deliberately, out of a
    /// board you have already cleared once.
    ///
    /// Symmetry is judged on OCCUPANCY (GameBoard.IsMirroredLeftRight / IsMirroredTopBottom): a cell
    /// holds a cube or it does not. Matching the KINDS as well would be nearly impossible to do on
    /// purpose, and this joker is about the silhouette you leave behind.
    /// </summary>
    public sealed class SimetriJoker : Joker
    {
        /// <summary>Turns it sleeps at the start of a round, and again after every clean sweep.
        /// It pays from this turn onward.</summary>
        public int WakesOnTurn = 5;

        /// <summary>Paid for a board mirrored on ONE axis.</summary>
        public int OneAxisBonus = 40;

        /// <summary>What both axes at once are worth, as a multiple of the above.</summary>
        public int BothAxesMultiplier = 3;

        private int turnsSinceReset;
        private int paidThisRound;

        public SimetriJoker()
            : base("simetri", "Simetri")
        {
            SetDescription(
                "Leave the board mirrored across the middle and it pays; mirrored on BOTH axes "
                    + "pays triple. It wakes on the 5th turn - and a clean sweep puts it back to "
                    + "sleep for another 5, so an empty board never counts.",
                "Tahtayı ortadan simetrik bırakırsan puan verir; İKİ eksende birden simetrikse üç "
                    + "katını verir. 5. turda uyanır - ve her temizlik onu 5 tur daha uykuya "
                    + "yatırır, yani boş tahta hiçbir zaman sayılmaz.");
        }

        /// <summary>Turns since the round began or the last sweep, whichever is later.</summary>
        public int TurnsSinceReset
        {
            get { return turnsSinceReset; }
        }

        public bool IsAwake
        {
            get { return turnsSinceReset >= WakesOnTurn; }
        }

        public override string StatusText
        {
            get
            {
                if (!IsAwake)
                {
                    return Loc.Pick("wakes in " + (WakesOnTurn - turnsSinceReset),
                        (WakesOnTurn - turnsSinceReset) + " tur sonra");
                }
                return paidThisRound > 0
                    ? "+" + paidThisRound
                    : Loc.Pick("watching", "bakıyor");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            turnsSinceReset = 0;
            paidThisRound = 0;
        }

        /// <summary>A sweep sends it back to sleep. The board it left behind is empty, which is
        /// symmetric on every axis there is - and paying for that would make this a sweep joker.
        /// </summary>
        public override void AfterCleanSweep(TurnContext turn)
        {
            turnsSinceReset = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            turnsSinceReset++;
            if (!IsAwake || turn.Round == null)
            {
                return;
            }
            // The MAIN board only: "Öteki dünya" opening a second arena must not pay twice for a
            // joker balanced against one.
            GameBoard board = turn.Round.MainBoard;
            bool leftRight = board.IsMirroredLeftRight();
            bool topBottom = board.IsMirroredTopBottom();
            if (!leftRight && !topBottom)
            {
                return;
            }
            int bonus = leftRight && topBottom
                ? OneAxisBonus * BothAxesMultiplier
                : OneAxisBonus;
            paidThisRound += bonus;
            turn.AddFlatScore(bonus, DefId);
        }
    }

    /// <summary>
    /// "Barut tedarikçisi" - dynamite that is left alone gets angrier. Every turn a dynamite block
    /// survives on the board it gains a charge, and when it finally goes up it pays for every one
    /// of them.
    ///
    /// Note the tension with what dynamite already does: a dynamite block detonates the whole board
    /// only when it is placed AND fully exploded on the SAME turn, which is a block with zero
    /// charges. So this joker pulls the other way - leave it standing, feed it turns, and cash it
    /// in later for score instead of a board wipe. Two ways to play the same block.
    ///
    /// Charges are per CARD, not per cube: a block that loses half its cubes keeps what it earned,
    /// and pays it when the rest goes.
    /// </summary>
    public sealed class BarutTedarikcisiJoker : Joker
    {
        /// <summary>Score per charge, per dynamite cube destroyed.</summary>
        public int BonusPerChargePerCube = 3;

        /// <summary>Charges a block can bank. Without a cap a stalling round would print score.</summary>
        public int MaxCharges = 20;

        private readonly Dictionary<int, int> chargesByCard = new Dictionary<int, int>();
        private int paidThisRound;

        public BarutTedarikcisiJoker()
            : base("barut_tedarikcisi", "Barut Tedarikçisi")
        {
            SetDescription(
                "Every turn a dynamite block sits on the board unexploded it gains a charge, and "
                    + "pays for all of them when it finally goes up. Patience is the ammunition.",
                "Tahtadaki bir dinamit bloğu patlamadan durduğu her tur güç kazanır ve sonunda "
                    + "patladığında hepsinin karşılığını öder. Cephane sabırdır.");
        }

        /// <summary>Charges banked across every dynamite block standing right now, for the UI.</summary>
        public int TotalCharges
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<int, int> entry in chargesByCard)
                {
                    total += entry.Value;
                }
                return total;
            }
        }

        public override string StatusText
        {
            get
            {
                int charges = TotalCharges;
                if (charges > 0)
                {
                    return Loc.Pick(charges + " charged", charges + " barut");
                }
                return paidThisRound > 0
                    ? "+" + paidThisRound
                    : Loc.Pick("no dynamite", "dinamit yok");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            chargesByCard.Clear();
            paidThisRound = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            PayForWhatWentUp(turn);
            if (turn.Round != null)
            {
                ChargeWhatSurvived(turn.Round.MainBoard);
            }
        }

        /// <summary>Every dynamite cube destroyed this turn pays its block's charges. Read from the
        /// turn's own destruction log, so it does not matter WHAT killed it - a line, a joker, a
        /// power, a boss: powder is powder.</summary>
        private void PayForWhatWentUp(TurnContext turn)
        {
            IReadOnlyList<DestroyedCube> destroyed = turn.Report.DestroyedCubes;
            int bonus = 0;
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Cube.Kind != CubeKind.Dynamite)
                {
                    continue;
                }
                int charges;
                if (chargesByCard.TryGetValue(destroyed[i].Cube.SourceCardId, out charges))
                {
                    bonus += charges * BonusPerChargePerCube;
                }
            }
            if (bonus <= 0)
            {
                return;
            }
            paidThisRound += bonus;
            turn.AddFlatScore(bonus, DefId);
        }

        /// <summary>Rebuilt from the board every turn rather than tracked by hand: a block that is
        /// gone stops charging by simply not being there, and one that arrived this turn starts at
        /// one. Nothing to forget and nothing to leak.</summary>
        private void ChargeWhatSurvived(GameBoard board)
        {
            var standing = new Dictionary<int, int>();
            foreach (GridPos cell in board.CellsOfKind(CubeKind.Dynamite))
            {
                Cube? cube = board.GetCube(cell);
                if (!cube.HasValue)
                {
                    continue;
                }
                standing[cube.Value.SourceCardId] = 1;
            }
            var next = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> entry in standing)
            {
                int had;
                chargesByCard.TryGetValue(entry.Key, out had);
                next[entry.Key] = had < MaxCharges ? had + 1 : MaxCharges;
            }
            chargesByCard.Clear();
            foreach (KeyValuePair<int, int> entry in next)
            {
                chargesByCard[entry.Key] = entry.Value;
            }
        }
    }

    /// <summary>
    /// "Antimadde" - erasing an element with a NEGATIVE block leaves you its antimatter: a card cut
    /// to the shape of exactly what you erased. Drop that card so it covers nothing but cubes of
    /// that same element and every cube of that element on the board is annihilated at once.
    ///
    /// The card rots in your hand: every turn you hold it the pay-off shrinks, and after five turns
    /// it is gone. So it is a race - find the perfect overlay before the antimatter decays, on a
    /// board that keeps changing under you.
    ///
    /// THE RULE IS THE ENGINE'S, THE PRICE IS THIS JOKER'S. BlockCard.AntimatterOf makes the card
    /// unplaceable anywhere but a perfect fit (RoundEngine.CanPlaceCard) and the turn resolver does
    /// the annihilating; all this class does is mint the card, let it rot, and pay for the blast.
    /// </summary>
    public sealed class AntimaddeJoker : Joker
    {
        /// <summary>Score per annihilated cube, before decay.</summary>
        public int BonusPerCube = 25;

        /// <summary>Turns the card survives in hand. It vanishes at the end of the last one.</summary>
        public int TurnsBeforeDecay = 5;

        /// <summary>What one turn of rot costs, in percent of the pay-off.</summary>
        public int DecayPercentPerTurn = 20;

        private int cardId = -1;
        private int turnsHeld;
        private int paidThisRound;

        public AntimaddeJoker()
            : base("antimadde", "Antimadde")
        {
            SetDescription(
                "When a NEGATIVE block erases element cubes, you get their antimatter: a card cut "
                    + "to the shape of what you erased. Drop it so it covers nothing but cubes of "
                    + "that element and EVERY cube of that element on the board is annihilated. It "
                    + "rots in your hand - the pay-off shrinks every turn and it is gone after 5.",
                "NEGATİF blok elementli küpleri sildiğinde antimaddesi eline gelir: sildiğin "
                    + "şeklin aynısı bir kart. Onu tahtada o elementten başka hiçbir şeye "
                    + "değmeyecek şekilde CUK oturtursan tahtadaki o elementin TÜM küpleri havaya "
                    + "uçar. Elinde bozulur - her tur getirisi azalır, 5 turda yok olur.");
        }

        /// <summary>True while an antimatter card is waiting to be used.</summary>
        public bool HasCard
        {
            get { return cardId >= 0; }
        }

        /// <summary>Turns the current card has been held.</summary>
        public int TurnsHeld
        {
            get { return turnsHeld; }
        }

        /// <summary>What the blast is worth per cube right now, after the rot.</summary>
        public int CurrentBonusPerCube
        {
            get
            {
                int left = 100 - DecayPercentPerTurn * turnsHeld;
                if (left < 0)
                {
                    left = 0;
                }
                return BonusPerCube * left / 100;
            }
        }

        public override string StatusText
        {
            get
            {
                if (HasCard)
                {
                    return Loc.Pick("decays in ", "bozulmaya ")
                        + (TurnsBeforeDecay - turnsHeld) + Loc.Pick("", " tur");
                }
                return paidThisRound > 0
                    ? "+" + paidThisRound
                    : Loc.Pick("waiting", "bekliyor");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // The card is a ROUND thing: a bonus card does not survive the round it was made in,
            // so the bookkeeping must not either.
            cardId = -1;
            turnsHeld = 0;
            paidThisRound = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            PayForAnAnnihilation(turn);
            RotTheCardWeHold(turn);
            MintFromWhatTheNegativeErased(turn);
        }

        /// <summary>The blast has already happened - the engine annihilated the cubes when the card
        /// went down. This is only the bill for it, at whatever the rot left of the price.</summary>
        private void PayForAnAnnihilation(TurnContext turn)
        {
            TurnReport report = turn.Report;
            if (!report.AnnihilatedKind.HasValue || report.Card == null
                || report.Card.Id != cardId)
            {
                return;
            }
            int cubes = report.ExtraExplodedCells.Count;
            int bonus = cubes * CurrentBonusPerCube;
            cardId = -1;
            turnsHeld = 0;
            if (bonus <= 0)
            {
                return;
            }
            paidThisRound += bonus;
            turn.AddFlatScore(bonus, DefId);
        }

        /// <summary>One more turn of rot, and the card is taken off the table when it is spent.
        /// Also drops the bookkeeping if the card left the hand some other way.</summary>
        private void RotTheCardWeHold(TurnContext turn)
        {
            if (!HasCard)
            {
                return;
            }
            if (!turn.Round.BonusHandHolds(cardId))
            {
                cardId = -1; // played, expired or taken by something else
                turnsHeld = 0;
                return;
            }
            turnsHeld++;
            if (turnsHeld >= TurnsBeforeDecay)
            {
                turn.Round.RemoveBonusCard(cardId);
                cardId = -1;
                turnsHeld = 0;
            }
        }

        /// <summary>
        /// A negative block just erased something. Every erased cube that carried an ELEMENT is
        /// antimatter waiting to happen; the card takes the shape of exactly those cells (confirmed
        /// design), for the kind that lost the most of them.
        ///
        /// One card per erasure, not one per kind: the design says "the antimatter card of that
        /// block" - singular - and a negative block that clips three different elements should be
        /// a choice about where you aimed it, not a windfall of three cards.
        /// </summary>
        private void MintFromWhatTheNegativeErased(TurnContext turn)
        {
            if (HasCard || turn.Round == null || turn.Report.Card == null
                || !turn.Round.CardHasElement(turn.Report.Card, BlockElement.Negative))
            {
                return;
            }
            var byKind = new Dictionary<CubeKind, List<GridPos>>();
            IReadOnlyList<DestroyedCube> erased = turn.Report.DestroyedCubes;
            for (int i = 0; i < erased.Count; i++)
            {
                CubeKind kind = erased[i].Cube.Kind;
                if (kind == CubeKind.Normal)
                {
                    continue; // a plain cube has no element, so it has no antimatter
                }
                List<GridPos> cells;
                if (!byKind.TryGetValue(kind, out cells))
                {
                    cells = new List<GridPos>();
                    byKind[kind] = cells;
                }
                cells.Add(erased[i].Pos);
            }
            CubeKind best = CubeKind.Normal;
            int most = 0;
            foreach (KeyValuePair<CubeKind, List<GridPos>> entry in byKind)
            {
                if (entry.Value.Count > most)
                {
                    most = entry.Value.Count;
                    best = entry.Key;
                }
            }
            if (most <= 0)
            {
                return;
            }
            BlockCard card = turn.Session.CreateCard(BlockShape.FromCells(byKind[best]), null);
            card.AntimatterOf = best;
            // ExpireFromRound: antimatter is not deck stock, and playing it consumes it.
            turn.Round.AddBonusCard(card, BonusPlayOutcome.ExpireFromRound);
            cardId = card.Id;
            turnsHeld = 0;
        }

    }

    /// <summary>
    /// "Eforsuz galibiyet" - it pays you for winning a round without touching a single power. The
    /// bonus lands as you walk into the market, which is the moment the round is genuinely over.
    ///
    /// And it pays DOUBLE for the hardest version of that: going into overtime and coming out of it
    /// alive, still without a power. Using one anywhere in the round - including in overtime -
    /// forfeits everything.
    ///
    /// "Going into overtime" is RoundEngine.ContinueCount, the only thing that counts the player
    /// DECLINING the advance offer, exactly as "Savunmacı" reads it.
    /// </summary>
    public sealed class EforsuzGalibiyetJoker : Joker
    {
        /// <summary>Paid on entering the market after a power-free round.</summary>
        public int Bonus = 60;

        /// <summary>What a power-free OVERTIME is worth, as a multiple of the above.</summary>
        public int OvertimeMultiplier = 2;

        private bool usedAPowerThisRound;
        private bool wentToOvertime;
        private int lastPaid;

        public EforsuzGalibiyetJoker()
            : base("eforsuz_galibiyet", "Eforsuz Galibiyet")
        {
            SetDescription(
                "Finish a round without using a single power and the market pays you a bonus as "
                    + "you walk in. Do it on a round you took into OVERTIME and came out of alive, "
                    + "and it pays double. One power anywhere in the round forfeits it.",
                "Bir raundu tek bir güç kullanmadan bitir, markete girerken bonus alırsın. Aynısını "
                    + "UZATMAYA gidip sağ çıktığın bir raunttta yaparsan iki katını verir. Raundun "
                    + "herhangi bir yerinde bir güç kullanmak hakkını yakar.");
        }

        /// <summary>True while the round is still clean of powers.</summary>
        public bool StillClean
        {
            get { return !usedAPowerThisRound; }
        }

        public override string StatusText
        {
            get
            {
                if (usedAPowerThisRound)
                {
                    return Loc.Pick("forfeited", "yakıldı");
                }
                return wentToOvertime
                    ? Loc.Pick("clean (x" + OvertimeMultiplier + ")", "temiz (x" + OvertimeMultiplier + ")")
                    : Loc.Pick("clean", "temiz");
            }
        }

        /// <summary>What it paid on the last market entry, for the UI.</summary>
        public int LastPaid
        {
            get { return lastPaid; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            usedAPowerThisRound = false;
            wentToOvertime = false;
        }

        public override void OnPowerUsed(RoundContext ctx, string powerId)
        {
            usedAPowerThisRound = true;
        }

        /// <summary>Read at the END of the round rather than watched turn by turn: ContinueCount is
        /// the count of declined offers, and only the final one matters.</summary>
        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            if (ctx.Round != null)
            {
                wentToOvertime = ctx.Round.ContinueCount > 0;
            }
        }

        /// <summary>The market is where it pays: a round is only truly finished once you are
        /// standing in the shop, and there is no market after a round you lost.</summary>
        public override void OnMarketEntered(SessionContext ctx)
        {
            lastPaid = 0;
            if (usedAPowerThisRound)
            {
                return;
            }
            lastPaid = wentToOvertime ? Bonus * OvertimeMultiplier : Bonus;
            // GrantCurrency, not AddCurrency: this is money from nowhere, and the ledger has to
            // be able to tell it apart from a sale.
            ctx.Session.GrantCurrency((long)lastPaid * ctx.Session.Config.Scoring.ScoreScale);
        }
    }
}
