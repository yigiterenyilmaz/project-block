// PURPOSE: "Tamagotchi"'s requests and everything around the pet that is not the pet: the two
// REQUEST PLATES, the PATIENCE RING behind them, the AURA and the local vignette, the FEED ZONE -
// and the DRAG TRACKING, the layer of acting that answers a card being carried toward it.
//
// THE PLATES ARE THE REQUEST, NOT A COUNTER. Two small trays beside the pet, cream inside a deep
// berry lip, each holding the requested card's OWN face (CardVisual, the real card's shape and
// elements - never a generic icon), readable at a glance. There is no "0/2" and no check mark: a fed
// plate collapses in on its card, takes a bite out of its corner and goes muted; a plate the fury
// bit cracks and drops away. Hover one for the card's tooltip (the controller asks PlateCardAt).
//
// THE PATIENCE RING is not a bar. It is a broken, scalloped halo of pet-toy beads behind the pair,
// at low opacity, about the character's width; as Core's deadline progress runs down beads darken
// and fall away. The character's acting stays the main information - the ring only confirms it.
//
// THE DRAG is acted by DISTANCE to the feed zone (the brief's four bands): far - the eyes follow;
// medium - the head turns and the body leans 2 px; near - the eyes grow, the paws come up, the mouth
// opens a third; in the zone - it leans 8-12 px, the mouth opens wide, two or three controlled
// excited beats, and the plate the card answers glows warm. A card it does NOT want gets the other
// face: mouth shut, head aside, eyes card -> plate -> card. The zone itself is a soft radial pink
// glow, only while a card is being dragged, never a rectangle.
// EXTENSION POINT: a third request would be one more plate in the same row.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        private sealed class PlateView
        {
            public int SlotId;
            public int CardId;
            public BlockCard Card;
            public CardValueTier Tier;
            public bool Fed;
            public bool Destroyed;
            public Transform Root;
            public SpriteRenderer Plate;
            public SpriteRenderer Glow;
            public SpriteRenderer Crack;
            public CardVisual Mini;
            public float PopAt = -1f;       // when it pops in (clock), -1 = hidden
            public float CollapseAt = -1f;  // when the fed collapse started
            public float DropAt = -1f;      // when the fury drop started
            public float FoldAt = -1f;      // when the fold-away started
            public float ShakeUntil = -1f;
            public float Highlight;
            public float HighlightTarget;
        }

        private readonly List<PlateView> plates = new List<PlateView>();
        private const int RingBeads = 14;
        private readonly SpriteRenderer[] ring = new SpriteRenderer[RingBeads];
        private readonly float[] ringLife = new float[RingBeads];
        private SpriteRenderer aura;
        private SpriteRenderer auraShadow;
        private SpriteRenderer vignette;
        private SpriteRenderer feedZone;
        private float vignetteBoost;
        private float auraBoost;
        private Color auraBoostColour = TamagotchiArt.WarmPink;
        private bool platesFolded;

        /// <summary>Slots whose meal is queued or playing: Core marks them fed at once, but the
        /// plate only collapses when the meal reaches it.</summary>
        private readonly HashSet<int> mealsOnTheWay = new HashSet<int>();
        private string platesKey = "";

        /// <summary>The plate sprite's width in its own units (220 px at 400 ppu).</summary>
        private const float PlateArtWidth = 0.55f;
        private const float PlateArtHeight = 0.65f;

        private void BuildDecor()
        {
            vignette = DecorSprite("Vignette", "fx_soft", -3);
            auraShadow = DecorSprite("AuraShadow", "fx_soft", -2);
            aura = DecorSprite("Aura", "fx_soft", -1);
            feedZone = DecorSprite("FeedZone", "fx_soft", -1);
            for (int i = 0; i < RingBeads; i++)
            {
                ring[i] = DecorSprite("Bead" + i, "ring_bead", 0);
            }
            HideDecor();
        }

        private SpriteRenderer DecorSprite(string name, string sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(decor, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = TamagotchiArt.Get(sprite);
            r.sortingOrder = order;
            r.color = new Color(1f, 1f, 1f, 0f);
            return r;
        }

        private void HideDecor()
        {
            foreach (PlateView p in plates)
            {
                if (p.Root != null)
                {
                    Destroy(p.Root.gameObject);
                }
            }
            plates.Clear();
            platesKey = "";
            foreach (SpriteRenderer r in ring)
            {
                if (r != null)
                {
                    r.color = new Color(1f, 1f, 1f, 0f);
                }
            }
            if (aura != null)
            {
                aura.color = Clear(aura.color);
                auraShadow.color = Clear(auraShadow.color);
                vignette.color = Clear(vignette.color);
                feedZone.color = Clear(feedZone.color);
            }
        }

        private static Color Clear(Color c)
        {
            c.a = 0f;
            return c;
        }

        // ================================================================== plates

        private static string KeyOf(TamagotchiRoundVisualState s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (PetRequest r in s.Requests)
            {
                sb.Append(r.SlotId).Append(':').Append(r.Card != null ? r.Card.Id : 0).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>Builds one plate per request slot, hidden until RevealPlates.</summary>
        private void RebuildPlates()
        {
            foreach (PlateView p in plates)
            {
                if (p.Root != null)
                {
                    Destroy(p.Root.gameObject);
                }
            }
            plates.Clear();
            platesFolded = false;
            platesKey = KeyOf(State);
            for (int i = 0; i < State.Requests.Count; i++)
            {
                PetRequest r = State.Requests[i];
                var p = new PlateView { SlotId = r.SlotId, Card = r.Card, CardId = r.Card != null ? r.Card.Id : 0, Tier = r.Tier, Fed = r.Fed };
                p.Root = new GameObject("Plate" + i).transform;
                p.Root.SetParent(decor, false);
                p.Glow = SpriteOn(p.Root, "Glow", "fx_soft", 2);
                // wider than the plate it sits behind (the plate is 0.55 x 0.65, the glow 0.32)
                p.Glow.transform.localScale = new Vector3(2.7f, 3.1f, 1f);
                p.Plate = SpriteOn(p.Root, "Plate", r.Fed ? "plate_bitten" : "plate", 3);
                if (r.Card != null && !r.Fed)
                {
                    BuildMini(p);
                }
                p.Crack = SpriteOn(p.Root, "Crack", "crack", 9);
                p.Crack.color = new Color(0.36f, 0.08f, 0.2f, 0f);
                p.Root.gameObject.SetActive(false);
                plates.Add(p);
            }
        }

        private void BuildMini(PlateView p)
        {
            // the requested card's own face, fitted into the plate's well
            p.Mini = CardVisual.Create(p.Root, "Mini", p.Card, true, false, new Vector2(0f, -0.005f), 4);
            float fit = Mathf.Min(0.43f / CardVisual.BodyWidth, 0.53f / CardVisual.BodyHeight);
            p.Mini.transform.localScale = new Vector3(fit, fit, 1f);
        }

        private SpriteRenderer SpriteOn(Transform parent, string name, string sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = TamagotchiArt.Get(sprite);
            r.sortingOrder = order;
            return r;
        }

        private void HidePlatesNow()
        {
            foreach (PlateView p in plates)
            {
                p.PopAt = -1f;
                p.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>The plates pop in, the second one 70 ms after the first.</summary>
        private void RevealPlates()
        {
            for (int i = 0; i < plates.Count; i++)
            {
                plates[i].PopAt = clock + 0.07f * i;
            }
        }

        private void RevealPlatesNow()
        {
            for (int i = 0; i < plates.Count; i++)
            {
                plates[i].PopAt = clock - 1f;
            }
        }

        /// <summary>Every plate folds away (2/2 fed, or the round ending).</summary>
        private void FoldPlates()
        {
            platesFolded = true;
            for (int i = 0; i < plates.Count; i++)
            {
                if (plates[i].FoldAt < 0f && plates[i].DropAt < 0f)
                {
                    plates[i].FoldAt = clock + 0.06f * i;
                }
            }
        }

        /// <summary>The world centre of plate <paramref name="i"/> (its rest, not its wobble).</summary>
        public Vector3 PlateWorld(int i)
        {
            int n = Mathf.Max(1, plates.Count);
            float w = PlateArtWidth * PlateWorldScale;
            float h = PlateArtHeight * PlateWorldScale;
            float gap = 0.1f * S;
            Vector2 c = PlatesCentreNow;
            float k = i - (n - 1) * 0.5f;
            return platesVerticalNow
                ? new Vector3(c.x, c.y - k * (h + gap), 0f)
                : new Vector3(c.x + k * (w + gap), c.y, 0f);
        }

        /// <summary>World units per plate-art unit.</summary>
        private float PlateWorldScale
        {
            get { return 1.3f * S * plateScaleNow; }
        }

        // where the plates are SHOWN: the home's, except that a move leaves them where they were
        // until they have folded away, so they never fly across the screen with the pet
        private Vector2 platesCentreNow;
        private bool platesVerticalNow;
        private float plateScaleNow = 1f;
        private bool platesPlaced;

        private Vector2 PlatesCentreNow
        {
            get { return platesPlaced ? platesCentreNow : Home.PlatesCentre; }
        }

        private void FollowHomeWithPlates()
        {
            if (!platesPlaced || !platesAwayWanted || platesAway >= 0.999f)
            {
                platesCentreNow = Home.PlatesCentre;
                platesVerticalNow = Home.PlatesVertical;
                plateScaleNow = Home.PlateScale;
                platesPlaced = true;
            }
        }

        private int PlateIndexOf(int slotId)
        {
            for (int i = 0; i < plates.Count; i++)
            {
                if (plates[i].SlotId == slotId)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The requested card on the plate under a world point (for the tooltip), or null.</summary>
        public BlockCard PlateCardAt(Vector2 world)
        {
            if (!shown)
            {
                return null;
            }
            for (int i = 0; i < plates.Count; i++)
            {
                PlateView p = plates[i];
                if (p.Root == null || !p.Root.gameObject.activeSelf || p.Destroyed)
                {
                    continue;
                }
                Vector3 c = PlateWorld(i);
                if (Mathf.Abs(world.x - c.x) <= PlateArtWidth * PlateWorldScale * 0.5f
                    && Mathf.Abs(world.y - c.y) <= PlateArtHeight * PlateWorldScale * 0.5f)
                {
                    return p.Card;
                }
            }
            return null;
        }

        /// <summary>A fed plate: its card shrinks in, the plate takes a bite and goes muted.</summary>
        private void CollapsePlate(int slotId)
        {
            mealsOnTheWay.Remove(slotId);
            int i = PlateIndexOf(slotId);
            if (i < 0)
            {
                return;
            }
            plates[i].Fed = true;
            plates[i].CollapseAt = clock;
        }

        /// <summary>The fury bit the pending plates: they crack, shake, and drop away.</summary>
        private void BitePendingPlates()
        {
            for (int i = 0; i < plates.Count; i++)
            {
                if (!plates[i].Fed && !plates[i].Destroyed)
                {
                    plates[i].Destroyed = true;
                    plates[i].DropAt = clock + 0.05f * i;
                }
                else if (plates[i].FoldAt < 0f && plates[i].DropAt < 0f)
                {
                    plates[i].FoldAt = clock + 0.12f;
                }
            }
        }

        private void ShakePlates(float seconds)
        {
            foreach (PlateView p in plates)
            {
                p.ShakeUntil = clock + seconds;
            }
        }

        // ================================================================== per frame

        private void TickDecor()
        {
            // a new set of requests (a new round in the lab, a load) rebuilds the plates
            string key = KeyOf(State);
            if (key != platesKey && State.BossActive && !exiting)
            {
                bool wasShown = plates.Count > 0 && plates[0].PopAt >= 0f;
                RebuildPlates();
                if (wasShown)
                {
                    RevealPlatesNow();
                }
            }
            // fed in full with no meal being shown (a loaded game): the plates are put away
            if (State.Satisfied && !platesFolded && mealsOnTheWay.Count == 0 && !queue.Has("feed"))
            {
                FoldPlates();
            }
            TickPlates();
            TickRing();
            TickAura();
            TickFeedZone();
        }

        private void TickPlates()
        {
            float scale = PlateWorldScale;
            Color tint = PlateTint();
            // a move: the plates fold away where they are and unfold beside the pet's new place
            platesAway = Mathf.MoveTowards(platesAway, platesAwayWanted ? 1f : 0f, Dt / (platesAwayWanted ? 0.11f : 0.16f));
            FollowHomeWithPlates();
            float away = Smooth(platesAway);
            for (int i = 0; i < plates.Count; i++)
            {
                PlateView p = plates[i];
                if (p.Root == null)
                {
                    continue;
                }
                // a plate fed without the meal being shown (a load, the lab) just shows as fed
                PetRequest? req = RequestFor(p.SlotId);
                if (req.HasValue && req.Value.Fed && !p.Fed && p.CollapseAt < 0f && !mealsOnTheWay.Contains(p.SlotId))
                {
                    p.Fed = true;
                    p.CollapseAt = clock - 10f;
                }
                // furious with no fury being shown (a loaded game): the pending plates are simply gone
                if (State.Furious && !p.Fed && !p.Destroyed && !queue.Has("fury"))
                {
                    p.Destroyed = true;
                    p.DropAt = clock - 10f;
                }
                if (p.PopAt < 0f || clock < p.PopAt)
                {
                    p.Root.gameObject.SetActive(false);
                    continue;
                }
                p.Root.gameObject.SetActive(true);
                Vector3 rest = PlateWorld(i);
                float pop = Mathf.Clamp01((clock - p.PopAt) / 0.2f);
                float s = Back(pop, 2.4f);
                Vector3 offset = Vector3.zero;
                float rot = 0f;
                float alpha = 1f;
                // bob a hair, out of step with the pet
                offset.y += Mathf.Sin(clock * 1.7f + i * 2.1f) * Px(0.7f);
                if (clock < p.ShakeUntil)
                {
                    offset.x += Mathf.Sin(clock * 75f + i) * Px(2.2f);
                    rot += Mathf.Sin(clock * 61f + i * 1.7f) * 4f;
                }
                // fed: the card shrinks inward and the plate swaps to its bitten self with a pop
                if (p.CollapseAt >= 0f)
                {
                    float c = Mathf.Clamp01((clock - p.CollapseAt) / 0.26f);
                    if (p.Mini != null)
                    {
                        float m = 1f - EaseIn(Mathf.Clamp01(c / 0.6f));
                        float fit = Mathf.Min(0.43f / CardVisual.BodyWidth, 0.53f / CardVisual.BodyHeight);
                        p.Mini.transform.localScale = new Vector3(fit * m, fit * m, 1f);
                        p.Mini.transform.localRotation = Quaternion.Euler(0f, 0f, 25f * (1f - m));
                        if (m <= 0.001f)
                        {
                            Destroy(p.Mini.gameObject);
                            p.Mini = null;
                        }
                    }
                    if (c >= 0.6f && p.Plate.sprite != TamagotchiArt.Get("plate_bitten"))
                    {
                        p.Plate.sprite = TamagotchiArt.Get("plate_bitten");
                    }
                    float bite = Bell(Mathf.Clamp01((c - 0.55f) / 0.45f));
                    s *= 1f - 0.12f * bite;
                }
                // the fury's bite: cracks, then it falls away tumbling
                if (p.DropAt >= 0f)
                {
                    float d = clock - p.DropAt;
                    p.Crack.color = new Color(0.36f, 0.08f, 0.2f, Mathf.Clamp01(d / 0.06f) * 0.9f);
                    if (d > 0.12f)
                    {
                        float f = Mathf.Clamp01((d - 0.12f) / 0.4f);
                        offset.y -= EaseIn(f) * 0.9f * S;
                        offset.x += f * 0.15f * S * (i == 0 ? -1f : 1f);
                        rot += f * (i == 0 ? 35f : -40f);
                        alpha = 1f - Smooth(f);
                        if (f >= 1f)
                        {
                            p.Root.gameObject.SetActive(false);
                        }
                    }
                }
                if (p.FoldAt >= 0f)
                {
                    float f = Mathf.Clamp01((clock - p.FoldAt) / 0.3f);
                    s *= 1f - Smooth(f) * 0.15f;
                    alpha *= 1f - Smooth(f);
                    offset.y += Smooth(f) * 0.12f * S;
                    p.Root.localScale = new Vector3(scale * s, scale * s * (1f - Smooth(f)), 1f);
                }
                else
                {
                    p.Root.localScale = new Vector3(scale * s, scale * s, 1f);
                }
                p.Highlight = Mathf.MoveTowards(p.Highlight, p.HighlightTarget, Dt / 0.12f);
                p.Root.localScale *= 1f + 0.06f * p.Highlight;
                if (away > 0.001f)
                {
                    Vector3 ls = p.Root.localScale;
                    p.Root.localScale = new Vector3(ls.x * (1f - 0.3f * away), ls.y * (1f - away), 1f);
                    alpha *= 1f - away;
                }
                p.Root.position = rest + offset;
                p.Root.rotation = Quaternion.Euler(0f, 0f, rot);
                Color plateColour = p.Fed ? new Color(0.86f, 0.78f, 0.80f) : tint;
                p.Plate.color = new Color(plateColour.r, plateColour.g, plateColour.b, alpha);
                Color glow = Color.Lerp(TamagotchiArt.WarmPink, TamagotchiArt.Cream, 0.45f);
                glow.a = (0.55f * p.Highlight + (p.Fed ? 0f : PendingGlow())) * alpha;
                p.Glow.color = glow;
                if (p.Mini != null)
                {
                    p.Mini.SetAlpha(alpha);
                }
                if (p.DropAt < 0f)
                {
                    p.Crack.color = Clear(p.Crack.color);
                }
            }
        }

        private PetRequest? RequestFor(int slotId)
        {
            foreach (PetRequest r in State.Requests)
            {
                if (r.SlotId == slotId)
                {
                    return r;
                }
            }
            return null;
        }

        /// <summary>The plate warms as the pet loses patience: cream, then warm pink, then a
        /// deeper raspberry when it is angry.</summary>
        private Color PlateTint()
        {
            switch (State.Stage)
            {
                case PetHungerStage.Impatient: return new Color(1f, 0.91f, 0.93f);
                case PetHungerStage.Angry: return new Color(1f, 0.80f, 0.86f);
                default: return Color.white;
            }
        }

        /// <summary>A faint warm glow under a pending plate that grows with the hunger.</summary>
        private float PendingGlow()
        {
            switch (State.Stage)
            {
                case PetHungerStage.Hungry: return 0.06f;
                case PetHungerStage.Impatient: return 0.1f;
                case PetHungerStage.Angry: return 0.14f;
                default: return 0.03f;
            }
        }

        // ================================================================== the patience ring

        private void TickRing()
        {
            bool show = plates.Count > 0 && plates[0].PopAt >= 0f && clock >= plates[0].PopAt
                && State.Wants && !platesFolded && platesAway < 0.5f;
            float alpha = Tuning.PatienceRingAlpha * (State.Stage == PetHungerStage.Impatient ? 1.3f
                : State.Stage == PetHungerStage.Angry ? 1.45f : 1f);
            int alive = Mathf.CeilToInt((1f - Mathf.Clamp01(State.Progress)) * RingBeads - 0.001f);
            Vector2 c = PlatesCentreNow;
            float pairW = platesVerticalNow ? PlateArtWidth * PlateWorldScale
                : plates.Count * PlateArtWidth * PlateWorldScale + 0.1f * S;
            float pairH = platesVerticalNow ? plates.Count * PlateArtHeight * PlateWorldScale + 0.1f * S
                : PlateArtHeight * PlateWorldScale;
            float rx = pairW * 0.5f + 0.14f * S;
            float ry = pairH * 0.5f + 0.16f * S;
            Color full = Color.Lerp(TamagotchiArt.WarmPink, TamagotchiArt.Cream, 0.3f);
            Color late = Color.Lerp(TamagotchiArt.WarmPink, TamagotchiArt.Raspberry, 0.6f);
            for (int i = 0; i < RingBeads; i++)
            {
                // the beads go from the top, clockwise, as the deadline runs down
                bool on = show && i >= RingBeads - alive;
                ringLife[i] = Mathf.MoveTowards(ringLife[i], on ? 1f : 0f, Dt / (on ? 0.2f : 0.35f));
                float a = Mathf.PI * 0.5f - (i + 0.5f) / RingBeads * Mathf.PI * 2f;
                // a broken halo: every bead a little off the ellipse, and a gap at the bottom
                float wobble = 1f + 0.04f * Mathf.Sin(i * 2.3f + clock * 0.8f);
                Vector2 at = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry) * wobble;
                float life = ringLife[i];
                // a bead going out sags and darkens before it fades
                at.y -= (1f - life) * 0.06f * S * (on ? 0f : 1f);
                ring[i].transform.position = at;
                ring[i].transform.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
                // a bead is about three quarters of its place on the ring: scallops, not dots
                float bs = 1.5f * S;
                ring[i].transform.localScale = new Vector3(bs, bs, 1f);
                Color col = Color.Lerp(late, full, Mathf.Clamp01((float)alive / RingBeads));
                col = Color.Lerp(TamagotchiArt.Berry, col, life);
                col.a = alpha * life * (i % 5 == 4 ? 0.55f : 1f);
                ring[i].color = col;
            }
        }

        // ================================================================== aura, vignette

        private void TickAura()
        {
            float strength;
            Color colour;
            switch (State.Stage)
            {
                case PetHungerStage.Impatient:
                    strength = 0.12f; colour = TamagotchiArt.WarmPink; break;
                case PetHungerStage.Angry:
                    strength = Tuning.AngryAuraStrength; colour = TamagotchiArt.Berry; break;
                case PetHungerStage.Furious:
                    // a perceptual strength, for blending in linear colour
                    strength = 1f - Mathf.Pow(1f - Tuning.FuriousAuraStrength, 2.2f);
                    colour = TamagotchiArt.DeepRaspberry;
                    break;
                case PetHungerStage.Satisfied:
                    strength = 0.04f; colour = TamagotchiArt.WarmPink; break;
                default:
                    strength = 0.015f; colour = TamagotchiArt.WarmPink; break;
            }
            auraBoost = Mathf.MoveTowards(auraBoost, 0f, Dt / 0.6f);
            vignetteBoost = Mathf.MoveTowards(vignetteBoost, 0f, Dt / 0.9f);
            float a = Mathf.Max(strength, auraBoost) * presence;
            Color c = auraBoost > strength ? auraBoostColour : colour;
            Vector3 centre = rig.BellyWorld + new Vector3(0f, 0.12f * S, 0f);
            float pulse = 1f + 0.04f * Noise(clock * 0.7f, 9);
            aura.transform.position = centre;
            aura.transform.localScale = Vector3.one * (2.3f * S * pulse / 0.32f);
            aura.color = new Color(c.r, c.g, c.b, a);
            // the plum shadow under a furious one
            bool furious = State.Stage == PetHungerStage.Furious;
            auraShadow.transform.position = centre + new Vector3(0f, -0.25f * S, 0f);
            auraShadow.transform.localScale = new Vector3(2.6f * S / 0.32f, 1.2f * S / 0.32f, 1f);
            Color plum = TamagotchiArt.Plum;
            auraShadow.color = new Color(plum.r, plum.g, plum.b, (furious ? 0.3f : 0f) * presence);
            // the local dark-magenta vignette round the pet - light now, because the hatred field
            // carries the screen's (the legacy fury still puffs it through vignetteBoost)
            float v = Mathf.Max(furious ? 0.07f : 0f, vignetteBoost);
            vignette.transform.position = centre;
            vignette.transform.localScale = Vector3.one * (5.2f * S / 0.32f);
            Color m = TamagotchiArt.DarkMagenta;
            vignette.color = new Color(m.r, m.g, m.b, v * presence);
        }

        /// <summary>A short outward puff of the aura (the fury burst, a 2/2 meal).</summary>
        private void PuffAura(Color colour, float strength)
        {
            auraBoostColour = colour;
            auraBoost = Mathf.Max(auraBoost, strength);
        }

        // ================================================================== the drag

        private struct DragInfo
        {
            public bool Active;
            public Vector2 World;
            public bool Accepted;
            public int CardId;
            public float Since;
            public int Band;
            public float BandSince;
            public bool WarnedWrong;
            public bool Cheered;
        }

        private DragInfo drag;
        private Vector2 dragHead;
        private float dragTilt;
        private PetEmotion dragEmotion = PetEmotion.Curious;
        private float dragEmotionWeight;
        private string dragMouth;
        private float dragIris = 1f;
        private Vector2? dragLook;
        private PetPaw? dragPawL;
        private PetPaw? dragPawR;
        private float dragMouthOpen = 1f;
        private float noticeBoost;

        /// <summary>The radius of the feed zone, in world units.</summary>
        public float FeedZoneRadius
        {
            get { return 0.82f * TamagotchiArt.TotalWidth * S; }
        }

        /// <summary>The feed zone's middle: the mouth, a little lower.</summary>
        public Vector2 FeedZoneCentre
        {
            get { return (Vector2)rig.MouthWorld - new Vector2(0f, 0.12f * S); }
        }

        /// <summary>A card is being carried (true) at a world point. <paramref name="accepted"/> is
        /// Core's answer to "would it eat this one" (TamagotchiBoss.Accepts).</summary>
        public void SetDrag(bool active, Vector2 world, int cardId, bool accepted)
        {
            if (!shown)
            {
                return;
            }
            if (active && !drag.Active)
            {
                drag = new DragInfo { Active = true, Since = clock, CardId = cardId, BandSince = clock, Band = -1 };
                // a drag is a gameplay event: an idle gives way at once
                if (queue.Current != null && queue.Current.Priority == PetPresentationPriority.Idle)
                {
                    queue.Stop(true);
                }
                if (State.BossActive && !exiting)
                {
                    Sound(PetSound.NoticeDraggedCard);
                    noticeBoost = 1f;
                }
            }
            if (!active)
            {
                ClearDrag();
                return;
            }
            drag.World = world;
            drag.Accepted = accepted;
            drag.CardId = cardId;
        }

        public void ClearDrag()
        {
            drag = new DragInfo();
            foreach (PlateView p in plates)
            {
                p.HighlightTarget = 0f;
            }
        }

        /// <summary>True when a card let go here lands in the pet's feed zone (while it still takes
        /// food). Whether it is FOOD is Core's question, not this one.</summary>
        public bool InFeedZone(Vector2 world)
        {
            if (!shown || !State.Wants || exiting)
            {
                return false;
            }
            return (world - FeedZoneCentre).magnitude <= FeedZoneRadius;
        }

        /// <summary>The drag's distance band: 0 far, 1 medium, 2 near, 3 in the feed zone.</summary>
        private int BandOf(float distance)
        {
            float r = FeedZoneRadius;
            return distance <= r ? 3 : distance <= 2.2f * r ? 2 : distance <= 4.2f * r ? 1 : 0;
        }

        /// <summary>Writes the drag's acting into the layer ComposePose reads. Muted while a beat
        /// that is not an idle owns the body.</summary>
        private void ApplyDragLayer(out Vector2 lean, out float rot)
        {
            lean = Vector2.zero;
            rot = 0f;
            dragHead = Vector2.zero;
            dragTilt = 0f;
            dragEmotionWeight = Mathf.MoveTowards(dragEmotionWeight, 0f, Dt / 0.15f);
            dragMouth = null;
            dragLook = null;
            dragPawL = null;
            dragPawR = null;
            dragMouthOpen = 1f;
            noticeBoost = Mathf.MoveTowards(noticeBoost, 0f, Dt / 0.35f);
            dragIris = 1f + 0.1f * noticeBoost;
            bool owned = queue.Current != null && queue.Current.Priority > PetPresentationPriority.Idle;
            if (!drag.Active || owned || exiting)
            {
                foreach (PlateView p in plates)
                {
                    p.HighlightTarget = 0f;
                }
                return;
            }
            Vector2 card = drag.World;
            Vector2 toCard = card - (Vector2)rig.MouthWorld;
            float d = (card - FeedZoneCentre).magnitude;
            int band = BandOf(d);
            if (band != drag.Band)
            {
                if (band >= 2 && !drag.Accepted && State.Wants && !drag.WarnedWrong)
                {
                    drag.WarnedWrong = true;
                    Sound(PetSound.WrongFoodNear);
                }
                if (band == 3 && drag.Accepted && State.Wants)
                {
                    Sound(PetSound.ValidFoodNear);
                    drag.Cheered = true;
                }
                drag.Band = band;
                drag.BandSince = clock;
            }
            Vector2 worldDir = toCard.sqrMagnitude > 0.0001f ? toCard.normalized : new Vector2(F, 0f);
            // the lean is a WORLD offset; the head, the tilt and the paws are in the pet's own frame
            Vector2 dir = ToLocal(worldDir);
            dragLook = LookTo(card) * (band >= 2 ? 1.15f : 1f);

            // a content pet only watches; a furious one glares at what it could take
            if (!State.Wants)
            {
                dragEmotion = State.Furious ? PetEmotion.Angry : PetEmotion.Happy;
                dragEmotionWeight = Mathf.MoveTowards(dragEmotionWeight, State.Furious ? 0.6f : 0.3f, Dt / 0.1f);
                foreach (PlateView p in plates)
                {
                    p.HighlightTarget = 0f;
                }
                return;
            }

            // THE WRONG CARD: mouth shut, head aside, eyes card -> plate -> card. No red X.
            if (!drag.Accepted && band >= 2)
            {
                dragEmotion = PetEmotion.Confused;
                dragEmotionWeight = Mathf.MoveTowards(dragEmotionWeight, 1f, Dt / 0.1f);
                dragMouth = "frown";
                float since = clock - drag.BandSince;
                float cycle = since % 1.1f;
                if (cycle > 0.3f && cycle < 0.65f && plates.Count > 0)
                {
                    dragLook = LookTo(PlateWorld(FirstPending()));
                }
                dragHead = -dir * 0.018f;
                dragTilt = dir.x * 7f;
                rot = dir.x * 1.2f;
                foreach (PlateView p in plates)
                {
                    p.HighlightTarget = 0f;
                }
                return;
            }

            float px = Home.PxToWorld;
            switch (band)
            {
                case 0:
                    // far: the eyes only
                    dragEmotion = PetEmotion.Curious;
                    dragEmotionWeight = Mathf.MoveTowards(dragEmotionWeight, 0.25f, Dt / 0.1f);
                    break;
                case 1:
                    // medium: the head turns, the body leans 2 px
                    dragEmotion = PetEmotion.Curious;
                    dragEmotionWeight = Mathf.MoveTowards(dragEmotionWeight, 0.6f, Dt / 0.1f);
                    lean = worldDir * 2f * px;
                    dragHead = dir * 0.012f;
                    dragTilt = -dir.x * Tuning.HeadLeanMax;
                    break;
                case 2:
                    // near: the eyes grow, the paws come up, the mouth opens a third
                    dragEmotion = PetEmotion.Curious;
                    dragEmotionWeight = Mathf.MoveTowards(dragEmotionWeight, 1f, Dt / 0.1f);
                    dragIris = 1.1f + 0.1f * noticeBoost;
                    dragMouth = "medium";
                    dragMouthOpen = 0.45f * Tuning.MouthNearScale;
                    lean = worldDir * 5f * px;
                    dragHead = dir * 0.018f;
                    dragTilt = -dir.x * Tuning.HeadLeanMax;
                    dragPawL = Paw(0.02f, 0.05f, 38f);
                    dragPawR = Paw(-0.02f, 0.05f, 38f);
                    break;
                default:
                    // in the zone: it reaches for it - mouth wide, a few excited beats
                    dragEmotion = PetEmotion.Excited;
                    dragEmotionWeight = Mathf.MoveTowards(dragEmotionWeight, 1f, Dt / 0.08f);
                    dragIris = 1.16f;
                    dragMouth = "wide";
                    dragMouthOpen = Mathf.Lerp(0.85f, 1f, Bell(Mathf.Repeat(clock * 1.4f, 1f))) * Tuning.MouthNearScale;
                    lean = worldDir * Tuning.NearFoodLean * px;
                    dragHead = dir * 0.024f;
                    dragTilt = -dir.x * Tuning.HeadLeanMax * 1.2f;
                    dragPawL = Paw(0.05f + Mathf.Max(0f, dir.x) * 0.06f, 0.08f, 58f);
                    dragPawR = Paw(-0.05f + Mathf.Min(0f, dir.x) * 0.06f, 0.08f, 58f);
                    // 2-3 controlled excited beats, then still
                    float since = clock - drag.BandSince;
                    if (since < 0.45f)
                    {
                        rot = Mathf.Sin(since * Mathf.PI * 2f / 0.15f) * 0.5f * (1f - since / 0.45f);
                    }
                    break;
            }
            // the plate this card answers glows warm
            foreach (PlateView p in plates)
            {
                p.HighlightTarget = band >= 2 && p.CardId == drag.CardId && !p.Fed ? (band == 3 ? 1f : 0.45f) : 0f;
            }
        }

        private void TickFeedZone()
        {
            bool on = drag.Active && State.Wants && !exiting;
            float a = 0f;
            if (on)
            {
                float d = (drag.World - FeedZoneCentre).magnitude;
                float near = 1f - Mathf.Clamp01((d - FeedZoneRadius) / (3f * FeedZoneRadius));
                a = 0.07f + 0.14f * near;
                if (d <= FeedZoneRadius)
                {
                    a = drag.Accepted ? 0.3f + 0.04f * Mathf.Sin(clock * 9f) : 0.1f;
                }
            }
            Color c = Color.Lerp(TamagotchiArt.WarmPink, TamagotchiArt.Raspberry, 0.25f);
            Color now = feedZone.color;
            float alpha = Mathf.MoveTowards(now.a, a, Dt / 0.12f);
            feedZone.color = new Color(c.r, c.g, c.b, alpha);
            feedZone.transform.position = FeedZoneCentre;
            feedZone.transform.localScale = Vector3.one * (FeedZoneRadius * 2.1f / 0.32f);
        }
    }
}
