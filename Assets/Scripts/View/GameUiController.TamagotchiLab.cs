// PURPOSE: "Tamagotchi" in the ANIMATION LAB (F3) - the brief's section "TAMAGOTCHI / BOSS": 98
// scenes (character, entrance, drag, feed, hunger, fury, board / joker / power / pile eating, post
// attack, exit, stress), its 22 debug toggles and the punish debug view, plus LOD and slow motion -
// and the CORRECTIVE PASS's own section (c1-c38: the fury old and new and part by part, the hatred
// field's layers, roaming by style, speech, the staged eating old and new, and an audio panel that
// plays every cue and sequence without the pet), seventeen more debug toggles and a few audio knobs.
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
        private float petLabRate = 1f;       // the lab's own speed, kept from scene to scene
        private string petLabHome;           // a named place the next scene starts at (null = the solver's)
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
            AddPet("50 dead silence", "50 ölü sessizlik", () => PetLabFury("silence", PetPunishKind.Board));
            AddPet("51 disappointment", "51 hayal kırıklığı", () => PetLabFury("disappointment", PetPunishKind.Board));
            AddPet("52 face break", "52 yüzün bozulması", () => PetLabFury("face break", PetPunishKind.Board));
            AddPet("53 fury burst", "53 öfke patlaması", () => PetLabFury("burst", PetPunishKind.Board));
            AddPet("+ target snap + hold", "+ hedefe kilitlenme + bekleme", () => PetLabFury("target", PetPunishKind.Joker));
            AddPet("54 furious idle (library on)", "54 öfkeli bekleme (kütüphane açık)", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.LabNextIdle(); });
            AddPetIdle("55 furious side-eye", "55 öfkeli yan bakış", TamagotchiView.IdleKind.FurySideEye, PetHungerStage.Furious);
            AddPetIdle("+ low panting", "+ alçak soluma", TamagotchiView.IdleKind.FuryPant, PetHungerStage.Furious);
            AddPetIdle("+ jaw twitch", "+ çene seğirmesi", TamagotchiView.IdleKind.JawTwitch, PetHungerStage.Furious);
            AddPetIdle("+ short growl", "+ kısa hırlama", TamagotchiView.IdleKind.ShortGrowl, PetHungerStage.Furious);
            AddPetIdle("+ claw tap", "+ pençe tıklatma", TamagotchiView.IdleKind.ClawTap, PetHungerStage.Furious);
            AddPetIdle("+ mouth lick", "+ ağız yalama", TamagotchiView.IdleKind.FuryLick, PetHungerStage.Furious);
            AddPetIdle("+ head snap", "+ baş çevirme", TamagotchiView.IdleKind.FuryHeadSnap, PetHungerStage.Furious);
            AddPetIdle("+ hunched breathing", "+ kambur soluma", TamagotchiView.IdleKind.HunchBreath, PetHungerStage.Furious);
            AddPetIdle("+ one eye squint", "+ tek göz kısma", TamagotchiView.IdleKind.EyeSquint, PetHungerStage.Furious);
            AddPetIdle("+ tension release", "+ gerginlik boşalması", TamagotchiView.IdleKind.TensionRelease, PetHungerStage.Furious);
            AddPetIdle("+ teeth clack", "+ diş takırtısı", TamagotchiView.IdleKind.TeethClack, PetHungerStage.Furious);
            AddPetIdle("+ screen-edge claw grip", "+ ekran kenarını pençeleme", TamagotchiView.IdleKind.ClawGrip, PetHungerStage.Furious);

            // --- BOARD EAT ---
            AddPet("56 single cell target", "56 tek hücre", () => PetLabBoard(PetLabCells(1), null, false));
            AddPet("57 2-cell target", "57 2 hücre", () => PetLabBoard(PetLabCells(2), null, false));
            AddPet("58 3-cell target", "58 3 hücre", () => PetLabBoard(PetLabCells(3), null, false));
            AddPet("59 irregular region", "59 düzensiz bölge", () => PetLabBoard(PetLabIrregular(), null, false));
            AddPet("60 target telegraph", "60 hedef işareti", () => PetLabBoard(PetLabCells(3), "telegraph", false));
            AddPet("+ pre-bite suction", "+ ısırık öncesi emme", () => PetLabBoard(PetLabCells(3), "suction", false));
            AddPet("61 lunge", "61 atılma", () => PetLabBoard(PetLabCells(3), "lunge", false));
            AddPet("62 bite", "62 ısırık", () => PetLabBoard(PetLabCells(3), "bite", false));
            AddPet("63 ground chunk pull", "63 zemin parçaları", () => PetLabBoard(PetLabCells(3), "chunks", false));
            AddPet("64 chew", "64 çiğneme", () => PetLabBoard(PetLabCells(3), "chew", false));
            AddPet("+ gulp (over the board)", "+ yutkunma (tahtanın üstünde)", () => PetLabBoard(PetLabCells(3), "gulp", false));
            AddPet("65 new board reveal + bite scar", "65 yeni tahta + ısırık izi", () => PetLabBoard(PetLabCells(3), "reveal", false));
            AddPet("66 full board-eat sequence (with the fury)", "66 tam tahta yeme (öfkeyle)", () => PetLabBoard(PetLabCells(3), null, true));

            // --- JOKER EAT ---
            AddPet("67 common joker", "67 sıradan joker", () => PetLabAsset(true, Rarity.Common, null));
            AddPet("68 rare joker", "68 nadir joker", () => PetLabAsset(true, Rarity.Rare, null));
            AddPet("69 legendary joker", "69 efsanevi joker", () => PetLabAsset(true, Rarity.Legendary, null));
            AddPet("70 target lock", "70 hedefe kilitlenme", () => PetLabAsset(true, Rarity.Rare, "lock"));
            AddPet("71 threat (mouth opens, the card wobbles)", "71 tehdit (ağız açılır, kart titrer)", () => PetLabAsset(true, Rarity.Rare, "threat"));
            AddPet("72 paw grab", "72 patiyle yakalama", () => PetLabAsset(true, Rarity.Legendary, "grab"));
            AddPet("+ struggle (two tugs)", "+ çekişme (iki asılma)", () => PetLabAsset(true, Rarity.Rare, "struggle"));
            AddPet("+ pull (out of the slot)", "+ çekiş (yuvadan çıkış)", () => PetLabAsset(true, Rarity.Rare, "pull"));
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

            AddTamagotchiCorrectiveLab();

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
            // the corrective pass
            AddPetToggle("ShowRoamingCandidates", () => TamagotchiDebug.ShowRoamingCandidates = !TamagotchiDebug.ShowRoamingCandidates);
            AddPetToggle("ShowChosenRoamingAnchor", () => TamagotchiDebug.ShowChosenRoamingAnchor = !TamagotchiDebug.ShowChosenRoamingAnchor);
            AddPetToggle("ShowSpeechBubbleAnchor", () => TamagotchiDebug.ShowSpeechBubbleAnchor = !TamagotchiDebug.ShowSpeechBubbleAnchor);
            AddPetToggle("ShowFuryFaceRig", () => TamagotchiDebug.ShowFuryFaceRig = !TamagotchiDebug.ShowFuryFaceRig);
            AddPetToggle("ShowFuryBodyDeform", () => TamagotchiDebug.ShowFuryBodyDeform = !TamagotchiDebug.ShowFuryBodyDeform);
            AddPetToggle("ShowHatredField", () => TamagotchiDebug.ShowHatredField = !TamagotchiDebug.ShowHatredField);
            AddPetToggle("ShowHatredWaveProgress", () => TamagotchiDebug.ShowHatredWaveProgress = !TamagotchiDebug.ShowHatredWaveProgress);
            AddPetToggle("ShowScreenVignette", () => TamagotchiDebug.ShowScreenVignette = !TamagotchiDebug.ShowScreenVignette);
            AddPetToggle("ShowChromaticEdge", () => TamagotchiDebug.ShowChromaticEdge = !TamagotchiDebug.ShowChromaticEdge);
            AddPetToggle("ShowCurrentAudioEvent", () => TamagotchiDebug.ShowCurrentAudioEvent = !TamagotchiDebug.ShowCurrentAudioEvent);
            AddPetToggle("ShowAudioLayerNames", () => TamagotchiDebug.ShowAudioLayerNames = !TamagotchiDebug.ShowAudioLayerNames);
            AddPetToggle("ShowAudioDuckAmount", () => TamagotchiDebug.ShowAudioDuckAmount = !TamagotchiDebug.ShowAudioDuckAmount);
            AddPetToggle("ShowPunishPhase", () => TamagotchiDebug.ShowPunishPhase = !TamagotchiDebug.ShowPunishPhase);
            AddPetToggle("ShowEatingProxy", () => TamagotchiDebug.ShowEatingProxy = !TamagotchiDebug.ShowEatingProxy);
            AddPetToggle("ShowBiteProgress", () => TamagotchiDebug.ShowBiteProgress = !TamagotchiDebug.ShowBiteProgress);
            AddPetToggle("ShowBoardChunkPath", () => TamagotchiDebug.ShowBoardChunkPath = !TamagotchiDebug.ShowBoardChunkPath);
            AddPetToggle("ShowAssetPullPath", () => TamagotchiDebug.ShowAssetPullPath = !TamagotchiDebug.ShowAssetPullPath);
            AddAnim("debug: all off", "hata ayıklama: hepsi kapalı", () => TamagotchiDebug.AllOff());
            AddAnim("quality: High / Medium / Low (cycles)", "kalite: Yüksek / Orta / Düşük (döngü)", () =>
            {
                TamagotchiView v = PetView;
                v.Lod = v.Lod == PetLod.High ? PetLod.Medium : v.Lod == PetLod.Medium ? PetLod.Low : PetLod.High;
                animLastLabel = "LOD " + v.Lod;
            });
            AddAnim("pet speed: 1x / 0.75x / 0.5x / 0.25x (cycles, kept by the next scene)", "pet hızı: 1x / 0.75x / 0.5x / 0.25x (döngü, sonraki sahnede kalır)", () =>
            {
                petLabRate = petLabRate > 0.9f ? 0.75f : petLabRate > 0.7f ? 0.5f : petLabRate > 0.4f ? 0.25f : 1f;
                PetView.PlaybackRate = petLabRate;
                animLastLabel = "pet " + petLabRate + "x (audio plays at 1x only)";
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
            v.PlaybackRate = petLabRate;
            TamagotchiHatredField.Layers.AllOn();
            jokerBar.SetHeldGap(-1);
            powerBar.SetHeldGap(-1);
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
            jokerBar.SetHeldGap(-1);
            powerBar.SetHeldGap(-1);
            boardView.SetImpulse(Vector2.zero);
            if (background != null)
            {
                background.SetMood("tamagotchi", null);
            }
        }

        /// <summary>The pet out with two requests, at a stage. <paramref name="fed"/> is a mask of
        /// fed slots (1 = the first, 2 = the second).</summary>
        private void PetLabShow(PetHungerStage stage, float progress, int fed = 0, bool deadlineNext = false)
        {
            TamagotchiView v = PetView;
            v.Home = PetLabHomeNamed(petLabHome) ?? SolvePetHome(petLabSide, TamagotchiView.Tuning.BodyScale);
            petLabHome = null;
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
            // it stands in the NEXT free slot of its bar: the gap the pet holds open is then past
            // the last real panel and nothing real is pushed aside
            report.InstanceId = -7700 - (joker ? 0 : 1);
            int key = joker ? report.InstanceId : report.InstanceId + TamagotchiView.PowerMemoryKey;
            Rect? next = PetLabNextSlot(joker);
            if (next.HasValue)
            {
                v.Anchors.PanelMemory[key] = next.Value;
                v.Anchors.PanelIndexMemory[key] = joker ? session.Jokers.Count : session.Powers.Count;
            }
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

        /// <summary>The world rect of the slot AFTER the last panel of a bar (where a new joker or
        /// power would appear), or null when the bar is empty or off screen.</summary>
        private Rect? PetLabNextSlot(bool joker)
        {
            int count = joker ? session.Jokers.Count : session.Powers.Count;
            if (count <= 0 || cam == null)
            {
                return null;
            }
            Vector2? last = joker ? jokerBar.PanelScreenCenter(count - 1) : powerBar.PanelScreenCenter(count - 1);
            if (!last.HasValue)
            {
                return null;
            }
            UiLayout layout = UiLayout.Active;
            float screenPerCanvas = Screen.width / Mathf.Max(1f, layout.CanvasReference.x);
            Vector2 panel = (joker ? layout.JokerPanel : layout.PowerPanel) * screenPerCanvas;
            float gap = (joker ? layout.JokerGap : layout.PowerGap) * screenPerCanvas;
            int columns = Mathf.Max(1, joker ? layout.JokerColumns : layout.PowerColumns);
            Vector2 at = last.Value;
            if (layout.BarsAsRow)
            {
                at.x += panel.x + gap;
            }
            else if (count % columns == 0)
            {
                // a new row, back at the edge's column
                at.y -= panel.y + gap;
                at.x += (joker ? 1f : -1f) * (columns - 1) * (panel.x + gap);
            }
            else
            {
                at.x += (joker ? -1f : 1f) * (panel.x + gap);
            }
            return ScreenRectToWorld(at, panel, -cam.transform.position.z);
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

        // ================================================================== the corrective pass

        /// <summary>The corrective brief's lab list (163), c1-c38, and its audio panel (131).</summary>
        private void AddTamagotchiCorrectiveLab()
        {
            // --- FURY ---
            AddPet("c1 OLD fury (the first version, for comparison)", "c1 ESKİ öfke (ilk sürüm, karşılaştırma için)", () => PetLabFuryWith(null, true, TamagotchiView.FuryParts.All));
            AddPet("c2 new: disappointment only", "c2 yeni: yalnız hayal kırıklığı", () => PetLabFury("disappointment", PetPunishKind.Board));
            AddPet("c3 new: face deformation only", "c3 yeni: yalnız yüz bozulması", () => PetLabFuryWith("face break", false, TamagotchiView.FuryParts.Face));
            AddPet("c4 new: growl + body tension", "c4 yeni: hırlama + gövde gerginliği", () => PetLabFuryWith("face break", false, TamagotchiView.FuryParts.Body | TamagotchiView.FuryParts.Voice));
            AddPet("c5 hatred wave only", "c5 yalnız nefret dalgası", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.IdlesEnabled = false; PetView.LabHatredWave(); });
            AddPet("c6 screen berry vignette only (held)", "c6 yalnız ekran vinyeti (sabit)", () => PetLabHatredHeld(true, false));
            AddPet("c7 chromatic edge only (held)", "c7 yalnız kenar renk ayrışması (sabit)", () => PetLabHatredHeld(false, true));
            AddPet("c8 full fury transformation", "c8 tam öfke dönüşümü", () => PetLabFury(null, PetPunishKind.Board));
            AddPet("c9 fury 0.5x", "c9 öfke 0.5x", () => { PetLabFury(null, PetPunishKind.Joker); PetView.PlaybackRate = 0.5f; });
            AddPet("c10 furious idle, 10 s (library + growl cadence)", "c10 öfkeli bekleme, 10 sn (kütüphane + hırlama)", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.LabNoRoam = true; });
            AddPet("+ impact only (edge spike + one arena impulse)", "+ yalnız darbe (kenar + tek tahta itmesi)", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.IdlesEnabled = false; PetView.LabHatredImpact(); });
            AddPet("+ fury with NO screen response (character only)", "+ ekran tepkisi OLMADAN öfke (yalnız karakter)", () => PetLabFuryWith(null, false, TamagotchiView.FuryParts.Face | TamagotchiView.FuryParts.Body | TamagotchiView.FuryParts.Voice));
            AddPet("+ static: normal vs furious (toggle)", "+ durağan: normal / öfkeli (değiştir)", () =>
            {
                petLabStaticFurious = !petLabStaticFurious;
                PetLabShow(petLabStaticFurious ? PetHungerStage.Furious : PetHungerStage.Calm, petLabStaticFurious ? 1f : 0f);
                PetView.LabStill = true;
                PetView.IdlesEnabled = false;
                PetView.LabNoHatred = true;
                animLastLabel = petLabStaticFurious ? "FURIOUS - no aura, no motion, no audio" : "NORMAL";
            });

            // --- ROAMING ---
            AddPet("c11 right -> left: hide and peek elsewhere", "c11 sağ -> sol: saklan, başka yerden bak", () => PetLabRoam("right edge, lower", "left edge, upper", PetMoveStyle.HideAndPeek, PetHungerStage.Hungry));
            AddPet("c12 left edge -> bottom corner: hop", "c12 sol kenar -> alt köşe: zıplama", () => PetLabRoam("left edge, lower", "bottom-left corner", PetMoveStyle.Hop, PetHungerStage.Calm));
            AddPet("c13 bottom edge sneak (corner -> over the pile)", "c13 alt kenarda sinsi süzülme (köşe -> deste üstü)", () => PetLabRoam("bottom-right corner", "peeking over the right pile", PetMoveStyle.Sneak, PetHungerStage.Calm));
            AddPet("+ to the top edge (hanging, head down)", "+ üst kenara (baş aşağı sarkma)", () => PetLabRoam("bottom-left corner", "top edge, left of centre", PetMoveStyle.HideAndPeek, PetHungerStage.Calm));
            AddPet("+ scamper along the bottom (pile -> corner)", "+ alt kenarda koşma (deste -> köşe)", () => PetLabRoam("behind the right pile", "bottom-right corner", PetMoveStyle.Scamper, PetHungerStage.Calm));
            AddPet("c14 corner sit", "c14 köşeye oturma", () => PetLabRoam("right edge, lower", "sitting in the bottom-right corner", PetMoveStyle.Hop, PetHungerStage.Calm));
            AddPet("c15 normal roam: 10 turns simulated", "c15 normal gezinme: 10 tur simülasyonu", () => PetLabRoamTurns(false));
            AddPet("c16 furious roam (gone, eyes elsewhere, head snap)", "c16 öfkeli gezinme (yok olur, gözler başka yerde)", () => PetLabRoam("bottom-right corner", "left edge, lower", PetMoveStyle.FuriousSnap, PetHungerStage.Furious));
            AddPet("+ furious roam: 10 turns simulated", "+ öfkeli gezinme: 10 tur simülasyonu", () => PetLabRoamTurns(true));

            // --- SPEECH ---
            AddPet("c17 normal bubble", "c17 normal balon", () => { PetLabShow(PetHungerStage.Calm, 0.1f); PetView.IdlesEnabled = false; PetView.LabSpeak("Bunu istiyorum!", "I want this one!", 2.6f); });
            AddPet("c18 hungry bubble (with the belly rub)", "c18 aç balonu (karın ovmayla)", () =>
            {
                PetLabShow(PetHungerStage.Calm, 0.3f);
                PetView.PlayHungerChange(new TamagotchiHungerChange { Previous = PetHungerStage.Calm, Stage = PetHungerStage.Hungry, Progress = 0.4f });
            });
            AddPet("c19 furious bubble", "c19 öfkeli balon", () => { PetLabShow(PetHungerStage.Furious, 1f); PetView.IdlesEnabled = false; PetView.LabSpeak("BEN ALIRIM.", "I'LL TAKE IT.", 2.6f); });
            AddPet("c20 bubble relocation (it follows a hop)", "c20 balonun yer değiştirmesi (zıplamayı izler)", () =>
            {
                PetLabRoam("right edge, lower", "bottom-right corner", PetMoveStyle.Hop, PetHungerStage.Calm);
                PetView.LabSpeak("Acıktım...", "I'm hungry...", 3.2f);
            });
            AddPet("+ every line, one after another", "+ bütün replikler, sırayla", () => PetLabAllLines());

            // --- EATING ---
            AddPet("c21 OLD fast joker eat (the first version)", "c21 ESKİ hızlı joker yeme (ilk sürüm)", () => { PetLabAsset(true, Rarity.Rare, null); PetView.LabLegacy = true; });
            AddPet("c22 new joker eat", "c22 yeni joker yeme", () => PetLabAsset(true, Rarity.Rare, null));
            AddPet("c23 new legendary joker eat", "c23 yeni efsanevi joker yeme", () => PetLabAsset(true, Rarity.Legendary, null));
            AddPet("c24 OLD board eat (the first version)", "c24 ESKİ tahta yeme (ilk sürüm)", () => { PetLabBoard(PetLabCells(3), null, false); PetView.LabLegacy = true; });
            AddPet("c25 new board eat", "c25 yeni tahta yeme", () => PetLabBoard(PetLabIrregular(), null, false));
            AddPet("c26 board eat 0.5x", "c26 tahta yeme 0.5x", () => { PetLabBoard(PetLabCells(3), null, false); PetView.PlaybackRate = 0.5f; });
            AddPet("c27 power eat (a snack: one tug, one big bite)", "c27 güç yeme (atıştırma: tek asılma, tek büyük ısırık)", () => PetLabAsset(false, Rarity.Common, null));
            AddPet("c28 high-value card (lift, lock, paw, two bites, gulp)", "c28 değerli kart (kalkış, kilit, pati, iki ısırık, yutma)", () => PetLabPile(true, 0, 1, null));
            AddPet("c29 low-value x5 pile (one at a time)", "c29 düşük x5 deste (tek tek)", () => PetLabPile(true, 5, 0, null));
            AddPet("c30 pile thinning (x5 at 0.5x)", "c30 destenin incelmesi (x5, 0.5x)", () => PetLabPile(true, 5, 0, null, false, 0.5f));
            AddPet("+ a second punish in the same fury (86% pace)", "+ aynı öfkede ikinci ceza (%86 tempo)", () => { PetLabAsset(true, Rarity.Rare, null); PetView.LabSetPunishCount(1); });
            AddPet("+ whole event: fury -> hold -> joker eat", "+ bütün olay: öfke -> bekleme -> joker yeme", () => PetLabAsset(true, Rarity.Legendary, null, true));
            AddPet("+ whole event: fury -> hold -> board eat", "+ bütün olay: öfke -> bekleme -> tahta yeme", () => PetLabBoard(PetLabIrregular(), null, true));

            // --- AUDIO (played without the pet: the mute test's other half) ---
            AddAnim("tamagotchi audio: c31 normal voice set", "tamagotchi ses: c31 normal ses seti", () => PetLabAudio(0.55f, PetSound.Enter, PetSound.Peek, PetSound.Idle, PetSound.Hungry, PetSound.NoticeDraggedCard, PetSound.ValidFoodNear, PetSound.WrongFoodNear, PetSound.Satisfied, PetSound.Smug));
            AddAnim("tamagotchi audio: c32 feed audio set", "tamagotchi ses: c32 besleme ses seti", () => PetLabAudioAt(
                PetSound.GrabFood, 0f, PetSound.Chomp, 0.2f, PetSound.Chew, 0.36f, PetSound.Chew, 0.47f, PetSound.ChompLight, 0.6f, PetSound.ChompLight, 0.72f, PetSound.Gulp, 0.88f, PetSound.Satisfied, 1.08f));
            AddAnim("tamagotchi audio: c33 growl (low + rasp stems)", "tamagotchi ses: c33 hırlama (alçak + hırıltı katmanları)", () => PetLabAudio(0.9f, PetSound.GrowlStart, PetSound.GrowlIdle, PetSound.GrowlIdle));
            AddAnim("tamagotchi audio: c34 fury burst (snarl)", "tamagotchi ses: c34 öfke patlaması (hırlayış)", () => PetLabAudio(0.8f, PetSound.FuryBreak));
            AddAnim("tamagotchi audio: c35 hatred wave + impact", "tamagotchi ses: c35 nefret dalgası + darbe", () => PetLabAudioAt(PetSound.HatredWave, 0f, PetSound.FuryImpact, 0.34f));
            AddAnim("tamagotchi audio: c36 board bite (suction, bite, grind, gulp)", "tamagotchi ses: c36 tahta ısırığı (emme, ısırık, öğütme, yutma)", () => PetLabAudioAt(
                PetSound.GrowlIdle, 0f, PetSound.BoardSuction, 0.3f, PetSound.BoardBite, 0.64f, PetSound.BoardGrind, 1.02f, PetSound.BoardGrind, 1.17f, PetSound.BoardGrind, 1.32f, PetSound.GulpDeep, 1.5f, PetSound.Grunt, 2.1f));
            AddAnim("tamagotchi audio: c37 joker bite (sting, grab, strain, pull, bites, gulp, grunt)", "tamagotchi ses: c37 joker ısırığı (kilit, yakalama, gerilme, çekiş, ısırıklar, yutma, homurtu)", () => PetLabAudioAt(
                PetSound.TargetAsset, 0f, PetSound.GrabAsset, 0.4f, PetSound.AssetStrain, 0.46f, PetSound.AssetStrain, 0.56f, PetSound.AssetPull, 0.65f,
                PetSound.AssetBite, 0.9f, PetSound.ChewFurious, 1.02f, PetSound.ChewFurious, 1.1f, PetSound.AssetBite, 1.19f, PetSound.GulpDeep, 1.36f, PetSound.Grunt, 1.95f));
            AddAnim("tamagotchi audio: c38 pile snack x5", "tamagotchi ses: c38 deste atıştırması x5", () => PetLabAudioAt(
                PetSound.PileSnack, 0f, PetSound.PileSnackLight, 0.22f, PetSound.PileSnackLight, 0.42f, PetSound.PileSnackLight, 0.6f, PetSound.PileSnack, 0.78f, PetSound.GulpDeep, 0.98f));
            AddAnim("tamagotchi audio: full fury (silence, sigh, growl, wave, snarl, impact)", "tamagotchi ses: tam öfke (sessizlik, iç çekiş, hırlama, dalga, hırlayış, darbe)", () =>
            {
                sfx.DuckForPet(1.2f, 1.7f);
                PetLabAudioAt(PetSound.Disappointed, 0.15f, PetSound.GrowlStart, 0.3f, PetSound.HatredWave, 0.55f, PetSound.FuryBreak, 0.72f, PetSound.FuryImpact, 0.9f, PetSound.BubbleFurious, 0.92f);
            });
            AddAnim("tamagotchi audio: chomp A / B / C / D (each take, dry)", "tamagotchi ses: çomp A / B / C / D (her çekim, kuru)", () => PetLabAudioTakes(PetSound.Chomp, 0.45f));
            AddAnim("tamagotchi audio: furious chomp A / B / C / D", "tamagotchi ses: öfkeli çomp A / B / C / D", () => PetLabAudioTakes(PetSound.ChompFurious, 0.5f));
            AddAnim("tamagotchi audio: chew sequence (six, never the same twice)", "tamagotchi ses: çiğneme dizisi (altı, aynısı art arda yok)", () => PetLabAudio(0.2f, PetSound.Chew, PetSound.Chew, PetSound.Chew, PetSound.Chew, PetSound.Chew, PetSound.Chew));
            AddAnim("tamagotchi audio: gulp / big gulp / deep gulp", "tamagotchi ses: yutkunma / büyük / derin", () => PetLabAudio(0.6f, PetSound.Gulp, PetSound.GulpBig, PetSound.GulpDeep));
            AddAnim("tamagotchi audio: furious idle (breath, teeth, growl, grunt)", "tamagotchi ses: öfkeli bekleme (soluk, diş, hırlama, homurtu)", () => PetLabAudio(1.1f, PetSound.FuriousBreath, PetSound.TeethClack, PetSound.GrowlIdle, PetSound.Grunt));
            AddAnim("tamagotchi audio: roaming normal (hide, taps, peek)", "tamagotchi ses: normal gezinme (saklanma, adımlar, bakış)", () => PetLabAudio(0.55f, PetSound.Hide, PetSound.Move, PetSound.Peek, PetSound.Stomp));
            AddAnim("tamagotchi audio: furious roam (scrape + growl)", "tamagotchi ses: öfkeli gezinme (sürtünme + hırlama)", () => PetLabAudio(0.7f, PetSound.MoveFurious, PetSound.GrowlIdle));
            AddAnim("tamagotchi audio: speech bubbles (normal, furious)", "tamagotchi ses: konuşma balonları (normal, öfkeli)", () => PetLabAudio(0.6f, PetSound.Bubble, PetSound.Bubble, PetSound.BubbleFurious, PetSound.BubbleFurious));
            AddAnim("tamagotchi audio knob: growl rasp layer 0.4 / 0.85 / 1.3 (cycles)", "tamagotchi ses ayarı: hırıltı katmanı 0.4 / 0.85 / 1.3 (döngü)", () =>
            {
                SoundFx.PetAudio.GrowlRaspLayer = SoundFx.PetAudio.GrowlRaspLayer < 0.6f ? 0.85f : SoundFx.PetAudio.GrowlRaspLayer < 1f ? 1.3f : 0.4f;
                animLastLabel = "growl rasp layer " + SoundFx.PetAudio.GrowlRaspLayer;
                sfx.Tamagotchi(PetSound.GrowlStart);
            });
            AddAnim("tamagotchi audio knob: growl low layer 0.5 / 1 / 1.4 (cycles)", "tamagotchi ses ayarı: alçak boğaz katmanı 0.5 / 1 / 1.4 (döngü)", () =>
            {
                SoundFx.PetAudio.GrowlLowLayer = SoundFx.PetAudio.GrowlLowLayer < 0.8f ? 1f : SoundFx.PetAudio.GrowlLowLayer < 1.2f ? 1.4f : 0.5f;
                animLastLabel = "growl low layer " + SoundFx.PetAudio.GrowlLowLayer;
                sfx.Tamagotchi(PetSound.GrowlStart);
            });
            AddAnim("tamagotchi audio knob: duck amount 0 / 0.28 / 0.45 (cycles)", "tamagotchi ses ayarı: kısma miktarı 0 / 0.28 / 0.45 (döngü)", () =>
            {
                SoundFx.PetAudio.MusicDuckAmount = SoundFx.PetAudio.MusicDuckAmount < 0.1f ? 0.28f : SoundFx.PetAudio.MusicDuckAmount < 0.4f ? 0.45f : 0f;
                animLastLabel = "duck " + SoundFx.PetAudio.MusicDuckAmount;
            });
            AddAnim("tamagotchi audio knob: voice volume 0.4 / 0.62 / 0.85 (cycles)", "tamagotchi ses ayarı: ses seviyesi 0.4 / 0.62 / 0.85 (döngü)", () =>
            {
                SoundFx.PetAudio.VoiceVolume = SoundFx.PetAudio.VoiceVolume < 0.5f ? 0.62f : SoundFx.PetAudio.VoiceVolume < 0.7f ? 0.85f : 0.4f;
                animLastLabel = "voice volume " + SoundFx.PetAudio.VoiceVolume;
                sfx.Tamagotchi(PetSound.Hungry);
            });
            // the field's own layers, one at a time
            AddAnim("tamagotchi hatred layer: vignette on/off", "tamagotchi nefret katmanı: vinyet aç/kapa", () => { TamagotchiHatredField.Layers.Vignette = !TamagotchiHatredField.Layers.Vignette; animLastLabel = "vignette " + TamagotchiHatredField.Layers.Vignette; });
            AddAnim("tamagotchi hatred layer: wave on/off", "tamagotchi nefret katmanı: dalga aç/kapa", () => { TamagotchiHatredField.Layers.Wave = !TamagotchiHatredField.Layers.Wave; animLastLabel = "wave " + TamagotchiHatredField.Layers.Wave; });
            AddAnim("tamagotchi hatred layer: chromatic edge on/off", "tamagotchi nefret katmanı: kenar ayrışması aç/kapa", () => { TamagotchiHatredField.Layers.Chromatic = !TamagotchiHatredField.Layers.Chromatic; animLastLabel = "chromatic " + TamagotchiHatredField.Layers.Chromatic; });
            AddAnim("tamagotchi hatred layer: board shadow on/off", "tamagotchi nefret katmanı: tahta gölgesi aç/kapa", () => { TamagotchiHatredField.Layers.BoardShadow = !TamagotchiHatredField.Layers.BoardShadow; animLastLabel = "board shadow " + TamagotchiHatredField.Layers.BoardShadow; });
            AddAnim("tamagotchi hatred layer: desaturation on/off", "tamagotchi nefret katmanı: renksizleşme aç/kapa", () => { TamagotchiHatredField.Layers.Desaturation = !TamagotchiHatredField.Layers.Desaturation; animLastLabel = "desaturation " + TamagotchiHatredField.Layers.Desaturation; });
        }

        private bool petLabStaticFurious;

        /// <summary>A place by its name out of the ones that are safe right now (or null).</summary>
        private TamagotchiHome PetLabHomeNamed(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            foreach (TamagotchiHome h in SolvePetHomes(false))
            {
                if (h.Name == name)
                {
                    return h;
                }
            }
            return null;
        }

        /// <summary>The fury with the first version's look, or with only some of its parts.</summary>
        private void PetLabFuryWith(string beat, bool legacy, TamagotchiView.FuryParts parts)
        {
            PetLabFury(beat, PetPunishKind.Board);
            TamagotchiView v = PetView;
            v.LabLegacy = legacy;
            v.LabNoHatred = legacy;
            v.LabFuryParts = parts;
        }

        /// <summary>The hatred field held at its peak with one layer of it showing.</summary>
        private void PetLabHatredHeld(bool vignette, bool chromatic)
        {
            PetLabShow(PetHungerStage.Furious, 1f);
            TamagotchiView v = PetView;
            v.IdlesEnabled = false;
            v.LabHatredHold = 1f;
            TamagotchiHatredField.Layers.Vignette = vignette;
            TamagotchiHatredField.Layers.Chromatic = chromatic;
            TamagotchiHatredField.Layers.Wave = false;
            TamagotchiHatredField.Layers.BoardShadow = false;
            TamagotchiHatredField.Layers.Desaturation = vignette;
        }

        /// <summary>A move from one named place to another by a named style. When a place is not
        /// safe on this screen, the solver's own is used and the label says so.</summary>
        private void PetLabRoam(string from, string to, PetMoveStyle style, PetHungerStage stage)
        {
            TamagotchiHome start = PetLabHomeNamed(from);
            petLabHome = start != null ? from : null;
            PetLabShow(stage, stage == PetHungerStage.Furious ? 1f : 0.3f);
            TamagotchiView v = PetView;
            v.IdlesEnabled = false;
            v.LabNoRoam = true;
            TamagotchiHome target = PetLabHomeNamed(to);
            if (target == null)
            {
                // anywhere else that is safe
                foreach (TamagotchiHome h in SolvePetHomes(stage == PetHungerStage.Furious))
                {
                    if (h.Name != v.Home.Name)
                    {
                        target = h;
                        break;
                    }
                }
            }
            if (target == null)
            {
                animLastLabel = "no other safe place on this screen";
                return;
            }
            animLastLabel = style + ": " + v.Home.Name + " -> " + target.Name + (start == null ? "  (\"" + from + "\" is not safe here)" : "");
            v.PlayRoam(target, style);
        }

        /// <summary>Ten turns "played" a second and a half apart with the scheduler on: the pet
        /// should show several places and poses, and never move every turn.</summary>
        private void PetLabRoamTurns(bool furious)
        {
            PetLabShow(furious ? PetHungerStage.Furious : PetHungerStage.Hungry, furious ? 1f : 0.3f);
            TamagotchiDebug.ShowChosenRoamingAnchor = true;
            petLabRoutine = StartCoroutine(PetLabRoamTurnsRoutine());
        }

        private IEnumerator PetLabRoamTurnsRoutine()
        {
            TamagotchiView v = PetView;
            for (int turn = 1; turn <= 10; turn++)
            {
                float t = 0f;
                while (t < 1.5f)
                {
                    t += Time.deltaTime * Mathf.Max(0.05f, v.PlaybackRate);
                    yield return null;
                }
                v.NoteTurn();
                animLastLabel = "turn " + turn + " / 10  -  at: " + v.Home.Name;
            }
            petLabRoutine = null;
        }

        /// <summary>Every line it can say, in its two voices.</summary>
        private void PetLabAllLines()
        {
            PetLabShow(PetHungerStage.Calm, 0.2f);
            PetView.IdlesEnabled = false;
            petLabRoutine = StartCoroutine(PetLabAllLinesRoutine());
        }

        private IEnumerator PetLabAllLinesRoutine()
        {
            TamagotchiView v = PetView;
            string[][] calm =
            {
                new[] { "Acıktım...", "I'm hungry..." }, new[] { "Bunu istiyorum!", "I want this one!" },
                new[] { "Bir tane daha!", "One more!" }, new[] { "Hmm?", "Hmm?" }, new[] { "Nam?", "Nom?" },
                new[] { "Hadi ama!", "Come on!" }, new[] { "Bana onu ver.", "Give me that." },
                new[] { "Son şansın...", "Last chance..." }, new[] { "O değil!", "Not that one!" }, new[] { "Doydum!", "I'm full!" }
            };
            string[][] furious =
            {
                new[] { "BEN ALIRIM.", "I'LL TAKE IT." }, new[] { "VER ŞUNU.", "GIVE IT." }, new[] { "YETER.", "ENOUGH." },
                new[] { "ACIM.", "HUNGRY." }, new[] { "HEPSİNİ YERİM.", "I'LL EAT IT ALL." }
            };
            foreach (string[] line in calm)
            {
                v.LabSpeak(line[0], line[1], 1.1f);
                yield return new WaitForSeconds(1.5f);
            }
            v.LabSetState(PetLabState(PetHungerStage.Furious, 1f, 0, false));
            yield return new WaitForSeconds(0.5f);
            foreach (string[] line in furious)
            {
                v.LabSpeak(line[0], line[1], 1.1f);
                yield return new WaitForSeconds(1.5f);
            }
            petLabRoutine = null;
        }

        /// <summary>Plays cues one after another, <paramref name="gap"/> apart - no pet needed.</summary>
        private void PetLabAudio(float gap, params PetSound[] cues)
        {
            if (petLabAudioRoutine != null)
            {
                StopCoroutine(petLabAudioRoutine);
            }
            var at = new List<KeyValuePair<PetSound, float>>();
            for (int i = 0; i < cues.Length; i++)
            {
                at.Add(new KeyValuePair<PetSound, float>(cues[i], i * gap));
            }
            petLabAudioRoutine = StartCoroutine(PetLabAudioRoutine(at, -1));
        }

        /// <summary>Plays cues at stated times (cue, seconds, cue, seconds ...): a whole event's
        /// audio on its own timeline.</summary>
        private void PetLabAudioAt(params object[] cuesAndTimes)
        {
            if (petLabAudioRoutine != null)
            {
                StopCoroutine(petLabAudioRoutine);
            }
            var at = new List<KeyValuePair<PetSound, float>>();
            for (int i = 0; i + 1 < cuesAndTimes.Length; i += 2)
            {
                at.Add(new KeyValuePair<PetSound, float>((PetSound)cuesAndTimes[i], System.Convert.ToSingle(cuesAndTimes[i + 1])));
            }
            petLabAudioRoutine = StartCoroutine(PetLabAudioRoutine(at, -1));
        }

        /// <summary>Every take of one cue in order, dry.</summary>
        private void PetLabAudioTakes(PetSound cue, float gap)
        {
            if (petLabAudioRoutine != null)
            {
                StopCoroutine(petLabAudioRoutine);
            }
            var at = new List<KeyValuePair<PetSound, float>>();
            int n = TamagotchiVoice.Variants(cue);
            for (int i = 0; i < n; i++)
            {
                at.Add(new KeyValuePair<PetSound, float>(cue, i * gap));
            }
            petLabAudioRoutine = StartCoroutine(PetLabAudioRoutine(at, 0));
        }

        private Coroutine petLabAudioRoutine;

        private IEnumerator PetLabAudioRoutine(List<KeyValuePair<PetSound, float>> cues, int firstTake)
        {
            float t = 0f;
            int next = 0;
            while (next < cues.Count)
            {
                while (next < cues.Count && cues[next].Value <= t)
                {
                    if (firstTake >= 0)
                    {
                        sfx.TamagotchiTake(cues[next].Key, firstTake + next);
                    }
                    else
                    {
                        sfx.Tamagotchi(cues[next].Key);
                    }
                    animLastLabel = sfx.PetLastCue + "  =  " + sfx.PetLastLayers;
                    next++;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            petLabAudioRoutine = null;
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
