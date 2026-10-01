// PURPOSE: "Tamagotchi" MOVING HOUSE - how the pet gets from one edge of the screen to another. WHEN
// and WHERE is TamagotchiRoamingController's (and which places are safe is the game controller's:
// HomeCandidates); this file is only the going. It never teleports.
//
//   A  HIDE AND PEEK ELSEWHERE (the long way, and the one the brief pictures): it draws back slowly
//      - first the body is gone, then only the eyes are left watching, then the eyes go. 200-400 ms
//      of nothing. Somewhere else: two antennae, then the eyes - it steals a look at the player's
//      cards - then the head, and it settles in.
//   B  SCAMPER: a few quick little steps along the same edge, body low, leaning the way it runs.
//   C  HOP: one small arc down into a corner (or up onto a side), its shadow letting go of it, a
//      squash on landing.
//   D  SNEAK: it sinks until only the top of its head and its eyes are over the edge, and that slides
//      along to the new place before it comes up.
//   E  SIT: where the place is a seat it arrives, lets itself down, feet out, paws on its belly.
//   FURIOUS it does none of that. It is gone at once, its eyes are up somewhere else a moment later,
//   it is out faster than it left, and its head snaps to what it wants with a low growl. Stylised -
//   never a jump scare: it is always seen to leave before it is seen to arrive.
//
// THE PLATES GO WITH IT, and they do not fly across the screen: they fold away where they are and
// unfold beside the pet in its new place (200-300 ms in all). A bubble it was saying is dropped.
//
// CONTINUITY. A place is a TamagotchiHome; changing it changes the base the whole pet is drawn from.
// A move seen on screen (B, C, D) therefore swaps the home FIRST and then walks act.Travel from
// "where it was drawn" to nothing, so the last frame of the move is exactly the new home's rest and
// there is no hand-off. A hidden move (A, furious) swaps while nothing is visible.
// EXTENSION POINT: a new style is a PetMoveStyle and a routine here.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        /// <summary>When and where (presentation only).</summary>
        public readonly TamagotchiRoamingController Roaming = new TamagotchiRoamingController();

        /// <summary>The places that are safe to live in right now, asked of the game's controller
        /// (true = for a furious pet: nothing playful). Null in a context with no screen to solve.</summary>
        public Func<bool, List<TamagotchiHome>> HomeCandidates;

        /// <summary>A home nearer a point than the current one, or null (the furious pet moving
        /// toward what it is about to take). The controller's.</summary>
        public Func<Vector2, TamagotchiHome> HomeToward;

        /// <summary>Told when it has moved in somewhere (the controller remembers it for a resize).</summary>
        public event Action<TamagotchiHome> MovedHome;

        /// <summary>The lab's switch: it stays where it is.</summary>
        public bool LabNoRoam;

        public PetMoveStyle LastMoveStyle { get; private set; }

        /// <summary>The home it is moving to while a move plays (debug), else null.</summary>
        public TamagotchiHome MovingTo { get; private set; }

        private float platesAway;        // 0 = out, 1 = folded away for a move
        private bool platesAwayWanted;

        private void ResetRoaming()
        {
            Roaming.Reset(State.Seed, Home != null ? Home.Name : null, clock);
            platesAway = 0f;
            platesAwayWanted = false;
            MovingTo = null;
        }

        /// <summary>A turn was played (the controller calls it once per turn).</summary>
        public void NoteTurn()
        {
            Roaming.NoteTurn(clock);
        }

        /// <summary>The player touched something (any input).</summary>
        public void NoteInput()
        {
            Roaming.NoteInput(clock);
        }

        /// <summary>
        /// Called by the idle scheduler - so only when nothing else is playing, nothing is queued
        /// and no card is being dragged. True when a move was started.
        /// </summary>
        private bool TickRoaming()
        {
            if (LabNoRoam || HomeCandidates == null || !State.BossActive || !Roaming.Due(clock))
            {
                return false;
            }
            bool furious = State.Furious;
            List<TamagotchiHome> places = HomeCandidates(furious);
            TamagotchiHome to = Roaming.Choose(places, Home, furious);
            if (to == null)
            {
                // nowhere else is safe just now: ask again in a turn rather than every idle
                Roaming.Moved(null, clock);
                return false;
            }
            PlayRoam(to, Roaming.StyleFor(Home, to, furious));
            return true;
        }

        /// <summary>Moves it to <paramref name="to"/> (the lab's buttons, and the scheduler).</summary>
        public void PlayRoam(TamagotchiHome to, PetMoveStyle style)
        {
            if (to == null || !shown || exiting)
            {
                return;
            }
            TamagotchiHome target = to;
            PetMoveStyle how = style;
            Enqueue(PetPresentationPriority.Request, "roam: " + style, () => RoamRoutine(target, how), false, 0f,
                delegate { FinishMove(target); });
        }

        private IEnumerator RoamRoutine(TamagotchiHome to, PetMoveStyle style)
        {
            LastMoveStyle = style;
            MovingTo = to;
            DismissSpeech();
            ClearDrag();
            platesAwayWanted = true;
            switch (style)
            {
                case PetMoveStyle.Scamper:
                    yield return MoveScamper(to);
                    break;
                case PetMoveStyle.Hop:
                    yield return MoveHop(to);
                    break;
                case PetMoveStyle.Sneak:
                    yield return MoveSneak(to);
                    break;
                case PetMoveStyle.FuriousSnap:
                    yield return MoveFurious(to, Anchors.HandCentre);
                    break;
                default:
                    yield return MoveHideAndPeek(to);
                    break;
            }
            FinishMove(to);
            // E: where the place is a seat, it lets itself down into it
            if (to.Sit > 0.01f)
            {
                Beat("sit");
                yield return Tween(0.34f, t =>
                {
                    float k = 1f - Back(t, 1.8f);
                    act.Squash = new Vector2(1f + 0.05f * k, 1f - 0.06f * k);
                    act.PawL = BlendPaw(Paw(0f, 0f, -18f), BellyPaw(false), Smooth(t));
                    act.PawR = BlendPaw(Paw(0f, 0f, -18f), BellyPaw(true), Smooth(t));
                });
            }
            else if (!State.Furious)
            {
                yield return Wait(0.12f);
            }
            if (!State.Furious && State.Wants)
            {
                // now and then it says something about the new view
                if (idleRng.NextDouble() < 0.45)
                {
                    Sound(PetSound.Hungry);
                    Speak("Acıktım...", "I'm hungry...", false);
                }
            }
        }

        /// <summary>The move is over (or was cut): it lives at <paramref name="to"/>, plates out.</summary>
        private void FinishMove(TamagotchiHome to)
        {
            if (MovingTo == null)
            {
                return;
            }
            if (!ReferenceEquals(Home, to))
            {
                SwitchHomeHidden(to);
                presence = RestPresence;
            }
            MovingTo = null;
            travelClip = null;
            platesAwayWanted = false;
            act.Travel = Vector2.zero;
            act.Tilt = 0f;
            Roaming.Moved(to, clock);
            if (MovedHome != null)
            {
                MovedHome(to);
            }
        }

        /// <summary>The home changes while nothing of the pet is visible.</summary>
        private void SwitchHomeHidden(TamagotchiHome to)
        {
            Home = to;
            presence = 0f;
            offsetNow = HideDirection * Home.HideDepth;
            tiltNow2 = Home.Tilt;
            sitNow = Home.Sit;
            rotNow = 0f;
            rig.UnitScale = Home.UnitScale;
            rig.Home = Home.Base;
            rig.SetClip(Home.Clip);
        }

        /// <summary>
        /// The home changes while the pet is ON SCREEN: it is drawn exactly where it was, and the
        /// returned vector is what act.Travel has to walk down to zero to bring it to its new rest
        /// (at presence <paramref name="showing"/>).
        /// </summary>
        private Vector2 SwitchHomeVisible(TamagotchiHome to, float showing, out float tiltWas)
        {
            Vector2 drawnAt = rig.transform.position;
            tiltWas = tiltNow2;
            // the clip must take in both floors while it travels: the lower of the two
            Rect clip = to.Clip;
            Rect old = Home.Clip;
            Rect both = Rect.MinMaxRect(Mathf.Min(clip.xMin, old.xMin), Mathf.Min(clip.yMin, old.yMin),
                Mathf.Max(clip.xMax, old.xMax), Mathf.Max(clip.yMax, old.yMax));
            Home = to;
            travelClip = both;
            presence = showing;
            act.Presence = showing;
            Vector2 rest = to.Base + HideDirection * ((1f - showing) * to.HideDepth);
            offsetNow = drawnAt - to.Base;
            rig.UnitScale = Home.UnitScale;
            rig.Home = Home.Base;
            return drawnAt - rest;
        }

        private Rect? travelClip;

        /// <summary>The clip the rig uses this frame: both homes' while a move is on screen.</summary>
        private Rect ClipNow
        {
            get { return MovingTo != null && travelClip.HasValue ? travelClip.Value : Home.Clip; }
        }

        // ================================================================== A: hide, peek elsewhere

        private IEnumerator MoveHideAndPeek(TamagotchiHome to)
        {
            Beat("hide");
            float rest = RestPresence;
            float eyes = Mathf.Min(rest, PresenceShowing(0.56f));
            Sound(PetSound.Hide);
            act.LookAt = Anchors.HandFocus ?? Anchors.HandCentre;
            // slowly back: first the body is gone...
            yield return Tween(Tuning.HideDuration * 0.55f, t => act.Presence = Mathf.Lerp(rest, eyes, Smooth(t)));
            // ... only the eyes, watching ...
            yield return Wait(0.2f);
            // ... and then the eyes too
            yield return Tween(Tuning.HideDuration * 0.45f, t => act.Presence = Mathf.Lerp(eyes, 0f, EaseIn(t)));
            act.Presence = 0f;
            presence = 0f;
            if (StopAfter("hide"))
            {
                yield return LabEnd(null);
                yield break;
            }
            yield return Wait(Roaming.Between(0.2f, 0.4f));

            Beat("peek");
            SwitchHomeHidden(to);
            act.Presence = 0f;
            yield return null;
            // two antennae ...
            Sound(PetSound.Peek);
            act.EarWobble = 9f;
            act.PresenceHalfLife = 0.03f;
            act.Presence = PresenceShowing(0.16f);
            yield return Wait(Tuning.PeekDuration * 0.4f);
            // ... the eyes, stealing a look at the player's cards ...
            act.EarWobble = 3f;
            act.Presence = PresenceShowing(0.58f);
            act.LookAt = Anchors.HandFocus ?? Anchors.HandCentre;
            yield return Wait(Tuning.PeekDuration * 0.6f + 0.18f);
            // ... the small head ...
            act.Presence = Mathf.Min(RestPresence, PresenceShowing(0.76f));
            yield return Wait(0.16f);
            // ... and it settles in
            float from = act.Presence ?? 0f;
            float restTo = RestPresence;
            act.EarWobble = null;
            yield return Tween(0.2f, t => act.Presence = Mathf.Lerp(from, restTo, EaseOut(t)));
            act.PresenceHalfLife = 0f;
        }

        // ================================================================== B: scamper

        private IEnumerator MoveScamper(TamagotchiHome to)
        {
            Beat("scamper");
            float tiltWas;
            Vector2 travel = SwitchHomeVisible(to, 1f, out tiltWas);
            float distance = travel.magnitude;
            int steps = Mathf.Clamp(Mathf.RoundToInt(distance / 0.75f), 2, 4);
            float seconds = Mathf.Clamp(distance / Mathf.Max(0.5f, Tuning.ScamperSpeed), 0.26f, 0.7f);
            float dir = travel.x > 0f ? -1f : 1f;   // the way it runs (travel points back to where it was)
            Sound(PetSound.Move);
            act.LookAt = (Vector3)(to.Base + new Vector2(dir * 2f, 0.6f * S));
            yield return Tween(seconds, t =>
            {
                float k = Smooth(t);
                float step = Mathf.Repeat(t * steps, 1f);
                float hop = Bell(step);
                act.Travel = travel * (1f - k) + new Vector2(0f, Px(5f) * hop);
                act.Tilt = (tiltWas - Home.Tilt) * (1f - k);
                // body low and leaning the way it goes; a little squash on every step
                act.Rot = -dir * 5f * Bell(t);
                act.Squash = new Vector2(1f + 0.03f * (1f - hop), 1f - 0.04f * (1f - hop));
                act.PawL = Paw(0f, 0.02f * hop, -18f + 26f * hop);
                act.PawR = Paw(0f, 0.02f * (1f - hop), -18f + 26f * (1f - hop));
                act.EarWobble = -dir * 7f;
            });
            act.Travel = Vector2.zero;
            act.EarWobble = null;
            yield return Tween(0.12f, t =>
            {
                float k = 1f - Back(t, 2f);
                act.Squash = new Vector2(1f + 0.04f * k, 1f - 0.045f * k);
                act.Rot = 0f;
            });
        }

        // ================================================================== C: hop

        private IEnumerator MoveHop(TamagotchiHome to)
        {
            Beat("hop");
            // a breath of anticipation where it is
            yield return Tween(0.09f, t => act.Squash = new Vector2(1f + 0.05f * t, 1f - 0.07f * t));
            float tiltWas;
            Vector2 travel = SwitchHomeVisible(to, 1f, out tiltWas);
            float height = Mathf.Max(0.45f * S, travel.magnitude * 0.22f);
            Sound(PetSound.Move);
            act.LookAt = (Vector3)(to.Base + new Vector2(0f, 0.5f * S));
            yield return Tween(Tuning.HopDuration, t =>
            {
                // the way across is even; the height is an arc
                act.Travel = travel * (1f - t) + new Vector2(0f, height * Bell(t));
                act.Tilt = (tiltWas - Home.Tilt) * (1f - Smooth(t));
                act.Lift = Bell(t);
                act.Squash = new Vector2(1f - 0.05f * Bell(t), 1f + 0.07f * Bell(t));
                act.Rot = (travel.x > 0f ? 1f : -1f) * 7f * Mathf.Sin(t * Mathf.PI * 2f) * 0.5f;
                act.EarWobble = -8f * Mathf.Cos(t * Mathf.PI);
            });
            act.Travel = Vector2.zero;
            act.Lift = 0f;
            act.Rot = 0f;
            act.EarWobble = null;
            Sound(PetSound.Stomp);
            // the landing: a squash, and a settle
            yield return Tween(0.18f, t =>
            {
                float k = 1f - Back(t, 2.2f);
                act.Squash = new Vector2(1f + 0.08f * k, 1f - 0.09f * k);
            });
        }

        // ================================================================== D: sneak

        private IEnumerator MoveSneak(TamagotchiHome to)
        {
            Beat("sneak");
            float rest = RestPresence;
            float eyes = Mathf.Min(rest, PresenceShowing(0.6f));
            Sound(PetSound.Hide);
            act.LookAt = Anchors.HandFocus ?? Anchors.HandCentre;
            yield return Tween(0.22f, t => act.Presence = Mathf.Lerp(rest, eyes, Smooth(t)));
            // the same much of it showing at the other place
            TamagotchiHome was = Home;
            Home = to;
            float eyesThere = Mathf.Min(RestPresence, PresenceShowing(0.6f));
            Home = was;
            float tiltWas;
            Vector2 travel = SwitchHomeVisible(to, eyesThere, out tiltWas);
            float seconds = Mathf.Clamp(travel.magnitude / Mathf.Max(0.5f, Tuning.ScamperSpeed * 0.45f), 0.45f, 1.1f);
            yield return Tween(seconds, t =>
            {
                float k = Smooth(t);
                // a slide, with the small bob of something creeping
                act.Travel = travel * (1f - k) + new Vector2(0f, Px(2f) * Mathf.Sin(t * Mathf.PI * 5f) * (1f - t));
                act.Tilt = (tiltWas - Home.Tilt) * (1f - k);
                act.Presence = eyesThere;
                act.LookAt = Anchors.HandFocus ?? Anchors.HandCentre;
                act.EarWobble = 4f * Mathf.Sin(t * 22f);
            });
            act.Travel = Vector2.zero;
            act.EarWobble = null;
            yield return Wait(0.14f);
            float restTo = RestPresence;
            Sound(PetSound.Peek);
            yield return Tween(0.2f, t => act.Presence = Mathf.Lerp(eyesThere, restTo, EaseOut(t)));
        }

        // ================================================================== furious

        /// <summary>The furious move: gone at once, eyes elsewhere, out faster than it left, a head
        /// snap at <paramref name="lookAt"/> and a low growl.</summary>
        private IEnumerator MoveFurious(TamagotchiHome to, Vector3 lookAt)
        {
            Beat("furious move");
            float rest = RestPresence;
            Sound(PetSound.MoveFurious);
            act.PresenceHalfLife = 0.02f;
            yield return Tween(0.11f, t => act.Presence = Mathf.Lerp(rest, 0f, EaseIn(t)));
            act.Presence = 0f;
            presence = 0f;
            yield return Wait(0.17f);
            SwitchHomeHidden(to);
            act.Presence = 0f;
            yield return null;
            // the eyes first
            act.LookAt = lookAt;
            act.Presence = PresenceShowing(0.58f);
            yield return Wait(0.15f);
            // ... and out, faster than it went
            float from = act.Presence ?? 0f;
            yield return Tween(0.1f, t => act.Presence = Mathf.Lerp(from, 1f, EaseOut(t)));
            act.PresenceHalfLife = 0f;
            // the head snaps to what it wants
            Sound(PetSound.GrowlIdle);
            yield return Tween(0.07f, t =>
            {
                HeadToward(lookAt, 1.4f * EaseOut(t));
                act.Squash = new Vector2(1f - 0.02f * Bell(t), 1f + 0.02f * Bell(t));
            });
            act.Squash = Vector2.one;
            yield return Wait(0.12f);
        }

        /// <summary>
        /// The furious pet goes where what it wants is: nearer the cells it will bite, toward the
        /// bar it will take from, beside the pile. Only when the controller has a place that is
        /// really nearer, and always seen to leave and arrive.
        /// </summary>
        private IEnumerator StalkToward(Vector2 target)
        {
            if (LabNoRoam || HomeToward == null)
            {
                yield break;
            }
            TamagotchiHome to = HomeToward(target);
            if (to == null || to.Name == Home.Name)
            {
                yield break;
            }
            MovingTo = to;
            LastMoveStyle = PetMoveStyle.FuriousSnap;
            DismissSpeech();
            yield return MoveFurious(to, target);
            FinishMove(to);
        }
    }
}
