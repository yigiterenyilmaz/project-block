// PURPOSE: The mutable "current rules of play" shared by GameSession and RoundEngine.
// EXTENSION POINT: jokers and powers that bend the rules (hand size changes,
// redraw rights, altered overtime costs...) should mutate THIS object at runtime -
// RoundEngine always reads it live instead of caching values.

namespace ProjectBlock.Core
{
    /// <summary>Live rule values. Defaults are the confirmed base-game rules.</summary>
    public sealed class RoundRules
    {
        /// <summary>Cards the hand is refilled to after every normal placement.</summary>
        public int HandSize = 3;

        /// <summary>Confirmed rule (2026-07-18 feedback): declining an advance offer
        /// ("continue") removes this many random cards from the draw pile for the rest of
        /// the round, on top of the mandatory hand redraw.</summary>
        public int CardsRemovedPerContinue = 2;

        /// <summary>Balance: each further continue in the same round costs this many MORE
        /// cards (k-th continue removes CardsRemovedPerContinue + k * this). Caps how long
        /// overtime can be farmed - without it a 60-point round could yield 1600+.</summary>
        public int ContinueCostEscalation = 2;

        /// <summary>Pure UI flag: show the top card of the draw pile face-up ("Insider",
        /// "Oryantasyon"). The core never reads it - the draw order is unchanged either way.</summary>
        public bool RevealTopDrawCard = false;

        /// <summary>Pure UI flag: how many cards of the discard pile the player may inspect
        /// ("Fraksiyon"). 0 means only the usual top card, and no inspection.</summary>
        public int RevealedDiscardCount = 0;

        /// <summary>Pure UI flag: hide the discard pile's top card and block inspection
        /// ("Fraksiyon" after a swap, until the next reshuffle).</summary>
        public bool HideDiscardTop = false;

        /// <summary>Pure UI flag: how many cards of the DRAW pile are shown face-up
        /// ("Büyüteç"). It is CONSUMED as cards are drawn - RoundEngine.NoteCardDrawn decrements
        /// it once per drawn card down to 0. Insider uses RevealTopDrawCard for the single case.</summary>
        public int RevealedDrawCount = 0;

        /// <summary>"Oryantasyon": a card that would go to the discard is buried at a random
        /// depth in the DRAW pile instead. The discard therefore stays nearly empty, which
        /// also means the deck effectively never runs out.</summary>
        public bool PlayedCardsReturnToDrawPile = false;

        /// <summary>"Genel temizlik": a board-clear triggered by a joker or power BETWEEN
        /// turns counts as a real clean sweep (normally those are ignored because they happen
        /// with no placement resolving). Off in the base game.</summary>
        public bool CountExternalSweeps = false;

        /// <summary>"İmitasyon": skip the engine's standard end-of-placement hand refill. The
        /// joker dumps the leftover hand and refills to the mirrored size itself in
        /// AfterTurnScored; if the engine topped the hand up first, that extra card would just
        /// be discarded again and inflate the mirror (the discard would grow too fast).</summary>
        public bool SkipStandardRefill = false;

        /// <summary>"İmitasyon": a hand refill draws only what the draw pile actually holds -
        /// it never auto-recycles the discard, and running the pile dry mid-refill is NOT a
        /// loss (the hand just stays partial). The joker recycles explicitly, only when a
        /// card is played into an already-empty draw pile.</summary>
        public bool DrawOnlyAvailableNoReshuffle = false;

        /// <summary>"retro": tetris placement mode. While on, a chosen block falls from the top
        /// and the player steers/rotates it (the View drives that; the engine still resolves the
        /// final placement the same way). It also lets ANY block rotate - not just mechanical -
        /// and pays ScoringConfig.RetroPlacementBonus per placement. Toggled by the retro power;
        /// persists across rounds until toggled off (it is a mode, not a per-round bend).</summary>
        public bool RetroMode = false;

        /// <summary>"retro" dead zone: how many rows on TOP of the board are the overflow "dead"
        /// zone (0 when not in retro). The board is grown by this many rows when retro turns on.
        /// Cubes destroyed in a dead row score nothing; while any dead cell is occupied the retro
        /// toggle refuses to turn off; and filling the whole GAME area (the rows below) is a loss.
        /// A row y is dead when y &gt;= Board.MinY + Board.Height - DeadZoneRows.</summary>
        public int DeadZoneRows = 0;
    }
}
