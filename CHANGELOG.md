# Changelog

Notable changes to **project_block**. Newest first. Joker/power names are the in-game
Turkish names with a short English gloss. This tracks the `balance` branch (pre-release), so
everything here is unreleased and balance numbers are still placeholders.

## Unreleased — `balance`

### Added
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
  live description, and grade it Common / Rare / Legendary. Saves to
  `Tools/RarityGrader/rarities.json`. (Grades don't yet affect prices or shop odds.)

### Changed
- **Batak** — moved from a joker to a **power**. The bet picker now opens from the power bar;
  placing a bet spends the power's charge, and any clean sweep recharges it so you can bet again.
  Payout/deadline rules are unchanged.
- **Dezenformasyon** (legendary) — the two deck halves now swap roles **every turn** instead of
  every round; the split is kept (never poured back together).
- **Scoring / economy rework** — overtime pays an escalating win bonus while regular actions pay
  almost nothing; placing blocks scores 0 by default; all money/scores use a single ×10 scale so
  the numbers read bigger without changing balance.

### Fixed
- **İmitasyon** — skips the engine's standard refill so the mirror grows 1‑2‑4‑8 instead of
  inflating.
- **Inflation deflate** — cubes are pushed inward using absolute coords so the shifted board
  origin is respected.
- Market elemental blocks are never 1×1 (they re-roll to a minimum size).

### Tools / internal
- `Tools/RarityGrader/` — the grader page, its generated data, and `rarities.json`.
- Local multi-agent coordination via `AGENT-COMMS.json` (git-excluded).

<!-- Add new entries at the top of the relevant section as work lands. -->
