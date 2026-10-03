// PURPOSE: BlackjackTableView's HUD - the BANK and its TARGET over the table, the hand's line, the
// SCORE DUEL between the boards, whose side is whose and whose turn it is, the result banner and
// the table's own message line.
//
// TWO SCALES, NEVER ONE. The bank is money and is set in the hero type at the top, with a thin
// LEDGER under it running from nothing to the target - the stage's start marked on it, a lozenge
// where the bank stands, the target's rings at its end. It is a ledger, not a progress bar: no
// fill colour, no percent. The hand's scores are points, set lower and lighter between the two
// arenas, each in its own side's ink (the player's warm cream-gold, the house's cooler cream), and
// they count with a glassy tick where the bank counts with a chip. A line's energy flies to a
// SCORE; nothing on this table ever flies from a board to the bank.
//
// Every number shown here is set from outside (Core's, through the controller); the HUD animates
// toward it and never works one out.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView
    {
        private TextMesh bankCaption;
        private TextMesh bankNumber;
        private SpriteRenderer ledgerLine;
        private SpriteRenderer ledgerFill;
        private SpriteRenderer ledgerStart;
        private SpriteRenderer ledgerEnd;
        private SpriteRenderer ledgerMarker;
        private SpriteRenderer targetIcon;
        private TextMesh targetText;
        private TextMesh doubleTag;
        private TextMesh miniLine;
        private TextMesh playerScore;
        private TextMesh dealerScore;
        private TextMesh vsText;
        private TextMesh diffText;
        private SpriteRenderer diffArrow;
        private SpriteRenderer vsSpark;
        private SpriteRenderer leadRipple;
        private TextMesh playerLabel;
        private TextMesh dealerLabel;
        private SpriteRenderer playerUnderline;
        private SpriteRenderer dealerUnderline;
        private TextMesh playerDone;
        private TextMesh dealerDone;
        private SpriteRenderer bannerBand;
        private SpriteRenderer bannerLineTop;
        private SpriteRenderer bannerLineBottom;
        private SpriteRenderer bannerGlint;
        private TextMesh bannerText;
        private TextMesh bannerSub;
        private TextMesh messageText;

        // ---- what the HUD is SHOWING, and where it is counting to
        private double shownBank;
        private long bankFrom, bankTo;
        private float bankT0 = -1f, bankDur;
        private float bankTickNext;
        private long target = 1;
        private long startPurse;
        private bool hudVisible = true;
        private bool scoresVisible;
        private float hudAlpha, hudAlphaTarget;

        private double shownPlayerScore, shownDealerScore;
        private int playerFrom, playerTo, dealerFrom, dealerTo;
        private float playerT0 = -1f, dealerT0 = -1f, playerDur, dealerDur;
        private float scoreTickNext;
        private int lead;
        private float leadT0 = -10f;
        private Side leadSide;
        private float playerPulseT0 = -10f, dealerPulseT0 = -10f, bankPulseT0 = -10f, targetPulseT0 = -10f;
        private float bankPulsePeak = 1.06f;
        private float scoresFold = 1f, scoresFoldTarget = 1f;

        private Side activeLabel;
        private float playerUnderlineA, dealerUnderlineA;
        private bool playerDoneOn, dealerDoneOn;
        private float playerDoneA, dealerDoneA;

        private float bannerT0 = -10f, bannerHold;
        private Color bannerInk = BlackjackShapes.Cream;
        private bool bannerUp;

        private static readonly Color BankInk = new Color(0.98f, 0.90f, 0.70f);
        private static readonly Color CaptionInk = new Color(0.78f, 0.66f, 0.42f, 0.9f);
        private static readonly Color MiniInk = new Color(0.82f, 0.78f, 0.68f, 0.75f);
        private static readonly Color VsInk = new Color(0.80f, 0.66f, 0.38f, 0.85f);
        private static readonly Color LabelInk = new Color(0.86f, 0.84f, 0.78f, 0.9f);
        private static readonly Color DoneInk = new Color(0.70f, 0.68f, 0.62f, 0.85f);
        private static readonly Color MessageInk = new Color(0.90f, 0.86f, 0.76f, 0.85f);
        public static readonly Color WinInk = new Color(1.00f, 0.84f, 0.50f);
        public static readonly Color LossInk = new Color(0.78f, 0.34f, 0.36f);
        public static readonly Color TieInk = new Color(0.86f, 0.86f, 0.84f);

        private void BuildHud()
        {
            bankCaption = Text("BankCaption", 0.2f, CaptionInk, HudOrder, TextAnchor.MiddleRight, false);
            bankNumber = Text("BankNumber", 0.5f, BankInk, HudOrder + 1, TextAnchor.MiddleLeft, true);
            ledgerLine = Sprite("LedgerLine", ViewUtil.WhiteSprite, BlackjackShapes.GoldDim, HudOrder - 2);
            ledgerFill = Sprite("LedgerFill", ViewUtil.WhiteSprite, BlackjackShapes.Gold, HudOrder - 1);
            ledgerStart = Sprite("LedgerStart", ViewUtil.WhiteSprite, BlackjackShapes.GoldDim, HudOrder - 1);
            ledgerEnd = Sprite("LedgerEnd", ViewUtil.WhiteSprite, BlackjackShapes.Gold, HudOrder - 1);
            ledgerMarker = Sprite("LedgerMarker", BlackjackShapes.TokenSprite, BlackjackShapes.Cream, HudOrder);
            targetIcon = Sprite("TargetIcon", BlackjackShapes.TargetSprite, BlackjackShapes.Gold, HudOrder);
            targetText = Text("TargetText", 0.22f, BlackjackShapes.Gold, HudOrder, TextAnchor.MiddleLeft, true);
            doubleTag = Text("DoubleTag", 0.15f, CaptionInk, HudOrder, TextAnchor.LowerCenter, false);
            miniLine = Text("MiniLine", 0.18f, MiniInk, HudOrder, TextAnchor.MiddleCenter, false);
            playerScore = Text("PlayerScore", 0.42f, BlackjackShapes.PlayerInk, HudOrder + 1, TextAnchor.MiddleRight, true);
            dealerScore = Text("DealerScore", 0.42f, BlackjackShapes.DealerInk, HudOrder + 1, TextAnchor.MiddleLeft, true);
            vsText = Text("Vs", 0.18f, VsInk, HudOrder, TextAnchor.MiddleCenter, false);
            diffText = Text("Diff", 0.19f, MiniInk, HudOrder, TextAnchor.MiddleCenter, false);
            diffArrow = Sprite("DiffArrow", BlackjackShapes.TriangleSprite, BlackjackShapes.Gold, HudOrder);
            vsSpark = Sprite("VsSpark", BlackjackShapes.RadialSprite, BlackjackShapes.Amber, HudOrder - 1);
            leadRipple = Sprite("LeadRipple", BlackjackShapes.TargetSprite, BlackjackShapes.Gold, HudOrder - 1);
            playerLabel = Text("PlayerLabel", 0.24f, LabelInk, HudOrder, TextAnchor.MiddleCenter, true);
            dealerLabel = Text("DealerLabel", 0.24f, LabelInk, HudOrder, TextAnchor.MiddleCenter, true);
            playerUnderline = Sprite("PlayerUnderline", ViewUtil.WhiteSprite, BlackjackShapes.Gold, HudOrder);
            dealerUnderline = Sprite("DealerUnderline", ViewUtil.WhiteSprite, BlackjackShapes.Gold, HudOrder);
            playerDone = Text("PlayerDone", 0.17f, DoneInk, HudOrder, TextAnchor.MiddleCenter, false);
            dealerDone = Text("DealerDone", 0.17f, DoneInk, HudOrder, TextAnchor.MiddleCenter, false);
            bannerBand = Sprite("BannerBand", BlackjackShapes.RadialSprite, new Color(0.03f, 0.03f, 0.035f), BannerOrder);
            bannerLineTop = Sprite("BannerLineTop", ViewUtil.WhiteSprite, BlackjackShapes.Gold, BannerOrder + 1);
            bannerLineBottom = Sprite("BannerLineBottom", ViewUtil.WhiteSprite, BlackjackShapes.Gold, BannerOrder + 1);
            bannerGlint = Sprite("BannerGlint", BlackjackShapes.RadialSprite, BlackjackShapes.Cream, BannerOrder + 2);
            bannerText = Text("BannerText", 0.56f, BlackjackShapes.Cream, BannerOrder + 3, TextAnchor.MiddleCenter, true);
            bannerSub = Text("BannerSub", 0.22f, MiniInk, BannerOrder + 3, TextAnchor.MiddleCenter, false);
            messageText = Text("Message", 0.2f, MessageInk, HudOrder, TextAnchor.MiddleCenter, false);
            playerLabel.text = Loc.Pick("YOU", "SEN");
            dealerLabel.text = Loc.Pick("HOUSE", "KASA");
            bankCaption.text = Loc.Pick("BANK", "KASAN");
            vsText.text = "VS";
            doubleTag.text = "×2";
        }

        private void PlaceHud()
        {
            float t = L.Type;
            Scale(bankCaption, t);
            Scale(bankNumber, t);
            bankCaption.transform.localPosition = L.Bank + new Vector2(-0.06f * t, -0.03f * t);
            bankNumber.transform.localPosition = L.Bank + new Vector2(0.06f * t, 0f);
            float w = L.LedgerHalfWidth;
            Size(ledgerLine, L.Ledger, new Vector2(w * 2f, 0.018f * t));
            Size(ledgerEnd, L.Ledger + new Vector2(w, 0f), new Vector2(0.02f * t, 0.16f * t));
            Size(targetIcon, L.Ledger + new Vector2(w + 0.2f * t, 0f), new Vector2(0.26f * t, 0.26f * t));
            targetText.transform.localPosition = L.Ledger + new Vector2(w + 0.38f * t, 0f);
            Scale(targetText, t);
            Scale(doubleTag, t);
            Scale(miniLine, t);
            miniLine.transform.localPosition = L.MiniLine;
            Scale(playerScore, t);
            Scale(dealerScore, t);
            Scale(vsText, t);
            Scale(diffText, t);
            playerScore.transform.localPosition = L.PlayerScore;
            dealerScore.transform.localPosition = L.DealerScore;
            vsText.transform.localPosition = L.Vs;
            diffText.transform.localPosition = L.Diff;
            Size(vsSpark, L.Vs, new Vector2(0.9f * t, 0.9f * t));
            Size(leadRipple, L.Vs, new Vector2(0.6f * t, 0.6f * t));
            Scale(playerLabel, t);
            Scale(dealerLabel, t);
            playerLabel.transform.localPosition = L.PlayerLabel;
            dealerLabel.transform.localPosition = L.DealerLabel;
            Size(playerUnderline, L.PlayerLabel + new Vector2(0f, -0.17f * t), new Vector2(0.62f * t, 0.022f * t));
            Size(dealerUnderline, L.DealerLabel + new Vector2(0f, -0.17f * t), new Vector2(0.78f * t, 0.022f * t));
            Scale(playerDone, t);
            Scale(dealerDone, t);
            playerDone.transform.localPosition = L.Player + new Vector2(0f, -L.BoardSize * 0.5f - 0.2f * t);
            dealerDone.transform.localPosition = L.Dealer + new Vector2(0f, -L.BoardSize * 0.5f - 0.2f * t);
            Vector2 mid = new Vector2(0f, L.GroupCenter.y);
            float bw = Mathf.Min(UiLayout.Active.HalfWidth * 2f - 0.6f, 9.5f * t);
            Size(bannerBand, mid, new Vector2(bw, 1.5f * t));
            Size(bannerLineTop, mid + new Vector2(0f, 0.46f * t), new Vector2(bw * 0.55f, 0.02f * t));
            Size(bannerLineBottom, mid + new Vector2(0f, -0.46f * t), new Vector2(bw * 0.55f, 0.02f * t));
            Size(bannerGlint, mid + new Vector2(0f, 0.46f * t), new Vector2(0.9f * t, 0.12f * t));
            Scale(bannerText, t);
            Scale(bannerSub, t);
            bannerText.transform.localPosition = mid + new Vector2(0f, 0.08f * t);
            bannerSub.transform.localPosition = mid + new Vector2(0f, -0.3f * t);
            Scale(messageText, t);
            messageText.transform.localPosition = L.Message;
            PlaceLedgerMarks();
        }

        private void ResetHud()
        {
            bankT0 = -1f;
            playerT0 = dealerT0 = -1f;
            shownPlayerScore = shownDealerScore = 0;
            playerFrom = playerTo = dealerFrom = dealerTo = 0;
            lead = 0;
            scoresVisible = false;
            scoresFold = scoresFoldTarget = 1f;
            activeLabel = Side.None;
            playerDoneOn = dealerDoneOn = false;
            bannerUp = false;
            bannerT0 = -10f;
            messageText.text = string.Empty;
            miniLine.text = string.Empty;
        }

        private static void Scale(TextMesh tm, float t)
        {
            tm.transform.localScale = new Vector3(t, t, 1f);
        }

        // ================================================================== the bank and its target

        /// <summary>The bank as it is SHOWN (it may still be counting to Core's number).</summary>
        public long ShownBank
        {
            get { return (long)System.Math.Round(shownBank); }
        }

        /// <summary>Sets the bank. Animated, it counts from what is shown with the bank's deeper tick.</summary>
        public void SetBank(long value, bool animate, float seconds = -1f)
        {
            if (!animate)
            {
                shownBank = value;
                bankFrom = bankTo = value;
                bankT0 = -1f;
                PlaceLedgerMarks();
                return;
            }
            bankFrom = ShownBank;
            bankTo = value;
            bankT0 = Time.time;
            bankDur = seconds > 0f ? seconds : T(BlackjackFx.Tuning.betBankCountDuration);
            bankTickNext = 0f;
        }

        /// <summary>The stage's target and where its bank started (the ledger's half-way mark).</summary>
        public void SetTarget(long targetValue, long start)
        {
            target = System.Math.Max(1, targetValue);
            startPurse = start;
            targetText.text = Loc.Pick("TARGET ", "HEDEF ") + target;
            PlaceLedgerMarks();
        }

        /// <summary>"HAND 1 · BET 200", or nothing between hands.</summary>
        public void SetHandLine(int hand, long bet)
        {
            miniLine.text = hand <= 0 ? string.Empty
                : Loc.Pick("HAND ", "EL ") + hand + (bet > 0 ? "  ·  " + Loc.Pick("BET ", "BAHİS ") + bet : string.Empty);
        }

        /// <summary>The bank's own answer to money arriving or leaving (1 -> peak -> settle).</summary>
        public void PulseBank(float peak)
        {
            bankPulseT0 = Time.time;
            bankPulsePeak = peak;
        }

        public void PulseTarget()
        {
            targetPulseT0 = Time.time;
        }

        public void SetHudVisible(bool visible)
        {
            hudVisible = visible;
        }

        private void PlaceLedgerMarks()
        {
            if (ledgerLine == null)
            {
                return;
            }
            float t = L.Type;
            float w = L.LedgerHalfWidth;
            float startFrac = Mathf.Clamp01(startPurse / (float)target);
            Size(ledgerStart, L.Ledger + new Vector2(-w + 2f * w * startFrac, 0f), new Vector2(0.016f * t, 0.12f * t));
            doubleTag.transform.localPosition = L.Ledger + new Vector2(-w + 2f * w * (startFrac + 1f) * 0.5f, 0.05f * t);
        }

        // ================================================================== the score duel

        /// <summary>Shows or hides the score duel (it is folded away between hands).</summary>
        public void SetScoresVisible(bool visible)
        {
            scoresVisible = visible;
            scoresFoldTarget = 1f;
        }

        /// <summary>Sets both hand scores. Animated, each counts with the score's glassy tick and
        /// pulses; the lead is read off the SHOWN values, so a lead change lands when it is seen.</summary>
        public void SetScores(int player, int dealer, bool animate)
        {
            if (!animate)
            {
                shownPlayerScore = playerFrom = playerTo = player;
                shownDealerScore = dealerFrom = dealerTo = dealer;
                playerT0 = dealerT0 = -1f;
                lead = System.Math.Sign(player - dealer);
                return;
            }
            SetScore(Side.Player, player);
            SetScore(Side.Dealer, dealer);
        }

        /// <summary>Counts one side's score to a new value.</summary>
        public void SetScore(Side side, int value)
        {
            float dur = T(BlackjackFx.Tuning.scoreCountDuration);
            if (side == Side.Player)
            {
                if (value == playerTo)
                {
                    return;
                }
                playerFrom = (int)System.Math.Round(shownPlayerScore);
                playerTo = value;
                playerT0 = Time.time;
                playerDur = dur;
                playerPulseT0 = Time.time;
            }
            else
            {
                if (value == dealerTo)
                {
                    return;
                }
                dealerFrom = (int)System.Math.Round(shownDealerScore);
                dealerTo = value;
                dealerT0 = Time.time;
                dealerDur = dur;
                dealerPulseT0 = Time.time;
            }
        }

        /// <summary>Folds the scores down to nothing and back up at zero (the next hand).</summary>
        public void FoldScores()
        {
            scoresFoldTarget = 0f;
        }

        /// <summary>Pulses both scores (the hand's end).</summary>
        public void PulseScores()
        {
            playerPulseT0 = dealerPulseT0 = Time.time;
        }

        /// <summary>Where a side's score number is (an essence flies here; a joker's points too).</summary>
        public Vector2 ScoreAnchor(Side side)
        {
            float t = L.Type;
            return side == Side.Player ? L.PlayerScore + new Vector2(-0.45f * t, 0f) : L.DealerScore + new Vector2(0.45f * t, 0f);
        }

        /// <summary>
        /// A line's energy carried from the board to its side's SCORE (never to the bank): a small
        /// glassy mote in that side's ink, then the number counts. The count waits for it.
        /// </summary>
        public void SendScore(Side side, Vector2 from, int value)
        {
            if (!isActiveAndEnabled)
            {
                SetScore(side, value);
                return;
            }
            StartCoroutine(SendScoreRoutine(side, from, value));
        }

        private System.Collections.IEnumerator SendScoreRoutine(Side side, Vector2 from, int value)
        {
            SpriteRenderer mote = RentToken();
            mote.sprite = BlackjackShapes.RadialSprite;
            Color ink = side == Side.Player ? BlackjackShapes.PlayerInk : BlackjackShapes.DealerInk;
            Vector2 to = ScoreAnchor(side);
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(0f, 0.6f * L.Type);
            float dur = T(0.32f);
            float t0 = Time.time;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / dur);
                float e = BlackjackFx.EaseIn(k);
                mote.transform.localPosition = BlackjackFx.Bezier(from, ctrl, to, e);
                float s = 0.22f * L.Type * (1f - 0.5f * k);
                mote.transform.localScale = new Vector3(s, s, 1f);
                mote.color = new Color(ink.r, ink.g, ink.b, BlackjackFx.LightAlpha(1.4f) * Mathf.Min(1f, k * 5f));
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
            mote.enabled = false;
            mote.sprite = BlackjackShapes.TokenSprite;
            SetScore(side, value);
        }

        public Vector2 BankAnchor
        {
            get { return L.Bank + new Vector2(0.5f * L.Type, 0f); }
        }

        public int ShownPlayerScore
        {
            get { return (int)System.Math.Round(shownPlayerScore); }
        }

        public int ShownDealerScore
        {
            get { return (int)System.Math.Round(shownDealerScore); }
        }

        // ================================================================== labels, the banner, the message

        private void SetActiveLabel(Side side)
        {
            activeLabel = side;
        }

        private void SetDoneTag(Side side, bool done)
        {
            if (side == Side.Player)
            {
                playerDoneOn = done;
                playerDone.text = Loc.Pick("DONE", "BİTTİ");
            }
            else
            {
                dealerDoneOn = done;
                dealerDone.text = Loc.Pick("NO MOVES", "HAMLE YOK");
            }
        }

        /// <summary>The result's own line, centred over the table: a title and a smaller line under
        /// it, in the result's ink. It holds, then goes; it is never a giant word.</summary>
        public void ShowBanner(string title, string sub, Color ink, float hold)
        {
            bannerText.text = title;
            bannerSub.text = sub ?? string.Empty;
            bannerInk = ink;
            bannerT0 = Time.time;
            bannerHold = hold;
            bannerUp = true;
        }

        public void HideBanner()
        {
            if (bannerUp)
            {
                bannerHold = Mathf.Min(bannerHold, Time.time - bannerT0 - T(0.28f));
            }
        }

        public void SetMessage(string text)
        {
            messageText.text = text ?? string.Empty;
        }

        // ================================================================== the frame

        private void TickHud(float dt)
        {
            hudAlphaTarget = hudVisible ? 1f : 0f;
            hudAlpha = Mathf.MoveTowards(hudAlpha, hudAlphaTarget, dt * 4f);
            float t = L.Type;
            float now = Time.time;

            // ---- the bank counts with a chip's tick
            if (bankT0 >= 0f)
            {
                float k = Mathf.Clamp01((now - bankT0) / Mathf.Max(0.01f, bankDur));
                shownBank = bankFrom + (bankTo - bankFrom) * BlackjackFx.EaseInOut(k);
                if (now >= bankTickNext && k < 1f && bankFrom != bankTo)
                {
                    bankTickNext = now + 0.065f;
                    if (Sfx != null)
                    {
                        Sfx.Casino(BlackjackCue.BankTick, bankTo > bankFrom ? 1.05f : 0.92f, 0.8f);
                    }
                }
                if (k >= 1f)
                {
                    bankT0 = -1f;
                    shownBank = bankTo;
                }
            }
            long bank = ShownBank;
            bankNumber.text = bank.ToString();
            float frac = Mathf.Clamp01((float)(shownBank / target));
            // low bank: a slow burgundy pressure in the number; near the target: the rings breathe
            bool low = startPurse > 0 && shownBank < startPurse * 0.25;
            Color bankInk = low ? Color.Lerp(BankInk, BlackjackShapes.BurgundyLight, 0.35f + 0.25f * Mathf.Sin(clock * 2.2f)) : BankInk;
            float bp = BlackjackFx.Punch((now - bankPulseT0) / T(0.32f), 1f, bankPulsePeak, 0.985f);
            bankNumber.transform.localScale = new Vector3(t * bp, t * bp, 1f);
            TextAlpha(bankNumber, bankInk, hudAlpha);
            TextAlpha(bankCaption, CaptionInk, hudAlpha);

            float w = L.LedgerHalfWidth;
            Alpha(ledgerLine, 0.5f * hudAlpha);
            Size(ledgerFill, L.Ledger + new Vector2(-w + w * frac, 0f), new Vector2(Mathf.Max(0.001f, 2f * w * frac), 0.026f * t));
            Alpha(ledgerFill, 0.7f * hudAlpha);
            Alpha(ledgerStart, 0.7f * hudAlpha);
            Alpha(ledgerEnd, 0.9f * hudAlpha);
            ledgerMarker.transform.localPosition = L.Ledger + new Vector2(-w + 2f * w * frac, 0f);
            ledgerMarker.transform.localScale = new Vector3(0.16f * t, 0.16f * t, 1f);
            Alpha(ledgerMarker, hudAlpha);
            bool near = frac >= 0.8f;
            float tp = BlackjackFx.Punch((now - targetPulseT0) / T(0.5f), 1f, 1.35f, 0.95f);
            float breathe = near ? 1f + 0.06f * Mathf.Sin(clock * 3.1f) : 1f;
            targetIcon.transform.localScale = BaseScale(targetIcon) * tp * breathe;
            Alpha(targetIcon, (near ? 0.95f : 0.7f) * hudAlpha);
            TextAlpha(targetText, BlackjackShapes.Gold, hudAlpha * (near ? 1f : 0.85f));
            TextAlpha(doubleTag, CaptionInk, hudAlpha * 0.8f);
            TextAlpha(miniLine, MiniInk, hudAlpha * (string.IsNullOrEmpty(miniLine.text) ? 0f : 1f));

            // ---- the score duel counts with the score's glassy tick
            TickScoreCount(ref shownPlayerScore, playerFrom, playerTo, ref playerT0, playerDur, now);
            TickScoreCount(ref shownDealerScore, dealerFrom, dealerTo, ref dealerT0, dealerDur, now);
            if (now >= scoreTickNext && (playerT0 >= 0f || dealerT0 >= 0f))
            {
                scoreTickNext = now + 0.055f;
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.ScoreTick, playerT0 >= 0f ? 1.04f : 0.94f, 0.75f);
                }
            }
            int p = ShownPlayerScore;
            int d = ShownDealerScore;
            int newLead = System.Math.Sign(p - d);
            if (newLead != lead && newLead != 0 && scoresVisible)
            {
                leadT0 = now;
                leadSide = newLead > 0 ? Side.Player : Side.Dealer;
                if (leadSide == Side.Player)
                {
                    playerPulseT0 = now;
                }
                else
                {
                    dealerPulseT0 = now;
                }
                if (Sfx != null && lead != 0)
                {
                    Sfx.Casino(BlackjackCue.LeadChange, leadSide == Side.Player ? 1.06f : 0.94f);
                }
            }
            if (newLead != 0 || p == 0)
            {
                lead = newLead;
            }
            scoresFold = Mathf.MoveTowards(scoresFold, scoresFoldTarget, dt / Mathf.Max(0.05f, T(0.18f)));
            if (scoresFoldTarget <= 0f && scoresFold <= 0f)
            {
                SetScores(0, 0, false);
                scoresFoldTarget = 1f;
            }
            float sa = (scoresVisible ? 1f : 0f) * hudAlpha;
            float leadPunch = BlackjackFx.Punch((now - leadT0) / T(0.4f), 1f, BlackjackFx.Tuning.leadChangePulse, 0.99f);
            float pp = BlackjackFx.Punch((now - playerPulseT0) / T(0.3f), 1f, BlackjackFx.Tuning.scorePulseScale, 0.99f);
            float dp = BlackjackFx.Punch((now - dealerPulseT0) / T(0.3f), 1f, BlackjackFx.Tuning.scorePulseScale, 0.99f);
            if (leadSide == Side.Player) pp = Mathf.Max(pp, leadPunch); else dp = Mathf.Max(dp, leadPunch);
            playerScore.text = p.ToString();
            dealerScore.text = d.ToString();
            playerScore.transform.localScale = new Vector3(t * pp, t * pp * scoresFold, 1f);
            dealerScore.transform.localScale = new Vector3(t * dp, t * dp * scoresFold, 1f);
            // the leader's number is the brighter one, never by much
            TextAlpha(playerScore, BlackjackShapes.PlayerInk, sa * (lead < 0 ? 0.78f : 1f));
            TextAlpha(dealerScore, BlackjackShapes.DealerInk, sa * (lead > 0 ? 0.78f : 1f));
            TextAlpha(vsText, VsInk, sa);
            int gap = System.Math.Abs(p - d);
            diffText.text = gap == 0 ? (p == 0 ? string.Empty : "=") : "+" + gap;
            TextAlpha(diffText, lead > 0 ? BlackjackShapes.PlayerInk : lead < 0 ? BlackjackShapes.DealerInk : MiniInk, sa * 0.85f);
            float diffW = DiffHalfWidth(diffText.text) * t;
            if (gap > 0)
            {
                // the arrow points at the leader
                float dir = lead > 0 ? -1f : 1f;
                diffArrow.transform.localPosition = L.Diff + new Vector2(dir * (diffW + 0.1f * t), 0f);
                diffArrow.transform.localRotation = Quaternion.Euler(0f, 0f, lead > 0 ? 180f : 0f);
                diffArrow.transform.localScale = new Vector3(0.13f * t, 0.13f * t, 1f);
                diffArrow.color = lead > 0 ? BlackjackShapes.PlayerInk : BlackjackShapes.DealerInk;
                Alpha(diffArrow, sa * 0.8f);
            }
            else
            {
                Alpha(diffArrow, 0f);
            }
            float sparkK = (now - leadT0) / T(0.45f);
            Alpha(vsSpark, sparkK < 1f ? BlackjackFx.LightAlpha(0.5f) * (1f - sparkK) * sa : 0f);
            float rippleK = (now - leadT0) / T(0.6f);
            leadRipple.transform.localScale = BaseScale(leadRipple) * (1f + 2.2f * BlackjackFx.EaseOut(rippleK));
            Alpha(leadRipple, rippleK < 1f ? 0.35f * (1f - rippleK) * sa : 0f);

            // ---- whose side is whose, and whose turn
            playerUnderlineA = Mathf.MoveTowards(playerUnderlineA, activeLabel == Side.Player ? 1f : 0f, dt * 5f);
            dealerUnderlineA = Mathf.MoveTowards(dealerUnderlineA, activeLabel == Side.Dealer ? 1f : 0f, dt * 5f);
            TextAlpha(playerLabel, Color.Lerp(LabelInk, BlackjackShapes.PlayerInk, playerUnderlineA), hudAlpha * (0.72f + 0.28f * playerUnderlineA));
            TextAlpha(dealerLabel, Color.Lerp(LabelInk, BlackjackShapes.DealerInk, dealerUnderlineA), hudAlpha * (0.72f + 0.28f * dealerUnderlineA));
            playerUnderline.transform.localScale = new Vector3(BaseScale(playerUnderline).x * (0.3f + 0.7f * playerUnderlineA), BaseScale(playerUnderline).y, 1f);
            dealerUnderline.transform.localScale = new Vector3(BaseScale(dealerUnderline).x * (0.3f + 0.7f * dealerUnderlineA), BaseScale(dealerUnderline).y, 1f);
            Alpha(playerUnderline, playerUnderlineA * 0.85f * hudAlpha);
            Alpha(dealerUnderline, dealerUnderlineA * 0.85f * hudAlpha);
            playerDoneA = Mathf.MoveTowards(playerDoneA, playerDoneOn ? 1f : 0f, dt * 4f);
            dealerDoneA = Mathf.MoveTowards(dealerDoneA, dealerDoneOn ? 1f : 0f, dt * 4f);
            TextAlpha(playerDone, DoneInk, playerDoneA * hudAlpha);
            TextAlpha(dealerDone, DoneInk, dealerDoneA * hudAlpha);

            TickBanner(now, t);
            TextAlpha(messageText, MessageInk, hudAlpha * (string.IsNullOrEmpty(messageText.text) ? 0f : 1f));
        }

        private static void TickScoreCount(ref double shown, int from, int to, ref float t0, float dur, float now)
        {
            if (t0 < 0f)
            {
                return;
            }
            float k = Mathf.Clamp01((now - t0) / Mathf.Max(0.01f, dur));
            shown = from + (to - from) * BlackjackFx.EaseOut(k);
            if (k >= 1f)
            {
                shown = to;
                t0 = -1f;
            }
        }

        private static float DiffHalfWidth(string s)
        {
            return (string.IsNullOrEmpty(s) ? 0 : s.Length) * 0.06f + 0.02f;
        }

        private void TickBanner(float now, float t)
        {
            if (!bannerUp)
            {
                Alpha(bannerBand, 0f);
                Alpha(bannerLineTop, 0f);
                Alpha(bannerLineBottom, 0f);
                Alpha(bannerGlint, 0f);
                TextAlpha(bannerText, bannerInk, 0f);
                TextAlpha(bannerSub, MiniInk, 0f);
                return;
            }
            float age = now - bannerT0;
            float inDur = T(BlackjackFx.Tuning.handResultIntro);
            float outDur = T(0.3f);
            float a;
            float scale;
            if (age < inDur)
            {
                float k = age / inDur;
                a = BlackjackFx.EaseOut(k);
                scale = k < 0.6f ? Mathf.Lerp(0.94f, 1.02f, BlackjackFx.EaseOut(k / 0.6f)) : Mathf.Lerp(1.02f, 1f, (k - 0.6f) / 0.4f);
            }
            else if (age < inDur + bannerHold)
            {
                a = 1f;
                scale = 1f;
            }
            else
            {
                float k = (age - inDur - bannerHold) / outDur;
                a = 1f - BlackjackFx.EaseIn(k);
                scale = 1f - 0.03f * k;
                if (k >= 1f)
                {
                    bannerUp = false;
                    a = 0f;
                }
            }
            bannerText.transform.localScale = new Vector3(t * scale, t * scale, 1f);
            TextAlpha(bannerText, bannerInk, a);
            TextAlpha(bannerSub, MiniInk, a * 0.95f);
            Alpha(bannerBand, BlackjackFx.DarkAlpha(0.55f) * a);
            float lineK = BlackjackFx.EaseOut(Mathf.Clamp01(age / inDur));
            Vector3 ls = BaseScale(bannerLineTop);
            bannerLineTop.transform.localScale = new Vector3(ls.x * lineK, ls.y, 1f);
            bannerLineBottom.transform.localScale = new Vector3(ls.x * lineK, ls.y, 1f);
            bannerLineTop.color = new Color(bannerInk.r, bannerInk.g, bannerInk.b, 0.7f * a);
            bannerLineBottom.color = new Color(bannerInk.r, bannerInk.g, bannerInk.b, 0.7f * a);
            // one glint runs the top line once (the gold line sweep)
            float sweep = Mathf.Clamp01(age / T(0.6f));
            float half = BaseScale(bannerLineTop).x * bannerLineTop.sprite.bounds.size.x * 0.5f;
            bannerGlint.transform.localPosition = new Vector2(Mathf.Lerp(-half, half, sweep), bannerLineTop.transform.localPosition.y);
            Alpha(bannerGlint, sweep < 1f ? BlackjackFx.LightAlpha(0.8f) * Mathf.Sin(Mathf.PI * sweep) * a : 0f);
        }
    }
}
