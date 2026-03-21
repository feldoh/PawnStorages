# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

The active development version is **1.6**. Each version has its own solution under `{version}/Source/PawnStorages/PawnStorages.sln`.

```bash
# Build (Debug) - from the solution directory
dotnet build 1.6/Source/PawnStorages/PawnStorages.sln

# Build Release (runs csharpier format + creates zip)
dotnet build 1.6/Source/PawnStorages/PawnStorages.sln -c Release

# Format code with CSharpier (required before Release builds)
csharpier format .
```

Building the `Homebound` project (the aggregator/release project) in Release mode:
- Runs `csharpier format` on the solution
- Copies all files into `Release/`
- Creates `Homebound.zip`
- Optionally syncs to Steam mods path if `RIMWORLD_STEAM_MODS_PATH` env var is set and no existing `PawnStorages` folder there

Compiled DLLs output to `{version}/Assemblies/` (e.g., `1.6/Assemblies/PawnStorages.dll`).

## Architecture Overview

This is a multi-version RimWorld mod (1.3–1.6) that allows storing pawns in buildings/items. The codebase targets **net48** and uses **Harmony** for patching.

### Project Structure

Each RimWorld version has its own source tree. **Focus on `1.6/Source/PawnStorages/`** for current development:

- **`PawnStorages/`** — Core mod assembly
- **`PawnStoragesModule-VEF/`** — Optional Vanilla Expanded Framework compatibility module
- **`Homebound/`** — Release aggregator project (no source files, just build targets)

### Core Class Hierarchy

```
ThingComp
└── CompPawnStorage          # Core storage logic; holds ThingOwner<Pawn> innerContainer
    └── CompTickedStorage    # Extends for animals: ticks age, nutrition, hediffs while stored

Building
└── PSBuilding               # Base building with alternate graphic support (PSExtension)
    └── Building_PawnStorage # Standard humanoid/prisoner storage; implements IPawnListParent
    └── Building_PawnDoor    # Pawn door (schedule-based entry/exit)

Building (separate)
└── Building_PSFarm          # Animal farm building; uses CompFarmStorage, CompFarmBreeder, etc.
```

### Key Components

**`CompPawnStorage`** (`CompPawnStorage.cs`) — The central component:
- Manages `ThingOwner<Pawn> innerContainer` for stored pawns
- Handles scheduling (time-based auto-enter/exit based on timetable assignments)
- `StorePawn()` / `ReleasePawn()` / `ApplyNeedsForStoredPeriodFor()` are the main entry/exit lifecycle methods
- Uses `PS_DefOf.PS_Home` time assignment for scheduling
- Pawn needs (food, rest, chemicals) are deferred and applied on release

**`CompAssignableToPawn_PawnStorage`** — Controls which pawns are assigned; supports Colonist/Slave/Prisoner owner types.

**`CompPawnStorageNutrition`** / **`CompPawnStorageProducer`** — Optional comps for nutrition consumption and item production while stored.

**`CompTickedStorage`** — Used for animal farms; emulates biological age, nutrition tick, and hediff ticks at a configurable `tickInterval`.

### Mod/Version Loading

`loadFolders.xml` controls which folders load per game version:
- `Common/` — Shared textures, sounds, languages across all versions
- `{version}/` — Version-specific Defs and Assemblies
- `Compatibility/{modId}/{version}/` — Conditional patches loaded via `IfModActive`

### Harmony Patches

All patches are in `Harmony/` and auto-discovered via `harmonyInstance.PatchAll()` in `HarmonyInit`. Key patches:
- `MapPawnsPatch` — Makes stored pawns visible in map pawn lists
- `DrawTimeAssignmentSelectorGrid_Patch` — Adds PS_Home time assignment to timetable UI
- `JobGiver_Work_Patch` / `JobGiver_GetRest_Patch` — Prevents stored pawns from being assigned work/rest
- `AddToTradeables_Patch` / `TradeUtilityBuy/Sell_Patch` — Enables trading stored pawns

### Compatibility Modules

Optional assemblies compiled separately from `PawnStoragesModule-VEF/`. These are loaded via `loadFolders.xml` only when the target mod is active.

### Adding New Storage Types

1. Create a `CompProperties_*` for configuration
2. Subclass `CompPawnStorage` (or `CompTickedStorage` for animals) if custom behavior needed
3. Subclass `Building_PawnStorage` (or `Building`) if custom building rendering/logic needed
4. Add Defs in `{version}/Defs/`
5. Register translations in `Common/Languages/`

### Pull Request Notes (from CONTRIBUTING.md)

- Mod compatibility patches must use `MayRequire`, `PatchOperationFindMod`, or `loadFolders.xml` entries
- New mod dependencies require `loadAfter` entries in `About/About.xml`
