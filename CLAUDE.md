# project_block

A Block Blast–style grid game with Balatro-style roguelike structure (rounds with
score thresholds, a deck of block cards, market between rounds; jokers/powers/elemental
block types come later). Unity 6 (6000.3.6f1), 2D URP, **new Input System only**.

## Layout

- `Assets/Scripts/Core/` — **all game rules.** Pure C# (`ProjectBlock.Core.asmdef`,
  `noEngineReferences`), deterministic via `IRandomSource`. Start reading at
  `Game/RoundEngine.cs` (turn state machine) and `Game/GameSession.cs` (run/rounds/market).
- `Assets/Scripts/View/` — disposable debug UI (runtime-generated sprites + HUD).
  Never put rules here.
- `Assets/Scenes/enes.unity` — the working scene (a single `GameBootstrap` object).
  **Only ever modify this scene**, never SampleScene or the URP template.

## Conventions (follow these)

- Every file starts with a `// PURPOSE:` header; extension points for future mechanics
  are marked `EXTENSION POINT`. Keep both up to date when editing.
- No `UnityEngine` and no un-seeded randomness inside `Core`.
- Rules that jokers/powers may bend live in mutable config objects (`RoundRules`,
  `ScoringConfig`) that the engine reads live — don't cache their values.
- Numbers in `ScoringConfig` / `DefaultRoundProgression` are balance placeholders;
  the flow around them is confirmed design.
- Turkish design terms → code names: el = `Hand`/turn, çekme destesi = `RoundDeck.DrawPile`,
  ıskarta = discard, oyun destesi = `GameSession.OwnedCards`, raunt = round,
  temizlik = clean sweep, bonus el = bonus hand, eşik = `RoundConfig.ScoreThreshold`.

## Testing

Core compiles outside Unity — make a throwaway console csproj that includes
`Assets/Scripts/Core/**/*.cs` (LangVersion 9) and drive `GameSession`/`RoundEngine`
directly. In-editor: open the enes scene and press Play (click cards / 1-9 to select,
click board to place, A/C on offers, N leaves market, R restarts).
