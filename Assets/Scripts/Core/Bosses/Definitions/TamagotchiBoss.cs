// PURPOSE: "Tamagotchi" - a pet that eats your CARDS, for good. It asks for TWO specific cards, and
// you have until the draw pile next runs dry to hand them over. Feed it and it asks again with the
// fresh deck. Leave it hungry and it goes BERSERK and helps itself (designer's call, 2026-09-30 -
// the first version asked for four SHAPES, cost nothing that lasted, topped the hand back up and
// simply lost the round when unfed, which made it a chore rather than a boss).
//
// WHAT IT ASKS FOR: two cards drawn from the ones ALIVE in the round (hand, draw pile, discard) and
// OWNED by the run, weighted toward the VALUABLE - an elemental block is four times as likely as a
// plain one, a two-element weld five times - so it USUALLY wants something that hurts to give, and
// sometimes it does not. A demand is a CARD, not a shape: the cheap copy of the same outline is
// not what it asked for. Picked once each, so a demand can always be met by a card that exists.
//
// FEEDING IT COSTS FOR GOOD. The card leaves the run deck (GameSession.RemoveOwnedCardForGood,
// with the tax bosses' floor: never below the hand size) and the hand is NOT topped up - the slot
// stays empty until the next placement refills it (RoundEngine.FeedCardToBoss). It still costs no
// turn.
//
// LEFT HUNGRY, IT GOES BERSERK. When the deck runs dry with anything still owed, the round is not
// lost; the pet rampages, once, at the end of that turn (the boss moves last, BEFORE the dead-end
// check - so what it does can decide the round) and then asks for two fresh cards. It rampages at
// the END of the turn rather than on the spot because the drying-out is raised mid-draw, before
// the discard is shuffled back in: only after the refill does the round know which cards the
// player holds and which ones come next, and the board bite is aimed at exactly those. One of:
//   BOARD       it eats CellsEatenPerRampage empty cells, chosen by analysis
//               (RoundEngine.ChooseCellsToStarve): the bites that squeeze the held and coming
//               cards hardest, never the last way out. They go dead for the round.
//   COLLECTION  it eats one of your JOKERS or POWERS, permanently, the valuable ones likelier. A
//               joker that may not be sold (a credit card with a debt on it) is never taken.
//   DECK        it eats cards out of the DRAW PILE, permanently, on an APPETITE: every card has a
//               worth (a plain block 1, an elemental one 5, a weld 6) and the pet eats until
//               RampageDeckAppetite is spent - so ONE valuable card satisfies it, or it chews
//               through up to five worthless ones. Always at least one card, never below the
//               deck floor.
// Which of the three is a weighted pick among those that have something to take.
//
// THE LOSS IS GONE. LossReason.PetWentHungry is kept for old saves and never raised now.
//
// A boss is round-scoped and may not touch session state - with ONE declared exception, the deck
// taxes, which this joins: taking cards, jokers and powers out of the run IS its effect.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    public enum PetRampageKind
    {
        Board = 0,
        Collection = 1,
        Deck = 2
    }

    /// <summary>What a berserk pet took, for the View. Reporting only, a new object per rampage.
    /// </summary>
    public sealed class PetRampageVisuals
    {
        public PetRampageKind Kind;

        /// <summary>BOARD: the cells it ate, in the order it chose them.</summary>
        public readonly List<GridPos> Cells = new List<GridPos>();

        /// <summary>COLLECTION: the joker or power it ate (one of the two).</summary>
        public string JokerDefId;
        public string PowerDefId;
        public string EatenName;

        /// <summary>DECK: the cards it ate out of the draw pile.</summary>
        public readonly List<int> CardIds = new List<int>();
        public readonly List<BlockShape> CardShapes = new List<BlockShape>();
        public readonly List<int> CardElementCounts = new List<int>();

        /// <summary>How many things it ate, whatever they were.</summary>
        public int Count
        {
            get
            {
                switch (Kind)
                {
                    case PetRampageKind.Board: return Cells.Count;
                    case PetRampageKind.Collection: return EatenName != null ? 1 : 0;
                    default: return CardIds.Count;
                }
            }
        }
    }

    /// <summary>"Tamagotchi" - feed it two cards before the deck runs out, or it feeds itself.</summary>
    public sealed class TamagotchiBoss : BossRound
    {
        /// <summary>Cards it asks for each time it gets hungry.</summary>
        public int DemandSize = 2;

        /// <summary>How much likelier an elemental card is to be demanded than a plain one.</summary>
        public int ValuableDemandWeight = 4;

        /// <summary>BOARD rampage: the empty cells it eats.</summary>
        public int CellsEatenPerRampage = 2;

        /// <summary>DECK rampage: the appetite it eats down, in card worth (a plain block is 1).
        /// </summary>
        public int RampageDeckAppetite = 5;

        /// <summary>Weights of the three rampages, among those that have something to take.</summary>
        public int BoardRampageWeight = 4;
        public int CollectionRampageWeight = 3;
        public int DeckRampageWeight = 3;

        /// <summary>Tests and the lab: always this rampage (-1 = the weighted pick).</summary>
        public int ForcedRampage = -1;

        /// <summary>The cards still owed, by id. Emptied by feeding, refilled when it gets hungry.
        /// </summary>
        private readonly List<int> demandedIds = new List<int>();

        /// <summary>Their shapes, same order, so the UI can show what is owed without a round to
        /// look the cards up in.</summary>
        private readonly List<BlockShape> demandedShapes = new List<BlockShape>();

        private int mealsEaten;
        private int feedingsMissed;
        private bool rampagePending;
        private int rampages;

        /// <summary>The last rampage, for the View: a new object each time, never saved.</summary>
        [field: NotSaved]
        public PetRampageVisuals LastRampage { get; private set; }

        public TamagotchiBoss()
            : base("tamagotchi", "Tamagotchi")
        {
            SetDescription(
                "It asks for two of your CARDS - usually valuable ones - and you have until the "
                    + "draw pile next runs dry to feed them to it. What it eats is gone for good, "
                    + "and the hand does not refill until you play. Leave it hungry and it goes "
                    + "berserk: it eats part of the board, a joker or power, or cards from your deck.",
                "Senden iki KART ister - genelde değerli olanları - ve onları vermek için çekme "
                    + "destesi bitene kadar vaktin var. Yediği kart sonsuza dek gider ve el, sen "
                    + "bir blok oynayana kadar dolmaz. Aç bırakırsan çıldırır: tahtanın bir kısmını, "
                    + "bir jokerini ya da gücünü, ya da destenden kartlar yer.");
        }

        /// <summary>The SHAPES still owed, for the UI to draw.</summary>
        public IReadOnlyList<BlockShape> Demands
        {
            get { return demandedShapes; }
        }

        /// <summary>The CARDS still owed, by id, same order as Demands.</summary>
        public IReadOnlyList<int> DemandedCardIds
        {
            get { return demandedIds; }
        }

        /// <summary>The owed cards themselves, found in the round (hand, draw pile, discard), same
        /// order as Demands - so the UI can show what they are MADE of, which is the point: the
        /// pet usually wants the valuable ones. A card that is no longer in the round is skipped.
        /// </summary>
        public List<BlockCard> DemandedCards(RoundEngine round)
        {
            var found = new List<BlockCard>();
            if (round == null)
            {
                return found;
            }
            for (int i = 0; i < demandedIds.Count; i++)
            {
                BlockCard card = FindInRound(round, demandedIds[i]);
                if (card != null)
                {
                    found.Add(card);
                }
            }
            return found;
        }

        /// <summary>Cards fed to it this round, for the UI.</summary>
        public int MealsEaten
        {
            get { return mealsEaten; }
        }

        /// <summary>Times it went berserk this round.</summary>
        public int Rampages
        {
            get { return rampages; }
        }

        /// <summary>True between a missed deadline and the end of the turn it rampages at.</summary>
        public bool RampagePending
        {
            get { return rampagePending; }
        }

        public override string StatusText
        {
            get
            {
                if (rampagePending)
                {
                    return Loc.Pick("BERSERK", "ÇILDIRDI");
                }
                return demandedIds.Count == 0
                    ? Loc.Pick("fed", "doydu")
                    : Loc.Pick("wants ", "istiyor: ") + demandedIds.Count;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            ClearDemands();
            mealsEaten = 0;
            feedingsMissed = 0;
            rampagePending = false;
            rampages = 0;
            LastRampage = null;
            Demand(ctx.Session, ctx.Round, ctx.Rng);
        }

        /// <summary>
        /// The deck has run out: the deadline and the next mealtime. Anything still owed - and
        /// still in the round to be fed - makes the pet go berserk at the end of this turn; either
        /// way it asks for two fresh cards now, from the cards alive this moment (the discard is
        /// about to be shuffled back in, so every one of them can still be reached).
        /// </summary>
        public override void OnDrawPileEmptied(RoundContext ctx)
        {
            RoundEngine round = ctx.Round;
            if (round == null)
            {
                return;
            }
            if (StillOwed(round) > 0)
            {
                feedingsMissed++;
                rampagePending = true;
            }
            Demand(ctx.Session, round, ctx.Rng);
        }

        /// <summary>The boss moves last: a pending rampage happens here, after the refill and
        /// before the dead-end check.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            if (!rampagePending || turn.Round == null)
            {
                return;
            }
            rampagePending = false;
            Rampage(turn.Session, turn.Round, turn.Rng);
        }

        /// <summary>
        /// Hands the pet the card in <paramref name="handIndex"/>. Returns false - and changes
        /// nothing - when the pet is not hungry or that card is not one it asked for.
        /// Goes through RoundEngine.FeedPet, which is what the UI calls; the rules are here.
        /// </summary>
        internal bool TryFeed(RoundEngine round, int handIndex)
        {
            if (round == null || demandedIds.Count == 0 || handIndex < 0
                || handIndex >= round.Hand.Count)
            {
                return false;
            }
            int match = demandedIds.IndexOf(round.Hand[handIndex].Id);
            if (match < 0)
            {
                return false;
            }
            RemoveDemandAt(match);
            mealsEaten++;
            round.FeedCardToBoss(handIndex);
            return true;
        }

        /// <summary>True if this held card is something the pet would accept right now. The UI
        /// asks so it can offer the card, and so it can grey out the ones that are no use.</summary>
        public bool Accepts(RoundEngine round, BlockCard card)
        {
            return round != null && card != null && demandedIds.Contains(card.Id);
        }

        // ------------------------------------------------------------------ demands

        /// <summary>
        /// Asks for a fresh pair, drawn from the cards ALIVE in the round and OWNED by the run,
        /// weighted toward the valuable. Never more than the deck can lose above its floor.
        /// </summary>
        private void Demand(GameSession session, RoundEngine round, IRandomSource rng)
        {
            ClearDemands();
            if (session == null || round == null || rng == null)
            {
                return;
            }
            var owned = new HashSet<int>();
            foreach (BlockCard card in session.OwnedCards)
            {
                owned.Add(card.Id);
            }
            var alive = new List<BlockCard>();
            for (int i = 0; i < round.Hand.Count; i++)
            {
                alive.Add(round.Hand[i]);
            }
            alive.AddRange(round.Deck.DrawPile);
            alive.AddRange(round.Deck.DiscardPile);
            alive.RemoveAll(c => c == null || !owned.Contains(c.Id));
            int spare = session.OwnedCards.Count - session.Config.Rules.HandSize;
            int wanted = DemandSize < spare ? DemandSize : spare;
            for (int i = 0; i < wanted && alive.Count > 0; i++)
            {
                int pick = WeightedPick(alive, DemandWeight, rng);
                BlockCard card = alive[pick];
                alive.RemoveAt(pick);
                demandedIds.Add(card.Id);
                demandedShapes.Add(round.EffectiveShape(card));
            }
        }

        private int DemandWeight(BlockCard card)
        {
            int elements = card.Elements.Count;
            return elements == 0 ? 1 : ValuableDemandWeight + (elements - 1);
        }

        /// <summary>Demands that can still be met: the card is still somewhere in the round. One
        /// that left by other means (a tax, an expiry) is not the player's debt.</summary>
        private int StillOwed(RoundEngine round)
        {
            int owed = 0;
            for (int i = 0; i < demandedIds.Count; i++)
            {
                if (InRound(round, demandedIds[i]))
                {
                    owed++;
                }
            }
            return owed;
        }

        private static bool InRound(RoundEngine round, int cardId)
        {
            return FindInRound(round, cardId) != null;
        }

        private static BlockCard FindInRound(RoundEngine round, int cardId)
        {
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == cardId)
                {
                    return round.Hand[i];
                }
            }
            foreach (BlockCard card in round.Deck.DrawPile)
            {
                if (card.Id == cardId)
                {
                    return card;
                }
            }
            foreach (BlockCard card in round.Deck.DiscardPile)
            {
                if (card.Id == cardId)
                {
                    return card;
                }
            }
            return null;
        }

        private void ClearDemands()
        {
            demandedIds.Clear();
            demandedShapes.Clear();
        }

        private void RemoveDemandAt(int index)
        {
            demandedIds.RemoveAt(index);
            demandedShapes.RemoveAt(index);
        }

        // ------------------------------------------------------------------ berserk

        private void Rampage(GameSession session, RoundEngine round, IRandomSource rng)
        {
            if (session == null || rng == null)
            {
                return;
            }
            List<Joker> jokers = EdibleJokers(session);
            IReadOnlyList<Power> powers = session.Powers.Powers;
            bool board = CellsEatenPerRampage > 0;
            bool collection = jokers.Count > 0 || powers.Count > 0;
            bool deck = round.Deck.DrawPile.Count > 0
                && session.OwnedCards.Count > session.Config.Rules.HandSize;
            int kind = ForcedRampage;
            if (kind < 0)
            {
                int wb = board ? BoardRampageWeight : 0;
                int wc = collection ? CollectionRampageWeight : 0;
                int wd = deck ? DeckRampageWeight : 0;
                int total = wb + wc + wd;
                if (total <= 0)
                {
                    return;
                }
                int roll = rng.NextInt(0, total);
                kind = roll < wb ? (int)PetRampageKind.Board
                    : roll < wb + wc ? (int)PetRampageKind.Collection
                    : (int)PetRampageKind.Deck;
            }
            var report = new PetRampageVisuals { Kind = (PetRampageKind)kind };
            switch ((PetRampageKind)kind)
            {
                case PetRampageKind.Board:
                    report.Cells.AddRange(round.EatCellsForGood(round.ChooseCellsToStarve(CellsEatenPerRampage)));
                    break;
                case PetRampageKind.Collection:
                    EatFromCollection(session, jokers, powers, rng, report);
                    break;
                default:
                    EatFromDeck(session, round, rng, report);
                    break;
            }
            rampages++;
            LastRampage = report;
        }

        /// <summary>The jokers it may take: every one the player could sell right now.</summary>
        private static List<Joker> EdibleJokers(GameSession session)
        {
            var edible = new List<Joker>();
            foreach (Joker joker in session.Jokers.Jokers)
            {
                if (session.Jokers.CanSell(joker))
                {
                    edible.Add(joker);
                }
            }
            return edible;
        }

        /// <summary>One joker or power, the pricier ones likelier - weighted by what the market
        /// charges for its rarity.</summary>
        private static void EatFromCollection(GameSession session, List<Joker> jokers,
            IReadOnlyList<Power> powers, IRandomSource rng, PetRampageVisuals report)
        {
            MarketConfig market = session.Config.Market;
            var weights = new List<int>();
            for (int i = 0; i < jokers.Count; i++)
            {
                weights.Add(System.Math.Max(1, market.JokerBuyPrice(RarityTable.For(jokers[i].DefId))));
            }
            for (int i = 0; i < powers.Count; i++)
            {
                weights.Add(System.Math.Max(1, market.PowerBuyPrice(RarityTable.For(powers[i].DefId))));
            }
            if (weights.Count == 0)
            {
                return;
            }
            int pick = WeightedIndex(weights, rng);
            if (pick < jokers.Count)
            {
                Joker eaten = jokers[pick];
                if (session.Jokers.Remove(eaten))
                {
                    report.JokerDefId = eaten.DefId;
                    report.EatenName = eaten.DisplayName;
                }
                return;
            }
            Power power = powers[pick - jokers.Count];
            if (session.Powers.Remove(power))
            {
                report.PowerDefId = power.DefId;
                report.EatenName = power.DisplayName;
            }
        }

        /// <summary>
        /// Cards out of the draw pile, permanently, until the appetite is spent: a valuable card
        /// satisfies it on its own, worthless ones are counted for less so it takes more of them.
        /// Always at least one, never below the deck floor, the valuable likelier every bite.
        /// </summary>
        private void EatFromDeck(GameSession session, RoundEngine round, IRandomSource rng,
            PetRampageVisuals report)
        {
            var owned = new HashSet<int>();
            foreach (BlockCard card in session.OwnedCards)
            {
                owned.Add(card.Id);
            }
            var menu = new List<BlockCard>();
            foreach (BlockCard card in round.Deck.DrawPile)
            {
                if (owned.Contains(card.Id))
                {
                    menu.Add(card);
                }
            }
            int appetite = RampageDeckAppetite;
            bool first = true;
            while (menu.Count > 0 && (first || appetite > 0))
            {
                // After the first bite only what still fits the appetite is on the menu.
                if (!first)
                {
                    int left = appetite;
                    menu.RemoveAll(c => Worth(c) > left);
                    if (menu.Count == 0)
                    {
                        break;
                    }
                }
                int pick = WeightedPick(menu, DemandWeight, rng);
                BlockCard card = menu[pick];
                menu.RemoveAt(pick);
                if (!session.RemoveOwnedCardForGood(card))
                {
                    break; // the deck is at its floor
                }
                report.CardIds.Add(card.Id);
                report.CardShapes.Add(card.Shape);
                report.CardElementCounts.Add(card.Elements.Count);
                appetite -= Worth(card);
                first = false;
            }
        }

        /// <summary>What a card is worth to the pet's appetite: a plain block 1, an elemental one
        /// 5, one more for every further element.</summary>
        internal static int Worth(BlockCard card)
        {
            int elements = card.Elements.Count;
            return elements == 0 ? 1 : 4 + elements;
        }

        private static int WeightedPick(List<BlockCard> cards, System.Func<BlockCard, int> weight,
            IRandomSource rng)
        {
            var weights = new List<int>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                weights.Add(System.Math.Max(1, weight(cards[i])));
            }
            return WeightedIndex(weights, rng);
        }

        private static int WeightedIndex(List<int> weights, IRandomSource rng)
        {
            int total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                total += weights[i];
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
