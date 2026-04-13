# Scene Configuration Manager

This project’s scene flow is driven by a config-first manager built around `SceneManagementConfig` and `GameController`.

## What It Does

- Centralizes macro-scene definitions (Title, Hub, Combat Arena, etc.)
- Plans scene transitions from rules instead of hard-coded scene loads
- Supports startup-mode routing (especially for editor debug workflows)
- Supports preserved scenes, dynamic unload rules, and unmanaged scenes
- Validates configuration from an editor tool window

## Core Files

- `Assets/Scripts/SceneManagement/SceneManagementConfig.cs`
- `Assets/Scripts/SceneManagement/GameController.cs`
- `Assets/Scripts/SceneManagement/SceneTransitionPlanner.cs`
- `Assets/Scripts/SceneManagement/SceneTransitionExecutor.cs`
- `Assets/Scripts/SceneManagement/SceneTransitionPlan.cs`
- `Assets/Scripts/SceneManagement/Editor/SceneManagementWindow.cs`
- `Assets/Scripts/SceneManagement/Editor/SceneManagementBootstrap.cs`
- `Assets/Scripts/SceneManagement/Editor/SceneManagementValidator.cs`

## Quick Start

1. Open `Tools > Scene Management` in Unity.
2. The tool ensures a config exists at:
   - `Assets/Config/SceneManagement/SceneManagementConfig.asset`
3. In the config, verify:
   - `Bootstrap Scene`
   - `Global Base Scenes`
   - `Global Unmanaged Scenes`
   - `Logical Scenes`
   - `Startup Modes`
4. Use the window’s `Validation` page and fix any errors.
5. Run game and publish `LoadMacroSceneEvent` to transition.

## Config Model

## Global

- `Bootstrap Scene`: root bootstrap scene.
- `Global Base Scenes`: always required for managed macro-scene transitions.
- `Global Unmanaged Scenes`: excluded from load/unload planning.

## Logical Scene Definition

Each `MacroSceneType` maps to one logical definition:

- `Required Scenes`: loaded for that macro scene.
- `Preserve If Loaded Scenes`: kept loaded only if already present.
- `Unload On Exit Rules`: dynamic unload rules (currently path-prefix matching).

## Startup Mode Binding

Maps `StartUpMode` to `MacroSceneType`.

- In editor, startup uses `DebugContext.RequestedStartMode`.
- In builds, startup falls back to `StartUpMode.TitleMenu`.

## Runtime Flow

1. `GameController` receives `LoadMacroSceneEvent` or `ReloadCurrentSceneEvent`.
2. `SceneTransitionPlanner` computes a `SceneTransitionPlan`.
3. `SceneTransitionExecutor` executes:
   - unload operations first
   - load operations second
4. On success, `GameController` publishes `MacroSceneLoadedEvent`.

## Transition Rules (Planner)

- Required set = `Global Base Scenes + Target Required Scenes`.
- If target is `CombatArena`, planner also resolves arena scene from `DebugContext.RequestedArenaName`.
- Preserve set = target `Preserve If Loaded Scenes`.
- Unload set = currently loaded managed scenes that are neither required nor preserved.
- Dynamic unload set = scenes matching current scene’s unload rules, excluding required/preserved/unmanaged.
- Force reload (`ReloadCurrentSceneEvent`) unloads currently loaded required scenes so they re-load cleanly.
- Unmanaged scenes are never added to load/unload operations.

## Events You Use

- Request transition:
  - `BitBox.Library.Eventing.GlobalEvents.LoadMacroSceneEvent`
  - `BitBox.Library.Eventing.GlobalEvents.ReloadCurrentSceneEvent`
- Observe completion:
  - `BitBox.Library.Eventing.GlobalEvents.MacroSceneLoadedEvent`

Example:

```csharp
_globalMessageBus.Publish(new LoadMacroSceneEvent(MacroSceneType.HubWorld));
_globalMessageBus.Publish(new ReloadCurrentSceneEvent());
```

## Editor Tooling

- Menu: `Tools > Scene Management`
- Pages:
  - `Overview`: logical-scene summary + transition preview
  - `Startup Modes`: startup binding table
  - `Validation`: full validation report
  - `Logical Scenes/*`: per-scene editing + preview + quick scene ping/open actions

## Validation Highlights

The validator checks:

- null/missing scene references
- duplicate logical definitions per `MacroSceneType`
- required/preserve overlaps
- bootstrap misuse
- duplicate startup bindings
- missing build-settings scene entries (warning)

## Combat Arena Note

Combat arena scene is selected dynamically from `DebugContext.RequestedArenaName` and expected under:

- `Assets/Scenes/Arenas/{ArenaName}.unity`

If missing/unloadable, planner falls back to `Arena_1`.

## Extending It

To add a new macro scene type:

1. Add enum value to `MacroSceneType`.
2. Add or update a logical scene definition in config (or bootstrap defaults).
3. Add startup binding if needed.
4. Ensure required scene refs are valid and in build settings.
5. Verify transition preview + validation in `Tools > Scene Management`.

## Troubleshooting

- Transition request ignored:
  - A transition is already running in `GameController`.
- No scene loaded:
  - target logical scene is missing from config.
- Unexpected scenes remain loaded:
  - check `Preserve If Loaded` and `Global Unmanaged Scenes`.
- Combat arena wrong scene:
  - verify `DebugContext.RequestedArenaName` and arena file path.
