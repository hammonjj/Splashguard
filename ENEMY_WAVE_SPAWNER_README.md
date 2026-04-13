# Enemy Wave Spawner

This document explains how to configure and use the arena enemy wave system implemented in this repo.

## What It Supports

- Sequential wave lists via `WaveStrategyAsset`
- Configurable enemy prefab source per strategy:
  - Use per-wave weighted entries (`WaveDefinition.EnemyEntries`)
  - Or use `ZoneData.EnemyPrefabs`
- Two wave types:
  - `Standard` (budget + batched spawns)
  - `Survival` (curve-driven spawn rate over time)
- Two trigger types:
  - `PreviousWaveCleared`
  - `Delay` (overlap with previous wave is allowed)
- Optional per-prefab pooling with prewarm + max pool size
- Arena completion event when all waves are done and cleaned up
- Structured wave telemetry for debug overlays

## Core Types

- `WaveStrategyAsset`  
  Path: `Assets/Scripts/Arena/Waves/WaveStrategyAsset.cs`
- `WaveDefinition`
- `WeightedEnemyEntry`
- Enums:
  - `WaveType`
  - `WaveTriggerType`
  - `WaveRuntimeState`

## Configuration Resolution Order

`EnemySpawner` resolves strategy in this order:

1. `ArenaDataReference.WaveStrategyOverride`
2. `ArenaDataReference.ZoneData.DefaultWaveStrategy`
3. No strategy -> warning + no spawn

## Quick Setup

1. Add `EnemySpawner` to your arena root object.
2. Ensure the same object has `ArenaDataReference`.
3. Assign `ArenaDataReference.SpawnPoints`.
4. Create a `WaveStrategyAsset`:
   - Right-click in Project:
     - `Create > Scriptable Objects > Arena > Wave Strategy`
5. Configure waves in the strategy:
   - Add `WaveDefinition` entries
   - Set `WaveType` / `TriggerType`
   - Choose enemy prefab source:
     - `Use ZoneData Enemy Prefabs = off`:
       - add weighted enemy prefabs in `EnemyEntries`
     - `Use ZoneData Enemy Prefabs = on`:
       - spawner uses `ZoneData.EnemyPrefabs` for wave spawn selection
6. Assign strategy:
   - Per-arena override on `ArenaDataReference.WaveStrategyOverride`, or
   - Zone default on `ZoneData.DefaultWaveStrategy`
7. Enter arena and trigger `MatchStartingEvent` through the normal encounter flow.

## Wave Behavior

## Standard Wave

- Uses:
  - `EnemyBudget`
  - `BatchSize`
  - `SecondsBetweenBatches`
- Spawns exact budget by weighted selection.
- Spawn points are random, non-repeating within a burst when possible.
- Wave completes after:
  - all batches spawned, and
  - all alive enemies from that wave are dead.

## Survival Wave

- Uses:
  - `SurvivalDurationSeconds`
  - `SurvivalSpawnRateCurve` (`Y = spawns/sec`, `X = 0..1`)
  - `SurvivalMaxAlive`
- Spawns while timer is active, constrained by alive cap.
- At timer end:
  - stops spawning immediately
  - waits for cleanup
- Wave completes only when alive count reaches zero.

## Trigger Behavior

## PreviousWaveCleared

- Wave starts when the previous wave is fully completed.

## Delay

- Delay is based on previous wave **start** time (`Time.time`, scaled time).
- Overlap is allowed:
  - next wave can begin while previous wave still has enemies alive.

## Pooling

- Controlled by `WaveStrategyAsset.EnablePooling`.
- Pool prefab set comes from the selected strategy prefab source:
  - `Use ZoneData Enemy Prefabs = on` -> `ZoneData.EnemyPrefabs`
  - `Use ZoneData Enemy Prefabs = off` -> wave `EnemyEntries`
- Per-prefab prewarm:
  - strategy default: `DefaultPrewarmPerPrefab`
  - optional override per `WeightedEnemyEntry.PrewarmOverride` (`-1` = strategy default)
- Per-prefab cap:
  - `MaxPoolSizePerPrefab`
- Death handling:
  - `EnemyDestroyOnDeath` returns to pool when `EnemyPoolMember` is present and accepted
  - falls back to destroy if pooling is unavailable/disabled

## Events Published

- `SpawnWaveEvent` (existing, published on each wave start)
- `ArenaClearedEvent` (new)
- `ArenaWaveTelemetryChangedEvent` (new)

Event files:

- `Assets/Scripts/Library/Eventing/SceneEvents/SpawnWaveEvent.cs`
- `Assets/Scripts/Library/Eventing/SceneEvents/ArenaClearedEvent.cs`
- `Assets/Scripts/Library/Eventing/SceneEvents/ArenaWaveTelemetryChangedEvent.cs`

## Debug Overlay Integration

`DebugStatsOverlay` subscribes to `ArenaWaveTelemetryChangedEvent` on the scene message bus and renders:

- Strategy name
- Current wave index/name/type
- Runtime state
- Trigger type + delay ETA (when applicable)
- Survival ETA (when applicable)
- Alive count and survival cap
- Spawned count
- Current survival spawn rate

If no active strategy is running, it shows:

- `Arena Spawn: idle`

## Runtime Safety / Notes

- `EnemySpawner` includes idempotent enable guards due current `MonoBehaviourBase.OnEnable` double-call behavior.
- If `DebugContext.DisableEnemySpawns` is true:
  - spawner skips wave execution
  - publishes `ArenaClearedEvent`

## Troubleshooting

- No enemies spawning:
  - Verify `MatchStartingEvent` is firing
  - Verify strategy resolves from override/default
  - Verify wave has valid `EnemyEntries` with positive weight and prefab refs
  - Verify arena has spawn points assigned
- Survival wave “stops early”:
  - Check `SurvivalMaxAlive` (cap may be reached)
- Overlay not updating:
  - Verify scene message bus exists and telemetry events are being published
