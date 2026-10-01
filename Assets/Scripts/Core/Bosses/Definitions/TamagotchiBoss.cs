// PURPOSE: "Tamagotchi" - a pet that eats your CARDS, for good. At the start of the round it asks
// for TWO specific cards and you have until the draw pile first runs dry to feed them to it. Fed in
// full it is SATISFIED for the rest of the round. Left hungry it goes FURIOUS for the rest of the
// round - it stops asking ("I am not asking any more, I will take it myself"), destroys its own
// requests, and at that deadline and every later one it TAKES something: a bite of the board, a
// joker or a power, or cards off a pile (designer's calls, 2026-09-30 and 2026-10-01).
//
// WHAT IT ASKS FOR. Two cards alive in the round (hand, draw pile, discard) and OWNED by the run,
// WEIGHTED by how much each is worth to the player right now (ValueScore: its elements, its size,
// how rare its shape is in the round, whether it was bought, whether it is in the hand or about to
// be drawn). The expensive ones are likelier, never certain: "this creature likes expensive things".
// A request is a CARD, not a shape - the cheap copy of the same outline is not what it asked for.
// The requests are fixed SLOTS: a fed one stays in its slot, eaten, so the UI keeps two plates.
//
// FEEDING IT COSTS FOR GOOD. The card leaves the run deck (GameSession.RemoveOwnedCardForGood,
// never below the hand size) and the hand is NOT topped up - the slot stays empty until the next
// placement refills it (RoundEngine.FeedCardToBoss). It costs no turn.
//
// THE DEADLINE runs down as the draw pile is drawn; HungerProgress / Stage are the rules' own
// reading of it (Calm -> Hungry -> Impatient -> Angry), so the View acts out exactly what the
// rules mean.
//
// THE PUNISH PLANNER (Rampage) is never a dice roll between punishments. It builds every
// candidate - a BOARD bite (RoundEngine.ChooseCellsToStarve: the connected bite that squeezes the
// held and coming cards hardest without ever eating the last way out), a JOKER, a POWER, a meal off
// the DRAW pile, a meal off the DISCARD pile - scores how much each would hurt (Pressure 0..1) and
// takes the cruellest, a few percent of seeded jitter keeping it from being fully predictable. Each
// candidate's own target is chosen with the same value bias: the expensive joker likelier, never
// certain. A pile meal runs on an APPETITE by tier - one HIGH value card satisfies it, MEDIUM ones
// count double a LOW one, LOW ones are a snack of up to five.
//
// It rampages at the END of the turn the deck ran dry in (the boss moves last, before the dead-end
// check), because the drying-out is raised mid-draw, before the discard is shuffled back in: only
// after the refill does the round know which cards the player holds and which come next.
//
// A boss is round-scoped and may not touch session state - with ONE declared exception, the deck
// taxes, which this joins: taking cards, jokers and powers out of the run IS its effect.
// LossReason.PetWentHungry is kept for old saves and never raised now.
// EXTENSION POINT: every number below is a balance placeholder.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Tamagotchi" - feed it two cards before the deck runs out, or it feeds itself.</summary>
    public sealed class TamagotchiBoss : BossRound
    {
        /// <summary>Cards it asks for at the start of the round.</summary>
        public int DemandSize = 2;

        /// <summary>BOARD punish: the empty cells one bite takes (one connected region).</summary>
        public int CellsEatenPerRampage = 3;

        /// <summary>How many coming cards (the top of the draw pile) the board bite reads.</summary>
        public int LookaheadDraws = 3;

        /// <summary>Value score at or above which a card is HIGH, and MEDIUM.</summary>
        public int HighValueScore = 50;
        public int MediumValueScore = 25;

        /// <summary>A pile meal's appetite, and what a card of each tier costs it.</summary>
        public int RampageDeckAppetite = 5;
        public int LowWorth = 1;
        public int MediumWorth = 2;
        public int HighWorth = 5;

        /// <summary>Percent of the deadline gone at which it turns Hungry, Impatient, Angry.</summary>
        public int HungryAtPercent = 30;
        public int ImpatientAtPercent = 55;
        public int AngryAtPercent = 80;

        /// <summary>Seeded jitter on each candidate's pressure, in percent, so the cruellest choice
        /// is never fully predictable. 0 = always the cruellest.</summary>
        public int PunishJitterPercent = 8;

        /// <summary>Tests and the lab: always this punish (a PetPunishKind; -1 = the planner).</summary>
        public int ForcedRampage = -1;

        /// <summary>The request slots: the card each asks for, its shape and tier, and whether it
        /// has been fed. Fixed for the round.</summary>
        private readonly List<int> requestIds = new List<int>();
        private readonly List<BlockShape> requestShapes = new List<BlockShape>();
        private readonly List<int> requestTiers = new List<int>();
        private readonly List<bool> requestFed = new List<bool>();

        private bool furious;
        private bool furyToAnnounce;
        private int furyMissing;
        private bool rampagePending;
        private int drawPileAtDemand;
        private int lastStage;
        private int mealsEaten;
        private int feedingsMissed;
        private int rampages;

        /// <summary>The cards it has eaten from the requests, by slot, so a fed plate can still be
        /// drawn with the card that went into it. Presentation only, not saved.</summary>
        [NotSaved]
        private readonly Dictionary<int, BlockCard> fedCards = new Dictionary<int, BlockCard>();

        /// <summary>The last feed, hunger change, fury and rampage, for the View - a new object
        /// each, never saved.</summary>
        [field: NotSaved]
        public TamagotchiFeedVisuals LastFeed { get; private set; }

        [field: NotSaved]
        public TamagotchiHungerChange LastHungerChange { get; private set; }

        [field: NotSaved]
        public TamagotchiFuryVisuals LastFury { get; private set; }

        [field: NotSaved]
        public PetRampageVisuals LastRampage { get; private set; }

        public TamagotchiBoss()
            : base("tamagotchi", "Tamagotchi")
        {
            SetDescription(
                "It asks for two of your CARDS - usually valuable ones - and you have until the "
                    + "draw pile first runs dry to feed them to it. What it eats is gone for good, "
                    + "and the hand does not refill until you play. Leave it hungry and it goes "
                    + "furious for the rest of the round: it takes part of the board, a joker or "
                    + "power, or cards from your deck - wherever it hurts most.",
                "Senden iki KART ister - genelde değerli olanları - ve onları vermek için çekme "
                    + "destesi ilk bitene kadar vaktin var. Yediği kart sonsuza dek gider ve el, "
                    + "sen bir blok oynayana kadar dolmaz. Aç bırakırsan raundun geri kalanında "
                    + "öfkelenir: tahtanın bir parçasını, bir jokerini ya da gücünü, ya da destenden "
                    + "kartlar alır - en çok acıtacak yerden.");
        }

        // ================================================================== what the View reads

        /// <summary>How many request slots this round has (fed or not).</summary>
        public int RequestCount
        {
            get { return requestIds.Count; }
        }

        /// <summary>One request slot: the card (null once nothing in the round can show it), its
        /// tier and whether it was fed.</summary>
        public PetRequest Request(RoundEngine round, int slot)
        {
            var request = new PetRequest { SlotId = slot };
            if (slot < 0 || slot >= requestIds.Count)
            {
                return request;
            }
            request.Tier = (CardValueTier)requestTiers[slot];
            request.Fed = requestFed[slot];
            BlockCard eaten;
            request.Card = request.Fed && fedCards.TryGetValue(slot, out eaten)
                ? eaten : FindInRound(round, requestIds[slot]);
            return request;
        }

        /// <summary>The shape each slot asked for, same order - what the UI falls back to when the
        /// card itself cannot be found (a fed card after a load).</summary>
        public IReadOnlyList<BlockShape> RequestShapes
        {
            get { return requestShapes; }
        }

        /// <summary>The ids of the cards still owed (not fed), in slot order.</summary>
        public IReadOnlyList<int> DemandedCardIds
        {
            get
            {
                var pending = new List<int>();
                if (!furious)
                {
                    for (int i = 0; i < requestIds.Count; i++)
                    {
                        if (!requestFed[i])
                        {
                            pending.Add(requestIds[i]);
                        }
                    }
                }
                return pending;
            }
        }

        /// <summary>The shapes still owed, for anything that only wants the outline.</summary>
        public IReadOnlyList<BlockShape> Demands
        {
            get
            {
                var pending = new List<BlockShape>();
                if (!furious)
                {
                    for (int i = 0; i < requestIds.Count; i++)
                    {
                        if (!requestFed[i])
                        {
                            pending.Add(requestShapes[i]);
                        }
                    }
                }
                return pending;
            }
        }

        /// <summary>The cards still owed, found in the round, in slot order.</summary>
        public List<BlockCard> DemandedCards(RoundEngine round)
        {
            var found = new List<BlockCard>();
            IReadOnlyList<int> pending = DemandedCardIds;
            for (int i = 0; i < pending.Count; i++)
            {
                BlockCard card = FindInRound(round, pending[i]);
                if (card != null)
                {
                    found.Add(card);
                }
            }
            return found;
        }

        /// <summary>Cards fed to it this round.</summary>
        public int MealsEaten
        {
            get { return mealsEaten; }
        }

        /// <summary>Times it punished this round.</summary>
        public int Rampages
        {
            get { return rampages; }
        }

        /// <summary>True once the deadline passed with something owed: for the rest of the round
        /// it asks for nothing and takes instead.</summary>
        public bool Furious
        {
            get { return furious; }
        }

        /// <summary>True once every request has been fed.</summary>
        public bool Satisfied
        {
            get
            {
                if (furious || requestIds.Count == 0)
                {
                    return false;
                }
                for (int i = 0; i < requestFed.Count; i++)
                {
                    if (!requestFed[i])
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>True between a missed deadline and the end of the turn it punishes at.</summary>
        public bool RampagePending
        {
            get { return rampagePending; }
        }

        /// <summary>How much of the deadline is gone, 0..1: the share of the draw pile drawn since
        /// it asked. 1 once it is furious; 0 when it is satisfied or asked for nothing.</summary>
        public float HungerProgress(RoundEngine round)
        {
            if (furious)
            {
                return 1f;
            }
            if (round == null || requestIds.Count == 0 || Satisfied || drawPileAtDemand <= 0)
            {
                return 0f;
            }
            float left = round.Deck.DrawPile.Count / (float)drawPileAtDemand;
            float progress = 1f - left;
            return progress < 0f ? 0f : progress > 1f ? 1f : progress;
        }

        /// <summary>The pet's mood as the rules mean it.</summary>
        public PetHungerStage Stage(RoundEngine round)
        {
            if (furious)
            {
                return PetHungerStage.Furious;
            }
            if (Satisfied)
            {
                return PetHungerStage.Satisfied;
            }
            if (requestIds.Count == 0)
            {
                return PetHungerStage.Calm;
            }
            float percent = HungerProgress(round) * 100f;
            return percent >= AngryAtPercent ? PetHungerStage.Angry
                : percent >= ImpatientAtPercent ? PetHungerStage.Impatient
                : percent >= HungryAtPercent ? PetHungerStage.Hungry
                : PetHungerStage.Calm;
        }

        public override string StatusText
        {
            get
            {
                if (furious)
                {
                    return Loc.Pick("FURIOUS", "ÖFKELİ");
                }
                if (Satisfied)
                {
                    return Loc.Pick("fed", "doydu");
                }
                return Loc.Pick("wants ", "istiyor: ") + DemandedCardIds.Count;
            }
        }

        // ================================================================== the round

        public override void OnRoundStarted(RoundContext ctx)
        {
            requestIds.Clear();
            requestShapes.Clear();
            requestTiers.Clear();
            requestFed.Clear();
            fedCards.Clear();
            furious = false;
            furyToAnnounce = false;
            furyMissing = 0;
            rampagePending = false;
            mealsEaten = 0;
            feedingsMissed = 0;
            rampages = 0;
            lastStage = (int)PetHungerStage.Calm;
            LastFeed = null;
            LastHungerChange = null;
            LastFury = null;
            LastRampage = null;
            Demand(ctx.Session, ctx.Round, ctx.Rng);
        }

        /// <summary>
        /// The deck has run dry. The FIRST time, it is the deadline: anything still owed - and
        /// still in the round to be fed - makes it furious for the rest of the round. Every time
        /// while it is furious, it punishes at the end of this turn.
        /// </summary>
        public override void OnDrawPileEmptied(RoundContext ctx)
        {
            RoundEngine round = ctx.Round;
            if (round == null)
            {
                return;
            }
            if (furious)
            {
                rampagePending = true;
                return;
            }
            int owed = StillOwed(round);
            if (owed > 0)
            {
                furious = true;
                furyToAnnounce = true;
                furyMissing = owed;
                feedingsMissed++;
                rampagePending = true;
            }
        }

        /// <summary>The boss moves last: the hunger is read, and a pending fury and punish happen
        /// here, after the refill and before the dead-end check.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            if (round == null)
            {
                return;
            }
            NoteStage(round);
            if (!rampagePending)
            {
                return;
            }
            rampagePending = false;
            PetRampageVisuals report = Rampage(turn.Session, round, turn.Rng);
            if (furyToAnnounce)
            {
                furyToAnnounce = false;
                LastFury = new TamagotchiFuryVisuals
                {
                    MissingFeedCount = furyMissing,
                    PunishKind = report != null ? report.Kind : PetPunishKind.Board
                };
            }
            if (report != null)
            {
                rampages++;
                LastRampage = report;
            }
        }

        /// <summary>Writes a hunger-change report when the stage moved.</summary>
        private void NoteStage(RoundEngine round)
        {
            PetHungerStage stage = Stage(round);
            if ((int)stage == lastStage)
            {
                return;
            }
            LastHungerChange = new TamagotchiHungerChange
            {
                Previous = (PetHungerStage)lastStage,
                Stage = stage,
                Progress = HungerProgress(round)
            };
            lastStage = (int)stage;
        }

        /// <summary>
        /// Hands the pet the card in <paramref name="handIndex"/>. Returns false - and changes
        /// nothing - when it is not one it asked for (or it no longer asks). Goes through
        /// RoundEngine.FeedPet, which is what the UI calls; the rules are here.
        /// </summary>
        internal bool TryFeed(RoundEngine round, int handIndex)
        {
            if (round == null || furious || handIndex < 0 || handIndex >= round.Hand.Count)
            {
                return false;
            }
            BlockCard card = round.Hand[handIndex];
            int slot = -1;
            for (int i = 0; i < requestIds.Count; i++)
            {
                if (!requestFed[i] && requestIds[i] == card.Id)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                return false;
            }
            requestFed[slot] = true;
            fedCards[slot] = card;
            mealsEaten++;
            round.FeedCardToBoss(handIndex);
            int remaining = 0;
            for (int i = 0; i < requestFed.Count; i++)
            {
                if (!requestFed[i])
                {
                    remaining++;
                }
            }
            LastFeed = new TamagotchiFeedVisuals
            {
                RequestSlotId = slot,
                Card = card,
                Tier = (CardValueTier)requestTiers[slot],
                HandSlotIndex = handIndex,
                RequestsRemaining = remaining
            };
            NoteStage(round);
            return true;
        }

        /// <summary>True if this held card is one it would take right now. The UI asks so it can
        /// react to a card being carried toward it.</summary>
        public bool Accepts(RoundEngine round, BlockCard card)
        {
            if (round == null || card == null || furious)
            {
                return false;
            }
            for (int i = 0; i < requestIds.Count; i++)
            {
                if (!requestFed[i] && requestIds[i] == card.Id)
                {
                    return true;
                }
            }
            return false;
        }

        // ================================================================== value

        /// <summary>
        /// How much a card is worth to the player right now: its elements (the most), its size,
        /// how rare its shape is among the cards alive in the round, whether it cost money, and
        /// whether it is in the hand or about to be drawn. The single definition every choice here
        /// reads - what it asks for, what it eats off a pile, and the tier the View dresses a bite
        /// in.
        /// </summary>
        public int ValueScore(BlockCard card, RoundEngine round)
        {
            if (card == null)
            {
                return 0;
            }
            int score = 0;
            int elements = card.Elements.Count;
            // An elemental block is ALWAYS high: the smallest one clears HighValueScore by itself.
            if (elements > 0)
            {
                score += 46 + 10 * (elements - 1);
            }
            score += 4 * card.Shape.Size;
            if (card.IsPurchased)
            {
                score += 6;
            }
            if (round != null)
            {
                int copies = 0;
                string key = card.Shape.CanonicalKey;
                foreach (BlockCard other in AliveCards(round))
                {
                    if (other.Shape.CanonicalKey == key)
                    {
                        copies++;
                    }
                }
                score += copies <= 1 ? 14 : copies == 2 ? 7 : 0;
                if (InHand(round, card.Id))
                {
                    score += 8;
                }
                else if (InComingDraws(round, card.Id))
                {
                    score += 5;
                }
            }
            return score;
        }

        public CardValueTier TierOf(BlockCard card, RoundEngine round)
        {
            int score = ValueScore(card, round);
            return score >= HighValueScore ? CardValueTier.High
                : score >= MediumValueScore ? CardValueTier.Medium
                : CardValueTier.Low;
        }

        /// <summary>What a card of this tier costs a pile meal's appetite.</summary>
        public int WorthOf(CardValueTier tier)
        {
            return tier == CardValueTier.High ? HighWorth
                : tier == CardValueTier.Medium ? MediumWorth
                : LowWorth;
        }

        // ================================================================== requests

        /// <summary>Asks for the round's cards, drawn from the ones ALIVE in the round and OWNED by
        /// the run, weighted by ValueScore. Never more than the deck can lose above its floor.</summary>
        private void Demand(GameSession session, RoundEngine round, IRandomSource rng)
        {
            if (session == null || round == null || rng == null)
            {
                return;
            }
            var owned = OwnedIds(session);
            var alive = new List<BlockCard>();
            foreach (BlockCard card in AliveCards(round))
            {
                if (owned.Contains(card.Id))
                {
                    alive.Add(card);
                }
            }
            int spare = session.OwnedCards.Count - session.Config.Rules.HandSize;
            int wanted = DemandSize < spare ? DemandSize : spare;
            var weights = new List<int>(alive.Count);
            for (int i = 0; i < alive.Count; i++)
            {
                weights.Add(System.Math.Max(4, ValueScore(alive[i], round)));
            }
            for (int i = 0; i < wanted && alive.Count > 0; i++)
            {
                int pick = WeightedIndex(weights, rng);
                BlockCard card = alive[pick];
                requestIds.Add(card.Id);
                requestShapes.Add(round.EffectiveShape(card));
                requestTiers.Add((int)TierOf(card, round));
                requestFed.Add(false);
                alive.RemoveAt(pick);
                weights.RemoveAt(pick);
            }
            drawPileAtDemand = round.Deck.DrawPile.Count;
        }

        /// <summary>Requests that can still be met: unfed, and the card still somewhere in the
        /// round. One that left by other means (a tax, an expiry) is not the player's debt.</summary>
        private int StillOwed(RoundEngine round)
        {
            int owed = 0;
            for (int i = 0; i < requestIds.Count; i++)
            {
                if (!requestFed[i] && FindInRound(round, requestIds[i]) != null)
                {
                    owed++;
                }
            }
            return owed;
        }

        // ================================================================== the punish planner

        private PetRampageVisuals Rampage(GameSession session, RoundEngine round, IRandomSource rng)
        {
            if (session == null || round == null || rng == null)
            {
                return null;
            }
            var report = new PetRampageVisuals();
            int lookahead = LookaheadDraws;

            // BOARD: the connected bite that squeezes the player hardest and still leaves an exit.
            int fittingBefore;
            float roomBefore = round.PetRoomNow(lookahead, out fittingBefore);
            List<GridPos> bite = round.ChooseCellsToStarve(CellsEatenPerRampage, lookahead, report.CellScores);
            var board = new PetPunishCandidate { Kind = PetPunishKind.Board, Valid = bite.Count > 0 };
            if (board.Valid)
            {
                GameBoard trial = GameBoard.CreateClone(round.MainBoard);
                trial.MarkDead(bite);
                int fittingAfter;
                float roomAfter = round.PetRoom(trial, lookahead, out fittingAfter);
                board.Pressure = roomBefore > 0f ? Clamp01((roomBefore - roomAfter) / roomBefore * 2.2f) : 0f;
                board.HardLockRisk = fittingBefore > 0 ? Clamp01(1f - fittingAfter / (float)fittingBefore) : 1f;
                board.Label = bite.Count + " cell(s)";
            }
            report.Candidates.Add(board);

            // JOKER and POWER: a target each, value-biased, and how much it would hurt to lose.
            Joker joker = PickJoker(session, rng, out float jokerPressure);
            report.Candidates.Add(new PetPunishCandidate
            {
                Kind = PetPunishKind.Joker,
                Valid = joker != null,
                Pressure = jokerPressure,
                Label = joker != null ? joker.DisplayName : null
            });
            Power power = PickPower(session, rng, out float powerPressure);
            report.Candidates.Add(new PetPunishCandidate
            {
                Kind = PetPunishKind.Power,
                Valid = power != null,
                Pressure = powerPressure,
                Label = power != null ? power.DisplayName : null
            });

            // THE PILES: the meal each would make, and what it is worth.
            List<BlockCard> drawMeal = PlanMeal(session, round, round.Deck.DrawPile, rng, out float drawPressure, true);
            report.Candidates.Add(new PetPunishCandidate
            {
                Kind = PetPunishKind.DrawPile,
                Valid = drawMeal.Count > 0,
                Pressure = drawPressure,
                Label = drawMeal.Count + " card(s)"
            });
            List<BlockCard> discardMeal = PlanMeal(session, round, round.Deck.DiscardPile, rng, out float discardPressure, false);
            report.Candidates.Add(new PetPunishCandidate
            {
                Kind = PetPunishKind.DiscardPile,
                Valid = discardMeal.Count > 0,
                Pressure = discardPressure,
                Label = discardMeal.Count + " card(s)"
            });

            // The cruellest, a few percent of seeded jitter keeping it from being fully predictable.
            PetPunishCandidate chosen = null;
            if (ForcedRampage >= 0)
            {
                foreach (PetPunishCandidate c in report.Candidates)
                {
                    if ((int)c.Kind == ForcedRampage && c.Valid)
                    {
                        chosen = c;
                    }
                }
            }
            if (chosen == null)
            {
                float best = float.MinValue;
                foreach (PetPunishCandidate c in report.Candidates)
                {
                    if (!c.Valid)
                    {
                        continue;
                    }
                    float jitter = PunishJitterPercent > 0
                        ? (rng.NextInt(0, 2 * PunishJitterPercent + 1) - PunishJitterPercent) / 100f
                        : 0f;
                    float score = c.Pressure * (1f + jitter);
                    if (score > best)
                    {
                        best = score;
                        chosen = c;
                    }
                }
            }
            if (chosen == null)
            {
                return null;
            }
            report.Kind = chosen.Kind;
            report.Seed = (uint)(rng.NextInt(1, int.MaxValue));
            switch (chosen.Kind)
            {
                case PetPunishKind.Board:
                    report.Cells.AddRange(round.EatCellsForGood(bite));
                    SetBounds(report);
                    break;
                case PetPunishKind.Joker:
                    report.InventoryIndex = IndexOf(session.Jokers.Jokers, joker);
                    if (session.Jokers.Remove(joker))
                    {
                        report.DefId = joker.DefId;
                        report.EatenName = joker.DisplayName;
                        report.InstanceId = joker.InstanceId;
                        report.Tier = TierOfRarity(RarityTable.For(joker.DefId));
                    }
                    break;
                case PetPunishKind.Power:
                    report.InventoryIndex = IndexOf(session.Powers.Powers, power);
                    if (session.Powers.Remove(power))
                    {
                        report.DefId = power.DefId;
                        report.EatenName = power.DisplayName;
                        report.InstanceId = power.InstanceId;
                        report.Tier = TierOfRarity(RarityTable.For(power.DefId));
                    }
                    break;
                case PetPunishKind.DrawPile:
                    Eat(session, round, round.Deck.DrawPile, drawMeal, report);
                    break;
                default:
                    Eat(session, round, round.Deck.DiscardPile, discardMeal, report);
                    break;
            }
            return report;
        }

        /// <summary>The joker it would take: one the player could sell right now, the rarer and the
        /// harder-working likelier. Its pressure is what losing it would cost.</summary>
        private Joker PickJoker(GameSession session, IRandomSource rng, out float pressure)
        {
            pressure = 0f;
            var edible = new List<Joker>();
            long totalPaid = 0;
            foreach (Joker joker in session.Jokers.Jokers)
            {
                if (session.Jokers.CanSell(joker))
                {
                    edible.Add(joker);
                    totalPaid += System.Math.Max(0L, joker.ProcPoints);
                }
            }
            if (edible.Count == 0)
            {
                return null;
            }
            var pressures = new List<float>(edible.Count);
            var weights = new List<int>(edible.Count);
            foreach (Joker joker in edible)
            {
                float rarity = RarityShare(RarityTable.For(joker.DefId));
                float activity = totalPaid > 0 ? System.Math.Max(0L, joker.ProcPoints) / (float)totalPaid : 0f;
                float p = Clamp01(0.25f + 0.45f * rarity + 0.30f * activity);
                pressures.Add(p);
                weights.Add(1 + (int)(p * p * 100f));
            }
            int pick = WeightedIndex(weights, rng);
            pressure = pressures[pick];
            return edible[pick];
        }

        /// <summary>The power it would take, the rarer and the charged likelier.</summary>
        private Power PickPower(GameSession session, IRandomSource rng, out float pressure)
        {
            pressure = 0f;
            IReadOnlyList<Power> powers = session.Powers.Powers;
            if (powers.Count == 0)
            {
                return null;
            }
            var pressures = new List<float>(powers.Count);
            var weights = new List<int>(powers.Count);
            foreach (Power power in powers)
            {
                float p = Clamp01(0.2f + 0.45f * RarityShare(RarityTable.For(power.DefId)) + (power.Charged ? 0.15f : 0f));
                pressures.Add(p);
                weights.Add(1 + (int)(p * p * 100f));
            }
            int pick = WeightedIndex(weights, rng);
            pressure = pressures[pick];
            return powers[pick];
        }

        /// <summary>
        /// The meal a pile would make: owned cards off it, the valuable likelier every bite, until
        /// the appetite is spent - one HIGH card satisfies it, MEDIUM ones cost twice a LOW one.
        /// Always at least one card, never below the deck floor. Its pressure is what the meal is
        /// worth to the player (and, off the draw pile, whether it takes cards about to be drawn).
        /// </summary>
        private List<BlockCard> PlanMeal(GameSession session, RoundEngine round,
            IReadOnlyList<BlockCard> pile, IRandomSource rng, out float pressure, bool drawPile)
        {
            pressure = 0f;
            var meal = new List<BlockCard>();
            int room = session.OwnedCards.Count - session.Config.Rules.HandSize;
            if (room <= 0 || pile.Count == 0)
            {
                return meal;
            }
            var owned = OwnedIds(session);
            var menu = new List<BlockCard>();
            foreach (BlockCard card in pile)
            {
                if (owned.Contains(card.Id))
                {
                    menu.Add(card);
                }
            }
            int appetite = RampageDeckAppetite;
            int value = 0;
            bool comingTaken = false;
            while (menu.Count > 0 && meal.Count < room && (meal.Count == 0 || appetite > 0))
            {
                if (meal.Count > 0)
                {
                    int left = appetite;
                    menu.RemoveAll(c => WorthOf(TierOf(c, round)) > left);
                    if (menu.Count == 0)
                    {
                        break;
                    }
                }
                var weights = new List<int>(menu.Count);
                for (int i = 0; i < menu.Count; i++)
                {
                    weights.Add(System.Math.Max(4, ValueScore(menu[i], round)));
                }
                int pick = WeightedIndex(weights, rng);
                BlockCard card = menu[pick];
                menu.RemoveAt(pick);
                meal.Add(card);
                appetite -= WorthOf(TierOf(card, round));
                value += ValueScore(card, round);
                if (drawPile && InComingDraws(round, card.Id))
                {
                    comingTaken = true;
                }
            }
            pressure = Clamp01(value / 160f + (comingTaken ? 0.1f : 0f));
            return meal;
        }

        private void Eat(GameSession session, RoundEngine round, IReadOnlyList<BlockCard> pile,
            List<BlockCard> meal, PetRampageVisuals report)
        {
            report.CountBefore = pile.Count;
            foreach (BlockCard card in meal)
            {
                CardValueTier tier = TierOf(card, round);
                if (!session.RemoveOwnedCardForGood(card))
                {
                    break; // the deck is at its floor
                }
                report.Cards.Add(card);
                report.CardTiers.Add(tier);
            }
            report.CountAfter = pile.Count;
        }

        // ================================================================== helpers

        private static void SetBounds(PetRampageVisuals report)
        {
            if (report.Cells.Count == 0)
            {
                return;
            }
            report.MinX = report.MaxX = report.Cells[0].X;
            report.MinY = report.MaxY = report.Cells[0].Y;
            foreach (GridPos cell in report.Cells)
            {
                report.MinX = System.Math.Min(report.MinX, cell.X);
                report.MaxX = System.Math.Max(report.MaxX, cell.X);
                report.MinY = System.Math.Min(report.MinY, cell.Y);
                report.MaxY = System.Math.Max(report.MaxY, cell.Y);
            }
        }

        private static float RarityShare(Rarity rarity)
        {
            return rarity == Rarity.Legendary ? 1f : rarity == Rarity.Rare ? 0.55f : 0.2f;
        }

        private static CardValueTier TierOfRarity(Rarity rarity)
        {
            return rarity == Rarity.Legendary ? CardValueTier.High
                : rarity == Rarity.Rare ? CardValueTier.Medium
                : CardValueTier.Low;
        }

        private static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    return i;
                }
            }
            return -1;
        }

        private static HashSet<int> OwnedIds(GameSession session)
        {
            var owned = new HashSet<int>();
            foreach (BlockCard card in session.OwnedCards)
            {
                owned.Add(card.Id);
            }
            return owned;
        }

        private static IEnumerable<BlockCard> AliveCards(RoundEngine round)
        {
            for (int i = 0; i < round.Hand.Count; i++)
            {
                yield return round.Hand[i];
            }
            foreach (BlockCard card in round.Deck.DrawPile)
            {
                yield return card;
            }
            foreach (BlockCard card in round.Deck.DiscardPile)
            {
                yield return card;
            }
        }

        private static bool InHand(RoundEngine round, int cardId)
        {
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }

        private bool InComingDraws(RoundEngine round, int cardId)
        {
            IReadOnlyList<BlockCard> draw = round.Deck.DrawPile;
            int coming = LookaheadDraws;
            for (int i = draw.Count - 1; i >= 0 && coming > 0; i--, coming--)
            {
                if (draw[i].Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }

        private static BlockCard FindInRound(RoundEngine round, int cardId)
        {
            if (round == null)
            {
                return null;
            }
            foreach (BlockCard card in AliveCards(round))
            {
                if (card.Id == cardId)
                {
                    return card;
                }
            }
            return null;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        private static int WeightedIndex(List<int> weights, IRandomSource rng)
        {
            int total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                total += weights[i];
            }
            if (total <= 0)
            {
                return 0;
            }
            int roll = rng.NextInt(0, total);
            for (int i = 0; i < weights.Count; i++)
            {
                if (roll < weights[i])
                {
                    return i;
                }
                roll -= weights[i];
            }
            return weights.Count - 1;
        }
    }
}
