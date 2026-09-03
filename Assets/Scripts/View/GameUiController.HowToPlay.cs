// PURPOSE: GameUiController's how-to-play screen - the only place the game explains itself
// to someone who has not read the design doc.
//
// PAGED, NOT SCROLLED: the body is a fixed box and uGUI Text simply CLIPS what does not fit,
// so a single long page silently loses its tail (the controls section did exactly that). Each
// page is one topic and is kept short enough to fit; adding material means adding a page, not
// growing one. Keep every page at or under ~19 lines.
//
// KEEP THIS HONEST in two directions:
//  - it documents the controls the other partials actually implement (.Drag for placing,
//    .Bars for the strips and the market, .Menus for Escape). A wrong controls screen is
//    worse than none.
//  - it describes only mechanics that EXIST. Cut or unbuilt ideas from the design doc (mirror
//    blocks, the kumbara element, the bonus/fragile/infinite market variants) are deliberately
//    absent - promising a tester something the build cannot do wastes their time.
// It deliberately does NOT enumerate individual jokers, powers, bosses or block elements:
// there are well over a hundred between them, they change weekly, and the player meets each
// one in the market with its own description. This screen teaches the CONCEPTS - what a
// joker is versus a power, what an element does to a block - not the catalogue.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Where BACK goes - the screen is reachable from the title and from a
        /// paused run, exactly like settings.</summary>
        private AppScreen howToPlayReturnTo = AppScreen.Title;

        private int howToPage;

        private void OpenHowToPlay(AppScreen returnTo)
        {
            howToPlayReturnTo = returnTo;
            howToPage = 0;
            screen = AppScreen.HowToPlay;
            ShowHowToPlay();
        }

        private void CloseHowToPlay()
        {
            screen = howToPlayReturnTo;
            ShowCurrentMenu();
        }

        private void ShowHowToPlay()
        {
            string[][] pages = HowToPlayPages();
            howToPage = Mathf.Clamp(howToPage, 0, pages.Length - 1);
            Color backdrop = howToPlayReturnTo == AppScreen.Paused
                ? MenuSkin.OverlayBackdrop
                : MenuSkin.Backdrop;
            var entries = new List<MenuEntry> { MenuEntry.Of(Loc.Pick("BACK", "GERİ")) };
            string hint = Loc.Pick("←  →  page ", "←  →  sayfa ")
                + (howToPage + 1) + " / " + pages.Length
                + Loc.Pick("     [Esc] back", "     [Esc] geri");
            menu.ShowText(Loc.Pick("HOW TO PLAY", "NASIL OYNANIR"),
                string.Join("\n", pages[howToPage]), hint, entries, backdrop);
        }

        /// <summary>Input for the reading screen: left/right (or the wheel) turn pages, BACK
        /// and Escape leave.</summary>
        private void HandleHowToPlayInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CloseHowToPlay();
                return;
            }
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame)
                {
                    TurnHowToPlayPage(+1);
                    return;
                }
                if (kb.leftArrowKey.wasPressedThisFrame)
                {
                    TurnHowToPlayPage(-1);
                    return;
                }
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    TurnHowToPlayPage(scroll > 0f ? -1 : +1);
                    return;
                }
            }
            if (ReadMenuChoice(kb, mouse) >= 0)
            {
                CloseHowToPlay();
            }
        }

        /// <summary>Turns a page, stopping at both ends rather than wrapping - wrapping from
        /// the last page back to the first reads as "the screen glitched".</summary>
        private void TurnHowToPlayPage(int delta)
        {
            int wanted = Mathf.Clamp(howToPage + delta, 0, HowToPlayPages().Length - 1);
            if (wanted == howToPage)
            {
                return;
            }
            howToPage = wanted;
            ShowHowToPlay();
        }

        private static string[][] HowToPlayPages()
        {
            return Loc.Language == GameLanguage.Turkish ? TurkishPages() : EnglishPages();
        }

        private static string[][] EnglishPages()
        {
            return new[]
            {
                new[]
                {
                    "THE GOAL",
                    "   Drag a block out of your hand onto the arena. Fill a whole row or column",
                    "   and it blows up.",
                    "",
                    "   Every round has its own arena size and its own SCORE THRESHOLD. Reach the",
                    "   threshold and you earn the right to move on. A run is 15 rounds, and every",
                    "   3rd one is a BOSS ROUND that bends a rule against you.",
                    "",
                    "   Score is also your money: what you earn is what you spend in the market.",
                    "",
                    "CLEAN SWEEP",
                    "   Emptying the arena completely is a 'clean sweep'. It pays a large bonus and",
                    "   recharges every power you hold - it is the engine of the whole game.",
                    "   A few block types are ignored by the check, so an arena holding only those",
                    "   still counts as swept."
                },
                new[]
                {
                    "THE TURN",
                    "   You hold 3 blocks. Place one, it goes to the discard, and you draw a",
                    "   replacement. Jokers and powers can change how many you hold.",
                    "",
                    "PASSING THE THRESHOLD",
                    "   The discard is shuffled back into the draw pile and you are offered the",
                    "   market. You can take it, or CONTINUE for more points.",
                    "",
                    "OVERTIME",
                    "   Once you continue, the offer to leave only comes back on each clean sweep.",
                    "   Continuing costs cards out of the round, and the price rises every time.",
                    "   Regular actions pay almost nothing now - the reward is for surviving each",
                    "   overtime, and it grows.",
                    "   If the draw pile empties before you sweep, you LOSE.",
                    "",
                    "LOSING",
                    "   The usual way is the arena filling up with nothing in hand that fits.",
                    "   Some jokers, powers and bosses can end a run in their own ways."
                },
                new[]
                {
                    "YOUR DECKS",
                    "   YOUR COLLECTION is every card you own. It is what a round is dealt from.",
                    "   The DRAW PILE is what you draw from; when it runs out, the discard is",
                    "   shuffled back into it.",
                    "   The DISCARD is where played and dumped cards pile up.",
                    "",
                    "   You pick a deck archetype when a run starts - classic, small blocks, big",
                    "   blocks or chaos - and it decides what shapes you keep drawing all run.",
                    "   Cards bought in the market join your collection from the next round.",
                    "",
                    "BONUS HAND",
                    "   Extra blocks that do not take up a hand slot. Playing one burns the next",
                    "   card off the draw pile face-up into the discard, and the bonus block is",
                    "   usually spent for good.",
                    "",
                    "EROSION - THE STALLING CLOCK",
                    "   Run the draw pile dry too often in one round and the arena starts eroding:",
                    "   the rim, a growing hole in the middle, or both. An EATEN cell is worse than",
                    "   a hole - it kills its row and its column, which can never be cleared again."
                },
                new[]
                {
                    "BLOCKS AND ELEMENTS",
                    "   A block bought in the market can carry an element that changes how it",
                    "   behaves - burning, flowing, refusing to be destroyed, paying points while",
                    "   it sits there, turning in your hand, or erasing whatever it lands on.",
                    "   Every card shows what it carries, and the market describes it before you",
                    "   buy it.",
                    "",
                    "JOKERS",
                    "   Bought and then left alone - passive, never need recharging, and they bend",
                    "   the rules on their own. You have a limited number of slots. They act in the",
                    "   order you bought them and stack rather than overwrite each other.",
                    "",
                    "POWERS",
                    "   Active instead. Each holds ONE charge, you may use at most one per turn,",
                    "   and using one never costs you a turn. A clean sweep is what puts the",
                    "   charges back - which is why sweeping matters even when you are ahead.",
                    "",
                    "RARITY",
                    "   Common, rare and legendary. Rarer costs more and shows up less often, and",
                    "   you may hold only one legendary joker at a time."
                },
                new[]
                {
                    "BOSS ROUNDS",
                    "   Every 3rd round brings one antagonist that bends a single rule for that",
                    "   round only - stealing from your hand, sealing cells, stripping elements,",
                    "   taxing your collection, silencing jokers, or limiting what scores.",
                    "   Never two at once, and never the same boss twice in a run.",
                    "",
                    "THE MARKET",
                    "   Between rounds: click an offer to buy it, click your deck to sell cards,",
                    "   click a joker or power in its bar to sell it, REROLL to restock the whole",
                    "   shelf for a price that rises each time you do it.",
                    "   [N] starts the next round.",
                    "",
                    "KEYS",
                    "   [Esc] pause      [L] language      [F2] rarity grader",
                    "   [1-9] use a joker      [A] advance      [C] continue (overtime)",
                    "   [F] feed the pet the card under the cursor (Tamagotchi rounds).",
                    "   Right-click a block in hand to rotate or reshape it.",
                    "   Click the draw pile to look through your collection.",
                    "",
                    "   Debug: [R] new run  [D] deck  [S] redraw  [B] bonus card",
                    "          [J] grant joker  [P] grant power  [K] sell the last joker",
                    "          [G] start a boss stage now (pick one, or draw at random)",
                    "          [Y] go into overtime for real, a step deeper each press"
                },
                new[]
                {
                    "GAMEPAD - THE CURSOR SCHEME",
                    "   A pad is picked up on its own - nudge a stick and the cursor appears.",
                    "   Touch the mouse or the keyboard and it hands the pointer straight back.",
                    "   SETTINGS holds a second scheme, DIRECT, on the next two pages.",
                    "",
                    "   Left stick     move the cursor   (in a menu: move the selection)",
                    "   D-pad          menu rows, settings, page turns, the retro falling piece",
                    "   A              click - and confirm in a menu",
                    "   X              right-click: rotate a block, reshape a fox",
                    "   B / Start      back - cancel, close a panel, or pause",
                    "   Y              the stage's one press: in the market it starts the next",
                    "                  round, and when the market is offered it takes it",
                    "   LT (hold)      the smuggle modifier in the market     RT: faster cursor",
                    "   LB / RB        scroll - your collection, the market shelf, these pages",
                    "   L3 / R3        hide or show the prompt strip / pay the debt (market)",
                    "",
                    "   Jokers and powers live on their bars: point at one and press A."
                },
                new[]
                {
                    "GAMEPAD - DIRECT: PLAYING A ROUND",
                    "   Turn it on in SETTINGS. There is NO cursor anywhere - the board, the",
                    "   shop and every panel are stepped through instead. Only a retro round is",
                    "   different: the falling piece keeps its own controls.",
                    "",
                    "   Stick / D-pad  step through your hand. Once a block is taken, walk it",
                    "                  around the arena one cell at a time.",
                    "   A              take the chosen block - then place it where it stands",
                    "   B              put the block back   (or pause, from the hand)",
                    "   X              rotate a gear block, reshape a fox",
                    "   Menu (three    look through your collection",
                    "     lines)",
                    "   RB (hold)      the joker strip - step it, A uses the one you stop on",
                    "   LB (hold)      the power strip - step it, A uses the one you stop on",
                    "   R3             feed the pet the chosen card (Tamagotchi rounds)",
                    "   L3             hide or show the prompt strip along the bottom",
                    "",
                    "   A joker or power that wants a target aims the same way: step the arena",
                    "   or your hand and press A. B cancels."
                },
                new[]
                {
                    "GAMEPAD - DIRECT: THE SHOP",
                    "   The shelf is stepped, not pointed at, and every verb the market has gets",
                    "   a button of its own.",
                    "",
                    "   Stick / D-pad  move to whatever is that way on the shelf - blocks run",
                    "                  down the left, jokers over powers on the right",
                    "   A              buy the offer you are on",
                    "   LT + A         take it FREE instead, if you have the smuggler",
                    "   X              reroll the shelf you are on",
                    "   Y              start the next round",
                    "   RB (hold)      the joker strip - step it, A sells the one you stop on",
                    "   LB (hold)      the power strip - step it, A sells the one you stop on",
                    "   Menu           your collection, priced to sell",
                    "   R3             pay off your debt, if you owe any",
                    "   L3             hide or show the prompt strip",
                    "   B              pause",
                    "",
                    "   A panel that opens over the shop is stepped the same way: a direction",
                    "   moves to what is that way on it, and A does what a click would."
                }
            };
        }

        private static string[][] TurkishPages()
        {
            return new[]
            {
                new[]
                {
                    "AMAÇ",
                    "   Elindeki bloğu oyun alanına sürükle. Bir satırı ya da sütunu tamamen",
                    "   doldurursan patlar.",
                    "",
                    "   Her rauntun kendi alan büyüklüğü ve kendi PUAN EŞİĞİ vardır. Eşiğe",
                    "   ulaşınca sonraki raunta geçme hakkı kazanırsın. Bir oyun 15 raunt sürer",
                    "   ve her 3. raunt, sana karşı bir kuralı büken bir PATRON RAUNDUDUR.",
                    "",
                    "   Puan aynı zamanda paran: kazandığın şey markette harcadığın şeydir.",
                    "",
                    "TEMİZLİK",
                    "   Oyun alanını tamamen boşaltmak 'temizlik'tir. Bol puan kazandırır ve",
                    "   elindeki bütün güçleri yeniden doldurur - oyunun motoru budur.",
                    "   Bazı blok türleri bu kontrolde sayılmaz; sadece onlardan oluşan bir alan",
                    "   yine de temizlenmiş sayılır."
                },
                new[]
                {
                    "TUR",
                    "   Elinde 3 blok tutarsın. Birini koyarsın, o kart ıskartaya gider ve yerine",
                    "   yeni bir kart çekersin. Jokerler ve güçler el boyutunu değiştirebilir.",
                    "",
                    "EŞİĞİ GEÇMEK",
                    "   Iskarta çekme destesine karılır ve sana market teklif edilir. Kabul",
                    "   edebilirsin ya da daha çok puan için DEVAM edebilirsin.",
                    "",
                    "UZATMA",
                    "   Devam ettikten sonra çıkma teklifi yalnızca her temizlikte geri gelir.",
                    "   Devam etmek raunttan kart götürür ve bedeli her seferinde artar.",
                    "   Artık sıradan hamleler neredeyse hiç puan vermez - ödül her uzatmayı",
                    "   atlatmaktadır ve giderek büyür.",
                    "   Temizlik yapmadan çekme destesi biterse KAYBEDERSİN.",
                    "",
                    "KAYBETMEK",
                    "   Olağan yol, alanın dolması ve elindeki hiçbir bloğun sığmamasıdır.",
                    "   Bazı jokerler, güçler ve patronlar da kendi yollarıyla oyunu bitirebilir."
                },
                new[]
                {
                    "DESTELERİN",
                    "   OYUN DESTESİ sahip olduğun bütün kartlardır; raunt ondan dağıtılır.",
                    "   ÇEKME DESTESİ kart çektiğin yerdir; bitince ıskarta karılıp yerine konur.",
                    "   ISKARTA oynanan ve elden çıkarılan kartların biriktiği yerdir.",
                    "",
                    "   Oyun başlarken bir deste türü seçersin - klasik, küçük bloklar, büyük",
                    "   bloklar ya da kaos - ve bu, oyun boyunca çekeceğin şekilleri belirler.",
                    "   Marketten aldığın kartlar sonraki rauntta desteye karışır.",
                    "",
                    "BONUS EL",
                    "   Elde yer kaplamayan ek bloklar. Birini oynadığında çekme destesinin",
                    "   üstündeki kart açık şekilde ıskartaya yakılır ve bonus blok genelde",
                    "   tamamen harcanmış olur.",
                    "",
                    "EROZYON - OYALANMA SAATİ",
                    "   Bir rauntta çekme destesini çok kez bitirirsen alan erimeye başlar:",
                    "   kenardan, ortada büyüyen bir delikten ya da ikisinden birden. YENEN bir",
                    "   hücre delikten beterdir - satırını ve sütununu öldürür, bir daha temizlenemez."
                },
                new[]
                {
                    "BLOKLAR VE ELEMENTLER",
                    "   Marketten alınan bir blok, davranışını değiştiren bir element taşıyabilir",
                    "   - yanan, akan, yok edilemeyen, durduğu sürece puan kazandıran, elinde",
                    "   dönen ya da üstüne düştüğü şeyi silen türler vardır.",
                    "   Her kart ne taşıdığını gösterir; market de almadan önce ne yaptığını",
                    "   anlatır.",
                    "",
                    "JOKERLER",
                    "   Alınır ve sonra unutulur - pasiftirler, yeniden doldurulmaları gerekmez ve",
                    "   kuralları kendiliğinden bükerler. Sınırlı sayıda slotun var. Aldığın",
                    "   sırayla çalışırlar; birbirlerinin üstüne binerler, birbirlerini ezmezler.",
                    "",
                    "GÜÇLER",
                    "   Bunlar aktiftir. Her birinin TEK şarjı vardır, turda en fazla birini",
                    "   kullanabilirsin ve kullanmak sana tur harcatmaz. Şarjları geri getiren",
                    "   şey temizliktir - öndeyken bile temizlik yapmanın sebebi budur.",
                    "",
                    "NADİRLİK",
                    "   Yaygın, nadir ve efsanevi. Nadir olan daha pahalıdır, daha seyrek çıkar ve",
                    "   aynı anda yalnızca bir efsanevi joker tutabilirsin."
                },
                new[]
                {
                    "PATRON RAUNTLARI",
                    "   Her 3. raunt, yalnızca o raunt için tek bir kuralı büken bir düşman",
                    "   getirir - elinden kart çalmak, hücre mühürlemek, elementleri silmek,",
                    "   desteni vergilendirmek, jokerleri susturmak ya da neyin puan verdiğini",
                    "   kısıtlamak. Aynı anda iki tane olmaz, bir oyunda aynısı iki kez gelmez.",
                    "",
                    "MARKET",
                    "   Rauntlar arası: almak için teklife tıkla, kart satmak için desteye tıkla,",
                    "   joker ya da güç satmak için barındaki panele tıkla, bütün rafı yenilemek",
                    "   için REROLL - bedeli her kullanışta artar.",
                    "   [N] sonraki raundu başlatır.",
                    "",
                    "TUŞLAR",
                    "   [Esc] duraklat      [L] dil      [F2] nadirlik defteri",
                    "   [1-9] joker kullan      [A] markete geç      [C] devam et (uzatma)",
                    "   [F] imlecin üstündeki kartı besle (Tamagotchi rauntlarında).",
                    "   Elindeki bloğa sağ tık: döndürür ya da şeklini değiştirir.",
                    "   Çekme destesine tıklayarak bütün kartlarına bakabilirsin.",
                    "",
                    "   Debug: [R] yeni oyun  [D] deste  [S] eli yenile  [B] bonus kart",
                    "          [J] joker ver  [P] güç ver  [K] son jokeri sat",
                    "          [G] hemen patron sahnesi başlat (seç ya da rastgele çek)",
                    "          [Y] gerçekten uzatmaya geç, her basışta bir kademe daha"
                },
                new[]
                {
                    "OYUN KOLU - İMLEÇ ŞEMASI",
                    "   Kol kendiliğinden devreye girer - çubuğa dokun, imleç belirir. Fareye ya",
                    "   da klavyeye dokunduğun anda imleci hemen geri verir.",
                    "   AYARLAR'da ikinci bir şema var: DOĞRUDAN, sonraki iki sayfada.",
                    "",
                    "   Sol çubuk      imleci gezdirir   (menüde: seçimi gezdirir)",
                    "   Yön tuşları    menü satırları, ayarlar, sayfa çevirme, retro düşen parça",
                    "   A              tıklama - menüde onay",
                    "   X              sağ tık: bloğu döndürür, tilkinin şeklini seçtirir",
                    "   B / Start      geri - iptal et, paneli kapat ya da duraklat",
                    "   Y              sahnenin tek tuşu: markette sonraki raundu başlatır,",
                    "                  market teklif edildiğinde markete geçer",
                    "   LT (basılı)    markette kaçakçılık tuşu      RT: imleci hızlandırır",
                    "   LB / RB        kaydırma - kartların, market rafı, bu sayfalar",
                    "   L3 / R3        ipucu şeridini gizle-göster / borcu öde (markette)",
                    "",
                    "   Jokerler ve güçler kendi barlarında: üstüne gel ve A'ya bas."
                },
                new[]
                {
                    "OYUN KOLU - DOĞRUDAN: RAUNT OYNAMAK",
                    "   AYARLAR'dan aç. HİÇBİR yerde imleç yoktur - tahta, market ve bütün",
                    "   paneller adım adım gezilir. Tek istisna retro raunt: düşen parça kendi",
                    "   kontrollerinde kalır.",
                    "",
                    "   Çubuk / Yön    elindeki kartları gezer. Blok aldıktan sonra bloğu",
                    "                  alanda hücre hücre yürütür.",
                    "   A              seçili bloğu al - sonra durduğu yere koy",
                    "   B              bloğu geri koy   (elindeyken: duraklat)",
                    "   X              çark bloğunu döndür, tilkiyi şekillendir",
                    "   Menu (üç       bütün kartlarına bak",
                    "     çizgi)",
                    "   RB (basılı)    joker barı - gez, A üstünde durduğunu kullanır",
                    "   LB (basılı)    güç barı - gez, A üstünde durduğunu kullanır",
                    "   R3             seçili kartı yaratığa yedirir (Tamagotchi rauntları)",
                    "   L3             alttaki ipucu şeridini gizler-gösterir",
                    "",
                    "   Hedef isteyen joker ya da güç de aynı şekilde nişan alır: alanı ya da",
                    "   elini gez ve A'ya bas. B iptal eder."
                },
                new[]
                {
                    "OYUN KOLU - DOĞRUDAN: MARKET",
                    "   Raf işaret edilmez, adım adım gezilir; marketin her eylemi kendi",
                    "   tuşunu alır.",
                    "",
                    "   Çubuk / Yön    o yönde ne varsa ona geçer - bloklar solda bir sütun,",
                    "                  jokerler ve güçler sağda alt alta",
                    "   A              üstünde durduğun ürünü satın al",
                    "   LT + A         kaçakçın varsa BEDAVA al",
                    "   X              üstünde durduğun rafı yenile",
                    "   Y              sonraki raundu başlat",
                    "   RB (basılı)    joker barı - gez, A üstünde durduğunu satar",
                    "   LB (basılı)    güç barı - gez, A üstünde durduğunu satar",
                    "   Menu           kartların, satış fiyatlarıyla",
                    "   R3             borcun varsa öde",
                    "   L3             ipucu şeridini gizle-göster",
                    "   B              duraklat",
                    "",
                    "   Marketin üstüne açılan panel de aynı şekilde gezilir: yön tuşu o yönde",
                    "   ne varsa ona geçer, A da tıklamanın yaptığını yapar."
                }
            };
        }
    }
}
