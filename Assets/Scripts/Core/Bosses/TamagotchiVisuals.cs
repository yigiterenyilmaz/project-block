// PURPOSE: What "Tamagotchi" did, written down for the VIEW - the data contract of the boss's
// presentation. Reporting only: every decision (which cards it asks for, whether a feed is valid,
// when it goes furious, what it punishes and exactly what it eats) is TamagotchiBoss's, and the
// View only ever plays these back. A NEW object per event, matched by IDENTITY in the View (the
// rule every per-turn report in the game follows), never saved.
//
// The value tier is Core's verdict on how much a card is worth to the player right now
// (TamagotchiBoss.ValueScore); the View uses it only to decide how ceremonious a bite looks.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>How much a card (or a joker/power) is worth to the player, in three steps.</summary>
    public enum CardValueTier
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>The pet's mood as the rules see it. Calm, Hungry, Impatient and Angry are the
    /// deadline running down while something is still owed; Furious is the deadline missed (it
    /// stays furious for the rest of the round); Satisfied is every request fed.</summary>
    public enum PetHungerStage
    {
        Calm = 0,
        Hungry = 1,
        Impatient = 2,
        Angry = 3,
        Furious = 4,
        Satisfied = 5
    }

    /// <summary>What a furious pet can take.</summary>
    public enum PetPunishKind
    {
        Board = 0,
        Joker = 1,
        Power = 2,
        DrawPile = 3,
        DiscardPile = 4
    }

    /// <summary>One option the punish planner weighed: what it would take and how much that would
    /// hurt (Pressure, 0..1), how close it comes to an unwinnable position (HardLockRisk, 0..1),
    /// and whether it could be taken at all. Development display only.</summary>
    public sealed class PetPunishCandidate
    {
        public PetPunishKind Kind;
        public bool Valid;
        public float Pressure;
        public float HardLockRisk;
        public string Label;
    }

    /// <summary>One empty cell the board bite considered as its FIRST bite: how much room the
    /// player would have left (lower is crueller) and whether it was refused for leaving no way
    /// out. Development display only.</summary>
    public struct PetCellScore
    {
        public GridPos Cell;
        public float Room;
        public bool Rejected;
    }

    /// <summary>One request slot as the View draws it.</summary>
    public struct PetRequest
    {
        public int SlotId;
        public BlockCard Card;
        public CardValueTier Tier;
        public bool Fed;
    }

    /// <summary>A card the player fed it.</summary>
    public sealed class TamagotchiFeedVisuals
    {
        public int RequestSlotId;
        /// <summary>The card itself - gone from the run, kept here so the View can draw what
        /// was eaten.</summary>
        public BlockCard Card;
        public CardValueTier Tier;
        /// <summary>The hand slot the card left, which stays EMPTY until the next placement.</summary>
        public int HandSlotIndex;
        public int RequestsRemaining;
    }

    /// <summary>The pet's hunger moving up a stage (or to satisfied / furious).</summary>
    public sealed class TamagotchiHungerChange
    {
        public PetHungerStage Previous;
        public PetHungerStage Stage;
        public float Progress;
    }

    /// <summary>The deadline missed: the pet goes furious, its requests are destroyed, and the
    /// first punish follows in the same breath.</summary>
    public sealed class TamagotchiFuryVisuals
    {
        public int MissingFeedCount;
        public PetPunishKind PunishKind;
    }

    /// <summary>What a furious pet took. Exactly one of the payload groups is filled, by Kind.
    /// </summary>
    public sealed class PetRampageVisuals
    {
        public PetPunishKind Kind;
        public uint Seed;

        /// <summary>BOARD: the cells it ate, in the order it chose them (one connected bite), and
        /// their bounds.</summary>
        public readonly List<GridPos> Cells = new List<GridPos>();
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;

        /// <summary>JOKER / POWER: what it ate. InstanceId is the inventory instance it had.</summary>
        public string DefId;
        public string EatenName;
        public int InstanceId;
        /// <summary>Where the eaten joker/power sat in its bar when it was taken.</summary>
        public int InventoryIndex = -1;
        public CardValueTier Tier;

        /// <summary>DRAW / DISCARD PILE: the cards it ate (gone from the run), their tiers, and the
        /// pile's count before and after.</summary>
        public readonly List<BlockCard> Cards = new List<BlockCard>();
        public readonly List<CardValueTier> CardTiers = new List<CardValueTier>();
        public int CountBefore;
        public int CountAfter;

        /// <summary>The planner's working: every candidate and, for the board, how each first bite
        /// scored. Development display only.</summary>
        public readonly List<PetPunishCandidate> Candidates = new List<PetPunishCandidate>();
        public readonly List<PetCellScore> CellScores = new List<PetCellScore>();

        /// <summary>How many things it ate, whatever they were.</summary>
        public int Count
        {
            get
            {
                switch (Kind)
                {
                    case PetPunishKind.Board: return Cells.Count;
                    case PetPunishKind.Joker:
                    case PetPunishKind.Power: return EatenName != null ? 1 : 0;
                    default: return Cards.Count;
                }
            }
        }
    }
}
