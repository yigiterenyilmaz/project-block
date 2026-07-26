# Changelog

Notable changes to **project_block**. Newest first. Joker/power names are the in-game
Turkish names with a short English gloss. This tracks the `balance` branch (pre-release), so
everything here is unreleased and balance numbers are still placeholders.

## Unreleased — `balance`

### Changed
- **A round's score is now capped at its own threshold.** The turn that crosses the bar takes
  you *to* it and no further — a clean sweep that would have carried 600 past a 650 bar all the
  way to 1200 now banks 650, and the run currency is capped with it, so the money can never
  outrun the meter that earned it. **Overtime is the only way past the threshold**, which is
  what overtime is for. A turn that does not reach the bar banks every point as before.

### Fixed
- **A turn can no longer push the round score backwards.** Negative score from an end-of-turn
  effect was landing on the round score directly, where the existing zero-floor could not see it —
  so a big enough penalty could undo score the player had already banked. Now clamped centrally:
  a turn may be worth nothing, never less.

### Added
- **Kaçakçı (joker)** — **one market item per visit, free** (SHIFT+click an offer) — but smuggled
  goods are **defective about half the time**, and you keep whatever you took. A junk **block** is a
  broken-up shape spanning 6x6: it does not fit a 5x5 arena *at all*, and on a bigger one it needs
  four exact cells right across the board, so a hand slot holding it is a slot you do not have that
  round — for the rest of the run. A broken **joker** is either dead in every **boss round** or dead
  outright, and it still takes up its slot. A broken **power** arrives **empty** and fills at a
  quarter of the rate, for good. The defect is visible immediately — on the card's band, the joker's
  status line, the power's meter — because hidden state reads as a bug and the gamble is in the
  taking, not the finding out. Smuggling counts as having shopped, so a joker that pays you for
  leaving the market empty-handed is not fooled by walking out with stolen stock.
- **Uzun Vadeli Yatırımcı (joker)** — a bet on the whole run. It is stocked **only in the first
  five markets**, does nothing for the twelve rounds after that, and pays out in exactly one
  place: the **final round**. There it is an **extra life** — lose that round and you play it
  again from the start, once — and the **key to two powers no market ever sells**. It can **never
  be sold**, so the slot is spent for the run whatever happens: reach the last round and it is the
  strongest thing you own, fall short and you paid for nothing. The do-over is a real do-over: the
  **same boss** comes back as a fresh instance, the failed attempt's round-end effects never fire,
  and the score it banked is **clawed back**, so a replayed round can still only pay once.
  *The two exclusive powers are not designed yet* — the unlock is wired (`Power.InvestorOnly`),
  and naming them is all that is left.
- **Savunmacı (joker)** — pays you for playing it safe, but only once you finally do not. Every
  round you finish **without going into overtime** banks a bonus. Then the first overtime you do
  go into and **come out of alive** pays the **whole bank at once** — and the bank starts filling
  again, so it is an engine you can run over and over, not a one-shot. "Going into overtime" is
  you **declining** the advance offer and playing on; simply crossing the bar and taking the offer
  is a safe round. "Coming out alive" is landing the **clean sweep** that raises the offer again,
  which is the only way an overtime ends without losing the run. Reading a round as safe-or-greedy
  is the whole decision: bank patiently, then pick the round where you cash in.
- **Besleme (joker)** — marks a patch of the board in the first round after you take it, and puts
  something **alive** in it. Every cube you explode inside the patch **feeds** it; a turn with
  none **starves** it. Fed enough it **GROWS** outward (1x1 → 2x2 → 3x3) and pays far more per
  cube — but a bigger creature needs more food for the next step, runs out of patience **faster**,
  and costs far more when it **shrinks**. Starve it all the way down and it **dies**, sending a
  big bill and leaving the joker **inert for the rest of the run** — feeding it as hard as you can
  is a trap, and knowing what size you can sustain is the whole skill. It lives across rounds (it
  is coordinates, not a cube) and the patch is drawn on the board so you can see what you are
  keeping alive.
- **Kiracı (joker)** — an **elementless** block that survives **5 turns** on the board turns to
  **GOLD**. Only plain cubes qualify; anything that already has an element is not a tenant.
  Read the trade before taking it: gold pays rent every turn it stands there, but gold also
  **never breaks** and **blocks a clean sweep**, so every tenant you let settle is a permanent
  fixture. Left alone long enough this joker slowly bricks the arena it is paying you for.
  Tenancy is per cell, so a cube that gets moved (a retro collapse, an inflation squeeze, a line
  swap, the escalator boss) starts its clock over — it stopped sitting still.
- **Şifacı (joker)** — every 5 turns it gives **one use back** to a random **spent** joker. The
  clock does not run down while there is nothing to heal: if no joker is empty when it comes
  due, it stays ready and heals the moment one empties, then sleeps again. So the wait is a
  promise, not a window you can miss. Passive jokers (and the healer itself) are never patients.
- **Yer altı kaynakları (joker)** — a finite seam of fuel for your **powers**. Every 3 turns it
  refills your spent **common** powers, every 5 turns your spent **rare** ones, on two clocks
  that run independently. Each power refilled costs the seam — **1** for a common, **2** for a
  rare — out of a capacity of 10 that is **never replenished, not even by a new round**. A tick
  with nothing to refuel is free. When the seam is worked out the joker goes quiet and does
  nothing at all — but it then **sells for exactly what you paid for it**, so it is a loan of
  fuel rather than a purchase: you get your money and your slot back. Legendary powers are
  outside the seam entirely.
- **Öteki dünya (power)** — clones the board and opens the copy **beneath** it; the rest of the
  round is played across both worlds. The mirror is a copy of the arena *as it stands when you
  cast it*, so timing is the whole game: cast it clean and you get two clean boards, cast it
  full and you get two full ones. The two worlds **share the deck and the discard** — only the
  hands are separate — so a turn now costs two cards from one pile. **A turn is a card in each
  world**: you book the mirror's half, then play above, and both land together. Explode the
  **same column in both worlds on one turn** for a bonus. Each world **sweeps for itself**. A
  world with nowhere to play **sits the turn out** instead of ending the round — only both being
  stuck at once loses it (`[M]` resolves a turn with the mirror alone when the main world is the
  stuck one). The round's threshold rises by half, which is the price of the second board.
- **Devre (joker)** — at some point each round a winding **circuit** is traced from one edge of
  the board to the opposite one. It runs edge to edge and is monotone along that axis: it winds
  up and down freely but never doubles back. Fill every cell of it and the circuit **breaks** —
  those blocks explode (a real explosion: it counts toward a clean sweep and toward Kayıt
  defteri, and pays the normal per-cube rate) and you get a bonus on top that scales with how
  long the circuit was. There is **no deadline**: it waits all round, which is what keeps it
  different from Meydan Okuma. One circuit per round. A cell counts as filled if it is filled
  now **or** was filled this turn and has already blown up, so a placement that completes the
  circuit and a line at the same time does not lose the circuit to its own line clear. The route
  is drawn on the board as a chain of green nodes.
- **Kredi kartı (joker)** — shop in the market with points you do not have. Your own score goes
  first and the shortfall becomes **debt**, which gains **10% interest at the end of every
  round**. Repaying is deliberately manual (`[O]` in the market): carrying the debt one more
  round is the decision the joker is built around. If a **boss round** ends with the debt still
  open you **lose the run** — round 15 included, so surviving the last round does not settle
  your books and the final market is not a free shopping spree. The joker **cannot be sold**
  while the debt is open, so there is no walking away from it. Practical consequence worth
  knowing: since paying only happens in the market, the real deadline is the market *before* the
  boss round — what you earn during the boss round itself cannot save you. The HUD shows the
  debt next to the score, names the round you must pay by, and the run-over screen says which
  it was (`LossReason.DebtNotRepaid`, the only loss in the game that is not about the board).
- **Boss rounds** — every third round (3, 6, 9, 12, 15) now draws a **boss** that bends the rules
  against you for that round only. A run never fights the same boss twice, the draw is
  deterministic from the run seed, and the HUD names the boss and describes what it is doing.
  All twenty:
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
  - **Titizlik** — only a clean sweep scores. Placing, clearing lines, combos and gold all pay
    nothing; sweeps pay a little more than usual to make up for it. Like Ufuk and Kule it only
    rewrites the BASE values, so your jokers' own bonuses still land — it beats your board, not
    your build.
  - **Cana geleceğine mala** — every time your draw pile runs out, a quarter of your score is
    gone. Your deck is untouched; it is the purse that pays. Shares its trigger with Harcama
    vergisi and with board erosion, so cycling the deck on that round is expensive three ways.
  - **Taş ve sopa** — every joker and every power is switched off, whatever its rarity: just you
    and the board. In exchange the score threshold drops by a quarter.
  - **Alacakaranlık** — the board goes **dark** and you play blind. What you built is still
    there, still scores, still blocks — you simply cannot see it. Only an **explosion** lights
    its own surroundings, for about a second, before the dark closes back over them. The
    placement preview shows *where* a block would land but never *whether* it fits, or you could
    map the whole board by waving the cursor across it; the explosion preview, the ghost traces,
    the idle element animations and every boss marker are hidden for the same reason. It is the
    one boss that bends **no rule at all** — a blindfold, not a rule change.
  - **Karantina** — every 4 turns two more of the **outermost** rows or columns are sealed off
    (a row and a column, two rows, or two columns) and the quarantine **works inward**, ring
    after ring. A cube that explodes inside a zone **loses exactly what it would have earned** —
    but only that cube: a five-cube clear with two inside still pays full price for the other
    three, so clipping a zone is a trade rather than a disaster. Sealed lines are washed yellow
    on the board without hiding what stands in them.
  - **Yürüyen merdiven** — at the end of every turn the whole board **rides up one row**: the
    top row is carried off and a fresh empty row arrives at the bottom. Like forgetting, the ride
    is **not destruction** — it pays nothing, sweeps nothing, and nothing resists it. A row keeps
    its contents while it moves, so the escalator can never complete a line for you. What it does
    is worse: everything you build drifts towards the exit, and the room you keep getting back
    arrives at the **bottom**, where a tall block cannot use it.
  - **Alzheimer** — every turn the board **forgets** the card you played 5 turns ago: whatever
    is left of it is lifted off the arena, intact or not. A four-cube block that has already had
    three cubes blown out still loses the fourth. Forgetting is **not destruction** — it pays
    nothing, counts toward no clean sweep and feeds no tally — and **nothing survives it**, not
    obsidian, not gold, not a Parazit host. That last part is the boss's one gift: a stone you
    could never shift will eventually be forgotten.
  - **Çıkmaz** — the round played backwards: **running out of room WINS it**, while **emptying
    the board or reaching the score threshold LOSES it**. The whole round is an exercise in
    playing badly on purpose — fill the arena, clear as little as you can, and above all do not
    score well. The **automatic** dead-end rescue (Deprem) is skipped, because a joker firing on
    its own would take a win you never chose to give up; the **offered** one (Kentsel Dönüşüm)
    still appears, and declining it is how you win. If a turn both dead-ends and breaks a rule,
    the loss wins — a careless last move still kills you.
  - **Terslik** — your jokers turn on you: the points they would give you are taken from you
    instead, and piggy banks leak value instead of filling. Jokers that hand out neither points
    nor value are untouched and keep doing their job — Insider still reveals, Seri tetik still
    holds the hand open. A turn can never pay less than nothing, so the worst it can do is make a
    turn worthless; your round score never goes backwards, and a drained piggy bank stops at empty.
  Switched-off jokers/powers grey out in their bars (they keep their sell value), a **reversed**
  joker goes purple and is tagged TERS, and a sealed cell is drawn barred on the board (a cold
  lock, distinct from the red scar of an eroded cell).
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
