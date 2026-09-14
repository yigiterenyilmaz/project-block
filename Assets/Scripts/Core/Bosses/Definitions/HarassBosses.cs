// PURPOSE: The three bosses that interfere with the player's turn itself rather than with
// scoring - "Alıkoyma" holds a card back, "Mapus" seals a cell of the board, "Feda" makes a
// bonus card cost the whole hand. All three act from the end-of-turn hook, which is BEFORE
// the dead-end check, so any of them can genuinely finish a round off.
//
// Every one of them re-rolls its victim from ctx.Rng, so a replay of the same seed harasses
// the player in exactly the same order.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Alıkoyma" - every turn one random held card is held back (frozen) for the
    /// next turn. Never when the hand is down to a single card, so it can annoy but never
    /// takes the player's last option away.</summary>
    public sealed class AlikoymaBoss : BossRound
    {
        /// <summary>Turns a seized card stays unplayable. One turn = "the next turn only",
        /// because the engine ticks freezes down at the end of every resolved turn.</summary>
        public int SeizeTurns = 1;

        private int seizedCardId;
        private bool holding;

        public AlikoymaBoss()
            : base("alikoyma", "Alıkoyma")
        {
            SetDescription(
                "Every turn it seizes a random card in your hand - you cannot play it on your "
                    + "next turn. It never seizes your last card.",
                "Her tur elindeki rastgele bir kartı alıkoyar - sonraki turunda onu "
                    + "oynayamazsın. Elinde tek kart kaldıysa dokunmaz.");
        }

        /// <summary>The card being held right now, for the UI. 0 when nothing is held.</summary>
        public int SeizedCardId
        {
            get { return holding ? seizedCardId : 0; }
        }

        public override string StatusText
        {
            get { return holding ? Loc.Pick("1 card held", "1 kart tutuldu") : null; }
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Seize(turn.Round, turn.Rng);
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // Bite immediately: the opening hand is already dealt, so the very first turn is
            // played one card short like every other turn.
            Seize(ctx.Round, ctx.Rng);
        }

        private void Seize(RoundEngine round, IRandomSource rng)
        {
            holding = false;
            if (round == null || round.Hand.Count <= 1)
            {
                return; // a single card is left alone - see the class docs
            }
            int index = rng.NextInt(0, round.Hand.Count);
            int cardId = round.Hand[index].Id;
            if (round.FreezeHandCard(cardId, SeizeTurns))
            {
                seizedCardId = cardId;
                holding = true;
            }
        }
    }

    /// <summary>
    /// "Mapus" - IT SEALS WHERE IT HURTS. Every turn it reads the board and puts its one seal on
    /// the empty cell whose row and column are CLOSEST TO COMPLETION: nothing may be placed
    /// there, and because a sealed cell still reads as an empty cell of its row and column, the
    /// line through it cannot be completed either. Sealing the last gap of a row does not just
    /// cost a square - it takes the row away.
    ///
    /// IT DOES NOT WANDER. A random cell per turn was a tax, not an antagonist: it almost never
    /// landed anywhere the player cared about, and when it did, it left again immediately. The
    /// seal now STAYS while it is still the most dangerous cell on the board, so denying a row
    /// is something the player has to play around rather than wait out.
    ///
    /// TWO GUARDS, and both are load-bearing:
    ///   - it may not hold ONE cell for more than MaxTurnsOnOneCell turns running, and the cell
    ///     it releases sits out that one retarget. Without this the seal parks on a row's last
    ///     gap for ever - the player fills the row's other cells, the row can never explode, the
    ///     cubes stand there for the rest of the round, and the round is lost to something with
    ///     no counterplay in it. A boss may be brutal; it may not be unanswerable.
    ///   - it is only STICKY while it is actually denying something. With nothing near completion
    ///     every cell is worth the same, and there it goes back to wandering - a seal that sat
    ///     still on an empty board would just be a dead square.
    /// </summary>
    public sealed class MapusBoss : BossRound
    {
        /// <summary>Empty cells the board must still have for a seal to be laid, so the very
        /// last hole is never the one taken away.</summary>
        public int MinFreeCells = 2;

        /// <summary>How many turns running the seal may hold one cell. Past this it MUST move,
        /// and the cell it leaves sits out that retarget - which is the window the player gets to
        /// finish the line it was denying. Zero switches the cap off entirely, and with it the
        /// only thing stopping a permanently dead row.</summary>
        public int MaxTurnsOnOneCell = 3;

        /// <summary>How near a line has to be to completion before it counts as a threat at all.
        /// Beyond this the seal must not be dragged around by a row nobody is close to
        /// finishing.</summary>
        public int ThreatWindow = 3;

        /// <summary>What a line one cube from completion is worth. Halved for two away, thirded
        /// for three - so the LAST gap of a row outweighs anything further off, and a cell that
        /// is the last gap of a row AND of a column outweighs everything.</summary>
        public int DangerBase = 60;

        private GridPos sealedCell;
        private bool hasSeal;

        /// <summary>How many retargets running the seal has held its current cell.</summary>
        private int turnsOnCell;

        private readonly List<GridPos> freeCells = new List<GridPos>();

        /// <summary>The empty REQUIRED cells, and what each one is worth to deny. Rebuilt from
        /// scratch every retarget: the board changes under it every turn.</summary>
        private readonly List<GridPos> candidates = new List<GridPos>();

        private readonly List<int> scores = new List<int>();

        private readonly List<GridPos> best = new List<GridPos>();

        /// <summary>WHAT THE SEAL DID THIS TURN, for the View - moved or held, released by the cap
        /// or not, and how close the lines it is sitting on are to completion. Reporting only, and
        /// [NotSaved] because it is rebuilt by the next retarget and means nothing across a load.
        /// </summary>
        [field: NotSaved]
        public MapusSealVisuals LastSeal { get; private set; }

        public MapusBoss()
            : base("mapus", "Mapus")
        {
            SetDescription(
                "Every turn it seals the empty cell whose row and column are closest to "
                    + "completion - nothing can be placed there, and the row and column through "
                    + "it cannot be completed either. The seal stays put while that is still the "
                    + "most dangerous cell on the board, but never for more than three turns "
                    + "running.",
                "Her tur, satırı ve sütunu tamamlanmaya EN YAKIN olan boş hücreyi kapatır - "
                    + "oraya hiçbir şey koyulamaz, o hücreden geçen satır ve sütun da "
                    + "tamamlanamaz. Tahtanın en tehlikeli yeri orası olduğu sürece mühür yerinde "
                    + "kalır, ama üst üste üç turdan fazla değil.");
        }

        /// <summary>The sealed cell, for the UI to mark. Meaningless when HasSeal is false.</summary>
        public GridPos SealedCell
        {
            get { return sealedCell; }
        }

        public bool HasSeal
        {
            get { return hasSeal; }
        }

        public override string StatusText
        {
            get
            {
                if (!hasSeal)
                {
                    return null;
                }
                string where = sealedCell.X + "," + sealedCell.Y;
                // How long it can still hold this cell, because "it will move next turn" is
                // something the player has to be able to plan around.
                return MaxTurnsOnOneCell > 0
                    ? Loc.Pick("sealed ", "kapalı ") + where
                        + " (" + turnsOnCell + "/" + MaxTurnsOnOneCell + ")"
                    : Loc.Pick("sealed ", "kapalı ") + where;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            turnsOnCell = 0;
            hasSeal = false;
            Retarget(ctx.Round, ctx.Rng);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Retarget(turn.Round, turn.Rng);
        }

        /// <summary>
        /// Reads the board and puts the seal where it denies the most. The old seal is always
        /// lifted first, so exactly one cell is ever sealed by this boss - even when the answer
        /// is the same cell as last turn.
        /// </summary>
        private void Retarget(RoundEngine round, IRandomSource rng)
        {
            if (round == null)
            {
                return;
            }
            // Board mutations go through the engine, so the seal and the no-playable-move check
            // can never disagree.
            round.ClearBoardSeals();
            GridPos? chosen = Choose(round.Board, rng);
            if (chosen.HasValue)
            {
                round.SealBoardCell(chosen.Value);
            }
        }

        /// <summary>
        /// THE SAME TARGETING, against a board of the caller's own and with no engine to route the
        /// seal through - what the ANIMATION LAB drives. It matters that this is not a second
        /// implementation: a lab that picked its own cell would be a drawing of the boss rather
        /// than the boss, and the whole point of the Mapus scenes is that the cell they seal is the
        /// cell the rules would seal. Returns the report the View plays.
        /// </summary>
        public MapusSealVisuals RetargetOn(GameBoard board, IRandomSource rng)
        {
            if (board == null)
            {
                return LastSeal;
            }
            board.ClearSeals();
            GridPos? chosen = Choose(board, rng);
            if (chosen.HasValue)
            {
                board.SealCell(chosen.Value);
            }
            return LastSeal;
        }

        /// <summary>
        /// Picks the cell that denies the most, and writes the report. The caller applies the seal
        /// - through the engine in a round, straight to the board in the lab.
        /// </summary>
        private GridPos? Choose(GameBoard board, IRandomSource rng)
        {
            if (LastSeal == null)
            {
                LastSeal = new MapusSealVisuals();
            }

            GridPos previous = sealedCell;
            bool hadSeal = hasSeal;
            hasSeal = false;
            // THE CAP. Held long enough, the seal has to let go - and the cell it releases is
            // barred from this one retarget, so the line it was denying really does open up.
            bool mustMove = hadSeal && MaxTurnsOnOneCell > 0 && turnsOnCell >= MaxTurnsOnOneCell;

            bool released = mustMove;

            Collect(board);
            if (freeCells.Count < MinFreeCells)
            {
                turnsOnCell = 0;
                Report(board, previous, hadSeal, released);
                return null; // never take the last hole away
            }

            int bestScore = int.MinValue;
            best.Clear();
            int previousScore = int.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                GridPos cell = candidates[i];
                if (hadSeal && cell.X == previous.X && cell.Y == previous.Y)
                {
                    previousScore = scores[i];
                    if (mustMove)
                    {
                        continue; // barred for this retarget
                    }
                }
                if (scores[i] > bestScore)
                {
                    bestScore = scores[i];
                    best.Clear();
                }
                if (scores[i] == bestScore)
                {
                    best.Add(cell);
                }
            }
            if (best.Count == 0)
            {
                turnsOnCell = 0;
                Report(board, previous, hadSeal, released);
                return null; // the barred cell was the only one left; the board breathes this turn
            }

            // STICKY ONLY WHILE IT IS DENYING SOMETHING. A seal sitting still on a board with
            // nothing near completion is just a dead square, so with no threat anywhere it goes
            // back to wandering.
            if (!mustMove && previousScore > 0 && previousScore >= bestScore)
            {
                sealedCell = previous;
                turnsOnCell++;
            }
            else
            {
                sealedCell = best[rng.NextInt(0, best.Count)];
                turnsOnCell = 1;
            }
            hasSeal = true;
            Report(board, previous, hadSeal, released);
            return sealedCell;
        }

        /// <summary>
        /// Writes down what just happened, for the View. Everything in it the rules already knew;
        /// the point is that the View never has to work any of it out - least of all "is this line
        /// being held by exactly this cell", which is the explosion rule's own question and must
        /// have exactly one answer in the codebase.
        /// </summary>
        private void Report(GameBoard board, GridPos previous, bool hadSeal, bool released)
        {
            if (LastSeal == null)
            {
                LastSeal = new MapusSealVisuals();
            }
            LastSeal.Clear();
            LastSeal.PreviousCell = previous;
            LastSeal.HadPrevious = hadSeal;
            LastSeal.Cell = sealedCell;
            LastSeal.HasSeal = hasSeal;
            LastSeal.MaxTurns = MaxTurnsOnOneCell;
            LastSeal.TurnsHeld = turnsOnCell;
            LastSeal.Released = released;
            LastSeal.Moved = hasSeal
                && (!hadSeal || previous.X != sealedCell.X || previous.Y != sealedCell.Y);
            if (!hasSeal)
            {
                return;
            }
            LastSeal.RowGaps = board.RowGapCount(sealedCell.Y);
            LastSeal.ColumnGaps = board.ColumnGapCount(sealedCell.X);
            // ONE gap left and the seal is standing in it: nothing else is holding that line up.
            LastSeal.RowHeldByTheSealAlone = LastSeal.RowGaps == 1;
            LastSeal.ColumnHeldByTheSealAlone = LastSeal.ColumnGaps == 1;
        }

        /// <summary>
        /// The empty cells, and what each one is worth to deny. A candidate is a REQUIRED empty
        /// cell: sealing bonus ground ("Tılsım") denies no line at all, because a line never
        /// waits for it. With no required cell free the boss falls back to any empty one, so it
        /// is never simply idle.
        /// </summary>
        private void Collect(GameBoard board)
        {
            freeCells.Clear();
            candidates.Clear();
            scores.Clear();
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var pos = new GridPos(x, y);
                    if (!board.IsInside(pos) || board.GetCube(pos).HasValue)
                    {
                        continue;
                    }
                    freeCells.Add(pos);
                    if (board.IsOptional(pos))
                    {
                        continue;
                    }
                    candidates.Add(pos);
                    scores.Add(Danger(board.RowGapCount(y)) + Danger(board.ColumnGapCount(x)));
                }
            }
            if (candidates.Count == 0)
            {
                for (int i = 0; i < freeCells.Count; i++)
                {
                    candidates.Add(freeCells[i]);
                    scores.Add(0);
                }
            }
        }

        /// <summary>What a line still needing <paramref name="gaps"/> cubes is worth to deny.
        /// One away dominates two, which dominates three; past ThreatWindow it is not a threat
        /// and must not drag the seal around. A dead line comes in as -1 and is worth nothing.
        /// </summary>
        private int Danger(int gaps)
        {
            return gaps <= 0 || gaps > ThreatWindow ? 0 : DangerBase / gaps;
        }
    }

    /// <summary>"Feda" - a bonus card is a sacrifice: playing one throws the rest of the hand
    /// into the discard. A fresh hand is dealt afterwards, so the cost is the cards you were
    /// holding (and the deck they drain), not the round.</summary>
    public sealed class FedaBoss : BossRound
    {
        private int sacrifices;

        public FedaBoss()
            : base("feda", "Feda")
        {
            SetDescription(
                "Playing a bonus card also throws your whole hand into the discard. A new hand "
                    + "is dealt in its place.",
                "Bonus kart oynamak tüm elini de ıskartaya atar. Yerine yeni bir el çekilir.");
        }

        public override string StatusText
        {
            get
            {
                return sacrifices > 0
                    ? sacrifices + Loc.Pick(" sacrificed", " feda")
                    : null;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            sacrifices = 0;
        }

        public override void OnBonusCardPlayed(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            if (round.Hand.Count == 0)
            {
                return;
            }
            sacrifices++;
            // Straight through the engine's own primitives, so the discard, the draw rules and
            // the deck-out loss all behave exactly as they do for any other hand churn.
            round.DiscardWholeHand();
            round.RefillHandToSize();
        }
    }

    /// <summary>
    /// "Çıkmaz" - the round played backwards. Running out of room WINS it; emptying the board
    /// or reaching the score threshold LOSES it.
    ///
    /// So the whole round is an exercise in playing badly on purpose: fill the arena, clear as
    /// little as you can get away with, and above all do not score well. Lines may still be
    /// cleared - that is often the only way to keep going - but a clear that empties the board
    /// is fatal, and so is creeping over the bar.
    ///
    /// Two rulings the engine carries (RoundEngine.RoundOutcomeInverted):
    ///  - the AUTOMATIC dead-end rescue is skipped, because a joker firing on its own would take
    ///    a win the player never chose to give up. The OFFERED rescue power still appears -
    ///    declining it is the win, which makes the offer a real decision instead of a formality;
    ///  - if a turn both dead-ends AND breaks a rule, the LOSS wins. That falls out of the
    ///    engine's own step order (threshold and sweep are settled before the dead-end check)
    ///    and it is the right way round: a careless last move should still kill you.
    /// </summary>
    public sealed class CikmazBoss : BossRound
    {
        public CikmazBoss()
            : base("cikmaz", "Çıkmaz")
        {
            SetDescription(
                "The round is upside down: you WIN it by running out of room, and you LOSE it by "
                    + "clearing the board or reaching the score threshold. Play badly on purpose.",
                "Raunt tersine döner: yer kalmayınca KAZANIRSIN, tahtayı temizlersen ya da puan "
                    + "eşiğine ulaşırsan KAYBEDERSİN. Bilerek kötü oyna.");
        }

        public override bool InvertsRoundOutcome
        {
            get { return true; }
        }

        public override string StatusText
        {
            get { return Loc.Pick("fill up to win", "dolduran kazanır"); }
        }
    }

    /// <summary>
    /// "Alzheimer" - the board keeps losing its memory. Every turn it forgets the card played
    /// MemoryTurns ago, and whatever is left of that card is lifted off the arena.
    ///
    /// "Whatever is left" is the point: the block does NOT have to be intact. A four-cube card
    /// that has already had three of its cubes blown out still loses the fourth, exactly as if
    /// it had never been laid.
    ///
    /// FORGETTING IS NOT DESTRUCTION. It pays nothing, counts toward no sweep and feeds no
    /// tally - and nothing survives it, obsidian and gold and a Parazit host included, because
    /// the board is not breaking those cubes, it is ceasing to remember them. That last part is
    /// the boss's one gift: a stone you could never shift will eventually be forgotten.
    ///
    /// The memory is per WORLD-agnostic card id, so a card laid in the mirror world ("Öteki
    /// dünya") is forgotten there too.
    /// </summary>
    public sealed class AlzheimerBoss : BossRound
    {
        /// <summary>How many turns a card stays remembered.</summary>
        public int MemoryTurns = 5;

        /// <summary>Card played on each turn, main world and mirror, indexed by turn - 1.
        /// -1 where a world played nothing that turn.</summary>
        private readonly List<int> mainCardByTurn = new List<int>();
        private readonly List<int> mirrorCardByTurn = new List<int>();

        private int cellsForgotten;

        public AlzheimerBoss()
            : base("alzheimer", "Alzheimer")
        {
            SetDescription(
                "Every turn the board forgets the card you played 5 turns ago: whatever is left "
                    + "of it is lifted off, intact or not. Nothing survives being forgotten - "
                    + "not even obsidian or gold.",
                "Her tur, 5 tur önce oynadığın kart unutulur: o karttan geriye ne kaldıysa "
                    + "alandan kalkar, bütün olması gerekmez. Unutulmaya hiçbir şey direnemez - "
                    + "obsidyen ve altın bile.");
        }

        /// <summary>Cells forgotten so far this round, for the UI.</summary>
        public int CellsForgotten
        {
            get { return cellsForgotten; }
        }

        public override string StatusText
        {
            get
            {
                return cellsForgotten > 0
                    ? cellsForgotten + Loc.Pick(" forgotten", " unutuldu")
                    : Loc.Pick("remembering", "hatırlıyor");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            mainCardByTurn.Clear();
            mirrorCardByTurn.Clear();
            cellsForgotten = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            // Remember this turn, padding for any turn this boss did not see, so the list index
            // always is the turn number - 1 and the look-back can never slip.
            int turnNumber = turn.Round.TurnNumber;
            while (mainCardByTurn.Count < turnNumber)
            {
                mainCardByTurn.Add(-1);
                mirrorCardByTurn.Add(-1);
            }
            mainCardByTurn[turnNumber - 1] = turn.Report.Card != null ? turn.Report.Card.Id : -1;
            mirrorCardByTurn[turnNumber - 1] =
                turn.Report.MirrorCard != null ? turn.Report.MirrorCard.Id : -1;

            int forgetTurn = turnNumber - MemoryTurns;
            if (forgetTurn < 1)
            {
                return; // nothing is old enough to have slipped the board's mind yet
            }
            Forget(turn, mainCardByTurn[forgetTurn - 1]);
            Forget(turn, mirrorCardByTurn[forgetTurn - 1]);
        }

        private void Forget(TurnContext turn, int cardId)
        {
            if (cardId < 0)
            {
                return;
            }
            cellsForgotten += turn.Round.ForgetCard(cardId).Count;
        }
    }

    /// <summary>
    /// "Yürüyen merdiven" - the arena is a moving staircase. At the end of every turn every row
    /// rides up one, the top row is carried off the board, and a fresh empty row arrives at the
    /// bottom.
    ///
    /// It is not destruction: the board is carrying cubes away rather than breaking them, so the
    /// ride pays nothing, counts toward no clean sweep and feeds no tally, and nothing resists it.
    ///
    /// A row keeps its contents while it moves, so the escalator can never complete a line. What
    /// it does is far worse: everything you build drifts towards the exit, and the space you keep
    /// getting back arrives at the BOTTOM, where a tall block cannot use it.
    ///
    /// It runs from the end-of-turn hook, which is before the threshold and dead-end checks, so
    /// the ride can genuinely decide the round either way - it can carry off the block that was
    /// about to lose you the round, or take the row you were one cube from clearing.
    /// </summary>
    public sealed class YuruyenMerdivenBoss : BossRound
    {
        private int cellsCarriedOff;

        public YuruyenMerdivenBoss()
            : base("yuruyen_merdiven", "Yürüyen Merdiven")
        {
            SetDescription(
                "At the end of every turn the whole board rides up one row. The top row is "
                    + "carried off and a fresh empty row arrives at the bottom.",
                "Her tur sonunda bütün oyun alanı bir satır yukarı kayar. En üstteki satır "
                    + "alandan çıkar, en alta boş bir satır gelir.");
        }

        /// <summary>Cells carried off the top so far this round, for the UI.</summary>
        public int CellsCarriedOff
        {
            get { return cellsCarriedOff; }
        }

        public override string StatusText
        {
            get
            {
                return cellsCarriedOff > 0
                    ? cellsCarriedOff + Loc.Pick(" carried off", " taşındı")
                    : Loc.Pick("rising", "yükseliyor");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            cellsCarriedOff = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            cellsCarriedOff += turn.Round.EscalateBoards().Count;
        }
    }

    /// <summary>
    /// "Alacakaranlık" - the arena goes dark and the player plays blind. What they built is
    /// still there, still scores, still blocks: they simply cannot see it. Not the cubes, not
    /// which cells are free - every cell in the arena is painted the same dead square, so the
    /// board says NOTHING. A dark board you can still read is not a dark board.
    ///
    /// Light comes from two places, and the difference between them is the round. Setting a
    /// block DOWN lights what it touches, faintly and one cell out: your own hand disturbing the
    /// dark, enough to confirm what you just did and hint at what it landed against. An
    /// EXPLOSION lights far more and far brighter, so making something happen is still the only
    /// way to actually see the board - and the more you clear, the more you learn. Both fade and
    /// the dark closes back over them.
    ///
    /// Two numbers make that a bargain rather than a punishment. The bar is cut to
    /// ThresholdFactor of the round's own, because a blind round asks for less; and a placement
    /// the board REFUSES costs RefusedPlacementPenaltyFactor of that bar. Groping about is the
    /// intended way to learn the board, and this is what it costs. Together they are the whole
    /// design: you cannot see, so you are asked for less, and finding out the hard way is priced.
    ///
    /// "Refuses" is the whole of it, and deliberately not "lands on a cube": what the board will
    /// not take is the only thing billed, and CanPlaceCard is the one authority on that. A
    /// NEGATIVE block is MEANT to be put down over cubes, and a fee for doing what a block is for
    /// would be a bug - it costs nothing, because the board accepts it. So does an antimatter key
    /// over its own kind, a ghost block hanging off the edge, and anything set down on
    /// transparent, void or mine cells. Only what was actually turned away is billed.
    ///
    /// It is still the boss that bends the fewest rules. HidesTheBoard is read once, by the
    /// View, and no rule in the engine depends on it; the other two are the ordinary threshold
    /// and penalty queries every boss may answer.
    /// </summary>
    public sealed class AlacakaranlikBoss : BossRound
    {
        /// <summary>What the blind round asks for, as a share of the round's normal bar.
        /// BALANCE PLACEHOLDER.</summary>
        private const double ThresholdFactor = 0.60;

        /// <summary>What one REFUSED placement costs, as a share of that lowered bar.
        /// BALANCE PLACEHOLDER.</summary>
        private const double RefusedPlacementPenaltyFactor = 0.02;

        public AlacakaranlikBoss()
            : base("alacakaranlik", "Alacakaranlık")
        {
            SetDescription(
                "The board goes dark and you play blind - you cannot tell a filled cell from an "
                    + "empty one. Placing a block dimly lights what it touches; an explosion "
                    + "lights far more, and far brighter. Both fade, and the dark closes back "
                    + "over them. The bar is cut to 60%, but every placement the board refuses "
                    + "costs you 2% of it.",
                "Oyun alanı karanlığa gömülür, körleme oynarsın - dolu kareyle boş kareyi "
                    + "birbirinden ayıramazsın. Blok koymak değdiği yerleri hafifçe aydınlatır; "
                    + "bir patlama çok daha genişi, çok daha parlak aydınlatır. İkisi de söner, "
                    + "karanlık üstünü tekrar örter. Eşik %60'a iner, ama tahtanın kabul "
                    + "etmediği her hamle sana eşiğin %2'sine mal olur.");
        }

        public override bool HidesTheBoard
        {
            get { return true; }
        }

        public override int FilterScoreThreshold(int threshold)
        {
            // Rounded UP, and never to nothing: a blind round is easier, not free.
            return Math.Max(1, (int)Math.Ceiling(threshold * ThresholdFactor));
        }

        public override int PenaltyOnIllegalPlacement(int threshold)
        {
            // The threshold handed in is the one FilterScoreThreshold already lowered, so the
            // fee is 2% of the bar the player is actually chasing. At least a point, or a small
            // enough round would make blundering free. Charged for a REFUSED placement only -
            // see the class comment on why a negative block pays nothing.
            return Math.Max(1, (int)Math.Round(threshold * RefusedPlacementPenaltyFactor));
        }

        public override string StatusText
        {
            get { return Loc.Pick("playing blind", "körleme"); }
        }
    }

    /// <summary>
    /// "Saatçi" - a hard deadline. The round must be finished inside a fixed number of turns; the
    /// turn the limit runs out with the bar unmet, the round is lost. No stalling, no grinding, no
    /// waiting for the perfect hand.
    ///
    /// It reads ThresholdReached, not ThresholdPassed: a boss moves BEFORE the engine's threshold
    /// check, so on the turn that crosses the bar the flag is still false while the score already
    /// covers it. Without that, the watchmaker would kill a round that was just won on the buzzer.
    ///
    /// Once the bar IS met the clock stops mattering - the round is complete, and overtime plays by
    /// its own rules (which are already a deadline of their own).
    ///
    /// The limit is a BALANCE PLACEHOLDER.
    /// </summary>
    public sealed class SaatciBoss : BossRound
    {
        /// <summary>
        /// Turns the player gets to reach the bar, WORKED OUT PER STAGE (see LimitFor). It was a
        /// flat 12 for every appearance, which is the one thing this boss could not be: the bar
        /// grows 1.5x per boss stage, so a fixed limit quietly demanded 17 points a turn at the
        /// boss of round 3 and 2,190 at the boss of 15. The same card was a formality early and
        /// unwinnable late, and which one you got was a draw from GameSession.DrawBoss.
        ///
        /// Set at round start and then only counted down, so it is a normal saved field.
        /// </summary>
        public int TurnLimit;

        /// <summary>Turns granted at the LAST boss of the run. The whole curve hangs off this
        /// one number - see LimitFor - so making the boss kinder or crueller is one edit.</summary>
        public int TurnsAtFinalRound = 100;

        /// <summary>
        /// The turn budget for a stage: <c>TurnsAtFinalRound * sqrt(round / totalRounds)</c>.
        ///
        /// SQUARE ROOT, not a line, and deliberately not the shape of the threshold. Measured
        /// against a scripted player, a stage takes roughly 26 turns at the boss of 3, 63 at the
        /// boss of 6 and 122 at the boss of 9 - so this hands out 45 / 63 / 77 / 89 / 100 across
        /// the five bosses: comfortable at the first, level with the measured need at the second,
        /// and increasingly a test of the build after that.
        ///
        /// It CANNOT hold the difficulty flat, and is not trying to. The bar grows 1.5x a round
        /// while a player's scoring rate grows maybe 1.2x, so any turn limit gets harder with
        /// depth; matching that gap would need 456 turns at the last boss, which is not a limit
        /// at all. A root curve gives away most of its slack early, where the boss should teach,
        /// and least at the end, where it should bite.
        ///
        /// Read off TotalRounds rather than a hard-coded 15, because run length and the round
        /// tables are meant to move together.
        /// </summary>
        private int LimitFor(RoundContext ctx)
        {
            int total = ctx != null && ctx.Session != null ? ctx.Session.Config.TotalRounds : 15;
            int round = ctx != null && ctx.Session != null ? ctx.Session.RoundNumber : 1;
            if (total < 1)
            {
                total = 1;
            }
            double share = Math.Min(1.0, Math.Max(1, round) / (double)total);
            return Math.Max(1, (int)Math.Round(TurnsAtFinalRound * Math.Sqrt(share)));
        }

        public SaatciBoss()
            : base("saatci", "Saatçi")
        {
            SetDescription(
                "You have a hard turn limit - wider on a late stage than an early one, but "
                    + "never generous. Reach the score threshold inside it or the round is lost "
                    + "- there is no stalling this one out.",
                "Kesin bir tur sınırın var - geç aşamalarda daha geniş, ama asla cömert değil. "
                    + "Puan eşiğini o sınırın içinde geç, yoksa raunt kaybedilir - bunu "
                    + "oyalanarak geçemezsin.");
        }

        /// <summary>Turns left before the deadline, for the UI. Never below 0.</summary>
        public int TurnsLeft { get; private set; }

        public override string StatusText
        {
            get { return TurnsLeft + Loc.Pick(" turns left", " tur kaldı"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            TurnLimit = LimitFor(ctx);
            TurnsLeft = TurnLimit;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            TurnsLeft = TurnLimit - turn.Round.TurnNumber;
            if (TurnsLeft < 0)
            {
                TurnsLeft = 0;
            }
            if (TurnsLeft > 0 || turn.Round.ThresholdReached)
            {
                return;
            }
            turn.Round.DeclareLoss(LossReason.OutOfTurns);
        }
    }

    /// <summary>
    /// "Kıtlık" - the deck turns against you. Every card that comes back from the discard picks up
    /// ONE extra cube, in a random spot against what it already has, so the blocks you keep playing
    /// keep getting fatter and harder to fit.
    ///
    /// It fires on the drying-out, which is the one moment the discard is recycled into the draw
    /// pile - so a card grows once per full trip through the deck, and a big deck feeds you thin
    /// cards for longer.
    ///
    /// ROUND-SCOPED (confirmed design): the growth lives in the engine's per-round shape store, the
    /// same one the fox reshape writes to, so GameSession.OwnedCards is never touched and the deck
    /// is its old self next round. This boss makes ONE round hell, it does not poison the run.
    ///
    /// The growth stays in ONE PIECE - the new cube always touches the block - so a fattened card
    /// is a harder card, never a nonsense one.
    ///
    /// UNBREAKABLE BLOCKS ARE SPARED (gold, obsidian, void). Nothing can clear them, so every
    /// cube of one clogs the arena for the rest of the round no matter how well the player plays;
    /// fattening them would bill the same card twice with no way to pay it off.
    /// </summary>
    public sealed class KitlikBoss : BossRound
    {
        private int cubesGrown;

        public KitlikBoss()
            : base("kitlik", "Kıtlık")
        {
            SetDescription(
                "Every card that comes back from the discard grows by one cube, somewhere at "
                    + "random. Unbreakable blocks - gold and obsidian - are spared. Play on long "
                    + "enough and your whole deck is too fat to fit.",
                "Iskartadan desteye dönen her kart rastgele bir yerinden bir küp büyür. "
                    + "Kırılamayan bloklar - altın ve obsidyen - bundan muaftır. Yeterince uzun "
                    + "oynarsan bütün desten tahtaya sığmayacak kadar şişer.");
        }

        /// <summary>Cubes added to cards this round, for the UI.</summary>
        public int CubesGrown
        {
            get { return cubesGrown; }
        }

        public override string StatusText
        {
            get
            {
                return cubesGrown > 0
                    ? "+" + cubesGrown + Loc.Pick(" cubes", " küp")
                    : Loc.Pick("cards fatten", "kartlar şişer");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            cubesGrown = 0;
        }

        /// <summary>The draw pile has just run dry, and the discard is about to be shuffled back
        /// in. Everything sitting in it is a card that was played and is coming back - so every one
        /// of them fattens.</summary>
        public override void OnDrawPileEmptied(RoundContext ctx)
        {
            RoundEngine round = ctx.Round;
            if (round == null)
            {
                return;
            }
            IReadOnlyList<BlockCard> comingBack = round.Deck.DiscardPile;
            for (int i = 0; i < comingBack.Count; i++)
            {
                // UNBREAKABLE BLOCKS ARE SPARED. A gold or obsidian block is already a liability
                // - nothing can clear it, so every cube of it clogs the arena for the rest of
                // the round - and fattening it would punish the same card twice over, in a way
                // the player can never undo. Asked of the ROUND, so the kind is the one the card
                // would actually lay; asked of CubeRules, so the list of unbreakable kinds lives
                // in one place.
                if (!CubeRules.IsDestructibleKind(round.EffectiveCubeKind(comingBack[i])))
                {
                    continue;
                }
                if (round.GrowCardShape(comingBack[i], ctx.Rng) != null)
                {
                    cubesGrown++;
                }
            }
        }
    }

    /// <summary>
    /// "Merkezkaç kuvveti" - the arena spins. At the end of every turn every cube is flung one cell
    /// further from the middle, and whatever goes over the edge is gone for nothing: no score, no
    /// clean-sweep credit, no ledger entry. Building outward is building on sand; the only place
    /// anything stays put is the exact centre of an odd board.
    ///
    /// It empties the board FOR you, which sounds like a favour and is not: a line you spent three
    /// turns assembling walks off the rim before you can complete it, and every cube that leaves
    /// takes its score with it. The way to beat it is to clear lines the turn you build them.
    ///
    /// The cubes are LIFTED rather than destroyed (RoundEngine.FlingBoardsOutward), so nothing here
    /// pays, nothing counts toward a sweep, and nothing lands in a destruction ledger.
    /// </summary>
    public sealed class MerkezkacBoss : BossRound
    {
        private int cubesFlungOff;

        public MerkezkacBoss()
            : base("merkezkac", "Merkezkaç Kuvveti")
        {
            SetDescription(
                "At the end of every turn every cube is flung one cell further from the middle. "
                    + "Whatever goes over the edge is destroyed and pays nothing.",
                "Her tur sonunda tahtadaki her küp merkezden bir kare daha uzağa itilir. "
                    + "Kenardan taşan küpler yok olur ve puan getirmez.");
        }

        /// <summary>Cubes flung off the arena this round, for the UI.</summary>
        public int CubesFlungOff
        {
            get { return cubesFlungOff; }
        }

        public override string StatusText
        {
            get
            {
                return cubesFlungOff > 0
                    ? cubesFlungOff + Loc.Pick(" flung off", " savruldu")
                    : Loc.Pick("spinning", "dönüyor");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            cubesFlungOff = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            cubesFlungOff += turn.Round.FlingBoardsOutward().Count;
        }
    }
}
