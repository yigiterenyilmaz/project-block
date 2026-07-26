// PURPOSE: The four workshop powers - the ones that take a block apart, weld two together, move an
// element from the board into your hand, and squeeze a patch of arena flat.
//
// They share one idea: every one of them is ROUND-SCOPED. The cards they make live in the bonus
// hand and are gone at the end of the round, the element they move goes home when the card is
// discarded, and the press lets go of the board four turns later. Your deck is exactly what it was
// when the round started - these change how you play a round, never what you own.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// "Neşter" - cut one block in your hand into two. You pick which cubes go into the first
    /// piece; the rest are the second, and the card you cut is gone.
    ///
    /// BOTH HALVES MUST HOLD TOGETHER. A cut that would leave a piece in two loose bits is refused,
    /// which is what stops the power from being a way to mint scattered nonsense.
    ///
    /// ROUND-SCOPED (confirmed design): the two pieces arrive as bonus cards and expire with the
    /// round, so the deck you own is untouched - the whole card comes back next round.
    /// </summary>
    public sealed class NesterPower : Power
    {
        public NesterPower()
            : base("nester", "Neşter")
        {
            SetDescription(
                "Cut a block in your hand in two - you choose which cubes go into the first piece. "
                    + "Both halves have to hold together. They arrive as bonus cards and last the "
                    + "round; the whole block is back in your deck next round.",
                "Elindeki bir bloğu ikiye kes - ilk parçaya hangi küplerin gireceğini sen "
                    + "seçersin. İki parça da kendi içinde bitişik olmak zorunda. Bonus kart "
                    + "olarak gelirler ve raunt boyunca kalırlar; blok bir sonraki raunt "
                    + "destende yine bütün.");
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.CardCubes; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return SplitOf(ctx, target) != null;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            List<GridPos>[] halves = SplitOf(ctx, target);
            if (halves == null)
            {
                return false;
            }
            RoundEngine round = ctx.Round;
            BlockCard whole = round.Hand[target.HandIndex.Value];
            IReadOnlyList<BlockElement> elements = whole.Elements;

            // The cut card leaves the round entirely - not to the discard, or it would come back
            // this round alongside its own halves.
            round.TakeCardOutOfRound(target.HandIndex.Value);
            foreach (List<GridPos> half in halves)
            {
                BlockCard piece = ctx.Session.CreateCard(BlockShape.FromCells(half), elements);
                round.AddBonusCard(piece, BonusPlayOutcome.ExpireFromRound);
            }
            return true;
        }

        /// <summary>The two halves a target describes, or null when the cut is not legal: no card,
        /// a one-cube block, a pick that is empty or everything, cubes that are not on the card, or
        /// a half that would fall into loose pieces.</summary>
        private static List<GridPos>[] SplitOf(RoundContext ctx, ActivationTarget target)
        {
            RoundEngine round = ctx != null ? ctx.Round : null;
            if (round == null || !target.HandIndex.HasValue || target.CellSet == null
                || target.HandIndex.Value < 0 || target.HandIndex.Value >= round.Hand.Count)
            {
                return null;
            }
            BlockShape shape = round.EffectiveShape(round.Hand[target.HandIndex.Value]);
            if (shape.Size < 2)
            {
                return null; // a single cube cannot be cut in two
            }
            var all = new List<GridPos>(shape.Cells);
            var first = new List<GridPos>();
            foreach (GridPos picked in target.CellSet)
            {
                if (!Holds(all, picked) || Holds(first, picked))
                {
                    return null; // not on this card, or picked twice
                }
                first.Add(picked);
            }
            var second = new List<GridPos>();
            foreach (GridPos cell in all)
            {
                if (!Holds(first, cell))
                {
                    second.Add(cell);
                }
            }
            if (first.Count == 0 || second.Count == 0)
            {
                return null; // that is not a cut, that is the same card
            }
            if (!IsConnected(first) || !IsConnected(second))
            {
                return null;
            }
            return new[] { first, second };
        }

        private static bool Holds(List<GridPos> cells, GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].X == cell.X && cells[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Flood-fills a cell set to prove it is one piece.</summary>
        internal static bool IsConnected(List<GridPos> cells)
        {
            if (cells.Count <= 1)
            {
                return true;
            }
            var seen = new List<GridPos> { cells[0] };
            var frontier = new List<GridPos> { cells[0] };
            while (frontier.Count > 0)
            {
                GridPos at = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                var neighbours = new[]
                {
                    new GridPos(at.X + 1, at.Y), new GridPos(at.X - 1, at.Y),
                    new GridPos(at.X, at.Y + 1), new GridPos(at.X, at.Y - 1)
                };
                foreach (GridPos next in neighbours)
                {
                    if (Holds(cells, next) && !Holds(seen, next))
                    {
                        seen.Add(next);
                        frontier.Add(next);
                    }
                }
            }
            return seen.Count == cells.Count;
        }
    }

    /// <summary>
    /// "Lehimleme" - weld two cards in your hand into one. You place the second against the first
    /// yourself, so which monster you end up with is your decision; both originals are gone.
    ///
    /// The join must TOUCH and must not OVERLAP - a weld is a weld. ROUND-SCOPED: the welded card
    /// is a bonus card and expires with the round, so the two blocks are back in your deck next
    /// round.
    /// </summary>
    public sealed class LehimlemePower : Power
    {
        public LehimlemePower()
            : base("lehimleme", "Lehimleme")
        {
            SetDescription(
                "Weld two cards in your hand into one - you decide where the second sits against "
                    + "the first. It lasts the round; both blocks are back in your deck next "
                    + "round.",
                "Elindeki iki kartı tek karta lehimle - ikincisinin birinciye nereden "
                    + "yapışacağına sen karar verirsin. Sadece o raunt için; iki blok da bir "
                    + "sonraki raunt destende yine ayrı.");
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.TwoHandCards; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return WeldOf(ctx, target) != null;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            List<GridPos> welded = WeldOf(ctx, target);
            if (welded == null)
            {
                return false;
            }
            RoundEngine round = ctx.Round;
            int first = target.HandIndex.Value;
            int second = target.SecondHandIndex.Value;
            // Both elements travel into the weld, distinct, so a fire block soldered to a gold one
            // makes something that is both.
            var elements = new List<BlockElement>();
            AddElements(elements, round.Hand[first]);
            AddElements(elements, round.Hand[second]);

            // Take the higher index out first, or the lower one shifts under us.
            round.TakeCardOutOfRound(first > second ? first : second);
            round.TakeCardOutOfRound(first > second ? second : first);
            BlockCard card = ctx.Session.CreateCard(BlockShape.FromCells(welded), elements);
            round.AddBonusCard(card, BonusPlayOutcome.ExpireFromRound);
            return true;
        }

        private static void AddElements(List<BlockElement> into, BlockCard card)
        {
            foreach (BlockElement element in card.Elements)
            {
                if (!into.Contains(element))
                {
                    into.Add(element);
                }
            }
        }

        /// <summary>The welded cell set, or null when the join is illegal: two different cards are
        /// needed, they must touch, and they must not overlap.</summary>
        private static List<GridPos> WeldOf(RoundContext ctx, ActivationTarget target)
        {
            RoundEngine round = ctx != null ? ctx.Round : null;
            if (round == null || !target.HandIndex.HasValue || !target.SecondHandIndex.HasValue
                || !target.Offset.HasValue)
            {
                return null;
            }
            int a = target.HandIndex.Value;
            int b = target.SecondHandIndex.Value;
            if (a == b || a < 0 || b < 0 || a >= round.Hand.Count || b >= round.Hand.Count)
            {
                return null;
            }
            // The first card's cells, kept SEPARATE: adjacency has to be judged against those
            // alone. Measuring against a list the second card is being added to would let it
            // count as touching ITSELF, and any join at all would pass.
            var firstCells = new List<GridPos>(round.EffectiveShape(round.Hand[a]).Cells);
            var welded = new List<GridPos>(firstCells);
            GridPos offset = target.Offset.Value;
            bool touches = false;
            foreach (GridPos cell in round.EffectiveShape(round.Hand[b]).Cells)
            {
                var moved = new GridPos(cell.X + offset.X, cell.Y + offset.Y);
                if (Holds(firstCells, moved))
                {
                    return null; // overlap: a weld cannot put two cubes in one place
                }
                if (Touches(firstCells, moved))
                {
                    touches = true;
                }
                welded.Add(moved);
            }
            return touches ? welded : null;
        }

        private static bool Holds(List<GridPos> cells, GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].X == cell.X && cells[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Touches(List<GridPos> cells, GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                int distance = System.Math.Abs(cells[i].X - cell.X)
                    + System.Math.Abs(cells[i].Y - cell.Y);
                if (distance == 1)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// "Gen nakli" - take the element out of a cube on the board and put it into a card in your
    /// hand. The cube goes plain, the card goes elemental.
    ///
    /// AND IT IS A LOAN, not a gift (confirmed design): when the card reaches the discard both go
    /// back to what they were - the card is plain again in your deck, and the cube on the board
    /// gets its element back if it is still standing. Play the card while you hold the gene, or
    /// give it back.
    /// </summary>
    public sealed class GenNakliPower : Power
    {
        public GenNakliPower()
            : base("gen_nakli", "Gen Nakli")
        {
            SetDescription(
                "Move the element out of a cube on the board and into a card in your hand - the "
                    + "cube goes plain, the card goes elemental. It is a LOAN: when the card hits "
                    + "the discard both go back to what they were.",
                "Tahtadaki bir küpün elementini elindeki bir karta aktar - küp elementsiz kalır, "
                    + "kart elementli olur. Bu bir ÖDÜNÇ: kart ıskartaya çıkınca ikisi de eski "
                    + "hâline döner.");
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.CellAndHandCard; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ElementUnder(ctx, target).HasValue;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            BlockElement? gene = ElementUnder(ctx, target);
            if (!gene.HasValue)
            {
                return false;
            }
            RoundEngine round = ctx.Round;
            BlockCard card = round.Hand[target.HandIndex.Value];
            return round.TransplantElement(target.Cell.Value, card, gene.Value);
        }

        /// <summary>The element the targeted cube carries, or null when there is nothing to move:
        /// no cube, a plain one, or a card that already carries an element of its own.</summary>
        private static BlockElement? ElementUnder(RoundContext ctx, ActivationTarget target)
        {
            RoundEngine round = ctx != null ? ctx.Round : null;
            if (round == null || !target.Cell.HasValue || !target.HandIndex.HasValue
                || target.HandIndex.Value < 0 || target.HandIndex.Value >= round.Hand.Count)
            {
                return null;
            }
            Cube? cube = round.Board.GetCube(target.Cell.Value);
            if (!cube.HasValue)
            {
                return null;
            }
            BlockElement? gene = ElementOf(cube.Value.Kind);
            if (!gene.HasValue)
            {
                return null; // a plain cube has no gene to give
            }
            return round.Hand[target.HandIndex.Value].Elements.Count > 0 ? null : gene;
        }

        /// <summary>The element a cube kind came from, or null for a kind no card can carry.</summary>
        internal static BlockElement? ElementOf(CubeKind kind)
        {
            switch (kind)
            {
                case CubeKind.Fire: return BlockElement.Fire;
                case CubeKind.Water: return BlockElement.Water;
                case CubeKind.Obsidian: return BlockElement.Obsidian;
                case CubeKind.Gold: return BlockElement.Gold;
                case CubeKind.Transparent: return BlockElement.Transparent;
                case CubeKind.Dynamite: return BlockElement.Dynamite;
                default: return null;
            }
        }
    }

    /// <summary>
    /// "Hidrolik pres" - squeeze a 2x2 patch of arena into one cell. Three cells of room, right
    /// now, for four turns; on the fifth it lets go and wants them back.
    ///
    /// While it is shut the pressed cube is an ordinary cube that happens to be worth FOUR when it
    /// breaks - so the clean way to use the power is to squeeze, use the room, and clear the press
    /// itself before it opens.
    ///
    /// Letting go is where the danger is, and the rules are the designer's (see GameBoard.Press):
    /// it pushes what is in the way outward and scores for whatever goes over the edge; obsidian
    /// and gold cannot be pushed, so a side holding one is not a direction and it opens the other
    /// way; and if no direction is open at all it DETONATES, taking the surrounding gold and
    /// obsidian with it and paying nothing.
    /// </summary>
    public sealed class HidrolikPresPower : Power
    {
        /// <summary>Turns it stays shut. It lets go at the end of the turn after these.</summary>
        public int TurnsCompressed = 4;

        /// <summary>Score per cube shoved off the board when it opens.</summary>
        public int BonusPerCubePushedOff = 30;

        private GridPos anchor;
        private Cube?[] swallowed;
        private int turnsLeft;

        public HidrolikPresPower()
            : base("hidrolik_pres", "Hidrolik Pres")
        {
            SetDescription(
                "Squeezes a 2x2 patch of the arena into ONE cell for four turns - three cells of "
                    + "room, right now. Break the pressed cube while it is shut and it pays for "
                    + "four. On the fifth turn it opens: what is in the way is shoved outward and "
                    + "whatever goes over the edge scores. Obsidian and gold will not budge, so it "
                    + "opens the other way - and if it cannot open at all, it detonates and takes "
                    + "them with it for nothing.",
                "Oyun alanından 2x2'lik bir parçayı 4 tur boyunca TEK kareye sıkıştırır - anında "
                    + "üç kare yer. Sıkışıkken o küpü patlatırsan dört küp değerinde puan verir. "
                    + "5. turda açılır: önündekileri dışarı ittirir ve kenardan taşan küpler puan "
                    + "getirir. Obsidyen ve altın itilemez, o yüzden diğer yöne açılır - hiç "
                    + "açılamazsa patlar ve onları da götürür, ama puan vermez.");
        }

        /// <summary>True while a patch is squeezed shut.</summary>
        public bool IsPressing
        {
            get { return swallowed != null; }
        }

        /// <summary>Where the press is, for the UI.</summary>
        public GridPos Anchor
        {
            get { return anchor; }
        }

        public int TurnsLeft
        {
            get { return turnsLeft; }
        }

        public override string StatusText
        {
            get
            {
                return IsPressing
                    ? Loc.Pick("opens in " + turnsLeft, turnsLeft + " tur sonra açılır")
                    : null;
            }
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.BoardArea; }
        }

        /// <summary>The four cells it would squeeze, for the UI preview.</summary>
        public override IReadOnlyList<GridPos> PreviewCells(ActivationTarget target)
        {
            var cells = new List<GridPos>();
            if (!target.Cell.HasValue)
            {
                return cells;
            }
            GridPos at = target.Cell.Value;
            cells.Add(at);
            cells.Add(new GridPos(at.X + 1, at.Y));
            cells.Add(new GridPos(at.X, at.Y + 1));
            cells.Add(new GridPos(at.X + 1, at.Y + 1));
            return cells;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // The board is new, so any press on the old one went with it.
            swallowed = null;
            turnsLeft = 0;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return !IsPressing && ctx != null && ctx.Round != null && target.Cell.HasValue
                && ctx.Round.Board.CanCompressAt(target.Cell.Value);
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (!CanRun(ctx, target))
            {
                return false;
            }
            anchor = target.Cell.Value;
            swallowed = ctx.Round.MainBoard.Compress(anchor);
            if (swallowed == null)
            {
                return false;
            }
            turnsLeft = TurnsCompressed;
            return true;
        }

        /// <summary>Counts the press down and lets it go on the turn after the last one. Also
        /// notices when the pressed cube was destroyed - then there is nothing left to open.
        /// </summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            if (!IsPressing || turn.Round == null)
            {
                return;
            }
            Cube? here = turn.Round.MainBoard.GetCube(anchor);
            if (!here.HasValue || here.Value.Kind != CubeKind.Compressed)
            {
                swallowed = null; // broken while shut: the player took the four-cube payout
                turnsLeft = 0;
                return;
            }
            turnsLeft--;
            if (turnsLeft > 0)
            {
                return;
            }
            PressExpansion result = turn.Round.ReleasePress(anchor, swallowed);
            swallowed = null;
            if (result != null && !result.Detonated && result.CubesPushedOff > 0)
            {
                turn.AddFlatScore(result.CubesPushedOff * BonusPerCubePushedOff, DefId);
            }
        }
    }
}
