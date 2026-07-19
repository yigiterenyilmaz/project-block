# CRT edge-bend (fullscreen barrel distortion) — wiring

The retro CRT "screen curve" is a fullscreen post-effect. The **shader** and the **game-side
toggle** are in the repo; the one step that must be done in the Unity Editor (it edits the URP
Renderer asset, which is fragile to hand-edit) is adding the Full Screen Pass feature. ~2 minutes.

## What's already in the project

- `Assets/Shaders/CrtEdgeBend.shader` — `Hidden/ProjectBlock/CrtEdgeBend`. A URP fullscreen blit
  shader that warps the image outward (barrel bulge) with a soft vignette.
- Game toggle — `GameUiController.SyncRetroPresentation()` sets the global float **`_CrtBend`**
  (`1` in retro mode, `0` otherwise) alongside the CRT overlay and audio. It is harmless before
  the steps below are done (nothing reads the global yet), so nothing breaks in the meantime.

The shader is driven by one global (`_CrtBend`) times two per-material strengths
(`_BarrelAmount`, `_Vignette`). At `_CrtBend = 0` it is an exact passthrough — no warp, no
darkening — so leaving the feature always-on only costs a cheap blit when retro is off.

## Editor steps (do these once)

1. **Make a material.** Right-click `Assets/Shaders/` → Create → Material, name it `CrtEdgeBend`.
   In its Inspector set Shader to **`Hidden/ProjectBlock/CrtEdgeBend`** (Shader dropdown → Hidden →
   ProjectBlock → CrtEdgeBend). Optionally tune **Barrel Amount** (~0.28) and **Vignette** (~0.5).
2. **Add the feature to the 2D renderer.** Select `Assets/Settings/Renderer2D.asset`. In the
   Inspector, **Add Renderer Feature → Full Screen Pass Renderer Feature**.
3. **Configure the feature:**
   - **Pass Material** = the `CrtEdgeBend` material from step 1.
   - **Injection Point** = **After Rendering Post Processing** (or Before — either reads fine;
     After keeps the whole final image curved).
   - Leave Requirements/other options at defaults.
4. **Play.** Toggle retro mode in-game (the Retro power). The whole screen should bow at the edges
   with a dark curved border. Turning retro off returns to a flat image (passthrough).

## Tuning / troubleshooting

- **Too strong / too subtle:** adjust `Barrel Amount` on the material (0 = flat, ~0.4 = strong).
- **No effect at all:** confirm the material's shader is `Hidden/ProjectBlock/CrtEdgeBend` and the
  feature's Pass Material points at it; confirm you edited **Renderer2D.asset** (the one the active
  URP asset uses).
- **Shader compile error** on this Unity version: the include path for the fullscreen `Vert` /
  `_BlitTexture` moved between URP versions. If `Blit.hlsl` isn't found at
  `Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl`, try
  `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Blit.hlsl` instead (swap the one
  `#include` in `CrtEdgeBend.shader`).
- The `_CrtBend` global is set every frame from `SyncRetroPresentation`; no per-feature reference
  is needed, so the feature can stay enabled permanently.
