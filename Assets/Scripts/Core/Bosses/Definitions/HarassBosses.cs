// PURPOSE: The three bosses that interfere with the player's turn itself rather than with
// scoring - "Alıkoyma" holds a card back, "Mapus" seals a cell of the board, "Feda" makes a
// bonus card cost the whole hand. All three act from the end-of-turn hook, which is BEFORE
// the dead-end check, so any of them can genuinely finish a round off.
//
// Every one of them re-rolls its victim from ctx.Rng, so a replay of the same seed harasses
// the player in exactly the same order.

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

    /// <summary>"Mapus" - every turn one random empty cell is sealed off: nothing may be
    /// placed on it while the seal lasts. A sealed cell still reads as an empty cell of its
    /// row and column, so the line through it cannot be completed that turn either.</summary>
    public sealed class MapusBoss : BossRound
    {
        /// <summary>Empty cells the board must still have for a seal to be laid, so the very
        /// last hole is never the one taken away.</summary>
        public int MinFreeCells = 2;

        private GridPos sealedCell;
        private bool hasSeal;

        private readonly List<GridPos> freeCells = new List<GridPos>();

        public MapusBoss()
            : base("mapus", "Mapus")
        {
            SetDescription(
                "Every turn it seals off one random empty cell - nothing can be placed there, "
                    + "and the row and column through it cannot be completed either.",
                "Her tur rastgele bir boş hücreyi kapatır - oraya hiçbir şey koyulamaz, o "
                    + "hücreden geçen satır ve sütun da tamamlanamaz.");
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
                return hasSeal
                    ? Loc.Pick("sealed ", "kapalı ") + sealedCell.X + "," + sealedCell.Y
                    : null;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Reseal(ctx.Round, ctx.Rng);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Reseal(turn.Round, turn.Rng);
        }

        /// <summary>Moves the seal to a fresh random empty cell. The old seal is always lifted
        /// first, so exactly one cell is ever sealed by this boss.</summary>
        private void Reseal(RoundEngine round, IRandomSource rng)
        {
            if (round == null)
            {
                return;
            }
            round.ClearBoardSeals();
            hasSeal = false;
            GameBoard board = round.Board;
            freeCells.Clear();
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var pos = new GridPos(x, y);
                    if (board.IsInside(pos) && !board.GetCube(pos).HasValue)
                    {
                        freeCells.Add(pos);
                    }
                }
            }
            if (freeCells.Count < MinFreeCells)
            {
                return;
            }
            sealedCell = freeCells[rng.NextInt(0, freeCells.Count)];
            round.SealBoardCell(sealedCell);
            hasSeal = true;
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
}
