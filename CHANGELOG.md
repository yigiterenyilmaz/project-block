# Changelog

Notable changes to **project_block**. Newest first. Joker/power names are the in-game
Turkish names with a short English gloss. This tracks the `balance` branch (pre-release), so
everything here is unreleased and balance numbers are still placeholders.

## Unreleased — `balance`

### Added
- **Market reroll** — a **REROLL** button under the offers refreshes every offer (blocks,
  jokers and powers) at once for an escalating cost (`RerollBaseCost + RerollCostStep × rerolls
  this visit`, ×10 scale). The price resets on the next market visit. Rerolls draw from their own
  deterministic rng so they vary per reroll and never disturb the deck/play stream; the initial
  stock is byte-identical to before.
- **Combo bonus** ("kombo") — clearing a line on consecutive turns stacks a growing point
  bonus (the n-th clearing turn in a row pays `n × ComboBonusPerStep`); a turn that clears no
  line resets the streak. It runs through the normal score pipeline (jokers scale it) and, like
  the rest of the regular base, trickles in overtime. The on-board "COMBO x" popup now reflects
  this real scoring streak instead of the destruction-only shake counter.
- **Kolay Para** (joker) — placing a block scores points, one bonus per cube. Fills the
  reserved "placement scores nothing on its own" slot from the scoring rework.
- **Halüsinasyon** (power) — shows up as a random *simple* power and, each time it is used,
  instantly recharges and morphs into a different one. Never becomes a legendary/stateful power.
- **Karakter Oluşturma** (power) — opens a designer to draw a custom block (any shape + one
  element); the block is baked into your deck and shuffles in from the next round.
- **Retro** (power, *in progress*) — a no-recharge toggle for a tetris/retro mode. Done so far:
  the toggle + a full-screen **CRT overlay** (scanlines, vignette, green tint, flicker), a flat
  score bonus per placement while on, and rotation for *any* block (not just mechanical). Still
  to come: blocks actually falling from the top (steerable falling-piece placement).
- **In-game Rarity Grader** — press **F2** in Play mode to browse every joker/power, read its
  live description, and grade it Common / Rare / Legendary (mouse-wheel or arrow keys to scroll).
  Saves to `Tools/RarityGrader/rarities.json`.
- **Rarity-driven shop** — those grades now matter: rarer jokers/powers **cost more**
  (×1 / ×2 / ×3 for common / rare / legendary) and **appear less often** in the market
  (draw weights ~100 / 35 / 8). One legendary joker held at a time, as before. Numbers are
  placeholders in `MarketConfig`; grades live in `RarityTable` (baked from the grader).

### Changed
- **Genel Temizlik** (joker) — description clarified: joker/power-triggered ("external") sweeps
  already pay the sweep bonus **and recharge your powers**, exactly like emptying the board on a
  placement; the text now says so.
- **İkinci Şans** (power) — now also **deals a fresh hand** when used: on top of clearing the
  board and reshuffling the deck, the current hand is recycled into the draw pile and a new hand
  is dealt, for a cleaner overtime restart.
- **Büyüteç** (power) — the reveal is now **consumable**: it uncovers the top two draw cards,
  and every card you draw leaves one fewer revealed (2 → 1 → 0) instead of showing the top two
  for the rest of the round.
- **Batak** — moved from a joker to a **power**. The bet picker now opens from the power bar;
  placing a bet spends the power's charge, and any clean sweep recharges it so you can bet again.
  Payout/deadline rules are unchanged.
- **Void cubes** ("Kara delik" traps) now **survive sweeps** — indestructible and sweep-exempt
  like obsidian, so they persist on the board; still consumed when a cube lands on them.
- **Dezenformasyon** (legendary) — the two deck halves now swap roles **every turn** instead of
  every round; the split is kept (never poured back together).
- **Scoring / economy rework** — overtime pays an escalating win bonus while regular actions pay
  almost nothing; placing blocks scores 0 by default; all money/scores use a single ×10 scale so
  the numbers read bigger without changing balance.

### Fixed
- **Retro CRT** — the overlay now turns off on restart (**R**) and on a deck change (a fresh
  game starts with retro off).
- **Totem** — the market is now shown when Totem ends overtime and advances the run mid-use
  (previously it advanced but the market stayed hidden).
- **Rarity grader (F2)** — the mouse wheel now scrolls the joker/power list.
- **İmitasyon** — skips the engine's standard refill so the mirror grows 1‑2‑4‑8 instead of
  inflating.
- **Inflation deflate** — cubes are pushed inward using absolute coords so the shifted board
  origin is respected.
- Market elemental blocks are never 1×1 (they re-roll to a minimum size).

### Tools / internal
- `Tools/RarityGrader/` — the grader page, its generated data, and `rarities.json`.
- Local multi-agent coordination via `AGENT-COMMS.json` (git-excluded).

<!-- Add new entries at the top of the relevant section as work lands. -->
