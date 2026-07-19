// PURPOSE: Everything that happened during one resolved turn, in order, so the UI can
// animate it and future jokers can react to it. RoundEngine fills this; nothing else
// writes to it. If you add a mechanic that does something new in a turn, add a field
// here instead of making the UI re-derive state.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Lifecycle state of a round.</summary>
    public enum RoundStatus
    {
        /// <summary>Player can place blocks.</summary>
        InProgress = 0,

        /// <summary>Threshold passed or overtime clean sweep: player must choose
        /// advance-to-market or continue via RoundEngine.DecideAdvance.</summary>
        AwaitingAdvanceDecision = 1,

        /// <summary>Round finished successfully; session moves to the market.</summary>
        Advanced = 2,

        /// <summary>Round lost; the run is over (see RoundEngine.Loss).</summary>
        Lost = 3
    }

    /// <summary>Why a round was lost.</summary>
    public enum LossReason
    {
        /// <summary>No held block (hand or bonus hand) fits anywhere on the board.</summary>
        NoPlayableMove = 0,

        /// <summary>Confirmed rule: before the threshold, the hand could not be refilled to
        /// full size because both draw and discard piles were empty.</summary>
        HandCannotBeRefilled = 1,

        /// <summary>"Batak": the player bet on clearing the board within N turns and the
        /// deadline passed without a clean sweep.</summary>
        BetFailed = 3,

        /// <summary>Overtime rule: after the threshold was passed, the draw pile ran dry
        /// before a clean sweep re-shuffled it.</summary>
        DrawPileEmptyAfterThreshold = 2,

        /// <summary>Retro top-out: a block reached the very top row, so there is no room to drop
        /// the next piece from above (even if lower rows still have gaps) - like Tetris.</summary>
        RetroTopOut = 4
    }

    /// <summary>One cube that was removed from the board during a turn, with the value it
    /// held. Jokers need the KIND (was it fire? ice?) and the SOURCE CARD, both of which
    /// are gone from the board by the time a hook runs.</summary>
    public readonly struct DestroyedCube
    {
        public readonly GridPos Pos;
        public readonly Cube Cube;

        public DestroyedCube(GridPos pos, Cube cube)
        {
            Pos = pos;
            Cube = cube;
        }
    }

    /// <summary>Immutable-after-resolution record of one turn.</summary>
    public sealed class TurnReport
    {
        public int TurnNumber { get; internal set; }
        public BlockCard Card { get; internal set; }
        public bool PlayedFromBonusHand { get; internal set; }
        public GridPos Origin { get; internal set; }
        public IReadOnlyList<GridPos> PlacedCells { get; internal set; }

        public IReadOnlyList<int> ExplodedRows { get; internal set; }
        public IReadOnlyList<int> ExplodedColumns { get; internal set; }
        public int CubesExploded { get; internal set; }

        /// <summary>True if this turn emptied the board ("temizlik").</summary>
        public bool CleanSweep { get; internal set; }

        /// <summary>The "kombo" streak after this turn: how many consecutive turns (including
        /// this one) have cleared >=1 line. 0 on a turn that cleared no line. Drives the UI
        /// combo popup and the BaseCombo score.</summary>
        public int ComboCount { get; internal set; }

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

        internal TurnReport()
        {
            PlacedCells = Array.Empty<GridPos>();
            ExplodedRows = Array.Empty<int>();
            ExplodedColumns = Array.Empty<int>();
            WaterFallFrames = Array.Empty<IReadOnlyList<WaterMove>>();
            DestroyedCubes = Array.Empty<DestroyedCube>();
            CardsFullyDestroyed = Array.Empty<int>();
        }
    }
}
