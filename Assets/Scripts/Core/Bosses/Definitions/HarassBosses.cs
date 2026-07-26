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
    /// still there, still scores, still blocks: they simply cannot see it.
    ///
    /// The only relief is an explosion. The blast lights its own surroundings for a moment and
    /// the board sinks back into the dark, so the one way to see anything is to make something
    /// happen - and the more you clear, the more you learn.
    ///
    /// This is the ONE boss that bends no rule at all. Every other query on BossRound exists
    /// because the engine had to behave differently; this one exists because the SCREEN does.
    /// The engine reads HidesTheBoard once, to hand it to the View, and nothing else.
    /// </summary>
    public sealed class AlacakaranlikBoss : BossRound
    {
        public AlacakaranlikBoss()
            : base("alacakaranlik", "Alacakaranlık")
        {
            SetDescription(
                "The board goes dark and you play blind. Only an explosion lights its own "
                    + "surroundings, for a moment, before the dark closes back over them.",
                "Oyun alanı karanlığa gömülür, körleme oynarsın. Sadece bir patlama kendi "
                    + "etrafını bir anlığına aydınlatır, sonra karanlık üstünü tekrar örter.");
        }

        public override bool HidesTheBoard
        {
            get { return true; }
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
        /// <summary>Turns the player gets to reach the bar.</summary>
        public int TurnLimit = 12;

        public SaatciBoss()
            : base("saatci", "Saatçi")
        {
            SetDescription(
                "You have a hard turn limit. Reach the score threshold inside it or the round is "
                    + "lost - there is no stalling this one out.",
                "Kesin bir tur sınırın var. Puan eşiğini o sınırın içinde geç, yoksa raunt "
                    + "kaybedilir - bunu oyalanarak geçemezsin.");
        }

        /// <summary>Turns left before the deadline, for the UI. Never below 0.</summary>
        public int TurnsLeft { get; private set; }

        public override string StatusText
        {
            get { return TurnsLeft + Loc.Pick(" turns left", " tur kaldı"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
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
    /// </summary>
    public sealed class KitlikBoss : BossRound
    {
        private int cubesGrown;

        public KitlikBoss()
            : base("kitlik", "Kıtlık")
        {
            SetDescription(
                "Every card that comes back from the discard grows by one cube, somewhere at "
                    + "random. Play on long enough and your whole deck is too fat to fit.",
                "Iskartadan desteye dönen her kart rastgele bir yerinden bir küp büyür. Yeterince "
                    + "uzun oynarsan bütün desten tahtaya sığmayacak kadar şişer.");
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
