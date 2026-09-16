// PURPOSE: What "Hazine"'s find actually DID, written down for the VIEW. Reporting only: the
// hunt, the draw of the reward or penalty and every number in it are unchanged.
//
// IT EXISTS BECAUSE THE VIEW MUST NOT WORK ANY OF THIS OUT, and because the obvious picture is
// wrong. "Treasure = points, dynamite = minus points" is what an explosion sprite suggests and it
// is NOT the rule: the treasure draws one of four rewards and only ONE of them is score (an extra
// half of the explosion's base line value, and only when a line actually went off); the others
// are a market discount, a refilled power and a bonus card. The dynamite draws one of three
// penalties and NONE of them is score: a charged power drained, a hand card frozen, the whole
// hand discarded - or nothing at all, when there was nothing it could take. So the report names
// the effect that was really applied, with the real number beside it, and a View that shows a
// "+N" or "-N" can only be showing a number the rules produced.
//
// THE SCORE IS MEASURED, NOT COPIED. ScoreDelta is RoundScore after the payment minus RoundScore
// before it, so a "Terslik" inversion (the payment runs backwards), the score scale and the
// never-below-nothing floor are all already in it. A treasure can therefore report a NEGATIVE
// score, and must be drawn that way.
//
// IT NEVER NAMES THE OTHER MARK. Finding one removes the other; where the other one WAS is hidden
// information the player never earned, so it is not in this report at all - there is nothing
// here a View could leak. Discoveries holds only cells the player actually blew open.
//
// A NEW OBJECT PER FIND, matched by IDENTITY in the View (see CLAUDE.md). Seed is presentation
// only (particle scatter, glint placement) and comes from the cells, never the round's random
// source.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    public enum HazineResult
    {
        TreasureOnly,
        DynamiteOnly,
        /// <summary>Both marks went in the same turn: no reward, no penalty.</summary>
        BothCancelled
    }

    /// <summary>The effect the rules actually applied. Exactly one per find.</summary>
    public enum HazineEffect
    {
        /// <summary>Cancelled - nothing happened, on purpose.</summary>
        None,
        /// <summary>Treasure: an extra share of the explosion's score. ScoreDelta is real.</summary>
        ExplosionBonus,
        /// <summary>Treasure: a market discount; Amount is the percent.</summary>
        MarketDiscount,
        /// <summary>Treasure: a spent power refilled; PowerId / PowerName.</summary>
        PowerRefilled,
        /// <summary>Treasure: a bonus copy of an owned card, CardId.</summary>
        BonusCard,
        /// <summary>Dynamite: a charged power burnt; PowerId / PowerName.</summary>
        PowerDrained,
        /// <summary>Dynamite: a hand card frozen; CardId, Amount turns.</summary>
        CardFrozen,
        /// <summary>Dynamite: the whole hand discarded and refilled; Amount cards.</summary>
        HandDiscarded,
        /// <summary>Dynamite: there was nothing it could take.</summary>
        Fizzled
    }

    /// <summary>A mark the player blew open, and the cube that stood on it.</summary>
    public sealed class HazineDiscovery
    {
        public GridPos Cell;

        public bool IsTreasure;

        /// <summary>The cube the turn destroyed on that cell - taken from the destruction log,
        /// so it is what was there when it went.</summary>
        public Cube Cube;
    }

    public sealed class HazineVisuals
    {
        public HazineResult Result;

        /// <summary>The marks found this turn: one, or two when they cancelled.</summary>
        public readonly List<HazineDiscovery> Discoveries = new List<HazineDiscovery>();

        public HazineEffect Effect;

        /// <summary>Percent, turns or cards, depending on Effect; 0 where it means nothing.
        /// </summary>
        public int Amount;

        /// <summary>What the round score actually moved by. Non-zero only for ExplosionBonus,
        /// and negative when the payment ran backwards.</summary>
        public int ScoreDelta;

        /// <summary>The power a PowerDrained took or a PowerRefilled filled (-1 otherwise).</summary>
        public int PowerId = -1;

        public string PowerName;

        /// <summary>The card a CardFrozen froze or a BonusCard minted (-1 otherwise).</summary>
        public int CardId = -1;

        /// <summary>Presentation seed from the cells. Never the round's random source.</summary>
        public uint Seed;

        public bool IsReward
        {
            get { return Result == HazineResult.TreasureOnly; }
        }

        public bool IsPenalty
        {
            get { return Result == HazineResult.DynamiteOnly; }
        }

        public HazineDiscovery Find(bool treasure)
        {
            for (int i = 0; i < Discoveries.Count; i++)
            {
                if (Discoveries[i].IsTreasure == treasure)
                {
                    return Discoveries[i];
                }
            }
            return null;
        }
    }
}
