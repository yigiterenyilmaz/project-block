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
        public static readonly Vector2 DrawPilePos = new Vector2(6.4f, -4.05f);
        public static readonly Vector2 DiscardPilePos = new Vector2(-6.4f, -4.05f);
        private static readonly Vector2 HandCenter = new Vector2(0f, -4.05f);
        private const float HandSpacing = 1.7f;
        private const int MaxStackLayers = 10;
        private const int CardsPerStackLayer = 3; // one visible card edge per N cards
        private const float StackOffset = 0.035f;
        private const float DealDuration = 0.3f;
        private const float DiscardDuration = 0.28f;

        // Sorting order tiers (board uses 0-2): pile slot 2, stack layers 3..35 (each card
        // back needs 3 orders), discard top card 36, hand/bonus cards 20 (+8 fly boost,
        // +10 drag boost - piles and hand never overlap spatially), pile fx 25+.
        private const int StackBaseOrder = 3;
        private const int StackOrderStep = 3;
        private const int DiscardTopOrder = 36;
        private const int HeldCardOrder = 20;
        private const int FxOrder = 25;

        private static readonly Color PileSlotColor = new Color(0.10f, 0.11f, 0.13f);

        private readonly Dictionary<int, CardVisual> heldVisuals = new Dictionary<int, CardVisual>();
        private Transform drawStackRoot;
        private Transform discardStackRoot;
        private CardVisual discardTopVisual;
        private TextMesh drawCountLabel;
        private int discardTopId = -1;
        private bool pilesBuilt;
        private Transform drawPileRoot;
        private Transform discardPileRoot;

        /// <summary>Removes every card visual (new game / new round).</summary>
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
                    visual.SetSortingBoost(8);
                    visual.FlyToAndDestroy(DiscardPilePos, DiscardDuration);
                }
            }
            heldVisuals.Clear();
            PlayShuffleFx(true);
            SyncInternal(round, null, true);
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
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                root.localScale = Vector3.one * (1f + 0.12f * k);
                yield return null;
            }
            root.localScale = Vector3.one;
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
                    visual.SetSortingBoost(8);
                    visual.FlyToAndDestroy(DiscardPilePos, DiscardDuration);
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
                if (!heldVisuals.TryGetValue(id, out visual))
                {
                    BlockCard card = slot < handCount
                        ? round.Hand[slot]
                        : round.BonusHand[slot - handCount].Card;
                    visual = CardVisual.Create(transform, "Card_" + id, card, true,
                        bonusIds.Contains(id), animate ? DrawPilePos : slotPos, HeldCardOrder,
                        round.EffectiveShape(card));
                    heldVisuals[id] = visual;
                    if (animate)
                    {
                        visual.MoveTo(slotPos, DealDuration, null);
                    }
                }
                else if ((Vector2)visual.transform.localPosition != slotPos)
                {
                    visual.MoveTo(slotPos, 0.15f, null);
                }
                visual.SlotIndex = slot;
                visual.HomePosition = slotPos;
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

        /// <summary>True if a world point is on the draw pile (used to open the deck overlay).</summary>
        public bool IsDrawPileAt(Vector2 world)
        {
            return Mathf.Abs(world.x - DrawPilePos.x) <= CardVisual.BodyWidth * 0.5f + 0.09f
                && Mathf.Abs(world.y - DrawPilePos.y) <= CardVisual.BodyHeight * 0.5f + 0.09f;
        }

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

        /// <summary>The held card under a world point (for drag pickup), or null.</summary>
        public CardVisual CardAt(Vector2 world)
        {
            foreach (CardVisual visual in heldVisuals.Values)
            {
                if (visual == null)
                {
                    continue;
                }
                Vector2 pos = visual.transform.localPosition;
                if (Mathf.Abs(world.x - pos.x) <= CardVisual.BodyWidth * 0.5f
                    && Mathf.Abs(world.y - pos.y) <= CardVisual.BodyHeight * 0.5f)
                {
                    return visual;
                }
            }
            return null;
        }

        private static Vector2 SlotPosition(int slot, int totalCount)
        {
            float startX = HandCenter.x - (totalCount - 1) * HandSpacing * 0.5f;
            return new Vector2(startX + slot * HandSpacing, HandCenter.y);
        }

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
            var slotSize = new Vector2(CardVisual.BodyWidth + 0.18f, CardVisual.BodyHeight + 0.18f);
            ViewUtil.MakeRect(drawPileRoot, "Slot", Vector2.zero, slotSize, PileSlotColor, 2);
            ViewUtil.MakeRect(discardPileRoot, "Slot", Vector2.zero, slotSize, PileSlotColor, 2);
            drawStackRoot = MakePileRoot("Stack", Vector2.zero);
            drawStackRoot.SetParent(drawPileRoot, false);
            discardStackRoot = MakePileRoot("Stack", Vector2.zero);
            discardStackRoot.SetParent(discardPileRoot, false);
            drawCountLabel = ViewUtil.MakeText3D(drawPileRoot, "Count",
                new Vector2(0f, CardVisual.BodyHeight * 0.5f + 0.34f), "0",
                56, 0.07f, Color.white, 37, TextAnchor.MiddleCenter);
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
            RebuildStack(drawStackRoot, round.Deck.DrawPile);
            RebuildStack(discardStackRoot, round.Deck.DiscardPile);
            int discardLayers = LayersFor(round.Deck.DiscardCount);

            IReadOnlyList<BlockCard> discardPile = round.Deck.DiscardPile;
            int topId = discardPile.Count > 0 ? discardPile[discardPile.Count - 1].Id : -1;
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
            if (topId >= 0)
            {
                Vector2 offset = new Vector2(discardLayers * StackOffset, discardLayers * StackOffset);
                discardTopVisual = CardVisual.Create(discardPileRoot, "DiscardTop",
                    discardPile[discardPile.Count - 1], true, false, offset, DiscardTopOrder);
            }
        }
    }
}
