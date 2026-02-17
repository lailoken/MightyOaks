# MightyOaks

![Icon](icon.png)

A simple Valheim mod that makes Oak trees significantly larger and more majestic.

## Features

- **Giant Oaks:** Oak trees have a configurable chance (default 10%) to spawn as "Mighty Oaks".
- **Configurable Scaling:** By default, Mighty Oaks are scaled between 1.0x and 12.0x their normal size.
- **Invulnerability:** Mighty Oaks can be set to be invulnerable (default: true), protecting them from accidental chopping.
- **Persistent:** The scale of each tree is saved in the world data, so your giant trees will remain giant.

## Installation

1. Install [BepInEx for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/).
2. Extract the `MightyOaks` folder into `BepInEx/plugins/`.

## Configuration

The configuration file is generated after the first run at `BepInEx/config/com.marius.mightyoaks.cfg`.

| Setting | Default | Description |
| :--- | :--- | :--- |
| **Enabled** | true | Enable the plugin. |
| **ScalingChance** | 10 | Percentage chance (0-100) for a new Oak to be scaled. |
| **MinScale** | 1.0 | Minimum random scale factor. |
| **MaxScale** | 12.0 | Maximum random scale factor. |
| **MakeInvulnerable** | true | If true, scaled oaks cannot be destroyed. |

## Changelog

- **1.0.0**: Initial release.
