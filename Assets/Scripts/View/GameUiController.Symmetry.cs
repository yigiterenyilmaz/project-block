// PURPOSE: "Simetri"'s payout, wired up - the one place SymmetryRewardView is reached from, by the
// game and by the animation lab alike - and the lab's own section for it.
//
// WHEN: from PlayExplosionFeedback, the moment the turn's own events are played, and DELAYED until
// the turn's lines have been seen through - the board the joker judged is the board after them, so
// recognising its symmetry over a line still breaking would be recognising a board that is not
// there yet. WHAT: the joker's own report (SymmetryVisuals), matched by IDENTITY, never re-read off
// the board. WHERE: board coordinates to world through the board's own transform (so the arena's
// pressure scale and tremor are followed), the score through the same anchor every payout uses.
//
// A BLIND ROUND STAYS BLIND: under "Alacakaranlık" the regions are not traced (a trace round a
// region would show where the cubes are); the recognition, the resonance and the payout still play.
// EXTENSION POINT: another board-shape joker would get a report and a view of its own the same way.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private SymmetryRewardView symmetry;
        private SymmetryVisuals symmetryPlayed;

        /// <summary>After the turn's lines have broken (the sweep's beam and burst), and after a
        /// plain placement has settled.</summary>
        public static float SymmetryAfterLines = 0.45f;
        public static float SymmetryAfterPlacement = 0.12f;

        private void EnsureSymmetry()
        {
            if (symmetry != null)
            {
                return;
            }
            var go = new GameObject("SymmetryReward");
            go.transform.SetParent(transform, false);
            symmetry = go.AddComponent<SymmetryRewardView>();
            symmetry.BoardToWorld = SymmetryBoardToWorld;
            symmetry.CellSize = delegate
            {
                return boardView != null ? boardView.CellWorldSize * boardView.transform.lossyScale.x : 0.9f;
            };
            symmetry.ScoreAnchor = ScoreWorldAnchor;
            symmetry.Pixel = delegate
            {
                return cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
            };
            symmetry.BoardImpulse = delegate(Vector2 offset)
            {
                if (boardView != null)
                {
                    boardView.SetImpulse(offset);
                }
            };
            symmetry.Sounded = delegate(SymmetryCue cue, int step)
            {
                if (sfx != null)
                {
                    sfx.Symmetry(cue, step);
                }
            };
        }

        /// <summary>A point in board coordinates (a cell's centre at its integer coordinate) in the
        /// world, through the board's own transform.</summary>
        private Vector2 SymmetryBoardToWorld(float bx, float by)
        {
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return new Vector2(bx, by);
            }
            Vector2 origin = boardView.CellToWorld(new GridPos(board.MinX, board.MinY));
            float size = boardView.CellWorldSize;
            Vector2 local = origin + new Vector2((bx - board.MinX) * size, (by - board.MinY) * size);
            return boardView.transform.TransformPoint(local);
        }

        /// <summary>The turn's half: the joker's newest report, played once.</summary>
        private void PlaySymmetryPayout(TurnReport report)
        {
            if (session == null || session.Jokers == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var joker = owned[i] as SimetriJoker;
                if (joker == null || joker.LastSymmetry == null || ReferenceEquals(joker.LastSymmetry, symmetryPlayed))
                {
                    continue;
                }
                symmetryPlayed = joker.LastSymmetry;
                bool lines = report != null && (report.CubesExploded > 0 || LateExplodedCount(report) > 0);
                PlaySymmetry(joker.LastSymmetry, lines ? SymmetryAfterLines : SymmetryAfterPlacement);
                return;
            }
        }

        /// <summary>The seam the game and the lab share. The lab fabricates only the REPORT (off a
        /// board of its own, through the same Describe the joker uses).</summary>
        private void PlaySymmetry(SymmetryVisuals seen, float delay)
        {
            if (seen == null || boardView == null)
            {
                return;
            }
            EnsureSymmetry();
            symmetry.Play(seen, delay, boardView.IsDark);
        }

        private void StopSymmetry()
        {
            if (symmetry != null)
            {
                symmetry.Stop();
            }
            if (session != null && session.Jokers != null)
            {
                foreach (Joker j in session.Jokers.Jokers)
                {
                    var s = j as SimetriJoker;
                    if (s != null && s.LastSymmetry != null)
                    {
                        symmetryPlayed = s.LastSymmetry;
                    }
                }
            }
        }

        // ================================================================== the lab

        private void AddSymmetryLab()
        {
            AddAnimSub("jokers", "simetri", "simetri", "simetri");
            AddAnim("simetri: 1 left-right mirror, +40", "simetri: 1 sol-sağ ayna, +40", () => PlaySymmetryLab(LabLeftRight, false));
            AddAnim("simetri: 2 top-bottom mirror, +40", "simetri: 2 üst-alt ayna, +40", () => PlaySymmetryLab(LabTopBottom, false));
            AddAnim("simetri: 3 half turn (180), +40", "simetri: 3 yarım dönüş (180°), +40", () => PlaySymmetryLab(LabHalfTurn, false));
            AddAnim("simetri: 4 BOTH mirrors, +120", "simetri: 4 ÇİFT ayna, +120", () => PlaySymmetryLab(LabBoth, false));
            AddAnim("simetri: 5 sparse board", "simetri: 5 seyrek tahta", () => PlaySymmetryLab(LabSparse, false));
            AddAnim("simetri: 6 dense board", "simetri: 6 yoğun tahta", () => PlaySymmetryLab(LabDense, false));
            AddAnim("simetri: 7 a board with a centre cell", "simetri: 7 merkez hücreli tahta", () => PlaySymmetryLab(LabCentre, false));
            AddAnim("simetri: 8 visual chunks debug", "simetri: 8 görsel bölge hata ayıklama", () =>
            {
                SymmetryRewardView.DebugFlags.ShowVisualChunks = true;
                SymmetryRewardView.DebugFlags.ShowChunkOrder = true;
                SymmetryRewardView.DebugFlags.ShowMatchedPairs = true;
                SymmetryRewardView.DebugFlags.ShowAxis = true;
                SymmetryRewardView.DebugFlags.ShowType = true;
                PlaySymmetryLab(LabDense, false);
            });
            AddAnim("simetri: + the brief's 2x2 blocks (region by region)", "simetri: + şartnamenin 2x2 blokları (bölge bölge)",
                () => PlaySymmetryLab(LabBlocks, false));
            AddAnim("simetri: + one lone pair", "simetri: + tek bir çift", () => PlaySymmetryLab(LabOnePair, false));
            AddAnim("simetri: + the cross (cells ON both axes)", "simetri: + haç (iki eksen ÜSTÜNDE hücreler)", () => PlaySymmetryLab(LabCross, false));
            AddAnim("simetri: + an inverted payout (\"Terslik\", -40)", "simetri: + ters ödeme (\"Terslik\", -40)", () => PlaySymmetryLab(LabLeftRight, true));
            AddAnim("simetri: + a blind round (no region traces)", "simetri: + karanlık raunt (bölge izi yok)", () =>
            {
                PlaySymmetryLab(LabBoth, false, true);
            });
            AddAnim("simetri: + both mirrors at 0.5x", "simetri: + çift ayna 0.5x", () =>
            {
                PlaySymmetryLab(LabBoth, false);
                Time.timeScale = 0.5f;
            });
            AddAnim("simetri: + left-right at 0.25x", "simetri: + sol-sağ 0.25x", () =>
            {
                PlaySymmetryLab(LabBlocks, false);
                Time.timeScale = 0.25f;
            });
            AddAnim("simetri: OLD version (per-cell flash + board ripple)", "simetri: ESKİ sürüm (hücre hücre flaş + tahta dalgası)", () =>
            {
                PlaySymmetryLab(LabBlocks, false, false, false);
                PlaySymmetryProc();
            });
            AddAnim("simetri audio: the +40 phrase (no picture)", "simetri ses: +40 cümlesi (görüntüsüz)", () => StartCoroutine(SymmetryAudioPhrase(false)));
            AddAnim("simetri audio: the +120 phrase (no picture)", "simetri ses: +120 cümlesi (görüntüsüz)", () => StartCoroutine(SymmetryAudioPhrase(true)));
            AddAnim("simetri audio: the lock ladder (8 steps)", "simetri ses: kilit merdiveni (8 basamak)", () => StartCoroutine(SymmetryAudioLadder()));
            AddAnim("simetri: pair order Auto / CentreOut / OutsideIn (cycles)", "simetri: çift sırası Oto / Merkezden / Dıştan (döngü)", () =>
            {
                SymmetryRewardView.Style.PairOrderMode = (SymmetryChunks.OrderMode)(((int)SymmetryRewardView.Style.PairOrderMode + 1) % 3);
                animLastLabel = "order " + SymmetryRewardView.Style.PairOrderMode;
            });
            AddAnim("simetri: largest region 3 / 4 / 6 cells (cycles)", "simetri: en büyük bölge 3 / 4 / 6 hücre (döngü)", () =>
            {
                int m = SymmetryRewardView.Style.ChunkMaxCells;
                m = m == 3 ? 4 : m == 4 ? 6 : 3;
                SymmetryRewardView.Style.ChunkMaxCells = m;
                SymmetryRewardView.Style.ChunkSplitLength = m;
                animLastLabel = "largest region " + m;
            });
            AddSymmetryToggle("ShowMatchedPairs", () => SymmetryRewardView.DebugFlags.ShowMatchedPairs = !SymmetryRewardView.DebugFlags.ShowMatchedPairs);
            AddSymmetryToggle("ShowVisualChunks", () => SymmetryRewardView.DebugFlags.ShowVisualChunks = !SymmetryRewardView.DebugFlags.ShowVisualChunks);
            AddSymmetryToggle("ShowChunkOrder", () => SymmetryRewardView.DebugFlags.ShowChunkOrder = !SymmetryRewardView.DebugFlags.ShowChunkOrder);
            AddSymmetryToggle("ShowAxis", () => SymmetryRewardView.DebugFlags.ShowAxis = !SymmetryRewardView.DebugFlags.ShowAxis);
            AddSymmetryToggle("ShowAnchors", () => SymmetryRewardView.DebugFlags.ShowAnchors = !SymmetryRewardView.DebugFlags.ShowAnchors);
            AddSymmetryToggle("ShowType", () => SymmetryRewardView.DebugFlags.ShowType = !SymmetryRewardView.DebugFlags.ShowType);
            AddAnim("simetri debug: all off", "simetri hata ayıklama: hepsi kapalı", () => SymmetryRewardView.DebugFlags.AllOff());
        }

        private void AddSymmetryToggle(string name, System.Action flip)
        {
            AddAnim("simetri debug: " + name, "simetri hata ayıklama: " + name, () =>
            {
                flip();
                animLastLabel = name;
            });
        }

        // the lab's boards, top row first ('X' a cube; the kinds are mixed on purpose - the joker
        // only looks at occupancy, and so does its light)
        private static readonly string[] LabLeftRight =
        {
            ".......", "XX...XX", ".XX.XX.", "X.....X", ".XX.XX.", "XXX.XXX", "......."
        };
        private static readonly string[] LabTopBottom =
        {
            "X..XX..", "XX...X.", "..X..XX", ".......", "..X..XX", "XX...X.", "X..XX.."
        };
        private static readonly string[] LabHalfTurn =
        {
            "XX.X.XX", "XX....X", ".....X.", ".X.X.X.", ".X.....", "X....XX", "XX.X.XX"
        };
        private static readonly string[] LabBoth =
        {
            "XX...XX", "X.....X", "..XXX..", "..X.X..", "..XXX..", "X.....X", "XX...XX"
        };
        private static readonly string[] LabSparse =
        {
            "X.....X", ".......", ".......", ".......", ".......", ".......", "..X.X.."
        };
        private static readonly string[] LabDense =
        {
            "XXX.XXX", "XX.X.XX", "XXX.XXX", ".XXXXX.", "XX...XX", "XXXXXXX", "X.XXX.X"
        };
        private static readonly string[] LabCentre =
        {
            ".......", ".X...X.", "..XXX..", "..XXX..", "..XXX..", ".X...X.", "......."
        };
        private static readonly string[] LabBlocks =
        {
            ".......", ".XX.XX.", ".XX.XX.", ".......", "X.....X", "XX...XX", "......."
        };
        private static readonly string[] LabOnePair =
        {
            ".......", ".......", ".......", ".X...X.", ".......", ".......", "......."
        };
        private static readonly string[] LabCross =
        {
            "...X...", "...X...", "...X...", "XXXXXXX", "...X...", "...X...", "...X..."
        };

        /// <summary>A board of the lab's own, built from <paramref name="rows"/>, read by the rules'
        /// own Describe, and played through the game's seam.</summary>
        private void PlaySymmetryLab(string[] rows, bool inverted, bool blind = false, bool play = true)
        {
            AnimResync();
            int h = rows.Length;
            int w = rows[0].Length;
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            CubeKind[] kinds = { CubeKind.Normal, CubeKind.Normal, CubeKind.Fire, CubeKind.Normal, CubeKind.Water, CubeKind.Normal, CubeKind.Gold };
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (rows[r][x] != 'X')
                    {
                        continue;
                    }
                    int y = h - 1 - r;
                    int pick = (x * 5 + y * 3) % kinds.Length;
                    board.SetCubeAt(new GridPos(x, y), new Cube(kinds[pick], cards[(x + y * 2) % cards.Count]));
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            boardView.Refresh();
            SymmetryVisuals seen = SymmetryVisuals.Describe(board);
            if (!seen.Any)
            {
                animLastLabel = "the lab board is not symmetric";
                return;
            }
            int scale = session != null && session.Config != null ? Mathf.Max(1, session.Config.Scoring.ScoreScale) : 10;
            var joker = new SimetriJoker();
            seen.Bonus = seen.BothMirrors ? joker.OneAxisBonus * joker.BothAxesMultiplier : joker.OneAxisBonus;
            seen.Points = seen.Bonus * scale * (inverted ? -1 : 1);
            animLastLabel = (seen.LeftRight ? "LR " : "") + (seen.TopBottom ? "TB " : "") + (seen.HalfTurn ? "180 " : "")
                + "pairs " + (seen.LeftRightPairs.Count + seen.TopBottomPairs.Count + seen.HalfTurnPairs.Count) + "  " + seen.Points;
            if (!play)
            {
                return;
            }
            EnsureSymmetry();
            symmetry.Play(seen, 0.2f, blind);
        }

        /// <summary>The payout's audio on its own timeline, as the picture would play it.</summary>
        private System.Collections.IEnumerator SymmetryAudioPhrase(bool twice)
        {
            var beats = new List<KeyValuePair<float, KeyValuePair<SymmetryCue, int>>>();
            System.Action<float, SymmetryCue, int> at = (t, c, s) => beats.Add(new KeyValuePair<float, KeyValuePair<SymmetryCue, int>>(t, new KeyValuePair<SymmetryCue, int>(c, s)));
            at(0f, SymmetryCue.Detect, 0);
            int pairs = twice ? 6 : 3;
            float gap = twice ? 0.08f : 0.1f;
            float t0 = 0.08f;
            float lastLock = 0f;
            for (int i = 0; i < pairs; i++)
            {
                float start = i < 3 || !twice ? t0 + i * gap : 0.3475f + (i - 3) * gap;
                at(start, SymmetryCue.Trace, i);
                lastLock = start + 0.1575f;
                at(lastLock, SymmetryCue.Lock, i);
            }
            float res = lastLock + (twice ? 0.15f : 0.05f);
            at(res, twice ? SymmetryCue.ResonanceDouble : SymmetryCue.Resonance, 0);
            at(res + 0.04f, twice ? SymmetryCue.PayoutBig : SymmetryCue.Payout, 0);
            at(res + 0.54f, SymmetryCue.Land, 0);
            beats.Sort((a, b) => a.Key.CompareTo(b.Key));
            float clock = 0f;
            int next = 0;
            while (next < beats.Count)
            {
                while (next < beats.Count && beats[next].Key <= clock)
                {
                    sfx.Symmetry(beats[next].Value.Key, beats[next].Value.Value);
                    next++;
                }
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private System.Collections.IEnumerator SymmetryAudioLadder()
        {
            for (int i = 0; i < SymmetrySound.LockSteps; i++)
            {
                sfx.Symmetry(SymmetryCue.Lock, i);
                animLastLabel = "lock step " + (i + 1) + " / " + SymmetrySound.LockSteps;
                float t = 0f;
                while (t < 0.22f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }
    }
}
