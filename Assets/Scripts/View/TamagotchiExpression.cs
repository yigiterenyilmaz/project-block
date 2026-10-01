// PURPOSE: "Tamagotchi"'s FACE as a set of layers - eyes, pupils, brows, mouth, cheeks, and the
// ears that carry the mood on top of the head - and the ten emotions the brief names, each one a
// setting of those layers rather than a sprite. Faces are BLENDED (Face.Lerp), never swapped: a
// sprite swap reads as a mascot changing stickers, a blend reads as a face changing its mind. The
// one discrete layer is the mouth's drawing, and TamagotchiRig hides that swap with a small pop.
//
// What each emotion is FOR, so they never drift into one another:
//   Neutral / Happy     the calm pet; Happy is a meal or a pat - crescent eyes, bright cheeks.
//   Hungry              wanting: a small "o", brows lifted inward, ears up a little.
//   Curious / Excited   a card coming closer (Curious far, Excited in the feed zone).
//   Confused            the wrong card: mouth shut, one brow up, eyes searching.
//   Impatient / Angry   the deadline running down - narrower eyes, sharper brows, a frown.
//   Furious             the deadline missed: small pupils, shrunk catchlights, deep berry cheeks,
//                       stiff ears - the SAME creature, dangerously hungry, never a horror mask.
//   Smug                after it took something: half-lidded, a lopsided grin.
//   Sleepy              satisfied: heavy lids, a soft mouth.
// EXTENSION POINT: a new emotion is a new case in For; a new face layer is a field in Face and a
// line in Lerp and in TamagotchiRig.Apply.

using UnityEngine;

namespace ProjectBlock.View
{
    public enum PetEmotion
    {
        Neutral,
        Happy,
        Hungry,
        Curious,
        Excited,
        Confused,
        Impatient,
        Angry,
        Furious,
        Smug,
        Sleepy
    }

    /// <summary>One setting of every face layer.</summary>
    public struct PetFace
    {
        public float Lid;          // upper lid closure 0..1 (both eyes)
        public float LowLid;       // lower lid pushed up 0..1
        public float Angry;        // the angry slant 0..1
        public float Happy;        // crescent eyes 0..1
        public float Iris;         // pupil scale
        public float Catch;        // catchlight scale
        public float Brow;         // degrees, + = inner ends down (cross)
        public float BrowY;        // units
        public float BrowA;        // alpha
        public string Mouth;       // which drawing
        public float MouthScale;
        public Color Cheek;
        public float CheekA;
        public float Ear;          // degrees outward (+) / inward-up (-)
        public float EarLift;      // units the ears ride up
        public float Glow;         // eye glow 0..1

        public static PetFace Lerp(PetFace a, PetFace b, float t)
        {
            t = Mathf.Clamp01(t);
            return new PetFace
            {
                Lid = Mathf.Lerp(a.Lid, b.Lid, t),
                LowLid = Mathf.Lerp(a.LowLid, b.LowLid, t),
                Angry = Mathf.Lerp(a.Angry, b.Angry, t),
                Happy = Mathf.Lerp(a.Happy, b.Happy, t),
                Iris = Mathf.Lerp(a.Iris, b.Iris, t),
                Catch = Mathf.Lerp(a.Catch, b.Catch, t),
                Brow = Mathf.Lerp(a.Brow, b.Brow, t),
                BrowY = Mathf.Lerp(a.BrowY, b.BrowY, t),
                BrowA = Mathf.Lerp(a.BrowA, b.BrowA, t),
                // the drawing changes at the middle of the blend, where the rig's pop hides it
                Mouth = t < 0.5f ? a.Mouth : b.Mouth,
                MouthScale = Mathf.Lerp(a.MouthScale, b.MouthScale, t),
                Cheek = Color.Lerp(a.Cheek, b.Cheek, t),
                CheekA = Mathf.Lerp(a.CheekA, b.CheekA, t),
                Ear = Mathf.Lerp(a.Ear, b.Ear, t),
                EarLift = Mathf.Lerp(a.EarLift, b.EarLift, t),
                Glow = Mathf.Lerp(a.Glow, b.Glow, t)
            };
        }
    }

    public static class TamagotchiExpression
    {
        public static PetFace For(PetEmotion emotion)
        {
            var f = new PetFace
            {
                Lid = 0f, LowLid = 0f, Angry = 0f, Happy = 0f, Iris = 1f, Catch = 1f,
                Brow = 0f, BrowY = 0f, BrowA = 0.5f, Mouth = "closed", MouthScale = 1f,
                Cheek = TamagotchiArt.CheekRose, CheekA = 0.55f, Ear = 0f, EarLift = 0f, Glow = 0f
            };
            switch (emotion)
            {
                case PetEmotion.Happy:
                    f.Happy = 1f; f.CheekA = 0.78f; f.Cheek = TamagotchiArt.CheekBright;
                    f.Brow = -5f; f.BrowY = 0.012f; f.Ear = -4f;
                    break;
                case PetEmotion.Hungry:
                    f.Mouth = "nom"; f.Brow = -7f; f.BrowY = 0.01f; f.BrowA = 0.6f;
                    f.Ear = -8f; f.EarLift = 0.012f;
                    break;
                case PetEmotion.Curious:
                    f.Mouth = "small"; f.Iris = 1.06f; f.Brow = -4f; f.BrowY = 0.016f;
                    f.Ear = -10f; f.EarLift = 0.015f;
                    break;
                case PetEmotion.Excited:
                    f.Mouth = "wide"; f.Iris = 1.12f; f.Catch = 1.15f; f.Brow = -8f; f.BrowY = 0.022f;
                    f.CheekA = 0.7f; f.Ear = -16f; f.EarLift = 0.025f;
                    break;
                case PetEmotion.Confused:
                    f.Mouth = "frown"; f.MouthScale = 0.85f; f.Lid = 0.22f; f.Brow = 9f;
                    f.BrowY = 0.006f; f.BrowA = 0.7f; f.Ear = 10f;
                    break;
                case PetEmotion.Impatient:
                    f.Mouth = "frown"; f.Lid = 0.3f; f.Brow = 10f; f.BrowA = 0.8f;
                    f.Ear = 12f; f.EarLift = -0.01f;
                    break;
                case PetEmotion.Angry:
                    f.Mouth = "frown"; f.Angry = 1f; f.Iris = 0.94f; f.Catch = 0.75f; f.Brow = 18f;
                    f.BrowY = -0.02f; f.BrowA = 0.95f; f.Cheek = Color.Lerp(TamagotchiArt.CheekRose,
                        TamagotchiArt.CheekBerry, 0.5f); f.CheekA = 0.65f; f.Ear = -2f;
                    break;
                case PetEmotion.Furious:
                    f.Mouth = "furious"; f.Angry = 1f; f.Iris = 0.84f; f.Catch = 0.45f; f.Brow = 24f;
                    f.BrowY = -0.03f; f.BrowA = 1f; f.Cheek = TamagotchiArt.CheekBerry; f.CheekA = 0.8f;
                    f.Ear = -14f; f.EarLift = 0.02f;
                    break;
                case PetEmotion.Smug:
                    f.Mouth = "smug"; f.Lid = 0.36f; f.LowLid = 1f; f.Brow = -4f; f.BrowA = 0.6f;
                    f.CheekA = 0.65f; f.Ear = 4f;
                    break;
                case PetEmotion.Sleepy:
                    f.Mouth = "small"; f.MouthScale = 0.85f; f.Lid = 0.62f; f.Brow = -3f;
                    f.CheekA = 0.6f; f.Ear = 14f; f.EarLift = -0.012f;
                    break;
            }
            return f;
        }
    }
}
