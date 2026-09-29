// PURPOSE: "Antimadde" wired up - the one place AntimatterBlastView is reached from. Played from
// PlayExplosionFeedback, the moment the turn's cubes break; the cells are the report's destroyed
// cubes of the annihilated kind, and the faces are what the board last showed there.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private AntimatterBlastView antimatter;

        /// <summary>True if this turn's report was an annihilation - the plain explode sound is
        /// then left to the blast's own boom.</summary>
        private static bool IsAnnihilation(TurnReport report)
        {
            return report != null && report.AnnihilatedKind.HasValue;
        }

        private void PlayAntimatter(TurnReport report)
        {
            if (!IsAnnihilation(report) || boardView == null)
            {
                return;
            }
            var cells = new List<GridPos>();
            var faces = new List<Color>();
            foreach (DestroyedCube dead in report.DestroyedCubes)
            {
                if (dead.Cube.Kind != report.AnnihilatedKind.Value)
                {
                    continue;
                }
                cells.Add(dead.Pos);
                Sprite tile;
                Color colour;
                faces.Add(boardView.TryCubeLook(dead.Pos, 5f, out tile, out colour) ? colour : Color.white);
            }
            PlayAntimatterCells(cells, faces);
        }

        /// <summary>The seam the lab drives too.</summary>
        private void PlayAntimatterCells(List<GridPos> cells, List<Color> faces)
        {
            if (cells.Count == 0)
            {
                return;
            }
            EnsureAntimatter();
            sfx.Whoosh();
            antimatter.Play(cells, faces);
        }

        private void EnsureAntimatter()
        {
            if (antimatter != null)
            {
                return;
            }
            var go = new GameObject("AntimatterBlast");
            // Under the BOARD's transform so the knock and the overtime squeeze carry it along.
            go.transform.SetParent(boardView.transform, false);
            antimatter = go.AddComponent<AntimatterBlastView>();
            antimatter.Build(boardView);
            antimatter.Peaked = delegate
            {
                sfx.Explode(4, 3);
                sfx.Rumble();
            };
        }
    }
}
