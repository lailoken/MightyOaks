# MightyOaks

![Icon](https://raw.githubusercontent.com/lailoken/MightyOaks/main/icon.png)

A simple Valheim mod that makes Oak trees significantly larger and more majestic.

## Features

- **Giant Oaks:** Oak trees have a configurable chance (default 25%) to spawn as "Mighty Oaks".
- **Configurable Scaling:** By default, Mighty Oaks are scaled between 1.0x and 12.0x their normal size.
- **Invulnerability:** Mighty Oaks can be set to be invulnerable (default: true), protecting them from accidental chopping.
- **Persistent:** The scale of each tree is saved in the world data (ZDO). Scale is set when the tree is first loaded by the server/host (owner).
- **Version enforcement:** Servers kick clients that don't have the mod or have an incompatible version, so no one can be chunk master without the mod—trees never load small and buildings attached to them don't collapse.
- **Config sync:** Scaling settings are synced from server to clients so everyone uses the same rules.

### Persistence and “permanent” display

Scale is stored in the world save (ZDO key `OakScaleFactor`). **Valheim does not apply ZDO scale to vegetation when loading**; it only applies position/rotation. So when you load the world **without** the mod, trees will appear at default size even though the scale value is in the save. To see scaled trees you must have the mod installed when playing. "Permanent without mod" isn't possible unless Valheim adds support for applying saved scale to vegetation.

## Installation

1. Install [BepInEx for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/).
2. Extract the `MightyOaks` folder into `BepInEx/plugins/`.

## Configuration

The configuration file is generated after the first run at `BepInEx/config/com.lailoken.mightyoaks.cfg`.

| Setting | Default | Description |
| :--- | :--- | :--- |
| **Enabled** | true | Enable the plugin. |
| **ScalingChance** | 25 | Percentage chance (0-100) for a new Oak to be scaled. |
| **MinScale** | 1.0 | Minimum random scale factor. |
| **MaxScale** | 12.0 | Maximum random scale factor. |
| **ScaleExponent** | 2.0 | Exponent for scale distribution. 1.0 is linear. Higher values make large trees rarer. |
| **ScaleToughness** | true | If true, health scales with size (roughly scale^2). |
| **MakeInvulnerable** | true | Enable invulnerability for trees above a certain size. |
| **InvulnerabilityThreshold** | 2.0 | Scale threshold above which trees become invulnerable. |

## Changelog

- **1.1.5**: Deterministic oak scale from world seed + position; only write to ZDO when the real world seed is available (no fallback). Fixes trees changing size on reload or when re-entering an area. Same defaults and protocol as 1.1.4; compatible with 1.1.x.
- **1.1.4**: Fixed broken icon link in README for Thunderstore.
- **1.1.3**: Switched to direct RPC handshake for reliable version checking during connection.
- **1.1.2**: Improved server validation stability and fixed disconnects for valid clients.
- **1.1.1**: Fixed handshake connection issues for valid clients.
- **1.1.0**: Added server-side version enforcement. Clients without the mod will be kicked.
- **1.0.0**: Initial release.
