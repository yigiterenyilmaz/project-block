// PURPOSE: "Tamagotchi" in the ANIMATION LAB (F3) - the brief's section "TAMAGOTCHI / BOSS": 98
// scenes (character, entrance, drag, feed, hunger, fury, board / joker / power / pile eating, post
// attack, exit, stress), its 22 debug toggles and the punish debug view, plus LOD and slow motion.
//
// THE LAB DRIVES THE REAL PET. Every scene calls the methods the game calls (TamagotchiView's
// PlayFeed, PlayFury, PreparePunish / PlayPunish, PlayIdle, Leave...) and only FABRICATES their
// arguments: the requests are cards out of the round as it stands (read, never moved), their tiers
// are a scratch TamagotchiBoss's own verdict (TierOf - the rules' definition, not a copy), the
// reports are built the way the boss builds them, and the board bites happen on a 7x7 board of the
// lab's OWN (GameBoard.MarkDeadOnLabBoard - the rules' own MarkDead). A "beat alone" scene uses
// TamagotchiView.LabIsolate: the sequence runs fast up to that beat and stops after it, so a retimed
// beat shows its new timing here for free. Nothing here touches the round's Core state; RESET or
// closing the lab gives the pet back to the round (StopPetLab).

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private int petLabSide;              // 0 free, +1 right, -1 left
        private UiShape? petLabLayoutBefore;
        private bool petLabLayoutForced;
        private CardVisual petLabCard;
        private Coroutine petLabRoutine;
        private readonly List<Rect> petLabExtraObstacles = new List<Rect>();

        private void AddTamagotchiLab()
        {
            AddAnimSub("bosses", "tamagotchi", "tamagotchi / boss", "tamagotchi / patron");

            // --- CHARACTER ---
            AddPet("1 static character", "1 durağan karakter", () => { PetLabShow(PetHungerStage.Calm, 0f); PetView.LabStill = true; PetView.IdlesEnabled = false; });
            AddPet("2 blink", "2 göz kırpma", () => { PetLabShow(PetHungerStage.Calm, 0f); PetView.IdlesEnabled = false; PetView.LabBlink(); });
            AddPet("3 weight shift (bob + sway)", "3 ağırlık aktarımı (salınım)", () => { PetLabShow(PetHungerStage.Calm, 0f); PetView.IdlesEnabled = false; PetView.LabBreath = false; PetView.LabBlinks = false; });
            AddPet("4 breathing alone", "4 yalnız nefes", () => { PetLabShow(PetHungerStage.Calm, 0f); PetView.IdlesEnabled = false; PetView.LabBob = false; PetView.LabSway = false; PetView.LabBlinks = false; });
            AddPetIdle("5 peek", "5 saklanıp bakma", TamagotchiView.IdleKind.Peek, PetHungerStage.Calm);
            AddPetIdle("6 hand wave", "6 el sallama", TamagotchiView.IdleKind.HandWave, PetHungerStage.Calm);
            AddPetIdle("7 yawn", "7 esneme", TamagotchiView.IdleKind.Yawn, PetHungerStage.Calm);
            AddPetIdle("8 think", "8 düşünme", TamagotchiView.IdleKind.Think, PetHungerStage.Calm);
            AddPetIdle("9 belly pat", "9 karın pışpışlama", TamagotchiView.IdleKind.BellyPat, PetHungerStage.Calm);
            AddPetIdle("10 cheek squish", "10 yanak sıkıştırma", TamagotchiView.IdleKind.CheekSquish, PetHungerStage.Calm);
            AddPetIdle("11 little hop", "11 küçük zıplama", TamagotchiView.IdleKind.LittleHop, PetHungerStage.Calm);
            AddPetIdle("12 request glance", "12 tabaklara bakış", TamagotchiView.IdleKind.RequestGlance, PetHungerStage.Calm);
            AddPetIdle("13 tongue lick", "13 dil yalama", TamagotchiView.IdleKind.Lick, PetHungerStage.Calm);
            AddPetIdle("14 edge tap", "14 kenara tıklama", TamagotchiView.IdleKind.EdgeTap, PetHungerStage.Calm);
            AddPetIdle("15 sleepy idle (satisfied)", "15 uykulu bekleme (doymuş)", TamagotchiView.IdleKind.NapWobble, PetHungerStage.Satisfied);
            AddPetIdle("+ snack dream (rare)", "+ atıştırma rüyası (nadir)", TamagotchiView.IdleKind.SnackDream, PetHungerStage.Calm);
            AddPetIdle("+ mouth open wait", "+ ağzı açık bekleme", TamagotchiView.IdleKind.MouthOpenWait, PetHungerStage.Calm);
            AddPetIdle("+ card track", "+ kart takibi", TamagotchiView.IdleKind.CardTrack, PetHungerStage.Calm);
            AddPet("+ live idle library (scheduler on, 30 s)", "+ canlı boşta kütüphanesi (zamanlayıcı açık)",
                () => { PetLabShow(PetHungerStage.Calm, 0.1f); PetView.LabNextIdle(); });

            // --- ENTRANCE ---
            AddPet("16 full entrance", "16 tam giriş", () => PetLabEnter(null, 1f));
            AddPet("17 peek only (antennae, eyes, scan)", "17 yalnız bakış (anten, göz, tarama)", () => PetLabEnter("peek", 1f));
            AddPet("18 body pop", "18 gövde fırlaması", () => PetLabEnter("pop", 1f));
            AddPet("19 request reveal", "19 istek tabakları", () => PetLabEnter("reveal", 1f));
            AddPet("20 entrance 0.5x", "20 giriş 0.5x", () => PetLabEnter(null, 0.5f));

            // --- DRAG ---
            AddPet("21 card far (eyes only)", "21 kart uzak (yalnız gözler)", () => PetLabDrag(5.2f, 5.2f, true, 2.4f, false));
            AddPet("22 card medium distance (head + 2 px lean)", "22 kart orta mesafe (baş + 2 px)", () => PetLabDrag(3.2f, 3.2f, true, 2.4f, false));
            AddPet("23 card near (eyes big, paws up, mouth 1/3)", "23 kart yakın (gözler büyük, patiler, ağız)", () => PetLabDrag(1.7f, 1.7f, true, 2.4f, false));
            AddPet("24 card in the feed zone (valid)", "24 kart besleme bölgesinde (geçerli)", () => PetLabDrag(0.55f, 0.55f, true, 2.6f, false));
            AddPet("25 wrong card (and let go)", "25 yanlış kart (ve bırakılır)", () => PetLabDrag(1.4f, 0.5f, false, 2.4f, true));
            AddPet("26 eye tracking (circling)", "26 göz takibi (dairesel)", () => PetLabDragCircle());
            AddPet("27 mouth response (far -> zone -> far)", "27 ağız tepkisi (uzak -> bölge -> uzak)", () => PetLabDrag(5.5f, 0.4f, true, 4.2f, false, true));

            // --- FEED ---
            AddPet("28 low-value card feed", "28 düşük değerli kart besleme", () => PetLabFeed(CardValueTier.Low, null, 0));
            AddPet("29 medium-value card feed", "29 orta değerli kart besleme", () => PetLabFeed(CardValueTier.Medium, null, 0));
            AddPet("30 high-value card feed", "30 yüksek değerli kart besleme", () => PetLabFeed(CardValueTier.High, null, 0));
            AddPet("31 grab only", "31 yalnız kavrama", () => PetLabFeed(CardValueTier.Medium, "grab", 0));
            AddPet("32 first bite", "32 ilk ısırık", () => PetLabFeed(CardValueTier.Medium, "first bite", 0));
            AddPet("33 chew only", "33 yalnız çiğneme", () => PetLabFeed(CardValueTier.Medium, "chew", 0));
            AddPet("34 gulp only", "34 yalnız yutkunma", () => PetLabFeed(CardValueTier.Medium, "gulp", 0));
            AddPet("35 satisfaction", "35 memnuniyet", () => PetLabFeed(CardValueTier.High, "satisfaction", 0));
            AddPet("36 request slot completion", "36 tabak tamamlanması",
                () => { PetLabShow(PetHungerStage.Hungry, 0.35f); PetView.LabCollapsePlate(0); });
            AddPet("37 first of two fed", "37 ikiden ilki", () => PetLabFeed(CardValueTier.Medium, null, 0));
            AddPet("38 second of two fed", "38 ikiden ikincisi", () => PetLabFeed(CardValueTier.High, null, 1));
            AddPet("39 2/2 satisfaction", "39 2/2 memnuniyet", () => PetLabFeed(CardValueTier.Medium, "full", 1));
            AddPet("40 empty hand slot aftermath", "40 boş el slotu sonrası", () => PetLabHandGap());

            // --- HUNGER ---
            AddPetStage("41 calm", "41 sakin", PetHungerStage.Calm, 0.1f);
            AddPetStage("42 hungry", "42 aç", PetHungerStage.Hungry, 0.4f);
            AddPetStage("43 impatient", "43 sabırsız", PetHungerStage.Impatient, 0.62f);
            AddPetStage("44 angry", "44 öfkeli", PetHungerStage.Angry, 0.86f);
            AddPet("45 furious prewarning", "45 öfke ön uyarısı", () =>
            {
                PetLabShow(PetHungerStage.Angry, 1f, deadlineNext: true);
                PetView.PlayPrewarning();
            });
            AddPet("46 patience ring full", "46 sabır halkası dolu", () => { PetLabShow(PetHungerStage.Calm, 0f); PetView.IdlesEnabled = false; });
            AddPet("47 patience ring half", "47 sabır halkası yarım", () => { PetLabShow(PetHungerStage.Hungry, 0.5f); PetView.IdlesEnabled = false; });
            AddPet("48 patience ring nearly empty", "48 sabır halkası neredeyse boş", () => { PetLabShow(PetHungerStage.Angry, 0.93f); PetView.IdlesEnabled = false; });

            // --- FURY ---
            AddPet("49 full furious transition", "49 tam öfke geçişi", () => PetLabFury(null, PetPunishKind.Board));
            AddPet("50 eye change", "50 göz değişimi", () => PetLabFury("eye change", PetPunishKind.Board));
            AddPet("51 body compression", "51 gövde sıkışması", () => PetLabFury("compression", PetPunishKind.Board));
            AddPet("52 fury burst", "52 öfke patlaması", () => PetLabFury("burst", PetPunishKind.Board));
            AddPet("53 request destruction", "53 tabakların ısırılması", () => PetLabFury("request destruction", PetPunishKind.Board));
            AddPet("54 furious idle (library on)", "54 öfkeli bekleme (kütüphane açık)", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.LabNextIdle(); });
            AddPetIdle("55 furious side-eye", "55 öfkeli yan bakış", TamagotchiView.IdleKind.SideEye, PetHungerStage.Furious);
            AddPetIdle("+ angry pant", "+ öfkeli soluma", TamagotchiView.IdleKind.AngryPant, PetHungerStage.Furious);
            AddPetIdle("+ paw flex", "+ pati germe", TamagotchiView.IdleKind.PawFlex, PetHungerStage.Furious);
            AddPetIdle("+ toe tap", "+ ayak vurma", TamagotchiView.IdleKind.ToeTap, PetHungerStage.Furious);
            AddPetIdle("+ board glance", "+ tahtaya bakış", TamagotchiView.IdleKind.BoardGlance, PetHungerStage.Furious);
            AddPetIdle("+ joker bar glance", "+ joker barına bakış", TamagotchiView.IdleKind.JokerGlance, PetHungerStage.Furious);
            AddPetIdle("+ deck glance", "+ desteye bakış", TamagotchiView.IdleKind.DeckGlance, PetHungerStage.Furious);
            AddPetIdle("+ mouth wipe", "+ ağız silme", TamagotchiView.IdleKind.MouthWipe, PetHungerStage.Furious);

            // --- BOARD EAT ---
            AddPet("56 single cell target", "56 tek hücre", () => PetLabBoard(PetLabCells(1), null, false));
            AddPet("57 2-cell target", "57 2 hücre", () => PetLabBoard(PetLabCells(2), null, false));
            AddPet("58 3-cell target", "58 3 hücre", () => PetLabBoard(PetLabCells(3), null, false));
            AddPet("59 irregular region", "59 düzensiz bölge", () => PetLabBoard(PetLabIrregular(), null, false));
            AddPet("60 target telegraph", "60 hedef işareti", () => PetLabBoard(PetLabCells(3), "telegraph", false));
            AddPet("61 lunge", "61 atılma", () => PetLabBoard(PetLabCells(3), "lunge", false));
            AddPet("62 bite", "62 ısırık", () => PetLabBoard(PetLabCells(3), "bite", false));
            AddPet("63 ground chunk pull", "63 zemin parçaları", () => PetLabBoard(PetLabCells(3), "chunks", false));
            AddPet("64 chew", "64 çiğneme", () => PetLabBoard(PetLabCells(3), "chew", false));
            AddPet("65 board new-edge aftermath", "65 yeni kenar sonrası", () => PetLabBoard(PetLabCells(3), "aftermath", false));
            AddPet("66 full board-eat sequence (with the fury)", "66 tam tahta yeme (öfkeyle)", () => PetLabBoard(PetLabCells(3), null, true));

            // --- JOKER EAT ---
            AddPet("67 common joker", "67 sıradan joker", () => PetLabAsset(true, Rarity.Common, null));
            AddPet("68 rare joker", "68 nadir joker", () => PetLabAsset(true, Rarity.Rare, null));
            AddPet("69 legendary joker", "69 efsanevi joker", () => PetLabAsset(true, Rarity.Legendary, null));
            AddPet("70 target lock", "70 hedefe kilitlenme", () => PetLabAsset(true, Rarity.Rare, "lock"));
            AddPet("71 tongue snatch", "71 dille kapma", () => PetLabAsset(true, Rarity.Common, "reach"));
            AddPet("72 paw snatch", "72 patiyle kapma", () => PetLabAsset(true, Rarity.Legendary, "reach"));
            AddPet("73 first bite", "73 ilk ısırık", () => PetLabAsset(true, Rarity.Legendary, "first bite"));
            AddPet("74 second bite", "74 ikinci ısırık", () => PetLabAsset(true, Rarity.Legendary, "second bite"));
            AddPet("75 permanent empty slot", "75 kalıcı boş slot", () => PetLabAsset(true, Rarity.Rare, "residue"));
            AddPet("76 full sequence", "76 tam dizi", () => PetLabAsset(true, Rarity.Legendary, null));

            // --- POWER EAT ---
            AddPet("77 power: low value", "77 güç: düşük değer", () => PetLabAsset(false, Rarity.Common, null));
            AddPet("78 power: high value", "78 güç: yüksek değer", () => PetLabAsset(false, Rarity.Legendary, null));
            AddPet("79 power: full sequence (with the fury)", "79 güç: tam dizi (öfkeyle)", () => PetLabAsset(false, Rarity.Rare, null, true));

            // --- PILE EAT ---
            AddPet("80 draw pile low-value x5", "80 çekme destesi düşük x5", () => PetLabPile(true, 5, 0, null));
            AddPet("81 discard low-value x5", "81 ıskarta düşük x5", () => PetLabPile(false, 5, 0, null));
            AddPet("82 high-value x1", "82 yüksek değer x1", () => PetLabPile(true, 0, 1, null));
            AddPet("83 mixed x3", "83 karışık x3", () => PetLabPile(true, 2, 1, null, true));
            AddPet("84 pile thinning (x8, slowed)", "84 destenin incelmesi (x8, yavaş)", () => PetLabPile(true, 8, 0, null, false, 0.5f));
            AddPet("85 rapid snack cadence (x12, stream)", "85 hızlı atıştırma (x12, akış)", () => PetLabPile(true, 12, 0, null));
            AddPet("86 full pile-eat sequence (with the fury)", "86 tam deste yeme (öfkeyle)", () => PetLabPile(true, 5, 0, null, false, 1f, true));

            // --- POST ATTACK ---
            AddPet("87 smug idle", "87 kendini beğenmiş bekleme", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.LabSmug(1.4f); });
            AddPet("88 belly pat (after a pile)", "88 karın pışpışlama (deste sonrası)", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.LabPostAttack(PetPunishKind.DrawPile); });
            AddPet("89 tongue wipe (after a joker)", "89 dil silme (joker sonrası)", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.LabPostAttack(PetPunishKind.Joker); });
            AddPet("+ cheeks full (after the board)", "+ yanaklar dolu (tahta sonrası)", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.LabPostAttack(PetPunishKind.Board); });

            // --- EXIT ---
            AddPet("90 satisfied exit", "90 doymuş çıkış", () => { PetLabShow(PetHungerStage.Satisfied, 0f, fed: 3); PetView.Leave(); });
            AddPet("91 furious exit", "91 öfkeli çıkış", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.Leave(); });

            // --- STRESS ---
            AddPet("92 many UI assets (crowded edges)", "92 çok UI öğesi (kalabalık kenar)", () => PetLabCrowded());
            AddPet("93 small screen (phone layout - F6 back)", "93 küçük ekran (telefon - F6 ile dön)", () => PetLabLayout(UiShape.Portrait));
            AddPet("94 large screen (desktop layout)", "94 büyük ekran (masaüstü)", () => PetLabLayout(UiShape.Desktop));
            AddPet("95 left-side home", "95 sol ev", () => { petLabSide = -1; PetLabShow(PetHungerStage.Hungry, 0.4f); TamagotchiDebug.ShowTamagotchiHomeAnchor = true; });
            AddPet("96 right-side home", "96 sağ ev", () => { petLabSide = 1; PetLabShow(PetHungerStage.Hungry, 0.4f); TamagotchiDebug.ShowTamagotchiHomeAnchor = true; });
            AddPet("97 busy board (bite)", "97 dolu tahta (ısırık)", () => PetLabBoard(PetLabCells(3), null, false, 0.8f));
            AddPet("98 empty board (bite)", "98 boş tahta (ısırık)", () => PetLabBoard(PetLabCells(3), null, false, 0f));

            // --- DEBUG (lab only) ---
            AddPetToggle("ShowTamagotchiHomeAnchor", () => TamagotchiDebug.ShowTamagotchiHomeAnchor = !TamagotchiDebug.ShowTamagotchiHomeAnchor);
            AddPetToggle("ShowTamagotchiDragDistance", () => TamagotchiDebug.ShowTamagotchiDragDistance = !TamagotchiDebug.ShowTamagotchiDragDistance);
            AddPetToggle("ShowFeedZone", () => TamagotchiDebug.ShowFeedZone = !TamagotchiDebug.ShowFeedZone);
            AddPetToggle("ShowEyeTarget", () => TamagotchiDebug.ShowEyeTarget = !TamagotchiDebug.ShowEyeTarget);
            AddPetToggle("ShowRequestSlots", () => TamagotchiDebug.ShowRequestSlots = !TamagotchiDebug.ShowRequestSlots);
            AddPetToggle("ShowHungerState", () => TamagotchiDebug.ShowHungerState = !TamagotchiDebug.ShowHungerState);
            AddPetToggle("ShowPatienceProgress", () => TamagotchiDebug.ShowPatienceProgress = !TamagotchiDebug.ShowPatienceProgress);
            AddPetToggle("ShowCurrentExpression", () => TamagotchiDebug.ShowCurrentExpression = !TamagotchiDebug.ShowCurrentExpression);
            AddPetToggle("ShowCurrentAnimationState", () => TamagotchiDebug.ShowCurrentAnimationState = !TamagotchiDebug.ShowCurrentAnimationState);
            AddPetToggle("ShowPunishType", () => TamagotchiDebug.ShowPunishType = !TamagotchiDebug.ShowPunishType);
            AddPetToggle("ShowBoardEatCandidates", () => TamagotchiDebug.ShowBoardEatCandidates = !TamagotchiDebug.ShowBoardEatCandidates);
            AddPetToggle("ShowBoardEatScores", () => TamagotchiDebug.ShowBoardEatScores = !TamagotchiDebug.ShowBoardEatScores);
            AddPetToggle("ShowRejectedHardLockCandidates", () => TamagotchiDebug.ShowRejectedHardLockCandidates = !TamagotchiDebug.ShowRejectedHardLockCandidates);
            AddPetToggle("ShowChosenBoardEatCells", () => TamagotchiDebug.ShowChosenBoardEatCells = !TamagotchiDebug.ShowChosenBoardEatCells);
            AddPetToggle("ShowJokerEatCandidateValues", () => TamagotchiDebug.ShowJokerEatCandidateValues = !TamagotchiDebug.ShowJokerEatCandidateValues);
            AddPetToggle("ShowPowerEatCandidateValues", () => TamagotchiDebug.ShowPowerEatCandidateValues = !TamagotchiDebug.ShowPowerEatCandidateValues);
            AddPetToggle("ShowPileEatCandidates", () => TamagotchiDebug.ShowPileEatCandidates = !TamagotchiDebug.ShowPileEatCandidates);
            AddPetToggle("ShowCardValueTier", () => TamagotchiDebug.ShowCardValueTier = !TamagotchiDebug.ShowCardValueTier);
            AddPetToggle("ShowFoodProxyBounds", () => TamagotchiDebug.ShowFoodProxyBounds = !TamagotchiDebug.ShowFoodProxyBounds);
            AddPetToggle("ShowBiteMask", () => TamagotchiDebug.ShowBiteMask = !TamagotchiDebug.ShowBiteMask);
            AddPetToggle("ShowPresentationQueue", () => TamagotchiDebug.ShowPresentationQueue = !TamagotchiDebug.ShowPresentationQueue);
            AddPetToggle("debug punish view", () => TamagotchiDebug.ShowPunishView = !TamagotchiDebug.ShowPunishView);
            AddAnim("debug: all off", "hata ayıklama: hepsi kapalı", () => TamagotchiDebug.AllOff());
            AddAnim("quality: High / Medium / Low (cycles)", "kalite: Yüksek / Orta / Düşük (döngü)", () =>
            {
                TamagotchiView v = PetView;
                v.Lod = v.Lod == PetLod.High ? PetLod.Medium : v.Lod == PetLod.Medium ? PetLod.Low : PetLod.High;
                animLastLabel = "LOD " + v.Lod;
            });
            AddAnim("pet slow motion: 1x / 0.5x / 0.25x (cycles)", "pet yavaş çekim: 1x / 0.5x / 0.25x (döngü)", () =>
            {
                TamagotchiView v = PetView;
                v.PlaybackRate = v.PlaybackRate > 0.9f ? 0.5f : v.PlaybackRate > 0.4f ? 0.25f : 1f;
                animLastLabel = "pet " + v.PlaybackRate + "x";
            });
            AddAnim("pet: give it back to the round", "pet: rauna geri ver", () => StopPetLab());
        }

        private void AddPet(string en, string tr, System.Action play)
        {
            AddAnim("tamagotchi: " + en, "tamagotchi: " + tr, () =>
            {
                PetLabBegin();
                play();
            });
        }

        private void AddPetIdle(string en, string tr, TamagotchiView.IdleKind kind, PetHungerStage stage)
        {
            AddPet(en, tr, () =>
            {
                PetLabShow(stage, stage == PetHungerStage.Furious ? 1f : stage == PetHungerStage.Satisfied ? 0f : 0.3f,
                    fed: stage == PetHungerStage.Satisfied ? 3 : 0);
                PetView.IdlesEnabled = false;
                PetView.PlayIdle(kind);
            });
        }

        private void AddPetStage(string en, string tr, PetHungerStage stage, float progress)
        {
            AddPet(en, tr, () =>
            {
                PetHungerStage previous = stage == PetHungerStage.Calm ? PetHungerStage.Calm : stage - 1;
                PetLabShow(stage, progress);
                if (stage != PetHungerStage.Calm)
                {
                    PetView.PlayHungerChange(new TamagotchiHungerChange { Previous = previous, Stage = stage, Progress = progress });
                }
            });
        }

        private void AddPetToggle(string name, System.Action flip)
        {
            AddAnim("tamagotchi debug: " + name, "tamagotchi hata ayıklama: " + name, () =>
            {
                flip();
                animLastLabel = name;
            });
        }

        // ================================================================== setup

        /// <summary>Every scene starts here: the pet is the lab's, isolation and life masks are
        /// cleared, the anchors are the screen's.</summary>
        private void PetLabBegin()
        {
            if (petLabRoutine != null)
            {
                StopCoroutine(petLabRoutine);
                petLabRoutine = null;
            }
            if (petLabCard != null)
            {
                Destroy(petLabCard.gameObject);
                petLabCard = null;
            }
            petLabDriving = true;
            TamagotchiView v = PetView;
            v.LabClear();
            v.ClearDrag();
            cardLayer.ClearPetGap();
            jokerBar.SetPresentationAlpha(1f);
            powerBar.SetPresentationAlpha(1f);
            RememberPetAnchors();
        }

        /// <summary>Gives the pet back to the round (RESET, closing the lab).</summary>
        private void StopPetLab()
        {
            if (petLabRoutine != null)
            {
                StopCoroutine(petLabRoutine);
                petLabRoutine = null;
            }
            if (petLabCard != null)
            {
                Destroy(petLabCard.gameObject);
                petLabCard = null;
            }
            petLabExtraObstacles.Clear();
            petLabSide = 0;
            if (petView != null)
            {
                petView.HideNow();
                petView.LabClear();
            }
            if (petLabLayoutForced)
            {
                UiLayout.Override = petLabLayoutBefore;
                petLabLayoutForced = false;
                WatchScreenShape();
            }
            petLabDriving = false;
            petRound = null;   // the round's own pet comes back in on the next frame
            if (cardLayer != null)
            {
                cardLayer.ClearPetGap();
                cardLayer.SetPileOverride(true, null);
                cardLayer.SetPileOverride(false, null);
            }
            jokerBar.SetPresentationAlpha(1f);
            powerBar.SetPresentationAlpha(1f);
            boardView.SetImpulse(Vector2.zero);
        }

        /// <summary>The pet out with two requests, at a stage. <paramref name="fed"/> is a mask of
        /// fed slots (1 = the first, 2 = the second).</summary>
        private void PetLabShow(PetHungerStage stage, float progress, int fed = 0, bool deadlineNext = false)
        {
            TamagotchiView v = PetView;
            v.Home = SolvePetHome(petLabSide, TamagotchiView.Tuning.BodyScale);
            v.LabShowNow(PetLabState(stage, progress, fed, deadlineNext));
        }

        private void PetLabEnter(string beat, float rate)
        {
            TamagotchiView v = PetView;
            v.Home = SolvePetHome(petLabSide, TamagotchiView.Tuning.BodyScale);
            v.PlaybackRate = rate;
            if (beat != null)
            {
                v.LabIsolate(beat);
            }
            v.LabEnter(PetLabState(PetHungerStage.Calm, 0f, 0, false));
        }

        /// <summary>The scratch boss whose TierOf is asked - the rules' definition of a card's
        /// worth, read against the round as it stands.</summary>
        private TamagotchiBoss petLabJudge;

        private CardValueTier PetLabTier(BlockCard card)
        {
            if (petLabJudge == null)
            {
                petLabJudge = new TamagotchiBoss();
            }
            return session != null && session.CurrentRound != null ? petLabJudge.TierOf(card, session.CurrentRound)
                : CardValueTier.Medium;
        }

        /// <summary>The cards alive in the round, hand first - what the lab's requests are made of.</summary>
        private List<BlockCard> PetLabCards()
        {
            var cards = new List<BlockCard>();
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round != null)
            {
                for (int i = 0; i < round.Hand.Count; i++)
                {
                    cards.Add(round.Hand[i]);
                }
                cards.AddRange(round.Deck.DrawPile);
                cards.AddRange(round.Deck.DiscardPile);
            }
            if (cards.Count == 0 && session != null)
            {
                cards.AddRange(session.OwnedCards);
            }
            return cards;
        }

        /// <summary>A card of the wanted tier (or the closest there is).</summary>
        private BlockCard PetLabCardOfTier(CardValueTier tier, int skip)
        {
            List<BlockCard> cards = PetLabCards();
            BlockCard fallback = null;
            foreach (BlockCard c in cards)
            {
                if (PetLabTier(c) == tier)
                {
                    if (skip-- <= 0)
                    {
                        return c;
                    }
                }
                if (fallback == null)
                {
                    fallback = c;
                }
            }
            return fallback;
        }

        private TamagotchiRoundVisualState PetLabState(PetHungerStage stage, float progress, int fed, bool deadlineNext)
        {
            var s = new TamagotchiRoundVisualState { BossActive = true, Stage = stage, Progress = progress, DeadlineNext = deadlineNext };
            BlockCard a = PetLabCardOfTier(CardValueTier.High, 0) ?? PetLabCardOfTier(CardValueTier.Medium, 0);
            BlockCard b = PetLabCardOfTier(CardValueTier.Medium, a != null && PetLabTier(a) == CardValueTier.Medium ? 1 : 0);
            if (b == a)
            {
                b = PetLabCardOfTier(CardValueTier.Low, 0);
            }
            s.Requests.Add(new PetRequest { SlotId = 0, Card = a, Tier = a != null ? PetLabTier(a) : CardValueTier.Medium, Fed = (fed & 1) != 0 });
            s.Requests.Add(new PetRequest { SlotId = 1, Card = b, Tier = b != null ? PetLabTier(b) : CardValueTier.Medium, Fed = (fed & 2) != 0 });
            s.Seed = 1234567u;
            return s;
        }

        // ================================================================== drag

        /// <summary>A lab card carried from <paramref name="from"/> to <paramref name="to"/> feed-zone
        /// radii away (along the line from the pet to the hand), held there, and let go.</summary>
        private void PetLabDrag(float from, float to, bool food, float seconds, bool drop, bool andBack = false)
        {
            PetLabShow(PetHungerStage.Hungry, 0.4f);
            PetView.IdlesEnabled = false;
            petLabRoutine = StartCoroutine(PetLabDragRoutine(from, to, food, seconds, drop, andBack));
        }

        private IEnumerator PetLabDragRoutine(float from, float to, bool food, float seconds, bool drop, bool andBack)
        {
            TamagotchiView v = PetView;
            yield return null;
            BlockCard card = food ? v.State.Requests[0].Card : PetLabCardOfTier(CardValueTier.Low, 2);
            petLabCard = CardVisual.Create(transform, "PetLabCard", card, true, false, Vector2.zero, CardLayerView.HandFrontOrder);
            petLabCard.SetAlpha(0.6f);
            Vector2 centre = v.FeedZoneCentre;
            Vector2 dir = ((Vector2)PetView.Anchors.HandCentre - centre).normalized;
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = Vector2.left;
            }
            float r = v.FeedZoneRadius;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / (seconds * 0.45f));
                float d = andBack ? Mathf.Lerp(from, to, Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI))
                    : Mathf.Lerp(from, to, k * k * (3f - 2f * k));
                Vector2 at = centre + dir * d * r;
                petLabCard.transform.position = at;
                v.SetDrag(true, at, card != null ? card.Id : 0, food);
                yield return null;
            }
            Vector2 end = petLabCard.transform.position;
            v.SetDrag(false, end, 0, false);
            if (drop && !food)
            {
                v.PlayWrongFood(end);
            }
            Destroy(petLabCard.gameObject);
            petLabCard = null;
            petLabRoutine = null;
        }

        private void PetLabDragCircle()
        {
            PetLabShow(PetHungerStage.Hungry, 0.4f);
            PetView.IdlesEnabled = false;
            petLabRoutine = StartCoroutine(PetLabCircleRoutine());
        }

        private IEnumerator PetLabCircleRoutine()
        {
            TamagotchiView v = PetView;
            yield return null;
            BlockCard card = v.State.Requests[0].Card;
            petLabCard = CardVisual.Create(transform, "PetLabCard", card, true, false, Vector2.zero, CardLayerView.HandFrontOrder);
            petLabCard.SetAlpha(0.6f);
            Vector2 centre = v.FeedZoneCentre + new Vector2(-v.Home.Facing * -2.6f * v.FeedZoneRadius, 0.6f);
            float t = 0f;
            while (t < 4.5f)
            {
                t += Time.deltaTime;
                Vector2 at = centre + new Vector2(Mathf.Cos(t * 1.6f), Mathf.Sin(t * 1.6f)) * 2.1f;
                petLabCard.transform.position = at;
                v.SetDrag(true, at, card != null ? card.Id : 0, true);
                yield return null;
            }
            v.SetDrag(false, petLabCard.transform.position, 0, false);
            Destroy(petLabCard.gameObject);
            petLabCard = null;
            petLabRoutine = null;
        }

        // ================================================================== feed

        private void PetLabFeed(CardValueTier tier, string beat, int slot)
        {
            int fed = slot == 1 ? 1 : 0;
            PetLabShow(PetHungerStage.Hungry, 0.35f, fed);
            TamagotchiView v = PetView;
            v.IdlesEnabled = false;
            if (beat != null)
            {
                v.LabIsolate(beat);
            }
            BlockCard card = v.State.Requests[slot].Card ?? PetLabCardOfTier(tier, 0);
            var meal = new TamagotchiFeedVisuals
            {
                RequestSlotId = slot,
                Card = card,
                Tier = tier,
                HandSlotIndex = 0,
                RequestsRemaining = slot == 0 ? 1 : 0
            };
            CardVisual from = cardLayer.VisualOfSlot(Mathf.Min(1, Mathf.Max(0, (session.CurrentRound != null ? session.CurrentRound.Hand.Count : 1) - 1)));
            Vector2 at = from != null ? (Vector2)from.transform.position : (Vector2)v.Anchors.HandCentre;
            v.PlayFeed(meal, at, CardLayerView.CardScale);
        }

        /// <summary>The hand with a card's gap held open (presentation only - the real hand is
        /// untouched) and the bite-ring left in it.</summary>
        private void PetLabHandGap()
        {
            PetLabShow(PetHungerStage.Hungry, 0.35f, 1);
            cardLayer.SetPetGap(1);
            RefreshAll(null);
            Vector2? gap = cardLayer.PetGapWorld;
            if (gap.HasValue)
            {
                PetView.SpawnHandResidue(gap.Value);
            }
        }

        // ================================================================== fury

        private void PetLabFury(string beat, PetPunishKind foreshadow)
        {
            PetLabShow(PetHungerStage.Angry, 1f);
            TamagotchiView v = PetView;
            v.IdlesEnabled = false;
            // the game's order: Core is furious from the frame it happens; the fury is told after
            var furious = PetLabState(PetHungerStage.Furious, 1f, 0, false);
            v.LabSetState(furious);
            if (beat != null)
            {
                v.LabIsolate(beat);
            }
            v.PlayFury(new TamagotchiFuryVisuals { MissingFeedCount = 2, PunishKind = foreshadow });
        }

        // ================================================================== the board

        private static List<GridPos> PetLabCells(int n)
        {
            var all = new List<GridPos> { new GridPos(3, 3), new GridPos(4, 3), new GridPos(4, 2) };
            return all.GetRange(0, Mathf.Clamp(n, 1, 3));
        }

        private static List<GridPos> PetLabIrregular()
        {
            return new List<GridPos> { new GridPos(2, 4), new GridPos(3, 4), new GridPos(3, 3), new GridPos(3, 2), new GridPos(4, 2) };
        }

        /// <summary>A 7x7 board of the lab's own, about <paramref name="density"/> full (never on
        /// the bitten cells), bitten by the rules' own MarkDead, and the bite played over it.</summary>
        private void PetLabBoard(List<GridPos> cells, string beat, bool withFury, float density = 0.42f)
        {
            List<int> cards = AnimBossCards();
            var board = new GameBoard(7, 7);
            var bitten = new HashSet<GridPos>(cells);
            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    var p = new GridPos(x, y);
                    if (bitten.Contains(p))
                    {
                        continue;
                    }
                    float roll = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);
                    if (roll < density)
                    {
                        board.SetCubeAt(p, new Cube(CubeKind.Normal, cards[(x * 3 + y) % cards.Count]));
                    }
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            boardView.Refresh();
            List<GridPos> eaten = board.MarkDeadOnLabBoard(cells);
            boardView.Refresh();
            RememberPetAnchors();

            PetLabShow(withFury ? PetHungerStage.Angry : PetHungerStage.Furious, 1f);
            TamagotchiView v = PetView;
            v.IdlesEnabled = false;
            if (beat != null)
            {
                v.LabIsolate(beat);
            }
            var report = new PetRampageVisuals { Kind = PetPunishKind.Board, Seed = 77u };
            report.Cells.AddRange(eaten);
            report.MinX = report.MaxX = eaten.Count > 0 ? eaten[0].X : 0;
            report.MinY = report.MaxY = eaten.Count > 0 ? eaten[0].Y : 0;
            foreach (GridPos c in eaten)
            {
                report.MinX = Mathf.Min(report.MinX, c.X);
                report.MaxX = Mathf.Max(report.MaxX, c.X);
                report.MinY = Mathf.Min(report.MinY, c.Y);
                report.MaxY = Mathf.Max(report.MaxY, c.Y);
            }
            PetLabCandidates(report, PetPunishKind.Board);
            // the planner's first-bite scores, drawn for the debug view: every empty cell, the
            // ones near the middle crueller (the lab cannot run the round's planner on its board)
            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    var p = new GridPos(x, y);
                    if (board.GetCube(p).HasValue || board.IsDead(p))
                    {
                        continue;
                    }
                    float room = 40f + 6f * (Mathf.Abs(x - 3) + Mathf.Abs(y - 3)) + (x * 7 + y * 3) % 5;
                    report.CellScores.Add(new PetCellScore { Cell = p, Room = room, Rejected = (x + y * 2) % 11 == 0 });
                }
            }
            if (withFury)
            {
                v.LabSetState(PetLabState(PetHungerStage.Furious, 1f, 0, false));
                v.PlayFury(new TamagotchiFuryVisuals { MissingFeedCount = 2, PunishKind = PetPunishKind.Board });
            }
            v.PreparePunish(report, null);
            v.PlayPunish(report);
        }

        /// <summary>The planner's candidates as the brief's debug view shows them.</summary>
        private static void PetLabCandidates(PetRampageVisuals report, PetPunishKind chosen)
        {
            report.Candidates.Add(new PetPunishCandidate { Kind = PetPunishKind.Board, Valid = true, Pressure = chosen == PetPunishKind.Board ? 0.78f : 0.52f, HardLockRisk = 0.12f, Label = "3 cell(s)" });
            report.Candidates.Add(new PetPunishCandidate { Kind = PetPunishKind.Joker, Valid = true, Pressure = chosen == PetPunishKind.Joker ? 0.81f : 0.64f, Label = "joker" });
            report.Candidates.Add(new PetPunishCandidate { Kind = PetPunishKind.Power, Valid = true, Pressure = chosen == PetPunishKind.Power ? 0.7f : 0.41f, Label = "power" });
            report.Candidates.Add(new PetPunishCandidate { Kind = PetPunishKind.DrawPile, Valid = true, Pressure = chosen == PetPunishKind.DrawPile ? 0.8f : 0.58f, Label = "5 card(s)" });
            report.Candidates.Add(new PetPunishCandidate { Kind = PetPunishKind.DiscardPile, Valid = true, Pressure = chosen == PetPunishKind.DiscardPile ? 0.79f : 0.33f, Label = "4 card(s)" });
        }

        // ================================================================== jokers and powers

        private void PetLabAsset(bool joker, Rarity rarity, string beat, bool withFury = false)
        {
            PetLabShow(withFury ? PetHungerStage.Angry : PetHungerStage.Furious, 1f);
            TamagotchiView v = PetView;
            v.IdlesEnabled = false;
            string defId = null;
            string name = null;
            if (joker)
            {
                foreach (JokerDefinition d in JokerRegistry.All)
                {
                    if (RarityTable.For(d.DefId) == rarity)
                    {
                        defId = d.DefId;
                        name = d.DisplayName;
                        break;
                    }
                }
            }
            else
            {
                foreach (PowerDefinition d in PowerRegistry.All)
                {
                    if (RarityTable.For(d.DefId) == rarity)
                    {
                        defId = d.DefId;
                        name = d.DisplayName;
                        break;
                    }
                }
            }
            if (defId == null)
            {
                defId = joker ? JokerRegistry.All[0].DefId : PowerRegistry.All[0].DefId;
                name = defId;
            }
            // stand in for a real panel when there is one, so the snatch leaves from a slot
            int instance = -1;
            if (joker && session.Jokers.Count > 0)
            {
                instance = session.Jokers.Jokers[session.Jokers.Count - 1].InstanceId;
            }
            else if (!joker && session.Powers.Count > 0)
            {
                instance = session.Powers.Powers[session.Powers.Count - 1].InstanceId;
            }
            var report = new PetRampageVisuals
            {
                Kind = joker ? PetPunishKind.Joker : PetPunishKind.Power,
                DefId = defId,
                EatenName = name,
                InstanceId = instance,
                Tier = rarity == Rarity.Legendary ? CardValueTier.High : rarity == Rarity.Rare ? CardValueTier.Medium : CardValueTier.Low,
                Seed = 31u
            };
            PetLabCandidates(report, report.Kind);
            if (beat != null)
            {
                v.LabIsolate(beat);
            }
            if (withFury)
            {
                v.LabSetState(PetLabState(PetHungerStage.Furious, 1f, 0, false));
                v.PlayFury(new TamagotchiFuryVisuals { MissingFeedCount = 1, PunishKind = report.Kind });
            }
            v.PreparePunish(report, null);
            v.PlayPunish(report);
        }

        // ================================================================== piles

        private void PetLabPile(bool draw, int low, int high, string beat, bool mixed = false, float rate = 1f, bool withFury = false)
        {
            PetLabShow(withFury ? PetHungerStage.Angry : PetHungerStage.Furious, 1f);
            TamagotchiView v = PetView;
            v.IdlesEnabled = false;
            v.PlaybackRate = rate;
            var report = new PetRampageVisuals { Kind = draw ? PetPunishKind.DrawPile : PetPunishKind.DiscardPile, Seed = 53u };
            List<BlockCard> source = PetLabCards();
            int made = 0;
            for (int i = 0; i < source.Count && made < low + high; i++)
            {
                BlockCard src = source[(i * 7) % source.Count];
                bool elemental = src.Elements.Count > 0;
                bool wantHigh = made < high || (mixed && made == low + high - 1);
                if (wantHigh != elemental && source.Count > low + high + 4)
                {
                    continue;
                }
                var copy = new BlockCard(-400 - made, src.Shape, src.Elements);
                report.Cards.Add(copy);
                report.CardTiers.Add(wantHigh ? CardValueTier.High : mixed && made == 0 ? CardValueTier.Medium : CardValueTier.Low);
                made++;
            }
            while (made < low + high && source.Count > 0)
            {
                BlockCard src = source[made % source.Count];
                report.Cards.Add(new BlockCard(-400 - made, src.Shape, src.Elements));
                report.CardTiers.Add(made < high ? CardValueTier.High : CardValueTier.Low);
                made++;
            }
            RoundEngine round = session.CurrentRound;
            IReadOnlyList<BlockCard> pile = round != null ? (draw ? round.Deck.DrawPile : round.Deck.DiscardPile) : null;
            report.CountAfter = pile != null ? pile.Count : 0;
            report.CountBefore = report.CountAfter + report.Cards.Count;
            PetLabCandidates(report, report.Kind);
            if (beat != null)
            {
                v.LabIsolate(beat);
            }
            if (withFury)
            {
                v.LabSetState(PetLabState(PetHungerStage.Furious, 1f, 0, false));
                v.PlayFury(new TamagotchiFuryVisuals { MissingFeedCount = 2, PunishKind = report.Kind });
            }
            v.PreparePunish(report, pile);
            v.PlayPunish(report);
        }

        // ================================================================== stress

        /// <summary>The home solved with eight more joker panels down the right edge and four more
        /// powers down the left, as a crowded run would have them.</summary>
        private void PetLabCrowded()
        {
            UiLayout layout = UiLayout.Active;
            petLabExtraObstacles.Clear();
            for (int i = 0; i < 8; i++)
            {
                float y = layout.OrthoSize - 0.3f - (i / 2) * 2.15f;
                float x = layout.HalfWidth - 0.2f - (i % 2) * 1.6f;
                petLabExtraObstacles.Add(new Rect(x - 1.5f, y - 2.05f, 1.5f, 2.05f));
            }
            for (int i = 0; i < 4; i++)
            {
                float y = layout.OrthoSize - 0.3f - i * 1.7f;
                petLabExtraObstacles.Add(new Rect(-layout.HalfWidth + 0.15f, y - 1.6f, 1.5f, 1.6f));
            }
            TamagotchiDebug.ShowTamagotchiHomeAnchor = true;
            PetLabShow(PetHungerStage.Hungry, 0.4f);
            petLabExtraObstacles.Clear();
        }

        private void PetLabLayout(UiShape shape)
        {
            if (!petLabLayoutForced)
            {
                petLabLayoutBefore = UiLayout.Override;
                petLabLayoutForced = true;
            }
            UiLayout.Override = shape;
            WatchScreenShape();
            TamagotchiDebug.ShowTamagotchiHomeAnchor = true;
            RememberPetAnchors();
            PetLabShow(PetHungerStage.Hungry, 0.4f);
        }
    }
}
