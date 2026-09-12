# Block Bonk

A Block Blast–style grid game with Balatro-style roguelike structure (rounds with
score thresholds, a deck of block cards, market between rounds). Jokers are in
(first wave); powers, elemental block types and the real market come later.
Unity 6 (6000.3.6f1), 2D URP, **new Input System only**.

## Run structure

A run is a sequence of **STAGES**, not just rounds: **15 numbered rounds** (`GameConfig.TotalRounds`)
with a **BOSS STAGE between every third one** — 1, 2, 3, *boss of 3*, 4, 5, 6, *boss of 6*, … 15,
*boss of 15*. **Twenty stages in all.** A boss is its own stage; it is never one of the numbered
rounds.

- A boss stage **carries the number of the round it follows** (`GameSession.RoundNumber` stays 3),
  and `GameSession.InBossStage` is what tells the two apart. `BossStageFollowsThisRound` asks
  whether one is coming.
- `DefaultRoundProgression.HasBossStageAfter` decides where they fall
  (`BossRoundInterval`, every 3rd). `GetRound(number, bossStage)` builds either stage: a boss stage
  keeps the arena of the round it follows and raises the bar instead (`BossThresholdFactor`, 1.5x).
  `RoundConfig.IsBossRound` is true **only** for a boss stage and is still the single source of
  truth for "this stage has a boss on it".
- **A market opens after every stage**, boss stages included — 3 → market → *boss of 3* → market → 4.
- The run ends on the **boss of round 15**. `IsFinalRound` means "the last STAGE", so it is false on
  round 15 itself while its boss is still to come. Surviving it ends the run in `GamePhase.RunWon`
  — no market after it, and `GameOver` stays loss-only, so anything waiting for a run to finish
  must accept **both** terminal phases.

Board size comes from `DefaultRoundProgression.BoardSizeBands` — a fixed table covering exactly
those 15 numbered rounds. **The arena is 7x7 for the WHOLE run** (designer's call, 2026-08-31); it
used to grow (1-5 on 7x7, 6-11 on 9x9, 12-15 on 11x11) and restoring that is three numbers in that
table. The bands themselves stay, because size is not all they carry: each one also names the
`ShuffleErosion` that punishes a stalling round, and those still step (rim → centre hole → both).
A late round is now a harder round in the same arena rather than a bigger one. A boss stage
inherits its round's band. Run length and that table are meant to change together.

Anything that rebuilds a `RoundConfig` from another one (a joker/power `FilterRoundConfig`) must
use **`RoundConfig.WithBoard`**, never a hand-written `new RoundConfig(...)`: a field listed by
hand is a field that can be forgotten, and both `IsBossRound` and `Erosion` have already been
dropped that way once each.

- `Assets/Scripts/Core/Bosses/` — the boss system. `BossRound.cs` is the base type, and the
  engine is the only caller. A boss is the round's ANTAGONIST: not owned, not bought, not
  sellable, drawn by `GameSession.DrawBoss` (own rng, no repeats per run) and attached to the
  `RoundEngine` before its first turn. Three rules everything follows from:
  1. **One boss per round, round-scoped.** It dies with the engine, so a boss must NEVER mutate
     session state (`RoundRules`, `ScoringConfig`) to express a rule bend — that would leak into
     the next round. Bends are **queries** the engine asks live (`IgnoresBlockElements`,
     `BlocksPowerRecharge`, `DisablesJoker/Power`, `BlocksPlacementOn`, `ScoreLineExplosion`,
     `ScoreCleanSweep`, `OnlyCleanSweepsScore`, `FilterScoreThreshold`, `InvertsJokerScore`,
     `LocksHandCard`, `HidesHandCards`).
     The ONE exception that cannot be a query is `FilterRoundConfig`: the board is built once, and
     "Dört kutup" has to change its SIZE, so a boss is **drawn before the engine exists** and gets
     to reshape the round first (via `RoundConfig.WithBoard`, never a hand-written `new`).
     The score bosses rewrite **base values only** — a joker's own bonuses always land on top.
     Read the round's bar from `RoundEngine.ScoreThreshold`, never `Config.ScoreThreshold`:
     a boss may ask for less and the two must never disagree.
     The deck taxes are the one exception: taking cards out of `OwnedCards` is their effect,
     not a rule bend.
  2. **Silencing is central**, like the overtime gate: `RoundEngine.IsSilencedByBoss` is checked
     by `JokerInventory.IsGated` and `PowerInventory`, so nothing is added/removed and no
     permanent effect gets undone and redone. Never test for a boss inside a joker or power.
     **Inverting is central too** (`InvertsJokerScore`, "Terslik"): `JokerInventory` opens a
     window around JOKER dispatch only, and inside it `ScoreBreakdown.AddFlat/AddMultiplier` and
     `Joker.Accrue` run backwards. Powers, base values and the engine's own bookkeeping share
     that breakdown and must never be caught by the window. A turn's `Total` floors at 0, so an
     inverted joker can empty a turn but never push the round score backwards.
  3. **The boss moves last** — after the player's own end-of-turn effects, but BEFORE the
     threshold and dead-end checks, so what it does can genuinely decide the round. That order is
     load-bearing twice over: a boss that ends the round on a condition ("Saatçi" running out of
     turns) must read `RoundEngine.ThresholdReached`, not `ThresholdPassed`, or it kills a round
     won on the buzzer; and a boss that restricts WHERE you may play gets `TryEscapeDeadEnd`,
     asked before the dead end is declared, so its own rule can never lock the round ("Dört kutup"
     turns to the next quarter and bills the player for the turn they could not use, "Şaşırtmaca"
     lifts the lock when the card you committed to fits nowhere).
  4. **A boss may be beaten on its OWN terms** — `RoundEngine.DeclareRoundWon` ("Matruşka"
     cracking the last doll, "Snake" dying). It banks **exactly the threshold**, sets
     `ThresholdPassed` so the turn resolver cannot also open overtime behind it, and goes straight
     to the market: beating a boss hands you the shop, not another lap. A LOSS on the same turn
     outranks it, so a careless last move still kills you. The status is settled at turn step 10
     with every other outcome, never mid-turn.
  Beware: bosses make frozen cards and sealed cells routine, so any driver that plays a card
  must skip `IsFrozen` cards and ask the board (never a raw `card.Has(...)`) where a block fits —
  and must look for origins with **`RoundEngine.EffectiveShape(card)`, never `card.Shape`**, because
  a card's shape is no longer fixed for the round ("Kıtlık" fattens every card that comes back from
  the discard, round-scoped, through the same store the fox reshape writes to).

## Layout

- `Assets/Scripts/Core/` — **all game rules.** Pure C# (`ProjectBlock.Core.asmdef`,
  `noEngineReferences`), deterministic via `IRandomSource`. Start reading at
  `Game/RoundEngine.cs` (turn state machine) and `Game/GameSession.cs` (run/rounds/market).
- `Assets/Scripts/Core/Jokers/` — the joker system. `Joker.cs` is the base type (all hooks
  are virtual no-ops), `JokerInventory.cs` is the only thing that calls them, and
  `Definitions/` holds one file per group of jokers.
- `Assets/Scripts/Core/Save/` — saving a run. `SaveGame.cs` is the only entry point
  (`Save(session) -> string`, `Load(string, template)`); Core never touches a file, so the
  platform layer decides where the string goes. `SaveFile.cs` is the positional key=value
  format, `CoreSerializers.cs` the structural types + the card table, and
  `ContentStateSerializer.cs` walks joker/power/boss fields by reflection so new content
  saves itself. See **Saving** below.
- `Assets/Scripts/View/` — disposable debug UI (runtime-generated sprites + HUD).
  Never put rules here. **Destruction is told apart by its language, and every blast passes
  its OWN colour.** A cleared LINE goes through `FlashLine` (`LineSweepView` + `LineBurstView`:
  a beam leaving the middle for both ends, escalating with the combo tier). A loose GROUP — a
  power, the sweeper, a "Hedefli" payout, a late reshape clear — goes through `FlashCells` →
  `ClusterBurstView`: a layered material break, not a particle spray. A pressure front runs out
  from the group's centroid; each cube — its face kept from the repaint that emptied the cell
  (`BoardView.TryCubeLook`) — darkens and trembles around a compact core, cracks along one of
  five preset patterns, and breaks: a faint ghost of its outline, the inner core for a few
  frames, then four debris families with their own size/speed/spin/life — major shell fragments
  cut from the cube's OWN texture (`Sprite.OverrideGeometry`) with a hot rim, secondary chunks,
  micro debris, hot specks — and a short heat residue. The shell wears the tile's own material
  (`ViewUtil.TileMaterial`), so water and fire keep moving until they break. The lab's cells hold
  no cubes, so its entries hand `FlashCells` real block faces (`AnimCubeFaces` →
  `ViewUtil.CardCubeTile`) — never the default tile tinted a colour. Detail falls in tiers with N (≤5, ≤12,
  ≤20, more) and every choice is deterministic per cell (no `Random`). **It does not move the
  camera** (designer's call: `ScreenImpulseStrength` is 0), nor does a turn whose only explosion
  it is; the 1–2 frame hit-stop freezes only the effect's own clock, never `Time.timeScale`.
  A boss taking cubes away is not an explosion either, and `TurnReport.LiftKindAt` says what
  became of each one (reporting only — `LiftedCells` itself is unchanged). A cube that VANISHED
  in place (`Removed`: "Alzheimer", "Hidrolik pres") goes through `PlayRemoval`, which draws one
  removal variant AT RANDOM per removal (repeats allowed) — variant 1 is `ColdSinkView`: the
  slot's floor opens into a three-step slate recess and the cube falls in, clipped by a
  `SpriteMask` mouth so it passes behind the front lip. Variant 2 is `PhaseFoldView`: no pit
  and no fall — the cube's volume is flattened (its face pulled toward its own average colour),
  peeled into two or three layers, pressed into a plate and folded into a narrow seam on one
  inner edge (chosen from the cell). The plate is clipped at the seam PER RENDERER by
  `Resources/Shaders/PhaseFold.shader` — a SpriteMask would show it inside the next cell's
  mask — and without that shader it falls back to plain sprites squeezed into the seam. Variant 3 is `CryoSublimationView`: the cube
  never moves — its heat is drawn out (highlights, contrast, a little saturation), frost walks in
  from the corners and edges and pushes its own colour into a last warm core, then its mass
  sublimates from its OUTLINE (never a hole in the middle: on a pale face that reads as a pit),
  leaving a brief pale haze, a thin frost shell that gives way inward, and frost dust. Both masks
  (five frost, five erosion, rank-equalised so the fronts move at a steady share of the face) are
  baked once into one small linear texture that `Resources/Shaders/CryoSublimation.shader`
  samples per renderer, turned/mirrored per cell; the vapour ribbons are pooled strip meshes on
  `Resources/Shaders/CryoVapour.shader`. Without the first shader the cube tints and fades;
  without the second there are no ribbons. A cube a MOVING board carried off
  (`Relocated`: "Yürüyen merdiven", "Merkezkaç kuvveti") is torn off along the step it was
  taking — `MomentumPeelView` ("Soğuk sökülme"): three or four laminae cut across the step
  (rounded chevrons on a diagonal — a straight diagonal cut takes the corners off as shard-like
  triangles), leading first, each stretching into a short cold streak, the trailing one snapping
  free last; clipped by the board's rect when it went over the edge, by its own cell when blocked
  (`Resources/Shaders/MomentumPeel.shader`). The direction is NEVER worked out in the View:
  `TurnReport.LiftMotionAt` carries, per carried cube, where it stood (`From`), its one-cell
  `Step`, why it could not land (`LiftReason`: ExitedBoard / NoGround / Blocked), the `Cube`
  itself (its cell may hold the cube that slid in) and whether it was the mirror world's —
  reporting only, written by `GameBoard.ShiftRowsUp(motions)` / `FlingCubesOutward(motions)`
  (the parameterless calls are unchanged; the baseline is byte-identical). The cubes the same
  move KEEPS ride to their new cells through `BossMoveView`: `TurnReport.BoardMoves` gives each
  one's `From` / `To` / `Step` / `Source` (reporting only, written by `ShiftRowsUp(motions, moves)`
  / `FlingCubesOutward(motions, moves)`). The escalator is a synchronized STEP CARRY (a tiny load,
  an eased carry, a sub-pixel overshoot), the centrifuge a RADIAL PUSH (a pixel of preload, a
  hard launch, a longer drag, a pixel of overshoot); both lift 1–2 px over a contact shadow that
  exists only while moving. `BoardView.HoldCells` keeps the destination cells blank through any
  repaint until the copies land, then `ReleaseCells` repaints them — never two cubes at once. Both
  start from `FinalizePlacement` on the repaint frame (`PlayBoardMotion`), never deferred behind
  the water, and a cube that does not survive starts its peel on the same board's launch curve
  (`BossMoveView.Launch`, no brake), so a row's survivors and casualties set off together. A cube CHANGED in
  place ("Kangren") is not drawn here at all: the rot has a system of its own, **`GangreneView`**
  (NECROTIC TAKEOVER / KURU ÇÜRÜME), owned by `BoardView` because half of it is PRESENCE — dead
  tissue on every rotten cube and a permanent band under every line the rot took whole, both of
  which outlive every repaint and every rebuild. A necrotic front crosses a cube from the side
  Core says the rot came in from (`TurnReport.GangreneSpread`: the cell, the rotten neighbour it
  crept out of, the cube that stood there, and the cubes beside it nothing can infect) and a
  little sooner at its edges than through its middle, so the last living colour is in the centre;
  the face never swaps — it drains, goes matte, cracks and gives its material up to the dead layer
  under it. An EMPTY cell is contaminated from that same side first, and then the dead mass rises
  a couple of pixels out of the floor and settles. `TurnReport.GangreneLineDeaths` reports every
  line that died in the ORDER they died, with the edge line each jump went to and the cubes it
  turned there, so a cascade plays step by step: the life goes out along the line and the cells'
  wash is swept in behind that front (`BoardView.SetRotWash` — which is why the board can withhold
  the wash the rules applied all at once), necrotic pressure travels to the edge, those cubes
  convert in place with a small stagger (an empty edge cell stays empty — the jump converts, it
  never creates), and a breath passes before the next step. Two shaders
  (`Resources/Shaders/Gangrene`, `GangreneStreak`) and ONE baked surface texture that the standing
  tissue and a conversion share pixel for pixel, so the hand-off at the end of a conversion shows
  nothing at all; without the shaders the cube cross-fades and the band is a flat plate. Palette:
  ash, charcoal and a muted dead olive — never a toxic or lime green, which would read as
  something to collect. The clean sweep (`BoardCleanseView`) and TNT (`DynamiteBlastView`) have their
  own, and so does **"Enfeksiyon"'s detonation** (`InfectionBurstView`): a block eaten from
  inside is not an impact, so it has its own sequence and neither flash nor shake. The ghost
  block goes up on the frame the cubes are removed and stands through the core's charge, veins
  run out from the ripe cell, each cube dissolves from its centre (a `SpriteMask` cutoff, no
  shader), and on the first detonation spores carry the spread to `LastSpreadCells`. The new
  cores' arrival is held (`InfectionCoreView.HoldBirth`) until those spores land, and they
  bloom without the core view's straight tendril. Light in these effects is always a gradient
  clipped to a cell or fading to zero at its own edge — never a flat tinted square laid over
  the grid, and never a full-board overlay.
- `Assets/Scripts/View/Menus/` — the menu layer (title, pause, settings, how to play, run
  summary). Unlike the rest of View this is NOT disposable: it is the real UI shell, built
  on the HUD canvas. Every screen is `MenuScreenView` with different content — do not
  subclass it — and every colour/metric lives in `MenuSkin` so art drops in by assigning a
  `Sprite` where a flat `Color` sits. `GameUiController.Menus.cs` holds the `AppScreen`
  state machine: while `screen != Playing` the menu layer owns the whole frame.
- `Assets/Scripts/View/GamepadBridge.cs` — **gamepad support, and the reason there is only ONE
  input path in the game.** The pad does not get handlers of its own: the bridge owns a virtual
  mouse and keyboard (real InputSystem devices it adds) and writes the sticks and buttons into
  them, so every existing handler goes on reading `Mouse.current` / `Keyboard.current`. Three
  consequences. **Only one device may be current**, so it writes only while the pad is the
  active device — touching the real mouse or keyboard hands the pointer back that same frame.
  **A synthesized press must survive `wasPressedThisFrame`**, which is why every synthetic
  button is held for a minimum number of frames with a forced gap behind it. And it is ticked
  from the TOP of `GameUiController.Update`, before that method reads a device — explicitly,
  not by script execution order. A new binding is one line in `BuildFrame`; the CONTEXT it
  needs (a list menu vs a pointer screen; market / round / advance decision) is passed in by
  the controller, so the bridge never asks the session anything itself.
- `Assets/Scripts/View/GameUiController.PadPlay.cs` — the **DIRECT** gamepad scheme (the other
  one is the cursor above; the player picks in SETTINGS). It steps through the hand, walks a
  block across the arena a cell at a time and holds a shoulder to reach a bar. It does not
  invent a second way to SEE the game, only a second way to DRIVE it: the pointer is still
  real, and the bridge SNAPS it onto whatever the pad has selected
  (`PointerMode.Snapped` + `PadSnapScreen`), so every hover visual and tooltip follows for
  free. What it must never do is fake a click — the buttons call the same methods the mouse
  handlers call (`PlayFromHand` → `FinalizePlacement`, `BeginActivation`,
  `RunPowerActivation`), exactly as the retro falling-piece controller already does. **The
  MARKET is direct too** (`HandlePadMarket`) and is stepped rather than pointed at, and every
  one of its verbs gets a BUTTON instead of a place to click. A direction is resolved
  **spatially** (`PadOfferInDirection`, off `MarketView.OfferWorldCenter`), never by walking the
  offer list: the shelf is not a row — blocks run down the left column with jokers over powers
  on the right — so the tile before the first power in the LIST is a joker while the tile to its
  left ON SCREEN is a block. Asking the layout is also what survives the shelf being rearranged. It owns the frame in the
  market and in an in-progress, non-retro round with nothing modal over either
  (`PadDirectPlayable`); everywhere else the pad falls back to the cursor, and
  `HandlePadRound` returns true only on the frames it actually acted, so the mouse and the
  debug keys keep working beside it.
- **A prompt that names a control goes through `PadOr`** (in `.PadPrompts`), never a bare
  `Loc.Pick("[A] advance...")`. The game is full of key names printed over the board, and every
  one of them is wrong for a player on a pad — so they are written once and say the key, the
  cursor scheme's button, or the direct scheme's, depending on what is driving. A view that
  cannot reach the controller takes a `Func<string>` instead (`MarketView.ProceedHint`), because
  the answer changes the moment a stick is nudged. **L3 hides the prompt strip**, and nothing
  else may claim it.
- `Assets/Scripts/View/GameUiController.PadPanels.cs` — **direct mode uses NO cursor
  anywhere**, so the panels are stepped too: the collection overlay, the deck pick, the option
  picker and the dead-end rescue. They all work the shelf's way — a panel exposes a count and a
  WORLD CENTRE per item, `PadNearestInDirection` picks what a direction leads to, the pointer is
  snapped onto it so the panel's own highlight and tooltip follow for free, and A calls the
  method a click there would. Adding a panel is those two accessors plus a case in
  `PadPanelDriven`/`PadPanelSnap` and a handler.
- `Assets/Scripts/View/GameUiController.PadPrompts.cs` — the strip along the bottom that says
  what the pad can do RIGHT NOW, shown only while one is driving. Its one rule: every line is
  written beside the branch that answers those buttons, because a prompt that has drifted from
  its binding is worse than none. **The pad never carries a debug key** — J/K/P/G/R/S and the
  F-keys stay on the keyboard, and neither scheme nor this strip mentions them.
- `Assets/Scenes/enes.unity` — the working scene (a single `GameBootstrap` object).
  **Only ever modify this scene**, never SampleScene or the URP template.
- `Tools/CoreTests/` — console test harness (outside `Assets/`, so Unity ignores it).
- `docs/jokers-plan.md` — classification of all 31 planned jokers, the central rule
  rulings, and the open design questions. Update it as jokers land.

## Conventions (follow these)

- Every file starts with a `// PURPOSE:` header; extension points for future mechanics
  are marked `EXTENSION POINT`. Keep both up to date when editing.
- No `UnityEngine` and no un-seeded randomness inside `Core`.
- Rules that jokers/powers may bend live in mutable config objects (`RoundRules`,
  `ScoringConfig`) that the engine reads live — don't cache their values.
- Numbers in `ScoringConfig` / `DefaultRoundProgression` / the joker fields are balance
  placeholders; the flow around them is confirmed design. One exception: a run is 15 numbered
  rounds with a boss stage between every third (20 stages in all), and the board-size table
  (`DefaultRoundProgression.BoardSizeBands` — 7x7 for every round; see above) is confirmed
  design, not a knob to tune. Surviving the BOSS OF ROUND 15 wins the run
  (`GamePhase.RunWon`); `GameOver` is loss-only, so anything waiting for a run to finish must
  accept both.
- **The threshold is a CEILING for normal play.** A round banks at most its own
  `RoundEngine.ScoreThreshold`: the turn that crosses the bar takes the score TO it and drops
  the excess, from the run currency too (`CapScoreAtThresholdOnCrossing`, called at BOTH
  crossing points — in-turn step 9 and `AddScoreOutsideTurn`). Scoring past the bar is what
  **overtime** is for, and the only way to it. So `TurnReport.ScoreGained` is what was BANKED
  while `TurnReport.Score.Total` is what the turn EARNED; they differ only on the crossing turn.
- **A TURN IS NEVER WORTH LESS THAN NOTHING.** Negative score is real ("Terslik" inverting every
  joker, "Besleme" billing you for a starving creature) but it may only eat what the turn earned.
  Two guards, and both are needed: `ScoreBreakdown.Total` floors at 0 for score settled before
  finalization, and `RoundEngine.ClampTurnScoreFloor` puts `RoundScore` back to where the turn
  started for everything added AFTER it (`AddLateTurnScore` writes to `RoundScore` directly and the
  `Total` floor cannot see it). `report.ScoreGained` is re-derived from the clamped delta, so the
  run currency follows. It runs at turn step 8.6 **and again after every late write**, because some
  land after that step — the dead-end check is later than 8.6, and "Dört kutup" bills you there.
- **Board erosion is the anti-stalling clock.** Each band also names a `ShuffleErosion`: past
  `RoundRules.FreeDeckRecycles` (2), every time the draw pile runs DRY the arena loses a piece —
  the rim (1-5), a growing centre hole (6-11), or both (12-15). It is counted in
  `RoundEngine.DeckRecycleCount`, NOT `RoundDeck.ShuffleCount` (that also counts reshuffles the
  rules and jokers order), and applied once centrally at turn step 8.5. A cell eaten this way
  (`GameBoard.MarkDead`) KILLS its row and column — unlike a plain hole in the bounding box,
  which is merely skipped. Never conflate the two.
- Turkish design terms → code names: el = `Hand`/turn, çekme destesi = `RoundDeck.DrawPile`,
  ıskarta = discard, oyun destesi = `GameSession.OwnedCards`, raunt = round,
  temizlik = clean sweep, bonus el = bonus hand, eşik = `RoundConfig.ScoreThreshold`,
  uzatma = overtime (playing on after the threshold), güç = power, ihale = auction.

## Joker rules (three decisions everything else follows from)

1. **Order is inventory order.** Every dispatch walks the jokers left to right
   (acquisition order). Score composes as: base values → all flat bonuses → all
   multipliers → floor once (`ScoreBreakdown`). A joker never overwrites another's value.
2. **Clean sweep is ONE central event.** Only `RoundEngine.TryResolveCleanSweep` may fire
   it, at most once per turn, and only when this turn's destruction emptied a board that
   was not already empty. Effects that can trigger a sweep call it; they never re-check
   the board themselves. Note this is stricter than "a line exploded": a full line of
   indestructible cubes destroys nothing, so it no longer re-triggers a sweep every turn
   once obsidian/gold sit on the board. **A line that would destroy nothing is not an
   explosion at all** — `ResolveFullLines` drops it, so a solid gold/obsidian row stops
   paying `PointsPerLine` and stops flashing every turn for the rest of the round. That is
   the same reasoning applied at the source, and it is what keeps score, animation and the
   sweep pre-condition agreeing on what an explosion is.
3. **Overtime disabling is central.** A joker sets `DisabledInOvertime` and
   `JokerInventory` skips all of its hooks once `ThresholdPassed`. Never write
   `if (overtime)` inside a joker. Overtime itself follows the continue-cost rule
   (declining an offer reshuffles the hand and removes an escalating number of cards),
   so anything that hands the player a free discard recycle there — like `RedrawHand` —
   must be gated. That is why Renovasyon is overtime-disabled and İade is not.

4. **Destruction goes through the engine.** `RoundEngine.DestroyCubes` (and `ForceCleanSweep`
   / `DeclareLoss`) rather than `GameBoard` directly, so the destruction log
   (`TurnReport.DestroyedCubes`, with each cube's kind and source card), the countable
   tally and the sweep pre-condition all stay correct. A joker that opts out of counting
   (Buldozer) passes `countsForSweep: false` — that is also what keeps it out of
   "Kayıt defteri"'s ledger.

Add a joker: subclass `Joker`, override only the hooks you need, register it in
`JokerRegistry`. It appears in the debug joker bar automatically. Jokers do NOT subscribe
to `TurnResolved` — that event stays a post-fact notification for the UI.

The roster now stands at **52 jokers, 35 powers and 37 bosses** (registry counts); of the
originally planned powers only "Dolly" is left, set aside by the designer.
See `docs/jokers-plan.md`.

**A board cell has FOUR states, not two.** `GameBoard` is a bounding box plus masks:
*required* (plain play area), *optional*, *hole* and *dead*. A **hole** is skipped by the
fullness check and cannot be built on; a **dead** cell (`MarkDead`, shuffle erosion) cannot be
built on either and KILLS its row and column; an **optional** cell is the only one that is
playable without being required — you may build on it, but a line does not wait for it while it
stands EMPTY, and a cube left standing there never blocks a clean sweep. "Tılsım" is what
grants them (`RoundConfig.OptionalPlayableCells`, a list kept separate from
`ExtraPlayableCells` precisely so bonus ground and "Kentsel Dönüşüm"'s permanent board cannot
be confused). Bonus ground may never hold a line up — and by the same token may never conjure
one: a row that is nothing but optional cells is not a row, however full of cubes it is. A cube
IN one is ordinary in every other respect: it explodes with the line and it scores. The mask
travels through `CreateResized`, `CreateClone` and the save file like `WaterFlow` does.

**Water does not always fall downward.** `GameBoard.WaterFlow` is the one-cell step water
settles along — `(0,-1)` on every ordinary arena, turned to any of the four sides for the rest
of the round by the "Kütleçekim merkezi" power (`RoundEngine.SetWaterFlow`, which also makes the
water already on the board obey at once). It lives on the BOARD, not in `RoundRules`, because
that is what round-scopes it: a fresh arena every round means gravity stands back up on its own.
Carried across `CreateResized` and `CreateClone`, so an inflation power cannot reset it and the
mirror world inherits the same pull. Anything that reasons about where water ends up must read
`WaterFlow` rather than assuming down.

**Block types are not all card-wide.** Most elements colour the whole block, but "Hedefli"
marks exactly ONE cube of it (`BlockCard.TargetCellIndex` -> `CubeKind.Target`, stamped by
`GameBoard.Place`), and the rule that follows lives in `RoundEngine.Targeted.cs`: whichever of
the block's cubes breaks FIRST decides everything - the target pays a bonus and takes the block
with it, anything else spends the block for that placement. "First" is judged per DESTRUCTION
BATCH, so a line that takes the target along with two plain cubes is a hit. It is settled from
`LogDestruction`, the ONE place a destruction is noticed, so every source (line, fire chain,
joker, power, boss, between-turn or in-turn) feeds it without knowing the rule exists. The index
is into the EFFECTIVE shape, so a rotation or a reshape moves the mark with the cube the player
was shown.

**Market credit ("Kredi kartı") is a SESSION rule, not joker state.** `GameSession` owns
`Debt`, `Spend` (own points first, borrow the shortfall) and `RepayDebt` (manual, market-only);
the joker is only the switch that turns `CreditAvailable` on and names the interest rate. The
debt compounds at the end of every STAGE and a **boss stage** that ends with it open ends the run
(`LossReason.DebtNotRepaid`) — checked BEFORE the final-stage win, so the boss of round 15 can be
survived and still lost. The real deadline is therefore the market before a boss stage. A credit joker cannot be sold while it owes (`JokerInventory.CanSell`), which
is what stops the debt being walked away from.

**The block shelf has a RUN-LONG cap: half the starting deck.** `GameSession.CardPurchaseLimit`
is `Config.Deck.Size / 2`, and `TryBuyOffer` / `TrySmuggleOffer` refuse a block past it — in Core,
so no UI route can slip by. What it counts is `PurchasedCardCount`, walked over `OwnedCards`
asking each card's `BlockCard.IsPurchased`, and that indirection is the rule: a card that came
off the SHELF took a slot and gives it back when sold, while a starting-deck card (or one a
joker handed you) never took one and selling it frees nothing. Deriving the count from the deck
SIZE instead would hand a slot back for every card sold. Jokers and powers are unaffected — the
cap exists to protect the deck's identity, not the wallet.

**A deck decides what you are DEALT, not what you may be SOLD.** `DeckDefinition` carries two
shape sources: `ShapeGenerator` (the deck itself when random, plus every card minted outside the
market) and `MarketShapeGenerator`, which is what the shop stocks from — the curated decks all
share `DeckLibrary.MarketShapes`, one of each distinct piece the three of them are built from.
A shop restricted to the pieces you already own is a shop with nothing on it. Chaos names no
market pool and so falls back to its own randomness. Deck compositions (2026-09-06): Small Blocks
18 cards, nothing over 3 cubes; Classic 24, two thirds small with ten tetrominoes; Big Blocks 24,
tetromino-heavy — the set Classic used to have. **Four cubes is the ceiling for a starting deck**
— the pentominoes were cut, and Chaos is the only place a five-cube piece is still dealt.

**The sell screen prices in a POPUP, not under every card.** `DeckOverlayView.Show(cards,
sellMode)` lays out the same grid either way and carries no price labels; what a card fetches,
and whether it is a bought card or one of your own, is
`GameUiController.Tooltips.ShowDeckCardTooltip` — one popup about the card under the cursor
instead of two dozen numbers competing for the eye. Unlike the plain card tooltip it always
appears, because a plain block has no element text and its price is the whole point of the
screen.

- `Assets/Scripts/Core/Powers/` — the power system. `Power.cs` is the base type,
  `PowerInventory.cs` the only caller. Powers are ACTIVE: one charge, refilled by a clean
  sweep or a new round, at most one per turn, and using one never costs a turn.

## Saving

A run can be saved at ANY point, mid-round included, and `CONTINUE` on the title picks it up.
Three rules the design turns on:

1. **The rng is restored by REPLAY, not by state.** `System.Random` will not say where it is,
   so `SeededRandom` records the shape of the draws taken (run-length encoded) and re-takes
   them on load. Swapping in a state-readable PRNG would have changed every draw in the game
   and invalidated the baseline trace — never do that.
2. **Content state is walked by reflection** (`ContentStateSerializer`), so a new joker saves
   correctly the day it is written. Fields are name-sorted, base class first, for a stable
   order. Only primitives, enums, `Nullable<T>`, collections, structs, `BlockShape` and a
   nested `Power` are supported — anything else throws by design, and the per-content
   round-trip tests are what catch it.
3. **A version mismatch is refused, never migrated.** Bump `SaveGame.FormatVersion` whenever
   the written fields change; older files then stop being offered rather than half-loading.

Loading must NOT re-acquire jokers/powers (`AddRestored`, not `Add`): `OnAcquired` applies
permanent rule changes that the saved `RoundRules` already contains, so re-running it would
compound them on every load.

## Testing

Core compiles and runs outside Unity:

- `dotnet run --project Tools/CoreTests` — assertion suite (jokers, score pipeline,
  charges, overtime gating, plus a fuzz pass over random joker sets). Exit code 1 on failure.
- `dotnet run --project Tools/CoreTests -- baseline` — deterministic scripted playthrough
  trace. **This is the regression net for Core refactors:** capture it before the change
  (`git stash` or `git archive HEAD` into a temp dir), capture it after, and diff. Base-game
  behaviour must stay byte-identical unless the change is intentional.

Test files compile INTO the Core assembly, so `internal` members are reachable.

In-editor: open the enes scene and press Play. Drag a card onto the board to place it,
A/C on offers, N leaves market, S redraws the hand, R restarts, F feeds the card under the
cursor to a "Tamagotchi" boss. Joker debug keys:
J grants the next joker from the registry, K sells the last one, 1-9 activate (a joker
that needs a target then waits for a click, Esc cancels).

A **gamepad** works everywhere the mouse and those keys do, because it drives exactly them
(see `GamepadBridge` above): stick aims, A clicks, X right-clicks, B/Start is Escape, Y is the
stage's one verb, LB/RB are the wheel. Nudge a stick to take the pointer, touch the mouse to
get it back. The HOW TO PLAY screen carries the full table in both languages.

**F3 opens the ANIMATION LAB** — a catalogue of every animation in the game, each playable on
demand, with knobs for the conditions that modulate them (combo streak, sweep count, overtime
level, board darkness, and a 0.1x-2x time scale for watching one frame by frame). It lives in
`View/AnimationLabView.cs` + `GameUiController.AnimationLab.cs` and is the way to work on an
animation without having to reach the game state that normally triggers it.

The rule it follows: **it drives the real animation code, never a copy.** An entry calls the
same method the game calls and only fabricates the ARGUMENTS, so a retimed animation shows its
new timing there for free. That is why `CardLayerView.PlayDebugAnimation` and the small
`FlashLine` / `FlashCells` / `PlayRemoval` / `PlayForcedExit` / `PlayBoardMoves` / `PlayGangreneScene` / `LiftCells` / `FlashDynamite` / `ShakeForBlast` / `Spawn*Popup` /
`EmitSweepConfetti` seams in `GameUiController.Feedback.cs` exist — the lab and the game both
go through them. Add an animation: add one line to
`BuildAnimCatalogue`. Nothing the lab does touches the round's Core state (`TurnReport` cannot
even be fabricated), and what it paints STAYS until the RESET entry or closing the lab resyncs.
The one place it runs board code is the boss scenes, which put up a `GameBoard` of the lab's OWN
in the view and run the real rules on it. The two MOVING boards ("Yürüyen merdiven", "Merkezkaç
kuvveti" — `AnimBossLift`) run `ShiftRowsUp` / `FlingCubesOutward` and hand what they report — with
the motions the board code wrote — to `PlayForcedExit` and `PlayBoardMoves`. "Kangren" has nine
scenes of its own (`AnimRot`): one per thing the rot does, each running `GameBoard.SpreadGangrene`
(the board is built so the rot has exactly ONE cell it can take, which is what makes a press
repeatable) and `GameBoard.InfectFullLines`, whose reporting overloads are public for exactly this
— so the lab's dead lines are really dead and their bands and washes are the rules' own rather than
a drawing of them. What the lab fabricates is only the ARGUMENTS: the shape of the board, and for
the staged scenes which cell finishes the line. Then `AnimResync` puts the round's board back. The "boss hareketiyle atılma" entries do the same for one step at a time (eight
directions, a holed arena, a staged blocked target). The two "İç Hareket" entries run a moving board's turn
end for its survivors (sparse to nearly full; the centrifuge on an odd board so its centre stays
put), and their debug switches can draw each move's vector and destination cell.
