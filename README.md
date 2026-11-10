# Horizon Mobile — Open-World Car Game (Unity 6)

A mobile open-world driving game inspired by Forza Horizon: drive a
1000x1000 m city, race rivals, outrun the police, deliver cargo, collect
coins, and build a garage of 8 cars. Built with Unity 6000.3.28f1 LTS
(URP, mobile-optimized). Graphics rebuild 2026-11-07: cars are
procedurally lofted PBR models (9 distinct body styles), buildings have
textured facades, the road is textured asphalt — no flat primitive boxes.

## Size budget (hard maximums)
- **Full game: under 2 GB, target ~800 MB. Lite version: under 1 GB.**
- Achieved with procedural everything: cars are lofted in code (kilobytes
  of profile data, no FBX), textures are small generated PNGs (2.4 MB
  total), audio is synthesised in code (**0 MB of audio files**), materials
  are shared, no asset packs or large downloads. Current project is
  ~9 MB excluding the editor-only Library folder; the APK's size is
  dominated by the Unity engine itself (est. 35–55 MB total), not content.

## The world — 3 districts

- **City core (1000x1000 m):** 10 roads, 100+ buildings, 60+ trees,
  6 checkpoints, 16 waypoints, coins and repair pickups.
- **Downtown (NE):** tall glass towers, including the 120 m Horizon
  Tower landmark with a neon sign.
- **Highway ring:** a 4-lane ring road at the map edge (x/z = ±450)
  for high-speed runs, with connector roads to the gas station and garage.
- **Off-road / desert (SW):** sand patch, 8 dunes, desert trees —
  low grip, high fun.

Also in the world: a **gas station** (canopy, pumps, neon sign) and a
**garage** building. All world objects are generated from primitives
(see `Assets/Scenes/Main.unity`); there are no external assets.

## Career & progression

- **PlayerProfile:** XP, level, reputation, total money, races won,
  top speed, distance driven, drift total.
- **Championship:** a 5-race season with points per finish (25/18/15/10),
  standings and a 3000-credit season reward.
- **SaveManager:** saves/loads everything (money, cars, upgrades, XP,
  level, best times, settings) via PlayerPrefs + JSON, versioned (v1).

## Garage — 8 cars, 5-level upgrades

| Car | Top speed | Price |
| --- | --- | --- |
| City Sprint | 165 km/h | free |
| Street GT | 198 km/h | $1,500 |
| Desert Fox | 205 km/h | $2,500 |
| Night Hawk | 220 km/h | $3,200 |
| Horizon R | 235 km/h | $4,000 |
| Turbo Falcon | 245 km/h | $6,000 |
| Apex GT | 258 km/h | $8,500 |
| Horizon X | 275 km/h | $12,000 |

**UpgradeSystem:** engine, tires, nitro and armor, 5 levels each,
costs scale with level, saved per car, applied to CarController.

## Game modes

- **Race** — 6 checkpoints, 3 laps vs the clock.
- **Street Race** — vs 3 AI rivals with rubber-banding.
- **Police Chase** — wanted level rises on crashes/speeding; police
  cars chase and try to bust you; escape to lose them.
- **Delivery** — timed pickup/delivery missions, reward by time left.
- **Time Trial** — 120 s, $800 reward.
- **Free Roam / Collectibles** — explore, collect coins ($50 each,
  $500 bonus for all).

## Weather, polish, performance

- **WeatherSystem:** Clear / Cloudy / Rain / Fog, changes fog, light
  and road grip (rain = 25% less grip); weather shown in the HUD.
- **Achievements:** 10+ (first win, 200 km/h, 10,000 drift points,
  100 coins, ...) with a gold toast in the HUD.
- **TutorialManager:** first-run driving tutorial steps.
- **SettingsManager:** graphics quality (Low/Med/High), sound volume,
  steering sensitivity, vibration, language FA/EN.
- **ObjectPool** for traffic/coins/pickups; **QualityScaler** lowers
  render scale/shadows when FPS drops.
- **HUD:** NITRO button + nitro/drift text fields are wired to
  NitroBoost/DriftScore (fixed in YAML — no manual wiring needed),
  plus weather, wanted level and achievement toast.

## Controls

Touch: left side = steering drag, right side = gas/brake buttons,
NITRO button for boost. Keyboard (for testing): WASD/arrows, Space =
nitro, Esc = pause.

## Open & build (after license activation)

1. Install Unity Hub + Unity **6000.3.28f1** (Android module).
2. Sign in and activate a free Personal license
   (`unity auth login`, then activate in the Hub).
3. Open this folder in the Hub, press Play to test.
4. Build: File > Build Settings > Android, or
   `Unity -batchmode -projectPath . -buildTarget Android -executeMethod BuildScript.BuildAndroid`
   (APK). The project was verified by compiling all C# with Unity's
   own Roslyn compiler and parsing all scene/prefab YAML (0 errors).
