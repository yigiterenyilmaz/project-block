// PURPOSE: Decides the setup (board size + threshold) for any STAGE of a run.
// Must be deterministic. New difficulty curves are new implementations.
//
// A run is a sequence of STAGES, not just rounds: every third numbered round is followed by a
// BOSS STAGE that carries the same round number. So round 3 is played, then round 3's boss, then
// round 4. That is why GetRound takes the flag - the two stages of the same number are different
// setups, and only the caller knows which one it is asking about.

using System;

namespace ProjectBlock.Core
{
    /// <summary>Provides the setup for any stage of a run. Must be deterministic.</summary>
    public interface IRoundProgression
    {
        /// <summary>The setup for a stage. <paramref name="bossStage"/> asks for the BOSS stage
        /// that follows that numbered round rather than the round itself.</summary>
        RoundConfig GetRound(int roundNumber, bool bossStage);

        /// <summary>True when a boss stage follows that numbered round.</summary>
        bool HasBossStageAfter(int roundNumber);
    }
}
