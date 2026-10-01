// PURPOSE: "Tamagotchi" EATING THE CARD IT WAS FED - the brief's main feed sequence, and the
// satisfaction that follows: CARD ACCEPT -> PET GRAB -> FIRST CHOMP -> CHEW -> NEXT CHOMP -> FINAL
// CHOMP -> GULP -> SATISFACTION -> REQUEST PLATE COLLAPSE.
//
// CORE FIRST. By the time this plays the card is out of the run (TamagotchiBoss.TryFeed ->
// RoundEngine.FeedCardToBoss) and the hand has a hole in it the controller keeps open
// (CardLayerView.SetPetGap) until the next placement's refill; this only SHOWS the meal, off the
// report, on a proxy of the card's own face (TamagotchiFoodProxy). The proxy comes from where the
// card was let go, is taken in two paws (it gives a pixel or two), and is eaten in bites: the bite
// line sits at the mouth and the card is pushed up across it a chomp at a time - 0.35, 0.70, 1 - so
// what is left always has the scalloped edge of the last bite, throws 2-4 flecks of its own colour,
// and shrinks 1 -> 0.85 -> 0.55 on the way. Never scaled to nothing and faded (the brief's FAIL).
//
// THE VALUE TIER IS CORE'S and only changes the ceremony: a LOW card is a quick snack (x0.82), a
// HIGH one is savoured (x1.18, one more chew, a bigger bounce, an eye sparkle and a heart or two).
// EXTENSION POINT: the timings are Tuning's; the shapes of the beats are here.

using System.Collections;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        private TamagotchiFoodProxy currentFood;

        /// <summary>
        /// Plays a meal Core has already resolved. <paramref name="from"/> is where the card was
        /// let go (or where it sat in the hand), <paramref name="fromScale"/> the scale it was drawn
        /// at there.
        /// </summary>
        public void PlayFeed(TamagotchiFeedVisuals report, Vector2 from, float fromScale)
        {
            if (report == null || !shown)
            {
                return;
            }
            ClearDrag();
            mealsOnTheWay.Add(report.RequestSlotId);
            TamagotchiFeedVisuals r = report;
            Enqueue(PetPresentationPriority.Feed, "feed", () => FeedRoutine(r, from, fromScale), false, 0f, null);
            // the brief's short lock: only for the snap and the grab
            LockFor(Tuning.FoodSnapDuration + Tuning.GrabDuration + 0.02f);
        }

        private float FeedMultiplier(CardValueTier tier)
        {
            float tierPace = tier == CardValueTier.High ? Tuning.HighValueFeedMultiplier
                : tier == CardValueTier.Low ? Tuning.LowValueFeedMultiplier : 1f;
            return tierPace * Tuning.FeedPace;
        }

        /// <summary>
        /// How big a card is while the pet holds it. It is held UNDER the mouth, on the belly, so
        /// what it may measure is the height from the mouth to the floor (the mouth is at 0.5 of a
        /// body unit): a card sized by the body's WIDTH instead came out nearly as tall as the pet
        /// and hung half of itself below the edge of the screen.
        /// </summary>
        private float HoldHeight
        {
            get { return 0.46f * S; }
        }

        private float HoldScale
        {
            get { return HoldHeight / CardVisual.BodyHeight; }
        }

        /// <summary>Where a held card sits: centred under the mouth with its top just below it.</summary>
        private Vector3 HoldPoint(float scale)
        {
            Vector3 mouth = rig.MouthWorld;
            float half = CardVisual.BodyHeight * scale * 0.5f;
            return mouth + new Vector3(F * 0.02f * S, -half - 0.035f * S, 0f);
        }

        /// <summary>The bite line: at the mouth, turned with the head.</summary>
        private void BiteAtMouth(TamagotchiFoodProxy food)
        {
            if (food == null)
            {
                return;
            }
            Vector3 mouth = rig.MouthWorld + new Vector3(0f, -0.012f * S, 0f);
            // the line turns with the head (half of its tilt: the jaw is not the whole head)
            food.SetBite(mouth, pose.HeadTilt * Mathf.Deg2Rad * 0.5f, true);
        }

        private IEnumerator FeedRoutine(TamagotchiFeedVisuals r, Vector2 from, float fromScale)
        {
            SetState(PetViewState.Eating);
            SetParticleBudget(8);
            float m = FeedMultiplier(r.Tier);
            bool high = r.Tier == CardValueTier.High;
            bool low = r.Tier == CardValueTier.Low;
            bool bites = Lod != PetLod.Low;

            var food = TamagotchiFoodProxy.ForCard(flight, r.Card, 1, bites);
            currentFood = food;
            food.Tier = r.Tier;
            food.transform.position = from;
            food.transform.localScale = new Vector3(fromScale, fromScale, 1f);
            food.InitialRect = new Rect(from - new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight) * fromScale * 0.5f,
                new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight) * fromScale);
            float hold = HoldScale;
            // the card turns toward the mouth's side as it comes: +-5..12 degrees
            float turnTo = Mathf.Clamp((from.x - rig.MouthWorld.x) * 4f, -12f, 12f);
            if (Mathf.Abs(turnTo) < 5f)
            {
                turnTo = 5f * (turnTo >= 0f ? 1f : -1f);
            }

            // ---- A: CARD ACCEPT - from the finger to the paws, ease-in
            Beat("accept");
            act.Emotion = PetEmotion.Excited;
            act.Mouth = "wide";
            act.MouthOpen = 0.9f;
            yield return Tween(Tuning.FoodSnapDuration * m, t =>
            {
                float k = EaseIn(t);
                Vector3 to = HoldPoint(hold);
                food.transform.position = Vector3.Lerp(from, to, k);
                float s = Mathf.Lerp(fromScale, hold, k);
                FoodScale(food, s, s);
                food.transform.rotation = Quaternion.Euler(0f, 0f, turnTo * k);
                act.LookAt = food.transform.position;
                act.PawL = PawToward(false, food.transform.position, 0.05f * k);
                act.PawR = PawToward(true, food.transform.position, 0.05f * k);
                act.Offset = (Vector2)((food.transform.position - rig.MouthWorld).normalized) * Px(4f) * k;
            });
            // into the pet's own food band: in front of the face, behind the paws that hold it
            food.transform.SetParent(rig.FoodRoot, true);
            food.SetOrder(TamagotchiRig.FoodOrder);
            Sound(PetSound.GrabFood);
            if (StopAfter("accept"))
            {
                yield return LabEnd(food);
                yield break;
            }

            // ---- B: GRAB - both paws on its edges; it gives a pixel or two
            Beat("grab");
            Haptic(high ? PetHaptic.FeedTapHigh : PetHaptic.FeedTap);
            yield return Tween(Tuning.GrabDuration * m, t =>
            {
                Vector3 c = HoldPoint(hold);
                food.transform.position = c;
                food.transform.rotation = Quaternion.Euler(0f, 0f, turnTo * (1f - 0.5f * t));
                float squeeze = 1f - 0.03f * Bell(t) - 0.012f * t;
                FoodScale(food, hold * squeeze, hold);
                float half = CardVisual.BodyWidth * hold * 0.5f;
                act.PawL = PawToward(false, c + new Vector3(-half, 0.02f * S, 0f), 0.06f);
                act.PawR = PawToward(true, c + new Vector3(half, 0.02f * S, 0f), 0.06f);
                act.Offset = Vector2.Lerp(act.Offset, Vector2.zero, t);
            });


            if (StopAfter("grab"))
            {
                yield return LabEnd(food);
                yield break;
            }
            // ---- C: FIRST CHOMP - mouth snaps open, the head comes forward, 20-35% goes in
            Beat("first bite");
            float first = low ? 0.25f : high ? 0.35f : 0.3f;
            yield return Chomp(food, 0f, first, 1f, Tuning.BiteDuration * m, true);

            if (StopAfter("first bite"))
            {
                yield return LabEnd(food);
                yield break;
            }
            // ---- D: CHEW - two or three, cheeks bulging left and right in turn
            Beat("chew");
            int chews = Tuning.ChewCount + (high ? 1 : 0);
            yield return Chew(food, chews, Tuning.ChewDuration * m, null);

            if (StopAfter("chew"))
            {
                yield return LabEnd(food);
                yield break;
            }
            // ---- E: NEXT CHOMP - further in, a little smaller
            Beat("next bite");
            yield return Chomp(food, first, 0.7f, 0.85f, Tuning.BiteDuration * 0.5f * m, false);
            if (StopAfter("next bite"))
            {
                yield return LabEnd(food);
                yield break;
            }

            // ---- the last little strip: quick
            Beat("final");
            yield return Chomp(food, 0.7f, 1.0f, 0.55f, Tuning.BiteDuration * 0.4f * m, false);
            DropFood(food);
            if (StopAfter("final"))
            {
                yield return LabEnd(null);
                yield break;
            }

            // ---- F: GULP - mouth shut, head up a touch, a wave from the face down to the belly
            Beat("gulp");
            yield return Gulp(Tuning.GulpDuration * m, high);

            if (StopAfter("gulp"))
            {
                yield return LabEnd(null);
                yield break;
            }
            // ---- SATISFACTION
            Beat("satisfaction");
            CollapsePlate(r.RequestSlotId);
            yield return Satisfied(Tuning.SatisfiedDuration * m, high);

            if (StopAfter("satisfaction"))
            {
                yield return LabEnd(null);
                yield break;
            }
            if (r.RequestsRemaining > 0)
            {
                Beat("glance");
                // the other plate, briefly: "one down - I want that one too"
                int other = FirstPending();
                act.Emotion = PetEmotion.Hungry;
                act.PawL = null;
                act.PawR = null;
                act.LookAt = PlateWorld(other);
                HeadToward(PlateWorld(other), 0.8f);
                act.Mouth = "nom";
                Say("Bir lokma! Öbürünü de istiyor.", "One down - it wants the other one too.");
                yield return Wait(0.38f);
            }
            else
            {
                Beat("full");
                yield return FullSatisfaction();
            }
        }

        /// <summary>
        /// One chomp: the mouth snaps open and the head comes forward a few pixels, then the jaw
        /// shuts and the card jumps <paramref name="to"/> of its height across the bite line - the
        /// edge it leaves is the bite's. A few flecks of the card's colour fly.
        /// </summary>
        private IEnumerator Chomp(TamagotchiFoodProxy food, float from, float to, float scaleTo, float seconds, bool hero)
        {
            float hold = HoldScale;
            float cardH = CardVisual.BodyHeight;
            float scaleFrom = food.transform.lossyScale.y / Mathf.Max(0.0001f, hold);
            Sound(hero ? PetSound.Chomp : PetSound.ChompLight);
            bool flecked = false;
            yield return Tween(seconds, t =>
            {
                // open over the first 40%, the bite lands at 55%
                float open = t < 0.4f ? EaseOut(t / 0.4f) : 1f - EaseIn((t - 0.4f) / 0.6f);
                act.Mouth = t < 0.55f ? "wide" : "chew_b";
                act.MouthOpen = 0.8f + 0.45f * open;
                act.Head = new Vector2(0f, -0.022f * open);
                act.Squash = new Vector2(1f + 0.012f * open, 1f - 0.012f * open);
                float bite = t < 0.55f ? from : Mathf.Lerp(from, to, EaseOut((t - 0.55f) / 0.25f));
                float s = Mathf.Lerp(scaleFrom, scaleTo, Mathf.Clamp01((t - 0.55f) / 0.45f)) * hold;
                FoodScale(food, s, s);
                // the card rides up across the line: what has gone in is above the mouth
                Vector3 c = HoldPoint(s) + new Vector3(0f, bite * cardH * s, 0f);
                food.transform.position = c;
                food.BiteProgress = bite;
                if (food.ShaderBites)
                {
                    BiteAtMouth(food);
                }
                else
                {
                    // LOW: no cut - the card steps smaller with each chomp instead
                    float k = Mathf.Lerp(1f, 0.2f, bite);
                    FoodScale(food, s * k, s * k);
                }
                float half = CardVisual.BodyWidth * s * 0.5f;
                act.PawL = PawToward(false, c + new Vector3(-half, -0.06f * S, 0f), 0.05f);
                act.PawR = PawToward(true, c + new Vector3(half, -0.06f * S, 0f), 0.05f);
                if (!flecked && t >= 0.55f)
                {
                    flecked = true;
                    BiteFlecks(rig.MouthWorld, food.Colour, hero ? 4 : 2);
                    act.Squash = new Vector2(1.03f, 0.97f);
                }
            });
        }

        /// <summary>Chewing: the mouth works between its two chewing shapes, the cheeks bulge in
        /// turn (2-3 px), the body squashes in time. <paramref name="tint"/> fills the cheeks
        /// (the board's own material after a bite of it).</summary>
        private IEnumerator Chew(TamagotchiFoodProxy food, int count, float each, Color? tint)
        {
            float bulge = Tuning.CheekBulge / 2.5f;
            act.PuffTint = tint;
            for (int i = 0; i < count; i++)
            {
                bool left = i % 2 == 0;
                Sound(PetSound.Chew);
                yield return Tween(each, t =>
                {
                    act.Mouth = t < 0.5f ? "chew_a" : "chew_b";
                    act.MouthOpen = 1f;
                    float k = Bell(t);
                    act.PuffL = left ? 0.75f * k * bulge : 0.15f * k;
                    act.PuffR = left ? 0.15f * k : 0.75f * k * bulge;
                    act.Squash = new Vector2(1f + 0.02f * k, 1f - 0.02f * k);
                    act.Head = new Vector2(0f, -0.006f * k);
                });
            }
            act.PuffL = 0f;
            act.PuffR = 0f;
        }

        /// <summary>Sets a proxy's WORLD scale whatever it is parented to (the pet's own root
        /// is scaled to the character's size, the flight layer is not).</summary>
        private static void FoodScale(TamagotchiFoodProxy food, float sx, float sy)
        {
            Transform parent = food.transform.parent;
            float ps = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;
            food.transform.localScale = new Vector3(sx / ps, sy / ps, 1f);
        }

        private void DropFood(TamagotchiFoodProxy food)
        {
            TamagotchiFoodProxy.Recycle(food);
            if (currentFood == food)
            {
                currentFood = null;
            }
        }

        /// <summary>The gulp: mouth shut, head up a touch, the face squeezing and then the belly
        /// swelling 1 -> 1.04 -> 1. No particle.</summary>
        private IEnumerator Gulp(float seconds, bool big)
        {
            act.Mouth = "closed";
            act.PawL = null;
            act.PawR = null;
            Sound(big ? PetSound.GulpBig : PetSound.Gulp);
            float belly = Tuning.BellyGulpScale * (big ? 1.25f : 1f);
            yield return Tween(seconds, t =>
            {
                act.Head = new Vector2(0f, 0.018f * Bell(Mathf.Clamp01(t * 1.6f)));
                act.HeadSquash = 1f - 0.07f * Bell(Mathf.Clamp01(t * 1.8f));
                act.Belly = belly * Bell(Mathf.Clamp01((t - 0.3f) / 0.7f));
                act.MouthOpen = 1f;
            });
            act.HeadSquash = 1f;
            act.Head = Vector2.zero;
        }

        /// <summary>Crescent eyes, brighter cheeks, paws on the belly, a 3-5 px bounce - and for a
        /// valuable meal a sparkle in the eyes, a bigger bounce and a heart or two.</summary>
        private IEnumerator Satisfied(float seconds, bool high)
        {
            Sound(PetSound.Satisfied);
            act.Emotion = PetEmotion.Happy;
            act.Cheek = TamagotchiArt.CheekBright;
            act.CheekA = 0.88f;
            act.PawL = BellyPaw(false);
            act.PawR = BellyPaw(true);
            float bounce = Px(high ? 6f : 4f);
            if (high)
            {
                HeartMotes(rig.EyeWorld(F < 0) + new Vector3(0f, 0.18f * S, 0f), 2);
            }
            yield return Tween(seconds, t =>
            {
                act.Offset = new Vector2(0f, bounce * Bell(Mathf.Clamp01(t * 1.6f)));
                float land = Mathf.Clamp01((t - 0.62f) / 0.38f);
                act.Squash = new Vector2(1f + 0.03f * Bell(land), 1f - 0.035f * Bell(land));
                if (high)
                {
                    act.SparkleL = Bell(t);
                    act.SparkleR = Bell(Mathf.Clamp01(t * 1.1f - 0.05f));
                }
            });
            act.SparkleL = 0f;
            act.SparkleR = 0f;
        }

        /// <summary>
        /// 2/2: the small satisfaction sequence (~0.55 s) - both paws pat the belly, a happy blink,
        /// a soft side-to-side wobble, the plates fold away, a tiny warm pink aura and two warm
        /// motes, a relaxed settle; then it leans on its edge and gives a small yawn. No firework:
        /// the round goes on.
        /// </summary>
        private IEnumerator FullSatisfaction()
        {
            Say("Tamagotchi doydu - bu raunt sana dokunmayacak.", "Fed in full - it will leave you alone this round.");
            act.Emotion = PetEmotion.Happy;
            act.LookAt = rig.BellyWorld;
            act.Look = new Vector2(0f, -0.9f);
            FoldPlates();
            PuffAura(TamagotchiArt.WarmPink, 0.22f);
            WarmMotes(rig.BellyWorld + new Vector3(0f, 0.3f * S, 0f), 2);
            yield return Tween(0.56f, t =>
            {
                // two pats with both paws
                float pat = Bell(Mathf.Repeat(t * 2f, 1f));
                PetPaw l = BellyPaw(false);
                PetPaw rr = BellyPaw(true);
                l.Offset += new Vector2(0f, 0.035f * pat);
                rr.Offset += new Vector2(0f, 0.035f * pat);
                act.PawL = l;
                act.PawR = rr;
                act.Rot = 2.2f * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t);
                act.Blink = t > 0.45f && t < 0.75f ? Bell((t - 0.45f) / 0.3f) : 0f;
                act.Belly = 0.02f * pat;
            });
            act.Look = null;
            act.Blink = -1f;
            act.PawL = null;
            act.PawR = null;
            // it leans back against its edge, content, and yawns
            act.Emotion = PetEmotion.Sleepy;
            yield return Tween(0.35f, t =>
            {
                act.Offset = new Vector2(-F * Px(4f) * Smooth(t), 0f);
                act.Rot = F * 3f * Smooth(t);
            });
            act.Mouth = "medium";
            yield return Tween(0.45f, t =>
            {
                act.MouthOpen = Mathf.Lerp(0.5f, 1.05f, Bell(t));
                act.Lid = 0.55f + 0.2f * Bell(t);
            });
            act.Mouth = null;
            yield return Tween(0.3f, t =>
            {
                act.Offset = new Vector2(-F * Px(4f) * (1f - Smooth(t)), 0f);
                act.Rot = F * 3f * (1f - Smooth(t));
            });
        }

        /// <summary>A card it does not want was let go in its zone: "not this one". The card goes
        /// back to the hand (the controller); the pet acts it out. No red X.</summary>
        public void PlayWrongFood(Vector2 at)
        {
            if (!shown)
            {
                return;
            }
            Vector2 card = at;
            Enqueue(PetPresentationPriority.Request, "wrong food", () => WrongFood(card), true, 0f, null);
        }

        private IEnumerator WrongFood(Vector2 card)
        {
            ClearDrag();
            act.Emotion = PetEmotion.Confused;
            act.Mouth = "frown";
            Vector2 dir = (card - (Vector2)rig.MouthWorld).normalized;
            act.Head = -dir * 0.02f;
            act.HeadTilt = dir.x * 8f;
            act.LookAt = card;
            yield return Wait(0.18f);
            if (plates.Count > 0)
            {
                act.LookAt = PlateWorld(FirstPending());
                yield return Wait(0.24f);
            }
            act.LookAt = card;
            yield return Tween(0.2f, t => act.Rot = Mathf.Sin(t * Mathf.PI * 2f) * 2f);
        }

        /// <summary>Everything in flight or in the paws goes (a teardown).</summary>
        private void ClearFoodAndFlights()
        {
            TamagotchiFoodProxy.Recycle(currentFood);
            currentFood = null;
            for (int i = flight.childCount - 1; i >= 0; i--)
            {
                Transform child = flight.GetChild(i);
                TamagotchiFoodProxy.Recycle(child.GetComponent<TamagotchiFoodProxy>());
            }
            if (rig != null && rig.FoodRoot != null)
            {
                for (int i = rig.FoodRoot.childCount - 1; i >= 0; i--)
                {
                    TamagotchiFoodProxy held = rig.FoodRoot.GetChild(i).GetComponent<TamagotchiFoodProxy>();
                    if (held != null)
                    {
                        TamagotchiFoodProxy.Recycle(held);
                    }
                    else
                    {
                        Destroy(rig.FoodRoot.GetChild(i).gameObject);
                    }
                }
            }
            ClearParticles();
            ClearPunishLeftovers();
        }
    }
}
