// PURPOSE: The animation-lab scenes for three jokers given statistics and a proc: "Yer altı
// kaynakları", "Kolay para" and "Genel temizlik". The catalogue ENTRIES are in
// GameUiController.AnimationLab.cs with every other entry; only the scenes live here, to keep
// out of a 10k-line file another agent is editing in the same tree.
//
// THE LAB RULE HOLDS: every scene drives the REAL animation code and fabricates only the
// ARGUMENTS. The seam's fill goes through PlaySeamRefuel (the seam RefreshAll calls) and the
// joker's card flashes through the bar's own proc light.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        // ------------------------------------------------------------- yer altı kaynakları

        /// <summary>A delivery of fuel into the powers actually held. count = int.MaxValue
        /// fills all of them (proves the stagger is squeezed); dregs = the seam's last pump.
        /// The report is fabricated with the joker's own numbers.</summary>
        private void AnimSeamRefuel(int count, bool dregs)
        {
            if (!AnimHasPower())
            {
                return;
            }
            IReadOnlyList<Power> powers = session.Powers.Powers;
            int take = Mathf.Clamp(count, 1, powers.Count);
            var seam = new YerAltiKaynaklariJoker();
            var report = new SeamRefuelVisuals();
            report.Tier = Rarity.Common;
            report.Capacity = seam.Capacity;
            for (int i = 0; i < take; i++)
            {
                report.PowerInstanceIds.Add(powers[i].InstanceId);
                report.CapacitySpent += seam.CommonCost;
            }
            report.CapacityLeft = dregs ? 0 : seam.Capacity - report.CapacitySpent;
            PlaySeamRefuel(report);
            animLastLabel = Loc.Pick(
                take + " power(s), seam " + report.CapacityLeft + "/" + report.Capacity,
                take + " güç, damar " + report.CapacityLeft + "/" + report.Capacity);
        }

        /// <summary>
        /// THE SAME FILL over a state the RULES made. A refuel is "it was empty and is now full",
        /// so the card has to be SEEN empty first: this burns the real charges
        /// (PowerInventory.BurnCharge), lets the bar show them spent, refills them through
        /// PowerInventory.Recharge - the primitive the joker itself uses - and plays the fill.
        /// Only charges that existed are put back, so nothing is left changed.
        /// </summary>
        private void AnimSeamRealRules()
        {
            if (AnimHasPower())
            {
                StartCoroutine(AnimSeamRealRulesRoutine());
            }
        }

        private IEnumerator AnimSeamRealRulesRoutine()
        {
            IReadOnlyList<Power> powers = session.Powers.Powers;
            var burned = new List<int>();
            for (int i = 0; i < powers.Count; i++)
            {
                if (session.Powers.BurnCharge(powers[i]))
                {
                    burned.Add(powers[i].InstanceId);
                }
            }
            if (burned.Count == 0)
            {
                animLastLabel = Loc.Pick("(every power was already spent)",
                    "(tüm güçler zaten boştu)");
                yield break;
            }
            powerBar.Refresh(session, null);
            yield return new WaitForSeconds(0.55f); // long enough to READ as spent

            var seam = new YerAltiKaynaklariJoker();
            var report = new SeamRefuelVisuals();
            report.Tier = Rarity.Common;
            report.Capacity = seam.Capacity;
            for (int i = 0; i < burned.Count; i++)
            {
                session.Powers.Recharge(burned[i]);
                report.PowerInstanceIds.Add(burned[i]);
                report.CapacitySpent += seam.CommonCost;
            }
            report.CapacityLeft = seam.Capacity - report.CapacitySpent;
            // The bar first, then the fill: the fill UNCOVERS the charged card.
            powerBar.Refresh(session, null);
            PlaySeamRefuel(report);
            AnimJokerProcNamed<YerAltiKaynaklariJoker>();
            animLastLabel = Loc.Pick(burned.Count + " refuelled for real",
                burned.Count + " güç gerçekten dolduruldu");
        }

        // -------------------------------------------------------------------- kolay para

        /// <summary>A placement paying out: the cells light, then the card procs and the
        /// joker's OWN number (its share of the bar x cubes, scaled) rises.</summary>
        private IEnumerator AnimKolayPara(int cubes)
        {
            var easy = new KolayParaJoker();
            long paid = (long)System.Math.Round(cubes * easy.ThresholdSharePerCube
                * (session.CurrentRound != null ? session.CurrentRound.ScoreThreshold : 0)
                * session.Config.Scoring.ScoreScale);
            List<GridPos> cells = AnimCells(cubes);
            if (cells.Count > 0)
            {
                boardView.LightUpAround(cells);
            }
            yield return new WaitForSeconds(0.12f);

            AnimJokerProcNamed<KolayParaJoker>();
            Vector2 at = cells.Count > 0
                ? boardView.CellToWorld(cells[cells.Count / 2]) + new Vector2(0f, 0.55f)
                : new Vector2(0f, 2.0f);
            FloatingTextFx.Spawn(transform, at, "+" + paid,
                new Color(1f, 0.88f, 0.52f), 60, 0.08f);
            animLastLabel = Loc.Pick(cubes + " cubes -> +" + paid, cubes + " küp -> +" + paid);
        }

        // ---------------------------------------------------------------- genel temizlik

        /// <summary>
        /// The switch being paid for - the only way this joker is ever seen working. Somebody
        /// ELSE's destruction is shown first and the card second, which is the order the rules
        /// resolve it in. The figure comes off the real scoring config, never a constant.
        /// </summary>
        private IEnumerator AnimGenelTemizlik(bool sweep)
        {
            List<GridPos> cells = AnimCells();
            ScoringConfig scoring = session.Config.Scoring;
            long paid = sweep
                ? (long)scoring.CleanSweepBonus * scoring.ScoreScale
                : (long)cells.Count * scoring.PointsPerCubeExploded * scoring.ScoreScale;

            if (sweep)
            {
                EmitSweepConfetti();
            }
            else if (cells.Count > 0)
            {
                FlashCells(cells, new Color(0.72f, 0.95f, 1f));
            }
            yield return new WaitForSeconds(sweep ? 0.45f : 0.30f);

            AnimJokerProcNamed<GenelTemizlikJoker>();
            Vector2 at = cells.Count > 0
                ? boardView.CellToWorld(cells[cells.Count / 2]) + new Vector2(0f, 0.55f)
                : new Vector2(0f, 2.0f);
            FloatingTextFx.Spawn(transform, at, "+" + paid,
                new Color(0.72f, 0.95f, 1f), 60, 0.08f);
            animLastLabel = Loc.Pick("the switch was worth +" + paid,
                "anahtar +" + paid + " kazandırdı");
        }

        // ----------------------------------------------------------------------- shared

        /// <summary>The proc light on a held joker of type T, or the first joker held if there
        /// is none - the light is the bar's, which card wears it is not what is being judged.
        /// </summary>
        private void AnimJokerProcNamed<T>() where T : Joker
        {
            if (!AnimShowJokerBar())
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] is T)
                {
                    jokerBar.ProcJoker(owned[i].InstanceId);
                    return;
                }
            }
            jokerBar.ProcJoker(owned[0].InstanceId);
        }
    }
}
