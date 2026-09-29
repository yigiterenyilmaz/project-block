// PURPOSE: "Parazit"'s SYMBIOTIC IMPRINT - the bound joker's REAL ICON, embedded in the parasite's
// membrane on the host cube, and the assimilation sequence that puts it there.
//
// WHAT IT REPLACES. The icon used to be a separate LOGO on the host: on the board a pod the nest grew
// round it with a film laid over the top, in the hand a dark disc with the icon on it. Both said "an
// icon was stuck on this cube" - a badge, a medallion, a token - and none of it had any physical
// relation to the membrane that is the whole identity of the host. The parasite has not been handed
// a badge. It has TAKEN the joker the way it has taken the cube, and the icon is now a brand pressed
// into its living tissue:
//
//   THE ICON      the joker's own art (never a generic symbol, never redrawn in code), fitted by its
//                 real silhouette (ParasiteIconProfile) to about a third of the cube and set a
//                 little off centre in the film. Its colour is partly drained and pulled toward the
//                 membrane's hue - Midas is still gold and Yangın still warm, but both have been
//                 taken - and a thin film lies over it with one or two bites out of its edges
//                 (ParasiteEmbeddedIcon.shader). No plate, no ring, no frame, no backdrop.
//   ITS BED       the film is darker and heavier where it holds the icon, and the block is drained
//                 harder in a few pixels round it (the membrane's and the drain's _IconZone): a
//                 PIGMENT DRAIN ZONE, never a halo. It is what lets the icon read on a pale block and
//                 an obsidian one alike.
//   THE FIBERS    four to seven thin organic fibers grow out of the film and grip the icon by
//                 DIFFERENT EDGES of its real outline - never its centre, never four straight lines
//                 to a middle. Uneven, slightly curved, one or two of them branching; one wraps round
//                 BEHIND the icon, the rest cross in FRONT, so the icon is held by the film rather
//                 than sewn onto it.
//
// ONE PHYSICAL SYSTEM. The icon has no motion of its own - no bob, no breathing, no pulse. When the
// cube pushes against the wrap the icon is dragged a fraction of the way with the film and stretched a
// percent along the push, the fibers on the far side tighten and the near ones slacken, and then it
// settles. Every few seconds a CONTRACTION WAVE crosses its region - one side, the middle, the other
// side - which says the film is still alive and still holding the joker too.
//
// THE ATTACH SEQUENCE ("Parazit" binding a joker - the market's attach panel, and the lab):
//   A  APPROACH   the icon's essence leaves its source (the joker row it was picked from) on one
//                 shallow arc, drying a little and shrinking to 0.85, with two to four tiny motes of
//                 its own colour behind it. Never the whole card, never a comet.
//   B  DIMPLE     a beat before it lands the film RECOGNISES it: the landing point collapses inward
//                 and the strands near it lean toward it. No flash, no burst.
//   C  SINK       it goes INTO the film rather than onto it: 1.00 -> 0.92 -> 0.87, opacity to about
//                 0.8, the film's veil and the bites out of its edges coming up in front of it, its
//                 inner edge darkening as it settles two pixels down.
//   D  GRAB       the fibers grow in on a stagger, each eased onto a different edge, and each one
//                 that takes hold tugs the icon a pixel its way before it settles back.
//   E  SEAL       one short compression pulse round it (1.00 -> 0.97 -> 1.00), its colour passes to
//                 its final parasitic treatment, and one dark wet sheen crosses it. Then it is part
//                 of the tissue.
// On the board a host that is already bound never replays this - a placed host comes in with its
// imprint already there and simply settles with the film (the ordinary seating).
//
// DEATH. The line takes the host and the joker with it: the icon does not come out as a separate
// item and it never flies back to the deck. It is dragged with the film as the wrap fails, splits
// along two to four jagged cuts into pieces that part with the tear, drains from its own colour to
// grey to dark plum, and is gone with the parasite's own material - while its fibers snap one by one.
// A power that the rules refuse gets only a short contraction and a micro tug: no death, no shield.
//
// THE VIEW DECIDES NONE OF IT. Which joker rides which cube is the controller's to say
// (SetRider, from the joker), a refusal is Core's, and the line that kills it is Core's. What is
// here is how it looks.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class ParasiteHostView
    {
        // =================================================================== TUNING
        /// <summary>Everything about the imprint's look and timing. Sizes are a share of a CUBE
        /// unless they say pixels; times are seconds. These are the start values the design names
        /// (parasiteIcon*, parasiteFiber*, parasiteAttach*, parasiteIdle*).</summary>
        public static class ImprintStyle
        {
            // ---- the icon ----
            /// <summary>The icon's visible silhouette, as a share of the cube's width
            /// (parasiteIconScale). A busy icon gets up to IconScaleRange more, a simple one up to
            /// that much less - 0.28..0.40 of the cube at the defaults.</summary>
            public static float IconScale = 0.34f;

            public static float IconScaleRange = 0.06f;

            /// <summary>Where it sits, in the cube's normalised space (0..1). Off centre, where the
            /// nest has stepped aside for it; one point per host, chosen inside this box.</summary>
            public static Vector2 IconSpotMin = new Vector2(0.52f, 0.46f);

            public static Vector2 IconSpotMax = new Vector2(0.62f, 0.56f);

            /// <summary>parasiteIconOpacity - how much of the icon shows through the film.</summary>
            public static float IconOpacity = 0.78f;

            /// <summary>parasiteIconColorRetention - how much of its own colour survives.</summary>
            public static float ColorRetention = 0.65f;

            /// <summary>parasiteIconDesaturation.</summary>
            public static float Desaturation = 0.25f;

            /// <summary>parasiteIconMembraneTint - the pull toward the membrane's hue.</summary>
            public static float MembraneTint = 0.25f;

            /// <summary>Brightness inside the film, before contrast compensation.</summary>
            public static float Brightness = 0.84f;

            /// <summary>The thin film lying over the whole icon.</summary>
            public static float Veil = 0.2f;

            /// <summary>parasiteIconDepth - how far it is sunk, in PIXELS.</summary>
            public static float DepthPixels = 2f;

            /// <summary>parasiteIconUvDistortion, in PIXELS.</summary>
            public static float UvDistortionPixels = 0.8f;

            public static float RadialSqueeze = 0.03f;

            /// <summary>parasiteIconEdgeConsume - how much of its outline the film has eaten.</summary>
            public static float EdgeConsume = 0.08f;

            /// <summary>parasiteIconContrastBoost: when the icon's own luminance is within
            /// ContrastThreshold of the film's, its brightness moves away from it by up to this.
            /// </summary>
            public static float ContrastBoost = 0.12f;

            public static float ContrastThreshold = 0.18f;

            /// <summary>The thin membrane-lit rim on a DARK host (0.15-0.30 of opacity, by how
            /// dark). Off on a light one.</summary>
            public static float RimMin = 0.15f;

            public static float RimMax = 0.3f;

            /// <summary>How far round the icon the block is drained harder, in pixels.</summary>
            public static float PigmentZonePixels = 7f;

            public static float PigmentZoneStrength = 1f;

            /// <summary>The nest, when an imprint has taken the middle: smaller, and off to the far
            /// side of the icon.</summary>
            public static float NestScaleWithIcon = 0.78f;

            public static float NestAway = 0.25f;

            // ---- the fibers ----
            public static int FiberCountMin = 5;

            public static int FiberCountMax = 5;

            /// <summary>Their width at the thickest, as a share of the cube.</summary>
            public static float FiberThicknessMin = 0.014f;

            public static float FiberThicknessMax = 0.024f;

            /// <summary>How far past the icon's edge a fiber is rooted in the film.</summary>
            public static float FiberReachMin = 0.1f;

            public static float FiberReachMax = 0.19f;

            public static float FiberBow = 0.22f;

            /// <summary>parasiteFiberTension - how hard they answer the cube pushing.</summary>
            public static float FiberTension = 0.8f;

            public static float FiberOpacity = 0.86f;

            public static int FiberSegments = 6;

            // ---- the attach sequence ----
            /// <summary>parasiteAttachTravelDuration.</summary>
            public static float AttachTravel = 0.22f;

            /// <summary>How far the approach arc bows, as a share of its length.</summary>
            public static float AttachArc = 0.18f;

            /// <summary>The scale the icon ends its approach at, relative to where it set off.
            /// </summary>
            public static float ApproachEndScale = 0.85f;

            /// <summary>parasiteMembraneDimpleDuration, and how long before landing it starts.
            /// </summary>
            public static float DimpleDuration = 0.09f;

            public static float DimpleLead = 0.055f;

            public static float DimpleStrength = 0.9f;

            /// <summary>parasiteIconSinkDuration, and the scale it sinks through.</summary>
            public static float SinkDuration = 0.16f;

            public static float SinkMid = 0.92f;

            public static float SinkFinal = 0.87f;

            /// <summary>parasiteFiberGrowthDuration (each fiber, min..max) and their stagger.</summary>
            public static float FiberGrowMin = 0.10f;

            public static float FiberGrowMax = 0.14f;

            public static float FiberStaggerMin = 0.015f;

            public static float FiberStaggerMax = 0.03f;

            /// <summary>The micro tug a fiber gives as it takes hold, in pixels.</summary>
            public static float FiberTugPixels = 1.5f;

            public static float FiberTugDuration = 0.09f;

            /// <summary>parasiteSealDuration, and the compression pulse inside it.</summary>
            public static float SealDuration = 0.13f;

            public static float SealPulse = 0.03f;

            /// <summary>The colour assimilation and the one wet sheen, from the seal on.</summary>
            public static float AssimilateDuration = 0.15f;

            public static float SheenDuration = 0.2f;

            public static float SheenAmount = 0.35f;

            public static int AttachMotes = 3;

            // ---- idle ----
            /// <summary>parasiteIdleIconFollowStrength - how much of the cube's push the icon goes
            /// with. It is in the film, so it goes most of the way, never all of it.</summary>
            public static float IdleIconFollow = 0.7f;

            /// <summary>The stretch along the push and the squeeze across it.</summary>
            public static float IdleStretchAlong = 0.02f;

            public static float IdleStretchAcross = 0.01f;

            /// <summary>parasiteIdleRareContractionMin/Max, and one wave's length.</summary>
            public static float WaveMinInterval = 3f;

            public static float WaveMaxInterval = 6f;

            public static float WaveDuration = 0.28f;

            public static float WaveStrength = 1f;

            /// <summary>A refused power: the tug toward the force, in pixels.</summary>
            public static float ResistTugPixels = 1.5f;
        }

        /// <summary>What the lab can switch off, one at a time - each through the thing it names,
        /// never by hiding a renderer after the fact.</summary>
        public static class ImprintLayers
        {
            public static bool ShowEmbeddedIcon = true;

            /// <summary>The raw icon drawn BESIDE the host, for comparison.</summary>
            public static bool ShowOriginalIcon = false;

            /// <summary>The film's veil over the icon.</summary>
            public static bool ShowMembraneFrontMask = true;

            /// <summary>The darker film under it (its bed in the membrane).</summary>
            public static bool ShowMembraneBackMask = true;

            public static bool ShowFiberBack = true;

            public static bool ShowFiberFront = true;

            public static bool ShowIconUVDistortion = true;

            public static bool ShowIconOcclusion = true;

            public static bool ShowIconContrastCompensation = true;

            public static bool ShowIconColorRetention = true;

            public static bool ShowIconBounds = false;

            public static bool ShowAttachPoints = false;

            public static bool ShowMembraneDimple = true;

            public static bool ShowPigmentDrain = true;

            /// <summary>The lab's playback rate for the attach sequence - 1x, 0.5x, 0.25x - so it
            /// can be watched without slowing the whole game.</summary>
            public static float AttachRate = 1f;

            public static void AllOn()
            {
                ShowEmbeddedIcon = true;
                ShowOriginalIcon = false;
                ShowMembraneFrontMask = true;
                ShowMembraneBackMask = true;
                ShowFiberBack = true;
                ShowFiberFront = true;
                ShowIconUVDistortion = true;
                ShowIconOcclusion = true;
                ShowIconContrastCompensation = true;
                ShowIconColorRetention = true;
                ShowIconBounds = false;
                ShowAttachPoints = false;
                ShowMembraneDimple = true;
                ShowPigmentDrain = true;
                AttachRate = 1f;
            }
        }

        // =================================================================== the material

        private static Material embeddedIconMaterial;

        private static bool embeddedIconLooked;

        /// <summary>The ParasiteEmbeddedIcon material, or null - the icon is then drawn on the plain
        /// sprite material, dimmed and pulled toward the film's colour by tint alone. Still the
        /// real icon, still not a badge.</summary>
        public static Material EmbeddedIconMaterial()
        {
            if (!embeddedIconLooked)
            {
                embeddedIconLooked = true;
                Shader shader = Shader.Find("ProjectBlock/ParasiteEmbeddedIcon");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/ParasiteEmbeddedIcon");
                }
                if (shader != null && shader.isSupported)
                {
                    embeddedIconMaterial = new Material(shader);
                    embeddedIconMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return embeddedIconMaterial;
        }

        private static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        private static readonly int VisibleId = Shader.PropertyToID("_Visible");
        private static readonly int MembraneColourId = Shader.PropertyToID("_Membrane");
        private static readonly int MembraneDeepId = Shader.PropertyToID("_MembraneDeep");
        private static readonly int TreatmentId = Shader.PropertyToID("_Treatment");
        private static readonly int RetentionId = Shader.PropertyToID("_Retention");
        private static readonly int IconDesatId = Shader.PropertyToID("_Desat");
        private static readonly int MembraneTintId = Shader.PropertyToID("_MembraneTint");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int IconOpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int VeilId = Shader.PropertyToID("_Veil");
        private static readonly int OcclusionId = Shader.PropertyToID("_Occlusion");
        private static readonly int DepthId = Shader.PropertyToID("_Depth");
        private static readonly int DistortId = Shader.PropertyToID("_Distort");
        private static readonly int StretchId = Shader.PropertyToID("_Stretch");
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int IconRimId = Shader.PropertyToID("_Rim");
        private static readonly int IconRimColourId = Shader.PropertyToID("_RimColour");
        private static readonly int IconSheenId = Shader.PropertyToID("_Sheen");
        private static readonly int IconTearId = Shader.PropertyToID("_Tear");
        private static readonly int DeathId = Shader.PropertyToID("_Death");

        // =================================================================== who rides where

        private struct RiderInfo
        {
            public Sprite Icon;
            /// <summary>The lab's comparison: the OLD look, the plain icon on the cube's corner.
            /// Never set by the game.</summary>
            public bool Sticker;
            /// <summary>Put there by the lab (SetRiderAt), so the game's own SetRider never
            /// takes it down under a scene that is playing.</summary>
            public bool Lab;
        }

        private readonly List<GridPos> riderScratch = new List<GridPos>();

        private readonly Dictionary<GridPos, RiderInfo> riders = new Dictionary<GridPos, RiderInfo>();

        /// <summary>The marks on the attach timeline, for playing one phase of it alone (the lab).
        /// </summary>
        public enum AttachPhase
        {
            Start,
            Dimple,
            Land,
            Grab,
            Seal,
            End
        }

        private struct AttachRequest
        {
            public bool HasSource;
            public Vector2 Source;
            public float SourceSize;
            public float StartAt;
            public float StopAt;
            public bool ByPhase;
            public AttachPhase From;
            public AttachPhase To;
        }

        private readonly Dictionary<GridPos, AttachRequest> pendingAttach =
            new Dictionary<GridPos, AttachRequest>();

        /// <summary>
        /// The bound joker's ICON and the cell of the host that carries it. Null takes it off. Asked
        /// every repaint (BoardView.SetParasiteRider); the joker is the only source. There is one
        /// passenger in the game, so this replaces whatever rode before.
        /// </summary>
        public void SetRider(GridPos? cell, Sprite icon)
        {
            riderScratch.Clear();
            foreach (KeyValuePair<GridPos, RiderInfo> entry in riders)
            {
                if (!entry.Value.Lab && (!cell.HasValue || !entry.Key.Equals(cell.Value)))
                {
                    riderScratch.Add(entry.Key);
                }
            }
            for (int i = 0; i < riderScratch.Count; i++)
            {
                riders.Remove(riderScratch[i]);
            }
            if (cell.HasValue)
            {
                if (icon != null)
                {
                    riders[cell.Value] = new RiderInfo { Icon = icon };
                }
                else
                {
                    riders.Remove(cell.Value);
                }
            }
        }

        /// <summary>The lab's form: several riders at once (the readability grid), and the OLD
        /// sticker look beside the new one for the side-by-side test.</summary>
        public void SetRiderAt(GridPos cell, Sprite icon, bool sticker)
        {
            if (icon == null)
            {
                riders.Remove(cell);
                return;
            }
            riders[cell] = new RiderInfo { Icon = icon, Sticker = sticker, Lab = true };
        }

        /// <summary>
        /// Plays the ASSIMILATION on a host: the icon leaves <paramref name="source"/> (this view's
        /// own space; null spawns it as a small essence on the host itself), is recognised by the
        /// film, sinks in, is gripped and sealed. <paramref name="sourceSize"/> is how big the icon
        /// was where it set off. The lab can start part-way in and hold at a point, to watch one
        /// phase alone.
        /// </summary>
        public void PlayAttach(GridPos cell, Vector2? source, float sourceSize)
        {
            PlayAttach(cell, source, sourceSize, 0f, float.MaxValue);
        }

        public void PlayAttach(GridPos cell, Vector2? source, float sourceSize, float startAt,
            float stopAt)
        {
            var request = new AttachRequest
            {
                HasSource = source.HasValue,
                Source = source.HasValue ? source.Value : Vector2.zero,
                SourceSize = sourceSize,
                StartAt = Mathf.Max(0f, startAt),
                StopAt = stopAt
            };
            Request(cell, request);
        }

        /// <summary>
        /// One PHASE of the assimilation alone, for the lab: it starts at <paramref name="from"/>
        /// and holds still at <paramref name="to"/> (End plays it out). Phases rather than
        /// seconds, because each host's fiber stagger is its own and a mark in seconds would land
        /// in a different place on every host.
        /// </summary>
        public void PlayAttachPhase(GridPos cell, Vector2? source, float sourceSize,
            AttachPhase from, AttachPhase to)
        {
            Request(cell, new AttachRequest
            {
                HasSource = source.HasValue,
                Source = source.HasValue ? source.Value : Vector2.zero,
                SourceSize = sourceSize,
                ByPhase = true,
                From = from,
                To = to
            });
        }

        private void Request(GridPos cell, AttachRequest request)
        {
            HostPiece h;
            if (hosts.TryGetValue(cell, out h) && h.Icon != null && !h.Icon.Sticker)
            {
                StartAttach(h.Icon, request);
                return;
            }
            pendingAttach[cell] = request;
        }

        private static float PhaseTime(Imprint im, AttachPhase phase)
        {
            switch (phase)
            {
                case AttachPhase.Start:
                    return 0f;
                case AttachPhase.Dimple:
                    return Mathf.Max(0f, im.Land - ImprintStyle.DimpleLead - 0.01f);
                case AttachPhase.Land:
                    return im.Land;
                case AttachPhase.Grab:
                    return im.Fibers.Count > 0 ? im.Fibers[0].GrowStart : im.Land;
                case AttachPhase.Seal:
                    return im.SealStart;
                default:
                    return float.MaxValue;
            }
        }

        /// <summary>How long the whole assimilation takes on this host, or 0 with none.</summary>
        public float AttachDuration(GridPos cell)
        {
            HostPiece h;
            return hosts.TryGetValue(cell, out h) && h.Icon != null ? h.Icon.Total : 0f;
        }

        /// <summary>The lab: the cube tries to get out NOW, from the next preset region.</summary>
        public void ForceStruggle(GridPos cell)
        {
            HostPiece h;
            if (!hosts.TryGetValue(cell, out h) || h.RuptureClock >= 0f)
            {
                return;
            }
            h.SeatClock = Mathf.Max(h.SeatClock, Style.SeatTotal);
            h.IdleClock = 0f;
            h.BulgeRegion = (h.BulgeRegion + 1) % BulgeRegions.Length;
            h.BulgeAt = BulgeRegions[h.BulgeRegion];
            h.StainAt = h.BulgeAt;
        }

        /// <summary>The lab: the rare contraction wave crosses the imprint NOW.</summary>
        public void ForceWave(GridPos cell)
        {
            HostPiece h;
            if (hosts.TryGetValue(cell, out h) && h.Icon != null)
            {
                StartWave(h.Cell, h.Icon);
            }
        }

        // =================================================================== one imprint

        private sealed class Fiber
        {
            public readonly List<SpriteRenderer> Segments = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Branch = new List<SpriteRenderer>();
            /// <summary>Behind the icon (true) or across it in front.</summary>
            public bool Back;
            /// <summary>Where it is rooted in the film, relative to the icon's centre, in cube
            /// units - so the root rides the film, and the tip rides the icon.</summary>
            public Vector2 Root;
            /// <summary>The point of the icon's own outline it grips, in the icon's n-space, and
            /// how far inside that edge its end lies (front fibers overlap the icon a little).
            /// </summary>
            public Vector2 Grip;
            public float Inset;
            public float Width;
            public float Bow;
            /// <summary>Its own uneven thickness, so no two fibers are the same cord.</summary>
            public float Lumps;
            public float GrowStart;
            public float GrowTime;
            /// <summary>Seconds into the rupture at which it snaps.</summary>
            public float SnapAt;
        }

        private sealed class Imprint
        {
            public Sprite Sprite;
            public ParasiteIconProfile Profile;
            public bool Sticker;
            public SpriteRenderer Icon;
            public readonly List<Fiber> Fibers = new List<Fiber>();
            public readonly List<SpriteRenderer> Motes = new List<SpriteRenderer>();
            /// <summary>The lab's overlays, rented only while their switch is on.</summary>
            public readonly List<SpriteRenderer> Debug = new List<SpriteRenderer>();
            public SpriteRenderer Original;

            /// <summary>Its place on the cube (cube units from the centre), its size (a share of
            /// the cube), which occlusion variant the film has grown into and the seed of its
            /// details. All stable per host.</summary>
            public Vector2 Spot;
            public float Size;
            public int Variant;
            public float Seed;

            // ---- the attach sequence ----
            public float AttachClock = -1f;
            public float StopAt = float.MaxValue;
            public bool Held;
            public bool HasSource;
            public Vector2 Source;
            public float SourceSize;
            public float Land;
            public float SealStart;
            public float Total;

            public bool Attaching
            {
                get { return AttachClock >= 0f && !Held; }
            }

            // ---- the rare contraction wave ----
            public float WaveWait;
            public float WaveClock = -1f;
            public Vector2 WaveDir = Vector2.right;
            public int WaveCount;

            // ---- what this frame worked out, for the pieces painted after it ----
            public float Dimple;
            public Vector2 DimpleAt;
            public Vector2 Centre;
            public float VisibleSize;
            public float Show;
            public float Treat;
            public float Sink;
            public float SheenAt = -1f;
            public float Phase;
            /// <summary>Seconds since the controller stopped naming a rider for this host.</summary>
            public float Orphaned;
        }

        /// <summary>How long an imprint outlives its rider before it is taken down, when no
        /// severance comes to claim it.</summary>
        private const float OrphanGrace = 0.3f;

        /// <summary>What the imprint is doing to the film round it this frame.</summary>
        private struct ImprintFrame
        {
            public bool Active;
            public Vector2 Centre;
            public float Size;
            public float Zone;
            public float ZoneRadius;
            public float Dimple;
            public Vector2 DimpleAt;
            public float DimpleRadius;

            /// <summary>The icon's bed as a shader region over a quad of <paramref name="quad"/>
            /// size centred on <paramref name="quadCentre"/>: uv centre, strength, radius.</summary>
            public Vector4 ZoneIn(Vector2 quadCentre, float quad, bool on)
            {
                if (!Active || !on || quad <= 0f)
                {
                    return new Vector4(0.5f, 0.5f, 0f, 0.2f);
                }
                Vector2 uv = new Vector2(0.5f, 0.5f) + (Centre - quadCentre) / quad;
                return new Vector4(uv.x, uv.y, Zone, ZoneRadius / quad);
            }

            public Vector4 DimpleIn(Vector2 quadCentre, float quad)
            {
                if (!Active || Dimple <= 0f || quad <= 0f)
                {
                    return new Vector4(0.5f, 0.5f, 0f, 0.2f);
                }
                Vector2 uv = new Vector2(0.5f, 0.5f) + (DimpleAt - quadCentre) / quad;
                return new Vector4(uv.x, uv.y, Dimple, DimpleRadius / quad);
            }
        }

        /// <summary>Where the imprint sits on a host, in cube units from the cube's centre - one
        /// stable point per cell inside ImprintStyle's box.</summary>
        private static Vector2 ImprintSpot(GridPos cell)
        {
            Vector2 lo = ImprintStyle.IconSpotMin;
            Vector2 hi = ImprintStyle.IconSpotMax;
            return new Vector2(Mathf.Lerp(lo.x, hi.x, Random01(cell, 520)) - 0.5f,
                Mathf.Lerp(lo.y, hi.y, Random01(cell, 521)) - 0.5f);
        }

        /// <summary>Where the nest goes when an imprint has the middle: the far side of it.</summary>
        private static Vector2 ImprintNestOffset(GridPos cell)
        {
            Vector2 spot = ImprintSpot(cell);
            Vector2 away = spot.sqrMagnitude > 1e-6f ? -spot.normalized : new Vector2(-0.8f, -0.6f);
            // Turned a little off the straight line, so the nest and the icon are not a pair of
            // dots on one diagonal.
            float turn = (Random01(cell, 522) - 0.5f) * 0.9f;
            float c = Mathf.Cos(turn);
            float s = Mathf.Sin(turn);
            away = new Vector2(away.x * c - away.y * s, away.x * s + away.y * c);
            return away * ImprintStyle.NestAway;
        }

        private Imprint BuildImprint(HostPiece h, RiderInfo rider)
        {
            var im = new Imprint
            {
                Sprite = rider.Icon,
                Sticker = rider.Sticker,
                Profile = ParasiteIconProfile.For(rider.Icon)
            };
            GridPos cell = h.Cell;
            im.Spot = ImprintSpot(cell);
            im.Variant = (int)(Random01(cell, 500) * 4f) % 4;
            im.Seed = Random01(cell, 501) * 10f;
            float complexity = im.Profile != null ? im.Profile.Complexity : 0.5f;
            im.Size = ImprintStyle.IconScale
                + (complexity - 0.5f) * 2f * ImprintStyle.IconScaleRange;
            im.WaveWait = Mathf.Lerp(ImprintStyle.WaveMinInterval, ImprintStyle.WaveMaxInterval,
                Random01(cell, 530));

            if (rider.Sticker)
            {
                im.Icon = Rent(rider.Icon, IconOrder, null);
                return im;
            }
            im.Icon = Rent(rider.Icon, IconOrder, EmbeddedIconMaterial());

            // THE FIBERS. Spread round the icon but never evenly, each gripping a different part of
            // its real outline, rooted a little way out in the film, and not one of them aimed at
            // the middle.
            Material harness = HarnessMaterial();
            int min = Mathf.Clamp(ImprintStyle.FiberCountMin, 4, 7);
            int max = Mathf.Clamp(ImprintStyle.FiberCountMax, min, 7);
            int count = min + Mathf.Min(max - min,
                (int)(Random01(cell, 510) * (max - min + 1)));
            float baseAngle = Random01(cell, 511) * Mathf.PI * 2f;
            int back = (int)(Random01(cell, 512) * count) % count;
            for (int i = 0; i < count; i++)
            {
                var f = new Fiber();
                float a = baseAngle + i * (Mathf.PI * 2f / count)
                    + (Random01(cell, 540 + i) - 0.5f) * 0.7f;
                f.Grip = im.Profile != null
                    ? im.Profile.EdgePoint(a)
                    : new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.85f;
                f.Back = i == back;
                // Two of the front ones cross well over the edge, the rest just reach it.
                int order = (i - back + count) % count;
                f.Inset = f.Back ? 0.1f : order == 1 || order == count - 1 ? 0.18f : 0.03f;
                float skew = (Random01(cell, 560 + i) - 0.5f) * 0.9f;
                float reach = Mathf.Lerp(ImprintStyle.FiberReachMin, ImprintStyle.FiberReachMax,
                    Random01(cell, 570 + i));
                float r = im.Size * 0.5f + reach;
                f.Root = new Vector2(Mathf.Cos(a + skew), Mathf.Sin(a + skew)) * r;
                // Rooted in the film, never past the cube's edge.
                Vector2 onCube = im.Spot + f.Root;
                if (onCube.magnitude > 0.44f)
                {
                    f.Root = onCube.normalized * 0.44f - im.Spot;
                }
                f.Width = Mathf.Lerp(ImprintStyle.FiberThicknessMin,
                    ImprintStyle.FiberThicknessMax, Random01(cell, 580 + i));
                f.Bow = ImprintStyle.FiberBow * (Random01(cell, 590 + i) * 2f - 1f);
                f.Lumps = Random01(cell, 600 + i) * 6.28f;
                int segments = Mathf.Max(3, ImprintStyle.FiberSegments);
                int fiberOrder = f.Back ? FiberBackOrder : FiberFrontOrder;
                for (int k = 0; k < segments; k++)
                {
                    f.Segments.Add(Rent(ParasiteShapes.TendrilSegment, fiberOrder, harness));
                }
                // SOME BRANCHING: every third fiber puts out one short side shoot.
                if (i % 3 == 0)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        f.Branch.Add(Rent(ParasiteShapes.TendrilSegment, fiberOrder, harness));
                    }
                }
                im.Fibers.Add(f);
            }
            int motes = Mathf.Clamp(ImprintStyle.AttachMotes, 0, 4);
            for (int i = 0; i < motes; i++)
            {
                im.Motes.Add(Rent(ParasiteShapes.Essence, MoteOrder, null));
            }
            Schedule(im, cell);
            return im;
        }

        /// <summary>The attach timeline for this host: when it lands, when each fiber grows, when
        /// the seal closes. Fixed per host, so the lab plays the same sequence every time.</summary>
        private static void Schedule(Imprint im, GridPos cell)
        {
            im.Land = ImprintStyle.AttachTravel;
            float at = im.Land + ImprintStyle.SinkDuration * 0.35f;
            float end = im.Land + ImprintStyle.SinkDuration;
            for (int i = 0; i < im.Fibers.Count; i++)
            {
                Fiber f = im.Fibers[i];
                f.GrowStart = at;
                f.GrowTime = Mathf.Lerp(ImprintStyle.FiberGrowMin, ImprintStyle.FiberGrowMax,
                    Random01(cell, 610 + i));
                f.SnapAt = Style.RuptureFirstRib + i * Style.RuptureRibStagger * 0.8f;
                end = Mathf.Max(end, f.GrowStart + f.GrowTime);
                at += Mathf.Lerp(ImprintStyle.FiberStaggerMin, ImprintStyle.FiberStaggerMax,
                    Random01(cell, 620 + i));
            }
            im.SealStart = end;
            im.Total = im.SealStart + Mathf.Max(ImprintStyle.SealDuration,
                Mathf.Max(ImprintStyle.AssimilateDuration, ImprintStyle.SheenDuration)) + 0.04f;
        }

        private static void StartAttach(Imprint im, AttachRequest request)
        {
            im.HasSource = request.HasSource;
            im.Source = request.Source;
            im.SourceSize = request.SourceSize;
            float start = request.ByPhase ? PhaseTime(im, request.From) : request.StartAt;
            float stop = request.ByPhase ? PhaseTime(im, request.To) : request.StopAt;
            im.AttachClock = Mathf.Min(start, im.Total);
            im.StopAt = stop;
            im.Held = im.AttachClock >= im.StopAt;
        }

        private void StartWave(GridPos cell, Imprint im)
        {
            im.WaveClock = 0f;
            im.WaveCount++;
            float a = Random01(cell, 700 + im.WaveCount) * Mathf.PI * 2f;
            im.WaveDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        private void DropImprint(HostPiece h)
        {
            Imprint im = h.Icon;
            if (im == null)
            {
                return;
            }
            Return(im.Icon);
            for (int i = 0; i < im.Fibers.Count; i++)
            {
                for (int k = 0; k < im.Fibers[i].Segments.Count; k++)
                {
                    Return(im.Fibers[i].Segments[k]);
                }
                for (int k = 0; k < im.Fibers[i].Branch.Count; k++)
                {
                    Return(im.Fibers[i].Branch[k]);
                }
            }
            for (int i = 0; i < im.Motes.Count; i++)
            {
                Return(im.Motes[i]);
            }
            for (int i = 0; i < im.Debug.Count; i++)
            {
                Return(im.Debug[i]);
            }
            Return(im.Original);
            h.Icon = null;
        }

        // =================================================================== the clock

        private void TickImprint(HostPiece h, float dt)
        {
            Imprint im = h.Icon;
            if (im == null || im.Sticker)
            {
                return;
            }
            RiderInfo rider;
            bool named = riders.TryGetValue(h.Cell, out rider);
            im.Orphaned = named ? 0f : im.Orphaned + dt;
            if (im.AttachClock >= 0f && !im.Held)
            {
                // The lab's slow motion reaches the lab's own riders and nothing else - the
                // market's bind must never be caught playing at a quarter speed.
                float rate = named && rider.Lab ? Mathf.Max(0f, ImprintLayers.AttachRate) : 1f;
                im.AttachClock += dt * rate;
                if (im.AttachClock >= im.StopAt)
                {
                    im.AttachClock = im.StopAt;
                    im.Held = true;
                }
                else if (im.AttachClock >= im.Total)
                {
                    im.AttachClock = -1f;
                }
            }
            if (im.WaveClock >= 0f)
            {
                im.WaveClock += dt;
                if (im.WaveClock > ImprintStyle.WaveDuration)
                {
                    im.WaveClock = -1f;
                    im.WaveWait = Mathf.Lerp(ImprintStyle.WaveMinInterval,
                        ImprintStyle.WaveMaxInterval, Random01(h.Cell, 710 + im.WaveCount));
                }
            }
            else if (im.AttachClock < 0f && h.SeatClock >= Style.SeatTotal && h.RuptureClock < 0f
                && h.IdleClock < 0f)
            {
                // RARE, and never on top of an escape attempt - one living beat at a time.
                im.WaveWait -= dt;
                if (im.WaveWait <= 0f)
                {
                    StartWave(h.Cell, im);
                }
            }
        }

        // =================================================================== painting

        /// <summary>A number of screen pixels in this view's own units, so "a pixel of tug" is a
        /// pixel on any board and inside the scaled market panel alike.</summary>
        private float Px(float pixels)
        {
            float cube = cubeSize > 0f ? cubeSize : cellSize;
            Camera cam = Camera.main;
            float scale = Mathf.Abs(transform.lossyScale.y);
            if (scale < 1e-5f)
            {
                scale = 1f;
            }
            if (cam == null || !cam.orthographic || Screen.height <= 0)
            {
                return pixels * cube / 70f;
            }
            return pixels * 2f * cam.orthographicSize / Screen.height / scale;
        }

        /// <summary>How far the landing dimple is pulling a strand whose middle is at
        /// <paramref name="mid"/>.</summary>
        private static Vector2 DimplePull(HostPiece h, Vector2 mid, float cube)
        {
            if (h.Icon == null || h.Icon.Dimple <= 0f)
            {
                return Vector2.zero;
            }
            Vector2 d = h.Icon.DimpleAt - mid;
            float near = 1f - Mathf.Clamp01(d.magnitude / (cube * 0.6f));
            return d.sqrMagnitude > 1e-8f
                ? d.normalized * (h.Icon.Dimple * near * cube * 0.035f)
                : Vector2.zero;
        }

        /// <summary>
        /// Works out where the icon is this frame and what it is doing to the film - before the
        /// drain and the membrane are painted, because both of them have to agree with it (the
        /// bed it lies in, the dimple it lands in).
        /// </summary>
        private ImprintFrame PrepareImprint(HostPiece h, Vector2 at, Vector2 load, float cube,
            float push, float grip, float clamp, float rupture)
        {
            var frame = new ImprintFrame();
            RiderInfo rider;
            if (!riders.TryGetValue(h.Cell, out rider) || rider.Icon == null)
            {
                // THE JOKER DIES WITH THE HOST, ON SCREEN. Core unbinds the passenger the moment
                // the line takes the cube, so the controller stops naming a rider in the same turn
                // the severance starts - and the imprint has to stay for that, or the icon would
                // blink out before the death it is the subject of. A short grace covers a repaint
                // that lands a frame before the severance does.
                if (h.Icon == null || h.Icon.Sticker)
                {
                    DropImprint(h);
                    return frame;
                }
                if (h.RuptureClock < 0f && h.Icon.Orphaned > OrphanGrace)
                {
                    DropImprint(h);
                    return frame;
                }
                rider = new RiderInfo { Icon = h.Icon.Sprite };
            }
            if (h.Icon == null || h.Icon.Sprite != rider.Icon || h.Icon.Sticker != rider.Sticker)
            {
                DropImprint(h);
                h.Icon = BuildImprint(h, rider);
                AttachRequest pending;
                if (pendingAttach.TryGetValue(h.Cell, out pending))
                {
                    pendingAttach.Remove(h.Cell);
                    if (!h.Icon.Sticker)
                    {
                        StartAttach(h.Icon, pending);
                    }
                }
            }
            Imprint im = h.Icon;
            if (im.Sticker)
            {
                return frame;
            }

            // ---- where the attach sequence is ----
            bool attaching = im.AttachClock >= 0f;
            float t = attaching ? im.AttachClock : float.MaxValue;
            float approach = attaching ? Ease(Span(t, 0f, im.Land)) : 1f;
            float sink = attaching ? Span(t, im.Land, im.Land + ImprintStyle.SinkDuration) : 1f;
            im.Sink = sink;
            float dimple = 0f;
            if (attaching && ImprintLayers.ShowMembraneDimple)
            {
                float d0 = im.Land - ImprintStyle.DimpleLead;
                float dk = Span(t, d0, d0 + ImprintStyle.DimpleDuration);
                dimple = dk > 0f && dk < 1f
                    ? Mathf.Sin(dk * Mathf.PI) * ImprintStyle.DimpleStrength
                    : 0f;
            }

            // ---- its size ----
            float finalSize = im.Size * cube;
            float scale = 1f;
            if (attaching)
            {
                float landScale = 1f / Mathf.Max(ImprintStyle.SinkFinal, 0.01f);
                if (t < im.Land)
                {
                    float from = im.HasSource && im.SourceSize > 0f
                        ? im.SourceSize / Mathf.Max(finalSize, 1e-5f)
                        : landScale * 0.35f;
                    // It sets off at its own size and arrives a little smaller, never swelling.
                    float to = im.HasSource
                        ? Mathf.Min(landScale, from * ImprintStyle.ApproachEndScale)
                        : landScale;
                    scale = Mathf.Lerp(from, Mathf.Max(to, landScale * 0.5f), approach);
                }
                else
                {
                    float k = sink < 0.5f
                        ? Mathf.Lerp(1f, ImprintStyle.SinkMid, Ease(sink / 0.5f))
                        : Mathf.Lerp(ImprintStyle.SinkMid, ImprintStyle.SinkFinal,
                            Ease((sink - 0.5f) / 0.5f));
                    scale = landScale * k;
                }
                float seal = Span(t, im.SealStart, im.SealStart + ImprintStyle.SealDuration);
                if (seal > 0f && seal < 1f)
                {
                    scale *= 1f - ImprintStyle.SealPulse * Mathf.Sin(seal * Mathf.PI);
                }
            }
            float wave = im.WaveClock >= 0f
                ? Mathf.Clamp01(im.WaveClock / Mathf.Max(ImprintStyle.WaveDuration, 0.01f))
                : -1f;
            if (wave >= 0f)
            {
                scale *= 1f - 0.015f * Mathf.Sin(wave * Mathf.PI);
            }

            // ---- where it is ----
            // IN THE FILM: it goes most of the way with the cube's push, never all of it.
            Vector2 rest = at + im.Spot * cube + load * ImprintStyle.IdleIconFollow;
            if (clamp > 0f && h.ClampFrom.sqrMagnitude > 0f)
            {
                // A refused power: a micro tug toward the force, with the film.
                rest += h.ClampFrom * (Px(ImprintStyle.ResistTugPixels) * clamp);
            }
            if (attaching)
            {
                // Each fiber that takes hold pulls it a pixel its way, and it settles back.
                for (int i = 0; i < im.Fibers.Count; i++)
                {
                    Fiber f = im.Fibers[i];
                    float hold = f.GrowStart + f.GrowTime;
                    float k = Span(t, hold, hold + ImprintStyle.FiberTugDuration);
                    if (k > 0f && k < 1f && f.Root.sqrMagnitude > 1e-8f)
                    {
                        rest += f.Root.normalized
                            * (Px(ImprintStyle.FiberTugPixels) * Mathf.Sin(k * Mathf.PI));
                    }
                }
            }
            Vector2 centre = rest;
            if (attaching && t < im.Land)
            {
                Vector2 from = im.HasSource ? im.Source : rest;
                Vector2 span = rest - from;
                // ONE SHALLOW ARC, bowed upward-ish, straight at the host - never a detour.
                Vector2 normal = new Vector2(-span.y, span.x);
                if (normal.y < 0f)
                {
                    normal = -normal;
                }
                Vector2 control = (from + rest) * 0.5f + normal * ImprintStyle.AttachArc;
                centre = Bezier(from, control, rest, approach);
            }

            // ---- its visibility: with the film when it seats, from its source when attaching ----
            float show = attaching
                ? (im.HasSource ? 1f : Ease(Span(t, 0f, im.Land * 0.6f)))
                : Ease(Span(h.SeatClock, Style.SeatCoreWake * 0.7f,
                    Style.SeatCoreWake * 0.7f + Style.SeatMembraneSpread));

            // ---- how far it has been taken ----
            float treat = 1f;
            if (attaching)
            {
                float assim = Ease(Span(t, im.SealStart,
                    im.SealStart + ImprintStyle.AssimilateDuration));
                treat = t < im.Land
                    ? 0.25f * approach
                    : 0.25f + 0.25f * Ease(sink) + 0.5f * assim;
            }
            float sheen = attaching
                ? Span(t, im.SealStart, im.SealStart + ImprintStyle.SheenDuration)
                : -1f;

            im.Centre = centre;
            im.VisibleSize = finalSize * scale;
            im.Show = show;
            im.Treat = treat;
            im.SheenAt = sheen > 0f && sheen < 1f ? sheen : -1f;
            im.Dimple = dimple;
            im.DimpleAt = rest;
            // The film's own phase: slow, and moved along by whatever the cube is doing to it.
            im.Phase = h.SeatClock * 0.55f + push * 1.4f + clamp * 0.9f + grip * 0.4f;

            float death = rupture >= 0f
                ? Ease(Span(rupture, Style.RuptureFinalGrip, Style.RuptureTotal - 0.1f))
                : 0f;
            frame.Active = true;
            frame.Centre = centre;
            frame.Size = im.VisibleSize;
            // The bed forms as the icon sinks, and goes with it at the end.
            frame.Zone = ImprintStyle.PigmentZoneStrength * Ease(sink) * show * (1f - death);
            frame.ZoneRadius = im.VisibleSize * 0.5f + Px(ImprintStyle.PigmentZonePixels);
            frame.Dimple = dimple;
            frame.DimpleAt = rest;
            frame.DimpleRadius = finalSize * 0.6f;
            return frame;
        }

        /// <summary>The icon, its fibers, the approach trail and the lab's overlays.</summary>
        private void PaintImprint(HostPiece h, ImprintFrame frame, Vector2 centre, float cube,
            float push, float grip, float clamp, float rupture)
        {
            Imprint im = h.Icon;
            if (im == null)
            {
                return;
            }
            if (im.Sticker)
            {
                PaintSticker(h, im, cube);
                return;
            }
            if (im.Icon == null)
            {
                return;
            }
            float t = im.AttachClock >= 0f ? im.AttachClock : float.MaxValue;
            bool attaching = im.AttachClock >= 0f;

            // ---- the icon ----
            Vector2 pushDir = (h.BulgeAt - new Vector2(0.5f, 0.5f)) * 2f;
            pushDir = pushDir.sqrMagnitude > 1e-5f ? pushDir.normalized : Vector2.right;
            float tear = rupture >= 0f
                ? Ease(Span(rupture, Style.RuptureFinalGrip,
                    Style.RuptureFinalGrip + Style.RuptureRibBreak * 3f))
                : 0f;
            float death = rupture >= 0f
                ? Ease(Span(rupture, Style.RuptureFinalGrip, Style.RuptureTotal - 0.1f))
                : 0f;
            // The last grip before the line lands deforms it WITH the film, along the tear.
            float strain = rupture >= 0f
                ? Ease(Span(rupture, 0f, Style.RuptureFinalGrip)) * (1f - tear)
                : 0f;
            Vector2 stretchDir = strain > 0f ? new Vector2(-h.TearDir.y, h.TearDir.x) : pushDir;
            float along = ImprintStyle.IdleStretchAlong * push + 0.05f * strain;
            float across = -ImprintStyle.IdleStretchAcross * push - 0.02f * strain;
            PlaceIcon(im.Icon, im.Profile, im.Centre, im.VisibleSize);
            im.Icon.color = new Color(1f, 1f, 1f,
                ImprintLayers.ShowEmbeddedIcon ? Mathf.Clamp01(im.Show) : 0f);
            if (EmbeddedIconMaterial() == null)
            {
                PaintIconFallback(h, im, death);
            }
            else
            {
                PaintIconBlock(h, im, stretchDir, along, across, tear, death);
            }

            // ---- the fibers ----
            for (int i = 0; i < im.Fibers.Count; i++)
            {
                PaintFiber(h, im, im.Fibers[i], i, centre, cube, push, pushDir, grip, clamp,
                    rupture, t, attaching);
            }

            // ---- the approach trail: two to four tiny motes of its own colour ----
            Color trail = im.Profile != null ? im.Profile.Average : PassengerColour(h.Passenger);
            float lum = 0.299f * trail.r + 0.587f * trail.g + 0.114f * trail.b;
            trail = Color.Lerp(trail, new Color(lum, lum, lum), 0.3f);
            for (int j = 0; j < im.Motes.Count; j++)
            {
                SpriteRenderer m = im.Motes[j];
                if (m == null)
                {
                    continue;
                }
                if (!attaching || !im.HasSource || t > im.Land + 0.1f)
                {
                    m.color = Clear;
                    continue;
                }
                float ap = Ease(Span(t, 0f, im.Land));
                float lag = Mathf.Clamp01(ap - 0.09f * (j + 1));
                Vector2 rest = im.DimpleAt;
                Vector2 from = im.Source;
                Vector2 span = rest - from;
                Vector2 normal = new Vector2(-span.y, span.x);
                if (normal.y < 0f)
                {
                    normal = -normal;
                }
                Vector2 control = (from + rest) * 0.5f + normal * ImprintStyle.AttachArc;
                Vector2 p = Bezier(from, control, rest, lag);
                m.transform.localPosition = new Vector3(p.x, p.y, 0f);
                float s = cube * 0.035f * (1f - j * 0.22f);
                Fit(m, s, s);
                float fade = 1f - Span(t, im.Land, im.Land + 0.1f);
                Color c = trail;
                c.a = 0.7f * (1f - j * 0.2f) * fade * (lag > 0f ? 1f : 0f);
                m.color = c;
            }

            PaintImprintDebug(h, im, centre, cube);
        }

        /// <summary>Puts the icon renderer so that its VISIBLE silhouette is <paramref name="size"/>
        /// across and centred on <paramref name="centre"/> - its padding is not the icon.</summary>
        private static void PlaceIcon(SpriteRenderer r, ParasiteIconProfile profile, Vector2 centre,
            float size)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            Bounds b = r.sprite.bounds;
            Rect vis = profile != null ? profile.Visible : new Rect(-1f, -1f, 2f, 2f);
            float visW = vis.width * 0.5f * b.size.x;
            float visH = vis.height * 0.5f * b.size.y;
            float k = size / Mathf.Max(Mathf.Max(visW, visH), 1e-5f);
            Vector2 visCentre = new Vector2(b.center.x + vis.center.x * b.extents.x,
                b.center.y + vis.center.y * b.extents.y);
            Vector2 pos = centre - visCentre * k;
            r.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(k, k, 1f);
        }

        /// <summary>Where a point of the icon's n-space is, in this view's space, once PlaceIcon
        /// has put it down.</summary>
        private static Vector2 IconPoint(SpriteRenderer r, Vector2 n)
        {
            Bounds b = r.sprite.bounds;
            Vector3 local = new Vector3(b.center.x + n.x * b.extents.x,
                b.center.y + n.y * b.extents.y, 0f);
            Vector3 s = r.transform.localScale;
            Vector3 p = r.transform.localPosition;
            return new Vector2(p.x + local.x * s.x, p.y + local.y * s.y);
        }

        private void PaintIconBlock(HostPiece h, Imprint im, Vector2 stretchDir, float along,
            float across, float tear, float death)
        {
            ParasiteIconProfile p = im.Profile;
            float treat = im.Treat;
            float sink = im.Sink;
            block.Clear();
            if (p != null)
            {
                block.SetVector(UVRectId, p.UVRect);
                Rect v = p.Visible;
                block.SetVector(VisibleId, new Vector4(v.xMin, v.yMin, v.xMax, v.yMax));
            }
            else
            {
                block.SetVector(UVRectId, new Vector4(0f, 0f, 1f, 1f));
                block.SetVector(VisibleId, new Vector4(-1f, -1f, 1f, 1f));
            }
            block.SetColor(MembraneColourId, h.Contrast.Skin);
            block.SetColor(MembraneDeepId, h.Contrast.Fold);
            block.SetFloat(TreatmentId, treat);
            bool retain = ImprintLayers.ShowIconColorRetention;
            block.SetFloat(RetentionId, retain ? ImprintStyle.ColorRetention : 1f);
            block.SetFloat(IconDesatId, retain ? ImprintStyle.Desaturation : 0f);
            block.SetFloat(MembraneTintId, retain ? ImprintStyle.MembraneTint : 0f);
            block.SetFloat(BrightnessId, IconBrightness(h, p));
            block.SetFloat(IconOpacityId, ImprintStyle.IconOpacity);
            block.SetFloat(VeilId, ImprintLayers.ShowMembraneFrontMask
                ? ImprintStyle.Veil * Ease(sink)
                : 0f);
            block.SetVector(OcclusionId, new Vector4(im.Variant,
                ImprintLayers.ShowIconOcclusion ? Ease(sink) : 0f,
                ImprintStyle.EdgeConsume, im.Seed));
            // Two pixels of depth, as a share of the icon's own half size.
            float half = Mathf.Max(im.VisibleSize * 0.5f, 1e-5f);
            block.SetFloat(DepthId, Mathf.Clamp01(Px(ImprintStyle.DepthPixels) / half * 8f)
                * Ease(sink));
            float amp = ImprintLayers.ShowIconUVDistortion
                ? Px(ImprintStyle.UvDistortionPixels) / half * Ease(sink)
                : 0f;
            block.SetVector(DistortId, new Vector4(amp,
                ImprintLayers.ShowIconUVDistortion ? ImprintStyle.RadialSqueeze * Ease(sink) : 0f,
                im.Phase, 0f));
            block.SetVector(StretchId, new Vector4(stretchDir.x, stretchDir.y, along, across));
            float wave = im.WaveClock >= 0f
                ? Mathf.Clamp01(im.WaveClock / Mathf.Max(ImprintStyle.WaveDuration, 0.01f))
                : -1f;
            block.SetVector(WaveId, new Vector4(im.WaveDir.x, im.WaveDir.y, wave,
                wave >= 0f ? ImprintStyle.WaveStrength : 0f));
            // THE DARK-HOST RIM: only as strong as the block's darkness asks for, and off on a
            // light one.
            float dark = h.Contrast.Darkness;
            float rim = dark > 0.04f
                ? Mathf.Lerp(ImprintStyle.RimMin, ImprintStyle.RimMax, dark) * Ease(sink)
                : 0f;
            block.SetFloat(IconRimId, rim);
            Color own = p != null ? p.Average : PassengerColour(h.Passenger);
            float lum = 0.299f * own.r + 0.587f * own.g + 0.114f * own.b;
            Color rimColour = Color.Lerp(new Color(lum, lum, lum), own, 0.35f);
            rimColour = Color.Lerp(rimColour, new Color(0.86f, 0.82f, 0.86f), 0.45f);
            block.SetColor(IconRimColourId, rimColour);
            block.SetVector(IconSheenId, new Vector4(im.SheenAt, ImprintStyle.SheenAmount,
                0.6f + im.Seed * 0.3f, 0f));
            block.SetVector(IconTearId, new Vector4(tear, im.Seed, 2f + im.Variant % 3,
                0.14f));
            block.SetFloat(DeathId, death);
            im.Icon.SetPropertyBlock(block);
        }

        /// <summary>
        /// The icon's brightness inside the film, CONTRAST-AWARE: when its own luminance is close
        /// to the membrane's (a dark purple icon in a dark purple film), it is moved away from it -
        /// up if it is the lighter of the two - by at most ContrastBoost. Small on purpose: this is
        /// separation, not an accessibility layer, and it never adds a halo.
        /// </summary>
        private static float IconBrightness(HostPiece h, ParasiteIconProfile p)
        {
            float b = ImprintStyle.Brightness;
            if (!ImprintLayers.ShowIconContrastCompensation || p == null)
            {
                return b;
            }
            Color skin = h.Contrast.Skin;
            float film = 0.299f * skin.r + 0.587f * skin.g + 0.114f * skin.b;
            float icon = p.Luminance * b;
            float gap = Mathf.Abs(icon - film);
            if (gap >= ImprintStyle.ContrastThreshold)
            {
                return b;
            }
            float need = 1f - gap / Mathf.Max(ImprintStyle.ContrastThreshold, 1e-4f);
            float dir = icon >= film ? 1f : -1f;
            return b * (1f + dir * ImprintStyle.ContrastBoost * need);
        }

        /// <summary>No shader: the icon on the plain material, pulled toward the film's colour and
        /// dimmed by tint alone.</summary>
        private static void PaintIconFallback(HostPiece h, Imprint im, float death)
        {
            Color skin = h.Contrast.Skin;
            Color c = Color.Lerp(Color.white, new Color(skin.r * 2.2f, skin.g * 2.2f, skin.b * 2.2f),
                0.3f * im.Treat);
            c *= Mathf.Lerp(1f, ImprintStyle.Brightness, im.Treat);
            c = Color.Lerp(c, new Color(skin.r, skin.g, skin.b), death);
            c.a = im.Icon.color.a * Mathf.Lerp(1f, ImprintStyle.IconOpacity, im.Treat)
                * (1f - death);
            im.Icon.color = c;
        }

        /// <summary>
        /// One fiber: a cubic curve from its root in the film to the point of the icon's outline it
        /// grips, drawn as a chain of pooled capsule segments - thin, tapering toward the icon,
        /// unevenly thick along its length, and slightly bowed. It grows root-first with an ease-out
        /// when the icon is grabbed; it tightens when the cube pushes AWAY from it and slackens when
        /// the push comes toward it; and at the rupture it snaps and its end whips back into the
        /// film.
        /// </summary>
        private void PaintFiber(HostPiece h, Imprint im, Fiber f, int index, Vector2 centre,
            float cube, float push, Vector2 pushDir, float grip, float clamp, float rupture,
            float t, bool attaching)
        {
            bool shown = f.Back ? ImprintLayers.ShowFiberBack : ImprintLayers.ShowFiberFront;
            float grow = attaching
                ? EaseOut(Span(t, f.GrowStart, f.GrowStart + f.GrowTime))
                : im.Show;
            float snap = rupture >= 0f ? Ease(Span(rupture, f.SnapAt, f.SnapAt + 0.12f)) : 0f;
            float reach = grow * (1f - snap * 0.85f);
            float alpha = ImprintStyle.FiberOpacity * Mathf.Clamp01(im.Show * 1.2f)
                * (1f - Ease(Span(snap, 0.5f, 1f)));
            if (!shown || reach <= 0.001f || alpha <= 0.001f)
            {
                HideSegments(f.Segments);
                HideSegments(f.Branch);
                return;
            }
            // The root is IN THE FILM: it rides the cube's whole load while the icon only goes most
            // of the way - which is exactly what puts a fiber under tension when the cube pushes.
            Vector2 root = centre + (im.Spot + f.Root) * cube;
            Vector2 tipEdge = IconPoint(im.Icon, f.Grip);
            Vector2 toMiddle = im.Centre - tipEdge;
            Vector2 tip = tipEdge + (toMiddle.sqrMagnitude > 1e-10f
                ? toMiddle.normalized * (f.Inset * im.VisibleSize * 0.5f)
                : Vector2.zero);
            Vector2 d = tip - root;
            float len = d.magnitude;
            if (len < 1e-5f)
            {
                HideSegments(f.Segments);
                HideSegments(f.Branch);
                return;
            }
            Vector2 dir = d / len;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            // TENSION: the push pulls the icon AWAY from a fiber on the far side, straightening
            // it; a fiber on the near side slackens and bows.
            Vector2 fromIcon = f.Root.sqrMagnitude > 1e-8f ? f.Root.normalized : Vector2.up;
            float facing = Vector2.Dot(fromIcon, pushDir);
            float wave = im.WaveClock >= 0f
                ? Mathf.Sin(Mathf.Clamp01(im.WaveClock / Mathf.Max(ImprintStyle.WaveDuration,
                    0.01f)) * Mathf.PI) * 0.35f
                : 0f;
            float tension = Mathf.Clamp01(Mathf.Max(0f, -facing) * push * ImprintStyle.FiberTension
                + grip * 0.5f + clamp * 0.4f + wave);
            float slack = Mathf.Clamp01(Mathf.Max(0f, facing) * push);
            float bow = f.Bow * len * (1f - tension * 0.75f) * (1f + slack * 0.8f);
            Vector2 c1 = root + d * 0.33f + normal * bow;
            Vector2 c2 = root + d * 0.7f - normal * (bow * 0.35f);

            int n = f.Segments.Count;
            float segLen = len / Mathf.Max(1, n - 1) * 2.4f;
            ParasiteContrast palette = h.Contrast;
            for (int k = 0; k < n; k++)
            {
                SpriteRenderer r = f.Segments[k];
                if (r == null)
                {
                    continue;
                }
                float u = n == 1 ? 0f : k / (float)(n - 1);
                if (u > reach)
                {
                    r.color = Clear;
                    continue;
                }
                Vector2 p = Cubic(root, c1, c2, tip, u);
                Vector2 tangent = CubicTangent(root, c1, c2, tip, u);
                // Thicker at the root, fine at the grip, and UNEVEN along the way.
                float w = Mathf.Lerp(f.Width * 1.25f, f.Width * 0.5f, u)
                    * (1f + 0.28f * Mathf.Sin(u * 7.3f + f.Lumps)) * (1f - tension * 0.2f)
                    * cube;
                r.transform.localPosition = new Vector3(p.x, p.y, 0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f);
                Fit(r, w, Mathf.Min(segLen, len * 0.6f));
                // Dark plum and muted violet, a crimson edge kept low: the harness's own palette,
                // at a low tone.
                PaintHarness(r, 0.3f, 0.5f, 0f, tension, -1f, -1f, alpha, palette);
            }

            // ONE SHORT SIDE SHOOT on the fibers that have one, off the film end of it.
            int b = f.Branch.Count;
            float branchGrow = Mathf.Clamp01((reach - 0.45f) / 0.4f);
            Vector2 bFrom = Cubic(root, c1, c2, tip, 0.38f);
            Vector2 bTan = CubicTangent(root, c1, c2, tip, 0.38f);
            float side = (index % 2 == 0) ? 1f : -1f;
            Vector2 bDir = (Rotate(bTan, 0.6f * side)).normalized;
            float bLen = len * 0.32f;
            for (int k = 0; k < b; k++)
            {
                SpriteRenderer r = f.Branch[k];
                if (r == null)
                {
                    continue;
                }
                float u = b == 1 ? 1f : (k + 1) / (float)b;
                if (u > branchGrow)
                {
                    r.color = Clear;
                    continue;
                }
                Vector2 curl = new Vector2(-bDir.y, bDir.x) * (side * bLen * 0.25f * u * u);
                Vector2 p = bFrom + bDir * (bLen * u) + curl;
                r.transform.localPosition = new Vector3(p.x, p.y, 0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(bDir.y, bDir.x) * Mathf.Rad2Deg - 90f);
                float w = f.Width * 0.6f * (1f - u * 0.5f) * cube;
                Fit(r, w, bLen / b * 2.2f);
                PaintHarness(r, 0.3f, 0.45f, 0f, tension, -1f, -1f, alpha * 0.85f, palette);
            }
        }

        private static void HideSegments(List<SpriteRenderer> segments)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].color = Clear;
                }
            }
        }

        /// <summary>
        /// THE OLD LOOK, for the lab's comparison only: the raw icon at full colour and full
        /// opacity, stuck on the cube's corner with nothing holding it. It is here so the
        /// side-by-side test can ask "on which one is the joker really inside the parasite?" and
        /// have the answer be obvious.
        /// </summary>
        private void PaintSticker(HostPiece h, Imprint im, float cube)
        {
            if (im.Icon == null || toWorld == null)
            {
                return;
            }
            Vector2 at = toWorld(h.Cell) + new Vector2(0.27f, 0.27f) * cube;
            PlaceIcon(im.Icon, im.Profile, at, cube * 0.36f);
            float show = Ease(Span(h.SeatClock, Style.SeatCoreWake * 0.7f,
                Style.SeatCoreWake * 0.7f + Style.SeatMembraneSpread));
            im.Icon.color = new Color(1f, 1f, 1f, show);
        }

        /// <summary>The lab's overlays: the icon's measured bounds, the points the fibers grip and
        /// root at, and the raw icon beside the host for comparison.</summary>
        private void PaintImprintDebug(HostPiece h, Imprint im, Vector2 centre, float cube)
        {
            int want = 0;
            if (ImprintLayers.ShowIconBounds)
            {
                want += 4;
            }
            if (ImprintLayers.ShowAttachPoints)
            {
                want += im.Fibers.Count * 2;
            }
            while (im.Debug.Count < want)
            {
                im.Debug.Add(Rent(ViewUtil.WhiteSprite, DebugOrder, null));
            }
            int used = 0;
            if (ImprintLayers.ShowIconBounds && im.Icon != null && im.Profile != null)
            {
                Rect v = im.Profile.Visible;
                Vector2 a = IconPoint(im.Icon, new Vector2(v.xMin, v.yMin));
                Vector2 b = IconPoint(im.Icon, new Vector2(v.xMax, v.yMax));
                float line = Px(1f);
                DebugBar(im.Debug[used++], new Vector2((a.x + b.x) * 0.5f, a.y), b.x - a.x, line);
                DebugBar(im.Debug[used++], new Vector2((a.x + b.x) * 0.5f, b.y), b.x - a.x, line);
                DebugBar(im.Debug[used++], new Vector2(a.x, (a.y + b.y) * 0.5f), line, b.y - a.y);
                DebugBar(im.Debug[used++], new Vector2(b.x, (a.y + b.y) * 0.5f), line, b.y - a.y);
                for (int i = used - 4; i < used; i++)
                {
                    im.Debug[i].color = new Color(0.3f, 1f, 0.9f, 0.85f);
                }
            }
            if (ImprintLayers.ShowAttachPoints && im.Icon != null)
            {
                float dot = Px(4f);
                for (int i = 0; i < im.Fibers.Count; i++)
                {
                    Fiber f = im.Fibers[i];
                    DebugBar(im.Debug[used], IconPoint(im.Icon, f.Grip), dot, dot);
                    im.Debug[used++].color = f.Back
                        ? new Color(1f, 0.5f, 0.2f, 0.95f)
                        : new Color(0.3f, 1f, 0.4f, 0.95f);
                    DebugBar(im.Debug[used], centre + (im.Spot + f.Root) * cube, dot, dot);
                    im.Debug[used++].color = new Color(1f, 0.95f, 0.3f, 0.95f);
                }
            }
            for (int i = used; i < im.Debug.Count; i++)
            {
                im.Debug[i].color = Clear;
            }

            if (ImprintLayers.ShowOriginalIcon)
            {
                if (im.Original == null)
                {
                    im.Original = Rent(im.Sprite, DebugOrder, null);
                }
                Vector2 beside = toWorld(h.Cell) + new Vector2(cellSize, 0f);
                PlaceIcon(im.Original, im.Profile, beside, im.Size * cube);
                im.Original.color = Color.white;
            }
            else if (im.Original != null)
            {
                Return(im.Original);
                im.Original = null;
            }
        }

        private static void DebugBar(SpriteRenderer r, Vector2 at, float w, float hgt)
        {
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            Fit(r, Mathf.Abs(w), Mathf.Abs(hgt));
        }

        // =================================================================== outside the board

        private static MaterialPropertyBlock stillBlock;

        /// <summary>
        /// The imprint as a STILL, for a host drawn somewhere with no membrane mesh and no time -
        /// the hand's cards, the deck list, the sell screen (CardVisual.SetRider). Same shader, same
        /// fit by the icon's real silhouette, its final assimilated state; the caller lays the film
        /// under it. No plate, ever: what it sits in is the membrane.
        /// </summary>
        public static void PaintEmbeddedStill(SpriteRenderer r, Sprite icon, Vector2 cubeCentre,
            float cube, int seed, ParasiteContrast palette)
        {
            if (r == null || icon == null)
            {
                return;
            }
            ParasiteIconProfile p = ParasiteIconProfile.For(icon);
            r.sprite = icon;
            var cell = new GridPos(seed, seed * 7 + 3);
            Vector2 spot = ImprintSpot(cell);
            float complexity = p != null ? p.Complexity : 0.5f;
            // A little bigger than on the board: a cube in the hand is a few dozen pixels, and the
            // icon has to survive that.
            float size = (ImprintStyle.IconScale + (complexity - 0.5f) * 2f
                * ImprintStyle.IconScaleRange) * 1.35f * cube;
            PlaceIcon(r, p, cubeCentre + spot * cube, size);
            Material mat = EmbeddedIconMaterial();
            if (mat == null)
            {
                Color skin = palette.Skin;
                Color c = Color.Lerp(Color.white,
                    new Color(skin.r * 2.2f, skin.g * 2.2f, skin.b * 2.2f), 0.3f)
                    * ImprintStyle.Brightness;
                c.a = ImprintStyle.IconOpacity;
                r.color = c;
                return;
            }
            r.sharedMaterial = mat;
            if (stillBlock == null)
            {
                stillBlock = new MaterialPropertyBlock();
            }
            MaterialPropertyBlock b = stillBlock;
            b.Clear();
            if (p != null)
            {
                b.SetVector(UVRectId, p.UVRect);
                Rect v = p.Visible;
                b.SetVector(VisibleId, new Vector4(v.xMin, v.yMin, v.xMax, v.yMax));
            }
            b.SetColor(MembraneColourId, palette.Skin);
            b.SetColor(MembraneDeepId, palette.Fold);
            b.SetFloat(TreatmentId, 1f);
            b.SetFloat(RetentionId, ImprintStyle.ColorRetention);
            b.SetFloat(IconDesatId, ImprintStyle.Desaturation);
            b.SetFloat(MembraneTintId, ImprintStyle.MembraneTint);
            b.SetFloat(BrightnessId, ImprintStyle.Brightness);
            b.SetFloat(IconOpacityId, Mathf.Min(1f, ImprintStyle.IconOpacity + 0.08f));
            b.SetFloat(VeilId, ImprintStyle.Veil);
            b.SetVector(OcclusionId, new Vector4((int)(Random01(cell, 500) * 4f) % 4, 1f,
                ImprintStyle.EdgeConsume, Random01(cell, 501) * 10f));
            b.SetFloat(DepthId, 0.6f);
            b.SetVector(DistortId, Vector4.zero);
            b.SetVector(StretchId, new Vector4(1f, 0f, 0f, 0f));
            b.SetVector(WaveId, new Vector4(1f, 0f, -1f, 0f));
            b.SetFloat(IconRimId, palette.Darkness > 0.04f
                ? Mathf.Lerp(ImprintStyle.RimMin, ImprintStyle.RimMax, palette.Darkness)
                : 0f);
            b.SetColor(IconRimColourId, new Color(0.8f, 0.76f, 0.8f));
            b.SetVector(IconSheenId, new Vector4(-1f, 0f, 0f, 0f));
            b.SetVector(IconTearId, Vector4.zero);
            b.SetFloat(DeathId, 0f);
            r.SetPropertyBlock(b);
        }

        // =================================================================== curves

        private static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        private static Vector2 Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        private static Vector2 CubicTangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            Vector2 v = 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
            return v.sqrMagnitude > 1e-10f ? v.normalized : (d - a).normalized;
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
