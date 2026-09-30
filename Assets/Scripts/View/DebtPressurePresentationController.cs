// PURPOSE: "Kredi kartı" - DEBT PRESSURE: the loan felt by the whole screen, not only read off
// the ledger. The first pass told the debt in one panel and left everything else exactly as a
// debt-free round - normal board, normal backdrop, normal header, a negative number - so what the
// player took away was "there is a debt widget on the right", never "I am playing this round in
// debt". This is the other half: the ledger informs; this PRESSES.
//
// THREE LEVELS, CONNECTED. INFORMATION is the ledger (DebtLedgerView). PRESSURE is here: the
// screen's edges, the backdrop, the board's surroundings, the score line and the round target.
// CONSEQUENCE is the final due and the bailiff, which this builds toward so HACİZ arrives as the
// payoff of a tension that was there all round rather than as a popup.
//
// ONE NUMBER DRIVES IT (Pressure01), and it is a PRESENTATION number, never a rule: the named
// state (Core's CreditDeadline) sets a ceiling - SAFE 0.15, PRESSURE 0.42, WARNING 0.68, FINAL
// DUE 1 - and how much of the loan is still owed moves it within that (a loan paid down to a
// quarter sits at about 60% of its ceiling; interest can push it a little past), and a stage
// whose minimum is paid eases it a further tenth. That is what makes PAYING feel like breathing
// and INTEREST like the weight coming back, and it is why a small remainder in the last stage
// does not look like a full one: the term alone does not make a paid loan frightening. Named
// state + a small continuous intensity, as asked - not a formula of everything.
//
// WHAT IT MOVES, every one of them outside the grid:
//   VIGNETTE     the screen's edges and the board's surroundings in deep burgundy-charcoal. The
//                spec's opacities (SAFE <= 0.03 ... FINAL 0.11-0.15) are PERCEPTUAL: this project
//                blends in LINEAR colour, where 0.13 of a near-black lands as a 6% drop nobody
//                sees (the Tılsım lesson). So each is converted, a_lin = 1 - (1 - a)^2.2, to the
//                alpha that gives the same drop on screen. Drawn at order -8: over the backdrop
//                and any boss atmosphere, under the board plate and everything on it. The board's
//                middle is simply outside its reach: the shape is clear over the arena.
//   DESATURATION the backdrop's ground loses 0-7.5% of its saturation (BackdropView's seam). The
//                blocks keep every bit of theirs.
//   OUTER SHADOW the board's own drop shadow deepens from WARNING on - under the plate, so only
//                its falloff shows.
//   BRACKETS     four dark, matte legal corner marks OUTSIDE the board's corners, faint from
//                PRESSURE, 2 px closer at WARNING and 5 px at FINAL: the contract beginning to
//                frame the play area. They never enter it and the cells never change size.
//   SCORE        a burgundy underline under the TOTAL's number - the same colour as the tab on
//                the ledger's head, which is how the two say they are one system without a line
//                drawn between them. It retracts when the debt is gone. The carry's arrival dips
//                the TOTAL (1 -> 0.97 -> 1) through a muted cream-burgundy (ScoreClaim; the score
//                line has one writer, GameUiController.TickScoreResponse).
//   TARGET       under "raunt 0 / 1548" a thin SPLIT bar - the round's own share in the score's
//                cream, the debt's burden in deep burgundy with a brass seam - filled by the
//                round's progress; beside the line a legible "900 + [648 BORÇ]". It answers "why
//                is the bar 1548" at a glance. The burden is Core's (RoundEngine.OwnBar and
//                CreditInstallment), never a subtraction done here.
//
// RELIEF AND PAIN ARE EVENTS ON TOP OF THE STEADY STATE. A payment lets the vignette go 10-20%
// further than its new steady level for a breath and the brackets out 1-2 px; the minimum paid
// takes a little more off (-0.015 / -0.025) and the burden plate answers "secured"; interest adds
// 0.02-0.04 and pulls the brackets 1 px in. None of them shakes anything, and none of them lets
// the screen go all the way back while a debt is still owed.
//
// NOTHING BLINKS. No flash, no pulse on the screen, no red tint over a block. The loudest thing
// this layer does is let the edges of the world get heavier, slowly.

using System;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class DebtPressurePresentationController : MonoBehaviour
    {
        // =================================================================== tuning
        public static class Style
        {
            /// <summary>The named states' ceilings on Pressure01.</summary>
            public static float SafeCeiling = 0.15f;
            public static float PressureCeiling = 0.42f;
            public static float WarningCeiling = 0.68f;
            public static float FinalCeiling = 1f;

            /// <summary>What a loan with nothing left of it (but not yet closed) still weighs, as
            /// a share of its state's ceiling. Paying never takes the screen all the way back
            /// while a debt is open.</summary>
            public static float LoadFloor = 0.5f;
            /// <summary>How much further interest can push a loan past what it opened at (at
            /// half as much again owed).</summary>
            public static float OverLoad = 0.12f;
            /// <summary>A stage whose minimum is paid weighs this much of what it would.</summary>
            public static float MinimumPaidEase = 0.9f;
            /// <summary>How fast the steady look follows a change, in Pressure01 per second.</summary>
            public static float Follow = 0.9f;

            // The curves, at Pressure01 = 0 / SAFE / PRESSURE / WARNING / FINAL DUE.
            public static readonly float[] Stops = { 0f, 0.15f, 0.42f, 0.68f, 1f };
            /// <summary>PERCEPTUAL vignette strength at the screen's edge (see the header).</summary>
            public static readonly float[] Vignette = { 0f, 0.02f, 0.05f, 0.085f, 0.14f };
            public static readonly float[] Desaturation = { 0f, 0f, 0.015f, 0.04f, 0.075f };
            public static readonly float[] BracketAlpha = { 0f, 0f, 0.35f, 0.8f, 0.95f };
            public static readonly float[] BracketInPx = { 0f, 0f, 0f, 2f, 5f };
            public static readonly float[] ShadowAlpha = { 0f, 0f, 0.05f, 0.18f, 0.32f };

            public static readonly Color VignetteInk = new Color(0.22f, 0.04f, 0.065f);
            public static readonly Color ShadowInk = new Color(0.07f, 0.015f, 0.028f);
            public static readonly Color BracketInk = new Color(0.33f, 0.07f, 0.1f);
            public static readonly Color ScoreDip = new Color(0.93f, 0.72f, 0.66f);
            public static readonly Color OwnFill = new Color(1f, 0.86f, 0.42f);
            public static readonly Color OwnTrack = new Color(0.33f, 0.29f, 0.2f);
            public static readonly Color BurdenTrack = new Color(0.3f, 0.06f, 0.09f);
            public static readonly Color BurdenFill = new Color(0.8f, 0.55f, 0.28f);
            public static readonly Color SeamBrass = new Color(0.62f, 0.5f, 0.3f);

            /// <summary>How far the vignette's clear zone reaches past the board's visible edge,
            /// and over how much it comes in; how far a screen edge's weight reaches inward.</summary>
            public static float ClearMargin = 0.3f;
            public static float ClearSoftness = 1.3f;
            public static float EdgeReach = 3.2f;

            /// <summary>The brackets: arm length, and how far outside the board's visible corner
            /// they stand before any pressure moves them in (world units).</summary>
            public static float BracketSize = 0.52f;
            public static float BracketGap = 0.12f;

            public static float ReliefTime = 1.1f;
            public static float PainTime = 1.2f;
            public static float CarryDipTime = 0.34f;
            public static float CarryDipScale = 0.97f;
            public static float CreakMin = 5f;
            public static float CreakMax = 8f;
        }

        /// <summary>The lab's switches: the FEATURES are on by default, the DEBUG READOUTS off.</summary>
        public static class Layers
        {
            public static bool Vignette = true;
            public static bool Desaturation = true;
            public static bool Brackets = true;
            public static bool BoardShadow = true;
            public static bool ScoreAccent = true;
            public static bool TargetBurden = true;

            public static bool ShowDebtPressureState;
            public static bool ShowDebtPressure01;
            public static bool ShowVignetteStrength;
            public static bool ShowBackgroundDesaturation;
            public static bool ShowBoardBracketOffset;
            public static bool ShowDebtBurdenHeader;

            public static void Reset()
            {
                Vignette = Desaturation = Brackets = BoardShadow = ScoreAccent = TargetBurden = true;
                ShowDebtPressureState = ShowDebtPressure01 = ShowVignetteStrength = false;
                ShowBackgroundDesaturation = ShowBoardBracketOffset = ShowDebtBurdenHeader = false;
            }

            /// <summary>Only the named feature on, for the lab's "X only" scenes.</summary>
            public static void Only(string feature)
            {
                Vignette = feature == "vignette";
                Desaturation = feature == "vignette";
                Brackets = feature == "brackets";
                BoardShadow = feature == "brackets";
                ScoreAccent = feature == "header";
                TargetBurden = feature == "header";
            }
        }

        /// <summary>One frame of what the rules say, and where things are on screen. Filled by
        /// the controller from the session - or by the lab with numbers of its own.</summary>
        public struct Frame
        {
            /// <summary>A loan is open and the run is on screen.</summary>
            public bool Active;
            /// <summary>A round is being played: the board, the header.</summary>
            public bool InRound;
            public CreditDeadline Deadline;
            /// <summary>What is owed over what the loan opened at (past 1 once interest grew it).</summary>
            public float DebtShare;
            /// <summary>This stage's minimum is not paid yet (in a round).</summary>
            public bool MinimumOwed;
            /// <summary>The board's VISIBLE rect in the world (its plate's edge).</summary>
            public Rect Board;
            /// <summary>The score line is showing, and where its parts are in the world.</summary>
            public bool Header;
            public Rect TotalNumber;
            public Rect RoundPart;
            /// <summary>The world x the burden legend must stay left of.</summary>
            public float HeaderRightLimit;
            public long OwnBar;
            public long Installment;
            public long RoundScore;
            public bool MinimumSatisfied;
        }

        public enum Cue
        {
            /// <summary>The final stage's low dry paper creak, every five to eight seconds.</summary>
            Creak,
            /// <summary>A payment let the pressure go.</summary>
            Release
        }

        public Action<Cue> Sounded;

        /// <summary>The backdrop's desaturation seam.</summary>
        public Action<float> Desaturate;

        /// <summary>The rate the transients play at (the lab's slow motion). The game sets 1.</summary>
        public float PlaybackRate = 1f;

        /// <summary>The pressure as it is SHOWN now (eased), 0..1.</summary>
        public float Pressure01 { get; private set; }

        /// <summary>What the rules' state asks for right now, before easing.</summary>
        public float TargetPressure { get; private set; }

        public CreditDeadline State
        {
            get { return frame.Deadline; }
        }

        /// <summary>The TOTAL's answer while the carried debt lands on it (TickScoreResponse).</summary>
        public float ScoreClaim { get; private set; }

        public float ScoreScale { get; private set; }

        public Color ScoreInk { get; private set; }

        // =================================================================== state

        private Camera cam;
        private Frame frame;
        private SpriteRenderer vignette;
        private Texture2D vignetteTexture;
        private Rect builtBoard;
        private Rect lastBoard;
        private Vector2 builtHalf;
        private SpriteRenderer shadow;
        private readonly SpriteRenderer[] brackets = new SpriteRenderer[4];
        private SpriteRenderer underline;
        private SpriteRenderer splitOwn;
        private SpriteRenderer splitOwnFill;
        private SpriteRenderer splitDebt;
        private SpriteRenderer splitDebtFill;
        private SpriteRenderer splitSeam;
        private SpriteRenderer legendRim;
        private SpriteRenderer legendPlate;
        private TextMesh legendOwn;
        private TextMesh legendDebt;
        private TextMesh devText;

        private float relief;
        private float reliefShare;
        private float pain;
        private float painShare;
        private float minimumRelief;
        private float secured;
        private float carryDip = -1f;
        private float underlineShown;
        private float headerShown;
        private float bracketShown;
        private float nextCreak = -1f;
        private float clock;
        private bool built;

        private const int VignetteOrder = -8;
        private const int ShadowOrder = -7;
        private const int BracketOrder = 2;
        private const int HeaderOrder = 60;

        // =================================================================== building

        public void Build(Camera camera)
        {
            cam = camera;
            if (built)
            {
                return;
            }
            built = true;
            vignette = MakeSprite("Vignette", null, VignetteOrder);
            shadow = MakeSprite("BoardShadow", DebtLedgerShapes.BoardShadow, ShadowOrder);
            for (int i = 0; i < 4; i++)
            {
                brackets[i] = MakeSprite("Bracket" + i, DebtLedgerShapes.Bracket, BracketOrder);
            }
            underline = MakeSprite("ScoreUnderline", ViewUtil.WhiteSprite, HeaderOrder);
            splitOwn = MakeSprite("SplitOwn", ViewUtil.WhiteSprite, HeaderOrder);
            splitOwnFill = MakeSprite("SplitOwnFill", ViewUtil.WhiteSprite, HeaderOrder + 1);
            splitDebt = MakeSprite("SplitDebt", ViewUtil.WhiteSprite, HeaderOrder);
            splitDebtFill = MakeSprite("SplitDebtFill", ViewUtil.WhiteSprite, HeaderOrder + 1);
            splitSeam = MakeSprite("SplitSeam", ViewUtil.WhiteSprite, HeaderOrder + 2);
            legendRim = MakeSprite("LegendRim", ViewUtil.RoundedSprite, HeaderOrder);
            legendPlate = MakeSprite("LegendPlate", ViewUtil.RoundedSprite, HeaderOrder + 1);
            legendOwn = DebtLedgerView.Text(transform, "LegendOwn", " ", 0.021f, Style.OwnFill,
                HeaderOrder + 2, TextAnchor.MiddleLeft);
            legendDebt = DebtLedgerView.Text(transform, "LegendDebt", " ", 0.021f,
                DebtLedgerView.Style.Cream, HeaderOrder + 3, TextAnchor.MiddleLeft);
            devText = DebtLedgerView.Text(transform, "PressureDev", " ", 0.0115f,
                new Color(0.6f, 1f, 0.9f), HeaderOrder + 9, TextAnchor.UpperLeft);
            ScoreScale = 1f;
            ScoreInk = Color.white;
        }

        /// <summary>This frame's facts. Called every frame by the controller (or the lab).</summary>
        public void SetFrame(Frame f)
        {
            frame = f;
            TargetPressure = f.Active ? Target(f.Deadline, f.DebtShare, f.MinimumOwed) : 0f;
        }

        /// <summary>
        /// THE ONE MAPPING from the rules' state to how much the screen presses - presentation,
        /// never a rule. The named state sets the ceiling; how much of the loan is still owed moves
        /// it within that; a paid minimum eases it a tenth.
        /// </summary>
        public static float Target(CreditDeadline deadline, float debtShare, bool minimumOwed)
        {
            float ceiling;
            switch (deadline)
            {
                case CreditDeadline.Safe: ceiling = Style.SafeCeiling; break;
                case CreditDeadline.Pressure: ceiling = Style.PressureCeiling; break;
                case CreditDeadline.Warning: ceiling = Style.WarningCeiling; break;
                case CreditDeadline.FinalDue: ceiling = Style.FinalCeiling; break;
                default: return 0f;
            }
            float load = Mathf.Lerp(Style.LoadFloor, 1f, Mathf.Clamp01(debtShare))
                + Style.OverLoad * Mathf.Clamp01((debtShare - 1f) * 2f);
            return Mathf.Clamp01(ceiling * load * (minimumOwed ? 1f : Style.MinimumPaidEase));
        }

        /// <summary>A payment landed: <paramref name="share"/> is how much of the debt it took.
        /// The screen lets go a little further than its new steady level for a breath.</summary>
        public void Relieve(float share)
        {
            relief = 1f;
            reliefShare = Mathf.Clamp01(share);
            Emit(Cue.Release);
        }

        /// <summary>Interest landed: <paramref name="share"/> is how much it added.</summary>
        public void Tighten(float share)
        {
            pain = 1f;
            painShare = Mathf.Clamp01(share * 6f);
        }

        /// <summary>This stage's minimum is paid: a small breath, and the burden says "secured".</summary>
        public void MinimumRelief()
        {
            minimumRelief = 1f;
            secured = 1f;
        }

        /// <summary>The carried debt's slip has landed on the TOTAL: it dips under the weight.</summary>
        public void CarryImpact()
        {
            carryDip = 0f;
        }

        /// <summary>Puts every transient down at once (the lab, a new run).</summary>
        public void Settle()
        {
            relief = pain = minimumRelief = secured = 0f;
            carryDip = -1f;
            ScoreClaim = 0f;
            ScoreScale = 1f;
            Pressure01 = TargetPressure;
            bracketShown = Curve(Style.BracketAlpha, Pressure01);
        }

        // =================================================================== the clock

        private void LateUpdate()
        {
            if (!built || cam == null)
            {
                return;
            }
            float dt = Time.deltaTime * PlaybackRate;
            clock += dt;
            Pressure01 = Mathf.MoveTowards(Pressure01, TargetPressure, Style.Follow * dt);
            relief = Mathf.Max(0f, relief - dt / Style.ReliefTime);
            pain = Mathf.Max(0f, pain - dt / Style.PainTime);
            minimumRelief = Mathf.Max(0f, minimumRelief - dt / Style.ReliefTime);
            secured = Mathf.Max(0f, secured - dt / 0.6f);

            PaintVignette();
            PaintBoard(dt);
            PaintHeader(dt);
            TickScoreClaim(dt);
            TickCreak();
            PaintDebug();
        }

        /// <summary>The perceptual strength at the screen's edge right now, relief and pain in.</summary>
        public float VignetteStrength
        {
            get
            {
                float steady = Curve(Style.Vignette, Pressure01);
                float strength = steady * (1f - (0.1f + 0.1f * reliefShare) * Breath(relief))
                    - Mathf.Lerp(0.015f, 0.025f, Pressure01) * Breath(minimumRelief)
                    + Mathf.Lerp(0.02f, 0.04f, painShare) * Breath(pain);
                return frame.Active ? Mathf.Clamp(strength, 0f, 0.2f) : 0f;
            }
        }

        public float DesaturationShown
        {
            get { return frame.Active ? Curve(Style.Desaturation, Pressure01) : 0f; }
        }

        /// <summary>How far the brackets stand in from where they start, in reference pixels.</summary>
        public float BracketOffsetPx
        {
            get
            {
                return Curve(Style.BracketInPx, Pressure01)
                    - (1f + reliefShare) * Breath(relief) - Breath(minimumRelief)
                    + Breath(pain);
            }
        }

        private void PaintVignette()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            float strength = Layers.Vignette ? VignetteStrength : 0f;
            if (Desaturate != null)
            {
                Desaturate(Layers.Desaturation ? DesaturationShown : 0f);
            }
            if (strength <= 0.0005f)
            {
                vignette.enabled = false;
                return;
            }
            Rect board = frame.Board.width > 0.01f ? frame.Board
                : new Rect(-halfW * 0.4f, -halfH * 0.5f, halfW * 0.8f, halfH);
            // Rebaked only when the screen or the arena REALLY moved: the overtime squeeze breathes
            // the arena a few percent every pulse, and a clear zone with 1.3 units of softness does
            // not care about a tenth of one.
            if (vignetteTexture == null || !Mathf.Approximately(builtHalf.x, halfW)
                || !Mathf.Approximately(builtHalf.y, halfH)
                || Vector2.Distance(builtBoard.center, board.center) > 0.35f
                || Mathf.Abs(builtBoard.width - board.width) > 0.35f
                || Mathf.Abs(builtBoard.height - board.height) > 0.35f)
            {
                BakeVignette(halfW, halfH, board, c);
            }
            vignette.enabled = true;
            vignette.transform.position = new Vector3(c.x, c.y, 0f);
            float w = halfW * 2f * 1.04f;
            float h = halfH * 2f * 1.04f;
            vignette.transform.localScale = new Vector3(w / vignetteTexture.width, h / vignetteTexture.height, 1f);
            // a PERCEPTUAL strength becomes the linear alpha that lands the same drop on screen
            float linear = 1f - Mathf.Pow(1f - strength, 2.2f);
            vignette.color = DebtLedgerView.WithAlpha(Style.VignetteInk, linear);
        }

        /// <summary>The vignette's SHAPE: weight that comes in from every screen edge (rounder at
        /// the corners), multiplied by a clear zone round the board - so the edges of the world
        /// get heavier and the arena never does.</summary>
        private void BakeVignette(float halfW, float halfH, Rect board, Vector3 camPos)
        {
            const float res = 16f;
            float w = halfW * 2f * 1.04f;
            float h = halfH * 2f * 1.04f;
            int tw = Mathf.Clamp(Mathf.RoundToInt(w * res), 32, 400);
            int th = Mathf.Clamp(Mathf.RoundToInt(h * res), 32, 400);
            if (vignetteTexture == null || vignetteTexture.width != tw || vignetteTexture.height != th)
            {
                if (vignetteTexture != null)
                {
                    Destroy(vignetteTexture);
                }
                vignetteTexture = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                vignetteTexture.wrapMode = TextureWrapMode.Clamp;
                vignetteTexture.filterMode = FilterMode.Bilinear;
                vignetteTexture.hideFlags = HideFlags.HideAndDontSave;
            }
            Vector2 bc = board.center - (Vector2)camPos;
            Vector2 bh = board.size * 0.5f;
            var px = new Color32[tw * th];
            for (int j = 0; j < th; j++)
            {
                float y = ((j + 0.5f) / th - 0.5f) * h;
                for (int i = 0; i < tw; i++)
                {
                    float x = ((i + 0.5f) / tw - 0.5f) * w;
                    // weight from the screen's edges: 1 at the edge, 0 a reach inside it
                    float edge = -RoundedBox(x, y, halfW, halfH, 1.6f);
                    float fromEdge = 1f - Mathf.Clamp01(edge / Style.EdgeReach);
                    fromEdge = fromEdge * fromEdge * (3f - 2f * fromEdge);
                    // clear over the arena and just round it
                    float outside = RoundedBox(x - bc.x, y - bc.y, bh.x, bh.y, 0.3f) - Style.ClearMargin;
                    float clear = Mathf.Clamp01(outside / Style.ClearSoftness);
                    clear = clear * clear * (3f - 2f * clear);
                    float a = Mathf.Clamp01(fromEdge * clear);
                    px[j * tw + i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            vignetteTexture.SetPixels32(px);
            vignetteTexture.Apply(false);
            if (vignette.sprite != null)
            {
                Destroy(vignette.sprite);
            }
            vignette.sprite = Sprite.Create(vignetteTexture, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 1f);
            builtBoard = board;
            builtHalf = new Vector2(halfW, halfH);
        }

        /// <summary>The board's surroundings: the deeper outer shadow and the four brackets.</summary>
        private void PaintBoard(float dt)
        {
            bool show = frame.Active && frame.InRound && frame.Board.width > 0.01f;
            float targetAlpha = show ? Curve(Style.BracketAlpha, Pressure01) : 0f;
            bracketShown = Mathf.MoveTowards(bracketShown, targetAlpha, dt * 1.6f);
            // the brackets fade out where the board WAS when it goes (the market)
            if (frame.Board.width > 0.01f)
            {
                lastBoard = frame.Board;
            }
            Rect b = lastBoard;
            // SHADOW: the board's own drop shadow, a little deeper - under the plate.
            float shadowAlpha = show && Layers.BoardShadow ? Curve(Style.ShadowAlpha, Pressure01) : 0f;
            if (shadowAlpha > 0.001f)
            {
                shadow.enabled = true;
                float boxW = b.width / DebtLedgerShapes.BoardShadowBox;
                float boxH = b.height / DebtLedgerShapes.BoardShadowBox;
                DebtLedgerView.Place(shadow, b.center, boxW, boxH);
                shadow.color = DebtLedgerView.WithAlpha(Style.ShadowInk, shadowAlpha);
            }
            else
            {
                shadow.enabled = false;
            }
            // BRACKETS: outside the corners, closing in by pressure, never into the arena.
            float px = ReferencePixel();
            float gap = Mathf.Max(0.02f, Style.BracketGap - BracketOffsetPx * px);
            float size = Style.BracketSize;
            float alpha = Layers.Brackets ? bracketShown : 0f;
            for (int i = 0; i < 4; i++)
            {
                SpriteRenderer r = brackets[i];
                if (alpha <= 0.001f)
                {
                    r.enabled = false;
                    continue;
                }
                r.enabled = true;
                // 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left; the sprite is drawn for
                // the top-left and turned a quarter per corner, clockwise
                Vector2 corner;
                Vector2 outward;
                switch (i)
                {
                    case 0: corner = new Vector2(b.xMin, b.yMax); outward = new Vector2(-1f, 1f); break;
                    case 1: corner = new Vector2(b.xMax, b.yMax); outward = new Vector2(1f, 1f); break;
                    case 2: corner = new Vector2(b.xMax, b.yMin); outward = new Vector2(1f, -1f); break;
                    default: corner = new Vector2(b.xMin, b.yMin); outward = new Vector2(-1f, -1f); break;
                }
                Vector2 point = corner + outward * gap;
                Vector2 centre = point - outward * (size * 0.5f);
                DebtLedgerView.Place(r, centre, size, size);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * i);
                r.color = DebtLedgerView.WithAlpha(Style.BracketInk, alpha);
            }
        }

        /// <summary>The score line's two answers: the underline under the TOTAL, and the round
        /// target's split into its own share and the debt's burden.</summary>
        private void PaintHeader(float dt)
        {
            bool header = frame.Header && frame.InRound;
            // THE UNDERLINE: the debt's colour under the TOTAL's number; it retracts when the
            // debt is gone rather than blinking off.
            underlineShown = Mathf.MoveTowards(underlineShown,
                header && frame.Active && Layers.ScoreAccent ? 1f : 0f, dt * 3f);
            if (underlineShown > 0.001f && frame.TotalNumber.width > 0.01f)
            {
                underline.enabled = true;
                Rect n = frame.TotalNumber;
                float w = n.width * DebtLedgerView.EaseInOut(underlineShown);
                float line = Mathf.Max(0.035f, n.height * 0.1f);
                DebtLedgerView.Place(underline, new Vector2(n.xMin + w * 0.5f, n.yMin - line * 0.8f), w, line);
                underline.color = DebtLedgerView.WithAlpha(DebtLedgerView.Style.Notch, 0.95f);
            }
            else
            {
                underline.enabled = false;
            }

            bool burden = header && frame.Active && frame.Installment > 0 && Layers.TargetBurden
                && frame.RoundPart.width > 0.01f;
            headerShown = Mathf.MoveTowards(headerShown, burden ? 1f : 0f, dt * 4f);
            float a = headerShown;
            if (a <= 0.001f)
            {
                splitOwn.enabled = splitOwnFill.enabled = splitDebt.enabled = false;
                splitDebtFill.enabled = splitSeam.enabled = legendRim.enabled = legendPlate.enabled = false;
                DebtLedgerView.SetText(legendOwn, " ");
                DebtLedgerView.SetText(legendDebt, " ");
                return;
            }
            splitOwn.enabled = splitOwnFill.enabled = splitDebt.enabled = true;
            splitDebtFill.enabled = splitSeam.enabled = true;
            Rect part = frame.RoundPart;
            long own = Math.Max(0L, frame.OwnBar);
            long debt = Math.Max(0L, frame.Installment);
            long bar = Math.Max(1L, own + debt);
            // THE SPLIT BAR: exactly as wide as "raunt 0 / 1548", its two parts in proportion -
            // the burden never narrower than a fifth, or a small minimum would vanish into a seam.
            float share = Mathf.Clamp(debt / (float)bar, 0.2f, 0.8f);
            float thick = Mathf.Max(0.04f, part.height * 0.12f);
            float y = part.yMin - thick * 1.1f;
            float ownW = part.width * (1f - share);
            float debtW = part.width - ownW;
            float ownMid = part.xMin + ownW * 0.5f;
            float debtMid = part.xMin + ownW + debtW * 0.5f;
            DebtLedgerView.Place(splitOwn, new Vector2(ownMid, y), ownW, thick);
            splitOwn.color = DebtLedgerView.WithAlpha(Style.OwnTrack, 0.9f * a);
            DebtLedgerView.Place(splitDebt, new Vector2(debtMid, y), debtW, thick);
            Color debtTrack = Color.Lerp(Style.BurdenTrack, new Color(0.34f, 0.27f, 0.16f),
                frame.MinimumSatisfied ? 0.7f : 0f);
            splitDebt.color = DebtLedgerView.WithAlpha(debtTrack, 0.95f * a);
            // the round's progress runs through both parts: its own share first, then the burden
            float ownProgress = Mathf.Clamp01(frame.RoundScore / (float)Math.Max(1L, own));
            float debtProgress = frame.MinimumSatisfied ? 1f
                : Mathf.Clamp01((frame.RoundScore - own) / (float)Math.Max(1L, debt));
            float ownFillW = ownW * ownProgress;
            DebtLedgerView.Place(splitOwnFill, new Vector2(part.xMin + ownFillW * 0.5f, y), ownFillW, thick);
            splitOwnFill.color = DebtLedgerView.WithAlpha(Style.OwnFill, 0.85f * a);
            float debtFillW = debtW * debtProgress;
            DebtLedgerView.Place(splitDebtFill, new Vector2(part.xMin + ownW + debtFillW * 0.5f, y),
                debtFillW, thick);
            splitDebtFill.color = DebtLedgerView.WithAlpha(Style.BurdenFill, 0.9f * a);
            // the seam: a muted brass edge where the round's own bar ends and the debt begins
            float flash = Breath(secured);
            DebtLedgerView.Place(splitSeam, new Vector2(part.xMin + ownW, y), Mathf.Max(0.02f, thick * 0.45f),
                thick * 2.2f);
            splitSeam.color = DebtLedgerView.WithAlpha(Color.Lerp(Style.SeamBrass, DebtLedgerView.Style.GoldIvory,
                flash), a);

            // THE LEGEND: "900 + [648 BORÇ]", legible, beside the line - never a microscopic chip.
            string ownText = DebtLedgerView.Money(own) + " +";
            string debtText = DebtLedgerView.Money(debt) + " " + Loc.Pick("DEBT", "BORÇ");
            DebtLedgerView.SetText(legendOwn, ownText);
            DebtLedgerView.SetText(legendDebt, debtText);
            float lineH = Mathf.Max(0.18f, part.height * 0.62f);
            float scale = lineH / (0.021f * 9f);
            float ownTextW = DebtLedgerView.TextWidth(legendOwn, ownText) * scale;
            float debtTextW = DebtLedgerView.TextWidth(legendDebt, debtText) * scale;
            float plateW = debtTextW + lineH * 0.9f;
            float plateH = lineH * 1.25f;
            float x = part.xMax + lineH * 0.7f;
            float total = ownTextW + lineH * 0.35f + plateW;
            bool fits = x + total <= frame.HeaderRightLimit;
            if (!fits)
            {
                legendRim.enabled = legendPlate.enabled = false;
                DebtLedgerView.SetText(legendOwn, " ");
                DebtLedgerView.SetText(legendDebt, " ");
                return;
            }
            float cy = part.center.y;
            legendOwn.transform.position = new Vector3(x, cy, 0f);
            legendOwn.transform.localScale = new Vector3(scale, scale, 1f);
            ViewUtil.SetTextColor(legendOwn, DebtLedgerView.WithAlpha(Style.OwnFill, a));
            float plateX = x + ownTextW + lineH * 0.35f;
            // "secured": the plate goes from burgundy to dark brass and answers with a small swell
            float swell = 1f + 0.05f * Breath(secured);
            legendRim.enabled = legendPlate.enabled = true;
            DebtLedgerView.Place(legendRim, new Vector2(plateX + plateW * 0.5f, cy),
                (plateW + 0.04f) * swell, (plateH + 0.04f) * swell);
            legendRim.color = DebtLedgerView.WithAlpha(Color.Lerp(Style.SeamBrass, DebtLedgerView.Style.GoldIvory,
                flash), a);
            DebtLedgerView.Place(legendPlate, new Vector2(plateX + plateW * 0.5f, cy), plateW * swell, plateH * swell);
            Color plate = frame.MinimumSatisfied ? new Color(0.24f, 0.18f, 0.1f) : DebtLedgerView.Style.Wine;
            legendPlate.color = DebtLedgerView.WithAlpha(plate, a);
            legendDebt.transform.position = new Vector3(plateX + lineH * 0.45f, cy, 0f);
            legendDebt.transform.localScale = new Vector3(scale, scale, 1f);
            ViewUtil.SetTextColor(legendDebt, DebtLedgerView.WithAlpha(DebtLedgerView.Style.Cream, a));
        }

        /// <summary>The carry's arrival on the TOTAL: 1 -> 0.97 -> 1 through a muted
        /// cream-burgundy. Published, never written - TickScoreResponse is the score's one writer.</summary>
        private void TickScoreClaim(float dt)
        {
            if (carryDip < 0f)
            {
                ScoreClaim = 0f;
                ScoreScale = 1f;
                return;
            }
            carryDip += dt;
            float u = Mathf.Clamp01(carryDip / Style.CarryDipTime);
            float k = Mathf.Sin(u * Mathf.PI);
            ScoreScale = 1f - (1f - Style.CarryDipScale) * k;
            ScoreInk = Style.ScoreDip;
            ScoreClaim = k;
            if (u >= 1f)
            {
                carryDip = -1f;
                ScoreClaim = 0f;
                ScoreScale = 1f;
            }
        }

        /// <summary>The final stage's only sound bed: a low, dry paper creak now and then - never
        /// a clock.</summary>
        private void TickCreak()
        {
            bool due = frame.Active && frame.InRound && frame.Deadline == CreditDeadline.FinalDue;
            if (!due)
            {
                nextCreak = -1f;
                return;
            }
            if (nextCreak < 0f)
            {
                nextCreak = clock + Mathf.Lerp(Style.CreakMin, Style.CreakMax, Hash(3));
                return;
            }
            if (clock >= nextCreak)
            {
                nextCreak = clock + Mathf.Lerp(Style.CreakMin, Style.CreakMax, Hash((int)(clock * 13f)));
                Emit(Cue.Creak);
            }
        }

        private void PaintDebug()
        {
            bool dev = UnityEngine.Debug.isDebugBuild || Application.isEditor;
            var sb = new System.Text.StringBuilder();
            if (dev && Layers.ShowDebtPressureState)
            {
                sb.Append("state ").Append(frame.Deadline).Append(frame.Active ? "" : " (no loan)")
                    .Append(frame.MinimumOwed ? "  minimum owed" : "  minimum paid").Append('\n');
            }
            if (dev && Layers.ShowDebtPressure01)
            {
                sb.Append("pressure01 ").Append(Pressure01.ToString("0.000")).Append("  target ")
                    .Append(TargetPressure.ToString("0.000")).Append("  debt share ")
                    .Append(frame.DebtShare.ToString("0.00")).Append('\n');
            }
            if (dev && Layers.ShowVignetteStrength)
            {
                float v = VignetteStrength;
                sb.Append("vignette ").Append(v.ToString("0.000")).Append(" perceptual, ")
                    .Append((1f - Mathf.Pow(1f - v, 2.2f)).ToString("0.000")).Append(" linear alpha\n");
            }
            if (dev && Layers.ShowBackgroundDesaturation)
            {
                sb.Append("backdrop desaturation ").Append((DesaturationShown * 100f).ToString("0.0")).Append("%\n");
            }
            if (dev && Layers.ShowBoardBracketOffset)
            {
                sb.Append("brackets ").Append(BracketOffsetPx.ToString("0.0")).Append(" px in, alpha ")
                    .Append(bracketShown.ToString("0.00")).Append("  shadow ")
                    .Append(Curve(Style.ShadowAlpha, Pressure01).ToString("0.00")).Append('\n');
            }
            if (dev && Layers.ShowDebtBurdenHeader)
            {
                sb.Append("bar ").Append(frame.OwnBar + frame.Installment).Append(" = own ").Append(frame.OwnBar)
                    .Append(" + burden ").Append(frame.Installment).Append("  round ").Append(frame.RoundScore)
                    .Append(frame.MinimumSatisfied ? "  secured" : "").Append('\n');
            }
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            devText.transform.position = new Vector3(c.x - halfW + 0.3f, c.y - halfH * 0.18f, 0f);
            DebtLedgerView.SetText(devText, sb.Length > 0 ? sb.ToString() : " ");
        }

        // =================================================================== plumbing

        /// <summary>A reference pixel in world units - a pixel at 1080 lines, so "2 px in" is the
        /// same look on every screen.</summary>
        private float ReferencePixel()
        {
            return cam.orthographicSize * 2f / 1080f;
        }

        /// <summary>Piecewise-linear through the named states' stops.</summary>
        public static float Curve(float[] values, float p)
        {
            float[] stops = Style.Stops;
            p = Mathf.Clamp01(p);
            for (int i = 1; i < stops.Length; i++)
            {
                if (p <= stops[i])
                {
                    float u = (p - stops[i - 1]) / Mathf.Max(0.0001f, stops[i] - stops[i - 1]);
                    return Mathf.Lerp(values[i - 1], values[i], u);
                }
            }
            return values[values.Length - 1];
        }

        private static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>A transient's shape, from its clock running 1 -> 0: in over the first fifth,
        /// out over the rest. Never a step - the screen breathes, it does not blink.</summary>
        private static float Breath(float remaining)
        {
            if (remaining <= 0f)
            {
                return 0f;
            }
            float u = 1f - Mathf.Clamp01(remaining);
            return u < 0.2f ? Ease(u / 0.2f) : 1f - Ease((u - 0.2f) / 0.8f);
        }

        private static float RoundedBox(float px, float py, float hx, float hy, float r)
        {
            r = Mathf.Min(r, Mathf.Min(hx, hy));
            float qx = Mathf.Abs(px) - (hx - r);
            float qy = Mathf.Abs(py) - (hy - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)
                + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private void Emit(Cue cue)
        {
            if (Sounded != null)
            {
                Sounded(cue);
            }
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        private void OnDestroy()
        {
            if (vignetteTexture != null)
            {
                Destroy(vignetteTexture);
                vignetteTexture = null;
            }
        }

        private static float Hash(int key)
        {
            unchecked
            {
                uint h = (uint)(key * 73856093) ^ 0x27D4EB2Fu;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
