# Size Budget — Horizon Mobile

Hard maximums set by the user (2026-11-07):
- **Full game: UNDER 2 GB** (target ~800 MB)
- **Lite / low-size version: UNDER 1 GB**
- The 1 GB game must look like other games' 3 GB — achieved with
  procedural/compressed assets, not by shipping less detail.

## How this build complies

| Asset type | What we ship | Disk cost |
|---|---|---|
| Car bodies | Procedural lofted meshes in code (`MeshFactory`, `CarBodyStyles`) — 9 silhouettes, 0 model files | ~0 MB |
| Car interiors / details | Parts generated in code (`CarVisuals.BuildInterior`) | ~0 MB |
| Building facades, roads, sky, neon | Procedural PNGs (`gen_textures.py`), max 2048 px (sky only), most 512 px | 2.4 MB total |
| Audio | Procedural synthesis in code (`SoundManager`) — 8 engine voices, gears, turbo, skid, nitro. No WAV/OGG files | 0 MB |
| Materials | Shared URP Lit materials created at runtime, reused across cars/buildings | ~0 MB |
| Textures (lite) | `LITE=1 python3 gen_textures.py` regenerates everything at half resolution into `Assets/Resources/Textures-lite/` | 664 KB (vs 2.4 MB full) |

Measured 2026-11-07:
- Project (excluding `Library/`): **9.3 MB**, 329 files, 51 scripts
- Project zip: **3.4 MB**
- Linux build folder (Mono2x): **94–96 MB** — dominated by the Unity
  player/engine itself (~42 MB `UnityPlayer.so` + managed code + shaders),
  not by game content. The game content inside it is a few MB.
- Estimated Android APK: **~35–55 MB** (engine-dominated), ~20x under the
  800 MB target. The headroom exists because we ship no FBX models, no
  recorded audio, no texture packs.

## Rules for keeping it small

1. **No FBX / model files.** Every car, building detail and prop is a
   procedural mesh or a Unity primitive. A scanned/licensed model pack
   would be the fastest way to blow the budget.
2. **No audio files.** All sound is synthesised (`SoundManager`).
   If real recordings are ever added, they must be compressed OGG loops
   and counted against the budget.
3. **Textures: procedural, ≤2048 px, shared.** `gen_textures.py` is the
   only texture source. New textures go there, not into the project as
   imports. Max 2048 only for the sky panorama; 512 for facades/roads.
4. **Shared materials.** `MeshFactory.Lit()` / `Emissive()` create one
   material per role and reuse it; do not create a material per building.
5. **No unused assets in the build.** `Resources/` is always included in
   a Unity build, so only textures the game actually loads may live in
   `Assets/Resources/Textures/` (currently 2.4 MB — negligible, but the
   rule stands as the roster grows to 50 cars).
6. **Lite variant:** run `LITE=1 python3 gen_textures.py`, then swap the
   `Textures-lite/` folder in for `Textures/` at build time (same file
   names, half resolution). Same gameplay, fewer audio layers if needed.
   Do NOT create the lite build yet — design only, per user instruction.

## Open items

- Texture `.meta` files have no ASTC/Android override set yet — set ASTC
  compression overrides before the first Android build (see
  `~/workspace/SizeOptimizationResearch.md` for the block-size table:
  4×4 hero, 6×6 main, 8×8 environment, 10×10+ background).
- The 50-car Tokyo roster (later phase) reuses the 9 procedural classes,
  so it adds data, not megabytes. If per-car textures are ever added,
  they must fit the table above.
- Full research: `~/workspace/SizeOptimizationResearch.md`.
