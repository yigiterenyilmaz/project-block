// PURPOSE: F4 - opening, closing and owning the BLOCK GALLERY (see BlockGalleryView).
// The panel draws itself from the enums; all this holds is the key and the open state.
//
// Like the animation lab it takes the whole frame while it is open, and for the same reason:
// it is a full-screen catalogue, so any click that fell through to the board underneath would
// place a block the player cannot see.

using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private BlockGalleryView blockGallery;

        private bool GalleryOpen
        {
            get { return blockGallery != null && blockGallery.IsOpen; }
        }

        private void OpenBlockGallery()
        {
            // The joker strip is screen-space and would draw over the catalogue whatever the
            // sorting order, exactly as it does over the animation lab.
            jokerBar.SetVisible(false);
            blockGallery.Show();
            // Diagnostic line: how many card faces are remembered, and whether the last board
            // repaint had cubes that could not find theirs. A placed block losing its face is
            // invisible otherwise - it just looks like an unpainted block type.
            blockGallery.SetStatus("faces " + cardFaces.Count
                + "   unresolved cubes " + boardView.UnresolvedCubes
                + (boardView.UnresolvedCubes > 0
                    ? " (e.g. card id " + boardView.UnresolvedExampleId + ")"
                    : string.Empty)
                + "   target_body tile "
                + (ViewUtil.HasTile("block_target_body") ? "loaded" : "MISSING"));
        }

        private void CloseBlockGallery()
        {
            blockGallery.Hide();
            jokerBar.SetVisible(true);
        }

        private void HandleBlockGalleryInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.f4Key.wasPressedThisFrame))
            {
                CloseBlockGallery();
                return;
            }
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            BlockElement? picked = blockGallery.ElementAt(world);
            if (!picked.HasValue)
            {
                return;
            }
            DealDebugBlock(picked.Value);
        }

        /// <summary>Deals one bonus card of the chosen element straight into the hand, so a
        /// block type can be PLAYED rather than just looked at. Round-scoped like every other
        /// bonus card: it never joins the owned deck, so a test block cannot pollute a run.
        /// The gallery stays open - you usually want several - and the hand behind it is
        /// refreshed at once, so closing shows the card already sitting there.</summary>
        private void DealDebugBlock(BlockElement element)
        {
            if (session == null || session.Phase != GamePhase.Round)
            {
                blockGallery.SetStatus(Loc.Pick("only during a round", "sadece raunt sırasında"));
                return;
            }
            BlockCard card = session.DebugAddElementCard(element);
            Debug.Log("[block_bonk] Debug block dealt: " + card);
            blockGallery.SetStatus(Loc.Pick(
                "dealt " + ViewUtil.ElementLabel(element) + " to your hand",
                ViewUtil.ElementLabel(element) + " eline verildi"));
            RefreshAll(null);
        }
    }
}
