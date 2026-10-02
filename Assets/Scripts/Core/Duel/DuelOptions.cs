// PURPOSE: Every way a held card can be WORN - the duel planner's view of the card abilities. An
// alchemical card is each of its elements in turn, a fox block every shape the round's deck holds
// (RoundEngine.RoundShapes, the same list the dead-end check uses), a mechanical block each of
// its four quarter turns; a plain card is just itself. And for each way of wearing it, every cell
// it may legally go - asked of the BOARD with the card's own placement rules (ghost overhang,
// negative over cubes, a void over anything it may swallow, an antimatter key on its own kind),
// so the planner never considers a move the rules would refuse and never misses one they allow.
//
// Every write it makes to try an option - the card's element choice, the round's fox form and
// rotation for it - is put back before it returns.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One way to wear a held card.</summary>
    internal struct CardOption
    {
        public BlockElement? Element;
        public BlockShape Fox;
        public int Rotation;

        /// <summary>The shape that lands, worn this way.</summary>
        public BlockShape Shape;

        /// <summary>The placement rules of the card worn this way.</summary>
        public bool Ghost;
        public bool Negative;
        public bool Void;
        public bool Antimatter;

        /// <summary>True when a placement can COMPLETE a line: not a gold block (gold locks every
        /// line it lands in), not a negative block or an antimatter key (they take cubes away),
        /// not a defective smuggled card (it falls through).</summary>
        public bool CanComplete;

        /// <summary>The element that decides what its cubes are worth on the board.</summary>
        public bool Gold;
        public bool Dynamite;
    }

    internal static class DuelOptions
    {
        /// <summary>Every distinct way to wear <paramref name="card"/> in <paramref name="round"/>.</summary>
        public static List<CardOption> Of(RoundEngine round, BlockCard card)
        {
            var options = new List<CardOption>();
            var seen = new HashSet<string>();
            int previousChoice = card.ActiveChoice;
            IReadOnlyList<BlockElement> choices = card.ElementChoices;
            int elementCount = card.IsAlchemical ? choices.Count : 1;
            try
            {
                for (int e = 0; e < elementCount; e++)
                {
                    BlockElement? element = null;
                    if (card.IsAlchemical)
                    {
                        element = choices[e];
                        card.Choose(element.Value);
                    }
                    // What the card IS worn as this element - asked once, not once per form.
                    CardOption worn = Describe(round, card, element, null, 0, card.Shape);
                    bool fox = round.CardHasElement(card, BlockElement.Fox);
                    bool mechanical = round.CardHasElement(card, BlockElement.Mechanical);
                    var forms = new List<BlockShape>();
                    if (fox)
                    {
                        var formKeys = new HashSet<string>();
                        foreach (BlockShape form in round.RoundShapes())
                        {
                            if (formKeys.Add(form.CanonicalKey))
                            {
                                forms.Add(form);
                            }
                        }
                        forms.Sort((a, b) => string.CompareOrdinal(a.CanonicalKey, b.CanonicalKey));
                    }
                    else
                    {
                        forms.Add(null);
                    }
                    for (int f = 0; f < forms.Count; f++)
                    {
                        BlockShape baseShape = forms[f] ?? card.Shape;
                        BlockShape turned = baseShape;
                        int turns = mechanical ? 4 : 1;
                        for (int r = 0; r < turns; r++)
                        {
                            if (r > 0)
                            {
                                turned = turned.RotatedClockwise();
                            }
                            string key = (element.HasValue ? ((int)element.Value).ToString() : "-")
                                + "|" + turned.CanonicalKey;
                            if (!seen.Add(key))
                            {
                                continue;
                            }
                            CardOption option = worn;
                            option.Fox = forms[f];
                            option.Rotation = r;
                            option.Shape = turned;
                            options.Add(option);
                        }
                    }
                }
            }
            finally
            {
                card.ActiveChoice = previousChoice;
            }
            return options;
        }

        private static CardOption Describe(RoundEngine round, BlockCard card, BlockElement? element,
            BlockShape fox, int rotation, BlockShape shape)
        {
            bool gold = round.CardHasElement(card, BlockElement.Gold);
            bool negative = round.CardHasElement(card, BlockElement.Negative);
            bool antimatter = card.AntimatterOf.HasValue;
            return new CardOption
            {
                Element = element,
                Fox = fox,
                Rotation = rotation,
                Shape = shape,
                Ghost = round.CardHasElement(card, BlockElement.Ghost),
                Negative = negative,
                Void = round.CardHasElement(card, BlockElement.Void),
                Antimatter = antimatter,
                Gold = gold,
                Dynamite = round.CardHasElement(card, BlockElement.Dynamite),
                CanComplete = !gold && !negative && !antimatter && !card.FallsThrough
            };
        }

        /// <summary>
        /// Every legal origin for <paramref name="card"/> worn as <paramref name="option"/>, in a
        /// stable order (x, then y). The board is asked directly for the common cases - the same
        /// call CanPlaceCard makes - and the round itself for the rare keys and holes, with the
        /// option put on and taken off again around the question.
        /// </summary>
        public static void Origins(RoundEngine round, BlockCard card, CardOption option,
            List<GridPos> into)
        {
            into.Clear();
            GameBoard board = round.Board;
            BlockShape shape = option.Shape;
            int minX = option.Ghost ? board.MinX - shape.Width + 1 : board.MinX;
            int maxX = option.Ghost ? board.MinX + board.Width - 1 : board.MinX + board.Width - shape.Width;
            int minY = option.Ghost ? board.MinY - shape.Height + 1 : board.MinY;
            int maxY = option.Ghost ? board.MinY + board.Height - 1 : board.MinY + board.Height - shape.Height;
            bool special = option.Void || option.Antimatter;
            int previousChoice = card.ActiveChoice;
            BlockShape previousFox = special ? round.FoxShapeOf(card.Id) : null;
            int previousTurns = special ? round.RotationStepsOf(card.Id) : 0;
            try
            {
                if (special)
                {
                    if (option.Element.HasValue)
                    {
                        card.Choose(option.Element.Value);
                    }
                    round.SetCardOrientation(card.Id, option.Fox, option.Rotation);
                }
                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        var origin = new GridPos(x, y);
                        bool legal = special
                            ? round.CanPlaceCard(card, origin)
                            : board.CanPlace(shape, origin, option.Ghost, option.Negative);
                        if (legal)
                        {
                            into.Add(origin);
                        }
                    }
                }
            }
            finally
            {
                if (special)
                {
                    round.SetCardOrientation(card.Id, previousFox, previousTurns);
                }
                card.ActiveChoice = previousChoice;
            }
        }

        /// <summary>Cards that play exactly alike - same shape, same elements and choice, same
        /// special nature - share one key, so the planner never weighs the same move twice.</summary>
        public static string TypeKey(BlockCard card)
        {
            var key = new System.Text.StringBuilder(card.Shape.CanonicalKey);
            key.Append('|');
            IReadOnlyList<BlockElement> elements = card.Elements;
            for (int i = 0; i < elements.Count; i++)
            {
                key.Append((int)elements[i]).Append(',');
            }
            key.Append('|').Append(card.ActiveChoice);
            if (card.AntimatterOf.HasValue)
            {
                key.Append("|A").Append((int)card.AntimatterOf.Value);
            }
            if (card.FallsThrough)
            {
                key.Append("|F");
            }
            return key.ToString();
        }

        /// <summary>The board cells a placement covers, as a mask - only the cells that are play
        /// area (a ghost's overhang is not).</summary>
        public static CellMask MaskOf(GameBoard board, BlockShape shape, GridPos origin)
        {
            var mask = new CellMask();
            IReadOnlyList<GridPos> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                GridPos cell = origin + cells[i];
                if (board.IsInside(cell))
                {
                    mask.Set(CellMask.IndexOf(board, cell));
                }
            }
            return mask;
        }
    }
}
