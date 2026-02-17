# Oak Scaler Plugin

This BepInEx plugin scales Oak trees in Valheim by a configurable factor and makes them (near) invulnerable.

## Installation

1.  **Compile the Plugin**:
    -   You will need a C# development environment (like Visual Studio or VS Code with C# extension).
    -   Open the `OakScalerPlugin` folder as a project.
    -   Ensure you have the required references:
        -   `BepInEx.dll`
        -   `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.PhysicsModule.dll`
        -   `assembly_valheim.dll` (from your Valheim game data)
    -   Build the solution to generate `OakScaler.dll`.

2.  **Copy to Plugins Folder**:
    -   Copy the compiled `OakScaler.dll` to your `BepInEx/plugins` folder.

## Configuration

The plugin creates a configuration file `com.marius.oakscaler.cfg` in `BepInEx/config` after the first run.

-   **ScalingChance**: Chance (0-100) to scale an Oak tree. Default: 10%.
-   **MinScale**: Minimum scale factor. Default: 2.0.
-   **MaxScale**: Maximum scale factor. Default: 10.0.
-   **MakeInvulnerable**: Make scaled trees invulnerable. Default: true.
-   **Enabled**: Enable/Disable the plugin. Default: true.

## How it Works

-   The plugin patches `ZNetView.Awake` to intercept Oak tree spawning.
-   It checks if the tree has already been scaled (stored in ZDO data).
-   If not, it rolls a chance based on `ScalingChance`.
-   If successful, it applies a random scale between `MinScale` and `MaxScale` and saves this scale to the ZDO so it persists.
-   Scaled trees are made invulnerable to prevent accidental destruction.

Note: Existing Oak trees in already explored areas might not be scaled unless they are re-loaded or if the plugin logic allows retroactive scaling (currently it only scales new or unchecked trees).
