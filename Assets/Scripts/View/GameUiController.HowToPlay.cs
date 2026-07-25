// PURPOSE: GameUiController's how-to-play / controls screen - the only place the game
// explains itself to someone who has not read the code.
//
// KEEP THIS HONEST: it documents the controls the other partials actually implement
// (.Drag for placing, .Bars for the joker/power strips and the market, .Menus for Escape).
// When a key changes there, change it here too - a wrong controls screen is worse than none.

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

        private void OpenHowToPlay(AppScreen returnTo)
        {
            howToPlayReturnTo = returnTo;
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
            Color backdrop = howToPlayReturnTo == AppScreen.Paused
                ? MenuSkin.OverlayBackdrop
                : MenuSkin.Backdrop;
            menu.ShowText(Loc.Pick("HOW TO PLAY", "NASIL OYNANIR"), HowToPlayBody(),
                Loc.Pick("BACK", "GERİ"), backdrop);
        }

        /// <summary>Input for a reading screen: the only entry is BACK, and Escape does the
        /// same thing.</summary>
        private void HandleHowToPlayInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CloseHowToPlay();
                return;
            }
            if (ReadMenuChoice(kb, mouse) >= 0)
            {
                CloseHowToPlay();
            }
        }

        private static string HowToPlayBody()
        {
            string[] english =
            {
                "GOAL",
                "   Fill a whole row or column and it blows up. Reach the round's score threshold",
                "   to move on. A run is 15 rounds; every 3rd one is a boss round that bends a rule.",
                "",
                "PLACING BLOCKS",
                "   Drag a block from your hand onto the board. Right-click a block to rotate a",
                "   GEARS block or reshape a FOX one. Clearing the LAST cube off the board is a",
                "   clean sweep: it pays a bonus and recharges every power.",
                "",
                "JOKERS AND POWERS",
                "   Jokers sit along the top and bend the rules on their own - you buy them and",
                "   leave them alone. Powers sit on the left: one charge each, at most one per",
                "   turn, and using one never costs you a turn. Click either to use it (or 1-9",
                "   for jokers). A clean sweep is what puts power charges back.",
                "",
                "THE MARKET",
                "   Between rounds: click an offer to buy, click your deck to sell cards, REROLL",
                "   to restock the whole shelf. [N] starts the next round.",
                "",
                "OVERTIME",
                "   Once you pass the threshold you can [A] advance to the market, or [C] continue",
                "   playing for more points. Continuing costs cards out of your deck, and the price",
                "   goes up every time you do it.",
                "",
                "KEYS",
                "   [Esc] pause      [L] language      [F2] rarity grader",
                "   Debug: [R] new run  [D] deck  [S] redraw hand  [B] bonus card",
                "          [J] grant joker  [P] grant power  [K] sell last joker"
            };
            string[] turkish =
            {
                "AMAÇ",
                "   Bir satırı ya da sütunu tamamen doldur, patlasın. Rauntun puan eşiğine ulaşınca",
                "   ilerlersin. Bir oyun 15 raunt; her 3. raunt bir kuralı büken patron raundudur.",
                "",
                "BLOK YERLEŞTİRME",
                "   Elindeki bloğu oyun alanına sürükle. Sağ tık ÇARK bloğunu döndürür, TİLKİ",
                "   bloğunu yeniden şekillendirir. Alandaki SON küpü de temizlemek 'temizlik'tir:",
                "   bonus kazandırır ve bütün güçleri yeniden doldurur.",
                "",
                "JOKERLER VE GÜÇLER",
                "   Jokerler üstte durur ve kuralları kendiliğinden büker - alırsın, sonra unutursun.",
                "   Güçler solda: her birinin tek şarjı vardır, turda en fazla biri kullanılır ve",
                "   kullanmak tur harcamaz. Kullanmak için tıkla (jokerler için 1-9 da olur).",
                "   Güç şarjlarını geri getiren şey temizliktir.",
                "",
                "MARKET",
                "   Rauntlar arası: almak için tekliflere tıkla, kart satmak için desteye tıkla,",
                "   rafı yenilemek için REROLL. [N] sonraki raundu başlatır.",
                "",
                "UZATMA",
                "   Eşiği geçtikten sonra [A] ile markete ilerlersin ya da [C] ile daha fazla puan",
                "   için devam edersin. Devam etmek desteden kart götürür ve her seferinde",
                "   bedeli artar.",
                "",
                "TUŞLAR",
                "   [Esc] duraklat      [L] dil      [F2] nadirlik not defteri",
                "   Debug: [R] yeni oyun  [D] deste  [S] eli yenile  [B] bonus kart",
                "          [J] joker ver  [P] güç ver  [K] son jokeri sat"
            };
            return string.Join("\n", Loc.Language == GameLanguage.Turkish ? turkish : english);
        }
    }
}
