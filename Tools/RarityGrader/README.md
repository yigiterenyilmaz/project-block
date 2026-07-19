# Rarity Grader

A tiny offline tool for **deciding** the rarity of every joker and power by reading
its in-game description and picking a tier. It only records the decisions — it does
**not** touch game code, prices, or shop odds (that wiring comes later).

## Tiers

| Tier | Colour | Meaning |
| --- | --- | --- |
| `common` | 🔵 blue | the baseline |
| `rare` | 🔴 red | notably stronger / more situational |
| `legendary` | 🟡 yellow | the pile-rewriters (one held at a time in-game) |

The four jokers already flagged `IsLegendary` in code (Oryantasyon, Dezenformasyon,
İmitasyon, Fraksiyon) start pre-seeded as **legendary**.

## Use it

1. Open `index.html` in any browser (double-click it — no server needed).
2. Read each item's EN/TR description and grade it:
   - Click a tier button, **or**
   - Use the keyboard: `↑`/`↓` (or `J`/`K`) to move, `1` common, `2` rare, `3`
     legendary. Assigning jumps to the next ungraded item.
   - Clicking the tier a card already has clears it.
3. Filter by Jokers/Powers, graded/ungraded, or search; toggle EN/TR/both.

Your progress **auto-saves in the browser** as you go, so you can close and come back.

## Persisting the decisions (the durable record)

`rarities.json` in this folder is the committed source of truth.

- **Export** (top-right) → *Download rarities.json* (or *Copy*), then replace this
  folder's `rarities.json` and commit it. That locks the decisions into the repo.
- **Import** → paste a `rarities.json` to reload it into the tool (e.g. on another
  machine or after clearing the browser).

Format: `{ version, tiers, gradedAt, assignments: { <defId>: <tier> } }`, keyed by the
stable joker/power `DefId` — so it survives display-name changes and maps straight
onto the definitions when the pricing/odds wiring is built.

## Regenerating the item list

`data.js` (the 62 items with their descriptions) is generated from Core. If jokers or
powers are added/renamed/re-described, refresh it:

```
python Tools/RarityGrader/extract.py
```

It reads the registries + definition files in registry order and rewrites `data.js`.
`DefId`s are stable keys, so existing grades in `rarities.json` keep matching.
