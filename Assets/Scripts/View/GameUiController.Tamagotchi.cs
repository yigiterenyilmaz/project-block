// PURPOSE: "Tamagotchi" wired into the round - the controller's half of the pet. TamagotchiView
// acts; this decides nothing either, it only CARRIES: Core's state into the view every frame
// (TamagotchiRoundVisualState, built from TamagotchiBoss), Core's per-event reports in by IDENTITY
// (LastFeed, LastHungerChange, LastFury, LastRampage - a new object per event, so a serial that
// restarts with the boss can never skip one), the screen's anchors (the board, the hand, the piles,
// and every joker and power panel REMEMBERED each frame, because by the time the pet eats one Core
// has already taken it out of the bar), and the player's hand: a card dragged toward the pet, a card
// let go in its feed zone (fed through RoundEngine.FeedPet, Core first, then the meal), and the gap
// the fed card leaves in the hand until the next placement refills it.
//
// WHERE THE PET LIVES (SolvePetHome) is solved here because only this class can see the whole
// screen: it stands at a SCREEN EDGE, never in a panel - on the desktop the bottom corner beside the
// draw pile (the right preferred, the left if the bars or piles crowd it), on a phone in the band
// between the board and the hand beside a pile, where the hand is the edge it hides behind. The
// side is chosen when the pet appears and kept for the round; a resize only re-places it there. It
// never covers the board.
// EXTENSION POINT: another screen shape is another candidate in SolvePetHome.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private TamagotchiView petView;
        private TamagotchiFeedVisuals lastPetFeed;
        private TamagotchiHungerChange lastPetHunger;
        private TamagotchiFuryVisuals lastPetFury;
        private PetRampageVisuals lastPetRampage;
        private PetRampageVisuals preparedPetRampage;
        private bool petDeadlineWarned;
        private RoundEngine petRound;
        private int petHomeSide;            // +1 right, -1 left; 0 = not chosen yet
        private UiShape petHomeShape;
        private float petHomeAspect;
        private Vector2? petHandFocus;

        /// <summary>True while the animation lab is driving the pet (the round's state stays out).</summary>
        private bool petLabDriving;

        private TamagotchiView PetView
        {
            get
            {
                if (petView == null)
                {
                    petView = TamagotchiView.Create(transform, sfx);
                    petView.Cards = cardLayer;
                    petView.Says += line =>
                    {
                        if (messageText != null)
                        {
                            messageText.text = line;
                        }
                    };
                    petView.DimBar += (dim, jokers) =>
                    {
                        if (jokers)
                        {
                            jokerBar.SetPresentationAlpha(dim ? 0.55f : 1f);
                        }
                        else
                        {
                            powerBar.SetPresentationAlpha(dim ? 0.55f : 1f);
                        }
                    };
                    petView.BoardImpulse += offset => boardView.SetImpulse(offset);
                }
                return petView;
            }
        }

        /// <summary>The pet's input lock (a feed's snap and grab; a punish until it has been
        /// seen). Part of the board-animation lock in Update.</summary>
        private bool PetLocksInput
        {
            get { return petView != null && !petLabDriving && petView.LocksInput; }
        }

        // ================================================================== every frame

        /// <summary>
        /// Called at the top of Update, before any input is read: the panels' places are taken
        /// while they still stand, the anchors and the round's state go to the view, and any
        /// report Core wrote since the last frame is played.
        /// </summary>
        private void TickTamagotchi()
        {
            if (session == null || petLabDriving)
            {
                return;
            }
            RoundEngine round = session.Phase == GamePhase.Round ? session.CurrentRound : null;
            var pet = round != null ? round.Boss as TamagotchiBoss : null;
            if (pet == null && petView == null)
            {
                return;
            }
            TamagotchiView view = PetView;
            RememberPetAnchors();
            bool introHolding = bossIdentity != null && bossIdentity.IntroPlaying;
            bool active = pet != null && screen == AppScreen.Playing
                && (round.Status == RoundStatus.InProgress || round.Status == RoundStatus.AwaitingAdvanceDecision);
            if (pet != null && !ReferenceEquals(round, petRound))
            {
                // a new round with a pet: a new home and a clean slate of reports
                petRound = round;
                petHomeSide = 0;
                lastPetFeed = pet.LastFeed;
                lastPetHunger = pet.LastHungerChange;
                lastPetFury = pet.LastFury;
                lastPetRampage = pet.LastRampage;
                preparedPetRampage = pet.LastRampage;
                petDeadlineWarned = false;
                view.HideNow();
            }
            if (view.Shown && (!active || pet == null))
            {
                view.Leave();
                return;
            }
            if (!active || introHolding)
            {
                return;
            }
            SolvePetHomeIfNeeded();
            view.Sync(BuildPetState(round, pet));
            PlayPetReports(round, pet);
        }

        private TamagotchiRoundVisualState BuildPetState(RoundEngine round, TamagotchiBoss pet)
        {
            var state = new TamagotchiRoundVisualState();
            state.BossActive = true;
            for (int i = 0; i < pet.RequestCount; i++)
            {
                state.Requests.Add(pet.Request(round, i));
            }
            state.Stage = pet.Stage(round);
            state.Progress = pet.HungerProgress(round);
            state.DeadlineNext = pet.DeadlineNext(round);
            state.HomeSide = petHomeSide;
            // stable for the round: its number and what it asked for
            uint seed = (uint)(session.RoundNumber * 2654435761u) ^ (session.InBossStage ? 0x9e37u : 0u);
            foreach (PetRequest r in state.Requests)
            {
                seed = seed * 31u + (uint)(r.Card != null ? r.Card.Id : r.SlotId + 7);
            }
            state.Seed = seed == 0 ? 1u : seed;
            return state;
        }

        /// <summary>Every report Core wrote since the last frame, played once each.</summary>
        private void PlayPetReports(RoundEngine round, TamagotchiBoss pet)
        {
            TamagotchiView view = PetView;
            // a meal the drag or the F key did not already play (a pad path, a lab)
            if (pet.LastFeed != null && !ReferenceEquals(pet.LastFeed, lastPetFeed))
            {
                lastPetFeed = pet.LastFeed;
                view.PlayFeed(pet.LastFeed, UiLayout.Active.HandCenter, CardLayerView.CardScale);
            }
            if (pet.LastHungerChange != null && !ReferenceEquals(pet.LastHungerChange, lastPetHunger))
            {
                lastPetHunger = pet.LastHungerChange;
                // the fury and the 2/2 are told by their own beats
                if (pet.LastHungerChange.Stage != PetHungerStage.Furious)
                {
                    view.PlayHungerChange(pet.LastHungerChange);
                }
            }
            if (pet.LastFury != null && !ReferenceEquals(pet.LastFury, lastPetFury))
            {
                lastPetFury = pet.LastFury;
                view.PlayFury(pet.LastFury);
            }
            if (pet.LastRampage != null && !ReferenceEquals(pet.LastRampage, lastPetRampage))
            {
                lastPetRampage = pet.LastRampage;
                if (!ReferenceEquals(preparedPetRampage, pet.LastRampage))
                {
                    PreparePetRampage(round, pet.LastRampage);
                }
                view.PlayPunish(pet.LastRampage);
            }
            bool deadline = pet.DeadlineNext(round);
            if (deadline && !petDeadlineWarned)
            {
                view.PlayPrewarning();
            }
            petDeadlineWarned = deadline;
        }

        /// <summary>
        /// The repaint after a turn: a punish Core has just resolved is covered ON THIS FRAME -
        /// the bitten cells still look like ground, the eaten joker still stands in its slot, the
        /// pile still holds its cards - so nothing is seen to vanish before the pet takes it.
        /// Called from RefreshAll.
        /// </summary>
        private void SyncPetOnRepaint(RoundEngine round)
        {
            var pet = round != null ? round.Boss as TamagotchiBoss : null;
            if (pet == null || petLabDriving)
            {
                return;
            }
            if (pet.LastRampage != null && !ReferenceEquals(pet.LastRampage, preparedPetRampage))
            {
                PreparePetRampage(round, pet.LastRampage);
            }
        }

        private void PreparePetRampage(RoundEngine round, PetRampageVisuals rampage)
        {
            preparedPetRampage = rampage;
            TamagotchiView view = PetView;
            if (!view.Shown)
            {
                return;
            }
            RememberPetAnchors();
            IReadOnlyList<BlockCard> pile = rampage.Kind == PetPunishKind.DrawPile ? round.Deck.DrawPile
                : rampage.Kind == PetPunishKind.DiscardPile ? round.Deck.DiscardPile : null;
            view.PreparePunish(rampage, pile);
        }

        // ================================================================== anchors

        /// <summary>The screen as the pet sees it, in world space. The panels are remembered by
        /// instance id while they still stand.</summary>
        private void RememberPetAnchors()
        {
            if (petView == null || cam == null || session == null)
            {
                return;
            }
            TamagotchiAnchors a = petView.Anchors;
            Rect arena = boardView.ArenaRect;
            a.Board = arena;
            a.BoardCentre = arena.center;
            a.CellSize = boardView.CellWorldSize;
            a.EmptySlot = boardView.EmptySlotSize;
            a.GroundColour = BoardView.EmptySlotColor;
            a.CellToWorld = c => (Vector2)boardView.transform.TransformPoint(boardView.CellToWorld(c));
            a.HandCentre = cardLayer.transform.TransformPoint(UiLayout.Active.HandCenter);
            a.HandFocus = petHandFocus;
            a.DrawPile = cardLayer.PileTopWorld(true);
            a.DiscardPile = cardLayer.PileTopWorld(false);
            float z = -cam.transform.position.z;
            float screenPerCanvas = Screen.width / Mathf.Max(1f, UiLayout.Active.CanvasReference.x);
            a.JokerPanels.Clear();
            Vector2 jokerSum = Vector2.zero;
            int jokers = 0;
            for (int i = 0; i < session.Jokers.Count; i++)
            {
                Vector2? c = jokerBar.PanelScreenCenter(i);
                if (!c.HasValue)
                {
                    continue;
                }
                Rect r = ScreenRectToWorld(c.Value, UiLayout.Active.JokerPanel * screenPerCanvas, z);
                a.JokerPanels[session.Jokers.Jokers[i].InstanceId] = r;
                jokerSum += r.center;
                jokers++;
            }
            a.PowerPanels.Clear();
            Vector2 powerSum = Vector2.zero;
            int powers = 0;
            for (int i = 0; i < session.Powers.Count; i++)
            {
                Vector2? c = powerBar.PanelScreenCenter(i);
                if (!c.HasValue)
                {
                    continue;
                }
                Rect r = ScreenRectToWorld(c.Value, UiLayout.Active.PowerPanel * screenPerCanvas, z);
                a.PowerPanels[session.Powers.Powers[i].InstanceId] = r;
                powerSum += r.center;
                powers++;
            }
            float hw = UiLayout.Active.HalfWidth;
            float oh = UiLayout.Active.OrthoSize;
            a.JokerBar = jokers > 0 ? jokerSum / jokers : new Vector2(hw * 0.8f, oh * 0.75f);
            a.PowerBar = powers > 0 ? powerSum / powers : new Vector2(-hw * 0.8f, oh * 0.75f);
        }

        private Rect ScreenRectToWorld(Vector2 centre, Vector2 size, float z)
        {
            Vector3 a = cam.ScreenToWorldPoint(new Vector3(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, z));
            Vector3 b = cam.ScreenToWorldPoint(new Vector3(centre.x + size.x * 0.5f, centre.y + size.y * 0.5f, z));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // ================================================================== the home

        private void SolvePetHomeIfNeeded()
        {
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1.78f;
            if (petHomeSide != 0 && petHomeShape == UiLayout.Active.Shape && Mathf.Approximately(petHomeAspect, aspect))
            {
                return;
            }
            petHomeShape = UiLayout.Active.Shape;
            petHomeAspect = aspect;
            TamagotchiHome home = SolvePetHome(petHomeSide, TamagotchiView.Tuning.BodyScale);
            petHomeSide = home.Facing < 0 ? 1 : -1;
            PetView.Home = home;
        }

        /// <summary>
        /// Where the pet lives: a screen edge with room for it and its two plates, clear of the
        /// board, the hand, the piles and the bars. <paramref name="keepSide"/> (+1 right, -1 left,
        /// 0 free) holds the side chosen for the round. Desktop prefers the RIGHT.
        /// </summary>
        private TamagotchiHome SolvePetHome(int keepSide, float bodyCells)
        {
            UiLayout layout = UiLayout.Active;
            var obstacles = PetObstacles();
            float cell = boardView.CellWorldSize > 0f ? boardView.CellWorldSize : layout.BoardWorldSize / 7f;
            int[] sides = keepSide != 0 ? new[] { keepSide } : new[] { 1, -1 };
            TamagotchiHome best = null;
            foreach (float cells in new[] { bodyCells, Mathf.Max(1.4f, bodyCells - 0.2f), 1.4f })
            {
                foreach (int side in sides)
                {
                    // the plates side by side over it, side by side lifted clear of what is under
                    // them, or stacked up the screen's edge - whichever fits first
                    foreach (int plates in new[] { 0, 2, 1 })
                    {
                        TamagotchiHome h = layout.IsPortrait ? PortraitHome(side, cells, cell) : CornerHome(side, cells, cell);
                        if (plates > 0 && !ArrangePlates(h, plates, obstacles))
                        {
                            continue;
                        }
                        bool free = !Overlaps(h.PetRect, obstacles) && !Overlaps(PlatesRect(h), obstacles);
                        if (free)
                        {
                            return h;
                        }
                        if (best == null)
                        {
                            best = h;
                        }
                    }
                }
            }
            best.Squeezed = true;
            return best;
        }

        /// <summary>Desktop: the bottom corner beside a pile, standing on the screen's edge.</summary>
        private TamagotchiHome CornerHome(int side, float cells, float cell)
        {
            UiLayout layout = UiLayout.Active;
            var h = new TamagotchiHome();
            h.Name = side > 0 ? "bottom-right corner" : "bottom-left corner";
            h.UnitScale = cells * cell / TamagotchiArt.TotalWidth;
            h.PxToWorld = layout.WorldPerCanvasPixel;
            float s = h.UnitScale;
            float edge = side * layout.HalfWidth;
            float floor = -layout.OrthoSize;
            h.Facing = -side;
            h.Base = new Vector2(edge - side * 0.72f * s, floor - 0.04f * s);
            h.Clip = Rect.MinMaxRect(-layout.HalfWidth, floor, layout.HalfWidth, layout.OrthoSize);
            h.HideDepth = (h.Base.y - floor) + 1.2f * s;
            h.PetRect = Rect.MinMaxRect(h.Base.x - 0.66f * s, floor, h.Base.x + 0.66f * s, h.Base.y + 1.12f * s);
            // the plates side by side above it, kept on screen
            float pairW = 2f * 0.55f * 1.3f * s + 0.1f * s;
            float plateH = 0.65f * 1.3f * s;
            float cx = side > 0 ? Mathf.Min(h.Base.x - 0.12f * s, layout.HalfWidth - pairW * 0.5f - 0.12f)
                : Mathf.Max(h.Base.x + 0.12f * s, -layout.HalfWidth + pairW * 0.5f + 0.12f);
            h.PlatesCentre = new Vector2(cx, h.Base.y + 1.2f * s + plateH * 0.5f + 0.16f * s);
            h.PlatesVertical = false;
            h.EdgeTap = new Vector2(edge, h.Base.y + 0.4f * s);
            return h;
        }

        /// <summary>Phone: in the band between the board and the hand, beside a pile; the hand's
        /// top is the edge it hides behind.</summary>
        private TamagotchiHome PortraitHome(int side, float cells, float cell)
        {
            UiLayout layout = UiLayout.Active;
            var h = new TamagotchiHome();
            h.Name = side > 0 ? "hand band, right" : "hand band, left";
            h.UnitScale = cells * cell / TamagotchiArt.TotalWidth;
            h.PxToWorld = layout.WorldPerCanvasPixel;
            float s = h.UnitScale;
            float handTop = layout.HandCenter.y + CardVisual.BodyHeight * layout.CardScale * 0.5f;
            Vector2 pile = side > 0 ? layout.DrawPile : layout.DiscardPile;
            float pileHalfW = (CardVisual.BodyWidth + 0.18f) * layout.PileScale * 0.5f;
            h.Facing = -side;
            h.Base = new Vector2(pile.x - side * (pileHalfW + 0.08f + 0.66f * s), handTop - 0.04f * s);
            h.Clip = Rect.MinMaxRect(-layout.HalfWidth, handTop, layout.HalfWidth, layout.OrthoSize);
            h.HideDepth = (h.Base.y - handTop) + 1.2f * s;
            h.PetRect = Rect.MinMaxRect(h.Base.x - 0.66f * s, handTop, h.Base.x + 0.66f * s, h.Base.y + 1.12f * s);
            float pairW = 2f * 0.55f * 1.15f * s + 0.1f * s;
            h.PlateScale = 1.15f / 1.3f;
            h.PlatesCentre = new Vector2(h.Base.x - side * (0.7f * s + pairW * 0.5f + 0.1f), h.Base.y + 0.55f * s);
            h.PlatesVertical = false;
            h.EdgeTap = new Vector2(pile.x - side * pileHalfW, h.Base.y + 0.4f * s);
            return h;
        }

        /// <summary>The other two ways to hang the plates: lifted over whatever is under them
        /// (1), or stacked up the edge beside the pet's head (2). False when it does not apply.</summary>
        private static bool ArrangePlates(TamagotchiHome h, int way, List<Rect> obstacles)
        {
            float s = h.UnitScale;
            Rect plates = PlatesRect(h);
            if (way == 1)
            {
                float lift = 0f;
                foreach (Rect o in obstacles)
                {
                    if (plates.Overlaps(o))
                    {
                        lift = Mathf.Max(lift, o.yMax + 0.08f - plates.yMin);
                    }
                }
                // a lift that leaves the plates floating far from the pet is no home
                if (lift <= 0f || lift > 0.7f * s)
                {
                    return false;
                }
                h.PlatesCentre += new Vector2(0f, lift);
                return true;
            }
            h.PlatesVertical = true;
            float plateW = 0.55f * 1.3f * s * h.PlateScale;
            float stackH = 2f * 0.65f * 1.3f * s * h.PlateScale + 0.1f * s;
            float edge = h.EdgeTap.x;
            float x = edge - Mathf.Sign(edge) * (plateW * 0.5f + 0.1f);
            h.PlatesCentre = new Vector2(x, h.PetRect.yMax + stackH * 0.5f + 0.12f * s);
            return true;
        }

        private static Rect PlatesRect(TamagotchiHome h)
        {
            float s = h.UnitScale * 1.3f * h.PlateScale;
            float w = h.PlatesVertical ? 0.55f * s : 2f * 0.55f * s + 0.1f * h.UnitScale;
            float hh = h.PlatesVertical ? 2f * 0.65f * s + 0.1f * h.UnitScale : 0.65f * s;
            return new Rect(h.PlatesCentre.x - w * 0.5f, h.PlatesCentre.y - hh * 0.5f, w, hh);
        }

        /// <summary>What the pet may not stand on: the board, the hand, the piles, every joker and
        /// power panel, and the HUD band along the top.</summary>
        private List<Rect> PetObstacles()
        {
            UiLayout layout = UiLayout.Active;
            var list = new List<Rect>();
            Rect board = boardView.ArenaRect;
            if (board.width <= 0f)
            {
                float b = layout.BoardWorldSize * 0.5f;
                board = new Rect(layout.BoardCenter.x - b, layout.BoardCenter.y - b, b * 2f, b * 2f);
            }
            list.Add(Grow(board, 0.15f));
            float cardH = CardVisual.BodyHeight * layout.CardScale;
            float handHalf = layout.HandFanSpanMax * 0.5f + CardVisual.BodyWidth * layout.CardScale * 0.5f;
            // a hovered card rises a little out of the fan; a plate it brushes is fine
            list.Add(new Rect(layout.HandCenter.x - handHalf, layout.HandCenter.y - cardH * 0.5f, handHalf * 2f, cardH + 0.12f));
            float pw = (CardVisual.BodyWidth + 0.18f) * layout.PileScale;
            float ph = (CardVisual.BodyHeight + 0.18f) * layout.PileScale;
            list.Add(new Rect(layout.DrawPile.x - pw * 0.5f, layout.DrawPile.y - ph * 0.5f, pw, ph + 0.35f));
            list.Add(new Rect(layout.DiscardPile.x - pw * 0.5f, layout.DiscardPile.y - ph * 0.5f, pw, ph + 0.35f));
            if (petView != null)
            {
                foreach (Rect r in petView.Anchors.JokerPanels.Values)
                {
                    list.Add(Grow(r, 0.08f));
                }
                foreach (Rect r in petView.Anchors.PowerPanels.Values)
                {
                    list.Add(Grow(r, 0.08f));
                }
            }
            // the lab's crowded-screen scene adds what a full run would have on its edges
            list.AddRange(petLabExtraObstacles);
            float top = layout.OrthoSize - (layout.MessageTop + layout.MessageFont + 8f) * layout.WorldPerCanvasPixel;
            list.Add(Rect.MinMaxRect(-layout.HalfWidth * 0.45f, top, layout.HalfWidth * 0.45f, layout.OrthoSize));
            return list;
        }

        private static Rect Grow(Rect r, float by)
        {
            return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
        }

        private static bool Overlaps(Rect r, List<Rect> obstacles)
        {
            foreach (Rect o in obstacles)
            {
                if (r.Overlaps(o))
                {
                    return true;
                }
            }
            return false;
        }

        // ================================================================== the hand

        /// <summary>A card is being dragged (or not): the pet tracks it. Called by HandleDrag.</summary>
        private void PetDragTick(RoundEngine round, CardVisual dragged, Vector2 world)
        {
            if (petView == null || !petView.Shown || petLabDriving)
            {
                return;
            }
            var pet = round != null ? round.Boss as TamagotchiBoss : null;
            BlockCard card = dragged != null && round != null ? CardOfSlot(round, dragged.SlotIndex) : null;
            if (pet == null || card == null)
            {
                petView.SetDrag(false, world, 0, false);
                return;
            }
            bool handCard = dragged.SlotIndex >= 0 && dragged.SlotIndex < round.Hand.Count;
            petView.SetDrag(true, world, card.Id, handCard && pet.Accepts(round, card));
        }

        /// <summary>
        /// A dragged card let go: if it landed in the pet's feed zone, this is a FEED attempt and
        /// returns true (the drop is the pet's, never the board's). Core decides whether the card
        /// is food; the pet shows the answer either way.
        /// </summary>
        private bool TryDropOnPet(RoundEngine round, CardVisual released, Vector2 world)
        {
            if (petView == null || !petView.Shown || petLabDriving || released == null)
            {
                return false;
            }
            var pet = round.Boss as TamagotchiBoss;
            if (pet == null || !petView.InFeedZone(world))
            {
                return false;
            }
            int slot = released.SlotIndex;
            if (slot >= 0 && slot < round.Hand.Count && FeedPetFromHand(round, slot, world,
                released.transform.lossyScale.x))
            {
                return true;
            }
            // not food: back to the hand, and the pet says so with its face
            petView.PlayWrongFood(world);
            released.MoveTo(released.HomePosition, 0.2f, null);
            messageText.text = Loc.Pick("It does not want that card - only the ones on its plates.",
                "O kartı istemiyor - sadece tabaklarındaki kartları ister.");
            return true;
        }

        /// <summary>
        /// Feeds the pet the hand card in <paramref name="slot"/>: Core first (RoundEngine.FeedPet
        /// takes it out of the run and leaves the hand a card short), then the hand is repainted
        /// with the slot held OPEN, and the meal plays from where the card was.
        /// </summary>
        private bool FeedPetFromHand(RoundEngine round, int slot, Vector2 from, float fromScale)
        {
            var pet = round.Boss as TamagotchiBoss;
            if (pet == null)
            {
                return false;
            }
            int before = round.Hand.Count;
            if (!round.FeedPet(slot))
            {
                return false;
            }
            TamagotchiFeedVisuals meal = pet.LastFeed;
            lastPetFeed = meal;
            bool shortHanded = round.Hand.Count < before;
            if (shortHanded && meal != null)
            {
                cardLayer.SetPetGap(meal.HandSlotIndex);
            }
            RefreshAll(null);
            if (meal != null)
            {
                PetView.PlayFeed(meal, from, fromScale);
                Vector2? gap = cardLayer.PetGapWorld;
                if (gap.HasValue)
                {
                    PetView.SpawnHandResidue(gap.Value);
                }
            }
            AutoSave();
            return true;
        }
    }
}
