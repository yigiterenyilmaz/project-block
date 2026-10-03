// PURPOSE: BlackjackTableView's DEALER - the house's CLOSED hand at the top right of its arena,
// and the one card it plays each turn, from the fan to the felt.
//
// THE HAND IS CLOSED AND HONEST. It shows exactly as many card backs as the house holds (Core's
// count, DuelAiPlay.HandBefore / HandAfter) and nothing else: no face, no element, no hint of what
// is in its pile or what will come in. The fan is slight and cold - the house does not fidget.
//
// THE PLAY IS ONE CAUSAL CHAIN, ~0.5-0.75 s: the house THINKS (a scan along the card edges, three
// gold points, the brass behind the fan breathing - a deterministic 0.25-0.55 s), the card Core
// chose comes forward and the others sit back, it RISES, travels a curve to where its block will
// stand and turns over halfway (back for the first half, its own face for the second - a scaleX
// flip, never a cut), its footprint warms on the board, it LANDS with a small squash and a tap on
// the felt - and only then does the block appear on the arena (the controller's onLand). The gap
// it left closes, and the refill slides in face-down from the house's own edge of the table.
// A board that changes without a card coming from the house's hand is the failure this exists for.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView
    {
        private sealed class FanCard
        {
            public Transform Root;
            public SpriteRenderer Back;
            public Vector2 Pos;
            public float Rot;
            public float Alpha;
            public Vector2 TargetPos;
            public float TargetRot;
            public float TargetAlpha = 1f;
            public float Dim;
            public float TargetDim;
            public bool Leaving;
        }

        /// <summary>The beats of one house play, in order (the lab stops after one and holds it).</summary>
        public enum DealerBeat
        {
            Think,
            Choose,
            Lift,
            Flip,
            Travel,
            Land,
            CloseGap,
            Refill,
            All
        }

        /// <summary>LAB ONLY: the beat a house play stops after (and holds). The beats before it run
        /// at six times the speed, so the one being looked at is not buried in the others.</summary>
        public DealerBeat LabStopAfter = DealerBeat.All;

        private readonly List<FanCard> fan = new List<FanCard>();
        private Transform currentFlight;
        private float isoBaseSpeed = 1f;
        private SpriteRenderer fanShadow;
        private SpriteRenderer fanGlow;
        private SpriteRenderer scanGlint;
        private readonly SpriteRenderer[] thinkDots = new SpriteRenderer[3];
        private readonly List<SpriteRenderer> footprint = new List<SpriteRenderer>();
        private float thinking;
        private float thinkT0;
        private float footprintA, footprintTarget;
        /// <summary>How many of the pooled footprint lights belong to the card in flight. The rest
        /// stay dark: a pooled light from an earlier, bigger block must never warm a cell this
        /// block is not going to.</summary>
        private int footprintCount;
        private int chosenSlot = -1;
        private Vector2 debugFlightFrom, debugFlightCtrl, debugFlightTo;
        private bool debugFlightOn;

        private float CardW
        {
            get { return CardVisual.BodyWidth * L.DealerCardScale; }
        }

        private float CardH
        {
            get { return CardVisual.BodyHeight * L.DealerCardScale; }
        }

        private void BuildDealer()
        {
            fanShadow = Sprite("FanShadow", BlackjackShapes.RadialSprite, Color.black, DealerHandOrder - 2);
            fanGlow = ViewUtil.MakePlate(transform, "FanGlow", Vector2.zero, Vector2.one, BlackjackShapes.Gold, DealerHandOrder - 1, ViewUtil.GlowSprite);
            scanGlint = Sprite("ScanGlint", BlackjackShapes.RadialSprite, BlackjackShapes.Cream, DealerHandOrder + 12);
            for (int i = 0; i < thinkDots.Length; i++)
            {
                thinkDots[i] = Sprite("ThinkDot" + i, BlackjackShapes.RadialSprite, BlackjackShapes.Gold, DealerHandOrder + 1);
            }
        }

        private void PlaceDealer()
        {
            float t = L.Type;
            Size(fanShadow, L.DealerHand + new Vector2(0f, -CardH * 0.42f), new Vector2(CardW * 3.4f, CardH * 0.5f));
            fanGlow.transform.localPosition = L.DealerHand;
            fanGlow.size = new Vector2(CardW * 3.2f, CardH * 1.5f);
            for (int i = 0; i < thinkDots.Length; i++)
            {
                Size(thinkDots[i], L.DealerHand + new Vector2((i - 1) * 0.13f * t, -CardH * 0.66f), new Vector2(0.08f * t, 0.08f * t));
            }
            LayoutFan(true);
        }

        private void ResetDealer()
        {
            if (currentFlight != null)
            {
                Destroy(currentFlight.gameObject);
                currentFlight = null;
            }
            for (int i = 0; i < fan.Count; i++)
            {
                Destroy(fan[i].Root.gameObject);
            }
            fan.Clear();
            thinking = 0f;
            chosenSlot = -1;
            footprintTarget = footprintA = 0f;
            footprintCount = 0;
            debugFlightOn = false;
            for (int i = 0; i < footprint.Count; i++)
            {
                footprint[i].enabled = false;
            }
        }

        /// <summary>How many cards the closed hand shows right now.</summary>
        public int DealerCardCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < fan.Count; i++)
                {
                    if (!fan[i].Leaving)
                    {
                        n++;
                    }
                }
                return n;
            }
        }

        private FanCard NewFanCard(Vector2 at)
        {
            var c = new FanCard();
            c.Root = new GameObject("HouseCard").transform;
            c.Root.SetParent(transform, false);
            c.Back = c.Root.gameObject.AddComponent<SpriteRenderer>();
            c.Back.sprite = BlackjackShapes.CardBackSprite;
            c.Pos = c.TargetPos = at;
            c.Alpha = 0f;
            return c;
        }

        /// <summary>Shows exactly <paramref name="count"/> closed cards, at once (no motion).</summary>
        public void SetDealerCards(int count)
        {
            count = Mathf.Max(0, count);
            while (DealerCardCount > count)
            {
                FanCard last = fan[fan.Count - 1];
                Destroy(last.Root.gameObject);
                fan.RemoveAt(fan.Count - 1);
            }
            while (DealerCardCount < count)
            {
                FanCard c = NewFanCard(L.DealerHand);
                c.Alpha = 1f;
                fan.Add(c);
            }
            LayoutFan(true);
        }

        /// <summary>Deals the house's closed hand in, one card after another from its edge.</summary>
        public IEnumerator DealDealerCards(int count)
        {
            SetDealerCards(0);
            for (int i = 0; i < count; i++)
            {
                FanCard c = NewFanCard(L.DealerEdge);
                fan.Add(c);
                LayoutFan(false);
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.CardSlide, 1f + 0.04f * i, 0.7f);
                }
                yield return Wait(T(0.07f));
            }
            yield return Wait(T(0.18f));
        }

        /// <summary>The closed hand slides away into the dark at the house's edge (between hands).</summary>
        public IEnumerator SlideDealerCardsAway()
        {
            for (int i = 0; i < fan.Count; i++)
            {
                fan[i].Leaving = true;
                fan[i].TargetPos = L.DealerEdge + new Vector2(0.3f * i, 0f);
                fan[i].TargetAlpha = 0f;
            }
            if (fan.Count > 0 && Sfx != null)
            {
                Sfx.Casino(BlackjackCue.CardSlide, 0.9f, 0.6f);
            }
            yield return Wait(T(0.3f));
            SetDealerCards(0);
        }

        /// <summary>Where each of n closed cards sits: a slight, cold fan.</summary>
        private Vector2 SlotPos(int i, int n, out float rot)
        {
            float mid = (n - 1) * 0.5f;
            float spacing = CardW * 0.58f;
            float span = spacing * (n - 1);
            if (span > CardW * 2.4f && n > 1)
            {
                spacing = CardW * 2.4f / (n - 1);
            }
            float off = i - mid;
            rot = -off * 3.5f;
            return L.DealerHand + new Vector2(off * spacing, -Mathf.Abs(off) * 0.012f * L.Type);
        }

        private void LayoutFan(bool snap)
        {
            var live = new List<FanCard>();
            for (int i = 0; i < fan.Count; i++)
            {
                if (!fan[i].Leaving)
                {
                    live.Add(fan[i]);
                }
            }
            for (int i = 0; i < live.Count; i++)
            {
                float rot;
                live[i].TargetPos = SlotPos(i, live.Count, out rot);
                live[i].TargetRot = rot;
                live[i].TargetAlpha = 1f;
                if (snap)
                {
                    live[i].Pos = live[i].TargetPos;
                    live[i].Rot = rot;
                    live[i].Alpha = 1f;
                }
            }
        }

        private void TickDealer(float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 18f * Mathf.Max(0.05f, BlackjackFx.Tuning.Speed));
            float s = L.DealerCardScale * 1.35f;
            for (int i = fan.Count - 1; i >= 0; i--)
            {
                FanCard c = fan[i];
                c.Pos = Vector2.Lerp(c.Pos, c.TargetPos, k);
                c.Rot = Mathf.Lerp(c.Rot, c.TargetRot, k);
                c.Alpha = Mathf.MoveTowards(c.Alpha, c.TargetAlpha, dt * 6f * BlackjackFx.Tuning.Speed);
                c.Dim = Mathf.Lerp(c.Dim, c.TargetDim, k);
                c.Root.localPosition = new Vector3(c.Pos.x, c.Pos.y, 0f);
                c.Root.localRotation = Quaternion.Euler(0f, 0f, c.Rot);
                c.Root.localScale = new Vector3(s, s, 1f);
                float v = 1f - 0.4f * c.Dim;
                c.Back.color = new Color(v, v, v, c.Alpha);
                c.Back.sortingOrder = DealerHandOrder + 2 + i;
                if (c.Leaving && c.Alpha <= 0.01f)
                {
                    Destroy(c.Root.gameObject);
                    fan.RemoveAt(i);
                }
            }
            int n = DealerCardCount;
            Alpha(fanShadow, n > 0 ? BlackjackFx.DarkAlpha(0.35f) : 0f);
            // thinking: the brass behind the fan breathes, three gold points step, a glint scans the edges
            float think = thinking;
            float age = Time.time - thinkT0;
            fanGlow.color = new Color(BlackjackShapes.Gold.r, BlackjackShapes.Gold.g, BlackjackShapes.Gold.b,
                BlackjackFx.LightAlpha(think * (0.25f + 0.12f * Mathf.Sin(age * 9f))));
            for (int i = 0; i < thinkDots.Length; i++)
            {
                float phase = Mathf.Repeat(age * 3.2f - i * 0.33f, 1f);
                Alpha(thinkDots[i], think * (0.25f + 0.65f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * phase))));
            }
            if (think > 0.01f && n > 0)
            {
                float sweep = Mathf.Repeat(age * 2.1f, 1f);
                float half = CardW * 0.58f * (n - 1) * 0.5f + CardW * 0.5f;
                scanGlint.transform.localPosition = L.DealerHand + new Vector2(Mathf.Lerp(-half, half, sweep), CardH * 0.47f);
                scanGlint.transform.localScale = new Vector3(0.32f * L.Type, 0.07f * L.Type, 1f);
                Alpha(scanGlint, think * 0.55f * Mathf.Sin(Mathf.PI * sweep));
            }
            else
            {
                Alpha(scanGlint, 0f);
            }
            footprintA = Mathf.MoveTowards(footprintA, footprintTarget, dt * 5f * BlackjackFx.Tuning.Speed);
            for (int i = 0; i < footprint.Count; i++)
            {
                if (i < footprintCount && footprintA > 0f)
                {
                    Alpha(footprint[i], BlackjackFx.LightAlpha(0.55f) * footprintA * (0.85f + 0.15f * Mathf.Sin(clock * 6f + i)));
                }
                else
                {
                    footprint[i].enabled = false;
                }
            }
        }

        // ================================================================== the play

        /// <summary>
        /// The house plays one card: think, choose, rise, travel and turn over, land - then
        /// <paramref name="onLand"/> puts the block on the arena - close the gap, refill. Every
        /// fact comes from Core's report: the slot it left, the hand's size before and after, the
        /// card and the shape it was laid as, and where it went.
        /// </summary>
        public IEnumerator PlayDealerCard(int slot, int handBefore, int handAfter, BlockCard card,
            BlockShape shape, Vector2 landAt, IList<Vector2> cells, float cellSize, float thinkSeconds, Action onLand)
        {
            if (DealerCardCount != handBefore)
            {
                SetDealerCards(handBefore);
            }
            if (slot < 0 || slot >= handBefore)
            {
                slot = Mathf.Max(0, handBefore - 1);
            }
            DealerBeat stop = LabStopAfter;
            bool iso = stop != DealerBeat.All;
            isoBaseSpeed = BlackjackFx.Tuning.Speed;
            if (iso && stop != DealerBeat.Think)
            {
                BlackjackFx.Tuning.Speed = isoBaseSpeed * 6f;
            }
            // ---- think
            thinking = 1f;
            thinkT0 = Time.time;
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.DealerThink);
            }
            yield return Wait(T(thinkSeconds));
            if (stop == DealerBeat.Think)
            {
                yield break;
            }
            thinking = 0f;

            // ---- choose: forward, the others sit back
            Focus(stop, DealerBeat.Choose);
            FanCard chosen = LiveCard(slot);
            chosenSlot = slot;
            if (chosen != null)
            {
                for (int i = 0; i < fan.Count; i++)
                {
                    fan[i].TargetDim = fan[i] == chosen ? 0f : 1f;
                }
                chosen.TargetPos += new Vector2(0f, BlackjackFx.Tuning.dealerChosenCardOffset * L.Type);
                chosen.TargetRot = 0f;
            }
            yield return Wait(T(BlackjackFx.Tuning.dealerChosenDuration + BlackjackFx.Tuning.dealerRevealDuration));
            if (stop == DealerBeat.Choose)
            {
                yield break;
            }

            // ---- the flight: the fan's card becomes the travelling card
            Focus(stop, DealerBeat.Lift);
            Vector2 from = chosen != null ? chosen.Pos : L.DealerHand;
            float fromRot = chosen != null ? chosen.Rot : 0f;
            if (chosen != null)
            {
                fan.Remove(chosen);
                Destroy(chosen.Root.gameObject);
            }
            for (int i = 0; i < fan.Count; i++)
            {
                fan[i].TargetDim = 0.35f;
            }
            var flight = new GameObject("HouseCardFlight").transform;
            flight.SetParent(transform, false);
            currentFlight = flight;
            if (stop == DealerBeat.Flip || stop == DealerBeat.Travel)
            {
                BlackjackFx.Tuning.Speed = isoBaseSpeed;
            }
            var flip = new GameObject("Flip").transform;
            flip.SetParent(flight, false);
            SpriteRenderer back = flip.gameObject.AddComponent<SpriteRenderer>();
            back.sprite = BlackjackShapes.CardBackSprite;
            back.sortingOrder = FlightOrder;
            CardVisual face = null;
            ShowFootprint(cells, cellSize);
            float s0 = L.DealerCardScale;
            float sPeak = s0 * 1.35f;
            Vector2 ctrl = new Vector2((from.x + landAt.x) * 0.5f, Mathf.Max(from.y, landAt.y) + 0.9f * L.Type);
            debugFlightFrom = from;
            debugFlightCtrl = ctrl;
            debugFlightTo = landAt;
            debugFlightOn = true;
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.CardSlide);
            }
            float travel = T(BlackjackFx.Tuning.dealerCardTravelDuration);
            float t0 = Time.time;
            bool flipped = false;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / travel);
                float e = BlackjackFx.EaseInOut(k);
                // it rises first: the first fifth is mostly lift
                Vector2 pos = BlackjackFx.Bezier(from, ctrl, landAt, e);
                flight.localPosition = pos;
                flight.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromRot, 0f, e) + Mathf.Sin(Mathf.PI * k) * -4f);
                float sc = Mathf.Lerp(s0, sPeak, Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.2f)) * 0.5f + e * 0.5f);
                flight.localScale = new Vector3(sc, sc, 1f);
                // the turn: scaleX through zero at the middle of the flight
                float flipK = Mathf.Clamp01((k - 0.5f) / Mathf.Max(0.05f, BlackjackFx.Tuning.dealerCardFlipDuration / BlackjackFx.Tuning.dealerCardTravelDuration) + 0.5f);
                float sx = Mathf.Abs(Mathf.Cos(Mathf.PI * flipK));
                if (!flipped && flipK >= 0.5f)
                {
                    flipped = true;
                    back.enabled = false;
                    if (card != null)
                    {
                        face = CardVisual.Create(flip, "HouseCardFace", card, true, false, Vector2.zero, FlightOrder, shape);
                    }
                    if (Sfx != null)
                    {
                        Sfx.Casino(BlackjackCue.CardFlick);
                    }
                }
                flip.localScale = new Vector3(Mathf.Max(0.02f, sx) * (back.enabled ? 1.35f : 1f), back.enabled ? 1.35f : 1f, 1f);
                if ((stop == DealerBeat.Lift && k >= 0.22f) || (stop == DealerBeat.Flip && flipped && flipK >= 0.9f))
                {
                    BlackjackFx.Tuning.Speed = isoBaseSpeed;
                    yield break;
                }
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
            if (stop == DealerBeat.Travel)
            {
                BlackjackFx.Tuning.Speed = isoBaseSpeed;
                yield break;
            }
            Focus(stop, DealerBeat.Land);
            // ---- the landing: a small squash, a tap on the felt, and the block appears
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.CardLand);
                Sfx.Casino(BlackjackCue.TableTap, 1.1f, 0.6f);
            }
            float land = T(BlackjackFx.Tuning.dealerCardLandDuration);
            float sq = BlackjackFx.Tuning.dealerCardLandSquash;
            t0 = Time.time;
            bool landedBlock = false;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / land);
                float y = k < 0.35f ? Mathf.Lerp(1f, 1f + sq, k / 0.35f)
                    : k < 0.7f ? Mathf.Lerp(1f + sq, 1f - sq, (k - 0.35f) / 0.35f)
                    : Mathf.Lerp(1f - sq, 1f, (k - 0.7f) / 0.3f);
                flight.localScale = new Vector3(sPeak * (2f - y), sPeak * y, 1f);
                if (!landedBlock && k >= 0.35f)
                {
                    landedBlock = true;
                    if (onLand != null)
                    {
                        onLand();
                    }
                }
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
            if (stop == DealerBeat.Land)
            {
                BlackjackFx.Tuning.Speed = isoBaseSpeed;
                yield break;
            }
            // the card gives way to its block
            float fade = T(0.08f);
            t0 = Time.time;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / fade);
                if (face != null)
                {
                    face.SetAlpha(1f - k);
                }
                flight.localScale = new Vector3(sPeak * (1f - 0.12f * k), sPeak * (1f - 0.12f * k), 1f);
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
            Destroy(flight.gameObject);
            currentFlight = null;
            footprintTarget = 0f;
            debugFlightOn = false;
            chosenSlot = -1;

            // ---- the gap closes, then the refill slides in face-down from the house's edge
            Focus(stop, DealerBeat.CloseGap);
            for (int i = 0; i < fan.Count; i++)
            {
                fan[i].TargetDim = 0f;
            }
            LayoutFan(false);
            yield return Wait(T(BlackjackFx.Tuning.dealerGapCloseDuration));
            if (stop == DealerBeat.CloseGap)
            {
                BlackjackFx.Tuning.Speed = isoBaseSpeed;
                yield break;
            }
            Focus(stop, DealerBeat.Refill);
            int refills = handAfter - DealerCardCount;
            for (int i = 0; i < refills; i++)
            {
                fan.Add(NewFanCard(L.DealerEdge));
                LayoutFan(false);
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.CardSlide, 1.08f, 0.55f);
                }
                yield return Wait(T(BlackjackFx.Tuning.dealerRefillDuration * 0.5f));
            }
            if (refills > 0)
            {
                yield return Wait(T(BlackjackFx.Tuning.dealerRefillDuration * 0.5f));
            }
            else if (handAfter < DealerCardCount)
            {
                SetDealerCards(handAfter);
            }
            if (iso)
            {
                BlackjackFx.Tuning.Speed = isoBaseSpeed;
            }
        }

        private void Focus(DealerBeat stop, DealerBeat beat)
        {
            if (stop == beat)
            {
                BlackjackFx.Tuning.Speed = isoBaseSpeed;
            }
        }

        /// <summary>LAB: the house thinking, held for a while.</summary>
        public IEnumerator LabThink(float seconds)
        {
            thinking = 1f;
            thinkT0 = Time.time;
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.DealerThink);
            }
            yield return Wait(seconds);
            thinking = 0f;
        }

        private FanCard LiveCard(int slot)
        {
            int n = 0;
            for (int i = 0; i < fan.Count; i++)
            {
                if (fan[i].Leaving)
                {
                    continue;
                }
                if (n == slot)
                {
                    return fan[i];
                }
                n++;
            }
            return null;
        }

        /// <summary>A soft warm light in each cell the block is about to stand in.</summary>
        private void ShowFootprint(IList<Vector2> cells, float cellSize)
        {
            int n = cells != null ? cells.Count : 0;
            while (footprint.Count < n)
            {
                footprint.Add(Sprite("Footprint" + footprint.Count, BlackjackShapes.RadialSprite, BlackjackShapes.Amber, FootprintOrder));
            }
            for (int i = 0; i < footprint.Count; i++)
            {
                if (i < n)
                {
                    Size(footprint[i], cells[i], new Vector2(cellSize * 1.25f, cellSize * 1.25f));
                    footprint[i].enabled = true;
                }
                else
                {
                    footprint[i].enabled = false;
                }
            }
            footprintCount = n;
            footprintA = 0f;
            footprintTarget = n > 0 ? 1f : 0f;
        }

        /// <summary>The thinking pause for one play: deterministic per hand and play, 0.25-0.55 s.</summary>
        public static float ThinkSecondsFor(int hand, int play)
        {
            float k = BlackjackFx.Hash01(hand * 31 + 7, play * 17 + 3);
            return Mathf.Lerp(BlackjackFx.Tuning.dealerThinkMin, BlackjackFx.Tuning.dealerThinkMax, k);
        }
    }
}
