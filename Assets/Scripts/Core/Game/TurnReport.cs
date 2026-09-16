// PURPOSE: The full record of ONE resolved placement - what was placed, what
// exploded/was destroyed, the score breakdown, and the round status afterwards.
// A post-fact notification for the View; jokers get their own mid-turn hooks.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>What became of a cube a boss took off the board (TurnReport.LiftedCells).</summary>
    public enum LiftKind
    {
        /// <summary>It vanished where it stood and its cell is empty now - "Alzheimer"
        /// forgetting a card, "Hidrolik pres" going off.</summary>
        Removed,

        /// <summary>The board MOVED and carried it over the edge - "Yürüyen merdiven",
        /// "Merkezkaç kuvveti". The cell it is reported at may hold another cube by now.</summary>
        Relocated,

        /// <summary>Nothing left: it changed in place - "Kangren".</summary>
        Transformed
    }

    /// <summary>Why a cube a MOVING board carried could not land (LiftMotion.Reason).</summary>
    public enum LiftReason
    {
        /// <summary>Not a move at all: the cube vanished or changed where it stood.</summary>
        None,

        /// <summary>Its step took it past the edge of the arena.</summary>
        ExitedBoard,

        /// <summary>Its step took it onto a cell that is not play area - a hole: there was
        /// nothing to stand on.</summary>
        NoGround,

        /// <summary>Its step took it into a cell something still stood in.</summary>
        Blocked
    }

    /// <summary>Which moving board carried a cube: the pace and character it is drawn with.</summary>
    public enum BoardMotionSource
    {
        /// <summary>Not a board's move.</summary>
        None,

        /// <summary>"Yürüyen merdiven": every row up one, as one mechanism.</summary>
        Escalator,

        /// <summary>"Merkezkaç kuvveti": every cube pushed one cell out from the middle.</summary>
        Centrifuge
    }

    /// <summary>
    /// How a cube a moving board carried off was going when it went - "Yürüyen merdiven",
    /// "Merkezkaç kuvveti". REPORTING ONLY: the rules have already moved the board and dropped the
    /// cube; this tells the View which way it was being pushed, why it could not land, where it
    /// stood and what it was, so the View never works a direction out from where a cube stood.
    /// </summary>
    public readonly struct LiftMotion
    {
        /// <summary>The cell it stood in when the step began. Usually its lifted cell; for a cube
        /// the escalator carried INTO a hole it is the cell below, since the lifted cell is the hole.</summary>
        public readonly GridPos From;

        /// <summary>The one-cell step it was taking: (0, 1) on the escalator; the sign of its
        /// offset from the middle on both axes for the centrifuge, so possibly diagonal. The cell
        /// it tried to reach is From plus Step. (0, 0) when Reason is None.</summary>
        public readonly GridPos Step;

        public readonly LiftReason Reason;

        /// <summary>The cube that went. Its cell may hold another by the time the View looks.</summary>
        public readonly Cube Cube;

        /// <summary>It went from the MIRROR world's board ("Öteki dünya"), not the main one.</summary>
        public readonly bool Mirror;

        /// <summary>Which moving board carried it - the pace it set off at.</summary>
        public readonly BoardMotionSource Source;

        public LiftMotion(GridPos from, GridPos step, LiftReason reason, Cube cube, bool mirror)
            : this(from, step, reason, cube, mirror, BoardMotionSource.None)
        {
        }

        public LiftMotion(GridPos from, GridPos step, LiftReason reason, Cube cube, bool mirror,
            BoardMotionSource source)
        {
            From = from;
            Step = step;
            Reason = reason;
            Cube = cube;
            Mirror = mirror;
            Source = source;
        }

        /// <summary>The same motion, marked as the mirror world's.</summary>
        internal LiftMotion OnMirror()
        {
            return new LiftMotion(From, Step, Reason, Cube, true, Source);
        }
    }

    /// <summary>
    /// A cube a moving board carried from one cell to another and that SURVIVED the move -
    /// "Yürüyen merdiven" riding its row up, "Merkezkaç kuvveti" pushing it one cell out.
    /// REPORTING ONLY: the board already stands in its new state; this tells the View where each
    /// cube was, where it is now and which board moved it, so the View draws the move without
    /// working out any of it.
    /// </summary>
    public readonly struct CellMove
    {
        public readonly GridPos From;

        public readonly GridPos To;

        /// <summary>To minus From: one cell, possibly diagonal.</summary>
        public readonly GridPos Step;

        public readonly Cube Cube;

        public readonly BoardMotionSource Source;

        /// <summary>On the MIRROR world's board ("Öteki dünya").</summary>
        public readonly bool Mirror;

        public CellMove(GridPos from, GridPos to, Cube cube, BoardMotionSource source, bool mirror)
        {
            From = from;
            To = to;
            Step = new GridPos(to.X - from.X, to.Y - from.Y);
            Cube = cube;
            Source = source;
            Mirror = mirror;
        }

        /// <summary>The same move, marked as the mirror world's.</summary>
        internal CellMove OnMirror()
        {
            return new CellMove(From, To, Cube, Source, true);
        }
    }

    /// <summary>
    /// "Kangren": the rot taking one more cell. REPORTING ONLY - the rules have already taken it;
    /// this tells the View which cell, which rotten neighbour it crept in from, what stood there
    /// and what it pressed against and could not take, so the View never works any of it out.
    /// </summary>
    public sealed class GangreneSpread
    {
        public GangreneSpread(GridPos cell, GridPos? source, Cube? before, IReadOnlyList<GridPos> immune)
        {
            Cell = cell;
            Source = source;
            Before = before;
            Immune = immune ?? Array.Empty<GridPos>();
        }

        /// <summary>The cell the rot took.</summary>
        public GridPos Cell { get; }

        /// <summary>The rotten neighbour it crept in from - the first of them right, up, left,
        /// down. Null for the seed, which touches nothing.</summary>
        public GridPos? Source { get; }

        /// <summary>The cube that stood there and turned where it stood; null for an empty cell,
        /// where a rotten cube grew.</summary>
        public Cube? Before { get; }

        /// <summary>The cubes next to the cell the rot could not take: obsidian, gold, a void trap,
        /// a parasite host.</summary>
        public IReadOnlyList<GridPos> Immune { get; }
    }

    /// <summary>
    /// "Kangren": a line the rot took whole, dying - and the jump it made to the nearer edge line,
    /// with the cubes it turned there. REPORTING ONLY, in the order the lines died.
    /// </summary>
    public sealed class GangreneLineDeath
    {
        public GangreneLineDeath(bool isRow, int line, int edgeLine, IReadOnlyList<GridPos> converted,
            IReadOnlyList<Cube> before, int pass)
        {
            IsRow = isRow;
            Line = line;
            EdgeLine = edgeLine;
            Converted = converted ?? Array.Empty<GridPos>();
            Before = before ?? Array.Empty<Cube>();
            Pass = pass;
        }

        /// <summary>A row (the line is an absolute y) or a column (an absolute x).</summary>
        public bool IsRow { get; }

        public int Line { get; }

        /// <summary>The edge line the rot jumped to, the same way round as the dead one.</summary>
        public int EdgeLine { get; }

        /// <summary>The cubes the jump turned where they stood; an empty edge cell stays empty.</summary>
        public IReadOnlyList<GridPos> Converted { get; }

        /// <summary>What each of them was before, index for index with Converted.</summary>
        public IReadOnlyList<Cube> Before { get; }

        /// <summary>Which pass of the cascade killed it: 0 for a line the spread completed, 1 for a
        /// line an earlier jump completed, and so on.</summary>
        public int Pass { get; }
    }

    /// <summary>Immutable-after-resolution record of one turn.</summary>
    public sealed class TurnReport
    {
        /// <summary>
        /// THE JOKERS THAT FIRED THIS TURN, by instance id, in the order they went off.
        ///
        /// Reporting only, and the ONE channel the bar's proc light reads: a joker announces
        /// itself through Joker.NoteProc and the View lights whatever is named here, so a new
        /// joker gets the flash by noting a proc rather than by anything in View learning its
        /// name. Never saved and rebuilt every turn - it is what just happened, not state.
        ///
        /// A joker may appear TWICE if it genuinely fired twice in one turn; the View treats a
        /// repeat as a second event, which is what it is.
        /// </summary>
        public List<int> ProcedJokers { get; } = new List<int>();

        public int TurnNumber { get; internal set; }
        public BlockCard Card { get; internal set; }
        public bool PlayedFromBonusHand { get; internal set; }
        public GridPos Origin { get; internal set; }
        public IReadOnlyList<GridPos> PlacedCells { get; internal set; }

        /// <summary>Which hand slot the card came from, or -1 for a bonus-hand card and for a
        /// world that sat the turn out. Recorded for "Bürokrasi bataklığı", whose tasks can ask
        /// you to play from one side of your hand.</summary>
        public int HandIndex { get; internal set; } = -1;

        public IReadOnlyList<int> ExplodedRows { get; internal set; }
        public IReadOnlyList<int> ExplodedColumns { get; internal set; }
        public int CubesExploded { get; internal set; }

        private readonly List<GridPos> extraExplodedCells = new List<GridPos>();

        /// <summary>Absolute cells destroyed by a board-reshape line clear that ran AFTER the
        /// placement's own explosion was already recorded - an inflation deflate squeeze or a
        /// board power ("Bardağın boş tarafı"). ExplodedRows/Columns/CubesExploded miss these,
        /// so the View blasts these cells (with the explosion sound) to make the late clear
        /// visible. Empty on an ordinary turn.</summary>
        public IReadOnlyList<GridPos> ExtraExplodedCells
        {
            get { return extraExplodedCells; }
        }

        internal void AddExtraExplodedCells(IReadOnlyList<GridPos> cells)
        {
            extraExplodedCells.AddRange(cells);
        }

        private readonly List<GridPos> liftedCells = new List<GridPos>();

        private readonly List<LiftKind> liftKinds = new List<LiftKind>();

        /// <summary>Cells a BOSS took off the board this turn without destroying anything -
        /// "Alzheimer" forgetting a card, "Yürüyen merdiven" carrying a row off the top.
        /// Deliberately separate from every explosion list: nothing was destroyed, so no score,
        /// no sweep and no tally are involved, and the View marks them its own way - which way
        /// depends on LiftKindAt, because not every entry here is a cube that vanished.</summary>
        public IReadOnlyList<GridPos> LiftedCells
        {
            get { return liftedCells; }
        }

        /// <summary>What happened to the cube at LiftedCells[index]. Reporting only: the View asks
        /// it so a cube that vanished, one the board carried off and one that changed in place
        /// are not all drawn the same way.</summary>
        public LiftKind LiftKindAt(int index)
        {
            return index >= 0 && index < liftKinds.Count ? liftKinds[index] : LiftKind.Removed;
        }

        private readonly List<LiftMotion> liftMotions = new List<LiftMotion>();

        /// <summary>How the cube at LiftedCells[index] was moving when it went, for a cube a
        /// moving board carried off; a motion whose Reason is None for everything else. Reporting
        /// only, like LiftKindAt.</summary>
        public LiftMotion LiftMotionAt(int index)
        {
            return index >= 0 && index < liftMotions.Count ? liftMotions[index] : default(LiftMotion);
        }

        internal void AddLiftedCells(IReadOnlyList<GridPos> cells, LiftKind kind)
        {
            AddLiftedCells(cells, kind, null);
        }

        /// <summary>As above, with how each cube was moving: one motion per cell, in order.</summary>
        internal void AddLiftedCells(IReadOnlyList<GridPos> cells, LiftKind kind,
            IReadOnlyList<LiftMotion> motions)
        {
            liftedCells.AddRange(cells);
            for (int i = 0; i < cells.Count; i++)
            {
                liftKinds.Add(kind);
                liftMotions.Add(motions != null && i < motions.Count ? motions[i] : default(LiftMotion));
            }
        }

        private readonly List<CellMove> boardMoves = new List<CellMove>();

        /// <summary>Cubes a moving board carried to another cell this turn that are still on the
        /// board, each with where it was and where it is. Reporting only; the cubes that did NOT
        /// survive the move are in LiftedCells, with their motions.</summary>
        public IReadOnlyList<CellMove> BoardMoves
        {
            get { return boardMoves; }
        }

        internal void AddBoardMoves(IReadOnlyList<CellMove> moves)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                boardMoves.Add(moves[i]);
            }
        }

        /// <summary>"Kangren": the cell the rot took this turn, where it crept in from and what stood
        /// there - null on a turn it did not spread. Reporting only.</summary>
        public GangreneSpread GangreneSpread { get; internal set; }

        private readonly List<GangreneLineDeath> gangreneLineDeaths = new List<GangreneLineDeath>();

        /// <summary>"Kangren": every line the rot took whole this turn, in the order they died, each with
        /// the edge line the rot jumped to and the cubes it turned there. Reporting only.</summary>
        public IReadOnlyList<GangreneLineDeath> GangreneLineDeaths
        {
            get { return gangreneLineDeaths; }
        }

        internal void AddGangreneLineDeaths(IReadOnlyList<GangreneLineDeath> deaths)
        {
            for (int i = 0; i < deaths.Count; i++)
            {
                gangreneLineDeaths.Add(deaths[i]);
            }
        }

        private readonly List<DollEvent> dollEvents = new List<DollEvent>();

        /// <summary>"Matruşka": what happened to the dolls this turn, in the order the boss decided it -
        /// the first doll set down, each doll that split and the cells its children went to, each
        /// last-generation doll that opened on nothing, each doll water carried to another cube, and
        /// the last doll going. Reporting only, like every list here: the View stages exactly this
        /// rather than working any of it out. Empty on every turn of every other round.</summary>
        public IReadOnlyList<DollEvent> DollEvents
        {
            get { return dollEvents; }
        }

        internal void AddDollEvent(DollEvent dollEvent)
        {
            dollEvents.Add(dollEvent);
        }

        /// <summary>Cells a DEFECTIVE SMUGGLED card ("Kaçakçı") passed through on its way out: it
        /// was placed legally and then fell straight through the arena and off the screen. Nothing
        /// landed there, so this is separate from PlacedCells and from every destruction list - it
        /// exists so the View can drop the cubes through the board, and for nothing else. Empty on
        /// every ordinary turn.</summary>
        public IReadOnlyList<GridPos> FellThroughCells { get; internal set; }

        /// <summary>The mirror world's half of the same thing ("Öteki dünya").</summary>
        public IReadOnlyList<GridPos> MirrorFellThroughCells { get; internal set; }

        /// <summary>The cube kind an "Antimadde" card annihilated this turn, or null on every
        /// ordinary turn. The cells themselves are in ExtraExplodedCells, like any late clear.</summary>
        public CubeKind? AnnihilatedKind { get; internal set; }

        private readonly List<GridPos> targetedExplodedCells = new List<GridPos>();

        private readonly List<GridPos> circuitExplodedCells = new List<GridPos>();

        private readonly List<GridPos> columnSweptCells = new List<GridPos>();

        /// <summary>
        /// Cells a "Hedefli" payout took with the target when the block went up whole.
        ///
        /// A channel of its OWN rather than ExtraExplodedCells, and that is load-bearing:
        /// "Antimadde" bills the player per cube in ExtraExplodedCells, so anything else that
        /// wrote into that list would be paid for by a joker that did not cause it. (Reachable:
        /// a negative block erasing a target cube mints an antimatter-of-Target card, and playing
        /// it sets off every armed targeted block at once.) The View treats the two lists alike -
        /// both are late clears that need blasting.
        /// </summary>
        /// <summary>What "İstilacı" took when its marked column came due. Its OWN channel for the
        /// same reason the circuit has one: this is not destruction in the scoring sense - it pays
        /// nothing, counts toward no sweep and feeds no ledger - so it must never be mistaken for
        /// cubes that exploded. Nothing in Core reads this back; it exists so the view can play the
        /// extraction, which it otherwise has no way to learn about.</summary>
        public IReadOnlyList<GridPos> ColumnSweptCells
        {
            get { return columnSweptCells; }
        }

        internal void AddColumnSweptCells(IReadOnlyList<GridPos> cells)
        {
            columnSweptCells.AddRange(cells);
        }

        /// <summary>What "Devre" took when its circuit broke. Its OWN channel, for the same
        /// reason "Hedefli" has one: ExtraExplodedCells is BILLED against by a joker, so putting
        /// these cells there would quietly change what that joker pays. Nothing in Core reads
        /// this list - it exists so the view can blast the cubes and set the cable off, which it
        /// otherwise has no way to learn about. An in-turn destruction reaches neither the report
        /// lists nor the external log, so without this the circuit's cubes simply vanish.</summary>
        public IReadOnlyList<GridPos> CircuitExplodedCells
        {
            get { return circuitExplodedCells; }
        }

        internal void AddCircuitExplodedCells(IReadOnlyList<GridPos> cells)
        {
            circuitExplodedCells.AddRange(cells);
        }

        public IReadOnlyList<GridPos> TargetedExplodedCells
        {
            get { return targetedExplodedCells; }
        }

        internal void AddTargetedExplodedCells(IReadOnlyList<GridPos> cells)
        {
            targetedExplodedCells.AddRange(cells);
        }

        private readonly List<int> targetedBlocksHit = new List<int>();

        /// <summary>Card ids of "Hedefli" blocks whose TARGET was broken first this turn, so the
        /// block paid out and went up whole. Empty on every ordinary turn; the cells it took are
        /// in ExtraExplodedCells like any other late clear.</summary>
        public IReadOnlyList<int> TargetedBlocksHit
        {
            get { return targetedBlocksHit; }
        }

        /// <summary>What those hits were worth in total, in logical points. The View pops it over
        /// the target cell; the score itself is already in the breakdown.</summary>
        public int TargetedBonus { get; private set; }

        internal void NoteTargetedBlockHit(int cardId, int bonus)
        {
            targetedBlocksHit.Add(cardId);
            TargetedBonus += bonus;
        }

        /// <summary>True if this turn emptied the board ("temizlik").</summary>
        public bool CleanSweep { get; internal set; }

        /// <summary>The "kombo" streak after this turn: how many consecutive turns (including
        /// this one) have cleared >=1 line. 0 on a turn that cleared no line. Drives the UI
        /// combo popup and the combo MULTIPLIER (see ComboMultiplier below).</summary>
        public int ComboCount { get; internal set; }

        /// <summary>
        /// True when THIS turn's combo only counted because a quiet turn was bridged
        /// ("Mikrodalga"). The streak did not survive on its own - it was asleep and this clear
        /// woke it up.
        ///
        /// It is the ENGINE that bends the reset (see RoundEngine.Turn), so the engine is what
        /// reports it: the joker only sets the allowance on RoundRules and would otherwise never
        /// learn that its rule had done anything. The combo popup says so, and the joker reads it
        /// to count its own proc.
        /// </summary>
        public bool ComboBridged { get; internal set; }

        /// <summary>The multiplier the combo applied to this turn, or 1.0 on a turn with no
        /// combo. Reporting only - the popup prints it, and it is what says how much a streak is
        /// actually doing when the flat number it used to show no longer exists.</summary>
        public double ComboMultiplier { get; internal set; } = 1.0;

        /// <summary>Every cube removed this turn, from any source (lines, fire chains,
        /// dynamite, joker effects), with the value it held. Grows as the turn resolves.</summary>
        public IReadOnlyList<DestroyedCube> DestroyedCubes { get; internal set; }

        /// <summary>Cards whose LAST cube on the board was destroyed this turn while the
        /// block was still intact - i.e. the whole block went at once ("Kazı çalışması").</summary>
        public IReadOnlyList<int> CardsFullyDestroyed { get; internal set; }

        /// <summary>True if a dynamite block fully exploded on its placement turn and
        /// cleared the board.</summary>
        public bool DynamiteTriggered { get; internal set; }

        /// <summary>Per-turn bonus earned from gold cubes on the board.</summary>
        public int GoldBonus { get; internal set; }

        /// <summary>Bonus awarded this turn for winning an overtime (a clean sweep survived
        /// past the threshold). 0 on turns that did not win an overtime. Pre-multiplier value,
        /// for the UI popup; the banked amount also runs through this turn's joker multipliers.</summary>
        public int OvertimeWinBonus { get; internal set; }

        /// <summary>Water fall animation frames: each entry is one pass of single-cell
        /// drops. Empty when no water moved. The UI replays these and blocks input
        /// while doing so.</summary>
        public IReadOnlyList<IReadOnlyList<WaterMove>> WaterFallFrames { get; internal set; }

        /// <summary>How many WaterFallFrames happened BEFORE the line explosion (the rest
        /// are post-explosion falls). Equals the frame count when nothing exploded. The UI
        /// uses it to play the boom at the right point of the fall animation.</summary>
        public int WaterFramesBeforeExplosion { get; internal set; }

        public int ScoreGained { get; internal set; }
        public int RoundScoreAfter { get; internal set; }

        /// <summary>How ScoreGained was built up (base values, then each joker's flat bonus
        /// and multiplier in inventory order). Null when the round runs without jokers.</summary>
        public ScoreBreakdown Score { get; internal set; }

        /// <summary>True if a draw attempt found the draw pile empty at any point this turn.
        /// Before the threshold that just means the discard was recycled; in overtime it is
        /// the loss condition. "Harcama bonusu" pays out on this.</summary>
        public bool DrawPileEmptiedThisTurn { get; internal set; }

        /// <summary>Bonus-hand plays only: draw pile card flipped face-up into the discard.</summary>
        public BlockCard BurnedCard { get; internal set; }

        /// <summary>True when the played card was a bonus-hand card that EXPIRED from the round
        /// (it did not join any pile). The UI vanishes it rather than flying it to the discard.</summary>
        public bool PlayedCardExpired { get; internal set; }

        /// <summary>True the one time the round score first reached the threshold.</summary>
        public bool ThresholdJustPassed { get; internal set; }

        /// <summary>True if the discard pile was shuffled into the draw pile at any point
        /// during this turn (refill recycle, threshold pass, overtime sweep). UI uses this
        /// for the shuffle animation.</summary>
        public bool DiscardWasReshuffled { get; internal set; }

        public RoundStatus StatusAfter { get; internal set; }

        // ---- the SECOND world ("Öteki dünya"). All empty/null on an ordinary round. ----

        /// <summary>The card the mirror world played this turn, or null (no mirror, or the
        /// mirror had nowhere to put anything and sat the turn out).</summary>
        public BlockCard MirrorCard { get; internal set; }

        public IReadOnlyList<GridPos> MirrorPlacedCells { get; internal set; }
        public IReadOnlyList<int> MirrorExplodedRows { get; internal set; }
        public IReadOnlyList<int> MirrorExplodedColumns { get; internal set; }
        public IReadOnlyList<GridPos> MirrorExplodedCells { get; internal set; }

        /// <summary>The mirror world emptied its own board this turn. Each world sweeps for
        /// itself, so this is independent of CleanSweep.</summary>
        public bool MirrorCleanSweep { get; internal set; }

        /// <summary>Columns that exploded in BOTH worlds this turn - the pay-off "Öteki dünya"
        /// is built around, and what the View celebrates.</summary>
        public IReadOnlyList<int> MirroredColumns { get; internal set; }

        internal TurnReport()
        {
            PlacedCells = Array.Empty<GridPos>();
            FellThroughCells = Array.Empty<GridPos>();
            MirrorFellThroughCells = Array.Empty<GridPos>();
            ExplodedRows = Array.Empty<int>();
            ExplodedColumns = Array.Empty<int>();
            WaterFallFrames = Array.Empty<IReadOnlyList<WaterMove>>();
            DestroyedCubes = Array.Empty<DestroyedCube>();
            CardsFullyDestroyed = Array.Empty<int>();
            MirrorPlacedCells = Array.Empty<GridPos>();
            MirrorExplodedRows = Array.Empty<int>();
            MirrorExplodedColumns = Array.Empty<int>();
            MirrorExplodedCells = Array.Empty<GridPos>();
            MirroredColumns = Array.Empty<int>();
        }
    }
}
