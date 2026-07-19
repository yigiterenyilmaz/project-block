// PURPOSE: In-editor debug overlay for DECIDING each joker/power's rarity by browsing the
// real registries and reading their live in-game descriptions. Press F2 in Play mode to
// open. It is a pure authoring tool: it records a common/rare/legendary choice per item to
// Tools/RarityGrader/rarities.json and does NOTHING to gameplay, pricing or shop odds
// (that wiring is a later task). Its auto-injection is guarded by UNITY_EDITOR, so it only
// ever spawns in the editor and never activates in a player build.
//
// It reads Core (JokerRegistry/PowerRegistry, Loc) but owns no rules - like the rest of
// Assets/Scripts/View, this is disposable presentation. While open it disables the scene's
// GameUiController so its keys/clicks don't bleed through; closing re-enables it.
//
// The on-disk format matches the browser grader's rarities.json:
//   { "version":1, "tiers":[...], "gradedAt":"...", "assignments": { "<defId>":"<tier>" } }
// keyed by the stable DefId, so both stay interchangeable.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    /// <summary>Self-injecting F2 overlay to grade joker/power rarities (editor-only).</summary>
    public sealed class RarityGraderView : MonoBehaviour
    {
        private const string Common = "common";
        private const string Rare = "rare";
        private const string Legendary = "legendary";

        private static readonly Color ColCommon = new Color(0.23f, 0.51f, 0.96f);
        private static readonly Color ColRare = new Color(0.94f, 0.27f, 0.27f);
        private static readonly Color ColLegend = new Color(0.92f, 0.70f, 0.05f);
        private static readonly Color ColUngraded = new Color(0.28f, 0.30f, 0.35f);
        private static readonly Color Ink = new Color(0.93f, 0.95f, 0.98f);
        private static readonly Color Faint = new Color(0.62f, 0.67f, 0.74f);

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInject()
        {
            if (FindAnyObjectByType<RarityGraderView>() != null)
            {
                return;
            }
            var go = new GameObject("RarityGraderView");
            go.AddComponent<RarityGraderView>();
        }
#endif

        private Camera cam;
        private GameUiController controller;
        private bool open;

        // 0 = jokers tab, 1 = powers tab.
        private int tab;
        private readonly int[] sel = { 0, 0 };
        private readonly int[] scroll = { 0, 0 };

        private readonly Dictionary<string, string> assignments = new Dictionary<string, string>();
        private Vector2 center;
        private int visibleRows = 12;

        // Hit rects (in local space, i.e. world minus camera center) rebuilt each frame we draw.
        private readonly List<RowHit> rowHits = new List<RowHit>();
        private readonly Rect[] swatchHits = new Rect[3];
        private Vector2 lastMouseScreen;

        private struct RowHit
        {
            public int Index;
            public float YCenter;
            public float XMin;
            public float XMax;
            public float Height;
        }

        private void Awake()
        {
            cam = Camera.main;
            Load();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }
            if (kb.f2Key.wasPressedThisFrame)
            {
                Toggle();
                return;
            }
            if (!open)
            {
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (kb.tabKey.wasPressedThisFrame
                || kb.leftArrowKey.wasPressedThisFrame
                || kb.rightArrowKey.wasPressedThisFrame)
            {
                tab = 1 - tab;
                Rebuild();
                return;
            }
            if (kb.downArrowKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame)
            {
                Move(1);
                return;
            }
            if (kb.upArrowKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame)
            {
                Move(-1);
                return;
            }
            if (kb.digit1Key.wasPressedThisFrame) { GradeSelected(Common); return; }
            if (kb.digit2Key.wasPressedThisFrame) { GradeSelected(Rare); return; }
            if (kb.digit3Key.wasPressedThisFrame) { GradeSelected(Legendary); return; }
            if (kb.digit0Key.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame)
            {
                ClearSelected();
                return;
            }
            HandleMouse();
        }

        private void Toggle()
        {
            if (open) Close(); else Open();
        }

        private void Open()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
            if (cam == null)
            {
                Debug.LogWarning("[RarityGrader] No main camera; cannot open.");
                return;
            }
            open = true;
            controller = FindFirstObjectByType<GameUiController>();
            if (controller != null)
            {
                controller.enabled = false; // stop the game UI eating our keys/clicks
            }
            center = cam.transform.position;
            Rebuild();
        }

        private void Close()
        {
            open = false;
            ClearChildren();
            if (controller != null)
            {
                controller.enabled = true;
                controller = null;
            }
        }

        // ------------------------------------------------------------------ data access

        private int Count()
        {
            return tab == 0 ? JokerRegistry.All.Count : PowerRegistry.All.Count;
        }

        private string DefIdAt(int i)
        {
            return tab == 0 ? JokerRegistry.All[i].DefId : PowerRegistry.All[i].DefId;
        }

        private string NameAt(int i)
        {
            return tab == 0 ? JokerRegistry.All[i].DisplayName : PowerRegistry.All[i].DisplayName;
        }

        private string DescAt(int i)
        {
            return tab == 0 ? JokerRegistry.All[i].Description : PowerRegistry.All[i].Description;
        }

        private bool PreLegendaryAt(int i)
        {
            return tab == 0 && JokerRegistry.All[i].IsLegendary;
        }

        private static Color TierColor(string tier)
        {
            switch (tier)
            {
                case Common: return ColCommon;
                case Rare: return ColRare;
                case Legendary: return ColLegend;
                default: return ColUngraded;
            }
        }

        // ------------------------------------------------------------------ actions

        private void Move(int delta)
        {
            int n = Count();
            sel[tab] = Mathf.Clamp(sel[tab] + delta, 0, n - 1);
            EnsureVisible();
            Rebuild();
        }

        private void GradeSelected(string tier)
        {
            assignments[DefIdAt(sel[tab])] = tier;
            Save();
            // Auto-advance so grading a long list is a rhythm of 1/2/3, 1/2/3...
            if (sel[tab] < Count() - 1)
            {
                sel[tab]++;
                EnsureVisible();
            }
            Rebuild();
        }

        private void ClearSelected()
        {
            assignments.Remove(DefIdAt(sel[tab]));
            Save();
            Rebuild();
        }

        private void EnsureVisible()
        {
            if (sel[tab] < scroll[tab])
            {
                scroll[tab] = sel[tab];
            }
            else if (sel[tab] >= scroll[tab] + visibleRows)
            {
                scroll[tab] = sel[tab] - visibleRows + 1;
            }
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }
            Vector2 screen = mouse.position.ReadValue();
            Vector2 world = cam.ScreenToWorldPoint(screen);
            Vector2 local = world - center;

            // Hover selects the row under the cursor, but only when the mouse actually moved,
            // so it never fights keyboard navigation.
            if ((screen - lastMouseScreen).sqrMagnitude > 1f)
            {
                lastMouseScreen = screen;
                for (int i = 0; i < rowHits.Count; i++)
                {
                    RowHit h = rowHits[i];
                    if (local.x >= h.XMin && local.x <= h.XMax
                        && Mathf.Abs(local.y - h.YCenter) <= h.Height * 0.5f)
                    {
                        if (sel[tab] != h.Index)
                        {
                            sel[tab] = h.Index;
                            Rebuild();
                        }
                        break;
                    }
                }
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                for (int t = 0; t < 3; t++)
                {
                    if (swatchHits[t].Contains(local))
                    {
                        GradeSelected(t == 0 ? Common : t == 1 ? Rare : Legendary);
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ drawing

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            rowHits.Clear();
        }

        private void Rebuild()
        {
            ClearChildren();
            transform.position = new Vector3(center.x, center.y, 0f);

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            // Full-screen dim.
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero,
                new Vector2(halfW * 2f + 4f, halfH * 2f + 4f), new Color(0f, 0f, 0f, 0.86f), 100);

            float top = halfH - 0.55f;
            float bottom = -halfH + 0.75f;

            // Title + tabs.
            string title = Loc.Pick("RARITY GRADER (F2)", "NADİRLİK NOTU (F2)");
            ViewUtil.MakeText3D(transform, "Title", new Vector2(-halfW + 0.5f, top),
                title, 60, 0.03f, Color.white, 102, TextAnchor.MiddleLeft);

            int graded = assignments.Count;
            int total = JokerRegistry.All.Count + PowerRegistry.All.Count;
            ViewUtil.MakeText3D(transform, "Prog", new Vector2(halfW - 0.5f, top),
                Loc.Pick("graded ", "notlanan ") + graded + " / " + total, 60, 0.03f,
                Ink, 102, TextAnchor.MiddleRight);

            float tabsY = top - 0.55f;
            DrawTab(new Vector2(-halfW + 0.5f, tabsY), Loc.Pick("JOKERS", "JOKERLER"), 0);
            DrawTab(new Vector2(-halfW + 3.4f, tabsY), Loc.Pick("POWERS", "GÜÇLER"), 1);

            // List column geometry.
            float listTop = tabsY - 0.7f;
            float rowH = 0.6f;
            visibleRows = Mathf.Max(1, Mathf.FloorToInt((listTop - bottom) / rowH));
            float listXMin = -halfW + 0.4f;
            float listXMax = -0.4f;
            float nameX = listXMin + 0.5f;

            int n = Count();
            EnsureVisible();
            int start = scroll[tab];
            int end = Mathf.Min(n, start + visibleRows);

            for (int i = start; i < end; i++)
            {
                float y = listTop - (i - start) * rowH;
                bool selected = i == sel[tab];
                string tier;
                assignments.TryGetValue(DefIdAt(i), out tier);

                if (selected)
                {
                    ViewUtil.MakeRect(transform, "Selbg", new Vector2((listXMin + listXMax) * 0.5f, y),
                        new Vector2(listXMax - listXMin, rowH * 0.92f),
                        new Color(1f, 1f, 1f, 0.12f), 101);
                }
                // rarity swatch
                ViewUtil.MakeRect(transform, "Sw_" + i, new Vector2(listXMin + 0.22f, y),
                    new Vector2(0.26f, rowH * 0.6f), TierColor(tier), 102);
                // name (dim if ungraded)
                ViewUtil.MakeText3D(transform, "Nm_" + i, new Vector2(nameX, y),
                    NameAt(i), 90, 0.014f, selected ? Color.white : (tier == null ? Faint : Ink),
                    103, TextAnchor.MiddleLeft);

                rowHits.Add(new RowHit
                {
                    Index = i, YCenter = y, XMin = listXMin, XMax = listXMax, Height = rowH
                });
            }

            if (start > 0)
            {
                ViewUtil.MakeText3D(transform, "Up", new Vector2(nameX, listTop + 0.32f),
                    "^ " + start + Loc.Pick(" above", " yukarıda"), 60, 0.02f, Faint, 103,
                    TextAnchor.MiddleLeft);
            }
            if (end < n)
            {
                ViewUtil.MakeText3D(transform, "Dn", new Vector2(nameX, bottom - 0.05f),
                    "v " + (n - end) + Loc.Pick(" below", " aşağıda"), 60, 0.02f, Faint, 103,
                    TextAnchor.MiddleLeft);
            }

            DrawDetail(new Vector2(0.2f, listTop + 0.2f), new Vector2(halfW - 0.6f, bottom));
            DrawFooter(new Vector2(0f, -halfH + 0.35f), halfW);
        }

        private void DrawTab(Vector2 pos, string label, int which)
        {
            bool on = tab == which;
            ViewUtil.MakeRect(transform, "Tab_" + which, new Vector2(pos.x + 1.15f, pos.y),
                new Vector2(2.6f, 0.5f), on ? new Color(0.22f, 0.26f, 0.34f) : new Color(0.13f, 0.15f, 0.19f),
                101);
            ViewUtil.MakeText3D(transform, "TabT_" + which, new Vector2(pos.x + 1.15f, pos.y),
                label, 70, 0.02f, on ? Color.white : Faint, 102, TextAnchor.MiddleCenter);
        }

        private void DrawDetail(Vector2 topLeft, Vector2 bottomRight)
        {
            int i = sel[tab];
            if (i < 0 || i >= Count())
            {
                return;
            }
            float xL = topLeft.x;
            float xR = bottomRight.x;
            float y = topLeft.y;
            float width = xR - xL;

            string tier;
            assignments.TryGetValue(DefIdAt(i), out tier);

            ViewUtil.MakeRect(transform, "DetBg", new Vector2((xL + xR) * 0.5f, (topLeft.y + bottomRight.y) * 0.5f),
                new Vector2(width + 0.2f, topLeft.y - bottomRight.y + 0.2f),
                new Color(0.10f, 0.12f, 0.15f, 0.9f), 101);

            ViewUtil.MakeText3D(transform, "DName", new Vector2(xL, y), NameAt(i),
                80, 0.024f, Color.white, 103, TextAnchor.MiddleLeft);
            y -= 0.5f;

            string kind = tab == 0 ? Loc.Pick("joker", "joker") : Loc.Pick("power", "güç");
            string tierLabel = tier == null ? Loc.Pick("ungraded", "notsuz") : tier;
            ViewUtil.MakeText3D(transform, "DKind", new Vector2(xL, y),
                kind + "   -   " + DefIdAt(i), 70, 0.016f, Faint, 103, TextAnchor.MiddleLeft);
            ViewUtil.MakeText3D(transform, "DTier", new Vector2(xR, y), tierLabel.ToUpperInvariant(),
                75, 0.02f, TierColor(tier), 103, TextAnchor.MiddleRight);
            y -= 0.2f;

            if (PreLegendaryAt(i))
            {
                y -= 0.35f;
                ViewUtil.MakeText3D(transform, "DLock", new Vector2(xL, y),
                    Loc.Pick("already legendary in-game (one held at a time)",
                        "oyunda zaten efsanevi (aynı anda bir tane)"),
                    60, 0.016f, ColLegend, 103, TextAnchor.MiddleLeft);
            }
            y -= 0.55f;

            int wrap = Mathf.Max(20, Mathf.FloorToInt(width / 0.16f));
            string desc = ViewUtil.WrapText(DescAt(i), wrap);
            ViewUtil.MakeText3D(transform, "DDesc", new Vector2(xL, y), desc,
                80, 0.018f, Ink, 103, TextAnchor.UpperLeft);

            // Clickable rarity swatches along the bottom of the detail panel.
            float sy = bottomRight.y + 0.55f;
            DrawSwatch(0, new Vector2(xL + 0.9f, sy), Loc.Pick("1 Common", "1 Yaygın"), Common, tier);
            DrawSwatch(1, new Vector2(xL + 3.1f, sy), Loc.Pick("2 Rare", "2 Nadir"), Rare, tier);
            DrawSwatch(2, new Vector2(xL + 5.3f, sy), Loc.Pick("3 Legendary", "3 Efsanevi"), Legendary, tier);
        }

        private void DrawSwatch(int slot, Vector2 pos, string label, string tier, string current)
        {
            bool on = current == tier;
            Color c = TierColor(tier);
            var size = new Vector2(2.0f, 0.5f);
            ViewUtil.MakeRect(transform, "SwBtn_" + slot, pos, size,
                on ? c : new Color(c.r, c.g, c.b, 0.35f), 102);
            ViewUtil.MakeText3D(transform, "SwLbl_" + slot, pos, label, 60, 0.016f,
                on ? Color.black : Ink, 103, TextAnchor.MiddleCenter);
            swatchHits[slot] = new Rect(pos.x - size.x * 0.5f, pos.y - size.y * 0.5f, size.x, size.y);
        }

        private void DrawFooter(Vector2 pos, float halfW)
        {
            int c = 0, r = 0, l = 0;
            foreach (var kv in assignments)
            {
                if (kv.Value == Common) c++;
                else if (kv.Value == Rare) r++;
                else if (kv.Value == Legendary) l++;
            }
            ViewUtil.MakeText3D(transform, "Counts", new Vector2(-halfW + 0.5f, pos.y),
                "C " + c + "   R " + r + "   L " + l, 60, 0.018f, Faint, 102, TextAnchor.MiddleLeft);
            ViewUtil.MakeText3D(transform, "Keys", new Vector2(halfW - 0.5f, pos.y),
                Loc.Pick("1/2/3 grade · 0 clear · Tab jokers/powers · arrows move · Esc close · auto-saved",
                    "1/2/3 not · 0 sil · Tab joker/güç · oklar gez · Esc kapat · otomatik kaydedilir"),
                55, 0.016f, Faint, 102, TextAnchor.MiddleRight);
        }

        // ------------------------------------------------------------------ persistence

        private static string RaritiesPath()
        {
            // Application.dataPath is <project>/Assets in the editor; the record lives beside
            // the browser grader under Tools/RarityGrader so both share one source of truth.
            DirectoryInfo root = Directory.GetParent(Application.dataPath);
            if (root != null)
            {
                string toolPath = Path.Combine(root.FullName, "Tools", "RarityGrader", "rarities.json");
                if (Directory.Exists(Path.GetDirectoryName(toolPath)))
                {
                    return toolPath;
                }
            }
            return Path.Combine(Application.persistentDataPath, "rarities.json");
        }

        private void Load()
        {
            assignments.Clear();
            string path = RaritiesPath();
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                Match block = Regex.Match(text, "\"assignments\"\\s*:\\s*\\{([^}]*)\\}");
                if (block.Success)
                {
                    foreach (Match m in Regex.Matches(block.Groups[1].Value,
                        "\"(\\w+)\"\\s*:\\s*\"(\\w+)\""))
                    {
                        assignments[m.Groups[1].Value] = m.Groups[2].Value;
                    }
                }
            }
            // Seed the game's already-legendary jokers if the record has nothing for them.
            foreach (JokerDefinition def in JokerRegistry.All)
            {
                if (def.IsLegendary && !assignments.ContainsKey(def.DefId))
                {
                    assignments[def.DefId] = Legendary;
                }
            }
        }

        private void Save()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": 1,\n");
            sb.Append("  \"tiers\": [\n    \"common\",\n    \"rare\",\n    \"legendary\"\n  ],\n");
            sb.Append("  \"gradedAt\": \"")
              .Append(System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))
              .Append("\",\n");
            sb.Append("  \"assignments\": {");

            // Write in registry order for stable, review-friendly diffs.
            var ordered = new List<string>();
            foreach (JokerDefinition d in JokerRegistry.All)
            {
                if (assignments.ContainsKey(d.DefId)) ordered.Add(d.DefId);
            }
            foreach (PowerDefinition d in PowerRegistry.All)
            {
                if (assignments.ContainsKey(d.DefId)) ordered.Add(d.DefId);
            }
            for (int i = 0; i < ordered.Count; i++)
            {
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    \"").Append(ordered[i]).Append("\": \"")
                  .Append(assignments[ordered[i]]).Append("\"");
            }
            sb.Append(ordered.Count > 0 ? "\n  }\n" : "}\n");
            sb.Append("}\n");

            try
            {
                File.WriteAllText(RaritiesPath(), sb.ToString(), new UTF8Encoding(false));
            }
            catch (IOException e)
            {
                Debug.LogWarning("[RarityGrader] Could not save: " + e.Message);
            }
        }
    }
}
