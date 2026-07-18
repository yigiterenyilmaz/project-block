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
        private const int MaxStackLayers = 5;
        private const float StackOffset = 0.05f;
        private const float DealDuration = 0.3f;
        private const float DiscardDuration = 0.28f;

        private static readonly Color PileSlotColor = new Color(0.10f, 0.11f, 0.13f);
        private static readonly Color DiscardStackColor = new Color(0.42f, 0.40f, 0.36f);

        private readonly Dictionary<int, CardVisual> heldVisuals = new Dictionary<int, CardVisual>();
        private readonly List<SpriteRenderer> drawStack = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> discardStack = new List<SpriteRenderer>();
        private CardVisual discardTopVisual;
        private int discardTopId = -1;
        private CardVisual drawTopVisual;
        private int drawTopId = -1;
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
                    false, false, from, 7 + i);
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
                        bonusIds.Contains(id), animate ? DrawPilePos : slotPos, 5);
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
                        true, false, DrawPilePos, 7);
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
            ViewUtil.MakeRect(drawPileRoot, "Slot", Vector2.zero, slotSize, PileSlotColor, 3);
            ViewUtil.MakeRect(discardPileRoot, "Slot", Vector2.zero, slotSize, PileSlotColor, 3);
            for (int i = 0; i < MaxStackLayers; i++)
            {
                Vector2 offset = new Vector2(i * StackOffset, i * StackOffset);
                SpriteRenderer drawLayer = ViewUtil.MakeRect(drawPileRoot, "Stack_" + i,
                    offset, new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight),
                    new Color(0.18f, 0.26f, 0.44f), 4);
                drawLayer.enabled = false;
                drawStack.Add(drawLayer);
                SpriteRenderer discardLayer = ViewUtil.MakeRect(discardPileRoot, "Stack_" + i,
                    offset, new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight),
                    DiscardStackColor, 4);
                discardLayer.enabled = false;
                discardStack.Add(discardLayer);
            }
        }

        private Transform MakePileRoot(string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            return go.transform;
        }

        private void UpdatePiles(RoundEngine round)
        {
            int drawLayers = Mathf.Min(MaxStackLayers, round.Deck.DrawCount);
            int discardLayers = Mathf.Min(MaxStackLayers, round.Deck.DiscardCount);
            for (int i = 0; i < MaxStackLayers; i++)
            {
                drawStack[i].enabled = i < drawLayers;
                discardStack[i].enabled = i < discardLayers;
            }

            UpdateDiscardTop(round, discardLayers);
            UpdateDrawTop(round, drawLayers);
        }

        private void UpdateDiscardTop(RoundEngine round, int discardLayers)
        {
            IReadOnlyList<BlockCard> discardPile = round.Deck.DiscardPile;
            BlockCard top = discardPile.Count > 0 ? discardPile[discardPile.Count - 1] : null;
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
                Vector2 offset = new Vector2(discardLayers * StackOffset, discardLayers * StackOffset);
                discardTopVisual = CardVisual.Create(discardPileRoot, "DiscardTop",
                    top, true, false, offset, 5);
            }
        }

        /// <summary>"Insider": shows the top of the DRAW pile face-up. Gated on the rule flag,
        /// because the draw pile is face-down by default and its order must not leak.</summary>
        private void UpdateDrawTop(RoundEngine round, int drawLayers)
        {
            IReadOnlyList<BlockCard> drawPile = round.Deck.DrawPile;
            BlockCard top = round.Rules.RevealTopDrawCard && drawPile.Count > 0
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
                Vector2 offset = new Vector2(drawLayers * StackOffset, drawLayers * StackOffset);
                drawTopVisual = CardVisual.Create(drawPileRoot, "DrawTop",
                    top, true, false, offset, 5);
            }
        }
    }
}
