// PURPOSE: THE BLACKJACK TABLE ON SCREEN - "Double or nothing / the house table". Everything the
// duel boss stage shows that is not a board or the player's hand: the casino felt and its light,
// the bank and its target, the score duel, the wager on the table, the house's CLOSED hand and the
// card it plays, the intro, the bet tray and every result. GameUiController.Duel drives it;
// this class only draws what it is told and plays the beats it is asked for.
//
// THE VIEW DECIDES NOTHING. Every number it prints - the bank, the target, the stake, the house's
// advance, both scores, the payout - is handed in from Core (BlackjackBoss / DuelHandResult), and
// every card the house plays arrives as Core's report (DuelAiPlay: which slot of its closed hand,
// how many cards before and after). The table animates TOWARD those numbers; it never works one
// out. The one thing it keeps of its own is what it is SHOWING right now (shownBank and the two
// shown scores), so a count can run from the old value to the new one.
//
// THREE HIERARCHIES (the brief's): information - bank, target, both scores, the bet, whose turn,
// the result; motion - the house's card, the money moving, the scores counting, the result;
// atmosphere - the felt, the amber light, the vignette, the motes. Atmosphere never competes: it
// sits under the boards (orders -200..-189) and moves on the scale of seconds.
//
// MONEY IS NEVER CONFUSED WITH POINTS. A line's energy goes to the HAND's score and nowhere else;
// the bank moves only when a stake is placed, a payout or a push comes back, or the house advances
// its 500 - and it moves with chips and a deeper tick, never with the score's glassy one.
//
// The table is split over partial files: .Hud (bank, target, scores, labels, banner), .Wager,
// .Dealer (the closed hand and the card in flight), .Intro, .Bet (the tray), .Results (the
// sequences: bet placed, deal, hand end, next hand, stage won, bankruptcy) and .Debug.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView : MonoBehaviour
    {
        public enum Side
        {
            None,
            Player,
            Dealer
        }

        /// <summary>The table's audio. Set by the owner; null is silence.</summary>
        public SoundFx Sfx;

        /// <summary>Where everything stands (BlackjackLayout.Solve).</summary>
        public BlackjackLayout L;

        private bool built;
        private float clock;

        // ---- sorting orders: atmosphere under the boards, information over them
        private const int FeltOrder = -200;
        private const int ShadeOrder = -198;
        private const int LightOrder = -197;
        private const int ArcOrder = -195;
        private const int SeamOrder = -194;
        private const int MoteOrder = -193;
        private const int VignetteOrder = -190;
        private const int StageShadowOrder = -6;
        private const int EdgeGlowOrder = -4;
        private const int FootprintOrder = 3;
        private const int VeilOrder = 8;
        private const int DealerHandOrder = 36;
        private const int WagerOrder = 40;
        private const int HudOrder = 45;
        private const int FlightOrder = 62;
        private const int BannerOrder = 70;
        private const int TrayOrder = 76;
        private const int IntroOrder = 82;
        private const int DebugOrder = 96;

        // ---- the felt
        private SpriteRenderer felt;
        private SpriteRenderer topShade;
        private SpriteRenderer centreLight;
        private SpriteRenderer playerWarm;
        private SpriteRenderer dealerCool;
        private SpriteRenderer burgundyDrift;
        private SpriteRenderer vignette;
        private SpriteRenderer arcLow;
        private SpriteRenderer arcHigh;
        private SpriteRenderer seam;
        private SpriteRenderer seamMark;
        private readonly SpriteRenderer[] stageShadow = new SpriteRenderer[2];
        private readonly SpriteRenderer[] edgeGlow = new SpriteRenderer[2];
        private readonly SpriteRenderer[] veil = new SpriteRenderer[2];
        private readonly List<SpriteRenderer> motes = new List<SpriteRenderer>();

        // ---- the mood, all presentation: where each value is going and where it is
        /// <summary>The amber light over the table (1 = its resting strength).</summary>
        private float lightTarget = 1f, light = 1f;
        /// <summary>The burgundy closing in at the edges (0 = resting).</summary>
        private float burgundyTarget, burgundy;
        /// <summary>How hard the current stake presses (0..1, the bet's share of the bank).</summary>
        private float tensionTarget, tension;
        /// <summary>The all-in's own weight.</summary>
        private float allInTarget, allIn;
        /// <summary>A stage won: the warm light opening up.</summary>
        private float winTarget, win;
        /// <summary>Per side: whose turn it is (edge lift), how dimmed it is, how grey.</summary>
        private readonly float[] activeTarget = new float[2], active = new float[2];
        private readonly float[] dimTarget = new float[2], dim = new float[2];
        private readonly float[] greyTarget = new float[2], grey = new float[2];
        private readonly float[] warmTarget = new float[2], warm = new float[2];
        /// <summary>The felt wipe between hands, over both boards (0..1).</summary>
        private float wipe;

        public static readonly Color VignetteInk = new Color(0.055f, 0.012f, 0.028f);
        private static readonly Color CoolLight = new Color(0.55f, 0.78f, 0.82f);
        private static readonly Color VeilInk = new Color(0.018f, 0.04f, 0.04f);
        private static readonly Color GreyInk = new Color(0.16f, 0.16f, 0.17f);

        // ================================================================== life

        /// <summary>Builds every piece once. Placed by Place; drawn while the object is active.</summary>
        public void Build()
        {
            if (built)
            {
                return;
            }
            built = true;
            BuildFelt();
            BuildHud();
            BuildWager();
            BuildDealer();
        }

        /// <summary>Puts everything where the layout says. Called whenever the screen changes.</summary>
        public void Place(BlackjackLayout layout)
        {
            Build();
            L = layout;
            PlaceFelt();
            PlaceHud();
            PlaceWager();
            PlaceDealer();
            PlaceBet();
            PlaceIntro();
        }

        /// <summary>Back to a calm, empty table: no stake, no house hand, nothing animating.</summary>
        public void ResetTable()
        {
            StopAllCoroutines();
            sequenceDepth = 0;
            lightTarget = light = 1f;
            burgundyTarget = burgundy = 0f;
            tensionTarget = tension = 0f;
            allInTarget = allIn = 0f;
            winTarget = win = 0f;
            wipe = 0f;
            for (int i = 0; i < 2; i++)
            {
                activeTarget[i] = active[i] = 0f;
                dimTarget[i] = dim[i] = 0f;
                greyTarget[i] = grey[i] = 0f;
                warmTarget[i] = warm[i] = 0f;
            }
            ResetHud();
            ResetWager();
            ResetDealer();
            HideBetTray();
            HideIntro();
        }

        /// <summary>How many sequences are playing - the controller holds input while any is.</summary>
        private int sequenceDepth;

        public bool Busy
        {
            get { return sequenceDepth > 0; }
        }

        /// <summary>Runs a sequence and counts it as busy while it plays.</summary>
        public Coroutine Run(IEnumerator sequence)
        {
            return StartCoroutine(Counted(sequence));
        }

        private IEnumerator Counted(IEnumerator sequence)
        {
            sequenceDepth++;
            try
            {
                while (true)
                {
                    object current;
                    try
                    {
                        if (!sequence.MoveNext())
                        {
                            break;
                        }
                        current = sequence.Current;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        break;
                    }
                    yield return current;
                }
            }
            finally
            {
                sequenceDepth = Mathf.Max(0, sequenceDepth - 1);
            }
        }

        private static float T(float seconds)
        {
            return BlackjackFx.Tuning.T(seconds);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }

        private void Update()
        {
            if (!built)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            TickFelt(dt);
            TickHud(dt);
            TickWager(dt);
            TickDealer(dt);
            TickBet(dt);
            TickIntro(dt);
            TickDebug();
        }

        // ================================================================== mood (the controller's knobs)

        /// <summary>Whose turn it is: that board's edge lifts warm, the other sits back a little.</summary>
        public void SetActiveSide(Side side)
        {
            activeTarget[0] = side == Side.Player ? 1f : 0f;
            activeTarget[1] = side == Side.Dealer ? 1f : 0f;
            SetActiveLabel(side);
        }

        /// <summary>A side that has nothing left to play dims and says so.</summary>
        public void SetSideDone(Side side, bool done)
        {
            int i = side == Side.Player ? 0 : 1;
            dimTarget[i] = done ? 0.55f : 0f;
            SetDoneTag(side, done);
        }

        /// <summary>The stake's share of the bank - a heavier stake glows harder and tightens the edges.</summary>
        public void SetBetTension(float share, bool isAllIn)
        {
            tensionTarget = Mathf.Clamp01(share);
            allInTarget = isAllIn ? BlackjackFx.Tuning.allInMoodStrength : 0f;
        }

        /// <summary>Clears every per-side mark (between hands).</summary>
        public void ClearSides()
        {
            for (int i = 0; i < 2; i++)
            {
                dimTarget[i] = 0f;
                greyTarget[i] = 0f;
                warmTarget[i] = 0f;
            }
            SetDoneTag(Side.Player, false);
            SetDoneTag(Side.Dealer, false);
        }

        /// <summary>The mood's state as text (the background-mood debug view).</summary>
        public string MoodReadout
        {
            get
            {
                return "light " + light.ToString("0.00") + "  burgundy " + burgundy.ToString("0.00")
                    + "  tension " + tension.ToString("0.00") + "  allin " + allIn.ToString("0.00")
                    + "  win " + win.ToString("0.00") + "  dim " + dim[0].ToString("0.00") + "/" + dim[1].ToString("0.00")
                    + "  grey " + grey[0].ToString("0.00");
            }
        }

        // ================================================================== the felt

        private void BuildFelt()
        {
            felt = Sprite("Felt", BlackjackShapes.FeltSprite, Color.white, FeltOrder);
            topShade = Sprite("TopShade", BlackjackShapes.TopShadeSprite, Color.black, ShadeOrder);
            burgundyDrift = Sprite("BurgundyDrift", BlackjackShapes.RadialSprite, BlackjackShapes.Burgundy, ShadeOrder + 1);
            centreLight = Sprite("CentreLight", BlackjackShapes.RadialSprite, BlackjackShapes.Amber, LightOrder);
            playerWarm = Sprite("PlayerWarm", BlackjackShapes.RadialSprite, BlackjackShapes.Amber, LightOrder);
            dealerCool = Sprite("DealerCool", BlackjackShapes.RadialSprite, CoolLight, LightOrder);
            arcLow = Sprite("ArcLow", BlackjackShapes.ArcSprite, BlackjackShapes.Gold, ArcOrder);
            arcHigh = Sprite("ArcHigh", BlackjackShapes.ArcSprite, BlackjackShapes.Gold, ArcOrder);
            seam = Sprite("Seam", ViewUtil.WhiteSprite, BlackjackShapes.Gold, SeamOrder);
            seamMark = Sprite("SeamMark", BlackjackShapes.TokenSprite, BlackjackShapes.Gold, SeamOrder);
            vignette = Sprite("Vignette", BlackjackShapes.VignetteSprite, VignetteInk, VignetteOrder);
            for (int i = 0; i < 2; i++)
            {
                stageShadow[i] = Sprite("StageShadow" + i, BlackjackShapes.RadialSprite, Color.black, StageShadowOrder);
                edgeGlow[i] = ViewUtil.MakePlate(transform, "EdgeGlow" + i, Vector2.zero, Vector2.one,
                    BlackjackShapes.Amber, EdgeGlowOrder, ViewUtil.GlowSprite);
                veil[i] = Sprite("Veil" + i, ViewUtil.WhiteSprite, VeilInk, VeilOrder);
            }
            for (int i = 0; i < 7; i++)
            {
                motes.Add(Sprite("Mote" + i, BlackjackShapes.RadialSprite, BlackjackShapes.Amber, MoteOrder));
            }
        }

        private void PlaceFelt()
        {
            UiLayout layout = UiLayout.Active;
            float w = layout.HalfWidth * 2f + 1.2f;
            float h = layout.OrthoSize * 2f + 1.2f;
            Size(felt, Vector2.zero, new Vector2(w, h));
            Size(vignette, Vector2.zero, new Vector2(w - 1f, h - 1f));
            Size(topShade, new Vector2(0f, layout.OrthoSize * 0.5f), new Vector2(w, layout.OrthoSize + 0.6f));
            float s = L.BoardSize;
            Vector2 c = L.GroupCenter;
            float groupW = L.Stacked ? s : s * 2f + L.Gap;
            float groupH = L.Stacked ? s * 2f + L.Gap : s;
            Size(centreLight, c + new Vector2(0f, s * 0.08f), new Vector2(groupW * 1.55f, groupH * 1.9f));
            Size(playerWarm, L.Player, new Vector2(s * 1.7f, s * 1.7f));
            Size(dealerCool, L.Dealer, new Vector2(s * 1.7f, s * 1.7f));
            Size(arcLow, new Vector2(c.x, L.BoardBottom - 0.18f * L.Type), new Vector2(groupW + 1.6f, 0.7f * L.Type));
            arcLow.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Size(arcHigh, new Vector2(c.x, L.Bank.y - 0.15f * L.Type), new Vector2(L.LedgerHalfWidth * 2.6f, 0.55f * L.Type));
            if (L.Stacked)
            {
                Size(seam, c, new Vector2(s * 0.92f, 0.016f));
            }
            else
            {
                Size(seam, c, new Vector2(0.016f, s * 0.92f));
            }
            Size(seamMark, c + new Vector2(0f, L.WagerBetween ? s * 0.38f : 0f), new Vector2(0.14f, 0.2f));
            Vector2[] boards = { L.Player, L.Dealer };
            for (int i = 0; i < 2; i++)
            {
                Size(stageShadow[i], boards[i] + new Vector2(0f, -s * 0.06f), new Vector2(s * 1.5f, s * 1.42f));
                edgeGlow[i].transform.localPosition = boards[i];
                edgeGlow[i].size = new Vector2(s + 0.75f, s + 0.75f);
                Size(veil[i], boards[i], new Vector2(s + BoardView.BorderOverhang, s + BoardView.BorderOverhang));
            }
            for (int i = 0; i < motes.Count; i++)
            {
                ResetMote(i, true);
            }
        }

        private void TickFelt(float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 3f);
            float slow = 1f - Mathf.Exp(-dt * 1.4f);
            light = Mathf.Lerp(light, lightTarget, slow);
            burgundy = Mathf.Lerp(burgundy, burgundyTarget, slow);
            tension = Mathf.Lerp(tension, tensionTarget, k);
            allIn = Mathf.Lerp(allIn, allInTarget, k);
            win = Mathf.Lerp(win, winTarget, slow);
            for (int i = 0; i < 2; i++)
            {
                active[i] = Mathf.Lerp(active[i], activeTarget[i], k);
                dim[i] = Mathf.Lerp(dim[i], dimTarget[i], k);
                grey[i] = Mathf.Lerp(grey[i], greyTarget[i], slow);
                warm[i] = Mathf.Lerp(warm[i], warmTarget[i], k);
            }

            // the light breathes (+-4% over ~6.5 s), never pulses
            float breath = 1f + 0.04f * Mathf.Sin(clock * 0.97f) + 0.015f * Mathf.Sin(clock * 2.3f + 1.1f);
            float lightA = BlackjackFx.LightAlpha(0.26f) * light * breath * (1f + win * 0.9f);
            Alpha(centreLight, lightA);
            centreLight.transform.localScale = BaseScale(centreLight) * (1f + win * 0.18f);
            Alpha(playerWarm, BlackjackFx.LightAlpha(0.10f) * light);
            Alpha(dealerCool, BlackjackFx.LightAlpha(0.06f) * Mathf.Lerp(1f, 0.6f, win));
            Alpha(topShade, BlackjackFx.DarkAlpha(0.32f));
            // a slow burgundy drift across the upper table (a period of ~40 s)
            UiLayout layout = UiLayout.Active;
            float dx = Mathf.Sin(clock * 0.15f) * layout.HalfWidth * 0.45f;
            burgundyDrift.transform.localPosition = new Vector3(dx, layout.OrthoSize * 0.55f, 0f);
            burgundyDrift.transform.localScale = new Vector3(layout.HalfWidth * 1.6f, layout.OrthoSize * 1.1f, 1f);
            Alpha(burgundyDrift, 0.22f + 0.25f * burgundy);

            // the edges: burgundy-charcoal, tightening with the stake and the all-in
            float edge = 0.78f + 0.12f * tension + 0.10f * allIn + 0.22f * burgundy - 0.25f * win;
            vignette.color = new Color(VignetteInk.r + burgundy * 0.05f, VignetteInk.g, VignetteInk.b + burgundy * 0.01f,
                Mathf.Clamp01(edge));
            vignette.transform.localScale = BaseScale(vignette) * (1f - 0.04f * tension - 0.03f * allIn);
            // the all-in darkens the table a few percent
            felt.color = Color.Lerp(Color.white, new Color(0.9f, 0.88f, 0.9f), allIn * 0.6f + burgundy * 0.4f);

            Alpha(arcLow, 0.13f * light);
            Alpha(arcHigh, 0.08f * light);
            Alpha(seam, 0.10f + 0.05f * Mathf.Sin(clock * 0.8f));
            Alpha(seamMark, L.WagerBetween ? 0.18f : 0.26f);

            for (int i = 0; i < 2; i++)
            {
                Alpha(stageShadow[i], BlackjackFx.DarkAlpha(0.42f));
                float lift = active[i] * (0.30f + 0.05f * Mathf.Sin(clock * 2.1f)) + warm[i] * 0.45f;
                edgeGlow[i].color = new Color(BlackjackShapes.Amber.r, BlackjackShapes.Amber.g * (1f - 0.1f * warm[i]),
                    BlackjackShapes.Amber.b, BlackjackFx.LightAlpha(lift));
                // the side not playing sits back a touch; a finished side, a dark one, more
                float d = Mathf.Max(dim[i], (1f - active[i]) * 0.14f * (active[0] + active[1]));
                Color ink = Color.Lerp(VeilInk, GreyInk, grey[i]);
                float a = BlackjackFx.DarkAlpha(Mathf.Clamp01(d * 0.6f + grey[i] * 0.55f + wipe * 0.85f));
                veil[i].color = new Color(ink.r, ink.g, ink.b, a);
            }
            TickMotes(dt);
        }

        // ---- motes: four to eight warm specks drifting in the margins, never over a board or the HUD

        private readonly List<Vector4> moteState = new List<Vector4>();
        private int moteSeed;

        private void ResetMote(int i, bool scatter)
        {
            while (moteState.Count <= i)
            {
                moteState.Add(Vector4.zero);
            }
            UiLayout layout = UiLayout.Active;
            moteSeed++;
            Vector2 at = Vector2.zero;
            for (int tries = 0; tries < 12; tries++)
            {
                float u = BlackjackFx.Hash01(moteSeed * 13 + tries, i * 7 + 1);
                float v = BlackjackFx.Hash01(moteSeed * 29 + tries, i * 11 + 3);
                at = new Vector2((u * 2f - 1f) * layout.HalfWidth * 0.95f, (v * 2f - 1f) * layout.OrthoSize * 0.9f);
                if (!InsideBoards(at, 0.4f) && !InsideHud(at))
                {
                    break;
                }
            }
            float life = 5f + 7f * BlackjackFx.Hash01(moteSeed, i);
            float born = scatter ? clock - life * BlackjackFx.Hash01(i, moteSeed + 5) : clock;
            moteState[i] = new Vector4(at.x, at.y, born, life);
            float size = 0.05f + 0.04f * BlackjackFx.Hash01(i * 3, moteSeed);
            motes[i].transform.localScale = new Vector3(size, size, 1f);
        }

        private void TickMotes(float dt)
        {
            int shown = Mathf.Clamp(4 + Mathf.RoundToInt(light * 3f + win * 2f), 4, motes.Count);
            for (int i = 0; i < motes.Count; i++)
            {
                if (i >= moteState.Count)
                {
                    ResetMote(i, true);
                }
                Vector4 m = moteState[i];
                float age = (clock - m.z) / Mathf.Max(0.1f, m.w);
                if (age >= 1f)
                {
                    ResetMote(i, false);
                    m = moteState[i];
                    age = 0f;
                }
                // 10-30 px over its whole life: a breath of air, not a particle system
                Vector2 drift = new Vector2(Mathf.Sin(m.z + age * 2.2f) * 0.12f, age * 0.22f);
                motes[i].transform.localPosition = new Vector2(m.x, m.y) + drift;
                float a = Mathf.Sin(Mathf.PI * age) * 0.16f * (i < shown ? 1f : 0f) * (0.6f + 0.4f * light);
                Alpha(motes[i], a);
            }
        }

        private bool InsideBoards(Vector2 p, float margin)
        {
            float h = L.BoardSize * 0.5f + margin;
            return (Mathf.Abs(p.x - L.Player.x) < h && Mathf.Abs(p.y - L.Player.y) < h)
                || (Mathf.Abs(p.x - L.Dealer.x) < h && Mathf.Abs(p.y - L.Dealer.y) < h);
        }

        private bool InsideHud(Vector2 p)
        {
            return Mathf.Abs(p.x) < L.LedgerHalfWidth + 1.4f && p.y > L.BoardTop - 0.1f;
        }

        // ================================================================== small helpers

        private SpriteRenderer Sprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        private readonly Dictionary<SpriteRenderer, Vector3> baseScales = new Dictionary<SpriteRenderer, Vector3>();

        /// <summary>Sizes a sprite to a WORLD size whatever its texture's own aspect, and remembers
        /// that size so a per-frame scale is always written from it, never from itself.</summary>
        private void Size(SpriteRenderer r, Vector2 at, Vector2 size)
        {
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Vector2 own = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            var scale = new Vector3(size.x / Mathf.Max(0.0001f, own.x), size.y / Mathf.Max(0.0001f, own.y), 1f);
            r.transform.localScale = scale;
            baseScales[r] = scale;
        }

        private Vector3 BaseScale(SpriteRenderer r)
        {
            Vector3 s;
            return baseScales.TryGetValue(r, out s) ? s : r.transform.localScale;
        }

        private static void Alpha(SpriteRenderer r, float a)
        {
            if (r == null)
            {
                return;
            }
            Color c = r.color;
            c.a = Mathf.Clamp01(a);
            r.color = c;
            r.enabled = c.a > 0.002f;
        }

        /// <summary>A world-space text, optionally bold, with the game's outline.</summary>
        private TextMesh Text(string name, float height, Color color, int order, TextAnchor anchor, bool bold)
        {
            TextMesh tm = ViewUtil.MakeText3D(transform, name, Vector2.zero, "0", 90, height / 9f, color, order, anchor);
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left
                : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight
                    ? TextAlignment.Right
                    : TextAlignment.Center;
            if (bold && ViewUtil.UiFontBold != null)
            {
                Font f = ViewUtil.UiFontBold;
                foreach (TextMesh each in tm.GetComponentsInChildren<TextMesh>(true))
                {
                    each.font = f;
                    each.alignment = tm.alignment;
                    each.GetComponent<MeshRenderer>().material = f.material;
                }
            }
            tm.text = string.Empty;
            return tm;
        }

        private static void TextAlpha(TextMesh tm, Color ink, float a)
        {
            if (tm == null)
            {
                return;
            }
            ViewUtil.SetTextColor(tm, new Color(ink.r, ink.g, ink.b, ink.a * Mathf.Clamp01(a)));
            tm.gameObject.SetActive(a > 0.002f);
        }
    }
}
