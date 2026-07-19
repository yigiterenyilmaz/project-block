# Rarity Grader

Deciding the **rarity** of every joker and power by reading its in-game description and
picking a tier. It only *records* the decisions — it does **not** touch game code, prices,
or shop odds (that wiring comes later).

## Tiers

| Tier | Colour | Meaning |
| --- | --- | --- |
| `common` | 🔵 blue | the baseline |
| `rare` | 🔴 red | notably stronger / more situational |
| `legendary` | 🟡 yellow | the pile-rewriters (one held at a time in-game) |

The four jokers already flagged `IsLegendary` in code (Oryantasyon, Dezenformasyon,
İmitasyon, Fraksiyon) start pre-seeded as **legendary**.

## How to grade — in the game (editor)

The grader is a debug overlay that lives in the game itself
(`Assets/Scripts/View/RarityGraderView.cs`). It self-injects in the **Unity editor** only.

1. Open the `enes` scene and press **Play**.
2. Press **F2** to open the Rarity Grader.
3. Browse the two tabs (**Jokers** / **Powers**) and read each item's live description:
   - `↑` / `↓` (or `J` / `K`) move the selection; the panel shows the full EN/TR rules text.
   - `Tab` (or `←` / `→`) switches between the jokers and powers tabs.
   - Mouse hover selects a row; clicking a rarity swatch grades it.
4. Grade the selected item: **`1` common · `2` rare · `3` legendary** (`0` clears it).
   Grading auto-advances to the next item, so it's a quick 1/2/3 rhythm.
5. `Esc` (or `F2`) closes. While open, the normal game input is paused.

Every change **auto-saves** to `rarities.json` in this folder — the committed source of
truth. Commit that file to lock the decisions in. It follows the game's language toggle,
so descriptions show in whichever language the game is set to.

## rarities.json

```json
{ "version": 1, "tiers": ["common","rare","legendary"], "gradedAt": "…",
  "assignments": { "<defId>": "<tier>", … } }
```

Keyed by the stable joker/power `DefId`, written in registry order for clean diffs — so it
survives display-name changes and maps straight onto the definitions when the pricing/odds
wiring is built later.

The in-game grader reads and writes this file directly (path:
`<project>/Tools/RarityGrader/rarities.json`), loading your previous decisions on open.
