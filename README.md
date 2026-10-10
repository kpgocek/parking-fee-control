<img src="https://raw.githubusercontent.com/thiago-rcarvalho/cs2-parking-fee-control/refs/heads/main/pdx/pfc-logo.png" width="280"/>

# Cities: Skylines II — Parking Fee Control Mod

Parking Fee Control is a mod that lets you manage parking fees for buildings and districts, helping you keep rates always up-to-date and exactly as you want!
Detailed info on the Paradox mod page: [Parking Fee Control](https://mods.paradoxplaza.com/mods/134032/Windows)

## Translating (Contributing to Locale)

Translations are stored in JSON files inside the `cs-parking-fees/Locale/` folder. To contribute:

1. Open or create a file for your language (e.g., `pt-BR.json`, `en-US.json`).
2. Add or update the translation keys/values as needed.
3. Submit a pull request with your changes.

### Supported Languages

> <img src="https://purecatamphetamine.github.io/country-flag-icons/3x2/US.svg" width="20"/> en-US | <img src="https://purecatamphetamine.github.io/country-flag-icons/3x2/BR.svg" width="20"/> pt-BR | <img src="https://purecatamphetamine.github.io/country-flag-icons/3x2/DE.svg" width="20"/> de-DE (by @AndyStgt89) | <img src="https://purecatamphetamine.github.io/country-flag-icons/3x2/CN.svg" width="20"/> zh-HANS (by @AriadusTT)

## Adding Compatibility for Other Mods

You can add compatibility with other mods by editing `cs-parking-fees/parking-data.json`:

1. Find the desired mod ID on the Paradox mods page (website/in-game) or in Skyve.
2. Note the prefab name for each parking asset in the mod (use the Scene Explorer mod or Asset Editor to get these names).
3. Open `cs-parking-fees/parking-data.json` and add the prefab names along with the mod ID.
4. Submit a pull request with your changes.



## Before Build this mod

**Configure your environment:**
- Create a file named `local.envs` in the project root (same folder as `compile.sh`). Example:
	```env
	export GAME_MODS_DIR="/path/to/your/Mods/ParkingFeeControl"
	export CSII_USERDATAPATH="/path/to/your/AppData/LocalLow/Collosal Order/Cities Skylines 2"
	export BUILD_DIR="./cs-parking-fees/bin/Debug/net48"
	```
- This file is ignored by git and allows each user to set their own mod output path.
- For C# dependencies, if you need to override library paths, create or edit `cs-parking-fees/Directory.Build.local.props`:
	```xml
	<Project>
		<PropertyGroup>
			<MANAGED_DLLS_PATH>/path/to/your/libs</MANAGED_DLLS_PATH>
		</PropertyGroup>
	</Project>
	```
**Build**

If you are using Linux, use the `compile.sh` script. But if you are using Windows, simply run your IDE build command.

The React UI is an independent npm project in `parking-fees-ui/`. Run `npm ci`
and `npm run dev` there for UI development, or `npm run build` for a standalone
production build. Commit `package-lock.json`; `node_modules/` and `output/` are generated.
Run `npm run typecheck` to check the UI and game API declarations without emitting files.

A normal C# build also builds the UI and assembles `ParkingFeeControl.dll`,
`ParkingFeeControl.mjs`, generated `mod.json`, other UI assets, and `Locale/`
in `cs-parking-fees/bin/<Configuration>/net48/` before the Windows SDK deploys
the mod. The UI metadata version comes from the C# project during a combined build.
Node.js and npm must be available on PATH. Windows builds use the CSII SDK;
SDK paths can come from the current process environment or the user environment.
`MANAGED_DLLS_PATH` remains an optional override for game references.

C# code is organized into `Diagnostics`, `GUI`, `GameIntegration`,
`Localization`, and `Persistence` namespaces. `Mod`, `ModSettings`, and
`DistrictParkingFee` retain their existing identities for compatibility.
See [ARCHITECTURE.md](ARCHITECTURE.md) for responsibilities, binding contracts,
file discovery, and the combined build pipeline.

**Publish**

Publishing uses the Windows CSII toolchain and its mod publisher.

New-mod and new-version publishing validate that the deployed content contains
the DLL, UI entry point, metadata, and locales before the publisher runs.
The existing Update profile remains a metadata-only operation.

### Rider Run/Debug configurations

Open `ParkingFeeControl.sln` from the repository root in Rider. Shared configurations
live in `.run/` and appear in the toolbar's Run/Debug configuration selector.

| Configuration | Action |
| --- | --- |
| Build Debug | Build the backend and frontend, assemble the mod, and deploy through the Windows SDK. |
| Build Release | Build and deploy the combined mod in Release configuration. |
| PublishNewMod | First publish: build Release and create a new Paradox Mods listing (SDK command `Publish`). |
| PublishNewVersion | Build Release and ship new code to the existing listing (SDK command `NewVersion`). |
| UpdatePublishedConfiguration | Change listing metadata without building or shipping new code (SDK command `Update`). |

Select a configuration and use **Run**. Publish configurations perform real uploads;
use **PublishNewVersion** to ship code updates to an existing mod. They reuse the profiles in
`cs-parking-fees/Properties/PublishProfiles/` and require publisher authentication.
Build configurations do not publish to Paradox Mods.

`PublishNewVersion` and `UpdatePublishedConfiguration` use the existing `ModId`
in `Properties/PublishConfiguration.xml`. Use `PublishNewMod` only when creating
a new listing; configure that XML for the new mod and then retain its assigned ID
for later updates.

`UpdatePublishedConfiguration` initializes the SDK paths and runs its `Publish`
target with `NoBuild=true`. It skips compilation, post-processing, UI building,
and deployment, and passes `Update` to the publisher. It uses the current
`PublishConfiguration.xml` directly, so edits to `pdx/ABOUT.md` alone are not
applied by this operation. Update the XML's description, dependencies and thumbnail
references before running it.

These Windows configurations use the standard .NET installation at
`C:/Program Files/dotnet/dotnet.exe`. If yours is elsewhere, change the executable
path in **Run > Edit Configurations**. Node.js/npm must be on PATH, and the CSII
modding toolchain must be configured. Restart Rider after changing environment variables.

The mod is a DLL loaded by Cities: Skylines II, so these configurations run build
and publishing commands rather than starting the mod as an application. For C#
breakpoints, build Debug and use Rider's Unity **Attach to Unity Process** against
a game instance with Unity debugging enabled. Debugging the build command does
not attach to the game.

## Acknowledgments

This mod was developed using as reference the excellent mods from these amazing modders: **yenyang, Bruceyboy24804, franzvz, TDW, DanielVNZ, Triton Supreme**.

Thank you for your work in the CS2 community ❤️

## License

Copyright (C) 2026 thiago-rcarvalho

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the [GNU General Public License](LICENSE) for more details.

This project incorporates a small amount of third-party MIT-licensed code; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for details.
