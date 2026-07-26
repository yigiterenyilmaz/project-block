// PURPOSE: Save/load for the plain structural types - positions, shapes, cards, cubes, and
// the two mutable config objects the rules let jokers bend (RoundRules, ScoringConfig).
//
// THE CARD TABLE is the important idea here. A BlockCard is a reference shared by the owned
// deck, the draw/discard/removed piles, the hand and the bonus hand at the same time, and
// GameSession removes cards by REFERENCE. So cards are written ONCE into a table and every
// other place stores only ids; loading rebuilds the table first and hands out the same
// instances, which keeps that sharing intact.
//
// Board cubes need no such care - a Cube already stores its source card as a plain int id.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Save/load for the structural value types.</summary>
    public static class CoreSerializers
    {
        // ------------------------------------------------------------------- GridPos

        public static void WritePos(SaveWriter w, string key, GridPos pos)
        {
            w.Write(key + ".x", pos.X);
            w.Write(key + ".y", pos.Y);
        }

        public static GridPos ReadPos(SaveReader r, string key)
        {
            int x = r.ReadInt(key + ".x");
            int y = r.ReadInt(key + ".y");
            return new GridPos(x, y);
        }

        public static void WritePosList(SaveWriter w, string key, IReadOnlyList<GridPos> cells)
        {
            w.Write(key + ".count", cells != null ? cells.Count : 0);
            if (cells == null)
            {
                return;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                WritePos(w, key + "." + i, cells[i]);
            }
        }

        public static List<GridPos> ReadPosList(SaveReader r, string key)
        {
            int count = r.ReadInt(key + ".count");
            var cells = new List<GridPos>(count);
            for (int i = 0; i < count; i++)
            {
                cells.Add(ReadPos(r, key + "." + i));
            }
            return cells;
        }

        // ---------------------------------------------------------------- BlockShape

        public static void WriteShape(SaveWriter w, string key, BlockShape shape)
        {
            WritePosList(w, key, shape.Cells);
        }

        public static BlockShape ReadShape(SaveReader r, string key)
        {
            return BlockShape.FromCells(ReadPosList(r, key));
        }

        // ----------------------------------------------------------------- BlockCard

        public static void WriteCard(SaveWriter w, string key, BlockCard card)
        {
            w.Write(key + ".id", card.Id);
            w.Write(key + ".custom", card.IsCustom);
            // "Kaçakçı": smuggled goods, and whether they were the defective kind. Without these a
            // reload would quietly turn a card that falls through the board into a healthy one.
            w.Write(key + ".smuggled", card.IsSmuggled);
            w.Write(key + ".falls", card.FallsThrough);
            // "Antimadde": which cube kind this card annihilates, or -1 for an ordinary card.
            // Without it a reloaded antimatter card would place like a normal block.
            w.Write(key + ".antimatter",
                card.AntimatterOf.HasValue ? (int)card.AntimatterOf.Value : -1);
            WriteShape(w, key + ".shape", card.Shape);
            IReadOnlyList<BlockElement> elements = card.Elements;
            w.Write(key + ".elements.count", elements.Count);
            for (int i = 0; i < elements.Count; i++)
            {
                w.Write(key + ".elements." + i, (int)elements[i]);
            }
            // Per-cube designed blocks ("Karakter oluşturma") carry one element per cube, so
            // the layout has to travel too - -1 stands for a plain cube.
            w.Write(key + ".percube", card.HasPerCubeElements);
            if (!card.HasPerCubeElements)
            {
                return;
            }
            int cubes = card.Shape.Cells.Count;
            w.Write(key + ".percube.count", cubes);
            for (int i = 0; i < cubes; i++)
            {
                BlockElement? element = card.CellElement(i);
                w.Write(key + ".percube." + i, element.HasValue ? (int)element.Value : -1);
            }
        }

        public static BlockCard ReadCard(SaveReader r, string key)
        {
            int id = r.ReadInt(key + ".id");
            bool custom = r.ReadBool(key + ".custom");
            bool smuggled = r.ReadBool(key + ".smuggled");
            bool falls = r.ReadBool(key + ".falls");
            int antimatter = r.ReadInt(key + ".antimatter");
            BlockShape shape = ReadShape(r, key + ".shape");
            int elementCount = r.ReadInt(key + ".elements.count");
            var elements = new List<BlockElement>(elementCount);
            for (int i = 0; i < elementCount; i++)
            {
                elements.Add((BlockElement)r.ReadInt(key + ".elements." + i));
            }
            bool perCube = r.ReadBool(key + ".percube");
            if (!perCube)
            {
                return Marked(new BlockCard(id, shape, elements, custom), smuggled, falls,
                    antimatter);
            }
            int cubes = r.ReadInt(key + ".percube.count");
            var layout = new List<BlockElement?>(cubes);
            for (int i = 0; i < cubes; i++)
            {
                int raw = r.ReadInt(key + ".percube." + i);
                layout.Add(raw < 0 ? (BlockElement?)null : (BlockElement)raw);
            }
            // Designed() recomputes the distinct element set from the layout, which is exactly
            // what was written above, so the card comes back identical.
            return Marked(BlockCard.Designed(id, shape, layout), smuggled, falls, antimatter);
        }

        /// <summary>Puts the after-the-fact marks back on a rebuilt card - the two "Kaçakçı"
        /// flags and the "Antimadde" kind. All three are stamped AFTER a card exists, exactly as
        /// the market and the joker do it, so they are set rather than constructed.</summary>
        private static BlockCard Marked(BlockCard card, bool smuggled, bool falls, int antimatter)
        {
            card.IsSmuggled = smuggled;
            card.FallsThrough = falls;
            card.AntimatterOf = antimatter < 0 ? (CubeKind?)null : (CubeKind)antimatter;
            return card;
        }

        // ---------------------------------------------------------------------- Cube

        public static void WriteCube(SaveWriter w, string key, Cube cube)
        {
            w.Write(key + ".kind", (int)cube.Kind);
            w.Write(key + ".card", cube.SourceCardId);
            w.Write(key + ".protected", cube.Protected);
        }

        public static Cube ReadCube(SaveReader r, string key)
        {
            CubeKind kind = (CubeKind)r.ReadInt(key + ".kind");
            int card = r.ReadInt(key + ".card");
            bool isProtected = r.ReadBool(key + ".protected");
            return new Cube(kind, card, isProtected);
        }

        // --------------------------------------------------------------- RoundConfig

        public static void WriteRoundConfig(SaveWriter w, string key, RoundConfig config)
        {
            w.Write(key + ".round", config.RoundNumber);
            w.Write(key + ".width", config.BoardWidth);
            w.Write(key + ".height", config.BoardHeight);
            w.Write(key + ".threshold", config.ScoreThreshold);
            w.Write(key + ".erosion", (int)config.Erosion);
            w.Write(key + ".boss", config.IsBossRound);
            WritePosList(w, key + ".extra", config.ExtraPlayableCells);
        }

        public static RoundConfig ReadRoundConfig(SaveReader r, string key)
        {
            int round = r.ReadInt(key + ".round");
            int width = r.ReadInt(key + ".width");
            int height = r.ReadInt(key + ".height");
            int threshold = r.ReadInt(key + ".threshold");
            var erosion = (ShuffleErosion)r.ReadInt(key + ".erosion");
            bool boss = r.ReadBool(key + ".boss");
            List<GridPos> extra = ReadPosList(r, key + ".extra");
            return new RoundConfig(round, width, height, threshold, extra, erosion, boss);
        }

        // ---------------------------------------------------------------- RoundRules

        /// <summary>The LIVE rules, not the defaults: jokers and powers mutate this object all
        /// run long (hand size, retro mode, the reveal flags), so a save that rebuilt it from
        /// defaults would quietly undo every one of them.</summary>
        public static void WriteRules(SaveWriter w, string key, RoundRules rules)
        {
            w.Write(key + ".handSize", rules.HandSize);
            w.Write(key + ".cardsPerContinue", rules.CardsRemovedPerContinue);
            w.Write(key + ".continueEscalation", rules.ContinueCostEscalation);
            w.Write(key + ".freeRecycles", rules.FreeDeckRecycles);
            w.Write(key + ".revealTopDraw", rules.RevealTopDrawCard);
            w.Write(key + ".revealedDiscard", rules.RevealedDiscardCount);
            w.Write(key + ".hideDiscardTop", rules.HideDiscardTop);
            w.Write(key + ".revealedDraw", rules.RevealedDrawCount);
            w.Write(key + ".playedToDraw", rules.PlayedCardsReturnToDrawPile);
            w.Write(key + ".externalSweeps", rules.CountExternalSweeps);
            w.Write(key + ".skipRefill", rules.SkipStandardRefill);
            w.Write(key + ".drawOnlyAvailable", rules.DrawOnlyAvailableNoReshuffle);
            w.Write(key + ".retro", rules.RetroMode);
            w.Write(key + ".deadZoneRows", rules.DeadZoneRows);
        }

        public static void ReadRulesInto(SaveReader r, string key, RoundRules rules)
        {
            rules.HandSize = r.ReadInt(key + ".handSize");
            rules.CardsRemovedPerContinue = r.ReadInt(key + ".cardsPerContinue");
            rules.ContinueCostEscalation = r.ReadInt(key + ".continueEscalation");
            rules.FreeDeckRecycles = r.ReadInt(key + ".freeRecycles");
            rules.RevealTopDrawCard = r.ReadBool(key + ".revealTopDraw");
            rules.RevealedDiscardCount = r.ReadInt(key + ".revealedDiscard");
            rules.HideDiscardTop = r.ReadBool(key + ".hideDiscardTop");
            rules.RevealedDrawCount = r.ReadInt(key + ".revealedDraw");
            rules.PlayedCardsReturnToDrawPile = r.ReadBool(key + ".playedToDraw");
            rules.CountExternalSweeps = r.ReadBool(key + ".externalSweeps");
            rules.SkipStandardRefill = r.ReadBool(key + ".skipRefill");
            rules.DrawOnlyAvailableNoReshuffle = r.ReadBool(key + ".drawOnlyAvailable");
            rules.RetroMode = r.ReadBool(key + ".retro");
            rules.DeadZoneRows = r.ReadInt(key + ".deadZoneRows");
        }

        // -------------------------------------------------------------- ScoringConfig

        /// <summary>Also live-mutable ("bereket" raises gained score permanently).</summary>
        public static void WriteScoring(SaveWriter w, string key, ScoringConfig scoring)
        {
            w.Write(key + ".scale", scoring.ScoreScale);
            w.Write(key + ".perCubePlaced", scoring.PointsPerCubePlaced);
            w.Write(key + ".retroPlacement", scoring.RetroPlacementBonus);
            w.Write(key + ".perLine", scoring.PointsPerLine);
            w.Write(key + ".comboStep", scoring.ComboBonusPerStep);
            w.Write(key + ".perCubeExploded", scoring.PointsPerCubeExploded);
            w.Write(key + ".multiLine", scoring.MultiLineBonusPerExtraLine);
            w.Write(key + ".sweep", scoring.CleanSweepBonus);
            w.Write(key + ".goldPerTurn", scoring.GoldPointsPerCubePerTurn);
            w.Write(key + ".overtimeFactor", scoring.OvertimeRegularScoreFactor);
            w.Write(key + ".overtimeBase", scoring.OvertimeWinBonusBaseFraction);
            w.Write(key + ".overtimeStep", scoring.OvertimeWinBonusStepFraction);
        }

        public static void ReadScoringInto(SaveReader r, string key, ScoringConfig scoring)
        {
            scoring.ScoreScale = r.ReadInt(key + ".scale");
            scoring.PointsPerCubePlaced = r.ReadInt(key + ".perCubePlaced");
            scoring.RetroPlacementBonus = r.ReadInt(key + ".retroPlacement");
            scoring.PointsPerLine = r.ReadInt(key + ".perLine");
            scoring.ComboBonusPerStep = r.ReadInt(key + ".comboStep");
            scoring.PointsPerCubeExploded = r.ReadInt(key + ".perCubeExploded");
            scoring.MultiLineBonusPerExtraLine = r.ReadInt(key + ".multiLine");
            scoring.CleanSweepBonus = r.ReadInt(key + ".sweep");
            scoring.GoldPointsPerCubePerTurn = r.ReadInt(key + ".goldPerTurn");
            scoring.OvertimeRegularScoreFactor = r.ReadDouble(key + ".overtimeFactor");
            scoring.OvertimeWinBonusBaseFraction = r.ReadDouble(key + ".overtimeBase");
            scoring.OvertimeWinBonusStepFraction = r.ReadDouble(key + ".overtimeStep");
        }
    }

    /// <summary>
    /// Every BlockCard in a run, written once and referenced by id everywhere else.
    /// See the file header for why references must be shared rather than duplicated.
    /// </summary>
    public sealed class CardTable
    {
        private readonly List<BlockCard> cards = new List<BlockCard>();
        private readonly Dictionary<int, BlockCard> byId = new Dictionary<int, BlockCard>();

        public void Add(BlockCard card)
        {
            if (card == null || byId.ContainsKey(card.Id))
            {
                return;
            }
            byId.Add(card.Id, card);
            cards.Add(card);
        }

        public void AddRange(IEnumerable<BlockCard> range)
        {
            if (range == null)
            {
                return;
            }
            foreach (BlockCard card in range)
            {
                Add(card);
            }
        }

        /// <summary>The card with this id, or null. A null return is normal for id 0 and for
        /// cubes whose source card left the run.</summary>
        public BlockCard Get(int id)
        {
            BlockCard card;
            return byId.TryGetValue(id, out card) ? card : null;
        }

        public void Write(SaveWriter w, string key)
        {
            w.Write(key + ".count", cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                CoreSerializers.WriteCard(w, key + "." + i, cards[i]);
            }
        }

        public static CardTable Read(SaveReader r, string key)
        {
            var table = new CardTable();
            int count = r.ReadInt(key + ".count");
            for (int i = 0; i < count; i++)
            {
                table.Add(CoreSerializers.ReadCard(r, key + "." + i));
            }
            return table;
        }

        /// <summary>Writes a pile as bare ids - the cards themselves live in the table.</summary>
        public void WriteRefs(SaveWriter w, string key, IReadOnlyList<BlockCard> pile)
        {
            w.Write(key + ".count", pile != null ? pile.Count : 0);
            if (pile == null)
            {
                return;
            }
            for (int i = 0; i < pile.Count; i++)
            {
                w.Write(key + "." + i, pile[i].Id);
            }
        }

        /// <summary>Reads a pile of ids back into the SHARED card instances. An id missing from
        /// the table would mean the save is inconsistent, so it throws rather than dropping a
        /// card and quietly changing the deck's size.</summary>
        public List<BlockCard> ReadRefs(SaveReader r, string key)
        {
            int count = r.ReadInt(key + ".count");
            var pile = new List<BlockCard>(count);
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt(key + "." + i);
                BlockCard card = Get(id);
                if (card == null)
                {
                    throw new SaveFormatException("Card " + id + " (" + key + "." + i
                        + ") is not in the save's card table.");
                }
                pile.Add(card);
            }
            return pile;
        }
    }
}
