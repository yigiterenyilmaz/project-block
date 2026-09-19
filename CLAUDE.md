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
  in place (`Removed`: "Alzheimer") goes through `PlayRemoval`, which draws one
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
- **"Yılan" (`SnakeView`)** — the one boss that is a LIVING THING on the board, and the first
  drawn from painted art rather than generated shapes. Six tiles in `Resources/Art/Blocks`
  (`snake_head`, `snake_head_open`, `snake_body`, `snake_bend`, `snake_tail`,
  `snake_body_swollen`), cut from one sheet at import time, and the whole system hangs off ONE
  property of them: each tile's PIVOT is on the cell centre its tube belongs to (custom pivot,
  one unit per cell in the `.meta`), so a segment is placed at a cell centre, turned in quarter
  steps, and its mouths land on the cell boundaries by themselves. The topology is DERIVED from
  the art's own connections - the bend is drawn LEFT + DOWN, and rotating it anticlockwise gives
  all four elbows, so nothing is ever mirrored and no hand-written table can rot (`TurnForBend`).
  The board does not draw snake cells at all (`BoardView.Refresh` blanks them): these sprites are
  the snake. MOTION IS ONE SPINE: Core reports the body after EVERY CELL of a slide
  (`SnakeBoss.LastTurn` → `SnakeTurnVisuals.StepSnapshots`), those cells are strung into a single
  polyline with the head's path in front, and every segment rides it at one cell's spacing - so
  the head leads, the body follows through its own shape, the tail comes last, and at the end each
  segment is exactly on the cell Core says it is in. A bend is NEVER snapped to its corner: pieces
  stay where the spine puts them, one cell of arc apart, which is the only way the body cannot come
  apart (the first version snapped, and the snake broke into floating tiles at every corner - see
  `docs/yilan-kayma.png`). A TAIL takes its direction from the tangent HALFWAY to its neighbour,
  never from the tangent at its own arc: that is the end of the polyline, and a tail just past a
  corner got a tangent across the turn, pointed off into nothing and left a gap. THE BITE IS THE END OF THE SLIDE, not a second clip, and it
  happens at the block's OWN FACE: the head comes to a stop with its snout on the block
  (`BiteLungeDistance`, nearly a whole cell short of the cell centre), draws back, opens
  (`snake_head_open`), and STRIKES over the block's near half (`BiteLungeCloseShare`) - it does not
  arrive in the cell on the lunge. The BLOCK ITSELF - its own face, kept from the report before the
  rules removed it - is pulled into the MOUTH, wherever the head has got to, and occluded by it,
  which is why it never goes through a removal variant. Then the mouth closes, a gulp passes into
  the neck, the snake pulls ITSELF the rest of the way onto the cell it has just emptied, and the
  segment behind the head swells: the snake grew. The first pass parked the head a quarter cell
  short instead, so at the moment of the bite it covered three quarters of the block: there was no
  block left to see, no contact line for an arc or a light to sit on, and every effect landed on
  the head's own snout - which is exactly what "a few sprite tweens" looks like. A cut is the
  player's line sending a constriction down the body from where it crossed
  (`CutRows`/`CutColumns`) to the tail, which lets go with a recoil and a few flecks while the new
  last segment morphs into a tail. Nothing here decides anything: which way it went, how far, what
  it ate, how many segments the lines cut and which cells left the tail all come from Core.
- **THE BITE IS ITS OWN SYSTEM** (`SnakeEatView`, `Resources/Shaders/SnakeEat` +
  `SnakeFilament`), because eating is the boss's hero moment and it turns on one distinction: we
  are not carrying the BLOCK to the mouth, we are carrying the block's MATTER there. A block that
  shrinks and slides into a jaw reads as a tile being deleted with a tween on it - which is what
  this replaced. Now its silhouette is taken away from the side the snake is on, in a few big soft
  lobes (two low-frequency cosines, never a straight wipe and never a noise dissolve), with a thin
  rim of its own energy colour where the front works and the light drawn out of the mass still
  standing; it is never scaled and never faded. What leaves it leaves along FILAMENTS - pooled
  strip meshes, one main thread and a few finer ones, each a bezier rooted in the matter that is
  STILL THERE (beyond the front, on the far side), curving into the mouth and ending BEHIND the
  head so the energy goes *into* it rather than across its face. The flow along them is baked into
  the mesh colours, so nothing scrolls a texture. How many and how bright follows how fast the
  block is actually coming apart, which is what sells the mass as conserved. The last fifth
  becomes a dense core that holds for a breath and is drunk by a final strand; a small light
  gathers INSIDE the open jaw and is shut in, never released. THE COLOURS ARE THE BLOCK'S OWN, AND THE
  SOURCE OF THEM MATTERS: a painted tile's TINT is `Color.white` (that is the whole point of a
  tile that paints itself) and on a protected cube it is a magenta wash, so a palette derived from
  `Look.Colour` came out white on everything and PINK on some of it - which is exactly what "gold
  eats magenta" was. `Look.Paint` carries what the block is actually made of
  (`ViewUtil.CubeMaterialColor`, without the protected cube's magenta state wash) and `Colours()`
  derives material / energy / hot core from THAT. There is no table of block types anywhere in it:
  gold eats gold, obsidian a deep violet, water blue - one eating language with the nuance carried
  by the colour it already wore, and nothing is ever taken to white (the ribbon's core is the same
  hue SCALED, because multiplying keeps the ratio between the channels). The old
  `BlockIngestStart` smears - three straight tapered marks at the jaw in that same white-or-pink
  tint - are gone: a straight smear IS the debug-laser look, and the filaments replaced it. The swallow then carries that colour through the snake itself: head, neck, first
  segment, each a beat behind the last, as a bulge and a tint UNDER the skin
  (`SegmentPose.AccentColour`), leaving a trace on the swollen segment - which is why "that went
  into it" reads at all. ONE CLOCK: the coil, the mouth, the lunge, the extraction, the core, the
  jaw and the gulp are one overlapping timeline in `SnakeEatView.Style`, and `SnakeView` reads the
  head's own motion off it (`HeadOffset`, `NeckGather`, `BiteClock`) - the head cannot be lunging
  while the matter waits. Nine moments are announced through `Sounded` for audio. See
  `docs/yilan-yeme-*.png`.
- **BEATEN, IT LETS GO OF WHAT IT TOOK** (`SnakeDefeatView`): the boss round won, and the only
  animation in the game whose subject is what the boss TOOK rather than what it is. It does not
  burst. Its own teal and cream go quiet, the colours of the blocks it ate this round wake up
  UNDER its surface (three pockets in `SnakeSkin`, mixed beneath the artwork so its shading and
  highlight survive - a coloured sprite laid over the segment would be a sticker, and a sticker
  says nothing about the colour having been inside), run to the middle as the body folds in
  UNEVENLY (the ends before the middle, every segment still reaching zero - scaling all of them
  faster instead left the ends gone a third of the way in and the middle never vanishing at all),
  and gather into one dense knot of a few wound strands. The knot does not explode either: it
  UNCOILS into three to six soft curved tapered ribbons - pooled strip meshes on the same
  `SnakeFilament` shader as the bite's - each one a colour it ACTUALLY ATE, each on its own arc,
  breaking into motes at their ends, with a very faint chromatic answer running out across the
  board as its cells' RIMS catch the round's dominant colour for a moment (drawn as marks of our
  own over the board, so no cell state is ever touched and there is nothing to restore). THE
  PALETTE IS THE ROUND'S: every bite calls `Remember` with the colours it derived off the block's
  own material, near colours become one family, and the one it ate most of gets the hero ribbon -
  so gold-purple-blue ends gold, purple, blue and there is no rainbow anywhere. That history is
  presentation only (no score, no behaviour, nothing saved) and is dropped when a new snake
  appears; beaten without eating anything, it falls back to the snake's own teal, cream and amber
  with two controlled accents. No flash, no shake, no confetti, and nothing is ever taken to
  white - the peak is the moment the knot opens, and it is made of colour rather than brightness.
  Six moments are announced through `Sounded`. See `docs/yilan-yenilgi-*.png`.
- **The snake's VFX are a layer of their own** (`SnakeVfxController`, `Resources/Shaders/SnakeSkin`):
  `SnakeView` owns POSES and drives it two ways - a `SegmentPose` per segment per frame, and ~20
  one-shot `Hook` signals (`MouthOpen`, `BiteContact`, `BlockIngestStart`, `GulpPass`, `CutImpact`,
  `TailDetach`, ...). Split that way because the two answer different questions: where a segment is
  this frame, and what just happened to it. Three kinds, and they do not mix. BODY is the surface
  itself, through ONE material and a `MaterialPropertyBlock` per renderer: a sheen biased to the
  art's own upper-left light in WORLD space (so a quarter turn does not turn the light), a leading
  edge while it moves, a warm accent found by the pixel's own warmth, a travelling band, squeeze,
  and the colour drained out of it. The shader only ever lerps a pixel toward its OWN brighter or
  deeper colour - that is what makes a strong response impossible to read as glow. CONTACT is what
  the board feels: shadows shaped per part and per orientation, pressure marks, and a light that
  POOLS ON THE FLOOR under a contact (`LocalLightGroundDrop`/`Flatten`) - centred on the head it was
  a halo, which is the one thing forbidden here. ACTION is the few controlled flecks a hero moment
  throws, on a budget that never grows with the snake. Sorting is the whole trick: light 1, board
  marks 2, shadows 3, proxy 4, body 5, head 6, **contact marks 7** and flecks 8 - a contact arc or
  an ingestion smear belongs OVER the jaw that makes it, and the first pass drew them under the
  head, where all that was ever seen of them was a sliver. Intensity is a hierarchy: idle barely
  there, the bite and the cut carrying real weight. The lab can switch each layer off on its own
  (motion / shadows / material / particles / board contact), which is the only way to see what each
  one is worth. See `docs/yilan-vfx-*.png`.
- **"Hidrolik pres" is a COMPRESSION, not a disappearance** (`HydraulicPressView`,
  `CompressedCubeView`, `PressureVesselView`, `Resources/Shaders/PressLamina` + `PressShell`).
  Four cubes going into one cell used to be a repaint — the cubes gone, a slate cube standing
  there, nothing in between — and the whole point of this system is that the middle of the event
  is now drawn: a 2x2 patch is taken under pressure, each occupied cube loses its VOLUME (bevel,
  contact depth, soft-3D shading suppressed by `PressLamina`, its face pulled toward its OWN
  average — never a `scaleY`, and never toward white: the average comes from `Look.Paint`, which
  is the trap the snake's palette already fell into once), becomes a thin PRESSED LAMINA that
  still wears its own material, and is drawn along a guide rail to the cell Core compressed into.
  **A PRESSED CUBE KEEPS ITS FOOTPRINT.** This is a top-down board, so "it lost its thickness" is
  said with SHADING, not with height: the first pass squashed each sprite to a fifth of its height
  and the result was a coloured bar with no relation to the cube it came from - a UI strip. The
  silhouette now stays a rounded square pressed down a few percent (`LaminaThickness` 0.86), the
  bevel is taken out of the shading (`PressLamina._Volume`: the light band on top and the dark one
  below, which are what a cube has and a plate does not), and the one piece of depth a plate really
  has is put back as a dark lip along its bottom edge (`_PlateEdge`). In the stack each lamina drops
  a shadow on the one under it, which is what makes four plates read as four rather than as one
  blob. The four JAWS are four short parts (`JawLength` 0.55 of a side) with their own contact
  shadows, never four edges meeting at the corners - a thin rectangle around the patch is a debug
  bounding box, which is what the first pass drew. And the shell does not fade in: four slate
  SHUTTERS travel in over the stack and the surface only completes behind them (`PaintShutters`),
  because a grey cube appearing where the colours used to be is the sprite swap this replaces.
  **An empty quadrant travels too**, as a dark NEGATIVE IMPRINT, because the rules store the hole
  and the picture that comes back four turns later has to have it in it. The four laminae stack
  with a readable offset for a breath, one heavy crush drives the spacing to nothing, and the
  slate shell closes OVER them and locks with a pixel of inward punch. **THE CHAMBER CLOSES ONTO
  THE ANCHOR**: Core puts the compressed cube on the patch's BOTTOM-LEFT cell, so jaws that
  squeezed to the patch's middle had the mechanism saying one thing while the material slid off
  to a corner — the four jaws now travel from the patch's centre onto that cell as the pressure
  builds. The shut cube is a PRESENCE (`CompressedCubeView`, owned by `BoardView` like the rot and
  the snake, drawn OVER the board's own slate cube so a missing shader costs detail and never the
  block): contained pressure that tightens the central dimple and the quadrant seams every couple
  of seconds and never once breathes or wobbles, and the turn it is on is said by PRESSURE AGEING rather than
  by a counter: the central dimple bites deeper, the quadrant seams tighten and darken, the outer
  bevel takes compression, the contact shadow draws in and gets heavier, and one more short
  PRESSURE SCAR - a tapered crease running out of the cross into a quadrant - locks each turn.
  Four orange dots is what that replaces, and it was a cooldown LED strip: it asked the player to
  count lights instead of reading "this capsule is holding a lot now". Amber survives only as the
  brief burnt strain that runs along a crease AS IT LOCKS, and as a trace of residue on the last
  turn; the capsule never becomes an orange cube. A turn does not arrive in one frame either - a
  ~180ms PRESSURE TICK loads the shell (its four edges press INWARD a pixel, never a scale),
  tightens the dimple, contracts the seams, locks the new crease, tightens the shadow and settles,
  and re-stating the same turn (which every repaint does) must not fire it. The number itself is
  the POWER's `TurnsLeft`, never counted in the View: the power sets it mid-turn and decrements at
  the end of that same turn, so the states the player sees are 4, 3, 2, 1 and the count is
  `total - TurnsLeft + 1`, running 1..4. They lock in the quadrants' own patch order, and the idle cycle quickens
  with the load (3.1s down to 1.8s) because the capsule is nearer its limit, not more alive. Opening runs backwards: the locks let go, the stored colours come up UNDER the seams for
  a moment (mixed beneath the slate — a coloured sprite over it would be a sticker), the shell
  retracts, and each lamina expands to the cell CORE restored it to, regaining its volume on the
  way; a stored hole comes back as a hole and never as a cube the View invented. **NOTHING IN IT
  IS DECIDED HERE.** `PressCompressionVisuals` / `PressReleaseVisuals` (written by
  `GameBoard.Press`, reporting only, `[NotSaved]` on the power's copies — the snake's report
  crashed saving once already) carry the four cells in patch order with a null per empty quadrant,
  every side the press PRESSED including the ones that refused and the cube and KIND that refused
  them, every cube it moved with its own step and a destination that is outside the board when it
  went over the edge, the axis the corner finally opened on, and exactly the cells a failure
  emptied. The reporting overloads are additive: the parameterless calls are the ones the rules
  have always made and the baseline is byte-identical.
  **THE REAL RULES ARE NOT SYMMETRICAL AND THE ANIMATION MUST NOT PRETTIFY THAT** — the anchor is
  restored in place, the cell to its RIGHT is pushed +x, the one ABOVE it +y, and only the
  DIAGONAL has a choice (horizontal, then vertical). So "a side is shut, so it opens the other
  way" happens at the CORNER and nowhere else; a blocked straight side detonates at once. A
  refusal is pressed and denied — a pixel of shell pressure, a compression mark on the face of the
  cube that will not budge (deeper on obsidian than on gold), zero movement, a pixel of recoil —
  and then a dull amber line carries the pressure through the shell to the axis that opened. No
  shield, no spark, no glow. A push is a directional pressure front (a low-opacity slate band over
  the floor, never a beam or a ring), the near cube answering first off the report's own `Order`,
  hydraulic easing rather than a lerp, a percent or two of compression along the axis and a shadow
  that lags; a cube shoved off the board keeps its velocity and goes, one movement, never a stop
  at the rim followed by a removal. Destination cells AND the restored 2x2 are held blank
  (`BoardView.HoldCells`) until the copies land, the same bargain `BossMoveView` makes.
  **The failure is its own event** (`PressureVesselView`), because it is the one thing in the game
  that removes gold and obsidian and it pays nothing: the seams darken, the dimple is driven in,
  a burnt amber stress comes up, every edge strains a pixel or two out — and then the shell does
  NOT burst outward, it collapses to the middle. That inversion is the identity; get it the TNT
  way round and nothing else matters. A short pressure knot, then an overpressure FRONT (a
  generated rounded-square ring with a cross influence — a filled plate grows into a pale wash,
  which is a screen effect) reaching only as far as the cells Core actually emptied, and the stone
  it reaches is CRUSHED FLAT rather than fractured: a thin lamina in its own material (gold an
  amber-gold, obsidian a deep violet), carried a fifth of a cell, drained, gone. No coins, no
  sparkle, no score pop, no white, no shake. Aiming has its own language too — the 2x2 is ONE
  mechanical area under a single thin slate pressure frame with four inward brackets
  (`BoardView.ShowPressPreview`), never four cells tinted the colour of an explosion, and the
  activation plays the compression INSTEAD of the cluster burst a board-targeting power would get,
  because the press destroys nothing. See `docs/hidrolik-pres-*.png` — and judge a face at the size a CELL is, never
  blown up.
- **"Tılsım" is paid for in one round and delivered in the next** (`TalismanView`,
  `TalismanShapes`, `Resources/Shaders/TalismanSpirit`). Ghost blocks may be played hanging off the
  board's edge, and the cubes that land outside persist as dead traces that no line, sweep or
  explosion ever touches. Tılsım harvests them — 15 points each — and reclaims the ground they
  occupied as BONUS GROUND. Nothing about that is one animation: the payoff lands on the NEXT
  round's board, with a market screen in between, so the power is five beats with a round boundary
  between the second and the third. **HARVEST**: not the cluster burst, which is a material break —
  each ghost lights from within in its OWN colour (from a snapshot Core takes before removing it;
  an animation cannot start from the colour of a block that has already been deleted), its shell
  comes apart into soft spectral flakes that keep that colour for a moment before turning ghost,
  and a soul kernel is left. Every ghost scores; only the ones the rules can reclaim from leave a
  seed, and "which of those is reclaimable" (the board never grows left or down) is written in
  exactly one place and carried out in the report. **CLAIM**: ONE OVERGROWTH PER CONNECTED
  COMPONENT, not one wreath per cell. **THE VINE IS A SPRITE SHEET, NOT CODE** — nine frames of one
  plant growing, at `Resources/Art/Fx/talisman_vine_sheet.png`, 5x2 in reading order. It replaced a
  drawn root system (a four-level hierarchy of tapered tube segments with forks, tips, crowns and
  contact shadows) that was rebuilt three times and never stopped reading as a wire diagram; the
  art settled it in one pass. What survives from that work is the CHOREOGRAPHY, which is the part
  that was doing the work: the cells are ordered by distance from the board and staggered, so the
  cover visibly crawls OUT from the board's own edge (there is still a rune where it leaves).
  The source art was nine drawings scattered on one canvas, and repacking it is where the
  animation is won or lost: **every frame is planted on the same FOOT** — the cut end of the stem,
  which becomes the sprite pivot — so the base stays nailed down while everything above it reaches
  out. Centre the frames instead and the plant slides across the board while it grows, which reads
  as nine different vines being swapped rather than one growing.
  **AND IT IS PLACED BY ITS MASS, NOT BY ITS FOOT**: the drawing's bulk sits 0.53 frame-heights
  from the stem's cut end, 24° above its own base line, so anchoring on the foot lands the mass a
  cell and a half from the cell it is meant to bury; the foot is solved BACKWARDS from where the
  mass has to land. A mirrored copy carries its mass on the other side of its own axis, so the
  lean flips with it. That also buys the growth for free — the early frames are small and still at
  the foot, so the vine reaches IN over the cell instead of swelling on top of it. **THEY STAND IN RANKS, SIDE BY SIDE** —
  one rank per layer of depth, spaced across it, every other rank offset half a space, all facing
  the same way, and the direction SNAPPED to an axis so the cover comes straight in off the board
  edge rather than spilling in at 37°. Mirroring alternates rather than being random, which reads
  as a weave; the random turn on top is small (±14°), because the whole point of a rank is that it
  has a direction. A 2x2 claim gets six vines and a single cell two.
  **THE PASS BEFORE THIS ONE OPTIMISED THE MEASURABLE THING AND LOST THE READABLE ONE.** It put
  five vines on every cell spread evenly round the circle, and it measured beautifully — 97% of
  every cell buried, against 72% for three in one direction. On the board it was a ball of snakes:
  a fan that covers every direction equally has no direction in it, and twenty of them at 0.42s
  each is not a plant growing over something. Coverage is a CONSTRAINT, not the objective.
  What hides the ground is not the vines at all, and the layer that does it has now failed three
  times the same way. It was a per-cell blob (a row of pale circles with plants on them), then a
  near-black one, then **ONE soft field over the claim's bounding box** — and that last one is the
  instructive failure, because on paper it is the right idea: a single mask, an irregular rim, no
  grid anywhere. On the board it was **a big transparent rounded rectangle with a bouquet on it**.
  Two things did it. A wobble on a rim is a decoration ON a rectangle, so the rectangle survives
  it; and a field that covers a bounding box says nothing about WHICH cells were taken, which is
  the one thing this layer exists to say.
  So nobody tries to draw an organic shape any more. **THE CURSE STAIN** (`TalismanStain`) is
  ASSEMBLED from four dull pieces that know only the gameplay set: a PATCH per reclaimed cell (a
  rounded irregular square just under a cell across — no bevel, no border, no highlight, because
  it is a stain and not a tile), a BRIDGE per 4-adjacent PAIR (wide, 0.82 of a cell: at 0.72 two
  patches read as two blobs kissing and the cell boundary is visible in the SILHOUETTE even when
  it is invisible in the tone), a MERGE per 2x2 in the shared corner, and BLEED tongues over every
  edge with no reclaimed neighbour — rooted INSIDE the body so they swell out of the mass rather
  than sitting on its rim like beads, and deliberately uneven (one wide, one a sliver, one barely
  there), which is what actually kills the rectangle. **So an L comes out L-shaped, a sparse claim
  comes out as islands, and a 3x3 with a hole comes out as a ring — none of the three is coded for
  anywhere.** They are what the field does when it is built from what the rules handed over.
  Three rules hold it together. **The parts combine with MAX into one baked field, never by being
  drawn over each other**: twenty semi-transparent quads double-blend wherever they overlap and
  the banding lands exactly on the cell boundaries, which is the seam the whole system exists to
  remove. **Depth comes from a BLUR of the finished union**, never from the part that drew the
  texel — per-part depth reads as lumps, with the bridge ovals and the cell circles visible
  straight through the tone. And **the animation is a CHANNEL rather than a timeline**: every
  texel carries WHEN it belongs to the claim (hung off `MeasureArrival`, which asks the vine
  network itself when it reached each cell), so one float plays the growth — patches opening from
  their middles, bridges closing from both ends at once and merging in the middle, tongues last —
  and the same float **run backwards is the unseal**, with the tongues retracting first and the
  bridges opening from their middles. The retraction is not written down anywhere.
  **AND THE DARKNESS IS TWO PASSES, BECAUSE THE BOARD RENDERS IN LINEAR COLOUR.** This is the
  arithmetic that defeated every earlier version: a near-black quad at the 0.28-0.38 alpha that
  reads as "quite dark" on paper lands at **0.87 of the background's sRGB luminance** — a drop you
  have to be told about. The design's own tone hierarchy asks for 0.65-0.72 through the mass,
  which needs the LINEAR value at about 0.43, and no alpha anybody would write by hand gets there.
  So the field is drawn twice off one shader: a **DRAIN** (`Blend DstColor OneMinusSrcAlpha`,
  premultiplied — a genuine masked multiply of what is already in the frame, which is the half an
  alpha overlay cannot do at any opacity) and then the **STAIN** over it, which is what pulls the
  hue and the saturation so the area comes out colder and dirtier rather than merely dimmer. The
  colour goes a breath BEFORE the darkness lands, which is the difference between the vine putting
  the light out and a decal arriving. Measured against the real backdrop: 0.67 of the background
  through the mass, 0.75 at the rim. **Two linear-colour traps are load-bearing and both are
  invisible until something looks subtly wrong**: the baked texture is created `linear: true`
  because three of its four channels are DATA (alpha is exempt from the sRGB decode, which is
  exactly why the old Alpha8 membrane never hit this), and the two tone colours are set with
  `SetVector` rather than `SetColor` because they are linear COEFFICIENTS, not swatches — a
  conversion would put the drain at roughly twice the darkening that was calibrated.
  **WHAT COVERS THE CLAIM WHERE THE ART DID NOT IS THE PLANT'S SHADOW, NOT MORE PLANT**
  (`TalismanTendril`). The honest way to cover more ground with a nine-frame drawing is to grow
  more of it, and that is exactly the pass that ended as a ball of snakes. So: SHADOW TENDRILS —
  simple dark curved ribbons that leave the branch nearest the barest place left in the claim
  (the same coverage field the vines grew against) and crawl into it, forking once at most,
  because two children is a root and three is a fern. They must never try to look like the art:
  no green, no highlight, no leaves, no tip detail — a tendril that gets pretty stops being
  support and starts competing with what it is holding up. Then VEIL POCKETS over the biggest
  holes those still leave, hung on three points of the network and sagging CONCAVE between them
  (a convex blob is a bubble sitting in the gap rather than the gap being covered), with their
  size clamped at both ends so one can never become a sheet. And during the unseal, DARK STREAMS:
  two to four per cell running from the patch into the vine that is pulling it back, which is what
  makes the ending a suction rather than a dissolve — darkness that only fades where it stands was
  never really there. All of it is soft-edged **in the VERTICES** (a ribbon is three rows, rim /
  spine / rim, with the rims at zero alpha; a pocket is two rings round a centre), so the entire
  system costs one white texture and no blur.
  **EACH VINE ALSO CARRIES ITS OWN DARKNESS, and it is not the contact shadow.** The shadow is
  tight and offset and says the vine is LYING on something; the curse depth is centred, a tenth
  wider, and says the ground under it is darker for the vine being there. Both, or the plant
  either floats or drags a smear. It is scaled about the plant's own MASS rather than its foot, or
  the swell slides off the drawing. **THE SEAL IS A CONTRACTION, NEVER A FLASH**: the gold runs
  the network once, the ground it has just passed over goes a few per cent darker behind it — the
  same growth channel, read as a travelling band, so what the gold passes is what the plant passed
  without a second timeline existing — and then the whole mass tightens two or three per cent from
  its rim and settles. The idle is that band again at a twentieth of the strength, every several
  seconds; there is no whole-area pulse, because a region that breathes as one is a UI element.
  **The claim is no longer stencilled to the darkness.** The branches used to be clipped to the
  membrane, which had a bounding box's worth of room to spare; the stain stops a sixth of a cell
  past the cells, so the same stencil would cut the hero art off mid-stem.
    **SIZE WENT 1.7 → 1.4 → 0.95 → 0.75 AND LANDED ON 1.2, AND THE WAY IT LANDED IS THE POINT.**
  Four passes were spent shrinking it against screenshots and memory, which is not comparing it
  against anything; the lab was then given a live cycle (0.60 / 0.75 / 0.95 / 1.20 / 1.40, spacing
  moving with it) and the answer arrived in one minute. When a number is being argued more than
  twice, stop tuning it and build the way to see it. What that cycle also separated is SIZE from
  DENSITY, which had been moving together the whole time and hiding which of the two was actually
  wrong: 1.2 was right and there were simply twice too many of them. A plant whose curl is wider than the blocks beside it is not undergrowth on the board,
  it is a creature on top of it, and every extra tenth of overhang buries more of the playable
  board next door. Smaller pieces and more of them keeps the coverage, tightens the claim's
  outline and leaves the individual plant legible instead of being one arm of something huge.
  What must hold while that number moves is the RATIO: the spacing stays around two thirds of the
  reach, so every vine still spills past its neighbours — pieces sized to their own station are a
  grid of badges however good the art — while the gaps between them stay wide enough that the
  stain shows through, which is what undergrowth actually looks like. The checker tests that
  ratio rather than the absolute size, because both of the rules written as absolutes fired the
  moment the vines were resized, which is the wrong reason for a rule to fire. Each is turned,
  mirrored, resized and started separately, because nine frames repeated at one size is a tiled
  texture. One vine takes 1.15s to run its nine frames — 128ms a frame — and a claim 1.6-2.9s. The first pass drew an
  independent ring round every cell with a pale rounded SQUARE under it, and that reads as four
  collectible badges on four tiles — which is wrong twice over, because there is no floor there yet
  and the cells are not four things. A CONTACT SHADOW goes under all of it, dropped in WORLD terms
  because a shadow does not turn with the thing casting it. **REVEAL**: next round the same builder puts the cover back
  up already grown and then runs THE SAME ANIMATION BACKWARDS, in the reverse ORDER too — the last
  vine to grow is the first to let go, so the cover peels off the way it crawled on and the ground
  is uncovered from the far side inwards. Fading it out instead is a dissolve, and a dissolve says
  the vines were never really there. The seed sits UNDER the cover in the sorting order, because a
  cover something shows through is not a cover. **AND THE FLOOR IS HELD BACK WHILE THE COVER IS
  STILL ON IT** (`BoardView.HoldCells`, the same bargain the press and `BossMoveView` make): the
  next round's board is BUILT with the bonus ground in it, so without this the gift is simply
  there from the first frame with some darkness fading off the top of it. Each cell's floor comes
  back, and its corner rune lights on it, at the moment the stain's OWN front has passed that
  cell — one number, read off the growth channel the shader is drawing with, rather than a
  per-cell delay running beside it. Two timelines drift, and when they do a rune arrives from
  under a patch that is still standing. **PRESENCE**: then it simply sits for a round, and this is the
  part the player actually looks at — four OPEN corner runes and no closed frame. That gap is the
  mechanic: an ordinary cell has a structural border a line runs through, and bonus ground is play
  area no line ever waits for, so its frame does not close. The rule is legible without a word of
  UI. **RECALL**: at the round's end the gift stops being matter and is drawn home along the vines.
  The small pieces around the vine — the edge rune, the seed, the harvest kernel and flakes, the
  corner runes — are still baked SDFs from FUSED CIRCLES, the opposite pole from Mapus's polygons,
  because this is a growing spiritual thing with no flat on it anywhere; using the wrong primitive
  is what made the parasite a flower and Mapus a rotor, and both were the same mistake.
  Two glyph traps are recorded in the shapes: an open ring with a tie across it is the letter e,
  and a stem built from big circles is a bottle. `TalismanActivationVisuals` and
  `TalismanGroundVisuals` are reporting only and `[NotSaved]`; the ground report is written when
  the next board's config is built and deliberately survives `OnRoundStarted`, which clears the
  power's working list while the View still has an unwrapping to play. **A ghost trace is drawn
  with the ghost block's own tile and its own billowing warp material** — both already existed and
  simply were not asked for, so the one cube in the game whose whole identity is "not quite solid"
  was for a long time the one drawn as a flat pale rectangle. Bonus ground is also real ground for
  the DEAD END: a line never waits for it, but a block still fits there, and losing with a legal
  move in hand would be the worst class of bug (`Tilsim_BonusGroundIsStillSomewhereToPlay`).
  **RECLAIMED GROUND IS A SET, NEVER A BOX ROUND IT.** The board's backing store is a rectangle, so
  a cell reclaimed past the right edge has to grow it — and the cells that rectangle grows over
  must NOT become playable as a side effect. Reclaiming (7,3) hands the player one cell, not a
  column of seven. `GameBoard`'s constructor already does this correctly and
  `Tilsim_ReclaimedGroundIsSparseNotRectangular` pins it shape by shape (a lone cell, gaps down a
  column, two columns, an L, four corners with holes between them, and a far cell that grows the
  bounds and nothing else), asserting the mask cell by cell rather than counting.
- **"Deprem" is the ARENA losing its footing, not an explosion** (`QuakeCollapseView`,
  `QuakeShapes`, `Resources/Shaders/QuakeDrop`). The rescue brings down a quarter of the
  destructible cubes when no move is left, pays nothing and is never a sweep - and it used to be
  drawn as a burst of dust on every cell, the explosion sound and a CAMERA shake, which is a line
  clear's language with no line in it. It is now nine beats on one clock, about three quarters of a
  second: a MICRO TREMOR of the arena itself (a pixel or two and a fifth of a degree, four controlled
  impulses; the camera, the hand and the HUD do not move), a REVEAL that shows the targets only once
  the quake has been felt everywhere, STRESS (a pixel of settle, the shadow spreading, a pressure
  mark or two in the cube's OWN colour darkened - never a white crack, nothing is breaking), the FAULT
  opening under it, DETACH, the SINK, dust and crumbs thrown sideways and falling, the fault CLOSING
  with no glow, and one small SETTLE of the arena. The waves a cube falls in come from a hash of its
  cell and the quake's seed - never a left-to-right scan and never one frame.
  **SINKING IS NOT SHRINKING.** A cube that shrinks and fades reads as deleted. When something sinks
  the GROUND LINE climbs its face: the clip line rises from the cube's base to past the crack's near
  edge, a ragged ledge rides on it, and the cube only settles a little, tips about its own BASE and
  darkens. The clip is per renderer (`QuakeDrop`, a world-space line in a property block) because a
  SpriteMask reveals every masked sprite in its range - two cubes collapsing side by side would show
  through each other's cracks, which is the trap `ColdSinkView` records.
  **THE CRACK TOOK THREE BAKES.** A jagged shape with ONE flat edge is a skyline in 2D: the first was
  a symmetric wedge with a single tip (a Christmas tree), the second an opening rising off a straight
  lip (a mountain range). A crack has no flat edge - two broken plates pulling apart, ragged on BOTH
  sides, widest off its middle, hairlines running on from it. And it is invisible without the FAR
  plate's edge catching the light: a charcoal crack on a dark slate cell was simply not there on
  the first render. See `quake_cracks2.png` / `quake_collapse2.png` in the scratchpad.
  **THE ARENA HAS ONE TRANSFORM WRITER.** `OvertimePressureView` already writes the board's scale and
  position every frame through `SetPressure`; a tremor writing the same transform would fight it and
  whichever ran last would win. So `SetPressure` and `SetTremor` only record their own term and
  `ApplyArenaTransform` composes them - scale AND turn about the board's own centre, knock and tremor
  on top; with no turn it is exactly the squeeze it always was.
  **THE VIEW DECIDES NOTHING.** `QuakeVisuals` carries the cells the engine ACTUALLY emptied (the
  return of `RoundEngine.DestroyCubes`, not the list asked for) and the cube that stood in each,
  snapshotted before it went; the choice itself lives in one static, `DepremJoker.ChooseCollapse`,
  with the round's random stream spent in exactly the order it always was (baseline byte-identical),
  and the lab runs that static on boards of its own. The fallen cubes are PROXIES drawn from the face
  the board last showed (`TryCubeLook`, which keeps a blind round blind); a water cube's swirl stops
  for the fifth of a second it sinks. The old path watched `CollapseCount` against a dictionary of
  counts keyed by joker instance id that was never cleared, so a new run silently skipped its first
  quake - it is gone, and the report is matched by identity like every other. Not done, on purpose:
  the half-pixel inertia of the cubes that stay, which would mean moving board-owned renderers one by
  one against every repaint. The lab has eleven scenes (one/three/five/ten/twenty cubes, mixed
  colours, near, far, edge, corner, and a full board with stone in it that stays), eight beats on
  their own, a proxy test, 0.5x/0.25x, and three debug overlays (targets red / could fall yellow /
  stone grey, the wave each cube falls in, crack bounds).
- **"Tutuştur" is a combustion wave, not twenty explosions** (`IgnitionBurnView`, `IgnitionShapes`,
  `Resources/Shaders/IgnitionBurn`, `GameUiController.Ignition.cs`). The joker takes every fire on the
  board through `DestroyCubes` when a fire goes up, which reaches no explosion list - the far fires
  used to vanish with the repaint. Now the repaint raises them as proxies and holds them
  (`SyncIgnition` -> `Prepare`), and `PlayExplosionFeedback` starts the chain at the SOURCE's own
  explosion peak (`PlayIgnition` -> `Begin`, the line sweep's propagation and hold): a small warm
  ripple on the source, then the fires BURN OUT row by row from the bottom - 45 ms a row, squeezed so
  the climb never passes 350 ms, a few ms of deterministic jitter inside a row so it is never a
  left-to-right sweep. One peak frame should say it alone: bottom clear, middle in smoke, top still
  fire. Each cube heats (brighter middle, darker rim, a couple of small flame licks on its rim), smoke
  is born at its BOTTOM edge, narrow and warm-lit, and climbs and widens over it; behind the smoke
  **the burn is a material, not an alpha** - `IgnitionBurn` chars it through a baked mask that is
  value noise pulled low toward the outline (so it chars from the edges and a few patches inward, the
  middle last) with an ember band on the front, sampled in OBJECT space because a packed tile's UV is a
  sub-rect; then it collapses into itself (x 0.62, y 0.45), embers and ash lift off, and the smoke
  outlives it by a breath before lifting away - which is what reveals the empty cell. Without the
  shader the cube tints to charcoal. Budgets step down with the count (4 / 3 / 2 puffs, embers and
  ash shared out) so twenty fires are dense and never a grey fog. Sound per ROW, one haptic.
  **THE VIEW DECIDES NOTHING**: `TutusturJoker.LastIgnition` (`IgnitionVisuals`, `[NotSaved]`) carries
  the cells `DestroyCubes` returned, the cube in each taken before it went, the fire cells the turn's
  log shows already went up (the fuse), and the points measured off the breakdown - 0 unless "Genel
  temizlik" makes the chain pay. The lab has single / beat-isolation / same-row / different-rows /
  five-row / full / sparse / 30-fire stress scenes, the whole scenario (a block lands in a row with a
  fire, the line's own explosion, the chain at its peak), a proxy test, a score scene, 0.5x / 0.25x and
  nine debug views; `Tools/UiLayoutCheck/tutustur.py` holds the lot. See `ignite_*.png` in the session
  scratchpad.
- **"Elmas Kazma" breaks the stone the sweep could not** (`QuarryBreakView`, `QuarryShapes`,
  `GameUiController.Quarry.cs`). The joker cracks every obsidian on a clean sweep through the
  engine's forced destruction, which reaches no explosion list - so for a long time the stone simply
  vanished under the sweep's wave and the player could not tell it from the cubes the sweep took.
  **THE ORDER IS THE MESSAGE**: the repaint that follows the turn has already emptied those cells, so
  that repaint raises the stones as PROXIES and holds them (`SyncQuarry` -> `Prepare`); the break is
  started from `PlayExplosionFeedback` (`PlayQuarry` -> `Begin`) and waits out the sweep's own charge
  and wave (read off `BoardCleanseView.Style`) plus a breath. The sweep takes its cubes, the obsidian
  is left standing, and only then does the pickaxe act. **Not an explosion recoloured**: a cold
  resonance (a pale plate at a few per cent, a reflection on the TOP and LEFT edges only - on all four
  it is a selection box - and a stress point at the crack junction), crystal cracks GROWN along
  three straight runs each (a main crack in two arms, one arm of a second line, up to two branches:
  never a web), a 60 ms anticipation that nudges the stone against the strike, one TAPERED strike
  (bright head, thinning to nothing behind - parallel sides are a laser) landing on the junction, a
  compression ALONG the strike (a pivot turned to the strike and scaled there, the stone
  counter-turned inside it) and then the break: **the shards are the stone** - its own tile cut into
  wedges by the lines through the junction (`QuarryShapes.SplitSquare` + `Wedge`, `OverrideGeometry`
  on the tile's texture, pivoted on the cube centre), heavy pieces that leave fast and stop, with a cold
  edge and a little rhombus dust between them. No flash, no smoke, no camera, no pickaxe sprite: the
  pickaxe is the motion. **THE VIEW DECIDES NOTHING**: `ElmasKazmaJoker.LastQuarry` (`QuarryVisuals`,
  `[NotSaved]`) carries the cells `DestroyCubes` RETURNED, the cube in each taken before it went, and
  the points MEASURED off the turn's breakdown (flat + late flat, at the score's scale); the faces are
  the board's own (`TryCubeLook`). One to three stones show their own value, four or more one total -
  and never before the break. Many stones are a wave (25 ms, squeezed to 120 ms), past ten the strikes
  land in three ticks and the detail steps down to hold the shard and dust budgets. The lab has
  seven beat-isolation entries, one / two / four / eight / sixteen stones, the sweep scene (the real
  sweep over the lab board, then the break), a proxy test, the subtotal mode, 0.5x / 0.25x and nine
  debug views; `Tools/UiLayoutCheck/elmas_kazma.py` holds the lot. See `quarry_mock*.png` in the
  session scratchpad.
- **"Hazine" is two DRAWN bursts with code around them, and the code is what makes them true**
  (`HazineRevealView`, `FrameSequenceFx`, `HazineShapes`, `GameUiController.Hazine.cs`). The art is
  two eight-frame sheets at `Resources/Art/Fx/hazine_treasure_sheet` / `hazine_dynamite_sheet`,
  repacked by `Tools/ArtPrep/pack_hazine_sheets.py` from scattered generated drawings: every frame
  on one 4x2 grid, ONE scale for the whole sheet so the growth survives, each centred on its own
  bright core, the generator's red/yellow fringe (saturated colour under alpha 32) and loose specks
  removed, and on the treasure every faint texel taken to the frame's own gold - pale ivory at
  alpha 0.1 reads as a cold grey smear on a dark board. `FrameSequenceFx` plays a sheet with a
  duration PER FRAME (the animator's table: the peak holds longest - played at `Style.Tempo`
  1.35, which keeps its shape; at 1 it read as slow, and the drawings are 1.7 / 1.75 cells), pooled, on the OWNER's clock so
  the lab's time scale slows the drawing and the code together, and a negative clock is a delay.
  **A sheet on its own is a sticker**, so everything else is laid on particular DRAWN frames: a beat
  of anticipation (the dynamite's is tension, a swell and a pinch) in the 50ms between the cube
  breaking and the reveal; a local light whose curve follows the drawing's own brightness and dies
  at its own edge (peak 0.18 / 0.24); motes and one or two glints, or charred chips, smoke and a
  soot mark that stays inside the cell and outlives the burst; the verdict at the peak; and the
  value carried as one small essence to where it actually went. **The dynamite knocks the ARENA,
  never the camera** - a third term on `BoardView` (`SetImpulse`), because a quake can bring down the
  cube a stick was buried under and two writers of that transform would fight.
  **MOST OF WHAT HAZINE DOES IS NOT SCORE, AND THE REPORT SAYS SO.** The treasure draws one of four
  rewards and only one is score (an extra half of the explosion's base line value, and only when a
  line went off - a clean sweep REPLACES the line score, so a sweep that finds the treasure has no
  line to multiply); the dynamite's three penalties are a drained power, a frozen card and a
  discarded hand, and none is score. `HazineVisuals` (`[NotSaved]`, a new object per find, written
  beside the code that applied each effect) names the effect and its real number, and the score
  is MEASURED around the payment (`RoundScore` before and after), so a "Terslik" round reports - and
  draws - a treasure that cost points. The verdict is therefore a word as often as a number ("%20
  İNDİRİM", "GÜÇ -1", "3 TUR DONDU", "ETKİSİZ"), and the essence flies only to a thing really on
  screen: the score label (through `TickScoreResponse`, which now takes a dip as well as a punch),
  the power panel the rules named (a refill's power is found by diffing charged flags around
  `RechargeOne`, no draw), the card frozen or minted, the discard pile, or the joker's own panel for
  a discount that waits for the market. **IT NEVER POINTS AT THE OTHER MARK**: finding one removes
  the other and where it lay was never earned, so the report does not carry it and the view draws
  at the found cells only - "the other one is gone too" is a single ember that appears INSIDE the
  found burst's light and is eaten there. **Both at once cancel**: both drawings are cut at their
  third frame, a core leaves each cell for the point between them, and they meet in the treasure
  drawing's own tail taken down to ivory, with a quiet "İPTAL" and nothing paid. The reveal is
  asked from `PlayExplosionFeedback`, not the repaint, and each cell's delay is when ITS cube
  breaks - a line's sweep front (`PropagationSeconds` times the distance from the middle), anything
  else the cluster burst - so a treasure is never revealed before the line that found it has
  broken. The lab has nineteen scenes (every effect, an inverted round, line end / loose / edge /
  corner, the dynamite under a quake, three cancels, and the HIDDEN-INFO test that prints how many
  cells the report names beside how many the view draws at), each drawing alone and everything but
  the drawing, 0.25x runs with a frame-index + ms readout, a cell outline of what the report names,
  and eleven switches; `Tools/UiLayoutCheck/hazine.py` holds the lot.
- **"Harcama bonusu" is the empty pile PAYING YOU BACK** (`RebateView`, `RebateShapes`,
  `GameUiController.Rebate.cs`). The mechanic is not "you scored some points" — it is "you spent
  the resource and the spending refunded you" — so the payout may not simply appear beside the
  score. It is one causal chain from the source to the target, six beats on one clock in about a
  second: the slot **settles and cools** (`CardLayerView.PlayDrawPileEmptyBeat`, which lives there
  because that class owns the pile's transform and an effect reaching in would fight the next
  repaint), a short muted-gold **line wakes** in the empty middle, a **voucher strip unfurls** out
  of it — height first, then width, because scaling as a block is a panel appearing rather than a
  receipt opening — the **real value is stamped** on with a pixel of compression and four to six
  gold chips, it is simply **legible for 0.18s** (the reward beat; without it the player sees a
  shape move rather than a number arrive), the strip **folds to its middle** into one small rebate
  core, and that core **arcs to the score** and is absorbed.
  **TWO FACTS ARE CORE’S AND NEITHER MAY BE DERIVED HERE.** The AMOUNT is
  `RebateVisuals.Payout`: `PointsPerEmptyDrawPile` is a balance placeholder and a hard-coded
  "+60" is a picture that will one day lie about the score. And the COUNT — the joker pays on a
  **BOOL** (`TurnReport.DrawPileEmptiedThisTurn`), not on a tally, so a turn in which the pile
  dried twice pays ONCE. A View hung off "the draw pile just emptied" would be watching the right
  fact and still be wrong, because it could not know the payment had already been made; it watches
  the PAYMENT, keyed on the report ITSELF. Both are pinned in `HarcamaBonusu_PaysWhenDrawPileEmpties` and
  by a lab scene that fires the seam twice with one report.
  **BOTH ANCHORS ARE ASKED FOR**: the source is the real draw pile’s position and its real
  width (`CardLayerView.PileWorldWidth`), the target the real score label through
  `ScoreWorldAnchor()` — the same anchor Midas uses, and for the same reason: a written-down
  corner is a payout that flies off the edge of a phone. The flight bows **perpendicular to its
  own travel**, so the arc stays in the plane of the journey instead of detouring over the board.
  **THE SILHOUETTES ARE DEFINED BY WHAT THEY MUST NOT BE.** The strip is a voucher ribbon 3:1 with
  concave ticket ends and three shallow notches — never a CARD (this draws on top of the card
  layer beside a real pile, where a portrait rounded rectangle reads as another card) and never a
  literal receipt (no paper, no tear teeth, no barcode, no fake text). The core is a notched
  lozenge — the strip folded, still the same object — never a COIN and never a soft round dot,
  which is the unexplained yellow blur Midas drew once. The first bake got both wrong and both
  were visible immediately: the strip grew EARS (the end profile was written in `v`, which spans
  only a sixth of the texture, so the cosine covered half a period and pushed the half-width out
  of the box) and the core came out an OVAL. Every texture is square with the shape’s own
  proportion baked in, so one sprite is one world unit in both axes and a `localScale` written as
  (width, height) cannot silently come out at the wrong aspect.
  **FOUR THINGS SHIPPED BROKEN AND ALL FOUR WERE MEASURABLE**, which is the lesson: every one of
  them was found by computing the number against the real anchors, not by looking harder at a
  screenshot. The STRIP was drawn `Color.white` — `Style.Cream` was never applied — so on the warm
  pile art no voucher appeared at all. The VALUE was sized as a share of the pile's WIDTH and came
  out at **180% of the strip it was meant to be printed on**, overflowing the tray and colliding
  with the pile's own count; it is now solved backwards from the strip's own height (62%). The
  FLIGHT bowed *perpendicular* to its travel, which against the real anchors put its control point
  at **(1.8, 0.0) — the middle of the board**; it is now a cubic bezier lifted straight up, built
  only from the two anchors, with no third reference anywhere. And the TRAIL was drawn on a shape
  that tapers to a point, **longer and wider than the token it followed**, rotated along the
  flight: an arrowhead, which is precisely what "a gold triangle flying across the board" was. A
  trail is now clamped below the token's own size. The board may be *crossed* incidentally; it may
  never be *aimed at*, and `ShowBezier` dots the whole path so that stops being a claim.
  **AND THE VALUE IS STAMP RED.** It shipped ivory, which measures **1.11** against the cream
  strip it is printed on — not a soft look, a number you cannot read — and it survived the strip
  being given its proper colour because ivory is invisible on white too. Red is also the right
  answer rather than merely a readable one: the beat IS a stamp, and stamped ink on a voucher is
  red. 5.07 against the strip, over the 4.5 the card plates are held to, with a bronze shadow a
  hair down and right; a brighter red reads better as a colour and worse as text (3.56). The
  checker computes that contrast rather than naming a swatch.
  **AND THE PILE HAS TO LOOK SPENT.** `CardLayerView.SetDrawPileShownEmpty` takes the stack, the
  top card and the COUNT away and drops a recess into the slot — presentation only, the rules'
  pile untouched. Without it the payout appears over a full-looking stack of twenty-odd cards and
  states no cause at all. It is re-applied at the **bottom** of `UpdatePiles`, because the recycle
  that usually follows refills the pile *while the receipt is still out*, and it is given back the
  moment the receipt folds (`ReceiptGone`) rather than when the token lands — holding the slot
  empty through the flight would be showing the player a lie about their deck.
  **THE TONE IS PART OF THE MECHANIC.** It is a COMMON joker, and the event it pays for is the one
  that eats the arena and, past the threshold, is the loss condition — so it plays in overtime
  too, without celebrating. No flash, no coin rain, no jackpot, no shake, no camera move: 12
  particles at the very most, and the score’s own answer is deliberately smaller than Midas’s
  because the receipt already showed the number and a second celebration of the same points is a
  duplicate reward. **The score line is now written in ONE place** (`TickScoreResponse`): two
  effects can warm it, each used to write the scale and colour itself, and whichever ticked second
  won — the one that had finished would reset the label to normal while the other was still
  mid-punch. Each view publishes a `ScoreWarm` and the strongest claim wins. The lab has the whole
  payout, seven beat-isolation entries, 0.5x/0.25x, ten switches, two debug overlays (ring the
  source and target; print what the rules said beside what is drawn) and the two scenes that
  matter: the GRANULARITY test and the TONE test.
- **"Buzluk" grows an ICE CRUST over water at the wall** (`IceFreezeView`, `IceGrowth`,
  `Resources/Shaders/CryoFreeze`). Two passes are buried here and the first one is the instructive
  failure, because on paper it was right: one progress value, a ragged front crossing the cube,
  the face lerped toward an ice colour behind it. Every still looked reasonable. On the board it
  read as the water being **RECOLOURED** — water and ice were two brightnesses of one sprite. A
  freeze is not one event, so it cannot be one number.
  **AN ICE CUBE IS STILL THE WATER TILE'S OWN PIXELS.** `ViewUtil.IceTile` is a second `Sprite`
  cut from the water texture (same rect, pivot, PPU) and that indirection is the trick: the
  material is chosen BY THE TILE, so giving ice a Sprite of its own hands it the cryo material
  everywhere at once — board, cluster-burst shell, invader column, press laminae — with no
  signature changed. There is no `block_ice.png` and there must not be one.
  **THE SHAPE IS A BAKED FIELD, NOT A FORMULA** (`IceGrowth`, four variants in one linear RGBA
  texture — all four channels are DATA, the trap `TalismanStain` already records). R is FINGER
  ARRIVAL, G FILM ARRIVAL, B cloud, A rim. Crystal fingers are grown as curved forking walks from
  3–5 seeds on the wall, one hero reaching most of the way and the rest stopping short; G is a
  **GEODESIC out of that field**, so the film closes the gaps between fingers from their sides at
  a steady rate rather than fading in everywhere. Baked for a wall on the LEFT: every other wall
  is a uv swizzle and a CORNER samples two tiles and takes MIN, so two crystal fields grow and
  meet in the middle with nothing coded for it.
  **THE LIQUID POCKET IS NOT DRAWN.** It is whatever the film has not reached, which is why it
  comes out irregular and off-centre instead of a shrinking disc — nobody chose its shape. The
  water's warp is scaled by that same mask, so the last swirl in the cube is in the last liquid
  in the cube, for free. The film must run PAST 1 (`FilmOver` 1.16) or the last texel to seal
  never seals and the pocket stays a bright blue patch that reads as a hole in the ice.
  **FIVE OVERLAPPING STAGES, ~0.8s**: `_Slow` (the flow dying, and only where it is still
  liquid), `_Seeds` (buds on the wall), `_Fingers`, `_Film` (the longest — it is what the eye
  follows), `_Thick` (the shell thickening after the surface closes, which is what stops the
  finished material arriving in one frame), then a LOCK that swells 2.2% and settles — water
  expands when it freezes and that is the only place the effect says so. They overlap heavily,
  which is what lets 0.8s not feel like 0.8s; standing ice is those five at their ends, so there
  is no separate static path and no hand-off.
  **THE FINISHED CUBE IS THREE PHYSICAL LAYERS**: the water BURIED (0.30 visible, and less as the
  shell thickens), the MILKY ICE BODY with its own thickness variation so it is never a flat
  plate, and the FROSTED CRYSTAL RIM — irregular, and baked **thicker on the wall side**, so even
  the still says which way the cold came from. The fingers stay in it as veins, core pale blue
  and edge near white.
  **WHICH WAY IS THE WALL IS CORE'S ANSWER.** `GameBoard.EdgeSidesOf` returns the sides as flags
  and `IsOnEdge` defers to it — one definition, the one the rule already used. A wall is any
  neighbour that is not PLAY AREA, so a cell beside a hole or an eroded cell is against one;
  nobody coded that and `Buzluk_AHoleIsAWallToo` pins it cell by cell. `FreezeVisuals` is
  reporting only, `[NotSaved]`, keyed on the report itself, and the baseline is byte-identical.
  **FIVE THINGS WERE GOT WRONG ON PAPER AND FIXED BY RENDERING THEM** (the mock is in the
  scratchpad; `ice_field` / `ice_growth` / `ice_final.png`). The ice must be built from the face's
  own LUMINANCE, never lerped toward a blue — luminance is where the tile's frame, bevel and lit
  top edge live, and a lerp to a flat colour leaves a frosted PLATE with no block in it. The
  geodesic must not WRAP (`np.roll` let the film leave one edge and come back the other). A
  finger's heading needs MEMORY or a long one spirals back on itself. Its soft edge must be
  BOUNDED — written as a plain distance it conflates "soft edge" with "grows forever" and every
  finger ends a quarter of the cell wide. And the rim's roughness needs THREE non-harmonic
  frequencies; two at a simple ratio repeat, and a repeating edge along a straight run is a row
  of SAW TEETH — grown twice, once in the shader and once in the bake.
  **AND IT STAYS IN ITS CELL.** An ice cube may never read as a bigger block than the one beside
  it, and that was broken two ways. The seating's scale was read back off the renderer and
  multiplied by the curve AGAIN every frame: seven frames at 60fps left the cube 6.6% oversized
  and the lab at 0.25x gave it twenty-eight frames and 29%, and it stayed there until something
  repainted the board. A per-frame scale is now written from a stored base, never from itself -
  the general rule, and the same class of bug as the fire's halo. The whole seating then came
  down under 1.5% (`ScaleCeiling` 1.02 is the documented ceiling) because saying "water expands"
  with SIZE is exactly the wrong way to say it; it is said with the shell instead - clouding,
  rim, crystal density, highlight. The frost rim itself scaled nothing (it is drawn inside the
  tile's own uv and cannot spill one pixel past the sprite) and still made the cube look bigger,
  because a bright band all the way round a dark cube is read as bulk - so its thickness and its
  strength are **silhouette numbers**, not decoration ones, and both came down by about 40%.
  Glints are clamped inside the cube's own face for the same reason.
  No snow, no burst, no shake, no flash, no fog: the loudest moment is the seating. A whole turn
  is capped at 1.15s with the stagger SQUEEZED rather than the total lengthened, so five cubes
  freezing is one cold wave along the wall. The lab has thirteen scenes (four walls alone, two
  corners, runs of three and five, several edges at once, the whole rim as a stress test, the
  HOLE-as-wall rule test, ice beside water it could not reach as the acceptance shot, ice beside
  obsidian and gold), seven stage-isolation entries, four DEBUG FIELD VIEWS that paint the wall
  sides / finger field / film field / liquid pocket instead of the ice — so "is it coming off the
  side Core named" and "is the pocket really the last unsealed region" are a glance rather than
  an argument — eleven switches and 0.5x/0.25x runs. Every scene runs `BuzlukJoker.FreezeOn` on a
  board of its own.
- **"Mapus" does not mark a cell, it TURNS IT INTO A PRISON** (`MapusSealView`, `MapusShapes`,
  `Resources/Shaders/MapusPit` + `MapusIron`). A sealed cell used to be the empty cell in a
  different colour, which said "somebody painted this square" — worse here than almost anywhere,
  because Mapus does not take a square: a sealed cell still reads as EMPTY, so the row AND the
  column through it cannot be completed either, and a cube short of a line the player had no way
  to see which cell was holding it. Five layers, and the design is not finished without all five.
  **THE PIT**: the cell's floor drops a step into the board, and it is lit like a HOLE and not like
  a bump — the upper-left light falls on the FAR inner wall and leaves the near one dark; one sign,
  and it decides whether the cell reads as sunken or as something standing on it. The void keeps a
  cold indigo of its own and never goes to black (a pure black disc in a cell is a hole punched in
  the render). **THE SOCKETS**: four recessed brackets in the cell's edges — without them the ribs
  are four shapes floating over a cell. **THE RIBS**: four warden ribs swing out of those sockets,
  broad at the root, tapering, each ending in a HOOK that turns the same way round the middle, so
  they read as an IRIS CLOSING rather than a compass rose; they never meet, and the gap they leave
  is where **THE SEAL** sits — a struck lump of dull garnet wax with a die's guilloche pressed into
  it, small, the only warm thing in the effect, and never a glow. **THE PRESSURE**: on the grid edge
  of every cell of that row and column, a faint bracket of shadow, travelling out cell by cell as
  the prison locks. Rows are bracketed top and bottom, columns left and right, so the two axes are
  told apart without either being a coloured stripe; nothing tints anybody's block.
  The silhouettes are baked SDFs like the parasite's, but from **convex polygons, not fused
  circles** — the parasite is an organism and has no flat on it anywhere, Mapus is forged iron and
  needs flat runs between crisp corners. **Three failures are buried in the shapes and all three
  were about PROPORTION, not detail.** Fused circles gave four soft petals round a red middle - a
  FLOWER. A broken ring with a bar across its gap in the seal is the letter G, exactly. And the
  first version that shipped read as a PROPELLER: its hook was nearly as wide as the rib's root and
  stuck out sideways, so the silhouette was narrow in the middle and wide at both ends, which is a
  fan blade by definition - and four of them leaning the same way is a shuriken. The fix was not
  more taper, a better hook, or (the answer after that) a chunkier piece: it was WHERE THE MASS
  IS. Four pieces converging on a small middle read as spokes however they are shaped, because the
  eye works out where a thing comes FROM by where its weight sits. So the weight moved to the
  edge: the WALL HOUSING is now the heavy element (about a third of a cell), the bolt sliding out
  of it is roughly half that and nearly parallel-sided, its latch head is narrower still rather
  than flared, and the four of them stop well short of the middle - what is left there is the
  prison gap, not a hub. They close in PAIRS, top and bottom then left and right, because four
  pieces arriving evenly round a circle is a fan opening whatever they look like.
  **Two switches exist so this is never argued about again**: the PROPELLER TEST draws the whole
  mechanism flat grey with no brand and no shading, leaving only the silhouette - if that reads as
  a fan, nothing painted on top of it will help, which is precisely how two passes were lost - and
  the EDGE-MASS TEST colours the housing green, the shaft blue and the head red, so "the green must
  be the heaviest region" is something you can see rather than discuss. The iron also has to be seen at all: it sits well above the board's own value,
  its light has a FLOOR under it (purely directional shading leaves half of every piece at the body
  colour, which on this board is near-black), it carries a groove down its middle, and each rib
  drops a contact shadow into the pit - without that it is printed on the cell rather than closed
  over it. The seal is a medallion, not a disc: a dried garnet rim, a burnt-wine body inside it and
  the die's mark inside that, because one flat red circle in the middle of a dark cell is a status
  LED. Every shape fills its own box, so a tuning number in the view
  means what it says (the `_FaceHalf` trap, again). The iron is lit **in WORLD space**: four ribs
  are one sprite at four rotations, and lit in their own local space each highlight points a
  different way, which reads as four separate objects rather than one mechanism.
  **THE VIEW DECIDES NONE OF IT.** `MapusSealVisuals` (Core, reporting only, `[NotSaved]`) carries
  which cell, whether this turn MOVED the seal or HELD it, whether the CAP just released one, and
  how many cubes the row and column still want (`GameBoard.RowGapCount` / `ColumnGapCount` — which
  count exactly what `ResolveFullLines` waits for, so there is one definition of a nearly-full line
  in the codebase and not two). Held is the small animation and moved is the big one; the cap's
  release is its own beat, because that is the one turn the player can finish the line and it has
  to look like a window rather than a wander. The boss's targeting lives in ONE method (`Choose`)
  that both the round and the **animation lab** call — the lab seals its own board through
  `MapusBoss.RetargetOn`, so the cell its sixteen scenes seal is the cell the rules would seal.
- **"Parazit"'s HOST CUBE is a clasp, not a colour** (`ParasiteHostView`,
  `Resources/Shaders/ParasiteHarness`). A host used to be its own colour lerped 55% toward magenta,
  and that wash was the entire visual language of the mechanic: it said "this one is pink" and
  nothing about a joker riding it, nothing about why a power bounced off it, and nothing about what
  a line clear was going to cost. THE CUBE UNDERNEATH NOW KEEPS EVERYTHING — sprite, colour,
  element, material, so a gold host still reads as gold — and the parasite is separate geometry
  WRAPPING it. Two earlier passes are buried here and both were the same
  mistake at different scales: a colour applied to the cube, then a set of parts bolted to it (four
  rounded squares, four strips, a centre dot — a UI lock). The cube is not MARKED and it is not
  CLAMPED. A thin TRANSLUCENT MEMBRANE lies over its face, its contour irregular (low-frequency
  lobes, so it runs near the cube's edge in places and pulls back toward the middle in others,
  never the square it is drawn on), thicker in some regions than others — and where it is THIN the
  block's own colour comes through, which is what keeps the block legible.
  **THE BLOCK UNDER IT IS DYING, AND THAT IS THE POINT** (`Resources/Shaders/ParasiteDrain`). A
  film over a cube reads as a film over a cube — clean, weightless, rather like glass, which is
  precisely what the pass before this looked like. What makes a parasite read as one is that the
  thing under it is losing its colour. So the cube's OWN face is drawn a second time, desaturated
  and dimmed and leaned toward a dead mauve, masked to the wrap's coverage and following its
  THICKNESS: palest where the film lies heaviest, still itself where it is thin. Never a tint on
  the cube (that is a global recolour and the whole point is that it is local), never greyscale and
  never black — a blue host stays a blue host, just a poisoned one. It is TIERED by coverage
  (`_Curve` above 1) so the thin edges stay honest while the heavy middle goes genuinely dead, and
  it puts the block's own HIGHLIGHT out as well (`_Kill`): a cube's life is in the bright band its
  bevel catches, and leaving that lit makes the whole thing a colour filter. Two to four NECROTIC
  PATCHES, baked as dried bitten-out lobes and placed stably per host, are the dark matte
  almost-unlit regions where the wrap has died onto the block; thin curved VEINS run out of the nest
  as secondary detail only, because a network of lines reading first is what made the earlier pass
  look like cracked glass. It spills a couple of
  pixels over the bevel, so it wraps rather than sits — and UNEVENLY (`_Curl`): in places it has
  curled right over the bevel in a thick dark lip with a lit top, in others it stops short on the
  face, because a band of the same weight all the way round is a BORDER and a border is what a
  decal has. Two or three THICK WRAP FOLDS — gathers of the film itself — cross the face, lit along
  one flank, shadowed and creased along the other, more opaque through their middles, and flattened
  by however hard the wrap is pulling. They live IN the membrane's shader (`_FoldA`/`B`/`C`) as
  shading and thickness rather than as pieces over it, and that distinction is the whole layer: a
  gather has no silhouette of its own, because it IS the sheet. Drawn as geometry they were a chain
  of overlapping convex segments, which is a TUBE however wide it is made — and a tube crossing a
  cube is a cable lying on it. The drain follows them, so the block is deadest under the heaviest
  part of the wrap rather than in some unrelated pattern. Two to four curved BINDING STRANDS — which
  really are cords, and really do break — cross the face on asymmetric paths (never corner-to-centre,
  never a perfect X).
  The film also has HOLES in it (`_WindowA` / `_WindowB`): two regions with no membrane and no
  drain at all, where the block is simply itself, seen THROUGH the wrap rather than under it, with
  the film gathered into a thicker lit lip around each — a sheet with no negative space in it reads
  as a filter over the cell, and the holes are what make the cube and the thing on it two objects.
  A hole is never round and the two are never the same size (a tear and a nick), and the block
  inside one is still PARTLY drained — it has been under this thing the whole time. Taken all the
  way back to its own colour it is a bright saturated disc on a dead face, which is a status light;
  made round it is a button; given one strong three-lobe harmonic it is a clover, which is worse
  than the circle it replaced.
  One lobed NEST sits slightly off centre where the strands gather: a SHELL with two or three
  growth ridges (`_Ridges`) and a contour light of its own, not a bead with a highlight, and the
  passenger inside it is THREE shells — a dark husk, the joker's own colour, a pale centre, all
  three derived from that one colour by scaling and lifting so the identity survives the layering.
  **THE PALETTE IS A FUNCTION OF THE BLOCK** (`ParasiteContrast` / `ParasiteContrastProfile`). A
  dark plum organism on an OBSIDIAN cube is dark-on-dark and the whole thing disappears — which is
  one of the two cubes in the game that most needs to be read. So the darkness of the host's own
  MATERIAL colour (never its renderer tint, which is white on a painted tile) opens the parasite's
  edges up toward ash and lilac and lights its contours (`_Rim`, taken from the baked silhouette's
  own alpha ramp so it follows the shape rather than a rectangle); on a bright block those close
  back down, because there a rim would only read as an outline. Only the CONTRAST moves — the hue
  family never does. The drain scales with it too: a near-black cube has almost no colour left to
  take, and draining it hard says nothing and only muddies the cell.
  **THE MEMBRANE IS A MESH** (`ParasiteMembraneMesh`, a 6x6 subdivided quad) for one reason: the
  idle is THE CUBE TRYING TO GET OUT. Every few seconds the film BULGES in one of six preset
  regions — the cube pushing from underneath — the film thins there so the block's colour shows
  through, the cube loads half a pixel that way, the strands nearest it tension and straighten, the
  core is pulled the OTHER way holding on, the passenger brightens, and then the wrap contracts and
  presses the cube back into its cell — AND TAKES ITS PAYMENT: the whole face withers for a moment
  as the cube goes back down (`WitherPulseStrength`), the region it fought in is left more drained
  than it was (`AfterDrainStrength`), and a little of that stays for good (`StainDepth`,
  accumulating to a cap and decaying very slowly), so a host that has struggled several times is
  visibly further gone than one just seated. That is what makes the idle a losing fight rather than
  a fidget. A quad has four corners and can only scale as a whole; a
  local bulge needs vertices. The cube itself is still almost all of the time — what moves is the
  thing on top of it. The other parts are baked silhouettes (`ParasiteShapes`: SDF blobs
  smooth-unioned, so the core is lobed and asymmetric and the strand segments are capsules), never
  a scaled rounded rectangle. That passenger colour comes from its DEF ID — never from the host cube's material,
  which says nothing about who is riding, and never the parasite's own plum, which would read as an
  empty socket. Roughly four fifths of the cell is still the block itself.
  **THE VIEW DECIDES NONE OF IT.** `ParasiteVisuals` carries the refusals: `GameBoard.DestroyCube`
  and `DestroyCubeForced` are the two central chokepoints the rules already funnel every external
  destroy and every forced pickup through, so each writes down a `HostRefusal` — the cell, what
  tried it, and the direction when there was one (`SetForcedStep`; a destroy has none and the report
  never invents one). The joker reports its own `HostPosition` and `PassengerIdentity`. Reporting
  only, cleared each turn, never saved, and the baseline is byte-identical.
  Four events, and they are told apart on purpose. SEATING (HOST AWAKENING): a faint stain, the
  heart GERMINATING out of the surface narrow and filling out, tendrils growing from it to the
  corners on a stagger, the clusters blooming and gripping, the membrane settling, and the
  passenger waking last. IDLE: a bond circulation every three to five seconds, node → tethers →
  anchors, with the passenger briefly legible; no particles, no constant glow, and the cube never
  breathes. CLAMP: a power or a moving board was refused, so the parasite GRIPS HARDER rather than
  raising a shield — the clusters facing the force SPREAD further over the surface
  (`Vector2.Dot` against the reported step) while the far ones pull back behind them, the tendrils
  tension and straighten, the heart compresses, the occlusion deepens and the cube loads a pixel
  into the force without leaving its cell. RUPTURE: the player's own line, the one thing it cannot hold — the wrap
  grips once more, then the film TEARS along the line's own band (`_Tear`, ragged because its own
  contour decides where it gives, and the orientation comes from `TurnReport.ExplodedRows`, never
  from the View), the strands break one at a time IN THE MIDDLE with each half retracting to its own
  end (the two segments at the break thinning to nothing first), the film peels back toward the
  core, the core is exposed and the passenger is fully visible for a beat — and on the frames the
  tear opens THE BLOCK'S COLOUR FLOODS BACK (`DeathColourReturn`, snapped rather than faded: a slow
  return reads as the effect switching off, and this has to read as the cube getting free a moment
  too late) — the cube then goes through the line's own destruction, and only then does the
  passenger collapse inward on its own colour. The folds do not break with the strands: they
  SLACKEN, heaping up as the tension goes out of them and peeling toward the core with the film. The player has to be able to read: I lost the cube
  AND the joker on it. The lab has TWENTY-SIX host scenes of its own (`AnimHost`) — the four
  materials, the passenger-identity cycle, seating, idle, both obsidian readability heroes, the two
  refusals, the sweep pass, both line orientations, the passenger's own death, both whole
  lifecycles, the withering at four strengths (off / thin / the tuned value / all the way), and each
  layer standing ALONE (membrane, necrotic crust, wrap folds, nest) — plus ten switches that take
  one layer away at a time. The "alone" scenes set their switches BEFORE the host is built, because
  a layer that is off is a layer that is never rented; and a layer judged with every other layer
  over it is being judged by the layers over it. See `docs/parazit-siluetler.png` for the baked
  shapes, `docs/parazit-zar.png` for the film over four materials with each of its parts switched
  off in turn, and `docs/parazit-kivrim.png` for the gathers drawn the WRONG way — as geometry —
  which is what settled the argument.
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
  the rim (1-5), a growing centre DEAD ZONE (6-11), or both (12-15). It is counted in
  `RoundEngine.DeckRecycleCount`, NOT `RoundDeck.ShuffleCount` (that also counts reshuffles the
  rules and jokers order), and applied once centrally at turn step 8.5. The dead zone
  (`GameBoard.Blight`, designer's call 2026-09-19) grows 1x1 → 3x3 → 5x5 and the fourth step
  loses the round (`LossReason.DeadZoneOverran`). A zone cell is still PLAY AREA — blocks go on
  it, its lines still fill and explode — but a row or column touching it PAYS NOTHING
  (`RoundEngine.BuildLineScore`). It is not the `dead` mask: a dead cell (`MarkDead`) cannot be
  built on and KILLS its row and column, unlike a plain hole in the bounding box, which is
  merely skipped. Never conflate the three.
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

The roster now stands at **51 jokers, 36 powers and 37 bosses** (registry counts); of the
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

**"Meydan Okuma" dares a line picked off a MEASURED sea of chances** (`LineChanceSea`, in
`Core/Jokers/LineChance.cs`). It used to mark any row or column at random, which made the bonus a
lottery. Now, whenever a dare is laid, every row and column gets its odds of being cleared within
the dare's own deadline (`MeydanOkumaJoker.DeadlineFor`, passed in rather than copied), and the
joker walks a LADDER: the full-bonus first dare goes on the HARDEST line that can still go off,
each halving aims at a more reasonable line (`AttemptTargets` 0 / 0.35 / 0.6), a line at or above
`GimmeChance` (0.75) is never dared at any attempt, and a line that was just missed is not dared
again straight away. **The odds are SAMPLED, not estimated**: a formula over gaps and fitting
cards is a second definition of "can this line be filled" that disagrees with the real one (water
falling out of a gap, a neighbouring clear re-opening a cell, a pocket nothing reaches), so each
future plays the deadline out on a CLONE of the board with the board's own `Place`,
`ResolveFullLines` and `SettleWaterAndReact`, in `RoundEngine.Turn`'s order. The hand is used as it
is (frozen cards thaw, boss-locked cards are gone, the bonus hand is spent once); the DRAW PILE is
known in content but not order, so every future shuffles it, and the discard comes back when it
runs dry exactly as `DrawWithRules` recycles it (never past the threshold, never under
"İmitasyon"); the player is a FOCUSED one who takes the move that puts the most cubes into the
line's gaps. Checked against arithmetic: a one-cell pocket whose only fitting block is one of
three cards in the draw pile, with three turns, has 2/3 odds on paper and measures 0.68; the same
block in hand measures 1.00 (a gimme, so never dared); nothing that fits, 0.00. **It touches
nothing**: clones only, its own xorshift seeded from the round's STATE, and the round's
`IRandomSource` is never drawn from - odds must not shift every later shuffle, and a replayed save
must reach the same dare. A line that can NEVER go off (dead, no required cell, a sealed "Mapus"
cell, explosions suppressed by "Bilinmezlik") is `Possible = false` and never a candidate: a hard
line is a dare, an impossible one is a rigged bet. When a board offers no honest dare it lays
nothing and looks again next turn, and the bonus is worked out from how many dares have been LAID
(`BaseBonus >> attemptsMade`), so waiting can never reset it. A line settled at one end after 12
futures (every one or none) stops being sampled - the ladder needs no precision there, and on the
real decks the median line sits at 0.88 - which took the whole 7x7 sea from 128 ms to 35 ms with
identical decisions. No saved field changed (tuning is `const`, the sea is `[NotSaved]`), so older
saves still load. **THE DEADLINE IS THE OPEN BALANCE QUESTION**: measured over 53 scattered boards
on the three real decks, `max(3, gaps)` - a turn per missing cube - leaves 13% of boards with no
honest dare at all and only 58% with a hard one, while `max(2, ceil(2*gaps/3))` gives every board a
dare and fills all three rungs most evenly (53 / 41 hard / 44 middling / 44 reasonable).

**The dare is drawn as a CONTRACT held on the board** (`ChallengeContractView`, `ChallengeShapes`,
`GameUiController.Challenge.cs`). Nothing drew the marked line at all before this - only the joker's
status text. Now two rails of PLATES run in the seams either side of the dared line, a seal CLAMP
grips each end, two or three motes at a tenth of an opacity run through it, and the wager sits
beside it as a token. **The first mock was a thin yellow rectangle** - two continuous rails and a
thin bridge across each end are a bounding box whatever their colour - and three things undid it:
the rail is one plate per cell with a bead and a hair of gap at every boundary; the plate throws a
faint warm light to ONE side, into the line (baked on +v, the view turns the plate); and the ends
are HEAVY - a ] bracket whose hooks reach back over the rail ends, with the seal on its spine and
no bridge at all. A frame of one weight is an outline; heavy ends and fine sides are a mechanism.
The blocks are never tinted and nothing fills the line. **The token is dark with a gold rim**:
ivory on gold measures about 1.3:1 (the "Harcama bonusu" stamp failure), so the face is burnished
cocoa and the ivory number reads at well over 10:1; it is baked as TWO HALVES cut along a slanted
ragged seam, which is what lets the bonus physically halve. **The motion says the result before
the number does**: a SUCCESS goes outward (the rails brighten as the line breaks, the clamps recoil
3 px, the rails break into gold plates thrown off the line, the token punches 1.15, folds into a
seed and the seed flies to the real TOPLAM, which punches 1.08); a MISS goes inward (the rails
tense, the clamps let go, each clamp REELS IN its own rail - the top one grew from one end and the
bottom from the other - a bronze cut runs down the token, the halves part 3 px, one goes out at
0.65, the other slides back and the token reforms smaller and duller around the NEW value, then
travels to the new line, lands, and the contract is laid there); running out is the miss with
nowhere to go, and the last half collapses into bronze dust. A retarget is never an instant swap.
Urgency is three looks (calm / tension / final: rails 0 / 1.5 / 3 px in, the current faster and
warmer, a 1.025 heartbeat on the last turn) from ONE definition, `ChallengeVisuals.UrgencyOf`.
**THE VIEW DECIDES NOTHING**: `MeydanOkumaJoker.LastEvent` (`ChallengeVisuals`, `[NotSaved]`, one
new object per event) says Started / Ticked / Succeeded / Failed / Expired with the contract before
and after - a miss and its new line are ONE event, the payment is MEASURED around `AddFlatScore`,
and a miss with no honest line yet carries only `NextBonus`, so the token PARKS on it until a later
Started moves it. There is no "/ 2" in the view. The deadline a dare started with is kept for the
countdown's look only, `[NotSaved]`, so the save format did not change (a loaded run counts what is
left as the whole deadline). **When matters**: the joker writes its event during the turn and the
repaint runs before the turn's explosions are drawn, so `RefreshAll` only hands over the joker's
LIVE state once its last event has been played, and the event itself plays from
`PlayExplosionFeedback` - the moment the dared line actually breaks. The lab has nineteen scenes
(spawn row / column, the three urgencies, token idle, success row / column / with the score path,
first miss, the halving alone, three retargets, second miss, expiry, long / short / irregular
boards) plus 0.5x / 0.25x success and miss, nine debug views and six layer switches; its numbers
are the joker's own (`BaseBonus`, `DeadlineFor` on the lab board's real gaps) and a success clears
the line through `FlashLine`. `Tools/UiLayoutCheck/meydan_okuma.py` holds the lot. See
`challenge_mock*.png` in the session scratchpad.

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
   correctly the day it is written. The ONE way out is `[NotSaved]` on a field, for per-turn data
   the VIEW reads back (`SnakeBoss.LastTurn`) - rebuilt by the next turn, meaningless across a load,
   and not state. It has to be written deliberately, so nothing is dropped by accident; a boss that
   kept such a field WITHOUT it could not be saved at all, and the round-trip tests are what said
   so. Fields are name-sorted, base class first, for a stable
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

**BOSS STAGES ARE DRESSED BY THEME, and F7 opens the BOSS LOOK LAB** (`GameUiController.BossIdentity.cs`,
`BossIdentityView*.cs`, `BossThemes.cs`). In the real game every boss stage plays its theme's intro
(the title card always waits for a click/key) and keeps its theme's background until the stage
ends; the lab previews everything and its hand picks never touch a real stage. Seven intros (Alarm,
Eclipse, Seal, Lockdown, Cinematic, Gravity well, Eruption) and eight persistent looks (hazard
tape, blood eclipse, rune circle, iron cage, letterbox, aura, orbital, lava lake — the last two
in `BossIdentityView.Worlds.cs`), chosen separately and previewed over the live board. A shared
red backdrop + outward sparks sit under every look, and an intro's title card waits for a
click or key (`HoldTitle`). No boss gets art of its own: a THEME is one of these visual styles
(intro + look), and `BossThemes.cs` puts every boss under the style that fits its character —
Runic, Eclipse, Cosmic, Inferno, Cage, Creature, Decree. A boss missing from the table gets Decree. "Use
in real boss stages" plays the chosen pair whenever a boss round appears (watched per frame,
not called from the round flow). Once one is picked, delete the losers rather than tuning them.

**F3 opens the ANIMATION LAB** — a catalogue of every animation in the game, each playable on
demand, with knobs for the conditions that modulate them (combo streak, sweep count, overtime
level, board darkness, and a 0.1x-2x time scale for watching one frame by frame). It lives in
`View/AnimationLabView.cs` + `GameUiController.AnimationLab.cs` and is the way to work on an
animation without having to reach the game state that normally triggers it.

**The panel is DRAGGABLE by its title bar** (shift+F3 puts it back; the place is remembered in
`PlayerPrefs` across sessions), because it is a side panel and some scenes play under wherever it
happens to be. The whole panel is built in world space under ONE root, so the drag is that root's
position and nothing inside it knows: the layout constants stay the panel's own coordinates and
every hit test converts a world point into them first (`ToPanel`) — building the rows at moved
positions instead would have put the offset in twelve places and left it out of the thirteenth.
Only the title bar grabs, so a drag can never be confused with playing a row, and the drag OWNS
the mouse for its frames so releasing over a row does not also fire it. The offset is clamped into
the camera every frame rather than on release, and the clamp keeps the **title bar** on screen in
particular: a panel that can be dropped where it cannot be grabbed is a panel that is gone.

**A PER-TURN REPORT IS MATCHED BY IDENTITY, NEVER BY ITS SERIAL.** Every joker that hands the
View a `[NotSaved]` report (`SpreadVisuals`, `FreezeVisuals`, `RebateVisuals`,
`MidasPayoutVisuals`, `QuakeVisuals`, `HazineVisuals`) writes a NEW object per use, and the view remembers the object it last
played (`ReferenceEquals`). They used to compare `report.Serial` against the last serial played,
and that was wrong in a way no single run shows: a serial restarts at 1 with every new joker,
while the views outlive a run (they are kept through `BoardView.Rebuild`, or live on the
controller), so a new run's first event was silently skipped whenever the run before had fired
exactly as many times. Identity is right in all three cases that matter - a repaint hands back the
same object, a new use writes a new one, and a loaded save has none - and each effect's checker
refuses a `Serial ==` comparison in its view.

The rule it follows: **it drives the real animation code, never a copy.** An entry calls the
same method the game calls and only fabricates the ARGUMENTS, so a retimed animation shows its
new timing there for free. That is why `CardLayerView.PlayDebugAnimation` and the small
`FlashLine` / `FlashCells` / `PlayRemoval` / `PlayForcedExit` / `PlayBoardMoves` / `PlayGangreneScene` / `PlaySnakeBody` / `PlaySnakeScene` / `FlashDynamite` / `ShakeForBlast` / `Spawn*Popup` /
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
the staged scenes which cell finishes the line. "Yılan" has twenty-two scenes of its own
(`AnimSnake`): the three spawn lengths, the idle, one/three/long slides, a slide that turns, boxed
in, eating a plain block, obsidian and gold, eat-and-grow, one/two/three tail cuts, the last
segment, a whole turn, a whole line clear, and three tests (the four directions, the five body
shapes, the bite in four directions) - each one a snake laid out by the lab and a turn shaped the
way Core shapes one, played through the game's own seams. A snake scene HOLDS what it ends on -
a segment longer, the head in the cell it emptied - like every other entry, until RESET or closing
the lab puts the round back; it used to resync itself a breath later, which read as the snake
biting and then going straight back to how it was. "Hidrolik pres" has twenty-three scenes of its own (`AnimPress`) and RUNS THE REAL RULES on a
board of the lab's own — `GameBoard.Compress` and `GameBoard.Expand` through their reporting
overloads, exactly as the power calls them — so the laminae, the push chain, the side that refuses,
the axis the corner opens on and the failure's footprint are the rules' answers rather than a
drawing of them: the squeeze at four/three/two/one/zero occupied quadrants, mixed materials, gold
and obsidian stored inside it, the four idle turns, a clean release, one pushed cube, a long chain,
a cube shoved off the board, a straight side shut by gold and by obsidian, the corner's reroute,
the failure, the failure shearing the stone around it, the press broken while shut, and the two
whole lifecycles. Fifteen more entries switch each layer off on its own (jaws, laminae, null
imprints, shell, pressure front, push response, seams, dimple, countdown marks, contact shadow,
inward collapse, burst, shear, residue, and all back on). Each one HOLDS what it ends on, and the
scene's label says what the report actually contained — how many quadrants were full, how many
cubes were shoved, how many went over the edge, which axis the corner used. "Tılsım" has a set of its own built round ONE question — is the darkness following the CELLS or
the box? Six geometry scenes are the acceptance set (a single cell, two side by side, a 2x2, an L,
a 3x3 with a hole, sparse islands) and three more play the same shapes coming apart next round;
seven isolate one layer of the curse at a time, and those go through the layer flags INTO THE BAKE
rather than hiding a renderer afterwards, because the patches, bridges, corner merges and tongues
are one baked field and there is no renderer to hide. Two more are the real test: the same claim
with the vines OFF (is the footprint still a cursed region on its own?) and back ON (do they read
as its skeleton?). Two debug overlays settle the argument rather than continuing it — one outlines
the cells the rules actually reclaimed together with their bounding rectangle, the other paints the
stain by the part that drew each texel (patch red, bridge blue, merge yellow, tongue green). The
"boss hareketiyle
atılma" entries do the same for one step at a time (eight directions, a holed arena, a staged
blocked target). The two "İç Hareket" entries run a moving board's turn
end for its survivors (sparse to nearly full; the centrifuge on an odd board so its centre stays
put), and their debug switches can draw each move's vector and destination cell.
