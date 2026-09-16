// PURPOSE: Everything card-shaped on screen: the hand row (draggable cards), the
// face-down draw pile (right), the discard pile with its top card face-up (left),
// and the basic animations between them (deal, discard, burn, shuffle).
// Sync() reconciles visuals against the engine state after every action; when a
// TurnReport is passed, differences are animated instead of snapped.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Owns and animates all card visuals + the two pile displays.</summary>
    public sealed class CardLayerView : MonoBehaviour
    {
        // WHERE THE HAND AND THE PILES GO is the layout's call, not this class's: a phone held
        // upright has about seven world units of width to play with where the desktop has
        // seventeen, so the fan tightens, the cards shrink and the piles come in off the edge.
        // See UiLayout - the desktop values are the constants that used to sit right here.
        public static Vector2 DrawPilePos
        {
            get { return UiLayout.Active.DrawPile; }
        }

        public static Vector2 DiscardPilePos
        {
            get { return UiLayout.Active.DiscardPile; }
        }

        private static Vector2 HandCenter
        {
            get { return UiLayout.Active.HandCenter; }
        }

        private static float HandSpacing
        {
            get { return UiLayout.Active.HandSpacing; }
        }

        /// <summary>How big a card is drawn. 1 on the desktop; smaller in portrait.</summary>
        public static float CardScale
        {
            get { return UiLayout.Active.CardScale; }
        }
        private const int MaxStackLayers = 10;
        private const int CardsPerStackLayer = 3; // one visible card edge per N cards
        private const float StackOffset = 0.035f;
        private const float DealDuration = 0.3f;
        private const float DiscardDuration = 0.28f;

        // Sorting order tiers (board uses 0-2): pile slot 2, stack layers 3..35 (each card
        // back needs 3 orders), discard top card 36, pile fx 25+, hand/bonus cards 12.
        // Piles and hand never overlap spatially, so the tiers may share numbers freely.
        //
        // THE HAND'S BUDGET, since it is no longer one order per card: a card spans 9
        // (HandFanOrderStep) and the fan alternates, so a resting card tops out at 12+9+8 = 29
        // - under the MARKET panel's backdrop at 31, which matters because the hand is still
        // drawn while the market is up. A hovered or dragged card takes HandFrontOrder for a top
        // of 38, under the deck overlay's dim at 39; neither gesture can happen while that
        // overlay is open, and this is why the base is 12 rather than the 20 it was.
        private const int StackBaseOrder = 3;
        private const int StackOrderStep = 3;
        private const int DiscardTopOrder = 36;
        private const int HeldCardOrder = 12;
        /// <summary>
        /// Loose card visuals that are not part of the hand - the "Şaşırtmaca" reveal beat, the
        /// shuffle and burn flourishes, the lab's samples. Every one of them is something being
        /// SHOWN over the hand, so it has to clear the fan; it used to sit at 25, which the fan
        /// now climbs straight through.
        ///
        /// 49 and not 34, because these are the one kind of card left that is NOT flattened -
        /// they are built and thrown away by the plain nine-order path - so the band has to be
        /// nine wide plus the three a shuffle deals: 49..59, which stops exactly short of the
        /// block gallery at 60. It therefore sits above the deck overlay, in company with the
        /// draw-pile peek that already did (DiscardTopOrder + up to 4, i.e. 36..48).
        /// </summary>
        private const int FxOrder = 49;

        private static readonly Color PileSlotColor = new Color(0.10f, 0.11f, 0.13f);

        private readonly Dictionary<int, CardVisual> heldVisuals = new Dictionary<int, CardVisual>();
        private Transform drawStackRoot;
        private Transform discardStackRoot;
        private CardVisual discardTopVisual;
        private TextMesh drawCountLabel;

        /// <summary>Holds the count and the sell plate, and follows the stack's own offset so
        /// both stay centred on the pile's visible top card however tall the pile is.</summary>
        private Transform drawLabelRoot;

        /// <summary>The "SELL CARDS" plate over the draw pile, shown in the market only.</summary>
        private Transform sellHintRoot;
        private TextMesh sellHintLabel;

        private readonly SpriteRenderer[] drawPileHoverEdges = new SpriteRenderer[4];
        private static readonly Color PileHoverColor = new Color(1f, 0.92f, 0.45f);

        private static readonly Color SellHintBodyColor = new Color(0.20f, 0.24f, 0.34f);
        private static readonly Color SellHintFrameColor = new Color(0.55f, 0.62f, 0.78f);
        private static readonly Color SellHintTextColor = new Color(1f, 0.92f, 0.45f);
        private int discardTopId = -1;
        private CardVisual drawTopVisual;
        private int drawTopId = -1;
        private bool pilesBuilt;
        private Transform drawPileRoot;
        private Transform discardPileRoot;

        /// <summary>Removes every card visual (new game / new round).</summary>
        /// <summary>
        /// The visual drawing one HELD card, or null when nothing is.
        ///
        /// "Midas" pays per gold CUBE and has to put each payout on the cube that earned it, so
        /// something outside this file needs to find a card's visual. Only held cards answer:
        /// the piles and the effects keep their own copies and none of them is the card the
        /// player is holding.
        /// </summary>
        public CardVisual Held(int cardId)
        {
            CardVisual visual;
            return heldVisuals.TryGetValue(cardId, out visual) ? visual : null;
        }

        public void Clear()
        {
            StopAllCoroutines(); // in-flight fx cards still self-destroy on arrival
            foreach (CardVisual visual in heldVisuals.Values)
            {
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
            }
            heldVisuals.Clear();
            if (discardTopVisual != null)
            {
                Destroy(discardTopVisual.gameObject);
                discardTopVisual = null;
            }
            discardTopId = -1;
            if (drawTopVisual != null)
            {
                Destroy(drawTopVisual.gameObject);
                drawTopVisual = null;
            }
            drawTopId = -1;
        }

        /// <summary>
        /// Reconciles visuals with the engine state. Pass the turn's report to animate
        /// (played card flies to the discard, new cards deal from the draw pile, burns and
        /// reshuffles fly between the piles); pass null to lay out instantly.
        /// </summary>
        public void Sync(RoundEngine round, TurnReport report)
        {
            SyncInternal(round, report, report != null);
        }

        /// <summary>Redraw animation: flies the whole hand to the discard, shows the
        /// shuffle, then deals the fresh hand. Call AFTER RoundEngine.RedrawHand().</summary>
        public void AnimateRedraw(RoundEngine round)
        {
            foreach (CardVisual visual in heldVisuals.Values)
            {
                if (visual != null)
                {
                    visual.SlotIndex = -1;
                    visual.SetFlattenedOrder(HandFrontOrder);
                    visual.FlyToAndDestroy(DiscardPilePos, DiscardDuration);
                }
            }
            heldVisuals.Clear();
            PlayShuffleFx(true);
            SyncInternal(round, null, true);
        }

        /// <summary>Single-card swap animation (İade): flies the returned card to the discard
        /// and deals its replacement from the draw pile. Call AFTER ReplaceHandCard().</summary>
        public void AnimateReplaceCard(RoundEngine round, int replacedCardId)
        {
            CardVisual old;
            if (heldVisuals.TryGetValue(replacedCardId, out old) && old != null)
            {
                heldVisuals.Remove(replacedCardId);
                old.SlotIndex = -1;
                old.SetFlattenedOrder(HandFrontOrder);
                old.FlyToAndDestroy(DiscardPilePos, DiscardDuration);
            }
            SyncInternal(round, null, true); // deals the replacement from the draw pile
        }

        /// <summary>Round-start presentation: shuffle flourish on the draw pile, then the
        /// opening deal. Replaces Clear()+Sync(null) when a round begins.</summary>
        public void AnimateRoundStart(RoundEngine round)
        {
            Clear();
            BuildPilesIfNeeded();
            UpdatePiles(round);
            StartCoroutine(RoundStartRoutine(round));
        }

        private IEnumerator RoundStartRoutine(RoundEngine round)
        {
            yield return ShuffleRoutine(false);
            SyncInternal(round, null, true);
        }

        /// <summary>Staggered card-backs flying onto the draw pile + a pile pulse.
        /// fromDiscard: true when the discard pile is being shuffled in; false for the
        /// round-start self-shuffle.</summary>
        private void PlayShuffleFx(bool fromDiscard)
        {
            StartCoroutine(ShuffleRoutine(fromDiscard));
        }

        private IEnumerator ShuffleRoutine(bool fromDiscard)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 from = fromDiscard
                    ? DiscardPilePos
                    : DrawPilePos + new Vector2(i % 2 == 0 ? 1.1f : -1.1f, 0.9f);
                CardVisual fx = CardVisual.Create(transform, "ShuffleFx", null,
                    false, false, from, FxOrder + i);
                fx.FlyToAndDestroy(DrawPilePos + new Vector2(0f, (1 - i) * 0.1f), 0.24f);
                yield return new WaitForSeconds(0.06f);
            }
            yield return new WaitForSeconds(0.2f);
            yield return PilePulse(drawPileRoot);
        }

        private static IEnumerator PilePulse(Transform root)
        {
            const float duration = 0.18f;
            float time = 0f;
            // MULTIPLIES the pile's resting size rather than replacing it - the root now carries
            // the layout's pile scale, and writing Vector3.one here would snap a phone's pile
            // back to desktop size the first time it was shuffled.
            float rest = UiLayout.Active.PileScale;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                float s = rest * (1f + 0.12f * k);
                root.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            root.localScale = new Vector3(rest, rest, 1f);
        }

        /// <summary>Shrinks and fades a card visual to nothing (an expiring bonus card).</summary>
        private IEnumerator VanishAndDestroy(CardVisual visual)
        {
            const float duration = 0.25f;
            Vector3 baseScale = visual.transform.localScale;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(time / duration);
                visual.transform.localScale = baseScale * k;
                visual.SetAlpha(k);
                yield return null;
            }
            Destroy(visual.gameObject);
        }

        private readonly List<CardVisual> labHand = new List<CardVisual>();

        /// <summary>
        /// LAB ONLY: a hand of cards the lab made up, laid out in the real hand's own slots.
        ///
        /// The animation lab may not touch the round, so an effect whose subject is what you are
        /// HOLDING cannot be shown with the real hand - there is no way to put gold in it without
        /// dealing one. This puts the lab's own cards where the hand would be and hands the
        /// visuals back, so the effect can be driven against them exactly as it is against the
        /// real ones. It holds until <see cref="ClearLabHand"/>, like every other lab scene.
        /// </summary>
        public IReadOnlyList<CardVisual> ShowLabHand(IReadOnlyList<BlockCard> cards)
        {
            ClearLabHand();
            if (cards == null)
            {
                return labHand;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                labHand.Add(CardVisual.Create(transform, "LabHand_" + i, cards[i], true, false,
                    SlotPosition(i, cards.Count), FxOrder));
            }
            return labHand;
        }

        public void ClearLabHand()
        {
            for (int i = 0; i < labHand.Count; i++)
            {
                if (labHand[i] != null)
                {
                    Destroy(labHand[i].gameObject);
                }
            }
            labHand.Clear();
        }

        /// <summary>
        /// "Şaşırtmaca": lays the hand the player WAS holding face up in front of them for a
        /// beat, then takes it away again. The real hand has already been mixed and dealt face
        /// down behind this, which is the whole point of showing it - the player learns what they
        /// are holding and never where any of it went.
        /// </summary>
        public void ShowRevealBeat(IReadOnlyList<BlockCard> cards, float seconds)
        {
            if (cards == null || cards.Count == 0)
            {
                return;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                Vector2 pos = SlotPosition(i, cards.Count) + new Vector2(0f, RevealBeatLift);
                CardVisual visual = CardVisual.Create(transform, "Reveal_" + i, cards[i],
                    true, false, pos, FxOrder);
                StartCoroutine(HoldThenVanish(visual, seconds));
            }
        }

        /// <summary>How far above the hand row the reveal beat sits, so it never covers the
        /// face-down cards it is telling you about.</summary>
        private const float RevealBeatLift = 1.25f;

        private readonly List<CardVisual> petDemandVisuals = new List<CardVisual>();

        /// <summary>Where "Tamagotchi" lays out what it wants: over the discard pile, clear of
        /// the hand row so the player can compare the two at a glance.</summary>
        private static readonly Vector2 PetDemandOrigin = new Vector2(-6.0f, -1.5f);
        private const float PetDemandSpacing = 1.0f;

        /// <summary>
        /// "Tamagotchi": shows the shapes the pet is still owed, as small face-up cards. They are
        /// drawn from throwaway BlockCards because a demand is a SHAPE and nothing else - what a
        /// card is made of never matters to the pet, and showing an element would imply it did.
        /// </summary>
        public void ShowPetDemands(IReadOnlyList<BlockShape> shapes)
        {
            for (int i = petDemandVisuals.Count - 1; i >= 0; i--)
            {
                if (petDemandVisuals[i] != null)
                {
                    Destroy(petDemandVisuals[i].gameObject);
                }
            }
            petDemandVisuals.Clear();
            if (shapes == null)
            {
                return;
            }
            for (int i = 0; i < shapes.Count; i++)
            {
                var pos = new Vector2(PetDemandOrigin.x, PetDemandOrigin.y - i * PetDemandSpacing);
                CardVisual visual = CardVisual.Create(transform, "PetWants_" + i,
                    new BlockCard(-1 - i, shapes[i]), true, false, pos, FxOrder);
                visual.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                petDemandVisuals.Add(visual);
            }
        }

        private IEnumerator HoldThenVanish(CardVisual visual, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (visual != null)
            {
                yield return VanishAndDestroy(visual);
            }
        }

        private void SyncInternal(RoundEngine round, TurnReport report, bool animate)
        {
            BuildPilesIfNeeded();

            int handCount = round.Hand.Count;
            int totalCount = handCount + round.BonusHand.Count;
            var wantedSlots = new Dictionary<int, int>();
            var bonusIds = new HashSet<int>();
            for (int i = 0; i < handCount; i++)
            {
                wantedSlots[round.Hand[i].Id] = i;
            }
            for (int i = 0; i < round.BonusHand.Count; i++)
            {
                BlockCard card = round.BonusHand[i].Card;
                wantedSlots[card.Id] = handCount + i;
                bonusIds.Add(card.Id);
            }

            // cards that left the hand: the played card flies to the discard, others vanish
            var leftIds = new List<int>();
            foreach (KeyValuePair<int, CardVisual> entry in heldVisuals)
            {
                if (!wantedSlots.ContainsKey(entry.Key))
                {
                    leftIds.Add(entry.Key);
                }
            }
            foreach (int id in leftIds)
            {
                CardVisual visual = heldVisuals[id];
                heldVisuals.Remove(id);
                if (visual == null)
                {
                    continue;
                }
                if (report != null && report.Card != null && report.Card.Id == id)
                {
                    visual.SlotIndex = -1;
                    visual.SetFlattenedOrder(HandFrontOrder);
                    if (report.PlayedCardExpired)
                    {
                        // An expiring bonus card joins no pile - it vanishes on the spot.
                        StartCoroutine(VanishAndDestroy(visual));
                    }
                    else
                    {
                        // "Baba Ocağı" buries played cards into the DRAW pile, not the
                        // discard, so the card flies to whichever pile actually received it.
                        Vector2 playedTarget = round.Rules.PlayedCardsReturnToDrawPile
                            ? DrawPilePos
                            : DiscardPilePos;
                        visual.FlyToAndDestroy(playedTarget, DiscardDuration);
                    }
                }
                else
                {
                    Destroy(visual.gameObject);
                }
            }

            // cards in the hand: deal the new ones, slide the shifted ones
            foreach (KeyValuePair<int, int> entry in wantedSlots)
            {
                int id = entry.Key;
                int slot = entry.Value;
                Vector2 slotPos = SlotPosition(slot, totalCount);
                CardVisual visual;
                // "Şaşırtmaca" deals the HAND face down - the bonus hand is never part of the
                // shell game - and turning one card over shows that one and only that one.
                bool faceUp = slot >= handCount || !round.HandIsFaceDown
                    || round.RevealedHandCardId == id;
                if (heldVisuals.TryGetValue(id, out visual) && visual != null
                    && visual.FaceUp != faceUp)
                {
                    // It flipped: the cached visual is the wrong side up, so it is rebuilt in
                    // place rather than slid around.
                    Destroy(visual.gameObject);
                    heldVisuals.Remove(id);
                }
                if (!heldVisuals.TryGetValue(id, out visual))
                {
                    BlockCard card = slot < handCount
                        ? round.Hand[slot]
                        : round.BonusHand[slot - handCount].Card;
                    visual = CardVisual.Create(transform, "Card_" + id, card, faceUp,
                        bonusIds.Contains(id), animate ? DrawPilePos : slotPos, HeldCardOrder,
                        round.EffectiveShape(card));
                    visual.SetBaseScale(CardScale);
                    heldVisuals[id] = visual;
                    if (animate)
                    {
                        visual.MoveTo(slotPos, DealDuration, null);
                    }
                }
                else if ((Vector2)visual.transform.localPosition != RestingPosition(id, slotPos))
                {
                    // RestingPosition, not slotPos: a rebuild in the middle of a hover - buying,
                    // a joker firing, anything that calls Sync - would otherwise drop the lifted
                    // card back into the row and leave it there, because SetHoveredCard only
                    // acts when the hovered card CHANGES.
                    visual.MoveTo(RestingPosition(id, slotPos), 0.15f, null);
                }
                visual.SlotIndex = slot;
                visual.HomePosition = slotPos;
                // The fan's draw order. Skipped for the card under the cursor and the card in
                // hand, which own a boost of their own until they are let go.
                if (id != hoveredCardId)
                {
                    visual.SetFlattenedOrder(HandFanOrder(slot));
                }
                // Asked of the ROUND, not the card: a freeze is round state with a countdown on
                // it ("Alıkoyma" holds a card for one turn, "Hazine" longer), and it is the same
                // question the drag path asks before refusing the pick-up.
                visual.SetFrozen(round.IsFrozen(id));
            }

            // pile-to-pile effects
            if (report != null)
            {
                if (report.DiscardWasReshuffled)
                {
                    PlayShuffleFx(true);
                }
                if (report.BurnedCard != null)
                {
                    CardVisual burnFx = CardVisual.Create(transform, "BurnFx", report.BurnedCard,
                        true, false, DrawPilePos, FxOrder);
                    burnFx.FlyToAndDestroy(DiscardPilePos, DiscardDuration);
                }
            }

            UpdatePiles(round);
        }

        /// <summary>One of this layer's own flourishes, for the animation lab to fire on
        /// demand (see PlayDebugAnimation).</summary>
        public enum DebugAnim
        {
            ShuffleSelf,
            ShuffleFromDiscard,
            PilePulseDraw,
            PilePulseDiscard,
            DealOne,
            DiscardOne,
            BurnOne,
            VanishOne
        }

        /// <summary>
        /// DEBUG SEAM for the animation lab (F3): plays one of the flourishes above on demand,
        /// on a throwaway card visual where one is needed.
        ///
        /// It exists so the lab drives THESE routines rather than a copy of them - when the
        /// shuffle or the discard flight is retimed, the lab shows the new timing for free.
        /// Nothing here reads or changes game state; <paramref name="round"/> is only used to
        /// name a real card so the visual looks like the ones in play.
        /// </summary>
        public void PlayDebugAnimation(DebugAnim which, RoundEngine round)
        {
            BuildPilesIfNeeded();
            BlockCard sample = round != null && round.Hand.Count > 0 ? round.Hand[0] : null;
            switch (which)
            {
                case DebugAnim.ShuffleSelf:
                    PlayShuffleFx(false);
                    break;
                case DebugAnim.ShuffleFromDiscard:
                    PlayShuffleFx(true);
                    break;
                case DebugAnim.PilePulseDraw:
                    StartCoroutine(PilePulse(drawPileRoot));
                    break;
                case DebugAnim.PilePulseDiscard:
                    StartCoroutine(PilePulse(discardPileRoot));
                    break;
                case DebugAnim.DealOne:
                    DebugFly(sample, false, DrawPilePos, SlotPosition(0, 1), DealDuration);
                    break;
                case DebugAnim.DiscardOne:
                    DebugFly(sample, true, SlotPosition(0, 1), DiscardPilePos, DiscardDuration);
                    break;
                case DebugAnim.BurnOne:
                    DebugFly(sample, true, DrawPilePos, DiscardPilePos, DiscardDuration);
                    break;
                case DebugAnim.VanishOne:
                    StartCoroutine(VanishAndDestroy(CardVisual.Create(transform, "LabVanish",
                        sample, true, true, SlotPosition(0, 1), FxOrder)));
                    break;
            }
        }

        /// <summary>Throwaway card that flies one leg and destroys itself - the same primitive
        /// every deal, discard and burn in Sync uses.</summary>
        private void DebugFly(BlockCard card, bool faceUp, Vector2 from, Vector2 to,
            float duration)
        {
            CardVisual fx = CardVisual.Create(transform, "LabFly", card, faceUp, false,
                from, FxOrder);
            fx.FlyToAndDestroy(to, duration);
        }

        /// <summary>True if a world point is on the draw pile (used to open the deck overlay).</summary>
        public bool IsDrawPileAt(Vector2 world)
        {
            return PileHit(world, DrawPilePos);
        }

        /// <summary>True if a world point is on the discard pile ("Fraksiyon" inspect).</summary>
        public bool IsDiscardPileAt(Vector2 world)
        {
            return PileHit(world, DiscardPilePos);
        }

        /// <summary>Scaled with the pile it is testing. A phone draws the piles at about half
        /// size, and an unscaled box would open the deck from well outside the card.</summary>
        private static bool PileHit(Vector2 world, Vector2 pile)
        {
            float scale = UiLayout.Active.PileScale;
            return Mathf.Abs(world.x - pile.x) <= CardVisual.BodyWidth * 0.5f * scale + 0.09f
                && Mathf.Abs(world.y - pile.y) <= CardVisual.BodyHeight * 0.5f * scale + 0.09f;
        }

        private int hoveredCardId = -1;

        /// <summary>Marks the card under the mouse (-1 = none): it grows slightly.
        /// Safe to call every frame; only transitions touch the visuals.</summary>
        public void SetHoveredCard(int cardId)
        {
            if (hoveredCardId == cardId)
            {
                return;
            }
            CardVisual previous;
            if (hoveredCardId >= 0 && heldVisuals.TryGetValue(hoveredCardId, out previous)
                && previous != null)
            {
                previous.SetHovered(false);
                // Back into the fan, at the order its slot says.
                previous.SetFlattenedOrder(HandFanOrder(previous.SlotIndex));
                previous.MoveTo(previous.HomePosition, HandHoverSeconds, null);
            }
            hoveredCardId = cardId;
            CardVisual current;
            if (cardId >= 0 && heldVisuals.TryGetValue(cardId, out current) && current != null)
            {
                current.SetHovered(true);
                // OUT of the fan and in front of all of it: in a tight hand the card being
                // pointed at is mostly buried, and growing it in place would only make a bigger
                // buried card.
                current.SetFlattenedOrder(HandFrontOrder);
                current.MoveTo(current.HomePosition + new Vector2(0f, HandHoverLift),
                    HandHoverSeconds, null);
            }
        }

        /// <summary>Puts a card back at the order its slot says. The drag path owns a card while
        /// it is being carried and has to hand it back when it lets go - and "back" is a place in
        /// the fan, which only this class knows how to work out.</summary>
        public void ReturnToFan(CardVisual visual)
        {
            if (visual == null)
            {
                return;
            }
            visual.SetFlattenedOrder(visual.SlotIndex >= 0
                ? HandFanOrder(visual.SlotIndex)
                : HandFrontOrder);
        }

        /// <summary>Where a held card actually sits: its slot, plus the hover lift when it is
        /// the one under the pointer.</summary>
        private Vector2 RestingPosition(int cardId, Vector2 slotPos)
        {
            return cardId == hoveredCardId
                ? slotPos + new Vector2(0f, HandHoverLift)
                : slotPos;
        }

        /// <summary>How long the hover lift takes. Short - it has to feel like the card is
        /// answering the pointer, not playing an animation.</summary>
        private const float HandHoverSeconds = 0.07f;

        /// <summary>Drops a card's visual so the next Sync rebuilds it (used after a
        /// mechanical rotation or fox reshape changed its displayed shape).</summary>
        public void ForgetCard(int cardId)
        {
            CardVisual visual;
            if (heldVisuals.TryGetValue(cardId, out visual))
            {
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
                heldVisuals.Remove(cardId);
            }
        }

        /// <summary>The held card sitting in a SLOT, or null. The inverse of CardAt: the mouse
        /// asks "what is under this point", a gamepad asks "where is the card I have chosen"
        /// (see GameUiController.PadPlay.cs).</summary>
        public CardVisual VisualOfSlot(int slot)
        {
            if (slot < 0)
            {
                return null;
            }
            foreach (CardVisual visual in heldVisuals.Values)
            {
                if (visual != null && visual.SlotIndex == slot)
                {
                    return visual;
                }
            }
            return null;
        }

        /// <summary>
        /// Re-fits everything this layer owns to the CURRENT layout profile.
        ///
        /// The piles are rebuilt rather than moved: their slot art is drawn at the profile's pile
        /// scale, so a profile change is a different sprite size, not a different position. The
        /// hand is re-placed and re-scaled in place - the cards themselves are unchanged, which is
        /// what keeps every dealing and discarding animation working exactly as it did.
        /// </summary>
        public void RelayoutForScreen()
        {
            if (pilesBuilt)
            {
                if (drawPileRoot != null)
                {
                    Destroy(drawPileRoot.gameObject);
                }
                if (discardPileRoot != null)
                {
                    Destroy(discardPileRoot.gameObject);
                }
                drawPileRoot = null;
                discardPileRoot = null;
                pilesBuilt = false;
                BuildPilesIfNeeded();
                SetPilesVisible(pilesVisible);
            }
            int total = 0;
            foreach (CardVisual visual in heldVisuals.Values)
            {
                if (visual != null && visual.SlotIndex >= 0)
                {
                    total++;
                }
            }
            foreach (CardVisual visual in heldVisuals.Values)
            {
                if (visual == null || visual.SlotIndex < 0)
                {
                    continue;
                }
                visual.SetBaseScale(CardScale);
                visual.SnapTo(SlotPosition(visual.SlotIndex, total));
            }
        }

        /// <summary>
        /// Shows or hides the two piles. The portrait MARKET covers the whole screen, and the
        /// piles' labels sort above its panel - so left alone they print through it. Their job
        /// there is taken by the market's own DECK button.
        /// </summary>
        public void SetPilesVisible(bool visible)
        {
            pilesVisible = visible;
            if (drawPileRoot != null && drawPileRoot.gameObject.activeSelf != visible)
            {
                drawPileRoot.gameObject.SetActive(visible);
            }
            if (discardPileRoot != null && discardPileRoot.gameObject.activeSelf != visible)
            {
                discardPileRoot.gameObject.SetActive(visible);
            }
        }

        /// <summary>Remembered so a REBUILD comes back in the state it was left in - a relayout
        /// destroys and recreates the piles, and they would otherwise reappear over the market
        /// that had just hidden them.</summary>
        private bool pilesVisible = true;

        /// <summary>The held card under a world point (for drag pickup), or null.</summary>
        public CardVisual CardAt(Vector2 world)
        {
            // THE CARD ON TOP WINS, and "on top" has to mean what the player can SEE on top.
            // The fan climbs left to right, so that is the HIGHEST fan order among the cards
            // under the pointer - which past MaxFannedSlots saturates, and the slot settles the
            // ones that then share it.
            //
            // (The older version returned the first hit while walking a Dictionary, whose order
            // is not defined at all.)
            CardVisual best = null;
            int bestBoost = 0;
            foreach (CardVisual visual in heldVisuals.Values)
            {
                if (visual == null)
                {
                    continue;
                }
                // The card's HOME, not where it is: a hovered card is lifted out of the row, and
                // testing against the lifted box makes it hold on to its own hover - the pointer
                // leaves the card, the card follows, and nothing else can take the hover.
                Vector2 pos = visual.HomePosition;
                // Scaled with the card: in portrait a card is drawn at 0.92, and testing against
                // the unscaled box would hand back a card the pointer is not actually over.
                if (Mathf.Abs(world.x - pos.x) > CardVisual.BodyWidth * CardScale * 0.5f
                    || Mathf.Abs(world.y - pos.y) > CardVisual.BodyHeight * CardScale * 0.5f)
                {
                    continue;
                }
                int boost = HandFanOrder(visual.SlotIndex);
                if (best == null || boost > bestBoost
                    || (boost == bestBoost && visual.SlotIndex > best.SlotIndex))
                {
                    best = visual;
                    bestBoost = boost;
                }
            }
            return best;
        }

        // ============================== THE HAND IS A FAN ================================
        // ONE row, always, and it OVERLAPS rather than wrapping or running off the edge. The
        // hand used to break into rows of 8 that stacked upward over the board - and a full row
        // of 8 was already 13.6 wide, straight through both piles at x +-6.4.
        //
        // TWO widths decide the shape, and the second one is the whole treatment:
        //   1. up to HandFanSpan the cards sit at their natural HandSpacing and nothing overlaps
        //      (a 7-card hand still has a gap between every card);
        //   2. past that the fan STOPS GROWING and the cards simply lie further and further over
        //      one another. It never widens to make room, and it never wraps.
        //
        // A crowded hand is therefore a deliberate stack of half-hidden cards rather than a
        // strip that creeps out towards the piles. That is fine, and it is the point: a card is
        // read by HOVERING - the one under the cursor lifts out of the row and rises clear of
        // everything (HandHoverLift, HandFrontBoost) - so covering a card costs nothing, while a
        // hand that keeps widening eventually reaches the draw pile and takes the deck/sell
        // screen away with it.
        //
        // The overlap runs one way and one way only: every card draws over the one to its left,
        // whole - outline, cubes and element band together - so the row reads as cards laid down
        // in order. See HandFanOrder for how that is paid for.

        /// <summary>Width the fan is happy at, between the outermost card CENTRES. Also its
        /// hard ceiling - past this it overlaps instead of growing. The layout's call (9 on the
        /// desktop, tighter in portrait); UiLayout's HandFanSpanMax / HandMinSpacing belonged to
        /// the old widening fan and are no longer read here.</summary>
        private static float HandFanSpan
        {
            get { return UiLayout.Active.HandFanSpan; }
        }

        /// <summary>Distance between two neighbouring cards for a hand of this size.</summary>
        private static float HandSpacingFor(int totalCount)
        {
            int gaps = totalCount - 1;
            if (gaps < 1)
            {
                return HandSpacing;
            }
            if (gaps * HandSpacing <= HandFanSpan)
            {
                return HandSpacing;                       // roomy: nothing overlaps
            }
            return HandFanSpan / gaps;                    // crowded: they lie over one another
        }

        private static Vector2 SlotPosition(int slot, int totalCount)
        {
            float spacing = HandSpacingFor(totalCount);
            float startX = HandCenter.x - (totalCount - 1) * spacing * 0.5f;
            return new Vector2(startX + slot * spacing, HandCenter.y);
        }

        /// <summary>
        /// THE FAN CLIMBS. Every card draws over the one to its left - all of it, outline and
        /// cubes and element band together - so a crowded hand reads as a stack of cards laid
        /// down left to right, which is the only overlap that means anything.
        ///
        /// It used to ALTERNATE (0, 9, 0, 9...) because a card spans nine sorting orders of its
        /// own and one band per card would have walked the hand up through the deck overlay
        /// within four of them. What that bought was a weave: card 1 drew over both 0 AND 2, and
        /// a left-hand card's cubes sat on top of its right-hand neighbour's face. The fix was
        /// not more orders but fewer - a hand card is FLATTENED into a single order and layered
        /// by depth instead (CardVisual.SetFlattenedOrder), so the whole fan costs one order per
        /// card and fits in the gap that was always there.
        ///
        /// Past MaxFannedSlots the climb saturates and the last cards share the top order. That
        /// is a hand of twenty-odd cards - only reachable through a joker that rewrites the hand
        /// size - and it degrades into the old ambiguity rather than into the piles.
        /// </summary>
        private static int HandFanOrder(int slot)
        {
            return HeldCardOrder + Mathf.Clamp(slot, 0, MaxFannedSlots);
        }

        /// <summary>How many cards get an order of their own before the climb saturates. The fan
        /// therefore occupies HeldCardOrder..HeldCardOrder+MaxFannedSlots, and HandFrontOrder
        /// sits directly above it - all of it below DiscardTopOrder, so the piles still draw over
        /// the hand and the deck overlay still covers everything.</summary>
        private const int MaxFannedSlots = 20;

        /// <summary>Order a hovered or dragged card takes: clear of the whole fan, whatever slot
        /// it came from. Absolute rather than a boost, because "in front of the fan" is a place,
        /// not an amount.</summary>
        public const int HandFrontOrder = HeldCardOrder + MaxFannedSlots + 1;

        /// <summary>How far a hovered card rises out of the fan - the Balatro read: the card you
        /// are pointing at leaves the row so it can be seen whole.</summary>
        private const float HandHoverLift = 0.38f;

        private void BuildPilesIfNeeded()
        {
            if (pilesBuilt)
            {
                return;
            }
            pilesBuilt = true;
            // each pile lives under its own root so the shuffle pulse can scale it
            drawPileRoot = MakePileRoot("DrawPile", DrawPilePos);
            discardPileRoot = MakePileRoot("DiscardPile", DiscardPilePos);
            // THE ROOT carries the size, not the slot sprite: the count, the sell prompt and the
            // stack are all children, and scaling only the slot left them at desktop size - the
            // sell prompt is wider than the pile itself and hung off the side of a phone.
            float pileScale = UiLayout.Active.PileScale;
            drawPileRoot.localScale = new Vector3(pileScale, pileScale, 1f);
            discardPileRoot.localScale = new Vector3(pileScale, pileScale, 1f);
            var slotSize = new Vector2(CardVisual.BodyWidth + 0.18f, CardVisual.BodyHeight + 0.18f);
            ViewUtil.MakeRect(drawPileRoot, "Slot", Vector2.zero, slotSize, PileSlotColor, 2);
            ViewUtil.MakeRect(discardPileRoot, "Slot", Vector2.zero, slotSize, PileSlotColor, 2);
            drawStackRoot = MakePileRoot("Stack", Vector2.zero);
            drawStackRoot.SetParent(drawPileRoot, false);
            discardStackRoot = MakePileRoot("Stack", Vector2.zero);
            discardStackRoot.SetParent(discardPileRoot, false);
            // The count and the sell prompt both ride ON the pile's face, in a root that follows
            // the stack's offset - floating above it, they read as loose HUD text that happens
            // to be nearby rather than as a label ON the thing you are meant to click.
            // Orders 38+ clear the stack layers (3..35) and the discard's face-up top (36).
            drawLabelRoot = MakePileRoot("Labels", Vector2.zero);
            drawLabelRoot.SetParent(drawPileRoot, false);
            drawCountLabel = ViewUtil.MakeText3D(drawLabelRoot, "Count",
                new Vector2(0f, 0.42f), "0",
                56, 0.075f, Color.white, 40, TextAnchor.MiddleCenter);
            // Above the count, and only in the market: the deck pile is also the SELL screen,
            // which nothing on screen said. A framed plate rather than loose text - floating
            // words over the background read as a debug print, not as something to click.
            sellHintRoot = MakePileRoot("SellHint", new Vector2(0f, -0.45f));
            sellHintRoot.SetParent(drawLabelRoot, false);
            var hintSize = new Vector2(1.72f, 0.44f);
            ViewUtil.MakeRect(sellHintRoot, "Frame", Vector2.zero,
                hintSize + new Vector2(0.10f, 0.10f), SellHintFrameColor, 38);
            ViewUtil.MakeRect(sellHintRoot, "Body", Vector2.zero, hintSize, SellHintBodyColor, 39);
            sellHintLabel = ViewUtil.MakeText3D(sellHintRoot, "Label", Vector2.zero,
                Loc.Pick("SELL CARDS", "KART SAT"),
                70, 0.027f, SellHintTextColor, 40, TextAnchor.MiddleCenter);
            sellHintRoot.gameObject.SetActive(false);

            // Hover outline, drawn OVER the stack so it reads whatever the pile's height.
            for (int i = 0; i < drawPileHoverEdges.Length; i++)
            {
                drawPileHoverEdges[i] = ViewUtil.MakeRect(drawPileRoot, "Hover_" + i,
                    Vector2.zero, Vector2.one, PileHoverColor, 41);
                drawPileHoverEdges[i].enabled = false;
            }
        }

        /// <summary>Outlines the draw pile while the pointer is over it - it is clickable in
        /// every phase (deck list in a round, sell screen in the market) and nothing said so.
        /// The outline is placed around the stack's CURRENT extent, so it fits a tall pile and
        /// a nearly empty one alike.</summary>
        public void SetDrawPileHovered(bool hovered)
        {
            if (drawPileHoverEdges[0] == null)
            {
                return;
            }
            if (!hovered)
            {
                for (int i = 0; i < drawPileHoverEdges.Length; i++)
                {
                    drawPileHoverEdges[i].enabled = false;
                }
                return;
            }
            Vector2 center = drawLabelRoot != null
                ? (Vector2)drawLabelRoot.localPosition
                : Vector2.zero;
            var half = new Vector2(CardVisual.BodyWidth * 0.5f + 0.16f,
                CardVisual.BodyHeight * 0.5f + 0.16f);
            const float t = 0.07f;
            PlaceEdge(drawPileHoverEdges[0], center + new Vector2(0f, half.y),
                new Vector2(half.x * 2f + t, t));
            PlaceEdge(drawPileHoverEdges[1], center - new Vector2(0f, half.y),
                new Vector2(half.x * 2f + t, t));
            PlaceEdge(drawPileHoverEdges[2], center - new Vector2(half.x, 0f),
                new Vector2(t, half.y * 2f + t));
            PlaceEdge(drawPileHoverEdges[3], center + new Vector2(half.x, 0f),
                new Vector2(t, half.y * 2f + t));
            for (int i = 0; i < drawPileHoverEdges.Length; i++)
            {
                drawPileHoverEdges[i].enabled = true;
            }
        }

        private static void PlaceEdge(SpriteRenderer edge, Vector2 center, Vector2 size)
        {
            edge.transform.localPosition = new Vector3(center.x, center.y, 0f);
            edge.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        /// <summary>Shows or hides the "click to sell" prompt over the draw pile. The controller
        /// turns it on for the market phase only.</summary>
        public void SetSellHint(bool visible)
        {
            if (sellHintRoot == null)
            {
                return;
            }
            // Re-texted on every toggle so a language switch mid-run is picked up.
            sellHintLabel.text = Loc.Pick("SELL CARDS", "KART SAT");
            sellHintRoot.gameObject.SetActive(visible);
        }

        private Transform MakePileRoot(string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            return go.transform;
        }

        /// <summary>Rebuilds a pile's stack as the decorated BACKS of its actual cards
        /// (sampled every few cards), so individual cards can be told apart.</summary>
        private void RebuildStack(Transform stackRoot, IReadOnlyList<BlockCard> pile)
        {
            for (int i = stackRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(stackRoot.GetChild(i).gameObject);
            }
            int layers = LayersFor(pile.Count);
            for (int i = 0; i < layers; i++)
            {
                // anchor sampling at the TOP: the topmost visible back is always the
                // pile's actual top card, so drawing visibly changes the pile
                int cardIndex = Mathf.Max(pile.Count - 1 - (layers - 1 - i) * CardsPerStackLayer, 0);
                var layerRoot = new GameObject("Back_" + i).transform;
                layerRoot.SetParent(stackRoot, false);
                layerRoot.localPosition = new Vector3(i * StackOffset, i * StackOffset, 0f);
                CardVisual.BuildBack(layerRoot, pile[cardIndex],
                    StackBaseOrder + i * StackOrderStep, null);
            }
        }

        /// <summary>Visible stack layers for a card count: one edge per few cards,
        /// so the stack keeps growing noticeably for realistic deck sizes.</summary>
        private static int LayersFor(int cardCount)
        {
            if (cardCount <= 0)
            {
                return 0;
            }
            return Mathf.Clamp((cardCount + CardsPerStackLayer - 1) / CardsPerStackLayer,
                1, MaxStackLayers);
        }

        private void UpdatePiles(RoundEngine round)
        {
            drawCountLabel.text = round.Deck.DrawCount.ToString();
            // The stack fans up-and-right one step per layer, so its visible middle moves as the
            // pile shrinks. The labels ride along, or they would slide off a thinning pile.
            float fan = (LayersFor(round.Deck.DrawCount) - 1) * StackOffset * 0.5f;
            drawLabelRoot.localPosition = new Vector3(fan, fan, 0f);
            RebuildStack(drawStackRoot, round.Deck.DrawPile);
            RebuildStack(discardStackRoot, round.Deck.DiscardPile);
            UpdateDiscardTop(round);
            UpdateDrawTop(round);
            UpdateRevealFans(round);
        }

        // ---- reveal peeks: the top few cards of a pile shown face-up (rule-driven info) ----
        // "Büyüteç" reveals exactly the top RevealedDrawCount DRAW cards and nothing deeper;
        // the rest of the pile stays face-down. The pile-top visual already shows ONE card, so
        // the remaining (N-1) cards stack UPWARD above the pile as a small, obvious peek - never
        // sideways into the play area, which read like the whole deck had opened up.
        // "Fraksiyon" (discard reveal) is handled by the deck-overlay inspect, not here.

        private const int MaxRevealPeek = 4; // safety cap; Büyüteç only ever asks for 2
        private const float RevealPeekScale = 0.6f;

        private readonly List<CardVisual> revealFanVisuals = new List<CardVisual>();

        private void UpdateRevealFans(RoundEngine round)
        {
            for (int i = revealFanVisuals.Count - 1; i >= 0; i--)
            {
                if (revealFanVisuals[i] != null)
                {
                    Destroy(revealFanVisuals[i].gameObject);
                }
            }
            revealFanVisuals.Clear();
            int drawReveal = round.Rules.RevealedDrawCount;
            if (round.Rules.RevealTopDrawCard)
            {
                drawReveal = Mathf.Max(drawReveal, 1);
            }
            BuildDrawPeek(round.Deck.DrawPile, drawReveal);
        }

        /// <summary>Shows exactly the top <paramref name="revealCount"/> draw cards. The top
        /// one is the pile-top visual; each further card stacks upward above the pile.</summary>
        private void BuildDrawPeek(IReadOnlyList<BlockCard> pile, int revealCount)
        {
            int extras = Mathf.Clamp(Mathf.Min(revealCount, pile.Count) - 1, 0, MaxRevealPeek);
            float step = CardVisual.BodyHeight * RevealPeekScale + 0.12f;
            for (int k = 1; k <= extras; k++)
            {
                BlockCard card = pile[pile.Count - 1 - k];
                CardVisual visual = CardVisual.Create(drawPileRoot, "DrawPeek_" + k, card,
                    true, false, new Vector2(0f, 0.7f + step * k), DiscardTopOrder + k);
                visual.transform.localScale = new Vector3(RevealPeekScale, RevealPeekScale, 1f);
                revealFanVisuals.Add(visual);
            }
        }

        private void UpdateDiscardTop(RoundEngine round)
        {
            IReadOnlyList<BlockCard> discardPile = round.Deck.DiscardPile;
            // "Fraksiyon" hides the discard top after a swap (until the next reshuffle).
            BlockCard top = !round.Rules.HideDiscardTop && discardPile.Count > 0
                ? discardPile[discardPile.Count - 1]
                : null;
            int topId = top != null ? top.Id : -1;
            if (topId == discardTopId)
            {
                return;
            }
            if (discardTopVisual != null)
            {
                Destroy(discardTopVisual.gameObject);
                discardTopVisual = null;
            }
            discardTopId = topId;
            if (top != null)
            {
                int layers = LayersFor(round.Deck.DiscardCount);
                Vector2 offset = new Vector2(layers * StackOffset, layers * StackOffset);
                discardTopVisual = CardVisual.Create(discardPileRoot, "DiscardTop",
                    top, true, false, offset, DiscardTopOrder);
            }
        }

        /// <summary>Shows the top of the DRAW pile face-up when a rule reveals it -
        /// "Insider"/"Baba Ocağı" (RevealTopDrawCard) or "Büyüteç" (RevealedDrawCount).
        /// Gated, because the draw pile is face-down by default and must not leak.</summary>
        private void UpdateDrawTop(RoundEngine round)
        {
            IReadOnlyList<BlockCard> drawPile = round.Deck.DrawPile;
            bool revealed = round.Rules.RevealTopDrawCard || round.Rules.RevealedDrawCount > 0;
            BlockCard top = revealed && drawPile.Count > 0
                ? drawPile[drawPile.Count - 1]
                : null;
            int topId = top != null ? top.Id : -1;
            if (topId == drawTopId)
            {
                return;
            }
            if (drawTopVisual != null)
            {
                Destroy(drawTopVisual.gameObject);
                drawTopVisual = null;
            }
            drawTopId = topId;
            if (top != null)
            {
                int layers = LayersFor(round.Deck.DrawCount);
                Vector2 offset = new Vector2(layers * StackOffset, layers * StackOffset);
                drawTopVisual = CardVisual.Create(drawPileRoot, "DrawTop",
                    top, true, false, offset, DiscardTopOrder);
            }
        }
    }
}
