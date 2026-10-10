# Solution architecture

Parking Fee Control ships as one game mod with a C# backend and a React UI.
`ParkingFeeControl.sln` builds `cs-parking-fees/ParkingFeeControl.csproj`; that
project builds the sibling `parking-fees-ui/` npm project and assembles its assets.
The UI solution folders expose files in Rider and Visual Studio. They are not a
second build project, so the frontend is built once by MSBuild.

| Location | Responsibility |
| --- | --- |
| `cs-parking-fees/Mod.cs` | Composition and lifecycle: load settings, fee configuration and translations, then register game and UI systems. |
| `cs-parking-fees/Application/` | User options exposed through the game's settings UI. |
| `cs-parking-fees/GameIntegration/` | Apply fee policies to game entities and serialize district fees in saves. |
| `cs-parking-fees/GUI/` | Adapt backend configuration and game entities into UI bindings; handle UI commands. |
| `cs-parking-fees/Persistence/` | Load asset compatibility data and read/write the user's fee configuration. |
| `cs-parking-fees/Localization/` | Load translation dictionaries and adapt them to the game localization API. |
| `cs-parking-fees/Diagnostics/` | Logging, including the debug setting. |
| `cs-parking-fees/Locale/` | Translation JSON copied into the assembled mod. |
| `parking-fees-ui/src/` | React components, vanilla component resolution, icons and binding payload types. |
| `parking-fees-ui/types/` | Shared declarations for game UI APIs and image imports; each API is declared once. |
| `pdx/` | Publishing descriptions, change log and artwork. |

These are responsibility folders within a single backend assembly, not isolated
layers. Systems use the shared settings and configuration owned by `Mod`.
The GUI depends on game integration and persistence; the backend never depends
on React files. `Mod`, `ModSettings` and `DistrictParkingFee` retain their original
`ParkingFeeControl` namespace so settings and saved ECS component identities stay
compatible. Other backend namespaces follow their folders.

## Runtime contract and data

`ParkingFeeUISystem` publishes `parkingfee.config` with a `categories` array.
`src/types/parking-types.ts` describes that payload and the `updateCategoryFee`
and `updatePrefabFee` command payloads. The panel also sends `applyNow` and
`refreshConfig` commands. Keep binding names, property names and payload types
aligned when changing either side.

`parking-data.json` is embedded as `ParkingFeeControl.parking-data.json`.
`ParkingDataLoader` can use an external file beside the assembly as an override,
then falls back to the embedded data. User fees live in `parking-config.json`;
game options use the game's settings store; district component fees live in the
save file. These have separate lifetimes and must not be treated as build assets.

## Build and file discovery

The .NET SDK discovers backend `*.cs` files recursively. Its default `None`
items expose locale JSON, project properties and publishing files. TypeScript
includes both `src/**/*` and `types/**/*`; webpack follows imports from
`src/index.tsx`. When adding UI files, also add them to the solution folders if
they should appear in the solution explorer.

MSBuild collects UI inputs inside the `PrepareUIBuild` target rather than as
project-level items, keeping duplicate frontend entries out of Rider's project
tree. Image imports remain supported; local CSS/Sass loaders and declarations
were removed because the UI currently uses inline styles and game-provided themes.

MSBuild restores npm dependencies when either package manifest changes or its
restore stamp is missing. It builds the UI when source/configuration inputs,
the backend version or required output files change. Successful builds must
produce the entry point and metadata before the build stamp is written.
The metadata-only `Update` publishing profile skips the UI pipeline.

The combined output contains the processed backend DLL, `ParkingFeeControl.mjs`,
generated `mod.json`, any emitted UI assets, and `Locale/*.json`. The UI version
comes from the backend project's `Version`. The Windows SDK then deploys this
directory; publishing uses that deployed content. Generated `bin/`, `obj/`,
`node_modules/` and UI `output/` directories do not belong in source control.

Use `npm run typecheck` and `npm run build` in `parking-fees-ui/` for frontend
validation, and `dotnet build ParkingFeeControl.sln -c Debug` for the integrated
SDK build. A build verifies packaging and compilation; game behavior still needs
an in-game check.
