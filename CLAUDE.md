# project_block

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
those 15 numbered rounds (1-5 on 5x5, 6-11 on 7x7, 12-15 on 9x9), and each band also names the
`ShuffleErosion` that punishes a stalling round. A boss stage inherits its round's band. Run length
and that table are meant to change together.

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
  Never put rules here.
- `Assets/Scripts/View/Menus/` — the menu layer (title, pause, settings, how to play, run
  summary). Unlike the rest of View this is NOT disposable: it is the real UI shell, built
  on the HUD canvas. Every screen is `MenuScreenView` with different content — do not
  subclass it — and every colour/metric lives in `MenuSkin` so art drops in by assigning a
  `Sprite` where a flat `Color` sits. `GameUiController.Menus.cs` holds the `AppScreen`
  state machine: while `screen != Playing` the menu layer owns the whole frame.
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
  (`DefaultRoundProgression.BoardSizeBands` — rounds 1-5 on 5x5, 6-11 on 7x7, 12-15 on 9x9) is
  confirmed design, not a knob to tune. Surviving the BOSS OF ROUND 15 wins the run
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
   once obsidian/gold sit on the board.
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

The roster now stands at **53 jokers, 35 powers and 37 bosses** (registry counts); of the
originally planned powers only "Dolly" is left, set aside by the designer.
See `docs/jokers-plan.md`.

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
A/C on offers, N leaves market, S redraws the hand, R restarts. Joker debug keys:
J grants the next joker from the registry, K sells the last one, 1-9 activate (a joker
that needs a target then waits for a click, Esc cancels).
