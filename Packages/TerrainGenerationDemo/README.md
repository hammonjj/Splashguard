# TerrainGenerationDemo

`TerrainGenerationDemo` is an embedded Unity package for generating deterministic procedural island terrain as a mesh. It includes a demo scene, a default preset, zone-based vertex coloring, generated placeholder props, preview generation, and basic export tools.

## Quick Start

1. Open the demo scene:
   `Packages/TerrainGenerationDemo/Scenes/TerrainGenerationDemo.unity`
2. Select `Terrain Generation Demo Runner` in the hierarchy.
3. The runner uses:
   `Packages/TerrainGenerationDemo/Assets/DefaultIslandPreset.asset`
4. Press Play or enable/regenerate the runner to create the generated child object:
   `Generated Island Terrain`
5. Open the editor tool from:
   `Tools > Terrain Generation > Demo Generator`

The editor window is the easiest way to tune values, preview the island, randomize the seed, generate into the open scene, and export assets.

## Editor Window Workflow

Open `Tools > Terrain Generation > Demo Generator`.

The window has these main controls:

- `Preset`: the `TerrainGeneratorPreset` asset to edit and generate from.
- `Create Preset`: creates a new preset asset.
- `Randomize Seed`: changes only the seed and refreshes the preview.
- `Preview Mode`: switches between height shading and terrain zone colors.
- `Generate In Open Scene`: finds or creates a `TerrainGenerationDemoRunner` and generates terrain in the current scene.
- `Export Mesh`: saves the generated mesh as a Unity `.asset`.
- `Export Preview PNG`: saves the current height or zone preview as a PNG.
- `Export TerrainData`: saves a normalized Unity `TerrainData` asset.

All editable preset fields have Unity tooltips. Hover over a label in the editor window or Inspector to see what the setting does.

## Preset Settings

Presets are `TerrainGeneratorPreset` ScriptableObjects. The default preset lives at:

`Packages/TerrainGenerationDemo/Assets/DefaultIslandPreset.asset`

Setting groups:

- `Grid`: seed, mesh resolution, and world size.
- `Height`: vertical scale and sea level.
- `Noise`: Perlin fBm controls and smooth/ridged terrain mode.
- `Island Shape`: falloff mode, falloff strength, and falloff curve.
- `Archipelago`: multi-island count, radius, spacing, and blend mode.
- `Terrain Zones`: shallow water, beach, rock, and mountain classification thresholds.
- `Zone Color Smoothing Passes`: softens the rendered color transition between terrain zones without changing the underlying zone map used for prop placement.
- `Props`: deterministic tree, rock, grass patch, and driftwood placement settings.
- `Scene Output`: optional mesh collider rebuild.

Important behavior:

- Same seed plus same settings produces the same terrain.
- Height values greater than or equal to `Sea Level` are classified as land.
- `Mask Mode` controls whether the terrain is an open noise field, one island, edge-shaped island, or archipelago.
- Terrain zones are derived from height, sea level, normalized elevation, and slope.
- Props are generated from the zone map and never spawn underwater.
- `Update Mesh Collider` is disabled by default because collider rebuilds are expensive while tuning.

## Terrain Zones And Colors

The generator classifies each height sample into a terrain zone:

- `DeepWater`: below the shallow-water band.
- `ShallowWater`: below sea level but near the shoreline.
- `Beach`: just above sea level.
- `Grassland`: moderate non-steep land.
- `Rock`: steep land below the mountain threshold.
- `Mountain`: high land above the mountain threshold.

The demo terrain material uses vertex colors, so these zones are visible directly on the generated mesh. This is intentionally simple and self-contained; final texture blending can replace it later without changing the core zone map.

`Zone Color Smoothing Passes` blends neighboring zone colors before they are written to the mesh and zone preview. The zone classification itself remains hard and deterministic, so prop rules still use the original beach, grassland, rock, mountain, shallow water, and deep water zones.

## Placeholder Props

The default prop library lives at:

`Packages/TerrainGenerationDemo/Assets/DefaultTerrainPropLibrary.asset`

It maps prop types to package-local placeholder prefabs:

- `Tree`: grassland only.
- `GrassPatch`: grassland only.
- `Rock`: rock and mountain zones.
- `Driftwood`: beach only.

The placeholders are generated from Unity primitive meshes when the runner instantiates the prop prefab. Replace the library entries with production prefabs later without changing the placement system.

## Demo Runner

`TerrainGenerationDemoRunner` is the scene component that applies a preset to a mesh.

It:

- Generates a `Heightfield` through the core generator.
- Generates a `TerrainZoneMap` from height, slope, and sea level.
- Builds separate mesh arrays for real terrain, shallow water, and deep water.
- Applies smoothed zone colors to the mesh vertex colors.
- Creates or reuses one container child named `Generated Island Terrain`.
- Creates or reuses `Real Terrain`, `Shallow Water`, `Deep Water`, and `Props` children under the generated terrain.
- Applies the land mesh collider only to `Real Terrain` when collider updates are enabled.
- Assigns the configured terrain material or a fallback material.

The runner is marked `ExecuteAlways`, so it can generate terrain in edit mode.

## Runtime API

The core generation API is in `BitBox.TerrainGeneration.Core`.

Basic usage:

```csharp
using BitBox.TerrainGeneration.Core;

TerrainGenerationRequest request = TerrainGenerationRequest.Default;
Heightfield heightfield = TerrainGenerator.GenerateHeightfield(request);
TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(
    heightfield,
    TerrainZoneSettings.Default,
    request.WorldSizeX,
    request.WorldSizeZ);
LayeredTerrainMeshes meshes = LayeredTerrainMeshBuilder.Build(
    heightfield,
    zoneMap,
    request.WorldSizeX,
    request.WorldSizeZ,
    TerrainZoneColorPalette.Default,
    smoothingPasses: 2);
```

To apply the mesh in Unity:

```csharp
using BitBox.TerrainGeneration.Unity;

UnityMeshApplier.ApplyTo(meshFilter, meshCollider, meshes.Land, updateCollider: false);
```

Assembly boundaries:

- `BitBox.TerrainGeneration.Core`: deterministic generation, no `UnityEditor`.
- `BitBox.TerrainGeneration.Unity`: UnityEngine adapters, no `UnityEditor`.
- `BitBox.TerrainGeneration.Editor`: editor window and exports.

## Exports

Use the editor window export buttons:

- Mesh `.asset`: reusable real terrain mesh generated from the current preset.
- Preview PNG: colorized height and land/water preview.
- `TerrainData`: normalized Unity terrain asset.

The mesh path warns and switches to 32-bit indices when the generated vertex count exceeds 65535. For runtime/mobile targets that do not support 32-bit mesh indices, add chunking before shipping large resolutions.

## Tests

Package EditMode tests live in:

`Packages/TerrainGenerationDemo/Tests/Editor`

They cover:

- Package scaffold and scene existence.
- Core assembly boundary.
- Mesh topology and regeneration behavior.
- Deterministic generation.
- Sea-level classification.
- Falloff masks and archipelago blending.
- Terrain zone classification and zone color mapping.
- Deterministic prop placement rules.
- Preset conversion, preview checksums, and export helpers.

Run them with Unity Test Runner or batch mode:

```bash
'/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity' \
  -batchmode \
  -nographics \
  -quit \
  -projectPath '/Users/jameshammond/Documents/Github/Personnel/Splashguard' \
  -runTests \
  -testPlatform EditMode \
  -testResults /tmp/TerrainGenerationDemo_EditModeResults.xml \
  -logFile -
```

## Troubleshooting

- If the generated object is missing, select the runner and click `Generate In Open Scene` from the editor window.
- If terrain changes are slow, lower `Resolution X` and `Resolution Z` while tuning.
- If physics is not needed, leave `Update Mesh Collider` disabled.
- If archipelago settings do not seem to matter, set `Mask Mode` to `Archipelago`.
- If props do not appear, verify `Generate Props` is enabled and `Prop Library` references `DefaultTerrainPropLibrary`.
- If zone colors do not appear, verify the terrain material uses `TerrainGenerationDemo/VertexColorUnlit`.
- If batch tests stall before NUnit results, check Unity licensing locally; generation code can still be statically compiled independently.
