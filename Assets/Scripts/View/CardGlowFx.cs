// PURPOSE: THE ONE WAY A BAR CARD LIGHTS UP. A soft halo behind a joker or power card that
// three different things drive, and the reason this is a component rather than three
// animations is that all three can be true at once: a joker can be asking to be used, firing,
// and being held down for a sale in the same second.
//
// THE THREE TERMS, and they are told apart on purpose - a player who cannot tell "you may use
// this" from "this just went off" learns nothing from either:
//
//   ATTENTION  a slow BREATH, on for as long as the card has something waiting to be done with
//              it ("Hileli zar" with its market pick unspent, an unbound "Parazit"). It is the
//              quietest of the three, because it is the one that is on the longest: a card that
//              shouts while it merely waits is a card the player learns to ignore.
//   PROC       a sharp FLASH that decays, fired the moment a joker actually did something. It
//              rises in two frames and falls over half a second - the asymmetry is what reads
//              as an event rather than a pulse.
//   HOLD       a steady RISE under a finger held on the card, so a press that is about to sell
//              something says so before it happens rather than after.
//
// ONE WRITER, like the arena's transform: each term only records its own claim and Compose
// takes the STRONGEST. Three effects writing the Image's colour in turn would leave whichever
// ran last in charge, which is the bug the score line already had (see ScoreWarm in CLAUDE.md).
//
// It is a GRADIENT that dies at its own edge (ViewUtil.GlowSprite), never a flat tinted plate -
// a hard-edged rectangle behind a card reads as a second card sticking out from under it.
//
// EXTENSION POINT: a new reason for a card to light up is a new term here plus one line in
// Compose - never a fourth thing writing Image.color.

using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>Drives one bar card's halo. Lives on the halo itself, so it survives every
    /// bar refresh and no bar has to remember what it was doing.</summary>
    public sealed class CardGlowFx : MonoBehaviour
    {
        /// <summary>
        /// THE PALETTE OF THE THREE TERMS, in one place because they only mean anything against
        /// each other. Gold INVITES, white-cyan REPORTS, red WARNS - and the player learns three
        /// colours rather than one per joker.
        ///
        /// Every one of them is a light, so they are bright and unsaturated at the core: a deep
        /// saturated halo behind a card reads as a coloured plate, which is the thing this whole
        /// file exists not to be.
        /// </summary>
        public static readonly Color AttentionColour = new Color(1f, 0.84f, 0.42f);

        /// <summary>A joker or power just fired. Cooler than the invitation on purpose - the two
        /// are never the same event and must never be the same colour.</summary>
        public static readonly Color ProcColour = new Color(0.72f, 0.95f, 1f);

        /// <summary>A finger is held on this card and a sale is coming.</summary>
        public static readonly Color HoldColour = new Color(1f, 0.42f, 0.34f);

        /// <summary>Seconds for one full breath. Slow on purpose - at a second or so it reads
        /// as a blink, which is an alert, and this is an invitation.</summary>
        private const float BreathPeriod = 2.1f;

        /// <summary>How far the breath swings, and the floor it never drops below. A breath that
        /// reaches zero is a flash repeating; keeping a little light on says the card is in this
        /// state continuously.</summary>
        private const float BreathLow = 0.30f;

        private const float BreathHigh = 0.85f;

        /// <summary>The proc's rise and fall. Short up, long down.</summary>
        private const float ProcRise = 0.05f;

        private const float ProcFall = 0.52f;

        /// <summary>How far past the card the halo reaches at full strength, as a fraction of
        /// the card's width. It also SCALES with the claim, so a stronger light is a bigger one -
        /// brightness alone on a small card is barely a change.</summary>
        private const float Spread = 0.10f;

        private Image image;

        private bool attentionOn;
        private Color attentionColour = Color.white;

        private float procTime = -1f;
        private Color procColour = Color.white;

        private float hold;
        private Color holdColour = Color.white;

        private RectTransform rect;

        /// <summary>The rect the halo tracks, in the halo's own parent space. Written by the bar
        /// whenever it lays its slots out - the halo lives in a layer of its own (so every glow
        /// draws under every card) and therefore cannot simply be a child of what it lights.</summary>
        private Vector2 followPosition;
        private Vector2 followSize;

        private void Awake()
        {
            image = GetComponent<Image>();
            rect = GetComponent<RectTransform>();
        }

        /// <summary>Points the halo at a card's slot. Position and size are the CARD's, in the
        /// glow layer's space; the spread is added here so a caller never has to know it.</summary>
        public void Follow(Vector2 position, Vector2 size)
        {
            followPosition = position;
            followSize = size;
            Apply(Compose());
        }

        /// <summary>The BREATH: true while this card has something waiting to be done with it.
        /// Idempotent, so a bar may re-assert it every refresh without restarting the cycle.</summary>
        public void SetAttention(bool on, Color colour)
        {
            attentionOn = on;
            attentionColour = colour;
        }

        /// <summary>The FLASH: this joker or power just fired. Restarts on every call, because
        /// two procs in quick succession are two events and should read as two.</summary>
        public void Proc(Color colour)
        {
            procColour = colour;
            procTime = 0f;
        }

        /// <summary>The RISE: how far through a press-and-hold this card is, 0 to 1. Zero takes
        /// the term away entirely.</summary>
        public void SetHold(float progress, Color colour)
        {
            hold = Mathf.Clamp01(progress);
            holdColour = colour;
        }

        /// <summary>True while anything is lighting this card. The bars ask so they can leave a
        /// finished halo switched off rather than drawing a transparent quad every frame.</summary>
        public bool Lit
        {
            get { return attentionOn || hold > 0f || procTime >= 0f; }
        }

        private void Update()
        {
            if (procTime >= 0f)
            {
                procTime += Time.unscaledDeltaTime;
                if (procTime > ProcRise + ProcFall)
                {
                    procTime = -1f;
                }
            }
            Apply(Compose());
        }

        /// <summary>The strongest claim on the halo this frame, as a colour with its strength in
        /// the alpha. Strongest rather than summed: three lights added together saturate, and a
        /// proc during a breath has to still read as a proc.</summary>
        private Color Compose()
        {
            Color best = new Color(0f, 0f, 0f, 0f);
            if (attentionOn)
            {
                // Sine rather than a triangle: a breath has no corners in it.
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BreathPeriod);
                Take(ref best, attentionColour, Mathf.Lerp(BreathLow, BreathHigh, k));
            }
            if (procTime >= 0f)
            {
                float k = procTime < ProcRise
                    ? procTime / ProcRise
                    : 1f - (procTime - ProcRise) / ProcFall;
                Take(ref best, procColour, Mathf.Clamp01(k));
            }
            if (hold > 0f)
            {
                // Eased UP, so the last third of a hold is visibly the last third - a linear
                // rise says nothing about how close the sale is.
                Take(ref best, holdColour, hold * hold * 0.9f + 0.1f);
            }
            return best;
        }

        private static void Take(ref Color best, Color colour, float strength)
        {
            if (strength <= best.a)
            {
                return;
            }
            best = new Color(colour.r, colour.g, colour.b, strength);
        }

        private void Apply(Color claim)
        {
            if (image == null)
            {
                return;
            }
            bool on = claim.a > 0.002f;
            if (image.enabled != on)
            {
                image.enabled = on;
            }
            if (!on)
            {
                return;
            }
            image.color = claim;
            if (rect != null)
            {
                float out_ = followSize.x * Spread * claim.a;
                rect.anchoredPosition = followPosition;
                rect.sizeDelta = followSize + new Vector2(out_ * 2f, out_ * 2f);
            }
        }
    }
}
