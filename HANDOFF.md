# Handoff — remaining work on `balance`

Handoff for continuing the joker/power/UI/retro batch. Read `CLAUDE.md` first (design +
conventions), then this. Latest pushed commit at handoff time: **`5e6e690`**.

## Progress (session 2026-07-20) — all 12 items addressed, pushed to `balance`

- **#1 Combo bonus** — `edec957`
- **#2 Market reroll** — `aee725e`
- **#3 Designer fixes (a–e)** — `7a70bfe` (drag-paint, element colours, connected-shape check,
  no ghost/gear) + `fa3d155` (custom card tag)
- **#4 Genel Temizlik** — `fad9c44` (description only; the mechanic already recharged powers on
  external sweeps via `RechargeAll`)
- **#5 Hover details** — `3ece746`
- **#6 Hileli Zar UX** — `f770695`
- **#7 Büyüteç consumable reveal** — `fc2de05`
- **#8 İkinci Şans hand redraw** — `43a67af`
- **#10 Retro SFX** (CRT hum + bit-crush) — `2bb2718`
- **#11 Falling-piece controller** — `d9db0c4` (FIRST DRAFT; fall speed / lock feel need Unity tuning)
- **#12 (partial)** board-power blast FX for Bardağın/Çerçeve — `38f9177`
- **#9 CRT edge-bend shader** — `e86927f` (shader + `_CrtBend` toggle shipped; needs ONE Editor
  step to wire the Full Screen Pass feature — see `docs/crt-edge-bend.md`)

### Still open / needs verification
- **Everything needs a Unity compile + playtest** (no .NET SDK on this machine; changes were only
  brace-checked). Run `Tools/CoreTests` (incl. `-- baseline`) once an SDK is available.
- **#9**: do the one Editor wiring step in `docs/crt-edge-bend.md`. If `Blit.hlsl` include path
  errors on this URP version, swap the one include (doc lists the alternate path).
- **#11**: real-time feel (fall interval, lock delay, DAS) wants iteration. In retro mode the drag
  path is bypassed, so clicking the draw/discard piles and right-click fox/rotate are not wired
  into the falling flow yet. A power that redraws the hand mid-fall could desync `retroFallHand`.
- **#12 inflation deflate crush FX — DEFERRED (deliberately).** Two blockers found: (1) the crush
  uses `Board.DestroyCubeForced` directly in `RoundEngine.ShiftColumnInward`/`ShiftRowInward`
  (bypassing `DestroyCubes`), and it runs *in-turn* (in `AfterTurnScored`, `currentReport != null`),
  so the handoff's "external destruction / only-with-Genel-Temizlik" premise doesn't hold; (2) the
  crushed cubes sit in the band the shrink REMOVES, and by the time the view sees the turn the board
  has been rebuilt smaller, so the old cell→world mapping is gone — correct FX needs the pre-shrink
  geometry. Both need a Unity build to get right. Bardağın/Çerçeve (no resize, already routed
  through `DestroyCubes`) were the safe, geometrically-correct half and are done (`38f9177`).

---

## How to work in this repo (important)

- **No .NET SDK on the dev machine.** You cannot compile or run `Tools/CoreTests` here.
  Verify changes two ways: (1) a brace/paren balance check with a small Python script (see
  the `scratchpad/bc*.py` pattern — strip comments + string literals, then count `{}()[]`);
  (2) **the user playtests in Unity and reports bugs** — that loop is the real verification,
  so keep changes reviewable and land them in small commits.
- **Multi-agent checkout.** `AGENT-COMMS.json` at the repo root is local-only (git-excluded).
  Read it before editing; register the files you hold; `git pull --rebase origin balance`
  before pushing; stage files deliberately (never `git add -A`). A second agent
  (`opus-scoring`) did the scoring/economy rework (commit `8678cbc`) — it's idle now.
- **Commits:** lowercase one-line messages in the existing style, **no co-author trailer**.
  Work in meaningful chunks, one feature per commit, push each. Keep `CHANGELOG.md` updated
  as work lands.
- **New scripts under `Assets/`** need a `.cs.meta` (2 lines: `fileFormatVersion: 2` + a
  fresh 32-hex GUID). Generate with `python -c "import uuid; print(uuid.uuid4().hex)"`.
- **Bilingual text:** every user-facing string uses `Loc.Pick(en, tr)`; joker/power text uses
  `SetDescription(en, tr)`. The legacy TextMesh font renders Turkish (ş, ğ, İ, ç, ü) but NOT
  arrows/box glyphs (▲▼•↑↓) — use ASCII in world-space labels.
- **Board coords:** the board is a bounding box + playable mask with a MOVABLE origin
  (`MinX/MinY` can go negative after inflation). View code must convert absolute↔local via
  `MinX/MinY`. This has caused several bugs.

---

## Already shipped this session (see `CHANGELOG.md` for the full list)

Kolay Para, Halüsinasyon, Karakter Oluşturma (block designer), Retro power (toggle + CRT
overlay + placement bonus + any-block rotation), Batak moved joker→power, the in-game Rarity
Grader (F2) + **rarity-driven pricing & shop odds**, void cubes surviving sweeps, and the two
fixes (CRT resets on restart/deck-change; Totem shows the market). Rarity grades live in
`Assets/Scripts/Core/Game/RarityTable.cs` (baked from `Tools/RarityGrader/rarities.json` — if
grades change, regenerate with `scratchpad/gen_rarity_table.py`).

---

## Remaining queue

Ordered roughly by priority. Each item: **what**, **decisions locked**, **where**, **gotchas**.

### 1. Combo bonus (design locked)
- **What:** a point bonus for **consecutive line-clear turns** that **stacks**. Each turn that
  explodes ≥1 row/column continues the combo (combo×1, ×2, ×3…, adding a growing bonus); a turn
  that clears no line resets the combo to 0.
- **Where:** this is Core scoring. Add a combo counter to `RoundEngine` (persists across turns
  within a round; reset at round start). In `RoundEngine.ResolvePlacement`, after the line
  explosion step, check whether any line cleared (`report.ExplodedRows.Count +
  report.ExplodedColumns.Count > 0`); if so increment combo and add `combo * ComboBonusPerStep`
  to the score, else reset combo to 0. Add `ScoringConfig.ComboBonusPerStep` (logical/small; the
  ×10 `ScoreScale` lifts it). `BereketJoker` (ScoreJokers.cs) shows the `ExplodedRows/Columns`
  read pattern.
- **Gotchas:** there is already a `comboStreak` field in `GameUiController` — check what it
  currently drives (likely a View FX) before reusing the name; the SCORING combo belongs in
  Core, not the View. `RedrawHand` must NOT count as a turn (it doesn't score). Decide overtime
  behavior (default: keep applying). Expose the current combo on `TurnReport` if you want a UI
  popup.

### 2. Reroll (design locked)
- **What:** one **Reroll** button in the market that refreshes ALL offers (blocks + jokers +
  powers) at once; cost **escalates** each reroll within a market visit and resets on the next
  visit.
- **Where:** `GameSession` owns the market. `RestockMarket()` builds offers via `AddJokerOffers`
  / `AddPowerOffers` / block loop, using a deterministic rng seeded `resolvedSeed * const +
  RoundNumber`. Add a `RerollMarket()` that regenerates offers with a seed that also varies by a
  **reroll counter** (so each reroll differs), charges the run currency (`TotalScore`, spent via
  the same path buys use), and increments the counter. Reset the counter on market entry
  (`OnRoundStatusChanged` → Advanced, and `LeaveMarket`). Add `MarketConfig.RerollBaseCost` +
  `RerollCostStep`. UI: add a reroll button to `MarketView` (+ show its current cost) and a click
  handler in `GameUiController.HandleMarketClick`.
- **Gotchas:** prices are `base * PriceMultiplier(rarity) * ScoreScale` — keep that. Don't
  disturb the main rng stream (joker/power stocking uses its own SeededRandom — do the same).

### 3. Character-creation designer fixes
Files: `Assets/Scripts/View/BlockDesignerView.cs` + the designer modal in `GameUiController`
(the `blockDesigner.IsOpen` block in `Update`, and `ConfirmBlockDesigner`).
- **Click-and-drag to paint:** currently a single click toggles one cell
  (`ToggleCellAt`). Add drag painting: on first press decide paint-vs-erase from the first cell,
  then while `leftButton.isPressed` paint/erase cells under the cursor. The modal currently only
  handles `leftButton.wasPressedThisFrame` — add an `isPressed` drag path.
- **Single connected piece:** on Confirm, reject shapes that aren't one 4-connected group (keep
  the designer open, maybe flash). Add a connectivity (flood-fill) check on the toggled cells.
- **Drop ghost & gears from the palette:** remove `BlockElement.Ghost` and
  `BlockElement.Mechanical` from `BlockDesignerView.Elements` — they don't make sense for a
  designed block.
- **Element color/texture on painted cubes:** when an element is selected, render the filled
  grid cells in that element's colour (`ViewUtil.ElementColor` / `ViewUtil.CubeDisplayColor`) so
  the preview matches the board.
- **Card type shows "custom":** the created card should display its type as **"custom"** (Loc
  `custom`/`özel`) wherever card element/type tags render (market tile tags — see the
  `16d1380` localize-market-tags commit — and `CardVisual`). Needs a way to identify designed
  cards; `BlockCard` has no "custom" flag today, so either add a marker to designed cards or tag
  them at creation in `GameSession.CreateDesignedBlock`.

### 4. Genel Temizlik grants power on external sweeps
- **What:** joker/power-procced (external) sweeps should **also grant/recharge a power**, and the
  descriptions should be updated to say so.
- **Where:** `GenelTemizlikJoker` (ScoreJokers.cs) flips `RoundRules.CountExternalSweeps`.
  Investigate the external-sweep path in `RoundEngine` (the "slim sweep" that counts + pays the
  bonus + recharges when `CountExternalSweeps`) — per project notes it already recharges powers,
  so confirm whether the ask is "make external sweeps recharge powers" (may already happen) vs
  "grant an EXTRA power charge." Update `SetDescription` on the joker accordingly.
- **Gotchas:** clean sweep is ONE central event (`RoundEngine.TryResolveCleanSweep`); external
  sweeps go through the same seam. Don't re-implement the sweep check.

### 5. Hover details for all jokers/powers
- **What:** hovering a joker/power panel in the bar shows its full details (DisplayName +
  Description + StatusText). Halüsinasyon should show its **current** form's details — its
  `Description` getter is already dynamic ("now X: <desc>"), so surfacing `Description` covers it.
- **Where:** there's a tooltip system already (`GrantPickerView` hover feeds a tooltip in
  `GameUiController` via `TryGetEntry`; see `tooltipRoot`, `HideTooltip`, `UpdateHover`). Add
  hover hit-testing to `JokerBarView` / `PowerBarView` (they already have `JokerIndexAt` /
  `PowerIndexAt`) and route the hovered item's text to the same tooltip.

### 6. Hileli Zar selection UX
- **What:** when picking the next round's opening hand, **highlight** chosen cards, allow
  **deselect by clicking again**, and **confirm with a button** (instead of auto-confirming at
  N).
- **Where:** `GameUiController` — `hileliPickMode`, `hileliSelection`, `TryHileliZarFromBar`,
  `ConfirmHileliZar`. The cards render in `deckOverlay` (`DeckOverlayView`). Currently clicking a
  card just adds its id (line ~254) and it auto-confirms at `hileliTarget`. Change: toggle
  add/remove on click, draw a highlight on selected cards, and add a confirm button (a rect +
  hit-test, or a key) that calls `ConfirmHileliZar` only when the count is right.

### 7. Büyüteç: reveal top 2, uncover one fewer per draw
- **What:** using Büyüteç reveals exactly the top 2 draw-pile cards; each time a card is drawn,
  one fewer is revealed (2 → 1 → 0). It's a consumable reveal, not a permanent "always show top
  N."
- **Where:** `BuyutecPower` sets `RoundRules.RevealedDrawCount` (today a static N). Make it a
  **decrementing** counter: set to 2 on use, and decrement on each draw (in the
  `RoundEngine` refill/draw path), clamped at 0. Reset appropriately at round start.
- **Gotchas:** `RevealedDrawCount` is a pure UI flag today (the core doesn't change draw order);
  the View reads it to show face-up draw cards. Make sure the decrement happens once per actual
  card drawn.

### 8. İkinci Şans also redraws the hand
- **What:** `IkinciSansPower` should redraw the hand when used (in addition to its current
  effect).
- **Where:** `IkinciSansPower.Run` (SpecialPowers.cs). Call the engine's hand-redraw. Look at how
  `RenovasyonJoker` / `İade` redraw (there's a `RedrawHand`-style path in `RoundEngine`); reuse it.
- **Gotchas:** redraw rules interact with overtime gating (see CLAUDE.md's `RedrawHand` note).
  A power redraw shouldn't hand a free discard recycle in overtime unless intended.

### 9. CRT edge-bend (real URP shader) — design locked, RISKY/UNTESTABLE
- **What:** the whole screen should **bend at the edges** (barrel distortion) in retro mode.
  This needs a real fullscreen shader — the current `CrtOverlayView` overlay can't curve the
  game image.
- **Where:** add a URP **fullscreen barrel-distortion shader** (`.shader`) + a
  `ScriptableRendererFeature`/Blit pass, and wire it into the project's URP Renderer asset
  (under `Assets/Settings/…`); toggle it with `RoundRules.RetroMode`. Keep the existing overlay
  for scanlines/vignette or fold it into the shader.
- **Gotchas:** Unity 6 URP fullscreen-pass setup is version-specific (Full Screen Pass Renderer
  Feature / `Blit`). Completely untestable here — expect a couple of iteration rounds with the
  user's Unity build. Confirm the URP renderer asset path before editing it.

### 10. CRT buzz + bit-crush SFX (retro)
- **What:** while retro is on, add a CRT **buzz/hum** and **bit-crush** the sound effects.
- **Where:** `Assets/Scripts/View/SoundFx.cs` for the buzz loop (an `AudioSource` looping a hum,
  started/stopped with `RetroMode`). Bit-crush: a small DSP component with `OnAudioFilterRead`
  (sample-rate downsampling + bit-depth reduction) on the `AudioListener` (crushes everything)
  or the SFX source; enable while `RetroMode`. Toggle both alongside the CRT in
  `RefreshAll` / `StartRoundPresentation` (where the CRT is already toggled).
- **Gotchas:** `OnAudioFilterRead` runs on the audio thread — keep it allocation-free.

### 11. Retro step 3 — the falling-piece controller (design locked, the BIG one)
- **What:** in retro mode, choosing a hand card spawns it at the **top**; the player steers it
  (←/→ move, a key to rotate, soft/hard drop); a **ghost** shows the landing; on lock it settles
  by gravity and resolves through the **existing** placement. Design locked: **hand cards fall &
  steer**; **existing scoring/clear rules kept** (gravity only chooses WHERE it lands); flat retro
  bonus already added.
- **Where:** mostly View/input. The engine is ready: `RoundEngine.EffectiveShape`/`RotateCard`
  (any block rotates in retro now), `CanPlaceCard(card, origin)`, and `PlayFromHand(handIndex,
  origin)` — the falling controller only needs to compute the gravity `origin` (lowest valid row
  for the piece's current column + rotation, via a `CanPlaceCard` scan from the top) and call
  `PlayFromHand`. New input mode in `GameUiController` (active while `RetroMode`) replacing drag,
  plus a falling-piece render in `BoardView`.
- **Gotchas:** movable board origin (`MinX/MinY`) makes the "lowest valid row" scan fiddly —
  compute in absolute coords. Real-time feel (fall speed, DAS/lock delay) needs Unity iteration.
  Untestable here.

### 12. Inflation / Bardağın explosion animation
- **What:** when the inflation powers (Yatay/Dikey/Hiper Enflasyon) and **Bardağın Boş Tarafı**
  crush cubes as they close in, play the **explosion animation** (like line explosions). These
  crushes award **no points unless the player holds the Genel Temizlik joker**.
- **Where:** the inflation powers (`InflationPowers.cs` — inflate then deflate/crush) and
  `BardaginBosTarafiPower` (BoardPowers.cs). Route the crushed cells through the blast FX
  (`BlastFxView` / `GameUiController.PlayPowerBlast`, which powers already use for previews).
- **Gotchas:** confirm current scoring — these crushes are external destruction, so points
  should only apply when `CountExternalSweeps` (Genel Temizlik) is set. Destruction must go
  through `RoundEngine.DestroyCubes` (never `GameBoard` directly) so the log/tally/sweep
  precondition stay correct.

---

## Balance placeholders introduced (all easily tuned)

- `MarketConfig`: `CommonPriceMultiplier=1`, `Rare=2`, `Legendary=3`; weights
  `Common=100`, `Rare=35`, `Legendary=8`.
- `ScoringConfig.RetroPlacementBonus = 3` (×10 ⇒ 30 effective).
- New joker/power `BaseSellValue`s: Kolay Para 40, Halüsinasyon 55, Karakter Oluşturma 55,
  Retro 50, Batak 65.
- `RarityTable.cs` is generated from `rarities.json` — regenerate if grades change.
