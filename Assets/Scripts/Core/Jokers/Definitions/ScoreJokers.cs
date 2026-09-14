// PURPOSE: Two score jokers that react to a board/deck event rather than to the block
// played: bereket (permanent growth) and Harcama bonusu (payout when the draw pile runs
// out).
//
// CONFIRMED RULES:
//  - bereket's "+ shaped explosion" is read as: at least one ROW and at least one COLUMN
//    exploded in the same turn. On a rectangular board those always cross, which is
//    exactly the plus. The growth is PERMANENT while the joker is held: every later turn
//    gets the accumulated bonus, including the turn that triggered the growth.
//  - Harcama bonusu pays whenever a draw attempt finds the pile empty - which can happen
//    several times in a round before the threshold, because the discard is recycled then.
//    It also pays in overtime, where that same event is the loss condition; the points are
//    a consolation, not a rescue.
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"bereket" - every plus-shaped explosion permanently raises the score this
    /// joker adds to every turn.</summary>
    public sealed class BereketJoker : Joker
    {
        /// <summary>Points added to each turn per stack collected.</summary>
        public int PointsPerStack = 5;

        /// <summary>Plus-shaped explosions seen so far. Permanent while the joker is held.</summary>
        public int Stacks { get; private set; }

        public BereketJoker()
            : base("bereket", "Bereket")
        {
            SetDescription(
                "Whenever a row and a column explode in the same turn, your score gains grow permanently.",
                "Aynı turda satır ve sütun birlikte patlarsa kazandığın puan kalıcı olarak artar.");
        }

        public override string StatusText
        {
            get { return Loc.Pick("+" + (Stacks * PointsPerStack) + "/turn", "+" + (Stacks * PointsPerStack) + "/tur"); }
        }

        public override void ModifyScore(TurnContext turn)
        {
            bool plusExplosion = turn.Report.ExplodedRows.Count > 0
                && turn.Report.ExplodedColumns.Count > 0;
            if (plusExplosion)
            {
                Stacks++;
            }
            if (Stacks > 0)
            {
                turn.Score.AddFlat(Stacks * PointsPerStack, DefId);
            }
        }
    }

    /// <summary>"Harcama bonusu" - pays out every time the draw pile runs dry.</summary>
    public sealed class HarcamaBonusuJoker : Joker
    {
        public int PointsPerEmptyDrawPile = 60;

        /// <summary>Times it paid out this round, for the UI.</summary>
        public int TriggeredThisRound { get; private set; }

        public HarcamaBonusuJoker()
            : base("harcama_bonusu", "Harcama Bonusu")
        {
            SetDescription(
                "You gain points every time the draw pile runs out.",
                "Çekme destesi her tükendiğinde puan kazanırsın.");
        }

        public override string StatusText
        {
            get { return Loc.Pick(TriggeredThisRound + " times", TriggeredThisRound + " kez"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            TriggeredThisRound = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (!turn.Report.DrawPileEmptiedThisTurn)
            {
                return;
            }
            TriggeredThisRound++;
            // Granted at end of turn but BEFORE the threshold check, so it can push the
            // round over the line on the very turn the deck ran out.
            turn.AddFlatScore(PointsPerEmptyDrawPile, DefId);
        }
    }

    /// <summary>
    /// "Tutumluluk" - a thin deck pays. Every turn adds a flat bonus for every card the deck is
    /// down on THE DECK YOU STARTED THE RUN WITH, so what it rewards is thinning: sell a card
    /// and the next turn pays more, buy one and it pays less.
    ///
    /// Measured against Config.Deck.Size rather than a fixed number (it used to be a flat 20,
    /// against decks of 18 to 26). An absolute reference means the joker is dead on a big deck
    /// and free money on a small one, and it went quietly dead altogether when the archetypes
    /// were resized - a Classic player had to sell five cards before seeing a single point, which
    /// reads exactly like a joker that does not work.
    ///
    /// It pays as a PERCENTAGE OF THE WHOLE TURN, not as flat points: the bonus goes into the
    /// multiplier stage, so it lifts every source at once - placement, lines, the sweep, gold,
    /// the overtime win bonus and every other joker's flat bonus. That is the joker's whole
    /// character: it is worth nothing on its own and worth more the better the rest of your
    /// build already scores. It is the first joker in the game to use the multiplier stage, so
    /// the composition rule matters - multipliers MULTIPLY together, never overwrite, and
    /// "Terslik" turns this one into the same discount it would have been a bonus.
    ///
    /// Late points (anything added after the score is finalized, via AddFlatScore) are outside
    /// the multiplier stage by the engine's own rule and are not lifted. That is not a special
    /// case for this joker - it is true of every multiplier there will ever be.
    ///
    /// THE CURVE ACCELERATES. Each card gone is worth MORE than the one before it: the n-th card
    /// out pays n+3 percent - +4, +5, +6 ... +15 - so the running total is n(n+7)/2 rather than a
    /// straight line: 4, 9, 15, 22, 30, 39, 49, 60, 72, 85, 99, 114. A flat rate per card makes
    /// the first sale and the twelfth worth exactly the same, which is a joker with no shape:
    /// nothing to commit to and nothing to reach for. Here the last card you can bring yourself
    /// to sell is the one that pays best, and the decision gets harder in the same breath - the
    /// deck you are cutting into is the deck that has to keep refilling your hand.
    ///
    /// THE CEILING IS THE BUILD. Twelve cards is half a 24-card deck and two thirds of an
    /// 18-card one, so a player who reaches the cap has rebuilt their run around this joker and
    /// is playing a deck that can barely refill a hand. A shade over a doubling (x2.14) is what
    /// that costs the deck to buy.
    ///
    /// CAPPED all the same, because thinning is nearly free in money terms: a plain block sells
    /// for nothing, so without a ceiling the curve would keep climbing for every card the deck
    /// could spare. MaxCardsCounted is where it stops.
    /// </summary>
    public sealed class TutumlulukJoker : Joker
    {
        /// <summary>Deck size the bonus is measured DOWN FROM. 0 (the default) means "whatever
        /// deck this run started with"; a positive value overrides that with a fixed number.</summary>
        public int ReferenceDeckSize = 0;

        /// <summary>What the FIRST card the deck is down pays, in percentage points.</summary>
        public int PercentFirstCard = 4;

        /// <summary>How much more each further card pays than the one before it. 1 means the
        /// cards are worth 4%, 5%, 6%, ... in turn, so the running total climbs 4, 9, 15, 22 -
        /// see the class comment. 0 would make it the flat line this deliberately is not, and
        /// this pair with MaxCardsCounted is the whole balance of the joker.</summary>
        public int PercentGrowthPerCard = 1;

        /// <summary>The most cards that may be counted, however thin the deck gets.</summary>
        public int MaxCardsCounted = 12;

        public TutumlulukJoker()
            : base("tutumluluk", "Tutumluluk")
        {
            SetDescription(
                "Every card your deck is down on the one you started with raises ALL your score - "
                    + "every source, not just one - and each card is worth more than the last: "
                    + "+4%, then +5%, then +6%... Up to +114% for twelve cards.",
                "Destendeki kart sayısı, başladığın desteye göre ne kadar azsa TÜM puanın o "
                    + "kadar artar - tek bir kaynak değil, hepsi. Her kart bir öncekinden daha "
                    + "çok değer: +%4, sonra +%5, sonra +%6... On iki kart için en fazla +%114.");
        }

        public override string StatusText
        {
            get
            {
                int percent = PercentFor(lastDeckSize);
                return Loc.Pick("+" + percent + "% to everything", "her şeye +%" + percent);
            }
        }

        /// <summary>The deck this run was dealt, and what it holds now. Both are remembered
        /// rather than read live, because StatusText has no session to ask - which is why the
        /// joker listens to OnDeckChanged as well as scoring: a sale happens in the MARKET, with
        /// no turn resolving, and a badge that only refreshed when a turn scored is what made
        /// this joker look broken.</summary>
        private int startingDeckSize = -1;

        private int lastDeckSize = -1;

        public override void OnAcquired(SessionContext ctx)
        {
            Sample(ctx);
        }

        public override void OnDeckChanged(SessionContext ctx)
        {
            Sample(ctx);
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Sample(ctx);
        }

        public override void ModifyScore(TurnContext turn)
        {
            // TurnContext is not a SessionContext (it carries the same session by hand), so the
            // sampling is spelled out here rather than shared.
            Sample(turn.Session);
            int percent = PercentFor(lastDeckSize);
            if (percent > 0)
            {
                // The multiplier stage, so it lifts the base values, the overtime win bonus and
                // every other joker's flat alike. ModifyScore is the only hook it is legal from
                // - the engine finalizes the score immediately after.
                turn.Score.AddMultiplier(1.0 + percent / 100.0, DefId);
            }
        }

        /// <summary>Reads the collection. The starting size is latched the first time it is seen
        /// so a load (which never re-runs OnAcquired) still knows what the run was dealt.</summary>
        private void Sample(SessionContext ctx)
        {
            Sample(ctx != null ? ctx.Session : null);
        }

        private void Sample(GameSession session)
        {
            if (session == null)
            {
                return;
            }
            if (startingDeckSize < 0)
            {
                startingDeckSize = session.Config.Deck.Size;
            }
            lastDeckSize = session.OwnedCards.Count;
        }

        /// <summary>
        /// How many percentage points the deck is currently worth. The n-th card gone pays
        /// PercentFirstCard + PercentGrowthPerCard * (n - 1), so the total is the arithmetic
        /// series of those - written closed-form rather than looped, which is the same number
        /// and says outright that it is a curve and not a rate.
        /// </summary>
        private int PercentFor(int deckSize)
        {
            int n = CardsUnder(deckSize);
            return n * PercentFirstCard + PercentGrowthPerCard * n * (n - 1) / 2;
        }

        /// <summary>Cards the deck is down on its reference, capped. Never negative: buying past
        /// the deck you started with does not become a penalty.</summary>
        private int CardsUnder(int deckSize)
        {
            if (deckSize < 0)
            {
                return 0;
            }
            int reference = ReferenceDeckSize > 0 ? ReferenceDeckSize : startingDeckSize;
            if (reference <= 0)
            {
                return 0;
            }
            int under = reference - deckSize;
            if (under <= 0)
            {
                return 0;
            }
            return under > MaxCardsCounted ? MaxCardsCounted : under;
        }
    }

    /// <summary>"Genel temizlik" - board-clears caused by a joker or a power between turns
    /// count as real clean sweeps. Normally they do not, because they happen with no placement
    /// resolving; this flips the central RoundRules switch that lets them through.</summary>
    public sealed class GenelTemizlikJoker : Joker
    {
        public GenelTemizlikJoker()
            : base("genel_temizlik", "Genel Temizlik")
        {
            SetDescription(
                "Explosions, line clears and clean sweeps caused by jokers and powers score "
                    + "like your own - sweeps pay the sweep bonus and recharge your powers. "
                    + "Normally they clear the board but pay nothing.",
                "Jokerlerin ve güçlerin yaptığı patlamalar, satır temizlemeleri ve temizlikler "
                    + "de senin yaptığın gibi puan verir - temizlik bonusunu verir ve güçlerini "
                    + "şarj eder. Normalde alanı temizler ama puan vermez.");
        }

        public override void OnAcquired(SessionContext ctx)
        {
            ctx.Rules.CountExternalSweeps = true;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Rules.CountExternalSweeps = false;
        }

        // RoundRules is shared and another effect may have reset it; re-assert each round.
        public override void OnRoundStarted(RoundContext ctx)
        {
            ctx.Rules.CountExternalSweeps = true;
        }
    }

    /// <summary>"Kolay para" - placing blocks pays. Base placement scores nothing on its own
    /// (ScoringConfig.PointsPerCubePlaced is 0, held for exactly this), so this joker is what
    /// turns cubes placed into points: every cube of the block you play this turn adds a flat
    /// bonus, which the multiplier stage still lifts. The small per-cube number is logical -
    /// the global ScoreScale multiplies it up like every other score.</summary>
    public sealed class KolayParaJoker : Joker
    {
        /// <summary>Points per cube placed this turn.</summary>
        public int PointsPerCube = 2;

        public KolayParaJoker()
            : base("kolay_para", "Kolay Para")
        {
            SetDescription(
                "Placing a block scores points - one bonus for every cube you place.",
                "Blok koymak puan kazandırır - koyduğun her küp için bonus.");
        }

        public override string StatusText
        {
            get { return Loc.Pick("+" + PointsPerCube + "/cube", "+" + PointsPerCube + "/küp"); }
        }

        public override void ModifyScore(TurnContext turn)
        {
            int cubes = turn.Report.PlacedCells != null ? turn.Report.PlacedCells.Count : 0;
            if (cubes > 0)
            {
                turn.Score.AddFlat(cubes * PointsPerCube, DefId);
            }
        }
    }
}
