# MightyOaks

![Icon](https://raw.githubusercontent.com/lailoken/MightyOaks/main/icon.png)

A simple Valheim mod that makes Oak trees significantly larger and more majestic.

## Features

- **Scalable objects:** Per-category chance (0–100) to scale: Oak trees, Ashlands burnt oaks (AshlandsTree6, AshlandsTree6_big), Plains stone columns (HeathRockPillar), Swamp ancient trees (SwampTree2), Mistlands trees (Yggdrasil shoots). 0 = that category is off.
- **Configurable scaling:** Min/max scale and exponent control size distribution; default scale range 1.0x–10.0x.
- **Invulnerability:** Trees/objects at or above **InvulnerabilityThreshold** scale become invulnerable. Set threshold to max (scale max + 1) to disable.
- **Spawn protection:** No scaling within **SpawnProtectionRadius** of world center (default 300). Set 0 to allow everywhere.
- **Persistent:** Scale is saved in world data (ZDO). Set when the object is first loaded by the server/host (owner).
- **Version enforcement:** Uses [Jötunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/) for server/client mod compatibility. Servers kick clients without the mod or with an incompatible version (major.minor must match).
- **Config sync:** All settings are synced server → clients via Jötunn (admin-only config); server is master.

### Persistence and “permanent” display

Scale is stored in the world save (ZDO key `OakScaleFactor`). **Valheim does not apply ZDO scale to vegetation when loading**; it only applies position/rotation. So when you load the world **without** the mod, trees will appear at default size even though the scale value is in the save. To see scaled trees you must have the mod installed when playing. "Permanent without mod" isn't possible unless Valheim adds support for applying saved scale to vegetation.

## Installation

1. Install [BepInEx for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/).
2. Install [Jötunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/) (required for version check and config sync).
3. Extract the `MightyOaks` folder into `BepInEx/plugins/`.

## Configuration

The configuration file is generated after the first run at `BepInEx/config/com.lailoken.mightyoaks.cfg`.

| Setting | Default | Description |
| :--- | :--- | :--- |
| **ChanceScaleOak** | 15 | Chance (0–100) to scale Oak trees. 0 = off. |
| **ChanceScaleAshlandsOaks** | 5 | Chance (0–100) to scale Ashlands burnt oaks (AshlandsTree6, AshlandsTree6_big). 0 = off. |
| **ChanceScalePlainsStoneColumns** | 0 | Chance (0–100) to scale Plains stone columns (HeathRockPillar). 0 = off. (10% recommended)|
| **ChanceScaleSwampAncientTrees** | 0 | Chance (0–100) to scale Swamp ancient trees (SwampTree2). 0 = off. (1% recommended)|
| **ChanceScaleMistlandsTrees** | 0 | Chance (0–100) to scale Mistlands trees (Yggdrasil shoots: YggaShoot1–3). 0 = off. (1% recommended) |
| **MinScale** | 1.0 | Minimum random scale factor (range 0.1–20). |
| **MaxScale** | 10.0 | Maximum random scale factor (range 0.1–20). |
| **ScaleExponent** | 3.0 | Exponent for scale distribution. 1.0 = linear; higher = large scales rarer. |
| **ScaleToughness** | true | If true, health scales with size (roughly scale²). |
| **InvulnerabilityThreshold** | 2.0 | Scale ≥ this → invulnerable. Range 0 to 21 (scale max+1); set to 21 to disable. |
| **SpawnProtectionRadius** | 300 | Radius from world center where no scaling. Set 0 to allow everywhere. |

## Changelog

- **1.2.0**: Deterministic scale from world seed + position; only write to ZDO when world seed is available. Chance-based options per category (ChanceScaleOak, ChanceScaleAshlandsOaks, ChanceScalePlainsStoneColumns, ChanceScaleSwampAncientTrees, ChanceScaleMistlandsTrees); 0 = off. Removed Enabled and scale-on bools. Invulnerability via InvulnerabilityThreshold only (range 0 to scale max+1; set max to disable). Scale Ashlands oaks (AshlandsTree6, 6_big), Plains columns (HeathRockPillar), Swamp ancient trees (SwampTree2), Mistlands trees (YggaShoot1–3). Spawn protection radius (default 300). Jötunn config sync; server-side mod check for clients without Jötunn.
- **1.1.4**: Fixed broken icon link in README for Thunderstore.
- **1.1.3**: Switched to direct RPC handshake for reliable version checking during connection.
- **1.1.2**: Improved server validation stability and fixed disconnects for valid clients.
- **1.1.1**: Fixed handshake connection issues for valid clients.
- **1.1.0**: Added server-side version enforcement. Clients without the mod will be kicked.
- **1.0.0**: Initial release.
