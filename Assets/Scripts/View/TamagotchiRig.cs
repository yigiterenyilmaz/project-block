// PURPOSE: "Tamagotchi"'s BODY - one small hierarchy of sprites built from TamagotchiArt's layers,
// and the one place a pose becomes transforms. It decides nothing: TamagotchiView works out a
// PetPose every frame and Apply() puts it on the sprites. That split is the brief's "the animator
// should remain if the art is replaced": the view animates numbers, this maps them to layers.
//
// THE PIVOTS ARE THE ACTING. The whole character squashes, sways and hops about its BASE (never its
// centre - a jelly wobbling about its middle is a blob, a creature shifting its weight over its feet
// is a pet); the face is its own group that turns and leans a little ahead of the body; the paws
// turn at the shoulder and the ears at their roots; the long tongue starts at the mouth and the
// stretchy arm at the shoulder. The whole rig also TILTS (PetPose.RootTilt) - that is how it leans in
// round the side of the screen or hangs over the top of it.
//
// TWO SKINS, ONE CREATURE (PetPose.Fury, 0..1). The cute skin and the furious one are separate
// layers of the same rig, and Fury morphs between them: the furious body (hunched, wide at the
// shoulders) comes up OVER the cute one and the cute one only goes when it is fully covered, so the
// morph never shows the screen through a half-faded body; the round eyes give way to slits turned
// down toward the nose with small hard pupils that still follow the look; wedge brows replace the
// soft ones, a dark tension smear replaces the blush, the head sinks forward, the paws grow claws.
// It is never a tint: TamagotchiView.Fury is the only thing that changes, and everything the
// creature does afterwards is drawn with the other skin.
//
// ONE SORTING GROUP for the whole pet. Inside it: shadow 0, feet 1, ears 2, body 3, belly 4, cheeks
// 5, eye glow 6, iris 7, catchlight 8, lids 9, furious eyes 10, their pupils 11, brows 12, mouth 13,
// tongue 14, FOOD 16-20 (whatever it is eating sits in front of the face and behind the paws that
// hold it), paws 21, long tongue / arm 22, sparkles 23, particles 24+.
//
// AN EDGE TO HIDE BEHIND. Two rectangular SpriteMasks (identical, one for the layers under the
// food band and one for those over it) cut the pet off at its nest's edge, so sinking behind the
// edge is just moving - whether that edge is the bottom of the screen or the top of the hand.
// The food band is left out of them on purpose: what it eats has a bite line of its own.
// EXTENSION POINT: a new body part is a renderer here, a field in PetPose and a line in Apply.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectBlock.View
{
    /// <summary>One paw's pose, relative to its rest.</summary>
    public struct PetPaw
    {
        public Vector2 Offset;
        public float Angle;
        public float Scale;
    }

    /// <summary>Everything the rig needs to draw one frame of the character.</summary>
    public sealed class PetPose
    {
        public Vector2 Offset;              // world, from the home
        public float RootTilt;              // degrees: the whole rig, about its base
        public float Rot;                   // degrees about the base (the sway)
        public Vector2 Squash = Vector2.one;
        public float Scale = 1f;
        public Vector2 Head;                // units
        public float HeadTilt;
        public float HeadSquash = 1f;       // the gulp wave passing through the face
        public Vector2 Look;                // -1..1 per axis
        public float Blink;                 // extra lid closure 0..1 (max-combined with the face)
        public PetFace Face = TamagotchiExpression.For(PetEmotion.Neutral);
        public float MouthOpen = 1f;
        public Vector2 MouthOffset;         // units (a jaw twitch)
        public float PuffL;
        public float PuffR;
        public Color? PuffTint;             // what is in the cheeks (board material after a bite)
        public float Belly;                 // added to the belly's scale
        public PetPaw PawL = new PetPaw { Scale = 1f };
        public PetPaw PawR = new PetPaw { Scale = 1f };
        public float Lick;                  // 0..1 the small tongue out
        public float EarWobble;             // degrees of looseness the ears are allowed
        public float GlowExtra;
        public float SparkleL;
        public float SparkleR;
        public float Alpha = 1f;
        public float ShadowA = 1f;
        public Color Tint = Color.white;

        /// <summary>0 = the cute skin, 1 = the furious one.</summary>
        public float Fury;
        public float SquintL;               // 0..1 one eye narrowing (furious)
        public float SquintR;
        public float Sit;                   // 0..1 the feet pushed out in front

        // THE REACH: a long tongue from the mouth, or a stretchy arm from a shoulder, to a point
        // in the world. Reach01 is how far along the way it has got.
        public bool Reach;
        public bool ReachTongue;
        public bool ReachRightPaw = true;
        public Vector3 ReachTarget;
        public float Reach01;
    }

    public sealed class TamagotchiRig : MonoBehaviour
    {
        public const int FoodOrder = 16;
        public const int FxOrder = 24;

        // the furious rig's own numbers (the baker's FURY table)
        private const float FuryHeadDrop = 0.035f;
        private const float FuryEyeTilt = 9f;
        private const float FuryEyeDrop = 0.012f;
        private const float FuryBrowDrop = 0.108f;
        private const float FuryBrowAngle = 27f;
        private const float FuryMouthDrop = 0.03f;
        private static readonly Color TensionColour = new Color(0.376f, 0.078f, 0.227f);

        private Transform motion;   // the body's base: hop, sway, squash
        private Transform head;     // the face group
        private SpriteRenderer shadow;
        private SpriteRenderer body, bodyF;
        private SpriteRenderer belly;
        private SpriteRenderer footL, footR, earL, earR, cheekL, cheekR;
        private SpriteRenderer footLF, footRF, earLF, earRF, tensionL, tensionR;
        private SpriteRenderer pawL, pawR, pawLF, pawRF;
        private SpriteRenderer browL, browR, browLF, browRF, mouth, tongueTip;
        private readonly SpriteRenderer[] iris = new SpriteRenderer[2];
        private readonly SpriteRenderer[] catchlight = new SpriteRenderer[2];
        private readonly SpriteRenderer[] glow = new SpriteRenderer[2];
        private readonly SpriteRenderer[] lid = new SpriteRenderer[2];
        private readonly SpriteRenderer[] lowLid = new SpriteRenderer[2];
        private readonly SpriteRenderer[] angry = new SpriteRenderer[2];
        private readonly SpriteRenderer[] happy = new SpriteRenderer[2];
        private readonly SpriteRenderer[] sparkle = new SpriteRenderer[2];
        private readonly SpriteRenderer[] furyEye = new SpriteRenderer[2];
        private readonly SpriteRenderer[] furyPupil = new SpriteRenderer[2];
        private SpriteRenderer reachStrip;
        private SpriteRenderer reachEnd;
        private SpriteMask clipLow;
        private SpriteMask clipHigh;
        private SortingGroup group;

        private readonly List<SpriteRenderer> all = new List<SpriteRenderer>();
        private readonly Dictionary<SpriteRenderer, Color> baseColours = new Dictionary<SpriteRenderer, Color>();
        private readonly Dictionary<SpriteRenderer, float> layerAlpha = new Dictionary<SpriteRenderer, float>();

        private string shownMouth;
        private float mouthPop;

        /// <summary>The pet's food band: whatever it holds is parented here.</summary>
        public Transform FoodRoot { get; private set; }

        /// <summary>World units per body unit (the character's size).</summary>
        public float UnitScale = 1f;

        /// <summary>The world point the character stands on when its offset is zero.</summary>
        public Vector2 Home;

        public static TamagotchiRig Create(Transform parent, int sortingOrder)
        {
            var go = new GameObject("Tamagotchi");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<TamagotchiRig>();
            rig.Build(sortingOrder);
            return rig;
        }

        private void Build(int sortingOrder)
        {
            group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = sortingOrder;

            var shadowRoot = new GameObject("ShadowRoot").transform;
            shadowRoot.SetParent(transform, false);
            shadow = Layer(shadowRoot, "shadow", Vector2.zero, 0);

            motion = new GameObject("Motion").transform;
            motion.SetParent(transform, false);
            footL = Layer(motion, "foot", TamagotchiArt.FootL, 1);
            footR = Layer(motion, "foot", TamagotchiArt.FootR, 1);
            footLF = Layer(motion, "foot_furious", TamagotchiArt.FootL, 1);
            footRF = Layer(motion, "foot_furious", TamagotchiArt.FootR, 1);
            body = Layer(motion, "body", Vector2.zero, 3);
            bodyF = Layer(motion, "body_furious", Vector2.zero, 3);
            belly = Layer(motion, "belly", TamagotchiArt.Belly, 4);

            head = new GameObject("Head").transform;
            head.SetParent(motion, false);
            head.localPosition = TamagotchiArt.FaceCentre;
            Vector2 fc = TamagotchiArt.FaceCentre;
            earL = Layer(head, "ear", TamagotchiArt.EarL - fc, 2);
            earR = Layer(head, "ear", TamagotchiArt.EarR - fc, 2);
            earR.flipX = true;
            earLF = Layer(head, "ear_furious", TamagotchiArt.EarL - fc, 2);
            earRF = Layer(head, "ear_furious", TamagotchiArt.EarR - fc, 2);
            earRF.flipX = true;
            cheekL = Layer(head, "cheek", TamagotchiArt.CheekL - fc, 5);
            cheekR = Layer(head, "cheek", TamagotchiArt.CheekR - fc, 5);
            tensionL = Layer(head, "cheek_tension", TamagotchiArt.CheekL - fc + new Vector2(0.02f, -0.01f), 5);
            tensionR = Layer(head, "cheek_tension", TamagotchiArt.CheekR - fc + new Vector2(-0.02f, -0.01f), 5);
            tensionR.flipX = true;
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                Vector2 at = (left ? TamagotchiArt.EyeL : TamagotchiArt.EyeR) - fc;
                glow[s] = Layer(head, "eye_glow", at, 6);
                iris[s] = Layer(head, "eye_iris", at, 7);
                happy[s] = Layer(head, "eye_happy", at + new Vector2(0f, -0.01f), 7);
                catchlight[s] = Layer(head, "eye_catch", at + TamagotchiArt.CatchOffset, 8);
                lid[s] = Layer(head, null, at, 9);
                lowLid[s] = Layer(head, "eye_lowlid_" + (left ? "L" : "R"), at, 9);
                angry[s] = Layer(head, "eye_angry_" + (left ? "L" : "R"), at, 9);
                furyEye[s] = Layer(head, "eye_fury_" + (left ? "L" : "R"), at + new Vector2(0f, -FuryEyeDrop), 10);
                furyPupil[s] = Layer(head, "eye_fury_pupil", at, 11);
                sparkle[s] = Layer(head, "fx_sparkle", at + new Vector2(0.05f, 0.07f), 23);
            }
            browL = Layer(head, "brow", TamagotchiArt.BrowL - fc, 12);
            browR = Layer(head, "brow", TamagotchiArt.BrowR - fc, 12);
            browR.flipX = true;
            browLF = Layer(head, "brow_furious", TamagotchiArt.BrowL - fc + new Vector2(0.01f, -FuryBrowDrop), 12);
            browRF = Layer(head, "brow_furious", TamagotchiArt.BrowR - fc + new Vector2(-0.01f, -FuryBrowDrop), 12);
            browRF.flipX = true;
            mouth = Layer(head, "mouth_closed", TamagotchiArt.Mouth - fc, 13);
            tongueTip = Layer(head, "tongue_tip", TamagotchiArt.Mouth - fc + new Vector2(0f, -0.02f), 14);

            FoodRoot = new GameObject("Food").transform;
            FoodRoot.SetParent(transform, false);

            pawL = Layer(motion, "paw", TamagotchiArt.PawL, FoodOrder + 5);
            pawL.flipX = true;
            pawR = Layer(motion, "paw", TamagotchiArt.PawR, FoodOrder + 5);
            pawLF = Layer(motion, "paw_furious", TamagotchiArt.PawL, FoodOrder + 5);
            pawLF.flipX = true;
            pawRF = Layer(motion, "paw_furious", TamagotchiArt.PawR, FoodOrder + 5);
            reachStrip = Layer(transform, "tongue_strip", Vector2.zero, FoodOrder + 6);
            reachEnd = Layer(transform, "tongue_end", Vector2.zero, FoodOrder + 6);

            // the nest's edge: the pet is drawn only inside these. A mask's range is back-exclusive:
            // the food band is FoodOrder..FoodOrder+4, the paws start right after it.
            clipLow = MakeClip("ClipLow", -1, FoodOrder - 1);
            clipHigh = MakeClip("ClipHigh", FoodOrder + 4, FxOrder + 40);
            foreach (SpriteRenderer r in all)
            {
                r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        private SpriteMask MakeClip(string name, int back, int front)
        {
            // A child of the pet, so it sorts inside the pet's group and clips only the pet.
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = ViewUtil.WhiteSprite;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = back;
            mask.frontSortingOrder = front;
            return mask;
        }

        private Rect clipRect = new Rect(-1000f, -1000f, 2000f, 2000f);

        /// <summary>Where the pet may be seen: everything inside this world rect - the screen less
        /// what lies beyond the nest's edge. Re-placed every frame in Apply, because the masks ride
        /// the pet's own (scaling, tilting) transform.</summary>
        public void SetClip(Rect world)
        {
            clipRect = world;
            PlaceClip();
        }

        private void PlaceClip()
        {
            Vector2 c = clipRect.center;
            float s = Mathf.Max(0.0001f, transform.lossyScale.x);
            foreach (SpriteMask mask in new[] { clipLow, clipHigh })
            {
                if (mask == null)
                {
                    continue;
                }
                mask.transform.position = new Vector3(c.x, c.y, 0f);
                mask.transform.rotation = Quaternion.identity;
                mask.transform.localScale = new Vector3(clipRect.width / s, clipRect.height / s, 1f);
            }
        }

        private SpriteRenderer Layer(Transform parent, string sprite, Vector2 at, int order)
        {
            var go = new GameObject(sprite ?? "layer");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, at.y, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                r.sprite = TamagotchiArt.Get(sprite);
            }
            r.sortingOrder = order;
            all.Add(r);
            baseColours[r] = Color.white;
            return r;
        }

        private void SetBase(SpriteRenderer r, Color c)
        {
            baseColours[r] = c;
        }

        private void SetAlpha(SpriteRenderer r, float a)
        {
            layerAlpha[r] = Mathf.Clamp01(a);
        }

        private float LayerAlpha(SpriteRenderer r)
        {
            float a;
            return layerAlpha.TryGetValue(r, out a) ? a : 1f;
        }

        /// <summary>The pet's sorting order among everything else on screen.</summary>
        public int Order
        {
            get { return group.sortingOrder; }
            set { group.sortingOrder = value; }
        }

        /// <summary>The world position of the middle of the mouth, as drawn this frame.</summary>
        public Vector3 MouthWorld
        {
            get { return mouth != null ? mouth.transform.position : transform.position; }
        }

        /// <summary>The world position of an eye, as drawn this frame.</summary>
        public Vector3 EyeWorld(bool left)
        {
            return iris[left ? 0 : 1] != null ? iris[left ? 0 : 1].transform.position : transform.position;
        }

        /// <summary>A shoulder in world space (where a stretchy arm starts).</summary>
        public Vector3 ShoulderWorld(bool right)
        {
            return motion.TransformPoint(right ? TamagotchiArt.PawR : TamagotchiArt.PawL);
        }

        /// <summary>The body's centre in world space.</summary>
        public Vector3 BellyWorld
        {
            get { return belly.transform.position; }
        }

        /// <summary>The top of the head in world space (where a speech bubble's tail points).</summary>
        public Vector3 HeadTopWorld
        {
            get { return motion.TransformPoint(new Vector3(0f, TamagotchiArt.HeadTop, 0f)); }
        }

        /// <summary>The direction the pet's own "up" points in the world this frame.</summary>
        public Vector2 UpWorld
        {
            get { return motion.up; }
        }

        // ------------------------------------------------------------------ applying a pose

        public void Apply(PetPose p)
        {
            float s = UnitScale * p.Scale;
            transform.position = new Vector3(Home.x + p.Offset.x, Home.y + p.Offset.y, 0f);
            transform.localScale = new Vector3(s, s, 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, p.RootTilt);

            float fury = Mathf.Clamp01(p.Fury);
            // the cute layers stay whole until the furious ones cover them, then go quickly
            float cute = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, fury));
            float faceFury = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, fury));
            float faceCute = 1f - faceFury;

            motion.localRotation = Quaternion.Euler(0f, 0f, p.Rot);
            motion.localScale = new Vector3(p.Squash.x, p.Squash.y, 1f);
            shadow.transform.localScale = new Vector3(Mathf.Lerp(1f, p.Squash.x, 0.6f) * (1f + 0.07f * fury), 1f, 1f);
            shadow.transform.rotation = Quaternion.identity;

            head.localPosition = (Vector3)(TamagotchiArt.FaceCentre + p.Head + new Vector2(0f, -FuryHeadDrop * fury));
            head.localRotation = Quaternion.Euler(0f, 0f, p.HeadTilt);
            head.localScale = new Vector3(1f / Mathf.Max(0.5f, Mathf.Sqrt(p.HeadSquash)), p.HeadSquash, 1f);

            SetAlpha(body, cute);
            SetAlpha(bodyF, fury);
            SetAlpha(belly, 1f - fury);
            SetAlpha(footL, cute);
            SetAlpha(footR, cute);
            SetAlpha(footLF, fury);
            SetAlpha(footRF, fury);

            // SITTING: the feet come out in front and turn out a little
            float sit = Mathf.Clamp01(p.Sit);
            Vector2 footOut = new Vector2(0.11f * sit, 0.035f * sit);
            PlaceFoot(footL, footLF, TamagotchiArt.FootL + new Vector2(-footOut.x, footOut.y), 18f * sit, 1f + 0.12f * sit);
            PlaceFoot(footR, footRF, TamagotchiArt.FootR + footOut, -18f * sit, 1f + 0.12f * sit);

            PetFace f = p.Face;
            Vector2 fc = TamagotchiArt.FaceCentre;
            float wob = p.EarWobble;
            Vector2 earLAt = TamagotchiArt.EarL - fc + new Vector2(-0.015f * fury, f.EarLift);
            Vector2 earRAt = TamagotchiArt.EarR - fc + new Vector2(0.015f * fury, f.EarLift);
            Quaternion earLRot = Quaternion.Euler(0f, 0f, TamagotchiArt.EarSplay + f.Ear + wob);
            Quaternion earRRot = Quaternion.Euler(0f, 0f, -(TamagotchiArt.EarSplay + f.Ear) + wob * 0.8f);
            foreach (SpriteRenderer e in new[] { earL, earLF })
            {
                e.transform.localPosition = earLAt;
                e.transform.localRotation = earLRot;
            }
            foreach (SpriteRenderer e in new[] { earR, earRF })
            {
                e.transform.localPosition = earRAt;
                e.transform.localRotation = earRRot;
            }
            SetAlpha(earL, cute);
            SetAlpha(earR, cute);
            SetAlpha(earLF, fury);
            SetAlpha(earRF, fury);

            // EYES: the iris moves a third of the look, the catchlight all of it - so a glance
            // reads as the eye turning, not as a dot sliding over a disc.
            float happyA = Mathf.Clamp01(f.Happy);
            float openA = (1f - happyA) * faceCute;
            float closure = Mathf.Clamp01(Mathf.Max(f.Lid, p.Blink));
            Vector2 look = new Vector2(Mathf.Clamp(p.Look.x, -1f, 1f), Mathf.Clamp(p.Look.y, -1f, 1f))
                * TamagotchiArt.LookRange;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                Vector2 at = (left ? TamagotchiArt.EyeL : TamagotchiArt.EyeR) - fc;
                iris[side].transform.localPosition = at + look * 0.35f;
                iris[side].transform.localScale = new Vector3(f.Iris, f.Iris, 1f);
                catchlight[side].transform.localPosition = at + TamagotchiArt.CatchOffset * f.Iris + look;
                catchlight[side].transform.localScale = new Vector3(f.Catch, f.Catch, 1f);
                lid[side].sprite = TamagotchiArt.Lid(left, closure);
                lid[side].transform.localScale = new Vector3(f.Iris, f.Iris, 1f);
                lowLid[side].transform.localScale = lid[side].transform.localScale;
                angry[side].transform.localScale = lid[side].transform.localScale;
                SetAlpha(iris[side], openA);
                SetAlpha(catchlight[side], openA * (closure > 0.7f ? 0f : 1f));
                SetAlpha(lid[side], openA);
                SetAlpha(lowLid[side], openA * Mathf.Clamp01(f.LowLid));
                SetAlpha(angry[side], openA * Mathf.Clamp01(f.Angry) * (closure > 0.85f ? 0f : 1f));
                SetAlpha(happy[side], happyA * faceCute);
                SetBase(glow[side], TamagotchiArt.GlowPink);
                SetAlpha(glow[side], Mathf.Clamp01(f.Glow + p.GlowExtra) * 0.85f * faceCute);
                float spark = left ? p.SparkleL : p.SparkleR;
                sparkle[side].transform.localScale = new Vector3(0.6f + 0.5f * spark, 0.6f + 0.5f * spark, 1f);
                sparkle[side].transform.localRotation = Quaternion.Euler(0f, 0f, spark * 40f);
                SetAlpha(sparkle[side], Mathf.Clamp01(spark) * faceCute);

                // THE FURIOUS EYE: a slit turned down toward the nose; it narrows instead of blinking,
                // and its small pupil is what follows the look
                float squint = left ? p.SquintL : p.SquintR;
                float open = (1f - 0.82f * closure) * (1f - 0.5f * Mathf.Clamp01(squint));
                furyEye[side].transform.localPosition = at + new Vector2(0f, -FuryEyeDrop);
                furyEye[side].transform.localRotation = Quaternion.Euler(0f, 0f, left ? -FuryEyeTilt : FuryEyeTilt);
                furyEye[side].transform.localScale = new Vector3(1f, Mathf.Max(0.12f, open), 1f);
                furyPupil[side].transform.localPosition = at + new Vector2(left ? 0.012f : -0.012f, -FuryEyeDrop - 0.012f)
                    + look * 0.6f;
                float pupil = Mathf.Clamp(f.Iris, 0.7f, 1.15f);
                furyPupil[side].transform.localScale = new Vector3(pupil, pupil * Mathf.Clamp01(open * 1.3f), 1f);
                SetAlpha(furyEye[side], faceFury);
                SetAlpha(furyPupil[side], faceFury * (open < 0.3f ? 0f : 1f));
            }

            browL.transform.localPosition = TamagotchiArt.BrowL - fc + new Vector2(0f, f.BrowY);
            browR.transform.localPosition = TamagotchiArt.BrowR - fc + new Vector2(0f, f.BrowY);
            browL.transform.localRotation = Quaternion.Euler(0f, 0f, -f.Brow);
            browR.transform.localRotation = Quaternion.Euler(0f, 0f, f.Brow);
            SetAlpha(browL, f.BrowA * faceCute);
            SetAlpha(browR, f.BrowA * faceCute);
            // the wedge brows: they come DOWN onto the eyes as the fury arrives
            float browIn = Mathf.Lerp(0.05f, 0f, faceFury);
            browLF.transform.localPosition = TamagotchiArt.BrowL - fc + new Vector2(0.01f, -FuryBrowDrop + browIn);
            browRF.transform.localPosition = TamagotchiArt.BrowR - fc + new Vector2(-0.01f, -FuryBrowDrop + browIn);
            browLF.transform.localRotation = Quaternion.Euler(0f, 0f, -FuryBrowAngle * Mathf.Lerp(0.4f, 1f, faceFury));
            browRF.transform.localRotation = Quaternion.Euler(0f, 0f, FuryBrowAngle * Mathf.Lerp(0.4f, 1f, faceFury));
            SetAlpha(browLF, faceFury);
            SetAlpha(browRF, faceFury);

            // THE MOUTH is the one drawing that swaps; a small pop hides the swap.
            string wanted = string.IsNullOrEmpty(f.Mouth) ? "closed" : f.Mouth;
            if (wanted != shownMouth)
            {
                Sprite next = TamagotchiArt.Get("mouth_" + wanted);
                if (next != null)
                {
                    mouth.sprite = next;
                    if (shownMouth != null)
                    {
                        mouthPop = 1f;
                    }
                    shownMouth = wanted;
                }
            }
            mouthPop = Mathf.MoveTowards(mouthPop, 0f, Time.deltaTime / 0.07f);
            float ms = f.MouthScale * (1f - 0.12f * mouthPop);
            mouth.transform.localScale = new Vector3(ms * Mathf.Lerp(1f, p.MouthOpen, 0.35f), ms * p.MouthOpen, 1f);
            mouth.transform.localPosition = TamagotchiArt.Mouth - fc + p.MouthOffset + new Vector2(0f, -FuryMouthDrop * fury);
            tongueTip.transform.localPosition = mouth.transform.localPosition + new Vector3(0f, -0.02f, 0f);

            Color puffTint = p.PuffTint ?? f.Cheek;
            float puffL = Mathf.Clamp01(p.PuffL);
            float puffR = Mathf.Clamp01(p.PuffR);
            cheekL.transform.localScale = new Vector3(1f + 0.4f * puffL, 1f + 0.3f * puffL, 1f);
            cheekR.transform.localScale = new Vector3(1f + 0.4f * puffR, 1f + 0.3f * puffR, 1f);
            cheekL.transform.localPosition = TamagotchiArt.CheekL - fc + new Vector2(-0.02f * puffL, 0f);
            cheekR.transform.localPosition = TamagotchiArt.CheekR - fc + new Vector2(0.02f * puffR, 0f);
            SetBase(cheekL, Color.Lerp(f.Cheek, puffTint, puffL));
            SetBase(cheekR, Color.Lerp(f.Cheek, puffTint, puffR));
            // the blush goes with the fury - but a cheek full of something still bulges
            SetAlpha(cheekL, Mathf.Clamp01(f.CheekA * faceCute + 0.6f * puffL));
            SetAlpha(cheekR, Mathf.Clamp01(f.CheekA * faceCute + 0.6f * puffR));
            SetBase(tensionL, TensionColour);
            SetBase(tensionR, TensionColour);
            SetAlpha(tensionL, 0.62f * faceFury);
            SetAlpha(tensionR, 0.62f * faceFury);

            belly.transform.localScale = new Vector3(1f + p.Belly, 1f + p.Belly * 0.8f, 1f);
            // the furious body has no belly layer: the gulp swells the body itself a little
            bodyF.transform.localScale = new Vector3(1f + p.Belly * 0.5f * fury, 1f, 1f);

            PlacePaw(pawL, pawLF, TamagotchiArt.PawL + p.PawL.Offset, -p.PawL.Angle, p.PawL.Scale);
            PlacePaw(pawR, pawRF, TamagotchiArt.PawR + p.PawR.Offset, p.PawR.Angle, p.PawR.Scale);
            // both there every frame; a reach hides the one that has become the far end of the arm
            SetAlpha(pawL, cute);
            SetAlpha(pawR, cute);
            SetAlpha(pawLF, fury);
            SetAlpha(pawRF, fury);

            float lick = Mathf.Clamp01(p.Lick);
            tongueTip.transform.localScale = new Vector3(lick, lick, 1f);
            SetAlpha(tongueTip, lick > 0.02f ? 1f : 0f);

            ApplyReach(p, fury);

            SetAlpha(shadow, 0.55f * p.ShadowA);
            Color tint = p.Tint;
            float alpha = Mathf.Clamp01(p.Alpha);
            foreach (SpriteRenderer r in all)
            {
                // a layer's own colour, its own alpha (written above), then the global tint/alpha
                Color b = baseColours[r];
                r.color = new Color(b.r * tint.r, b.g * tint.g, b.b * tint.b, b.a * LayerAlpha(r) * alpha);
            }
            PlaceClip();
        }

        private static void PlaceFoot(SpriteRenderer a, SpriteRenderer b, Vector2 at, float angle, float scale)
        {
            foreach (SpriteRenderer r in new[] { a, b })
            {
                r.transform.localPosition = at;
                r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                r.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static void PlacePaw(SpriteRenderer a, SpriteRenderer b, Vector2 at, float angle, float scale)
        {
            float s = scale <= 0f ? 1f : scale;
            foreach (SpriteRenderer r in new[] { a, b })
            {
                r.transform.localPosition = at;
                r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                r.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        private void ApplyReach(PetPose p, float fury)
        {
            bool on = p.Reach && p.Reach01 > 0.001f;
            reachStrip.enabled = on;
            reachEnd.enabled = on;
            if (!on)
            {
                return;
            }
            bool furious = fury > 0.5f;
            reachStrip.sprite = TamagotchiArt.Get(p.ReachTongue ? "tongue_strip" : "arm_strip");
            reachEnd.sprite = TamagotchiArt.Get(p.ReachTongue ? "tongue_end" : furious ? "paw_furious" : "paw");
            // the stretched arm wears the skin it belongs to
            SetBase(reachStrip, !p.ReachTongue && furious ? new Color(0.74f, 0.42f, 0.55f) : Color.white);
            Vector3 from = p.ReachTongue ? MouthWorld : ShoulderWorld(p.ReachRightPaw);
            Vector3 to = Vector3.Lerp(from, p.ReachTarget, Mathf.Clamp01(p.Reach01));
            Vector3 d = to - from;
            float length = d.magnitude;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float s = Mathf.Max(0.0001f, transform.lossyScale.x);
            reachStrip.transform.position = from;
            reachStrip.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            // the strip is one unit long at rest: stretch it to the length, and thin it a touch
            // as it stretches so it reads as elastic rather than as a pole
            float thin = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(length / (3f * s)));
            reachStrip.transform.localScale = new Vector3(length / s, thin, 1f);
            reachEnd.transform.position = to;
            reachEnd.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            reachEnd.transform.localScale = new Vector3(1f, 1f, 1f);
            SetAlpha(reachStrip, 1f);
            SetAlpha(reachEnd, 1f);
            // the paw that is reaching is the far end of the arm, so the one on the body hides
            if (!p.ReachTongue)
            {
                SetAlpha(p.ReachRightPaw ? pawR : pawL, 0f);
                SetAlpha(p.ReachRightPaw ? pawRF : pawLF, 0f);
            }
        }

        /// <summary>The renderers of the pet, for a debug outline of its bounds.</summary>
        public Bounds WorldBounds
        {
            get
            {
                var b = new Bounds(body.bounds.center, body.bounds.size);
                b.Encapsulate(earL.bounds);
                b.Encapsulate(earR.bounds);
                b.Encapsulate(pawL.bounds);
                b.Encapsulate(pawR.bounds);
                return b;
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
