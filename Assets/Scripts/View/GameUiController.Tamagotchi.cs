// PURPOSE: "Tamagotchi" going berserk, told to the player. The rampage itself is Core's
// (TamagotchiBoss.LastRampage, a new report per rampage, matched by identity here); this only says
// what it took. The board bite needs no drawing of its own - the eaten cells come back from the
// repaint as dead cells - and a joker, power or card it ate has already left its bar or pile.
// EXTENSION POINT: a rampage animation of its own belongs here; for now it is a headline, a line
// in the message bar and a sound.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private PetRampageVisuals lastPetRampage;

        private static readonly Color PetRampageColor = new Color(1f, 0.42f, 0.55f);

        private void PlayPetRampage(TamagotchiBoss pet)
        {
            PetRampageVisuals rampage = pet != null ? pet.LastRampage : null;
            if (rampage == null || ReferenceEquals(rampage, lastPetRampage))
            {
                return;
            }
            lastPetRampage = rampage;
            string what = PetRampageText(rampage);
            FloatingTextFx.Spawn(transform, MainBoardCenter + new Vector2(0f, 1.3f),
                Loc.Pick("TAMAGOTCHI WENT BERSERK!", "TAMAGOTCHİ ÇILDIRDI!"), PetRampageColor, 72, 0.09f);
            FloatingTextFx.Spawn(transform, MainBoardCenter + new Vector2(0f, 0.6f), what,
                PetRampageColor, 46, 0.1f);
            messageText.text = what;
            sfx.Vanish();
            sfx.Rumble();
        }

        private static string PetRampageText(PetRampageVisuals rampage)
        {
            switch (rampage.Kind)
            {
                case PetPunishKind.Board:
                    return Loc.Pick("It ate " + rampage.Count + " cells of the board.",
                        "Tahtadan " + rampage.Count + " hücre yedi.");
                case PetPunishKind.Joker:
                    return Loc.Pick("It ate your joker " + rampage.EatenName + ".",
                        rampage.EatenName + " jokerini yedi.");
                case PetPunishKind.Power:
                    return Loc.Pick("It ate your power " + rampage.EatenName + ".",
                        rampage.EatenName + " gücünü yedi.");
                case PetPunishKind.DiscardPile:
                    return Loc.Pick("It ate " + rampage.Count + " card(s) from your discard.",
                        "Iskartandan " + rampage.Count + " kart yedi.");
                default:
                    return Loc.Pick("It ate " + rampage.Count + " card(s) from your deck.",
                        "Destenden " + rampage.Count + " kart yedi.");
            }
        }
    }
}
