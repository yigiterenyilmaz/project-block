# Changelog

Notable changes to **project_block**. Newest first. Joker/power names are the in-game
Turkish names with a short English gloss. This tracks the `balance` branch (pre-release), so
everything here is unreleased and balance numbers are still placeholders.

## Unreleased — `balance`

### Added
- **Boss rounds** — every third round (3, 6, 9, 12, 15) now draws a **boss** that bends the rules
  against you for that round only. A run never fights the same boss twice, the draw is
  deterministic from the run seed, and the HUD names the boss and describes what it is doing.
  All eleven:
  - **Alıkoyma** — seizes a random card in your hand every turn (never your last one).
  - **Mapus** — seals one random empty cell per turn: nothing can be placed there, and the row
    and column through it cannot be completed either.
  - **Vanilya** — every block loses its element for the round (no fire, gold, ghost, gears, TNT).
  - **Feda** — playing a bonus card also throws your whole hand into the discard; a new hand is dealt.
  - **Tükenmişlik** — powers never refill for the rest of the round, not even from a clean sweep.
  - **Anarşi** — every rare and legendary joker and power is switched off for the round.
  - **Harcama vergisi** — each time your draw pile empties, **2 cards leave your deck for the rest
    of the run**.
  - **Özel tüketim vergisi** — using a power **permanently** costs a card from your deck.
  - **Ufuk** — only horizontal clears score (they pay a little more); columns pay nothing.
  - **Kule** — the same, mirrored: only vertical clears score.
  - **Oburluk** — if your joker slots are full, one random joker goes silent for the round; same
    for your powers. A free slot keeps everything working.
  Switched-off jokers/powers grey out in their bars (they keep their sell value), and a sealed
  cell is drawn barred on the board (a cold lock, distinct from the red scar of an eroded cell).
  Note that **Harcama vergisi hangs off the same trigger as board erosion** — an empty draw pile
  on that round costs you both cards and arena. Numbers are balance placeholders, as usual.
- **A run is now 15 rounds** — surviving round 15 ends the run in victory (`GamePhase.RunWon`,
  shown as "RUN COMPLETE" with the final score) instead of opening another market. `GameOver`
  now means a loss and nothing else, so losing round 15 is still a loss. Run length lives in
  `GameConfig.TotalRounds`, independent of the difficulty curve.
- **Boss rounds flagged** — rounds 3, 6, 9, 12 and 15 are marked `RoundConfig.IsBossRound`
  (`DefaultRoundProgression.BossRoundInterval`) and the HUD labels them `[BOSS]`. The flag is
  scaffolding only: **no boss behaviour yet**, it just gives that work one thing to hang off.
- **Board-power explosion FX** — whole-board powers that destroy board-dependent cells
  (**Bardağın Boş Tarafı**, **Çerçeve**...) now play the explosion animation on the cells they
  clear, via a between-turn destruction log the view reads. (The inflation deflate crush FX is
  still pending — it resizes the board, so the crushed band's coordinates need special handling.)
- **Retro audio** — while retro mode is on, a looping **CRT hum** plays and every sound is
  **bit-crushed** (sample-and-hold downsample + bit-depth reduction), toggled alongside the CRT
  overlay. Part of the in-progress Retro power.
- **Joker/power hover details** — hovering a held joker or power panel in the bar now pops a
  tooltip with its name, live description and status (works during a round and in the market).
  Dynamic text is surfaced live, so Halüsinasyon shows its current form.
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
  the toggle + a full-screen **CRT overlay** (scanlines, vignette, green tint, flicker), a CRT
  hum + bit-crushed audio, a flat score bonus per placement while on, rotation for *any* block,
  and a **steerable falling-piece placement**: click a hand card to drop it from the top, then
  Left/Right move, Up/X rotate, Down soft-drops and Space hard-drops with a landing ghost — on
  lock it settles through the normal placement rules (gravity only chooses where it lands).
  *First pass; fall speed / lock feel still want tuning in Unity.* Retro also adds a **dead zone**:
  an overflow area grown on top of the game area (separated by a **red line**) where clears score
  nothing — you can't turn retro off while it holds cubes, and filling the game area below is a
  loss. Blocks also **fall by gravity** in retro (like water, but every cube): after a clear, the
  stack drops to fill the gap, and a fall that completes a new line cascades. Only **full rows**
  clear in retro (no vertical/column explosions), and you lose by **topping out** — a block
  reaching the top row so nothing can drop from above, Tetris-style. A fullscreen **CRT edge-bend**
  (barrel-distortion) shader now ships too — the whole screen bows at the edges in retro mode —
  driven by the `_CrtBend` global; it needs one Editor step to wire (see `docs/crt-edge-bend.md`).
- **In-game Rarity Grader** — press **F2** in Play mode to browse every joker/power, read its
  live description, and grade it Common / Rare / Legendary (mouse-wheel or arrow keys to scroll).
  Saves to `Tools/RarityGrader/rarities.json`.
- **Rarity-driven shop** — those grades now matter: rarer jokers/powers **cost more**
  (×1 / ×2 / ×3 for common / rare / legendary) and **appear less often** in the market
  (draw weights ~100 / 35 / 8). One legendary joker held at a time, as before. Numbers are
  placeholders in `MarketConfig`; grades live in `RarityTable` (baked from the grader).

### Changed
- **Batak** (power) — the bet is now a **two-digit locker** (00–99) instead of a 1–8 list: spin
  each dial with the mouse wheel or its +/- buttons, then **Bet**. Higher bets are safer but pay
  less (a bet past the payout cutoff earns nothing), as before.
- **Karakter Oluşturma** (block designer) — nicer to draw with: **click-and-drag to paint** (the
  first cell decides paint vs. erase), filled cells now show the **selected element's colour**,
  and Confirm **rejects shapes that aren't one connected piece** (with a warning) instead of
  baking a scattered block. Ghost and gear (Mechanical) are dropped from the element palette.
  Designed blocks are now tagged just **"custom"** on the card. The element palette drops TNT and
  Fox (as well as the earlier Ghost/gear) — none make sense for a hand-drawn block — and a block
  is capped at **5 cubes**.
- **Hileli Zar** (power) — the opening-hand picker is now deliberate: click to select/deselect
  cards (chosen cards are highlighted), and commit with a **CONFIRM** button that only lights up
  once exactly the right number is picked, instead of auto-confirming on the last click.
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
- **Big hands wrap** — a hand larger than 8 cards (e.g. **İmitasyon**, whose hand mirrors the
  discard) now wraps into extra rows stacked upward instead of running off the screen.
- **Retro bit-crush** — the bit-crush now actually grits the sound effects: the filter moved onto
  the **AudioListener** (the camera) so it processes the whole mix. On an AudioSource-only object
  `OnAudioFilterRead` was not reliably called, so SFX played clean.
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
