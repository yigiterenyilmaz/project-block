// PURPOSE: "Tamagotchi" - a pet that eats blocks. It asks for FOUR shapes and you have until the
// draw pile next runs dry to hand them over; miss the deadline and the round is lost. Feed it
// and it asks for four more, and the clock starts again with the fresh deck.
//
// What makes it a real cost rather than a chore: a card you feed it LEAVES THE ROUND. It is not
// discarded, it is gone until the next round - so every meal thins the deck you are still trying
// to clear the board with, and shortens the very clock you are racing.
//
// THE MATCH IS ON SHAPE ALONE. What a card is made of does not matter: fire, gold, targeted,
// smuggled - if the outline fits the pet's demand, it is food. Exactly the shape, though;
// nothing is rotated to make it fit, so the demand is asking for a specific block and not a
// family of them. What it asks for is drawn from the cards ALIVE in the round at the moment it
// asks, so a demand can always be met by cards the player actually has somewhere.
//
// Feeding costs no turn (it is not a placement) and the hand tops itself up afterwards, so the
// pet is a tax on the DECK, never on the tempo.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Tamagotchi" - feed it four shapes before the deck runs out.</summary>
    public sealed class TamagotchiBoss : BossRound
    {
        /// <summary>Cards it asks for each time it gets hungry.</summary>
        public int DemandSize = 4;

        /// <summary>The shapes still owed. Emptied by feeding, refilled when it gets hungry.</summary>
        private readonly List<BlockShape> demands = new List<BlockShape>();

        private int mealsEaten;
        private int feedingsMissed;

        public TamagotchiBoss()
            : base("tamagotchi", "Tamagotchi")
        {
            SetDescription(
                "It asks for four block SHAPES and you have until the draw pile next runs dry to "
                    + "hand them over - miss the deadline and the round is lost. What a card is "
                    + "made of does not matter, only its outline. Anything you feed it leaves the "
                    + "round for good.",
                "Dört blok ŞEKLİ ister ve onları vermek için çekme destesi bitene kadar vaktin "
                    + "var - yetiştiremezsen raunt kaybedilir. Kartın türü fark etmez, sadece "
                    + "şekli. Verdiğin her kart o raunt boyunca desteden çıkar.");
        }

        /// <summary>The shapes still owed, for the UI to draw.</summary>
        public IReadOnlyList<BlockShape> Demands
        {
            get { return demands; }
        }

        /// <summary>Cards fed to it this round, for the UI.</summary>
        public int MealsEaten
        {
            get { return mealsEaten; }
        }

        public override string StatusText
        {
            get
            {
                return demands.Count == 0
                    ? Loc.Pick("fed", "doydu")
                    : Loc.Pick("wants ", "istiyor: ") + demands.Count;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            demands.Clear();
            mealsEaten = 0;
            feedingsMissed = 0;
            Demand(ctx.Round, ctx.Rng);
        }

        /// <summary>
        /// The deck has run out, which is both the deadline and the next mealtime. The deadline is
        /// judged FIRST: anything still owed at this moment is a lost round, and only a pet that
        /// was fed gets to ask again.
        /// </summary>
        public override void OnDrawPileEmptied(RoundContext ctx)
        {
            RoundEngine round = ctx.Round;
            if (round == null)
            {
                return;
            }
            if (demands.Count > 0)
            {
                feedingsMissed++;
                round.DeclareLoss(LossReason.PetWentHungry);
                return;
            }
            Demand(round, ctx.Rng);
        }

        /// <summary>
        /// Hands the pet the card in <paramref name="handIndex"/>. Returns false - and changes
        /// nothing - when the pet is not hungry or that card's shape is not on the list.
        ///
        /// Goes through RoundEngine.FeedPet, which is what the UI calls; the rules are here.
        /// </summary>
        internal bool TryFeed(RoundEngine round, int handIndex)
        {
            if (round == null || demands.Count == 0 || handIndex < 0
                || handIndex >= round.Hand.Count)
            {
                return false;
            }
            BlockCard card = round.Hand[handIndex];
            // The shape the card IS right now, so a reshaped fox counts as what the player can
            // see, not as what was printed on it.
            BlockShape offered = round.EffectiveShape(card);
            int match = IndexOfShape(offered);
            if (match < 0)
            {
                return false;
            }
            demands.RemoveAt(match);
            mealsEaten++;
            round.FeedCardToBoss(handIndex);
            return true;
        }

        /// <summary>True if this held card is something the pet would accept right now. The UI
        /// asks so it can offer the card, and so it can grey out the ones that are no use.</summary>
        public bool Accepts(RoundEngine round, BlockCard card)
        {
            if (round == null || card == null || demands.Count == 0)
            {
                return false;
            }
            return IndexOfShape(round.EffectiveShape(card)) >= 0;
        }

        private int IndexOfShape(BlockShape shape)
        {
            for (int i = 0; i < demands.Count; i++)
            {
                if (demands[i].CanonicalKey == shape.CanonicalKey)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Asks for a fresh set of shapes, drawn from the cards ALIVE in the round right now -
        /// the hand, the draw pile and the discard. Each card may be picked once, so a shape is
        /// only ever demanded twice when the round really does hold two of them, and a demand can
        /// therefore always be met by cards that exist.
        /// </summary>
        private void Demand(RoundEngine round, IRandomSource rng)
        {
            demands.Clear();
            if (round == null)
            {
                return;
            }
            var alive = new List<BlockShape>();
            for (int i = 0; i < round.Hand.Count; i++)
            {
                alive.Add(round.EffectiveShape(round.Hand[i]));
            }
            foreach (BlockCard card in round.Deck.DrawPile)
            {
                alive.Add(round.EffectiveShape(card));
            }
            foreach (BlockCard card in round.Deck.DiscardPile)
            {
                alive.Add(round.EffectiveShape(card));
            }
            int wanted = DemandSize < alive.Count ? DemandSize : alive.Count;
            for (int i = 0; i < wanted; i++)
            {
                int pick = rng.NextInt(0, alive.Count);
                demands.Add(alive[pick]);
                alive.RemoveAt(pick);
            }
        }
    }
}
