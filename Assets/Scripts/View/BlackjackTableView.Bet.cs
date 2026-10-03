// PURPOSE: BlackjackTableView's BET TRAY - where the stake is chosen before every hand.
//
// A TYPED NUMBER FIRST. The amount is a field the player types into (digits only, any whole
// number up to the bank; an empty field is allowed while typing and simply cannot be confirmed),
// and the chips under it are SHORTCUTS into that same field: a tenth, a quarter, a half, three
// quarters and ALL-IN - the stakes Core offers (BlackjackBoss.BetOptions), never a figure worked
// out here. Typing past the bank clamps to the bank with a small refusal, never an error box.
//
// The stake is felt before it is placed: the heavier it is against the bank, the warmer the tray's
// glow and the tighter the table's edges (SetBetTension); ALL-IN is its own state - a deep burgundy
// chip with a gold edge, the table a few per cent darker, a deep thud and a stack of chips, the
// music ducking for a breath. Confirming (Enter, the button, or the pad's A on it) hands the amount
// to the controller, which places it through Core and then plays the money onto the table.
//
// Every item has a world centre so the pad steps it like any other panel (.PadPanels).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView
    {
        private Transform trayRoot;
        private SpriteRenderer trayGlow;
        private SpriteRenderer trayRim;
        private SpriteRenderer trayBody;
        private TextMesh trayTitle;
        private TextMesh trayBank;
        private TextMesh trayTarget;
        private SpriteRenderer fieldRim;
        private SpriteRenderer fieldBody;
        private TextMesh fieldText;
        private SpriteRenderer caret;
        private TextMesh trayNote;
        private TextMesh trayHint;
        private readonly List<SpriteRenderer> chipDiscs = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> chipRings = new List<SpriteRenderer>();
        private readonly List<TextMesh> chipLabels = new List<TextMesh>();
        private readonly List<TextMesh> chipAmounts = new List<TextMesh>();
        private SpriteRenderer confirmRim;
        private SpriteRenderer confirmBody;
        private TextMesh confirmText;
        private bool trayBuilt;
        private bool trayOpen;
        private float trayT0;
        private float trayA;
        private Vector2 trayCentre;
        private Vector2 traySize;

        private long trayBankValue;
        private readonly List<long> stakes = new List<long>();
        private string typed = string.Empty;
        private int hoverItem = -1;
        private int focusItem = -1;
        private float shakeT0 = -10f;
        private float noteT0 = -10f;
        private bool wasAllIn;
        private bool listening;
        private bool trayFolding;

        private static readonly Color AllInDisc = new Color(0.62f, 0.22f, 0.27f);

        public bool BetTrayOpen
        {
            get { return trayOpen; }
        }

        /// <summary>The amount in the field (0 when empty).</summary>
        public long TypedBet
        {
            get
            {
                long v;
                return long.TryParse(typed, out v) ? v : 0;
            }
        }

        /// <summary>Chips, then the confirm button.</summary>
        public int BetItemCount
        {
            get { return stakes.Count + 1; }
        }

        private void BuildTray()
        {
            if (trayBuilt)
            {
                return;
            }
            trayBuilt = true;
            trayRoot = new GameObject("BetTray").transform;
            trayRoot.SetParent(transform, false);
            trayGlow = ViewUtil.MakePlate(trayRoot, "Glow", Vector2.zero, Vector2.one, BlackjackShapes.Amber, TrayOrder, ViewUtil.GlowSprite);
            trayRim = ViewUtil.MakeRounded(trayRoot, "Rim", Vector2.zero, Vector2.one, BlackjackShapes.GoldDim, TrayOrder + 1);
            trayBody = ViewUtil.MakeRounded(trayRoot, "Body", Vector2.zero, Vector2.one, new Color(0.045f, 0.075f, 0.072f, 0.97f), TrayOrder + 2);
            trayTitle = TrayText("Title", 0.22f, CaptionInk, TextAnchor.MiddleCenter, false);
            trayBank = TrayText("Bank", 0.2f, BankInk, TextAnchor.MiddleLeft, true);
            trayTarget = TrayText("Target", 0.2f, BlackjackShapes.Gold, TextAnchor.MiddleRight, true);
            fieldRim = ViewUtil.MakeRounded(trayRoot, "FieldRim", Vector2.zero, Vector2.one, BlackjackShapes.Gold, TrayOrder + 3);
            fieldBody = ViewUtil.MakeRounded(trayRoot, "FieldBody", Vector2.zero, Vector2.one, new Color(0.03f, 0.03f, 0.035f), TrayOrder + 4);
            fieldText = TrayText("Field", 0.5f, BankInk, TextAnchor.MiddleCenter, true);
            caret = ViewUtil.MakeRect(trayRoot, "Caret", Vector2.zero, Vector2.one, BlackjackShapes.Gold, TrayOrder + 6);
            trayNote = TrayText("Note", 0.16f, LossInk, TextAnchor.MiddleCenter, false);
            trayHint = TrayText("Hint", 0.14f, MiniInk, TextAnchor.MiddleCenter, false);
            confirmRim = ViewUtil.MakeRounded(trayRoot, "ConfirmRim", Vector2.zero, Vector2.one, BlackjackShapes.Gold, TrayOrder + 3);
            confirmBody = ViewUtil.MakeRounded(trayRoot, "ConfirmBody", Vector2.zero, Vector2.one, new Color(0.62f, 0.48f, 0.24f), TrayOrder + 4);
            confirmText = TrayText("Confirm", 0.22f, new Color(0.1f, 0.06f, 0.04f), TextAnchor.MiddleCenter, true);
            trayRoot.gameObject.SetActive(false);
        }

        private TextMesh TrayText(string name, float height, Color ink, TextAnchor anchor, bool bold)
        {
            TextMesh tm = Text(name, height, ink, TrayOrder + 7, anchor, bold);
            tm.transform.SetParent(trayRoot, false);
            return tm;
        }

        private void PlaceBet()
        {
            if (!trayBuilt)
            {
                return;
            }
            float t = Mathf.Max(0.66f, L.Type);
            traySize = new Vector2(Mathf.Min(6.6f * t, UiLayout.Active.HalfWidth * 2f - 0.4f), 3.4f * t);
            trayCentre = new Vector2(0f, L.GroupCenter.y + 0.05f * t);
            trayRoot.localPosition = trayCentre;
            trayGlow.size = traySize + new Vector2(1.1f, 1.1f) * t;
            trayRim.size = traySize + new Vector2(0.05f, 0.05f) * t;
            trayBody.size = traySize;
            float top = traySize.y * 0.5f;
            float half = traySize.x * 0.5f;
            trayTitle.transform.localPosition = new Vector2(0f, top - 0.3f * t);
            trayBank.transform.localPosition = new Vector2(-half + 0.3f * t, top - 0.3f * t);
            trayTarget.transform.localPosition = new Vector2(half - 0.3f * t, top - 0.3f * t);
            Vector2 field = new Vector2(0f, top - 0.95f * t);
            fieldRim.transform.localPosition = field;
            fieldRim.size = new Vector2(2.6f, 0.72f) * t + new Vector2(0.05f, 0.05f) * t;
            fieldBody.transform.localPosition = field;
            fieldBody.size = new Vector2(2.6f, 0.72f) * t;
            fieldText.transform.localPosition = field;
            trayNote.transform.localPosition = field + new Vector2(0f, -0.52f * t);
            LayoutChips();
            Vector2 confirm = new Vector2(0f, -top + 0.42f * t);
            confirmRim.transform.localPosition = confirm;
            confirmRim.size = new Vector2(2.1f, 0.5f) * t + new Vector2(0.04f, 0.04f) * t;
            confirmBody.transform.localPosition = confirm;
            confirmBody.size = new Vector2(2.1f, 0.5f) * t;
            confirmText.transform.localPosition = confirm;
            trayHint.transform.localPosition = new Vector2(half - 1.25f * t, -top + 0.42f * t);
        }

        private void LayoutChips()
        {
            float t = Mathf.Max(0.66f, L.Type);
            float y = traySize.y * 0.5f - 2.0f * t;
            int n = stakes.Count;
            float span = Mathf.Min(traySize.x - 1.0f * t, 4.6f * t);
            for (int i = 0; i < n; i++)
            {
                float x = n == 1 ? 0f : -span * 0.5f + span * i / (n - 1);
                chipRings[i].transform.localPosition = new Vector2(x, y);
                chipRings[i].transform.localScale = new Vector3(0.74f * t, 0.74f * t, 1f);
                chipDiscs[i].transform.localPosition = new Vector2(x, y);
                chipDiscs[i].transform.localScale = new Vector3(0.62f * t, 0.62f * t, 1f);
                chipLabels[i].transform.localPosition = new Vector2(x, y + 0.01f * t);
                chipAmounts[i].transform.localPosition = new Vector2(x, y - 0.46f * t);
            }
        }

        // ================================================================== open / close

        /// <summary>Opens the tray for a bank, its target and Core's stakes (smallest first, the
        /// last one all of it). The field starts EMPTY - the player decides.</summary>
        public void ShowBetTray(long bank, long targetValue, IList<long> offered)
        {
            BuildTray();
            trayBankValue = bank;
            stakes.Clear();
            for (int i = 0; i < offered.Count; i++)
            {
                stakes.Add(offered[i]);
            }
            while (chipDiscs.Count < stakes.Count)
            {
                int i = chipDiscs.Count;
                chipRings.Add(ViewUtil.MakeIcon(trayRoot, "ChipRing" + i, Vector2.zero, 1f, BlackjackShapes.Amber, TrayOrder + 3, BlackjackShapes.RadialSprite));
                chipDiscs.Add(ViewUtil.MakeIcon(trayRoot, "Chip" + i, Vector2.zero, 1f, Color.white, TrayOrder + 4, BlackjackShapes.ChipSprite));
                chipLabels.Add(TrayText("ChipLabel" + i, 0.17f, BlackjackShapes.Cream, TextAnchor.MiddleCenter, true));
                chipAmounts.Add(TrayText("ChipAmount" + i, 0.15f, MiniInk, TextAnchor.MiddleCenter, false));
            }
            string[] shares = { "%10", "%25", "%50", "%75" };
            for (int i = 0; i < chipDiscs.Count; i++)
            {
                bool on = i < stakes.Count;
                chipDiscs[i].gameObject.SetActive(on);
                chipRings[i].gameObject.SetActive(on);
                chipLabels[i].gameObject.SetActive(on);
                chipAmounts[i].gameObject.SetActive(on);
                if (!on)
                {
                    continue;
                }
                bool allIn = stakes[i] == bank;
                chipLabels[i].text = allIn ? Loc.Pick("ALL", "HEPSİ")
                    : stakes.Count == 5 ? shares[i] : stakes[i].ToString();
                chipAmounts[i].text = stakes[i].ToString();
            }
            trayTitle.text = Loc.Pick("BET", "BAHİS");
            trayBank.text = Loc.Pick("BANK ", "KASAN ") + bank;
            trayTarget.text = Loc.Pick("TARGET ", "HEDEF ") + targetValue;
            confirmText.text = Loc.Pick("PLACE BET", "BAHSİ KOY");
            trayHint.text = string.Empty;
            typed = string.Empty;
            hoverItem = focusItem = -1;
            wasAllIn = false;
            trayNote.text = string.Empty;
            PlaceBet();
            trayRoot.gameObject.SetActive(true);
            trayOpen = true;
            trayFolding = false;
            trayT0 = Time.time;
            SetBetTension(0f, false);
            Listen(true);
        }

        public void HideBetTray()
        {
            trayOpen = false;
            Listen(false);
            if (trayBuilt)
            {
                trayRoot.gameObject.SetActive(false);
            }
        }

        private void Listen(bool on)
        {
            Keyboard kb = Keyboard.current;
            if (on && !listening && kb != null)
            {
                kb.onTextInput += OnTyped;
                listening = true;
            }
            else if (!on && listening)
            {
                if (kb != null)
                {
                    kb.onTextInput -= OnTyped;
                }
                listening = false;
            }
        }

        private void OnDisable()
        {
            Listen(false);
        }

        private void OnTyped(char c)
        {
            if (!trayOpen || c < '0' || c > '9')
            {
                return;
            }
            if (typed.Length == 0 && c == '0')
            {
                return; // no leading zeros
            }
            if (typed.Length >= 10)
            {
                return;
            }
            SetTyped(typed + c, true);
        }

        /// <summary>Puts an amount in the field, clamping to the bank with a small refusal.</summary>
        private void SetTyped(string value, bool byKey)
        {
            long v;
            if (!string.IsNullOrEmpty(value) && long.TryParse(value, out v) && v > trayBankValue)
            {
                value = trayBankValue.ToString();
                Refuse(Loc.Pick("Your bank holds ", "Kasanda ") + trayBankValue + Loc.Pick("", " var"));
            }
            typed = value ?? string.Empty;
            if (byKey && Sfx != null)
            {
                Sfx.Casino(BlackjackCue.ChipTap, 1.25f, 0.35f);
            }
            long now = TypedBet;
            bool allIn = now > 0 && now == trayBankValue;
            SetBetTension(trayBankValue > 0 ? now / (float)trayBankValue : 0f, allIn);
            if (allIn && !wasAllIn)
            {
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.AllInThud);
                    Sfx.DuckCasino(0.25f, 0.7f);
                }
            }
            wasAllIn = allIn;
        }

        private void Refuse(string note)
        {
            shakeT0 = Time.time;
            noteT0 = Time.time;
            trayNote.text = note;
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.InvalidBet);
            }
        }

        // ================================================================== input

        /// <summary>Which item is at a world point (chips 0..n-1, then confirm), or -1.</summary>
        public int BetItemAt(Vector2 world)
        {
            if (!trayOpen)
            {
                return -1;
            }
            Vector2 local = world - trayCentre;
            float t = Mathf.Max(0.66f, L.Type);
            for (int i = 0; i < stakes.Count; i++)
            {
                if (Vector2.Distance(local, (Vector2)chipDiscs[i].transform.localPosition) < 0.36f * t)
                {
                    return i;
                }
            }
            Vector2 c = confirmBody.transform.localPosition;
            if (Mathf.Abs(local.x - c.x) < confirmBody.size.x * 0.5f && Mathf.Abs(local.y - c.y) < confirmBody.size.y * 0.5f)
            {
                return stakes.Count;
            }
            return -1;
        }

        public Vector2? BetItemWorldCenter(int index)
        {
            if (!trayOpen || index < 0 || index > stakes.Count)
            {
                return null;
            }
            Vector2 local = index < stakes.Count ? (Vector2)chipDiscs[index].transform.localPosition : (Vector2)confirmBody.transform.localPosition;
            return (Vector2)transform.TransformPoint(trayCentre + local);
        }

        public void HoverBetItem(int index)
        {
            hoverItem = index;
        }

        public void FocusBetItem(int index)
        {
            focusItem = index;
        }

        /// <summary>Presses an item: a chip puts its stake in the field, the button confirms.
        /// Returns the confirmed amount, or 0.</summary>
        public long PressBetItem(int index)
        {
            if (!trayOpen || trayFolding || index < 0)
            {
                return 0;
            }
            if (index < stakes.Count)
            {
                SetTyped(stakes[index].ToString(), false);
                if (Sfx != null)
                {
                    Sfx.Casino(stakes[index] == trayBankValue ? BlackjackCue.ChipStack : BlackjackCue.ChipTap);
                }
                return 0;
            }
            return Confirm();
        }

        /// <summary>The keys: digits arrive by text input; these are the rest. Returns a confirmed amount or 0.</summary>
        public long HandleBetKeys(Keyboard kb)
        {
            if (!trayOpen || trayFolding || kb == null)
            {
                return 0;
            }
            if (kb.backspaceKey.wasPressedThisFrame && typed.Length > 0)
            {
                SetTyped(typed.Substring(0, typed.Length - 1), true);
            }
            if (kb.deleteKey.wasPressedThisFrame)
            {
                SetTyped(string.Empty, true);
            }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                // the arrows walk the chips, putting each stake in the field
                int cur = stakes.IndexOf(TypedBet);
                int next = kb.rightArrowKey.wasPressedThisFrame ? cur + 1 : (cur < 0 ? stakes.Count - 1 : cur - 1);
                next = Mathf.Clamp(next, 0, stakes.Count - 1);
                PressBetItem(next);
            }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                return Confirm();
            }
            return 0;
        }

        /// <summary>Nudges the field by a tenth of the bank (the pad's shoulders, the wheel).</summary>
        public void NudgeBet(int direction)
        {
            long step = System.Math.Max(1, trayBankValue / 10);
            long v = System.Math.Max(0, System.Math.Min(trayBankValue, TypedBet + direction * step));
            SetTyped(v <= 0 ? string.Empty : v.ToString(), true);
        }

        private long Confirm()
        {
            long v = TypedBet;
            if (v <= 0)
            {
                Refuse(Loc.Pick("Type a stake or pick a chip", "Bir miktar yaz ya da fiş seç"));
                return 0;
            }
            if (v > trayBankValue)
            {
                Refuse(Loc.Pick("Your bank holds ", "Kasanda ") + trayBankValue + Loc.Pick("", " var"));
                return 0;
            }
            return v;
        }

        // ================================================================== the frame

        private void TickBet(float dt)
        {
            if (!trayBuilt || !trayOpen)
            {
                return;
            }
            float t = Mathf.Max(0.66f, L.Type);
            float age = Time.time - trayT0;
            float open = Mathf.Clamp01(age / T(0.22f));
            trayA = BlackjackFx.EaseOut(open);
            float sc = Mathf.Lerp(0.94f, 1f, BlackjackFx.EaseOut(open));
            float shake = Time.time - shakeT0 < 0.25f ? Mathf.Sin((Time.time - shakeT0) * 70f) * 0.04f * t * (1f - (Time.time - shakeT0) / 0.25f) : 0f;
            if (!trayFolding)
            {
                trayRoot.localPosition = trayCentre;
                trayRoot.localScale = new Vector3(sc, sc, 1f);
            }
            long v = TypedBet;
            bool allIn = v > 0 && v == trayBankValue;
            float share = trayBankValue > 0 ? Mathf.Clamp01(v / (float)trayBankValue) : 0f;
            Color glow = Color.Lerp(BlackjackShapes.Amber, BlackjackShapes.BurgundyLight, allIn ? 0.6f : 0f);
            trayGlow.color = new Color(glow.r, glow.g, glow.b, BlackjackFx.LightAlpha(0.15f + 0.55f * share) * trayA);
            trayRim.color = new Color(allIn ? BlackjackShapes.Gold.r : BlackjackShapes.GoldDim.r, allIn ? BlackjackShapes.Gold.g : BlackjackShapes.GoldDim.g,
                allIn ? BlackjackShapes.Gold.b : BlackjackShapes.GoldDim.b, trayA);
            trayBody.color = new Color(allIn ? 0.11f : 0.045f, allIn ? 0.04f : 0.075f, allIn ? 0.055f : 0.072f, 0.97f * trayA);
            TextAlpha(trayTitle, CaptionInk, trayA);
            TextAlpha(trayBank, BankInk, trayA);
            TextAlpha(trayTarget, BlackjackShapes.Gold, trayA);
            // the field: the number, a caret, a rim that warms with the stake
            fieldText.transform.localPosition = (Vector2)fieldBody.transform.localPosition + new Vector2(shake, 0f);
            fieldText.text = typed.Length > 0 ? typed : "0";
            TextAlpha(fieldText, typed.Length > 0 ? BankInk : new Color(0.5f, 0.48f, 0.42f), trayA);
            Color rim = Color.Lerp(BlackjackShapes.GoldDim, allIn ? BlackjackShapes.Gold : BlackjackShapes.Amber, share);
            fieldRim.color = new Color(rim.r, rim.g, rim.b, trayA);
            fieldBody.color = new Color(0.03f, 0.03f, 0.035f, trayA);
            float textHalf = (typed.Length > 0 ? typed.Length : 1) * 0.15f * t;
            caret.transform.localPosition = (Vector2)fieldBody.transform.localPosition + new Vector2(textHalf + 0.06f * t + shake, 0f);
            caret.transform.localScale = new Vector3(0.025f * t, 0.42f * t, 1f);
            Alpha(caret, (Mathf.Repeat(Time.time * 1.6f, 1f) < 0.55f ? 0.85f : 0f) * trayA);
            float noteA = trayNote.text.Length > 0 ? Mathf.Clamp01(2.2f - (Time.time - noteT0)) : 0f;
            TextAlpha(trayNote, LossInk, noteA * trayA);
            // the chips
            for (int i = 0; i < stakes.Count; i++)
            {
                bool selected = stakes[i] == v && v > 0;
                bool isAllIn = stakes[i] == trayBankValue;
                bool hot = i == hoverItem || i == focusItem;
                float lift = selected ? 0.06f * t : hot ? 0.03f * t : 0f;
                Vector2 basePos = chipRings[i].transform.localPosition;
                chipDiscs[i].transform.localPosition = basePos + new Vector2(0f, lift);
                chipLabels[i].transform.localPosition = basePos + new Vector2(0f, lift + 0.01f * t);
                Color disc = isAllIn ? AllInDisc : Color.white;
                disc.a = trayA;
                chipDiscs[i].color = disc;
                float ring = selected ? 0.7f : hot ? 0.4f : 0f;
                chipRings[i].color = new Color(isAllIn ? 1f : BlackjackShapes.Amber.r, isAllIn ? 0.55f : BlackjackShapes.Amber.g,
                    isAllIn ? 0.45f : BlackjackShapes.Amber.b, BlackjackFx.LightAlpha(ring) * trayA);
                TextAlpha(chipLabels[i], isAllIn ? BlackjackShapes.Gold : BlackjackShapes.Cream, trayA);
                TextAlpha(chipAmounts[i], selected ? BankInk : MiniInk, trayA);
            }
            // the button: lit only when there is something to place
            bool can = v > 0 && v <= trayBankValue;
            bool cHot = hoverItem == stakes.Count || focusItem == stakes.Count;
            Color body = can ? (cHot ? new Color(0.78f, 0.62f, 0.32f) : new Color(0.66f, 0.51f, 0.26f)) : new Color(0.24f, 0.22f, 0.2f);
            body.a = trayA;
            confirmBody.color = body;
            confirmRim.color = new Color(BlackjackShapes.Gold.r, BlackjackShapes.Gold.g, BlackjackShapes.Gold.b, (can ? 0.95f : 0.35f) * trayA);
            TextAlpha(confirmText, can ? new Color(0.1f, 0.06f, 0.04f) : new Color(0.5f, 0.47f, 0.42f), trayA);
            TextAlpha(trayHint, MiniInk, trayA * 0.8f);
        }

        /// <summary>The tray folds down onto the wager plate (the stake has been placed).</summary>
        public System.Collections.IEnumerator FoldBetTray()
        {
            if (!trayBuilt || !trayOpen)
            {
                yield break;
            }
            Listen(false);
            trayFolding = true;
            float dur = T(0.2f);
            float t0 = Time.time;
            Vector2 from = trayCentre;
            Vector2 to = L.Wager;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / dur);
                float e = BlackjackFx.EaseIn(k);
                trayRoot.localPosition = Vector2.Lerp(from, to, e);
                trayRoot.localScale = new Vector3(1f - 0.8f * e, (1f - 0.8f * e) * (1f - 0.5f * e), 1f);
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
            trayFolding = false;
            HideBetTray();
        }

        /// <summary>LAB: types a string into the field a key at a time, as a player would.</summary>
        public System.Collections.IEnumerator LabType(string text, float gap)
        {
            for (int i = 0; i < text.Length; i++)
            {
                OnTyped(text[i]);
                float end = Time.time + gap;
                while (Time.time < end)
                {
                    yield return null;
                }
            }
        }

        /// <summary>LAB: presses the confirm button (a refusal plays if the field cannot be placed).</summary>
        public long LabConfirm()
        {
            return Confirm();
        }

        /// <summary>The input's state as text (the bet-input debug view).</summary>
        public string BetInputReadout
        {
            get
            {
                return "typed '" + typed + "'  value " + TypedBet + "  bank " + trayBankValue
                    + "  hover " + hoverItem + "  focus " + focusItem + "  valid " + (TypedBet > 0 && TypedBet <= trayBankValue);
            }
        }
    }
}
