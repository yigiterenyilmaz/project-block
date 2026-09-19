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

        /// <summary>
        /// What it paid THIS TURN, for the animation. Written where the payment is made and
        /// nowhere else, so the picture cannot pay a different number - or pay twice for a turn
        /// the rules only paid once for (see RebateVisuals).
        /// </summary>
        [field: NotSaved]
        public RebateVisuals LastRebate { get; private set; }

        [NotSaved]
        private int rebateSerial;

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
            // The receipt is written HERE, beside the payment, which is what keeps the two the
            // same number and the same count. One turn, one payment, one serial.
            LastRebate = new RebateVisuals
            {
                Serial = ++rebateSerial,
                // SCREEN points: the receipt is read next to the score label, and it used to
                // print the logical number (+6 for a payout of 60).
                Payout = PointsPerEmptyDrawPile * (turn.Score.ScoreScale < 1 ? 1 : turn.Score.ScoreScale),
                TimesThisRound = TriggeredThisRound,
                ThresholdPassed = turn.Round.ThresholdPassed
            };
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

        /// <summary>
        /// It keeps statistics, and for a MULTIPLIER they are the only way to know what it is
        /// worth. "+%14 to everything" is the rate, not the return: the same rate is worth
        /// nothing on a weak turn and a great deal on a strong one, so the rate alone cannot tell
        /// the player whether thinning the deck has paid for the cards they sold. The tally
        /// answers that, and the two numbers sit side by side - the rate on the card, the points
        /// it has actually added in the tooltip.
        /// </summary>
        public override bool TracksProcs
        {
            get { return true; }
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

        /// <summary>
        /// WHAT THE MULTIPLIER WAS ACTUALLY WORTH THIS TURN. Measured here rather than in
        /// ModifyScore because a multiplier's value is not knowable when it is applied: jokers
        /// dispatch in inventory order, so the flat bonuses and the multipliers that land AFTER
        /// this one are part of what it ends up lifting. By AfterTurnScored the engine has
        /// finalized the score and the breakdown is settled.
        ///
        /// The arithmetic is the breakdown's own, run twice - once with the turn's multiplier as
        /// it stands and once with this joker's factor divided back out - and the difference is
        /// the points that exist only because this card is held. Late flats are deliberately
        /// outside it: the engine does not multiply them, so claiming them would be a lie. Kept
        /// in the LOGICAL economy, like every other ProcPoints, and the UI scales it on the way
        /// out.
        ///
        /// THE FACTOR IS READ BACK OFF THE CONTRIBUTION rather than recomputed, so "Terslik"
        /// needs no special case: the boss turns the multiplier into its reciprocal inside
        /// AddMultiplier, and reading what was really applied makes the tally correctly report a
        /// LOSS on those turns instead of the bonus it did not give.
        ///
        /// It counts WITHOUT a flash (the turn-less NoteProc): this joker lifts nearly every turn
        /// in the run, and a card that strobes the bar every single turn stops meaning anything -
        /// the proc light is for events, and a passive rate is not one.
        /// </summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            ScoreBreakdown score = turn.Score;
            if (score.Total <= 0)
            {
                return; // the turn paid nothing, so nothing here lifted anything
            }
            double factor = 1.0;
            System.Collections.Generic.IReadOnlyList<ScoreContribution> entries =
                score.Contributions;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Source == DefId && entries[i].Multiplier != 1.0)
                {
                    factor *= entries[i].Multiplier;
                }
            }
            if (factor == 1.0)
            {
                return;
            }
            double multiplied = score.BaseTotal * score.RegularScoreFactor
                + score.BaseOvertimeBonus + score.FlatBonus;
            double gained = multiplied * score.Multiplier
                - multiplied * (score.Multiplier / factor);
            int extra = (int)System.Math.Round(gained);
            if (extra != 0)
            {
                NoteProc(extra);
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

        /// <summary>
        /// THE HARDEST JOKER IN THE GAME TO PUT A NUMBER ON, and the reason it keeps statistics
        /// is that a player cannot possibly keep them by eye. It has no hook that fires and it
        /// pays nothing itself: it flips ONE rule, and the points that follow land in the base
        /// line and sweep fields, in a power's echo, in a fire chain - credited to nobody. So
        /// "Genel temizlik" is a card you could hold for a whole run with no idea whether it had
        /// ever been worth its slot.
        ///
        /// The engine answers that (RoundEngine.ExternalScoreCredited): every payment that exists
        /// ONLY because the switch is on is credited there, and this reads the difference at the
        /// end of each turn. A firing is therefore "the switch paid for something this turn",
        /// which is the event the player would call a proc, and the tally is what the switch has
        /// been worth.
        /// </summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        /// <summary>How much of the engine's meter this joker has already counted. Saved (no
        /// [NotSaved]) so a run resumed mid-round cannot count the same points twice.</summary>
        private int creditSeen;

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
            // The meter lives on the RoundEngine and so restarts with every round. What this
            // joker remembers has to restart with it, or the first turn of round two would look
            // like the switch had gone backwards.
            creditSeen = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            CollectCredit(turn);
        }

        /// <summary>A between-turn payment (a power's echo, a board-clear with no placement
        /// resolving) that the round ended on would otherwise never be counted - the turn that
        /// would have collected it never comes.</summary>
        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            CollectCredit(null, ctx.Round);
        }

        private void CollectCredit(TurnContext turn)
        {
            CollectCredit(turn, turn != null ? turn.Round : null);
        }

        private void CollectCredit(TurnContext turn, RoundEngine round)
        {
            if (round == null)
            {
                return;
            }
            int credited = round.ExternalScoreCredited;
            // A LOADED run, or a fresh engine, restarts the meter below what was last seen. Take
            // the meter's word for it rather than counting the whole of it again.
            if (credited < creditSeen)
            {
                creditSeen = credited;
                return;
            }
            int gained = credited - creditSeen;
            if (gained <= 0)
            {
                return;
            }
            creditSeen = credited;
            NoteProc(gained, turn);
        }
    }

    /// <summary>"Kolay para" - placing blocks pays. Base placement scores nothing on its own
    /// (ScoringConfig.PointsPerCubePlaced is 0, held for exactly this), so this joker is what
    /// turns cubes placed into points: every cube of the block you play this turn adds a flat
    /// bonus, which the multiplier stage still lifts.
    ///
    /// THE BONUS IS A SHARE OF THE ROUND'S BAR (RoundEngine.ScoreThreshold, read live so a boss
    /// that lowers the bar lowers this too), not a fixed number: a flat +2 was worth a real
    /// chunk of round one and nothing at all by round twelve. A share is usually a FRACTION of
    /// a logical point, so the remainder is carried to the next placement rather than floored
    /// away - over a round it pays exactly its share.</summary>
    public sealed class KolayParaJoker : Joker
    {
        /// <summary>Share of the round's threshold paid per cube placed (0.001 = 0.1%).</summary>
        public double ThresholdSharePerCube = 0.001;

        /// <summary>The fraction of a logical point owed but not yet paid.</summary>
        private double owed;

        /// <summary>What one cube pays right now, in SCREEN points - the tooltip's number.
        /// Refreshed whenever a round starts or a block is scored; [NotSaved] because it is a
        /// display cache, rebuilt the moment the round is touched.</summary>
        [NotSaved]
        private double shownPerCube = -1;

        public KolayParaJoker()
            : base("kolay_para", "Kolay Para")
        {
            SetDescription(
                "Placing a block scores points - every cube you place pays 0.1% of the round's "
                    + "target score.",
                "Blok koymak puan kazandırır - koyduğun her küp, rauntun hedef puanının "
                    + "%0.1'ini kazandırır.");
        }

        public override string StatusText
        {
            get
            {
                if (shownPerCube < 0)
                {
                    return Loc.Pick("0.1% of target/cube", "hedefin %0.1'i/küp");
                }
                string v = shownPerCube.ToString(shownPerCube < 10 ? "0.#" : "0",
                    System.Globalization.CultureInfo.InvariantCulture);
                return Loc.Pick("+" + v + "/cube", "+" + v + "/küp");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Remember(ctx.Round, ctx.Scoring.ScoreScale);
        }

        private void Remember(RoundEngine round, int scale)
        {
            if (round != null)
            {
                shownPerCube = round.ScoreThreshold * ThresholdSharePerCube * scale;
            }
        }

        /// <summary>
        /// It keeps statistics. This is a joker whose whole worth is a small number repeated a
        /// great many times, which is exactly the case a running total answers and a description
        /// cannot: "+2 a cube" says nothing about whether it has earned its slot back, and the
        /// tally does. Printed from ZERO, so a card held through a round that placed nothing
        /// still says so rather than going silent.
        /// </summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public override void ModifyScore(TurnContext turn)
        {
            Remember(turn.Round, turn.Score.ScoreScale);
            int cubes = turn.Report.PlacedCells != null ? turn.Report.PlacedCells.Count : 0;
            if (cubes > 0)
            {
                owed += cubes * turn.Round.ScoreThreshold * ThresholdSharePerCube;
                int bonus = (int)System.Math.Floor(owed);
                owed -= bonus;
                if (bonus <= 0)
                {
                    return;
                }
                turn.Score.AddFlat(bonus, DefId);
                // ONE FIRING PER PLACEMENT, not one per cube: the block is what the player played
                // and the block is what paid. The points are the NOMINAL bonus, like every other
                // joker's - what a later multiplier stage does with it is not this joker's to
                // claim, and what "Terslik" does to it is the boss's doing rather than a firing
                // that did not happen.
                NoteProc(bonus, turn);
            }
        }
    }
}
