// PURPOSE: "Tamagotchi"'s DEVELOPMENT VIEW - the brief's 22 debug toggles and its punish view,
// plus the corrective pass's seventeen (where it could live and where it is going, the bubble's
// anchor, the furious face and body as numbers, the hatred field's levels and its wave's radius, the
// audio event, its layers and the duck, the punish's phase, the bite's progress and the paths the
// asset and the chunks took),
// drawn by the pet itself (lines, rects, circles and text in its flight layer) so the animation lab
// can switch each one on and see why the pet did what it did. Every number shown is Core's or the
// view's own state; nothing here is computed for the display alone except where it is drawn.
//
// ALL OFF BY DEFAULT and only reachable from the lab (F3): a final build shows no debug at all.
// The punish view lists every candidate the planner weighed (PetRampageVisuals.Candidates) with its
// pressure and hard-lock risk and marks the chosen one, the way the brief draws it.

using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The brief's debug toggles. Lab only.</summary>
    public static class TamagotchiDebug
    {
        public static bool ShowTamagotchiHomeAnchor;
        public static bool ShowTamagotchiDragDistance;
        public static bool ShowFeedZone;
        public static bool ShowEyeTarget;
        public static bool ShowRequestSlots;
        public static bool ShowHungerState;
        public static bool ShowPatienceProgress;
        public static bool ShowCurrentExpression;
        public static bool ShowCurrentAnimationState;
        public static bool ShowPunishType;
        public static bool ShowBoardEatCandidates;
        public static bool ShowBoardEatScores;
        public static bool ShowRejectedHardLockCandidates;
        public static bool ShowChosenBoardEatCells;
        public static bool ShowJokerEatCandidateValues;
        public static bool ShowPowerEatCandidateValues;
        public static bool ShowPileEatCandidates;
        public static bool ShowCardValueTier;
        public static bool ShowFoodProxyBounds;
        public static bool ShowBiteMask;
        public static bool ShowPresentationQueue;
        public static bool ShowPunishView;

        // the corrective pass (brief 164)
        public static bool ShowRoamingCandidates;
        public static bool ShowChosenRoamingAnchor;
        public static bool ShowSpeechBubbleAnchor;
        public static bool ShowFuryFaceRig;
        public static bool ShowFuryBodyDeform;
        public static bool ShowHatredField;
        public static bool ShowHatredWaveProgress;
        public static bool ShowScreenVignette;
        public static bool ShowChromaticEdge;
        public static bool ShowCurrentAudioEvent;
        public static bool ShowAudioLayerNames;
        public static bool ShowAudioDuckAmount;
        public static bool ShowPunishPhase;
        public static bool ShowEatingProxy;
        public static bool ShowBiteProgress;
        public static bool ShowBoardChunkPath;
        public static bool ShowAssetPullPath;

        private static bool AnyCorrective
        {
            get
            {
                return ShowRoamingCandidates || ShowChosenRoamingAnchor || ShowSpeechBubbleAnchor || ShowFuryFaceRig
                    || ShowFuryBodyDeform || ShowHatredField || ShowHatredWaveProgress || ShowScreenVignette
                    || ShowChromaticEdge || ShowCurrentAudioEvent || ShowAudioLayerNames || ShowAudioDuckAmount
                    || ShowPunishPhase || ShowEatingProxy || ShowBiteProgress || ShowBoardChunkPath || ShowAssetPullPath;
            }
        }

        public static bool Any
        {
            get
            {
                return ShowTamagotchiHomeAnchor || ShowTamagotchiDragDistance || ShowFeedZone || ShowEyeTarget
                    || ShowRequestSlots || ShowHungerState || ShowPatienceProgress || ShowCurrentExpression
                    || ShowCurrentAnimationState || ShowPunishType || ShowBoardEatCandidates || ShowBoardEatScores
                    || ShowRejectedHardLockCandidates || ShowChosenBoardEatCells || ShowJokerEatCandidateValues
                    || ShowPowerEatCandidateValues || ShowPileEatCandidates || ShowCardValueTier
                    || ShowFoodProxyBounds || ShowBiteMask || ShowPresentationQueue || ShowPunishView
                    || AnyCorrective;
            }
        }

        public static void AllOff()
        {
            ShowTamagotchiHomeAnchor = ShowTamagotchiDragDistance = ShowFeedZone = ShowEyeTarget = false;
            ShowRequestSlots = ShowHungerState = ShowPatienceProgress = ShowCurrentExpression = false;
            ShowCurrentAnimationState = ShowPunishType = ShowBoardEatCandidates = ShowBoardEatScores = false;
            ShowRejectedHardLockCandidates = ShowChosenBoardEatCells = ShowJokerEatCandidateValues = false;
            ShowPowerEatCandidateValues = ShowPileEatCandidates = ShowCardValueTier = false;
            ShowFoodProxyBounds = ShowBiteMask = ShowPresentationQueue = ShowPunishView = false;
            ShowRoamingCandidates = ShowChosenRoamingAnchor = ShowSpeechBubbleAnchor = ShowFuryFaceRig = false;
            ShowFuryBodyDeform = ShowHatredField = ShowHatredWaveProgress = ShowScreenVignette = false;
            ShowChromaticEdge = ShowCurrentAudioEvent = ShowAudioLayerNames = ShowAudioDuckAmount = false;
            ShowPunishPhase = ShowEatingProxy = ShowBiteProgress = ShowBoardChunkPath = ShowAssetPullPath = false;
        }
    }

    public sealed partial class TamagotchiView
    {
        private Transform debugRoot;
        private readonly List<SpriteRenderer> debugLines = new List<SpriteRenderer>();
        private readonly List<TextMesh> debugTexts = new List<TextMesh>();
        private int debugLineUsed;
        private int debugTextUsed;
        private const int DebugOrder = 40;

        private void TickDebug()
        {
            debugLineUsed = 0;
            debugTextUsed = 0;
            if (TamagotchiDebug.Any)
            {
                DrawDebug();
            }
            for (int i = debugLineUsed; i < debugLines.Count; i++)
            {
                debugLines[i].enabled = false;
            }
            for (int i = debugTextUsed; i < debugTexts.Count; i++)
            {
                debugTexts[i].gameObject.SetActive(false);
            }
        }

        private void HideDebug()
        {
            foreach (SpriteRenderer l in debugLines)
            {
                l.enabled = false;
            }
            foreach (TextMesh t in debugTexts)
            {
                t.gameObject.SetActive(false);
            }
        }

        private void DrawDebug()
        {
            Color cyan = new Color(0.4f, 0.95f, 1f);
            Color yellow = new Color(1f, 0.9f, 0.3f);
            Color pink = new Color(1f, 0.5f, 0.75f);
            Color white = new Color(1f, 1f, 1f, 0.9f);
            if (TamagotchiDebug.ShowTamagotchiHomeAnchor)
            {
                DebugCross(Home.Base, 0.2f, cyan);
                DebugRect(Home.PetRect, cyan);
                DebugLine(new Vector2(Home.Clip.xMin, Home.Clip.yMin), new Vector2(Home.Clip.xMax, Home.Clip.yMin), yellow);
                DebugText(Home.Base + new Vector2(0f, -0.15f), "home: " + Home.Name + (Home.Squeezed ? " (squeezed)" : "")
                    + "  side " + (Home.Facing < 0 ? "R" : "L"), cyan);
            }
            if (TamagotchiDebug.ShowFeedZone)
            {
                DebugCircle(FeedZoneCentre, FeedZoneRadius, pink);
                DebugCircle(FeedZoneCentre, FeedZoneRadius * 2.2f, new Color(1f, 0.5f, 0.75f, 0.5f));
                DebugCircle(FeedZoneCentre, FeedZoneRadius * 4.2f, new Color(1f, 0.5f, 0.75f, 0.25f));
            }
            if (TamagotchiDebug.ShowTamagotchiDragDistance && drag.Active)
            {
                DebugLine(drag.World, FeedZoneCentre, yellow);
                float d = (drag.World - FeedZoneCentre).magnitude;
                string[] bands = { "far", "medium", "near", "IN ZONE" };
                DebugText(Vector2.Lerp(drag.World, FeedZoneCentre, 0.5f), d.ToString("0.00") + " " + bands[BandOf(d)]
                    + (drag.Accepted ? " (food)" : " (not food)"), yellow);
            }
            if (TamagotchiDebug.ShowEyeTarget)
            {
                Vector3 eyes = (rig.EyeWorld(true) + rig.EyeWorld(false)) * 0.5f;
                Vector2 target = act.LookAt.HasValue ? (Vector2)act.LookAt.Value
                    : drag.Active ? drag.World : Anchors.HandFocus ?? Anchors.BoardCentre;
                DebugCross(target, 0.15f, pink);
                DebugLine(eyes, target, new Color(1f, 0.5f, 0.75f, 0.35f));
            }
            if (TamagotchiDebug.ShowRequestSlots || TamagotchiDebug.ShowCardValueTier)
            {
                for (int i = 0; i < plates.Count; i++)
                {
                    Vector3 c = PlateWorld(i);
                    var r = new Rect(c.x - PlateArtWidth * PlateWorldScale * 0.5f, c.y - PlateArtHeight * PlateWorldScale * 0.5f,
                        PlateArtWidth * PlateWorldScale, PlateArtHeight * PlateWorldScale);
                    if (TamagotchiDebug.ShowRequestSlots)
                    {
                        DebugRect(r, yellow);
                    }
                    string label = (TamagotchiDebug.ShowRequestSlots ? "slot " + plates[i].SlotId + " #" + plates[i].CardId
                        + (plates[i].Fed ? " fed" : plates[i].Destroyed ? " bitten" : " pending") : "")
                        + (TamagotchiDebug.ShowCardValueTier ? " " + plates[i].Tier : "");
                    DebugText(new Vector2(r.center.x, r.yMax + 0.08f), label, yellow);
                }
            }
            var sb = new StringBuilder();
            if (TamagotchiDebug.ShowHungerState)
            {
                sb.Append("hunger: ").Append(State.Stage).Append('\n');
            }
            if (TamagotchiDebug.ShowPatienceProgress)
            {
                sb.Append("patience: ").Append((1f - State.Progress).ToString("0.00"))
                    .Append(State.DeadlineNext ? "  NEXT DRAW = DEADLINE" : "").Append('\n');
            }
            if (TamagotchiDebug.ShowCurrentExpression)
            {
                sb.Append("face: ").Append(moodShown).Append(act.Emotion.HasValue ? " + " + act.Emotion.Value : "")
                    .Append(dragEmotionWeight > 0.05f ? " + drag " + dragEmotion : "").Append("  mouth ").Append(face.Mouth).Append('\n');
            }
            if (TamagotchiDebug.ShowCurrentAnimationState)
            {
                sb.Append("state: ").Append(ViewState).Append("  playing: ").Append(Playing).Append('\n');
            }
            if (TamagotchiDebug.ShowPresentationQueue)
            {
                sb.Append("queue: ").Append(queue.Current != null ? "[" + queue.Current.Priority + "] " + queue.Current.Name : "-");
                foreach (TamagotchiPresentationQueue.Item item in queue.Pending)
                {
                    sb.Append(" | ").Append(item.Priority).Append(' ').Append(item.Name);
                }
                sb.Append('\n');
            }
            DrawCorrectiveDebug(sb);
            PetRampageVisuals p = LastPunish;
            if (TamagotchiDebug.ShowPunishType)
            {
                sb.Append("punish: ").Append(p != null ? p.Kind + " x" + p.Count : "none yet").Append('\n');
            }
            if (p != null && (TamagotchiDebug.ShowPunishView || TamagotchiDebug.ShowJokerEatCandidateValues
                || TamagotchiDebug.ShowPowerEatCandidateValues || TamagotchiDebug.ShowPileEatCandidates))
            {
                char letter = 'A';
                string chosen = "-";
                foreach (PetPunishCandidate c in p.Candidates)
                {
                    bool show = TamagotchiDebug.ShowPunishView
                        || (TamagotchiDebug.ShowJokerEatCandidateValues && c.Kind == PetPunishKind.Joker)
                        || (TamagotchiDebug.ShowPowerEatCandidateValues && c.Kind == PetPunishKind.Power)
                        || (TamagotchiDebug.ShowPileEatCandidates
                            && (c.Kind == PetPunishKind.DrawPile || c.Kind == PetPunishKind.DiscardPile));
                    if (show)
                    {
                        sb.Append("Candidate ").Append(letter).Append(": ").Append(c.Kind)
                            .Append(c.Label != null ? " (" + c.Label + ")" : "")
                            .Append(c.Valid ? "  Pressure +" + c.Pressure.ToString("0.00") : "  -")
                            .Append(c.Kind == PetPunishKind.Board && c.Valid ? "  HardLockRisk " + c.HardLockRisk.ToString("0.00") : "")
                            .Append('\n');
                    }
                    if (c.Kind == p.Kind && c.Valid)
                    {
                        chosen = letter.ToString();
                    }
                    letter++;
                }
                if (TamagotchiDebug.ShowPunishView)
                {
                    sb.Append("Chosen: ").Append(chosen).Append('\n');
                }
            }
            if (sb.Length > 0)
            {
                Vector2 at = Home.PlatesCentre + new Vector2(-F * 0.2f, 1.15f * S);
                DebugText(at, sb.ToString(), white, F > 0 ? TextAnchor.LowerLeft : TextAnchor.LowerRight);
            }
            if (p != null && p.Kind == PetPunishKind.Board || (p != null && TamagotchiDebug.ShowBoardEatCandidates))
            {
                DrawBoardDebug(p);
            }
            if (TamagotchiDebug.ShowFoodProxyBounds || TamagotchiDebug.ShowEatingProxy)
            {
                foreach (TamagotchiFoodProxy f in GetComponentsInChildren<TamagotchiFoodProxy>())
                {
                    Bounds b = f.WorldBounds;
                    DebugRect(new Rect(b.min, b.size), new Color(0.5f, 1f, 0.5f));
                    if (TamagotchiDebug.ShowCardValueTier)
                    {
                        DebugText((Vector2)b.max, f.Tier.ToString(), new Color(0.5f, 1f, 0.5f));
                    }
                }
            }
            if (TamagotchiDebug.ShowBiteMask)
            {
                Vector2 m = rig.MouthWorld + new Vector3(0f, -0.012f * S, 0f);
                DebugLine(m + new Vector2(-0.6f * S, 0f), m + new Vector2(0.6f * S, 0f), pink);
                float tooth = 0.62f * S / 5.2f;
                DebugCircle(m + new Vector2(-1.55f * tooth, -0.05f * tooth), 0.95f * tooth, pink);
                DebugCircle(m + new Vector2(0f, -0.32f * tooth), 1.08f * tooth, pink);
                DebugCircle(m + new Vector2(1.55f * tooth, -0.05f * tooth), 0.95f * tooth, pink);
            }
        }

        private List<TamagotchiHome> debugCandidates;
        private float debugCandidatesAt = -10f;

        /// <summary>The corrective pass's readouts: lines into the shared text block, shapes on the
        /// screen where the thing they are about is.</summary>
        private void DrawCorrectiveDebug(StringBuilder sb)
        {
            Color cyan = new Color(0.4f, 0.95f, 1f);
            Color green = new Color(0.45f, 1f, 0.55f);
            Color berry = new Color(1f, 0.35f, 0.65f);
            Color amber = new Color(1f, 0.75f, 0.3f);
            if (TamagotchiDebug.ShowRoamingCandidates && HomeCandidates != null)
            {
                // asked twice a second: it is the controller's whole solver
                if (clock - debugCandidatesAt > 0.5f || debugCandidates == null)
                {
                    debugCandidates = HomeCandidates(State.Furious);
                    debugCandidatesAt = clock;
                }
                foreach (TamagotchiHome h in debugCandidates)
                {
                    bool here = h.Name == Home.Name;
                    DebugRect(h.PetRect, here ? cyan : green);
                    DebugText(new Vector2(h.PetRect.center.x, h.PetRect.yMax + 0.1f), h.Name + (h.Sit > 0f ? " [sit]" : "")
                        + (h.Rest < 0.95f ? " [peek]" : ""), here ? cyan : green);
                }
                sb.Append("roaming: ").Append(debugCandidates.Count).Append(" safe places, turn ")
                    .Append(Roaming.TurnsSinceMove).Append('/').Append(Roaming.NextMoveInTurns).Append('\n');
            }
            if (TamagotchiDebug.ShowChosenRoamingAnchor)
            {
                if (MovingTo != null)
                {
                    DebugCross(MovingTo.Base, 0.25f, amber);
                    DebugRect(MovingTo.PetRect, amber);
                    DebugLine(rig.BellyWorld, MovingTo.Base + HomeUp * (0.5f * S), amber);
                }
                sb.Append("move: ").Append(MovingTo != null ? LastMoveStyle + " -> " + MovingTo.Name : "at " + Home.Name)
                    .Append("  (last style ").Append(LastMoveStyle).Append(")\n");
                sb.Append("lived at: ").Append(string.Join(" > ", new List<string>(Roaming.History).ToArray())).Append('\n');
            }
            if (TamagotchiDebug.ShowSpeechBubbleAnchor)
            {
                DebugCross(rig.MouthWorld, 0.1f, berry);
                if (SpeechShowing != null)
                {
                    DebugRect(SpeechRect, berry);
                    DebugLine(SpeechRect.center, rig.MouthWorld, berry);
                }
                sb.Append("speech: ").Append(SpeechShowing != null ? "\"" + SpeechShowing.Replace("\n", " ") + "\"" : "-")
                    .Append("  next remark in ").Append(Mathf.Max(0f, nextBubbleGap - (clock - lastBubbleAt)).ToString("0.0")).Append(" s\n");
            }
            if (TamagotchiDebug.ShowFuryFaceRig)
            {
                DebugCross(rig.EyeWorld(true), 0.06f, berry);
                DebugCross(rig.EyeWorld(false), 0.06f, berry);
                DebugCross(rig.MouthWorld, 0.08f, berry);
                sb.Append("fury face: skin ").Append(furyNow.ToString("0.00")).Append("  mouth ").Append(face.Mouth)
                    .Append(" x").Append(face.MouthScale.ToString("0.00")).Append("  squint L").Append(act.SquintL.ToString("0.0"))
                    .Append(" R").Append(act.SquintR.ToString("0.0")).Append("  iris ").Append(face.Iris.ToString("0.00")).Append('\n');
            }
            if (TamagotchiDebug.ShowFuryBodyDeform)
            {
                sb.Append("fury body: squash ").Append(squashNow.x.ToString("0.000")).Append(" x ").Append(squashNow.y.ToString("0.000"))
                    .Append("  scale ").Append(pose.Scale.ToString("0.000")).Append("  lean ").Append(rotNow.ToString("0.0"))
                    .Append(" deg  offset ").Append((offsetNow.x / Mathf.Max(0.0001f, Home.PxToWorld)).ToString("0.0")).Append(',')
                    .Append((offsetNow.y / Mathf.Max(0.0001f, Home.PxToWorld)).ToString("0.0")).Append(" px\n");
            }
            if (TamagotchiDebug.ShowHatredField)
            {
                DebugCross(hatredSource, 0.2f, berry);
                sb.Append("hatred: ambient ").Append(hatredAmbient.ToString("0.00")).Append("  surge ").Append(hatredSurge.ToString("0.00"))
                    .Append("  impact ").Append(hatredImpact.ToString("0.00")).Append("  level ").Append(HatredLevel.ToString("0.00"))
                    .Append(hatred != null && !hatred.Supported ? "  (NO SHADER: nothing drawn)" : "").Append('\n');
            }
            if (TamagotchiDebug.ShowHatredWaveProgress)
            {
                if (hatredWave >= 0f)
                {
                    float reach = 0f;
                    Rect sc = Anchors.Screen;
                    foreach (Vector2 corner in new[] { new Vector2(sc.xMin, sc.yMin), new Vector2(sc.xMax, sc.yMin),
                        new Vector2(sc.xMin, sc.yMax), new Vector2(sc.xMax, sc.yMax) })
                    {
                        reach = Mathf.Max(reach, Vector2.Distance(corner, hatredSource));
                    }
                    DebugCircle(hatredSource, hatredWave * reach * 1.08f, berry);
                }
                sb.Append("wave: ").Append(hatredWave >= 0f ? hatredWave.ToString("0.00") : "-").Append('\n');
            }
            if (TamagotchiDebug.ShowScreenVignette)
            {
                sb.Append("vignette: ").Append(HatredVignetteNow.ToString("0.000")).Append(" perceptual")
                    .Append(Anchors.DebtVignette > 0.001f ? "  (debt already shows " + Anchors.DebtVignette.ToString("0.000") + ": the heavier wins)" : "")
                    .Append('\n');
            }
            if (TamagotchiDebug.ShowChromaticEdge)
            {
                Rect sc = Anchors.Screen;
                float inset = Px(Tuning.HatredChromaticOffset * 2.6f);
                DebugRect(new Rect(sc.xMin + inset, sc.yMin + inset, sc.width - inset * 2f, sc.height - inset * 2f),
                    new Color(1f, 0.35f, 0.65f, 0.25f + 0.75f * HatredChromaticNow));
                sb.Append("edge split: ").Append(HatredChromaticNow.ToString("0.00")).Append("  offset ")
                    .Append(Tuning.HatredChromaticOffset.ToString("0.0")).Append(" px\n");
            }
            if (TamagotchiDebug.ShowCurrentAudioEvent)
            {
                sb.Append("audio: ").Append(sfx != null && sfx.PetLastCue != null ? sfx.PetLastCue : LastSound.ToString())
                    .Append("  ").Append((clock - LastSoundAt).ToString("0.00")).Append(" s ago\n");
            }
            if (TamagotchiDebug.ShowAudioLayerNames && sfx != null)
            {
                sb.Append("layers: ").Append(sfx.PetLastLayers ?? "-").Append('\n');
            }
            if (TamagotchiDebug.ShowAudioDuckAmount && sfx != null)
            {
                sb.Append("duck: ").Append((sfx.PetDuckNow * 100f).ToString("0")).Append("% of the rest of the game\n");
            }
            if (TamagotchiDebug.ShowPunishPhase)
            {
                sb.Append("phase: ").Append(Playing).Append(" / ").Append(CurrentBeat ?? "-").Append("  pace ")
                    .Append(pace.ToString("0.00")).Append("  last punish ").Append(LastPunishSeconds.ToString("0.00")).Append(" s\n");
            }
            if (TamagotchiDebug.ShowBiteProgress)
            {
                Vector2 at = (Vector2)rig.MouthWorld + new Vector2(-0.4f * S, -0.75f * S);
                DebugLine(at, at + new Vector2(0.8f * S, 0f), new Color(1f, 1f, 1f, 0.35f));
                DebugLine(at, at + new Vector2(0.8f * S * Mathf.Clamp01(BiteProgressNow), 0f), berry);
                sb.Append("bite: ").Append((BiteProgressNow * 100f).ToString("0")).Append("%\n");
            }
            if (TamagotchiDebug.ShowBoardChunkPath)
            {
                int half = DebugChunkPaths.Count / 2;
                for (int i = 0; i < half; i++)
                {
                    DebugLine(DebugChunkPaths[i], DebugChunkPaths[half + i], amber);
                    DebugCross(DebugChunkPaths[i], 0.1f, amber);
                }
            }
            if (TamagotchiDebug.ShowAssetPullPath)
            {
                for (int i = 1; i < DebugPullPath.Count; i++)
                {
                    DebugLine(DebugPullPath[i - 1], DebugPullPath[i], amber);
                }
                if (itemRect.width > 0f && prepared != null)
                {
                    DebugRect(itemRect, amber);
                }
            }
        }

        private void DrawBoardDebug(PetRampageVisuals p)
        {
            if (p == null)
            {
                return;
            }
            float cell = Anchors.CellSize;
            if (TamagotchiDebug.ShowBoardEatCandidates || TamagotchiDebug.ShowBoardEatScores
                || TamagotchiDebug.ShowRejectedHardLockCandidates)
            {
                float min = float.MaxValue;
                float max = float.MinValue;
                foreach (PetCellScore s in p.CellScores)
                {
                    if (!s.Rejected)
                    {
                        min = Mathf.Min(min, s.Room);
                        max = Mathf.Max(max, s.Room);
                    }
                }
                foreach (PetCellScore s in p.CellScores)
                {
                    Vector2 at = CellWorld(s.Cell);
                    if (s.Rejected)
                    {
                        if (TamagotchiDebug.ShowRejectedHardLockCandidates)
                        {
                            DebugLine(at + new Vector2(-0.3f, -0.3f) * cell, at + new Vector2(0.3f, 0.3f) * cell, Color.red);
                            DebugLine(at + new Vector2(-0.3f, 0.3f) * cell, at + new Vector2(0.3f, -0.3f) * cell, Color.red);
                        }
                        continue;
                    }
                    if (TamagotchiDebug.ShowBoardEatCandidates)
                    {
                        // cruel (low room) red, kind (lots of room) green
                        float k = max > min ? (s.Room - min) / (max - min) : 1f;
                        DebugRect(new Rect(at - Vector2.one * cell * 0.4f, Vector2.one * cell * 0.8f),
                            Color.Lerp(new Color(1f, 0.25f, 0.3f), new Color(0.3f, 1f, 0.45f), k));
                    }
                    if (TamagotchiDebug.ShowBoardEatScores)
                    {
                        DebugText(at, s.Room.ToString("0"), Color.white);
                    }
                }
            }
            if (TamagotchiDebug.ShowChosenBoardEatCells && p.Kind == PetPunishKind.Board)
            {
                foreach (GridPos c in p.Cells)
                {
                    Vector2 at = CellWorld(c);
                    DebugRect(new Rect(at - Vector2.one * cell * 0.5f, Vector2.one * cell), new Color(1f, 0.4f, 0.8f));
                }
            }
        }

        // ---- primitives

        private void EnsureDebugRoot()
        {
            if (debugRoot == null)
            {
                debugRoot = new GameObject("PetDebug").transform;
                debugRoot.SetParent(flight, false);
            }
        }

        private void DebugLine(Vector2 a, Vector2 b, Color c)
        {
            EnsureDebugRoot();
            if (debugLineUsed >= debugLines.Count)
            {
                var go = new GameObject("DebugLine");
                go.transform.SetParent(debugRoot, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = ViewUtil.WhiteSprite;
                r.sortingOrder = DebugOrder;
                debugLines.Add(r);
            }
            SpriteRenderer line = debugLines[debugLineUsed++];
            line.enabled = true;
            line.color = c;
            Vector2 d = b - a;
            line.transform.position = (a + b) * 0.5f;
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(Mathf.Max(0.001f, d.magnitude), 0.02f, 1f);
        }

        private void DebugRect(Rect r, Color c)
        {
            DebugLine(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), c);
            DebugLine(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), c);
            DebugLine(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), c);
            DebugLine(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), c);
        }

        private void DebugCross(Vector2 at, float size, Color c)
        {
            DebugLine(at + new Vector2(-size, 0f), at + new Vector2(size, 0f), c);
            DebugLine(at + new Vector2(0f, -size), at + new Vector2(0f, size), c);
        }

        private void DebugCircle(Vector2 at, float radius, Color c)
        {
            const int n = 28;
            for (int i = 0; i < n; i++)
            {
                float a0 = i / (float)n * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)n * Mathf.PI * 2f;
                DebugLine(at + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                    at + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, c);
            }
        }

        private void DebugText(Vector2 at, string text, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            EnsureDebugRoot();
            if (debugTextUsed >= debugTexts.Count)
            {
                TextMesh t = ViewUtil.MakeText3D(debugRoot, "DebugText", Vector2.zero, "", 90, 0.012f, c,
                    DebugOrder + 1, anchor);
                debugTexts.Add(t);
            }
            TextMesh mesh = debugTexts[debugTextUsed++];
            mesh.gameObject.SetActive(true);
            mesh.transform.position = at;
            mesh.anchor = anchor;
            mesh.text = text;
            ViewUtil.SetTextColor(mesh, c);
        }
    }
}
