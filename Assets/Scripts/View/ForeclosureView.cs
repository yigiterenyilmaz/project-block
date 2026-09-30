// PURPOSE: "Kredi kartı" - HACİZ, the loan's big negative payoff, as a full presentation event.
// The term ran out with money still owed, and the bank takes the player's things one by one.
//
// THE ENTRY. A breath of tension, then the screen loses its footing - not all at once: a cold dim
// and a closing vignette over ~0.2 s, the bars stepping back (they live on the overlay canvas and
// would cover everything), the UI still readable. Then THE STAMP: a wide, slightly turned legal
// seal in dried burgundy with cream lettering, driven down 1.8 -> 0.92 -> 1.04 -> 1.0, a few
// pixels of impulse, a spray of paper fibre and a compression of the vignette - no explosion. It
// holds long enough to be read, then shrinks to the side and BECOMES the FORECLOSURE LEDGER: a
// tall dossier with the seal on its head, what is still OWED and what has been COLLECTED, and a
// BANK DRAWER at its foot - a graphite opening under a brass lip, never a bin.
//
// ONE THING AT A TIME, IN CORE'S ORDER. Each asset: TARGET LOCK (a 1.04 lift, brass appraisal
// corners - no neon), APPRAISAL (a slip: the shelf value small, a legal slash across it, the
// seizure value stamped in larger), the SEIZURE RIBBON sealing it, the LIFT off its slot, the
// CONFISCATION (a shallow curve into the drawer, shrinking and draining as it goes, squeezed in
// its last stretch and swallowed BEHIND the lip - it never fades out before it gets there), the
// drawer's clack, and the VALUE TRANSFER: its seizure value as a brass token back up to the debt,
// which rolls DOWN. The first three go at full weight; after that the cadence tightens, and once
// the valuables are gone the dossier cools into SCRAP COLLECTION - plain blocks lifted, stamped
// with their pittance and gone in a quick, one-by-one rhythm while the deck count thins. If the
// debt reaches zero the sequence stops there: the file is CLOSED (a warm seal, not a celebration),
// the drawer shuts, the dossier folds and the screen comes back.
//
// THE VIEW DECIDES NOTHING: the order, every value, every half price, the debt before and after
// each seizure, the deck count after each card - all of it is the CreditStatement Core wrote as it
// happened. The assets are already gone from the rules when this plays; what flies is a PROXY
// built from the thing's own art at the place it stood.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class ForeclosureView : MonoBehaviour
    {
        // =================================================================== tuning
        public static class Style
        {
            public static int Order = 170;

            public static readonly Color DriedRed = new Color(0.46f, 0.08f, 0.1f);
            public static readonly Color Dim = new Color(0.06f, 0.055f, 0.065f);
            public static float DimAlpha = 0.34f;
            public static float VignetteAlpha = 0.55f;
            public static float BarsAlpha = 0.28f;

            public static float TensionTime = 0.18f;
            public static float EntryTime = 0.19f;
            public static float StampTime = 0.21f;
            public static float StampHold = 0.46f;
            public static float StampToLedger = 0.32f;
            public static float StampRotation = -5.5f;
            public static float StampWidth = 6.2f;
            public static float ImpactShake = 0.035f;

            public static float LedgerWidth = 2.0f;
            public static float LedgerHeight = 3.4f;

            // full cadence (the first three valuables)
            public static float Lock = 0.08f;
            public static float Appraise = 0.17f;
            public static float Ribbon = 0.1f;
            public static float Lift = 0.08f;
            public static float Travel = 0.32f;
            public static float Transfer = 0.17f;
            // accelerated
            public static float FastAppraise = 0.1f;
            public static float FastTravel = 0.21f;
            // scrap
            public static float ScrapTravel = 0.16f;
            public static int FullCadenceCount = 3;

            public static float Summary = 0.65f;
            public static float Fold = 0.26f;
        }

        public static class Layers
        {
            public static bool ShowAssetNormalValue = true;
            public static bool ShowAssetSeizureValue = true;
            public static bool ShowForeclosureSelectedAsset;
            public static bool ShowAssetSourceAnchor;
            public static bool ShowBankDrawerAnchor;
            public static bool ShowAssetTravelPath;
            public static bool ShowDebtTransferPath;
            public static bool ShowForeclosureQueue;
            public static bool ShowRemainingAssetCount;

            public static void Reset()
            {
                ShowAssetNormalValue = true;
                ShowAssetSeizureValue = true;
                ShowForeclosureSelectedAsset = false;
                ShowAssetSourceAnchor = false;
                ShowBankDrawerAnchor = false;
                ShowAssetTravelPath = false;
                ShowDebtTransferPath = false;
                ShowForeclosureQueue = false;
                ShowRemainingAssetCount = false;
            }
        }

        /// <summary>One thing to take: what Core took, and where on screen it stood.</summary>
        public struct Asset
        {
            public SeizedItem Item;
            public Vector2 Source;
        }

        public enum Cue
        {
            Tension,
            SilenceDip,
            Stamp,
            Lock,
            Appraise,
            Ribbon,
            Clack,
            Transfer,
            ScrapMode,
            Cleared,
            Complete
        }

        public Action<Cue> Sounded;

        /// <summary>A small screen impulse, in world units.</summary>
        public Action<float> Impulse;

        /// <summary>The overlay-canvas bars' alpha (they would cover the proxies otherwise).</summary>
        public Action<float> BarsAlpha;

        /// <summary>Called with the debt every time it moves on the dossier - the main ledger
        /// follows it.</summary>
        public Action<long> DebtMoved;

        public float PlaybackRate = 1f;

        public bool Playing { get; private set; }

        private Camera cam;
        private SpriteRenderer dim;
        private SpriteRenderer vignette;
        private Transform stampRoot;
        private Transform ledger;
        private SpriteRenderer ledgerBody;
        private SpriteRenderer ledgerTrim;
        private SpriteRenderer headerSeal;
        private TextMesh headerText;
        private TextMesh owedLabel;
        private TextMesh owedValue;
        private TextMesh collectedLabel;
        private TextMesh collectedValue;
        private TextMesh modeText;
        private TextMesh deckText;
        private TextMesh summaryText;
        private SpriteRenderer drawerLip;
        private SpriteRenderer drawerOpening;
        private SpriteRenderer drawerShutter;
        private SpriteMask drawerMask;
        private TextMesh queueText;
        private readonly List<GameObject> debugMarks = new List<GameObject>();
        private float dimK;
        private float vignetteK;
        private float vignettePunch;
        private float ledgerAlpha;
        private bool scrapMode;
        private long owedShown;
        private long collectedShown;

        public void Build(Camera camera)
        {
            cam = camera;
            if (dim != null)
            {
                return;
            }
            dim = Sprite(transform, "Dim", ViewUtil.WhiteSprite, Style.Order);
            vignette = Sprite(transform, "Vignette", DebtLedgerShapes.Vignette, Style.Order + 1);
        }

        /// <summary>Plays a foreclosure. <paramref name="assets"/> are the statement's seizures in
        /// the order Core took them, each with where it stood on screen.</summary>
        public void Play(CreditStatement statement, List<Asset> assets, Vector2 ledgerAt, float ledgerScale)
        {
            Stop();
            Playing = true;
            StartCoroutine(Sequence(statement, assets, ledgerAt, ledgerScale, false));
        }

        /// <summary>The lab: the entry and the stamp alone, no dossier.</summary>
        public void PlayStampOnly(Vector2 ledgerAt, float ledgerScale)
        {
            Stop();
            Playing = true;
            StartCoroutine(Sequence(null, new List<Asset>(), ledgerAt, ledgerScale, true));
        }

        /// <summary>Tears everything down at once and gives the screen back.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            if (stampRoot != null)
            {
                Destroy(stampRoot.gameObject);
                stampRoot = null;
            }
            if (ledger != null)
            {
                Destroy(ledger.gameObject);
                ledger = null;
            }
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (dim != null && (c == dim.transform || c == vignette.transform))
                {
                    continue;
                }
                Destroy(c.gameObject);
            }
            ClearDebug();
            dimK = 0f;
            vignetteK = 0f;
            vignettePunch = 0f;
            PaintAtmosphere();
            if (Playing && BarsAlpha != null)
            {
                BarsAlpha(1f);
            }
            Playing = false;
        }

        // =================================================================== the sequence

        private IEnumerator Sequence(CreditStatement statement, List<Asset> assets, Vector2 ledgerAt,
            float ledgerScale, bool stampOnly)
        {
            // 1. tension: the books lock.
            Emit(Cue.Tension);
            yield return Wait(Style.TensionTime);
            Emit(Cue.SilenceDip);

            // 2. the screen loses its footing - over ~0.2 s, never in one frame.
            if (BarsAlpha != null)
            {
                BarsAlpha(Style.BarsAlpha);
            }
            float t = 0f;
            while (t < Style.EntryTime)
            {
                t += Dt;
                float u = DebtLedgerView.EaseInOut(t / Style.EntryTime);
                dimK = u;
                vignetteK = u;
                yield return null;
            }

            // 3. THE STAMP.
            BuildStamp();
            t = 0f;
            bool hit = false;
            while (t < Style.StampTime)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / Style.StampTime);
                float s = u < 0.45f ? Mathf.Lerp(1.8f, 0.92f, DebtLedgerView.EaseOut(u / 0.45f))
                    : u < 0.75f ? Mathf.Lerp(0.92f, 1.04f, (u - 0.45f) / 0.3f)
                    : Mathf.Lerp(1.04f, 1f, (u - 0.75f) / 0.25f);
                stampRoot.localScale = new Vector3(s, s, 1f);
                SetStampAlpha(Mathf.Clamp01(t / 0.05f));
                if (!hit && u >= 0.45f)
                {
                    hit = true;
                    Emit(Cue.Stamp);
                    if (Impulse != null)
                    {
                        Impulse(Style.ImpactShake);
                    }
                    Fibres(stampRoot.position, 9);
                    vignettePunch = 0.18f;
                }
                vignettePunch = Mathf.Max(0f, vignettePunch - Dt * 0.8f);
                yield return null;
            }
            float hold = 0f;
            while (hold < Style.StampHold)
            {
                hold += Dt;
                vignettePunch = Mathf.Max(0f, vignettePunch - Dt * 0.8f);
                yield return null;
            }
            vignettePunch = 0f;

            if (stampOnly)
            {
                yield return Wait(0.3f);
                yield return Exit();
                yield break;
            }

            // 4. the stamp shrinks to the side and becomes the dossier.
            BuildLedger(ledgerAt, ledgerScale, statement);
            Vector3 from = stampRoot.position;
            t = 0f;
            while (t < Style.StampToLedger)
            {
                t += Dt;
                float u = DebtLedgerView.EaseInOut(t / Style.StampToLedger);
                stampRoot.position = Vector3.Lerp(from, headerSeal.transform.position, u);
                float s = Mathf.Lerp(1f, 0.22f * ledgerScale, u);
                stampRoot.localScale = new Vector3(s, s, 1f);
                SetStampAlpha(1f - u);
                ledgerAlpha = u;
                PaintLedger();
                yield return null;
            }
            Destroy(stampRoot.gameObject);
            stampRoot = null;
            ledgerAlpha = 1f;
            PaintLedger();
            PaintQueue(statement, -1);

            // 5. one thing at a time, in Core's order.
            int valuables = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                Asset asset = assets[i];
                bool scrap = asset.Item.Kind == SeizedKind.PlainBlock;
                if (scrap && !scrapMode)
                {
                    scrapMode = true;
                    Emit(Cue.ScrapMode);
                    DebtLedgerView.SetText(modeText, Loc.Pick("COLLECTING WHAT IS LEFT", "ELDEKİLER TOPLANIYOR"));
                }
                bool full = !scrap && valuables < Style.FullCadenceCount;
                if (!scrap)
                {
                    valuables++;
                }
                PaintQueue(statement, i);
                yield return Seize(asset, full, scrap);
                if (asset.Item.DebtAfter <= 0)
                {
                    break; // Core stopped here, so does the file.
                }
            }

            // 6. the file closes.
            bool cleared = statement != null && statement.DebtAfter <= 0 && statement.WrittenOff <= 0;
            if (statement != null && statement.WrittenOff > 0)
            {
                DebtLedgerView.SetText(modeText, Loc.Pick("REST WRITTEN OFF ", "KALAN SİLİNDİ ")
                    + DebtLedgerView.Money(statement.WrittenOff));
                owedShown = 0;
                if (DebtMoved != null)
                {
                    DebtMoved(0);
                }
            }
            if (cleared || (statement != null && statement.WrittenOff > 0))
            {
                Emit(Cue.Cleared);
                yield return ClosedSeal();
            }
            yield return SummaryHold(statement, assets.Count);
            yield return Exit();
        }

        private IEnumerator Seize(Asset asset, bool full, bool scrap)
        {
            SeizedItem item = asset.Item;
            Transform proxy = BuildProxy(item, asset.Source, out List<SpriteRenderer> renderers,
                out CardVisual card);
            float heavy = item.Kind == SeizedKind.Joker && item.Rarity != Rarity.Common ? 1.25f : 1f;
            float baseScale = item.Kind == SeizedKind.Power ? 0.9f : 1f;
            DebugMark(asset.Source, new Color(0.4f, 1f, 0.9f), Layers.ShowAssetSourceAnchor);

            // TARGET LOCK
            Emit(Cue.Lock);
            List<SpriteRenderer> corners = Corners(proxy, item.Kind == SeizedKind.Joker || item.Kind == SeizedKind.Power
                ? new Vector2(1.05f, 1.35f) : new Vector2(1.1f, 1.45f));
            float t = 0f;
            while (t < Style.Lock * heavy)
            {
                t += Dt;
                float s = baseScale * Mathf.Lerp(1f, 1.04f, t / (Style.Lock * heavy));
                proxy.localScale = new Vector3(s, s, 1f);
                SetCornersAlpha(corners, Mathf.Clamp01(t / (Style.Lock * heavy)));
                yield return null;
            }

            // APPRAISAL
            if (!scrap)
            {
                Emit(Cue.Appraise);
                yield return Appraisal(proxy, item, full ? Style.Appraise * heavy : Style.FastAppraise);
                // THE RIBBON seals it as the bank's
                Emit(Cue.Ribbon);
                yield return RibbonOver(proxy, Style.Ribbon * (full ? heavy : 0.8f));
            }
            else
            {
                yield return LowStamp(proxy, item);
            }

            // LIFT
            float lift = item.Kind == SeizedKind.Joker ? 0.08f * heavy : 0.055f;
            Vector3 home = proxy.position;
            t = 0f;
            while (t < Style.Lift)
            {
                t += Dt;
                float u = DebtLedgerView.EaseOut(t / Style.Lift);
                proxy.position = home + new Vector3(0f, lift * u, 0f);
                Tint(renderers, card, 1f - 0.2f * u, 1f);
                yield return null;
            }
            DestroyAll(corners);

            // CONFISCATION: a shallow curve into the drawer, shrinking and draining, squeezed
            // in the last stretch and swallowed behind the lip.
            Vector2 start = proxy.position;
            Vector2 mouth = drawerOpening.transform.position;
            Vector2 end = mouth + new Vector2(0f, -0.45f * ledger.localScale.y);
            Vector2 ctrl = (start + mouth) * 0.5f + new Vector2(0f, 0.8f);
            float travel = scrap ? Style.ScrapTravel : full ? Style.Travel * heavy : Style.FastTravel;
            if (Layers.ShowAssetTravelPath)
            {
                DebugPath(start, ctrl, mouth, new Color(1f, 0.8f, 0.3f));
            }
            DebugMark(mouth, new Color(1f, 0.5f, 0.2f), Layers.ShowBankDrawerAnchor);
            bool masked = false;
            t = 0f;
            float total = travel * 1.18f;
            while (t < total)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / travel);
                Vector2 p = u < 1f ? DebtLedgerView.Bezier(start, ctrl, mouth, DebtLedgerView.EaseInOut(u))
                    : Vector2.Lerp(mouth, end, Mathf.Clamp01((t - travel) / (travel * 0.18f)));
                proxy.position = new Vector3(p.x, p.y, 0f);
                float s = baseScale * (u < 0.5f ? Mathf.Lerp(1.04f, 0.75f, u / 0.5f) : Mathf.Lerp(0.75f, 0.45f, (u - 0.5f) / 0.5f));
                float squash = u > 0.85f ? Mathf.Lerp(1f, 0.78f, (u - 0.85f) / 0.15f) : 1f;
                proxy.localScale = new Vector3(s, s * squash, 1f);
                Tint(renderers, card, Mathf.Lerp(0.8f, 0.45f, u), 1f);
                if (!masked && u > 0.8f)
                {
                    masked = true;
                    SetMasked(renderers, card);
                }
                yield return null;
            }
            Emit(Cue.Clack);
            StartCoroutine(DrawerKick());
            Destroy(proxy.gameObject);

            // VALUE TRANSFER: its seizure value back up to the debt, which rolls DOWN.
            yield return Transfer(item, scrap ? 0.12f : full ? Style.Transfer : 0.13f);
        }

        private IEnumerator Transfer(SeizedItem item, float duration)
        {
            Emit(Cue.Transfer);
            Vector2 from = drawerOpening.transform.position;
            Vector2 to = owedValue.transform.position;
            var token = new GameObject("SeizureToken").transform;
            token.SetParent(transform, false);
            TextMesh label = DebtLedgerView.Text(token, "Label", DebtLedgerView.Money(item.Credited),
                0.0125f, DebtLedgerView.Style.Brass, Style.Order + 24, TextAnchor.MiddleCenter);
            if (Layers.ShowDebtTransferPath)
            {
                DebugPath(from, (from + to) * 0.5f + new Vector2(0.4f, 0f), to, new Color(0.5f, 1f, 0.5f));
            }
            float t = 0f;
            long owedFrom = item.DebtBefore;
            long collectedFrom = collectedShown;
            while (t < duration)
            {
                t += Dt;
                float u = DebtLedgerView.EaseInOut(t / duration);
                token.position = DebtLedgerView.Bezier(from, (from + to) * 0.5f + new Vector2(0.4f, 0f), to, u);
                yield return null;
            }
            Destroy(token.gameObject);
            _ = label;
            // the debt rolls down, what was collected rolls up
            float k = 0f;
            const float roll = 0.14f;
            while (k < roll)
            {
                k += Dt;
                float u = DebtLedgerView.EaseOut(k / roll);
                owedShown = (long)Mathf.Lerp(owedFrom, item.DebtAfter, u);
                collectedShown = (long)Mathf.Lerp(collectedFrom, collectedFrom + item.Credited, u);
                owedValue.transform.localPosition = new Vector3(0f, OwedY - 0.03f * Mathf.Sin(u * Mathf.PI), 0f);
                PaintLedger();
                yield return null;
            }
            owedShown = item.DebtAfter;
            collectedShown = collectedFrom + item.Credited;
            owedValue.transform.localPosition = new Vector3(0f, OwedY, 0f);
            if (item.Card != null)
            {
                DebtLedgerView.SetText(deckText, Loc.Pick("DECK ", "DESTE ") + item.DeckCountAfter);
            }
            PaintLedger();
            if (DebtMoved != null)
            {
                DebtMoved(item.DebtAfter);
            }
        }

        // =================================================================== pieces

        private void BuildStamp()
        {
            stampRoot = new GameObject("HacizStamp").transform;
            stampRoot.SetParent(transform, false);
            Vector2 centre = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
            stampRoot.position = new Vector3(centre.x, centre.y + 0.4f, 0f);
            stampRoot.localRotation = Quaternion.Euler(0f, 0f, Style.StampRotation);
            SpriteRenderer plate = Sprite(stampRoot, "Plate", DebtLedgerShapes.Stamp, Style.Order + 30);
            DebtLedgerView.Place(plate, Vector2.zero, Style.StampWidth, Style.StampWidth / 3f);
            // The word fills the seal whatever its length: "HACİZ" is short, "FORECLOSED" is not.
            string word = Loc.Pick("FORECLOSED", "HACİZ");
            TextMesh text = DebtLedgerView.Text(stampRoot, "Text", word,
                Mathf.Min(0.1f, 0.62f / Mathf.Max(1, word.Length)), DebtLedgerView.Style.Cream,
                Style.Order + 31, TextAnchor.MiddleCenter);
            _ = text;
            SetStampAlpha(0f);
        }

        private void SetStampAlpha(float a)
        {
            if (stampRoot == null)
            {
                return;
            }
            foreach (SpriteRenderer r in stampRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                r.color = DebtLedgerView.WithAlpha(Style.DriedRed, 0.95f * a);
            }
            foreach (TextMesh m in stampRoot.GetComponentsInChildren<TextMesh>())
            {
                if (m.transform.parent == stampRoot)
                {
                    ViewUtil.SetTextColor(m, DebtLedgerView.WithAlpha(DebtLedgerView.Style.Cream, a));
                }
            }
        }

        private const float OwedY = 0.78f;

        private void BuildLedger(Vector2 at, float scale, CreditStatement statement)
        {
            ledger = new GameObject("ForeclosureLedger").transform;
            ledger.SetParent(transform, false);
            ledger.position = new Vector3(at.x, at.y, 0f);
            ledger.localScale = new Vector3(scale, scale, 1f);
            float w = Style.LedgerWidth;
            float h = Style.LedgerHeight;
            int o = Style.Order + 10;
            ledgerBody = Sprite(ledger, "Body", ViewUtil.RoundedSprite, o);
            DebtLedgerView.Place(ledgerBody, Vector2.zero, w, h);
            ledgerTrim = Sprite(ledger, "Trim", ViewUtil.RoundedSprite, o - 1);
            DebtLedgerView.Place(ledgerTrim, Vector2.zero, w + 0.05f, h + 0.05f);
            headerSeal = Sprite(ledger, "HeaderSeal", DebtLedgerShapes.StampSmall, o + 2);
            DebtLedgerView.Place(headerSeal, new Vector2(0f, h * 0.5f - 0.32f), 1.0f, 0.5f);
            headerSeal.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            headerText = DebtLedgerView.Text(ledger, "Header", Loc.Pick("FORECLOSED", "HACİZ"), 0.02f,
                DebtLedgerView.Style.Cream, o + 3, TextAnchor.MiddleCenter);
            headerText.transform.localPosition = new Vector3(0f, h * 0.5f - 0.32f, 0f);
            headerText.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            owedLabel = DebtLedgerView.Text(ledger, "OwedLabel", Loc.Pick("STILL OWED", "KALAN BORÇ"), 0.009f,
                DebtLedgerView.Style.BrassDim, o + 3, TextAnchor.MiddleCenter);
            owedLabel.transform.localPosition = new Vector3(0f, OwedY + 0.24f, 0f);
            owedValue = DebtLedgerView.Text(ledger, "Owed", " ", 0.022f, DebtLedgerView.Style.CreamWarm,
                o + 3, TextAnchor.MiddleCenter);
            owedValue.transform.localPosition = new Vector3(0f, OwedY, 0f);
            collectedLabel = DebtLedgerView.Text(ledger, "CollectedLabel", Loc.Pick("COLLECTED", "TOPLANAN"),
                0.009f, DebtLedgerView.Style.BrassDim, o + 3, TextAnchor.MiddleCenter);
            collectedLabel.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            collectedValue = DebtLedgerView.Text(ledger, "Collected", " ", 0.017f, DebtLedgerView.Style.Brass,
                o + 3, TextAnchor.MiddleCenter);
            collectedValue.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            modeText = DebtLedgerView.Text(ledger, "Mode", " ", 0.0085f, new Color(0.62f, 0.66f, 0.7f),
                o + 3, TextAnchor.MiddleCenter);
            modeText.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            deckText = DebtLedgerView.Text(ledger, "Deck", " ", 0.0095f, DebtLedgerView.Style.Cream,
                o + 3, TextAnchor.MiddleCenter);
            deckText.transform.localPosition = new Vector3(0f, -0.42f, 0f);
            summaryText = DebtLedgerView.Text(ledger, "Summary", " ", 0.0085f, DebtLedgerView.Style.Cream,
                o + 3, TextAnchor.UpperCenter);
            summaryText.transform.localPosition = new Vector3(0f, -0.62f, 0f);

            // THE BANK DRAWER: a graphite opening under a brass lip. What goes in goes BEHIND the
            // lip (the mask covers everything above the opening's floor, so a proxy sliding down
            // into it is cut at the lip rather than faded).
            float drawerY = -h * 0.5f + 0.42f;
            drawerOpening = Sprite(ledger, "DrawerOpening", ViewUtil.WhiteSprite, o + 1);
            DebtLedgerView.Place(drawerOpening, new Vector2(0f, drawerY), w * 0.72f, 0.2f);
            drawerLip = Sprite(ledger, "DrawerLip", ViewUtil.WhiteSprite, o + 30);
            DebtLedgerView.Place(drawerLip, new Vector2(0f, drawerY - 0.1f), w * 0.8f, 0.05f);
            drawerShutter = Sprite(ledger, "DrawerShutter", ViewUtil.WhiteSprite, o + 2);
            DebtLedgerView.Place(drawerShutter, new Vector2(0f, drawerY), w * 0.72f, 0f);
            var maskGo = new GameObject("DrawerMask");
            maskGo.transform.SetParent(ledger, false);
            drawerMask = maskGo.AddComponent<SpriteMask>();
            drawerMask.sprite = ViewUtil.WhiteSprite;
            // everything above the drawer's floor, as tall as the screen
            float maskH = 30f;
            Vector2 unit = ViewUtil.WhiteSprite.bounds.size;
            maskGo.transform.localPosition = new Vector3(0f, drawerY - 0.1f + maskH * 0.5f, 0f);
            maskGo.transform.localScale = new Vector3(40f / unit.x, maskH / unit.y, 1f);

            long startDebt = statement != null ? (statement.DebtBeforeForeclosure > 0
                ? statement.DebtBeforeForeclosure
                : statement.Seized.Count > 0 ? statement.Seized[0].DebtBefore : statement.DebtAfter) : 0;
            owedShown = startDebt;
            collectedShown = 0;
            scrapMode = false;
            DebtLedgerView.SetText(deckText, statement != null && statement.DeckCountBeforeForeclosure > 0
                ? Loc.Pick("DECK ", "DESTE ") + statement.DeckCountBeforeForeclosure : " ");
            ledgerAlpha = 0f;
            PaintLedger();
        }

        private void PaintLedger()
        {
            if (ledger == null)
            {
                return;
            }
            Color body = scrapMode ? new Color(0.07f, 0.075f, 0.085f) : new Color(0.085f, 0.07f, 0.078f);
            ledgerBody.color = DebtLedgerView.WithAlpha(body, 0.97f * ledgerAlpha);
            ledgerTrim.color = DebtLedgerView.WithAlpha(DebtLedgerView.Style.Brass, 0.8f * ledgerAlpha);
            headerSeal.color = DebtLedgerView.WithAlpha(Style.DriedRed, 0.95f * ledgerAlpha);
            drawerOpening.color = DebtLedgerView.WithAlpha(new Color(0.03f, 0.03f, 0.035f), ledgerAlpha);
            drawerLip.color = DebtLedgerView.WithAlpha(DebtLedgerView.Style.Brass, ledgerAlpha);
            drawerShutter.color = DebtLedgerView.WithAlpha(new Color(0.16f, 0.15f, 0.16f), ledgerAlpha);
            DebtLedgerView.SetText(owedValue, DebtLedgerView.Money(owedShown));
            DebtLedgerView.SetText(collectedValue, DebtLedgerView.Money(collectedShown));
            foreach (TextMesh m in new[] { headerText, owedLabel, owedValue, collectedLabel, collectedValue,
                modeText, deckText, summaryText })
            {
                Color c = m.color;
                c.a = ledgerAlpha;
                ViewUtil.SetTextColor(m, c);
            }
        }

        private IEnumerator Appraisal(Transform proxy, SeizedItem item, float duration)
        {
            var slip = new GameObject("Appraisal").transform;
            slip.SetParent(transform, false);
            slip.position = proxy.position + new Vector3(0f, 1.0f, 0f);
            int o = Style.Order + 26;
            SpriteRenderer plate = Sprite(slip, "Plate", ViewUtil.RoundedSprite, o);
            DebtLedgerView.Place(plate, Vector2.zero, 1.2f, 0.5f);
            SpriteRenderer rim = Sprite(slip, "Rim", ViewUtil.RoundedSprite, o - 1);
            DebtLedgerView.Place(rim, Vector2.zero, 1.24f, 0.54f);
            TextMesh normal = DebtLedgerView.Text(slip, "Normal", Loc.Pick("VALUE ", "DEĞER ")
                + DebtLedgerView.Money(item.Value), 0.009f, DebtLedgerView.Style.Cream, o + 1, TextAnchor.MiddleCenter);
            normal.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            SpriteRenderer slash = Sprite(slip, "Slash", ViewUtil.WhiteSprite, o + 2);
            slash.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            TextMesh seized = DebtLedgerView.Text(slip, "Seized", Loc.Pick("SEIZED ", "HACİZ ")
                + DebtLedgerView.Money(item.Credited), 0.0135f, DebtLedgerView.Style.Amber, o + 1, TextAnchor.MiddleCenter);
            seized.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            float t = 0f;
            float total = duration + 0.12f;
            while (t < total)
            {
                t += Dt;
                float a = Mathf.Clamp01(t / (duration * 0.3f));
                plate.color = DebtLedgerView.WithAlpha(new Color(0.08f, 0.07f, 0.075f), 0.96f * a);
                rim.color = DebtLedgerView.WithAlpha(DebtLedgerView.Style.Brass, 0.8f * a);
                ViewUtil.SetTextColor(normal, DebtLedgerView.WithAlpha(DebtLedgerView.Style.Cream,
                    Layers.ShowAssetNormalValue ? a : 0f));
                // the legal slash through the shelf value
                float cut = Mathf.Clamp01((t - duration * 0.35f) / (duration * 0.25f));
                DebtLedgerView.Place(slash, new Vector2(0f, 0.11f), 0.95f * cut, 0.012f);
                slash.color = DebtLedgerView.WithAlpha(DebtLedgerView.Style.Crimson,
                    Layers.ShowAssetNormalValue ? a : 0f);
                // the seizure value stamps in, larger
                float st = Mathf.Clamp01((t - duration * 0.6f) / (duration * 0.4f));
                float s = st <= 0f ? 1.4f : Mathf.Lerp(1.4f, 1f, DebtLedgerView.EaseOut(st));
                seized.transform.localScale = new Vector3(s, s, 1f);
                ViewUtil.SetTextColor(seized, DebtLedgerView.WithAlpha(DebtLedgerView.Style.Amber,
                    Layers.ShowAssetSeizureValue ? st : 0f));
                yield return null;
            }
            // it rides with the asset a moment, then goes
            StartCoroutine(FadeAway(slip.gameObject, 0.22f));
        }

        private IEnumerator LowStamp(Transform proxy, SeizedItem item)
        {
            var root = new GameObject("LowStamp").transform;
            root.SetParent(transform, false);
            root.position = proxy.position + new Vector3(0f, 0.2f, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, -6f);
            TextMesh text = DebtLedgerView.Text(root, "Value", DebtLedgerView.Money(item.Credited), 0.012f,
                new Color(0.62f, 0.66f, 0.7f), Style.Order + 27, TextAnchor.MiddleCenter);
            float t = 0f;
            while (t < 0.07f)
            {
                t += Dt;
                float s = Mathf.Lerp(1.5f, 1f, DebtLedgerView.EaseOut(t / 0.07f));
                root.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            StartCoroutine(FadeAway(root.gameObject, 0.25f));
            _ = text;
        }

        private IEnumerator RibbonOver(Transform proxy, float duration)
        {
            var root = new GameObject("Ribbon").transform;
            root.SetParent(proxy, false);
            root.localRotation = Quaternion.Euler(0f, 0f, 24f);
            SpriteRenderer band = Sprite(root, "Band", DebtLedgerShapes.Ribbon, Style.Order + 25);
            SpriteRenderer seal = Sprite(root, "Seal", DebtLedgerShapes.Seal, Style.Order + 26);
            float t = 0f;
            while (t < duration)
            {
                t += Dt;
                float u = DebtLedgerView.EaseOut(t / duration);
                float len = 1.7f * u;
                DebtLedgerView.Place(band, new Vector2(-0.85f + len * 0.5f, 0f), len, 0.2f);
                band.color = DebtLedgerView.WithAlpha(Style.DriedRed, 0.92f);
                DebtLedgerView.Place(seal, new Vector2(-0.85f + len * 0.6f, 0f), 0.16f, 0.16f);
                seal.color = DebtLedgerView.WithAlpha(new Color(0.3f, 0.05f, 0.07f), u);
                yield return null;
            }
        }

        private IEnumerator ClosedSeal()
        {
            var root = new GameObject("ClosedSeal").transform;
            root.SetParent(ledger, false);
            root.localPosition = new Vector3(0f, 0.55f, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, -4f);
            SpriteRenderer plate = Sprite(root, "Plate", DebtLedgerShapes.StampSmall, Style.Order + 20);
            DebtLedgerView.Place(plate, Vector2.zero, 1.5f, 0.75f);
            TextMesh text = DebtLedgerView.Text(root, "Text", Loc.Pick("FILE CLOSED", "BORÇ KAPANDI"), 0.013f,
                DebtLedgerView.Style.Ink, Style.Order + 21, TextAnchor.MiddleCenter);
            float t = 0f;
            while (t < 0.22f)
            {
                t += Dt;
                float s = t < 0.12f ? Mathf.Lerp(1.5f, 0.95f, DebtLedgerView.EaseOut(t / 0.12f))
                    : Mathf.Lerp(0.95f, 1f, (t - 0.12f) / 0.1f);
                root.localScale = new Vector3(s, s, 1f);
                float a = Mathf.Clamp01(t / 0.05f);
                plate.color = DebtLedgerView.WithAlpha(DebtLedgerView.Style.GoldIvory, 0.95f * a);
                ViewUtil.SetTextColor(text, DebtLedgerView.WithAlpha(DebtLedgerView.Style.Ink, a));
                yield return null;
            }
            // the dossier warms back to neutral
            scrapMode = false;
            PaintLedger();
            yield return Wait(0.35f);
        }

        private IEnumerator SummaryHold(CreditStatement statement, int taken)
        {
            if (statement == null || ledger == null)
            {
                yield break;
            }
            long start = statement.DebtBeforeForeclosure > 0 ? statement.DebtBeforeForeclosure : owedShown;
            DebtLedgerView.SetText(summaryText, Loc.Pick(
                "FORECLOSURE DONE\nDEBT " + DebtLedgerView.Money(start) + " -> " + DebtLedgerView.Money(statement.DebtAfter)
                    + "\nTAKEN " + taken,
                "HACİZ SONU\nBORÇ " + DebtLedgerView.Money(start) + " -> " + DebtLedgerView.Money(statement.DebtAfter)
                    + "\nALINAN " + taken + " varlık"));
            PaintLedger();
            yield return Wait(Style.Summary);
        }

        private IEnumerator Exit()
        {
            // the drawer shuts, the dossier folds, the screen comes back
            float t = 0f;
            while (t < 0.15f && ledger != null)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / 0.15f);
                DebtLedgerView.Place(drawerShutter, new Vector2(0f, drawerOpening.transform.localPosition.y),
                    Style.LedgerWidth * 0.72f, 0.2f * u);
                yield return null;
            }
            t = 0f;
            Vector3 ledgerScale = ledger != null ? ledger.localScale : Vector3.one;
            while (t < Style.Fold)
            {
                t += Dt;
                float u = DebtLedgerView.EaseInOut(t / Style.Fold);
                if (ledger != null)
                {
                    ledger.localScale = new Vector3(ledgerScale.x, ledgerScale.y * (1f - u), 1f);
                }
                if (stampRoot != null)
                {
                    SetStampAlpha(1f - u);
                }
                dimK = 1f - u;
                vignetteK = 1f - u;
                yield return null;
            }
            if (BarsAlpha != null)
            {
                BarsAlpha(1f);
            }
            Emit(Cue.Complete);
            Stop();
        }

        // =================================================================== proxies

        private Transform BuildProxy(SeizedItem item, Vector2 at, out List<SpriteRenderer> renderers,
            out CardVisual card)
        {
            renderers = new List<SpriteRenderer>();
            card = null;
            var root = new GameObject("Proxy_" + item.Name).transform;
            root.SetParent(transform, false);
            root.position = new Vector3(at.x, at.y, 0f);
            int o = Style.Order + 20;
            if (item.Kind == SeizedKind.Joker || item.Kind == SeizedKind.Power)
            {
                bool joker = item.Kind == SeizedKind.Joker;
                Sprite frame = ViewUtil.CardSprite(joker ? "card_joker" : "card_power");
                SpriteRenderer shadow = Sprite(root, "Shadow", DebtLedgerShapes.Soft, o - 1);
                DebtLedgerView.Place(shadow, new Vector2(0.04f, -0.08f), 1.3f, 1.5f);
                shadow.color = new Color(0f, 0f, 0f, 0.4f);
                SpriteRenderer body = Sprite(root, "Frame", frame != null ? frame : ViewUtil.RoundedSprite, o);
                float h = joker ? 1.4f : 1.1f;
                float w = frame != null ? h * frame.bounds.size.x / frame.bounds.size.y : h * 0.72f;
                DebtLedgerView.Place(body, Vector2.zero, w, h);
                body.color = frame != null ? Color.white : new Color(0.2f, 0.18f, 0.2f);
                Sprite icon = joker ? ViewUtil.JokerIcon(item.DefId) : ViewUtil.PowerIcon(item.DefId);
                SpriteRenderer iconR = Sprite(root, "Icon", icon, o + 1);
                if (icon != null)
                {
                    float iw = w * 0.62f;
                    DebtLedgerView.Place(iconR, new Vector2(0f, h * 0.08f), iw, iw * icon.bounds.size.y / icon.bounds.size.x);
                    iconR.color = Color.white;
                }
                renderers.Add(body);
                renderers.Add(iconR);
                renderers.Add(shadow);
            }
            else
            {
                if (item.Card != null)
                {
                    card = CardVisual.Create(root, "Card", item.Card, true, false, Vector2.zero, o);
                    card.SetBaseScale(0.75f);
                }
            }
            return root;
        }

        private void Tint(List<SpriteRenderer> renderers, CardVisual card, float brightness, float alpha)
        {
            Color c = new Color(brightness, brightness, brightness, alpha);
            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer r = renderers[i];
                if (r == null || r.sprite == DebtLedgerShapes.Soft)
                {
                    continue;
                }
                r.color = c;
            }
            if (card != null)
            {
                // a card has no tint seam of its own; alpha is what it offers
                card.SetAlpha(Mathf.Lerp(0.55f, 1f, brightness));
            }
        }

        private static void SetMasked(List<SpriteRenderer> renderers, CardVisual card)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
            }
            if (card != null)
            {
                foreach (SpriteRenderer r in card.GetComponentsInChildren<SpriteRenderer>())
                {
                    r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
                foreach (MeshRenderer m in card.GetComponentsInChildren<MeshRenderer>())
                {
                    m.enabled = false; // text cannot be masked; the label goes with the lip
                }
            }
        }

        private List<SpriteRenderer> Corners(Transform proxy, Vector2 size)
        {
            var list = new List<SpriteRenderer>();
            for (int i = 0; i < 4; i++)
            {
                SpriteRenderer c = Sprite(proxy, "Corner" + i, DebtLedgerShapes.Corner, Style.Order + 24);
                float sx = i == 1 || i == 3 ? 1f : -1f;
                float sy = i >= 2 ? -1f : 1f;
                DebtLedgerView.Place(c, new Vector2(sx * size.x * 0.5f, sy * size.y * 0.5f), 0.22f, 0.22f);
                c.transform.localScale = new Vector3(-sx * Mathf.Abs(c.transform.localScale.x),
                    sy * Mathf.Abs(c.transform.localScale.y), 1f);
                list.Add(c);
            }
            if (Layers.ShowForeclosureSelectedAsset)
            {
                SpriteRenderer box = Sprite(proxy, "SelectedBox", ViewUtil.WhiteSprite, Style.Order + 23);
                DebtLedgerView.Place(box, Vector2.zero, size.x, size.y);
                box.color = new Color(0.4f, 1f, 0.9f, 0.18f);
                list.Add(box);
            }
            return list;
        }

        private static void SetCornersAlpha(List<SpriteRenderer> corners, float a)
        {
            for (int i = 0; i < corners.Count; i++)
            {
                if (corners[i].sprite == DebtLedgerShapes.Corner)
                {
                    corners[i].color = DebtLedgerView.WithAlpha(DebtLedgerView.Style.Brass, a);
                }
            }
        }

        private static void DestroyAll(List<SpriteRenderer> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    Destroy(list[i].gameObject);
                }
            }
        }

        private IEnumerator DrawerKick()
        {
            if (drawerLip == null)
            {
                yield break;
            }
            Vector3 home = drawerLip.transform.localPosition;
            float t = 0f;
            while (t < 0.09f && drawerLip != null)
            {
                t += Dt;
                drawerLip.transform.localPosition = home + new Vector3(0f, -0.018f * Mathf.Sin(t / 0.09f * Mathf.PI), 0f);
                yield return null;
            }
            if (drawerLip != null)
            {
                drawerLip.transform.localPosition = home;
            }
        }

        private IEnumerator FadeAway(GameObject go, float duration)
        {
            var sprites = go.GetComponentsInChildren<SpriteRenderer>();
            var texts = go.GetComponentsInChildren<TextMesh>();
            var start = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                start[i] = sprites[i].color;
            }
            float t = 0f;
            while (t < duration && go != null)
            {
                t += Dt;
                float a = 1f - Mathf.Clamp01(t / duration);
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] != null)
                    {
                        sprites[i].color = DebtLedgerView.WithAlpha(start[i], start[i].a * a);
                    }
                }
                foreach (TextMesh m in texts)
                {
                    if (m != null && m.transform.parent == go.transform)
                    {
                        ViewUtil.SetTextColor(m, DebtLedgerView.WithAlpha(m.color, a));
                    }
                }
                yield return null;
            }
            if (go != null)
            {
                Destroy(go);
            }
        }

        private void Fibres(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                StartCoroutine(Fibre(at, i));
            }
        }

        private IEnumerator Fibre(Vector2 at, int i)
        {
            var go = new GameObject("Fibre");
            go.transform.SetParent(transform, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = DebtLedgerShapes.Fleck;
            r.sortingOrder = Style.Order + 32;
            float a = Hash(i * 31 + 7) * Mathf.PI * 2f;
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (2.5f + 2f * Hash(i * 17 + 3));
            Vector2 p = at + new Vector2(Mathf.Cos(a) * 2.5f, Mathf.Sin(a) * 0.8f);
            float t = 0f;
            float life = 0.4f + 0.2f * Hash(i + 11);
            Color colour = i % 3 == 0 ? DebtLedgerView.Style.Cream : Style.DriedRed;
            while (t < life)
            {
                float dt = Dt;
                t += dt;
                v *= Mathf.Exp(-5f * dt);
                v += Vector2.down * 2f * dt;
                p += v * dt;
                go.transform.position = new Vector3(p.x, p.y, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 400f * t * (i % 2 == 0 ? 1f : -1f));
                float s = 0.07f;
                go.transform.localScale = new Vector3(s / r.sprite.bounds.size.x, s * 0.5f / r.sprite.bounds.size.y, 1f);
                r.color = DebtLedgerView.WithAlpha(colour, 1f - t / life);
                yield return null;
            }
            Destroy(go);
        }

        // =================================================================== atmosphere

        private void LateUpdate()
        {
            PaintAtmosphere();
        }

        private void PaintAtmosphere()
        {
            if (dim == null || cam == null)
            {
                return;
            }
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            dim.transform.position = new Vector3(c.x, c.y, 0f);
            DebtLedgerView.Place(dim, Vector2.zero, w * 1.1f, h * 1.1f);
            dim.transform.position = new Vector3(c.x, c.y, 0f);
            dim.color = DebtLedgerView.WithAlpha(Style.Dim, Style.DimAlpha * dimK);
            DebtLedgerView.Place(vignette, Vector2.zero, w * 1.15f * (1f - vignettePunch * 0.3f),
                h * 1.15f * (1f - vignettePunch * 0.3f));
            vignette.transform.position = new Vector3(c.x, c.y, 0f);
            vignette.color = DebtLedgerView.WithAlpha(Color.black, Mathf.Clamp01(Style.VignetteAlpha * vignetteK + vignettePunch));
        }

        /// <summary>The final-due atmosphere: a whisper of vignette and dim while the last stage
        /// of a term is being played. 0 turns it off. Only while no foreclosure is running.</summary>
        public void SetFinalDueAtmosphere(float k)
        {
            if (Playing)
            {
                return;
            }
            // +3-6% vignette, a few percent of dim - "something is closing in", nothing more.
            dimK = 0.12f * k;
            vignetteK = 0.1f * k;
        }

        // =================================================================== debug

        private void PaintQueue(CreditStatement statement, int current)
        {
            bool dev = UnityEngine.Debug.isDebugBuild || Application.isEditor;
            if (statement == null || !dev || (!Layers.ShowForeclosureQueue && !Layers.ShowRemainingAssetCount))
            {
                if (queueText != null)
                {
                    DebtLedgerView.SetText(queueText, " ");
                }
                return;
            }
            if (queueText == null)
            {
                queueText = DebtLedgerView.Text(transform, "Queue", " ", 0.008f, new Color(0.6f, 1f, 0.9f),
                    Style.Order + 40, TextAnchor.UpperLeft);
            }
            Vector2 at = cam != null ? (Vector2)cam.ViewportToWorldPoint(new Vector3(0.02f, 0.62f, 0f)) : Vector2.zero;
            queueText.transform.position = new Vector3(at.x, at.y, 0f);
            var sb = new System.Text.StringBuilder();
            if (Layers.ShowForeclosureQueue)
            {
                for (int i = 0; i < statement.Seized.Count && i < 18; i++)
                {
                    SeizedItem s = statement.Seized[i];
                    sb.Append(i == current ? "> " : "  ").Append('#').Append(i + 1).Append(' ')
                        .Append(s.Kind).Append(' ').Append(s.Name).Append("  normal ").Append(s.Value)
                        .Append("  seizure ").Append(s.Credited).Append('\n');
                }
            }
            if (Layers.ShowRemainingAssetCount)
            {
                sb.Append("remaining ").Append(Mathf.Max(0, statement.Seized.Count - current - 1));
            }
            DebtLedgerView.SetText(queueText, sb.ToString());
        }

        private void DebugMark(Vector2 at, Color colour, bool on)
        {
            if (!on)
            {
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer r = Sprite(transform, "Mark", ViewUtil.WhiteSprite, Style.Order + 41);
                DebtLedgerView.Place(r, at, i == 0 ? 0.3f : 0.02f, i == 0 ? 0.02f : 0.3f);
                r.color = colour;
                debugMarks.Add(r.gameObject);
            }
        }

        private void DebugPath(Vector2 a, Vector2 c, Vector2 b, Color colour)
        {
            for (int i = 0; i <= 12; i++)
            {
                SpriteRenderer r = Sprite(transform, "PathDot", DebtLedgerShapes.Soft, Style.Order + 41);
                DebtLedgerView.Place(r, DebtLedgerView.Bezier(a, c, b, i / 12f), 0.07f, 0.07f);
                r.color = colour;
                debugMarks.Add(r.gameObject);
            }
        }

        private void ClearDebug()
        {
            for (int i = 0; i < debugMarks.Count; i++)
            {
                if (debugMarks[i] != null)
                {
                    Destroy(debugMarks[i]);
                }
            }
            debugMarks.Clear();
            if (queueText != null)
            {
                Destroy(queueText.gameObject);
                queueText = null;
            }
        }

        // =================================================================== plumbing

        private float Dt
        {
            get { return Time.deltaTime * PlaybackRate; }
        }

        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Dt;
                yield return null;
            }
        }

        private void Emit(Cue cue)
        {
            if (Sounded != null)
            {
                Sounded(cue);
            }
        }

        private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Color.clear;
            return r;
        }

        private static float Hash(int key)
        {
            unchecked
            {
                uint h = (uint)(key * 73856093) ^ 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
