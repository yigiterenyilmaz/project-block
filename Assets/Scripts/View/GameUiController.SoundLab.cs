// PURPOSE: The SOUND LAB (F9) - every sound effect in the game, GROUPED, on the same panel as the
// animation lab (AnimationLabView, a second instance with its own title and remembered place).
//
// BUILT FOR COMPARING FAST. Every row is one sound with its VERSIONS laid side by side as buttons
// - the four sets (OLD, NEW, V3, MIX) for the rebuilt cues, the original and variants A/B/C for the
// recommended ones, the candidates for the line clear:
//   - CLICK a version button to play it. RIGHT CLICK (or shift+click) it to also make it the one
//     the game uses - for the rebuilt cues that is the whole SET, which is global; for a
//     recommended cue it is that cue alone. The game's version is GREEN, the last played AMBER.
//   - Clicking the row's name plays the game's version; right clicking it plays them all in turn.
//   - The keys work too: point at a row, 1..9 plays a version, shift+1..9 picks it; space replays.
//   - The knobs (context: block size, lines, combo, the sweep's place in the round, MIX's thump and
//     blast, the gap between versions) live in the SETTINGS group at the bottom and are hidden
//     until it is opened, so the list gets the room the rest of the time.
// Groups open and close by clicking their header. F9 or Esc closes the lab, shift+F9 puts the
// panel back. It goes through SoundFx's own public methods, so what is heard is what the game
// plays. Like the animation lab it owns the whole frame while open.
// EXTENSION POINT: a new cue is one line in BuildSoundCatalogue.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private AnimationLabView soundLab;

        /// <summary>One sound and its versions: how to play version i, and - when the game has a
        /// choice between them - which one it uses and how to change that.</summary>
        private sealed class SoundEntry
        {
            public string Group;
            public string En;
            public string Tr;
            public string[] Versions;
            public Func<int, IEnumerator> Play;
            public Func<int> GameVersion;       // null: the game has no choice here
            public Action<int> SetGameVersion;
        }

        private readonly List<SoundEntry> soundEntries = new List<SoundEntry>();
        private readonly List<string> soundGroupOrder = new List<string>();
        private readonly Dictionary<string, string> soundGroupTr = new Dictionary<string, string>();

        /// <summary>A sub-group's parent (a group absent here is top level). Sub-groups sit
        /// under their parent's own rows, a level deeper.</summary>
        private readonly Dictionary<string, string> soundGroupParent = new Dictionary<string, string>();
        private readonly HashSet<string> soundOpenGroups = new HashSet<string>();
        private readonly List<AnimationLabView.Row> soundRows = new List<AnimationLabView.Row>();
        private readonly List<int> soundRowEntry = new List<int>();     // -1 on a header
        private readonly List<string> soundRowGroup = new List<string>();

        private int soundSelected;
        private int soundScroll;
        private bool soundDragging;
        private Vector2 soundDragGrab;
        private SoundEntry soundLastEntry;
        private int soundLastVersion = -1;      // -1: "every version in turn"
        private string soundLastPlayed = string.Empty;
        private Coroutine soundRunning;

        // Knob state.
        private int soundCubes = 3;
        private int soundLines = 1;
        private int soundCombo = 1;
        private int soundSweeps = 1;
        private float soundGap = 0.6f;

        private bool SoundLabOpen
        {
            get { return soundLab != null && soundLab.IsOpen; }
        }

        private void EnsureSoundLab()
        {
            if (soundLab != null)
            {
                return;
            }
            var go = new GameObject("SoundLab");
            go.transform.SetParent(transform, false);
            soundLab = go.AddComponent<AnimationLabView>();
            soundLab.PrefsKey = "soundlab";
            soundLab.TitleEn = "SOUND LAB (F9)";
            soundLab.TitleTr = "SES LABI (F9)";
            soundLab.HelpEn = "click a version to play  -  right-click it: the game uses it  -  1-9 keys too";
            soundLab.HelpTr = "sürüme tıkla: çal  -  sağ tık: oyunda kullan  -  1-9 tuşları da";
        }

        private void OpenSoundLab()
        {
            EnsureSoundLab();
            BuildSoundCatalogue();
            soundSelected = 0;
            soundScroll = 0;
            soundDragging = false;
            soundLastEntry = null;
            soundLastPlayed = string.Empty;
            HideTooltip();
            // The joker strip is on the screen-space HUD and draws over a world-space panel.
            jokerBar.SetVisible(false);
            RebuildSoundRows();
            RedrawSoundLab();
        }

        private void CloseSoundLab()
        {
            soundLab.Hide();
            soundDragging = false;
            jokerBar.SetVisible(true);
        }

        // ------------------------------------------------------------------ catalogue

        private void SoundGroup(string en, string tr)
        {
            soundGroupOrder.Add(en);
            soundGroupTr[en] = tr;
        }

        private void SoundSubGroup(string parent, string en, string tr)
        {
            SoundGroup(en, tr);
            soundGroupParent[en] = parent;
        }

        /// <summary>A missing / weak sound's prototypes: takes A, B, C, not wired into the game
        /// yet, so there is no game version to mark.</summary>
        private void AddProto(string group, ProtoCue cue)
        {
            int family;
            string en, tr;
            SoundFx.ProtoInfo(cue, out family, out en, out tr);
            AddVersions(group, en, tr, SoundFx.ProtoVersionNames, v => sfx.AuditionProto(cue, v));
        }

        private static IEnumerator Once(Action a)
        {
            a();
            yield break;
        }

        /// <summary>A rebuilt cue: its versions are the four SETS, and the game's choice is the
        /// global set.</summary>
        private void AddSetCue(string group, string en, string tr, Action<SoundFx> cue)
        {
            AddSetSequence(group, en, tr, set => Once(() => sfx.Audition(set, cue)));
        }

        private void AddSetSequence(string group, string en, string tr, Func<int, IEnumerator> seq)
        {
            soundEntries.Add(new SoundEntry
            {
                Group = group, En = en, Tr = tr, Versions = SoundFx.SetNames, Play = seq,
                GameVersion = () => sfx.Set, SetGameVersion = v => sfx.Set = v,
            });
        }

        /// <summary>A recommended cue: the original and its variants, chosen per cue.</summary>
        private void AddRecCue(string group, string en, string tr, RecCue cue, float pitch)
        {
            soundEntries.Add(new SoundEntry
            {
                Group = group, En = en, Tr = tr, Versions = SoundFx.RecVersionNames,
                Play = v => Once(() => sfx.AuditionRec(cue, v, pitch)),
                GameVersion = () => sfx.RecChoice(cue), SetGameVersion = v => sfx.SetRecChoice(cue, v),
            });
        }

        /// <summary>A row of named versions with no game choice behind it (layers, archives).
        /// </summary>
        private void AddVersions(string group, string en, string tr, string[] names, Action<int> play)
        {
            soundEntries.Add(new SoundEntry
            {
                Group = group, En = en, Tr = tr, Versions = names, Play = v => Once(() => play(v)),
            });
        }

        /// <summary>Every cue, grouped.</summary>
        private void BuildSoundCatalogue()
        {
            soundEntries.Clear();
            soundGroupOrder.Clear();
            soundGroupTr.Clear();
            soundGroupParent.Clear();

            const string place = "Placing a block";
            SoundGroup(place, "Blok yerleştirme");
            AddSetCue(place, "place", "yerleştir", s => s.Place(soundCubes));
            AddSetCue(place, "card pickup", "kart kaldırma", s => s.Pickup());
            AddSetCue(place, "rejected drop", "reddedilen bırakma", s => s.Reject());
            AddSetSequence(place, "pickup > place", "kaldır > yerleştir", PickupThenPlaceSeq);
            AddSetSequence(place, "placing streak 1..5", "yerleştirme serisi 1..5", PlaceStreakSeq);

            const string lines = "Lines";
            SoundGroup(lines, "Satırlar");
            AddSetCue(lines, "row / column clear", "satır / sütun temizliği", s => s.Explode(soundLines, soundCombo));
            AddSetSequence(lines, "a turn: place > clear", "bir tur: yerleştir > temizle", PlaceThenClearSeq);
            AddSetSequence(lines, "combo climb 1..6", "kombo tırmanışı 1..6", ComboClimbSeq);
            AddSetSequence(lines, "one, two, three lines", "bir, iki, üç satır", LineTiersSeq);
            AddVersions(lines, "MIX clear, layer by layer", "MIX temizliği, katman katman",
                new[] { "TOP", "THUMP", "BED", "ALL" },
                v =>
                {
                    if (v < 3) { sfx.AuditionLineLayer(v, soundLines); }
                    else { sfx.Audition(3, s => s.Explode(soundLines, soundCombo)); }
                });

            const string sweep = "Clean sweep";
            SoundGroup(sweep, "Temizlik");
            AddSetCue(sweep, "sweep", "temizlik", s => s.CleanSweep(1f + 0.12f * Mathf.Min(soundSweeps - 1, 8)));
            AddSetSequence(sweep, "a round's sweeps 1..5", "rauntun temizlikleri 1..5", SweepClimbSeq);
            AddSetSequence(sweep, "place > sweep > flame", "yerleştir > temizlik > alev", PlaceThenSweepSeq);

            const string cards = "Cards & market";
            SoundGroup(cards, "Kartlar ve market");
            AddSetCue(cards, "shuffle", "karıştırma", s => s.Shuffle());
            AddSetCue(cards, "buy", "satın alma", s => s.Buy());
            AddSetCue(cards, "vanish", "yok olma", s => s.Vanish());
            AddSetCue(cards, "fusion / copy", "füzyon / kopya", s => s.Fusion());
            AddSetCue(cards, "chime, low", "çınlama, pes", s => s.Chime(0.75f));
            AddSetCue(cards, "chime, high", "çınlama, tiz", s => s.Chime(1.4f));

            const string fire = "Fire";
            SoundGroup(fire, "Ateş");
            AddSetCue(fire, "flame", "alev", s => s.Flame());
            AddSetCue(fire, "fuse, first charge", "fitil, ilk yük", s => s.Fuse(0.1f));
            AddSetCue(fire, "fuse, about to go", "fitil, patlamak üzere", s => s.Fuse(1f));

            // Still placeholder audio in every set until a variant is picked here.
            const string powers = "Recommended: power effects";
            SoundGroup(powers, "Öneri: güç efektleri");
            AddRecCue(powers, "cut (Neşter)", "kesik (Neşter)", RecCue.Cut, 1f);
            AddRecCue(powers, "drum (totem)", "davul (totem)", RecCue.Drum, 1f);
            AddRecCue(powers, "rumble (earth)", "gürleme (toprak)", RecCue.Rumble, 1f);
            AddRecCue(powers, "leaf pluck (İkinci Şans)", "yaprak (İkinci Şans)", RecCue.Pluck, 1f);
            AddRecCue(powers, "whoosh", "hışırtı", RecCue.Whoosh, 1f);
            AddRecCue(powers, "squish (deflate)", "ezilme (sönme)", RecCue.Squish, 1f);
            AddRecCue(powers, "stretch (inflate)", "gerilme (şişme)", RecCue.Stretch, 1f);

            const string stings = "Recommended: boss stings";
            SoundGroup(stings, "Öneri: boss girişleri");
            AddRecCue(stings, "siren", "siren", RecCue.StingSiren, 1f);
            AddRecCue(stings, "boom", "bum", RecCue.StingBoom, 1f);
            AddRecCue(stings, "gong", "gong", RecCue.StingGong, 1f);
            AddRecCue(stings, "clank", "şangırtı", RecCue.StingClank, 1f);
            AddRecCue(stings, "drone", "uğultu", RecCue.StingDrone, 1f);
            AddRecCue(stings, "magma", "magma", RecCue.StingMagma, 1f);

            // MISSING & WEAK: sounds the game does not have, or answers with a stand-in. Prototypes
            // only - nothing here is wired in until a take is picked (SoundFx.Prototypes.cs).
            const string missing = "Missing & weak sounds";
            SoundGroup(missing, "Eksik ve zayıf sesler");
            string[] families =
            {
                "Joker effects that are silent", "Round flow & feedback", "Cards, deck & market", "Stand-ins to replace",
            };
            string[] familiesTr =
            {
                "Sessiz joker efektleri", "Raunt akışı ve geri bildirim", "Kart, deste ve market", "Değişecek yedek sesler",
            };
            for (int f = 0; f < families.Length; f++)
            {
                SoundSubGroup(missing, families[f], familiesTr[f]);
            }
            foreach (ProtoCue cue in (ProtoCue[])Enum.GetValues(typeof(ProtoCue)))
            {
                int family;
                string en, tr;
                SoundFx.ProtoInfo(cue, out family, out en, out tr);
                AddProto(families[family], cue);
            }

            // The line-clear candidates the listening went through, through MIX with its layers.
            const string archive = "Line clear candidates (archive)";
            SoundGroup(archive, "Satır temizliği adayları (arşiv)");
            AddVersions(archive, "first round", "ilk tur", new[] { "CRUNCH", "CRISP", "CHUNKY", "ZIP", "POP", "CHIP" },
                v => sfx.AuditionLineStyle(v, soundLines, soundCombo));
            AddVersions(archive, "the ZIP family", "ZIP ailesi", new[] { "ZIP", "TWIN", "SPARK", "GLISS*", "EDGE", "SOFT" },
                v => sfx.AuditionLineStyle(v == 0 ? 3 : 5 + v, soundLines, soundCombo));
        }

        private void RebuildSoundRows()
        {
            soundRows.Clear();
            soundRowEntry.Clear();
            soundRowGroup.Clear();
            foreach (string group in soundGroupOrder)
            {
                if (!soundGroupParent.ContainsKey(group))
                {
                    AddSoundGroupRows(group, 0);
                }
            }
            // SETTINGS is the last group, and opening it shows the knobs under the list instead of
            // rows: they are needed now and then, not all the time.
            soundRows.Add(AnimationLabView.Row.Header(SoundSettingsGroup, "Ayarlar (bağlam ve seviyeler)",
                soundOpenGroups.Contains(SoundSettingsGroup), SoundKnobs().Count, 0));
            soundRowEntry.Add(-1);
            soundRowGroup.Add(SoundSettingsGroup);
            soundSelected = Mathf.Clamp(soundSelected, 0, Mathf.Max(0, soundRows.Count - 1));
            ClampSoundScroll();
        }

        private const string SoundSettingsGroup = "Settings (context & levels)";

        private int SoundRowsShown
        {
            get { return soundOpenGroups.Contains(SoundSettingsGroup) ? AnimationLabView.VisibleRows : 20; }
        }

        private void ClampSoundScroll()
        {
            soundScroll = Mathf.Clamp(soundScroll, 0, Mathf.Max(0, soundRows.Count - SoundRowsShown));
        }

        /// <summary>A group's header, and - when it is open - its own rows and then its sub-groups,
        /// each a level deeper.</summary>
        private void AddSoundGroupRows(string group, int depth)
        {
            bool open = soundOpenGroups.Contains(group);
            soundRows.Add(AnimationLabView.Row.Header(group, soundGroupTr[group], open, SoundCountIn(group), depth));
            soundRowEntry.Add(-1);
            soundRowGroup.Add(group);
            if (!open)
            {
                return;
            }
            for (int i = 0; i < soundEntries.Count; i++)
            {
                SoundEntry e = soundEntries[i];
                if (e.Group != group)
                {
                    continue;
                }
                // Every version is a button on the row: the game's green, the last played amber.
                int marked = e.GameVersion != null ? e.GameVersion() : -1;
                int lit = e == soundLastEntry ? soundLastVersion : -1;
                soundRows.Add(AnimationLabView.Row.WithButtons(e.En, e.Tr, depth + 1, e.Versions, marked, lit));
                soundRowEntry.Add(i);
                soundRowGroup.Add(group);
            }
            foreach (string sub in soundGroupOrder)
            {
                string parent;
                if (soundGroupParent.TryGetValue(sub, out parent) && parent == group)
                {
                    AddSoundGroupRows(sub, depth + 1);
                }
            }
        }

        /// <summary>How many sounds a group holds, its sub-groups included.</summary>
        private int SoundCountIn(string group)
        {
            int count = 0;
            foreach (SoundEntry e in soundEntries)
            {
                string g = e.Group;
                while (g != null)
                {
                    if (g == group) { count++; break; }
                    string parent;
                    g = soundGroupParent.TryGetValue(g, out parent) ? parent : null;
                }
            }
            return count;
        }

        private SoundEntry SelectedSoundEntry
        {
            get
            {
                if (soundSelected < 0 || soundSelected >= soundRowEntry.Count || soundRowEntry[soundSelected] < 0)
                {
                    return null;
                }
                return soundEntries[soundRowEntry[soundSelected]];
            }
        }

        // ------------------------------------------------------------------ knobs

        private List<AnimationLabView.Knob> SoundKnobs()
        {
            var knobs = new List<AnimationLabView.Knob>();
            knobs.Add(new AnimationLabView.Knob("the game plays set", "oyunun seti", SoundFx.SetNames[sfx.Set]));
            knobs.Add(new AnimationLabView.Knob("block size", "blok boyu", soundCubes + Loc.Pick(" cubes", " küp")));
            knobs.Add(new AnimationLabView.Knob("lines", "satır", soundLines.ToString()));
            knobs.Add(new AnimationLabView.Knob("combo streak", "kombo", soundCombo.ToString()));
            knobs.Add(new AnimationLabView.Knob("sweep # this round", "raunttaki temizlik #", soundSweeps.ToString()));
            knobs.Add(new AnimationLabView.Knob("MIX thump", "MIX gümleme", Mathf.RoundToInt(sfx.MixThumpLevel * 100f) + "%"));
            knobs.Add(new AnimationLabView.Knob("MIX explosion bed", "MIX patlama zemini", Mathf.RoundToInt(sfx.MixBlastLevel * 100f) + "%"));
            knobs.Add(new AnimationLabView.Knob("gap between versions", "sürümler arası", soundGap.ToString("0.0") + "s"));
            return knobs;
        }

        private static int Step(int value, int dir, int min, int max)
        {
            int span = max - min + 1;
            return min + ((value - min + dir) % span + span) % span;
        }

        private void CycleSoundKnob(int knob, int dir)
        {
            switch (knob)
            {
                case 0: sfx.Set = Step(sfx.Set, dir, 0, SoundFx.SetNames.Length - 1); RebuildSoundRows(); break;
                case 1: soundCubes = Step(soundCubes, dir, 1, 7); break;
                case 2: soundLines = Step(soundLines, dir, 1, 4); break;
                case 3: soundCombo = Step(soundCombo, dir, 0, 9); break;
                case 4: soundSweeps = Step(soundSweeps, dir, 1, 9); break;
                case 5: sfx.MixThumpLevel = Step(Mathf.RoundToInt(sfx.MixThumpLevel * 10f), dir, 0, 15) / 10f; break;
                case 6: sfx.MixBlastLevel = Step(Mathf.RoundToInt(sfx.MixBlastLevel * 10f), dir, 0, 15) / 10f; break;
                case 7: soundGap = Step(Mathf.RoundToInt(soundGap * 10f), dir, 2, 15) / 10f; break;
            }
        }

        // ------------------------------------------------------------------ input

        private static readonly Key[] SoundDigitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        private static readonly Key[] SoundNumpadKeys =
        {
            Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9,
        };

        /// <summary>Owns the whole frame while the lab is open, like the animation lab.</summary>
        private void HandleSoundLabInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && kb.f9Key.wasPressedThisFrame
                && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                soundLab.ResetPosition();
                soundDragging = false;
                RedrawSoundLab();
                return;
            }
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.f9Key.wasPressedThisFrame))
            {
                CloseSoundLab();
                return;
            }
            if (kb != null)
            {
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                for (int d = 0; d < SoundDigitKeys.Length; d++)
                {
                    if (kb[SoundDigitKeys[d]].wasPressedThisFrame || kb[SoundNumpadKeys[d]].wasPressedThisFrame)
                    {
                        SoundEntry e = SelectedSoundEntry;
                        if (e != null && d < e.Versions.Length)
                        {
                            if (shift && e.SetGameVersion != null)
                            {
                                e.SetGameVersion(d);
                                RebuildSoundRows();
                            }
                            PlaySoundVersion(e, d);
                        }
                        return;
                    }
                }
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    if (soundLastEntry != null)
                    {
                        PlaySoundVersion(soundLastEntry, soundLastVersion);
                    }
                    return;
                }
                if (kb.downArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                {
                    MoveSoundSelection(soundSelected + (kb.downArrowKey.wasPressedThisFrame ? 1 : -1));
                    return;
                }
                if (kb.enterKey.wasPressedThisFrame)
                {
                    ClickSoundRow(soundSelected, false);
                    return;
                }
            }
            if (mouse == null)
            {
                return;
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                soundScroll += wheel > 0f ? -3 : 3;
                ClampSoundScroll();
                RedrawSoundLab();
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            if (soundDragging)
            {
                if (!mouse.leftButton.isPressed)
                {
                    soundDragging = false;
                    RedrawSoundLab();
                    return;
                }
                soundLab.Offset = soundLab.ClampOffset(world - soundDragGrab, cam);
                return;
            }
            if (mouse.leftButton.wasPressedThisFrame && soundLab.TitleBarContains(world))
            {
                soundDragging = true;
                soundDragGrab = world - soundLab.Offset;
                return;
            }
            // POINTING SELECTS: the number keys act on the row under the cursor, so comparing is
            // hover + 1, 2, 3 with no click in between.
            int hovered = soundLab.RowAt(world);
            if (hovered >= 0 && hovered < soundRows.Count && hovered != soundSelected)
            {
                soundSelected = hovered;
                RedrawSoundLab();
            }
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                bool right = mouse.rightButton.wasPressedThisFrame;
                // A VERSION BUTTON: left click plays it; right click (or shift+click) also makes it
                // the one the game uses.
                int buttonRow, button;
                if (soundLab.ButtonAt(world, out buttonRow, out button)
                    && buttonRow >= 0 && buttonRow < soundRowEntry.Count && soundRowEntry[buttonRow] >= 0)
                {
                    SoundEntry picked = soundEntries[soundRowEntry[buttonRow]];
                    bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    if ((right || shift) && picked.SetGameVersion != null)
                    {
                        picked.SetGameVersion(button);
                    }
                    soundSelected = buttonRow;
                    PlaySoundVersion(picked, button);
                    return;
                }
                int knob = soundLab.KnobAt(world);
                if (knob >= 0)
                {
                    CycleSoundKnob(knob, right ? -1 : 1);
                    RedrawSoundLab();
                    return;
                }
                if (hovered >= 0 && hovered < soundRows.Count)
                {
                    ClickSoundRow(hovered, right);
                }
            }
        }

        private void MoveSoundSelection(int row)
        {
            soundSelected = Mathf.Clamp(row, 0, Mathf.Max(0, soundRows.Count - 1));
            if (soundSelected < soundScroll) { soundScroll = soundSelected; }
            if (soundSelected >= soundScroll + SoundRowsShown)
            {
                soundScroll = soundSelected - SoundRowsShown + 1;
            }
            RedrawSoundLab();
        }

        /// <summary>A header opens or closes its group. A row plays the version the GAME uses (the
        /// first when it has no choice), or with a right click every version in turn.</summary>
        private void ClickSoundRow(int row, bool everyVersion)
        {
            if (row < 0 || row >= soundRows.Count)
            {
                return;
            }
            soundSelected = row;
            int entry = soundRowEntry[row];
            if (entry < 0)
            {
                string group = soundRowGroup[row];
                if (!soundOpenGroups.Remove(group))
                {
                    soundOpenGroups.Add(group);
                }
                RebuildSoundRows();
                RedrawSoundLab();
                return;
            }
            SoundEntry e = soundEntries[entry];
            PlaySoundVersion(e, everyVersion ? -1 : (e.GameVersion != null ? e.GameVersion() : 0));
        }

        /// <summary>Plays one version, or (-1) every version in turn with the gap knob between.
        /// A new request cuts off a run still going, so a fast comparison never piles up.</summary>
        private void PlaySoundVersion(SoundEntry e, int version)
        {
            if (soundRunning != null)
            {
                StopCoroutine(soundRunning);
            }
            soundLastEntry = e;
            soundLastVersion = version;
            RebuildSoundRows();
            if (version < 0)
            {
                soundRunning = StartCoroutine(PlayEveryVersion(e));
                soundLastPlayed = Loc.Pick(e.En, e.Tr) + Loc.Pick(": every version in turn", ": tüm sürümler sırayla");
            }
            else
            {
                soundRunning = StartCoroutine(e.Play(version));
                soundLastPlayed = Loc.Pick(e.En, e.Tr) + ": " + (version + 1) + " " + e.Versions[version];
            }
            RedrawSoundLab();
        }

        private IEnumerator PlayEveryVersion(SoundEntry e)
        {
            for (int v = 0; v < e.Versions.Length; v++)
            {
                if (v > 0)
                {
                    yield return new WaitForSecondsRealtime(soundGap);
                }
                soundLastPlayed = Loc.Pick(e.En, e.Tr) + ": " + (v + 1) + " " + e.Versions[v]
                    + Loc.Pick("  (in turn)", "  (sırayla)");
                soundLastVersion = v;
                RebuildSoundRows();
                RedrawSoundLab();
                yield return StartCoroutine(e.Play(v));
            }
        }

        /// <summary>The status line: the pointed-at row's versions by number, the game's marked
        /// with *, and what was played last.</summary>
        private string SoundStatus()
        {
            return string.IsNullOrEmpty(soundLastPlayed)
                ? Loc.Pick("green = the game uses it  -  amber = last played", "yeşil = oyun kullanıyor  -  turuncu = son çalınan")
                : "> " + soundLastPlayed;
        }

        private void RedrawSoundLab()
        {
            var knobs = soundOpenGroups.Contains(SoundSettingsGroup)
                ? SoundKnobs() : new List<AnimationLabView.Knob>();
            soundLab.SetContent(soundRows, soundSelected, soundScroll, knobs, SoundStatus());
        }

        // ------------------------------------------------------------------ sequences

        private IEnumerator PickupThenPlaceSeq(int set)
        {
            sfx.Audition(set, s => s.Pickup());
            yield return new WaitForSecondsRealtime(0.45f);
            sfx.Audition(set, s => s.Place(soundCubes));
        }

        private IEnumerator PlaceStreakSeq(int set)
        {
            for (int c = 1; c <= 5; c++)
            {
                int cubes = c;
                sfx.Audition(set, s => s.Place(cubes));
                yield return new WaitForSecondsRealtime(0.32f);
            }
        }

        private IEnumerator PlaceThenClearSeq(int set)
        {
            sfx.Audition(set, s => s.Place(soundCubes));
            yield return new WaitForSecondsRealtime(0.12f);
            sfx.Audition(set, s => s.Explode(soundLines, soundCombo));
            yield return new WaitForSecondsRealtime(0.6f);
        }

        private IEnumerator ComboClimbSeq(int set)
        {
            for (int c = 1; c <= 6; c++)
            {
                int streak = c;
                sfx.Audition(set, s => s.Place(soundCubes));
                yield return new WaitForSecondsRealtime(0.1f);
                sfx.Audition(set, s => s.Explode(1 + (streak % 3 == 0 ? 1 : 0), streak));
                yield return new WaitForSecondsRealtime(0.55f);
            }
        }

        private IEnumerator LineTiersSeq(int set)
        {
            for (int l = 1; l <= 3; l++)
            {
                int count = l;
                sfx.Audition(set, s => s.Explode(count, soundCombo));
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        private IEnumerator SweepClimbSeq(int set)
        {
            for (int k = 1; k <= 5; k++)
            {
                float pitch = 1f + 0.12f * (k - 1);
                sfx.Audition(set, s => s.CleanSweep(pitch));
                yield return new WaitForSecondsRealtime(1.3f);
            }
        }

        private IEnumerator PlaceThenSweepSeq(int set)
        {
            sfx.Audition(set, s => s.Place(soundCubes));
            yield return new WaitForSecondsRealtime(0.12f);
            float pitch = 1f + 0.12f * Mathf.Min(soundSweeps - 1, 8);
            sfx.Audition(set, s => { s.CleanSweep(pitch); s.Flame(); });
            yield return new WaitForSecondsRealtime(1.4f);
        }
    }
}
