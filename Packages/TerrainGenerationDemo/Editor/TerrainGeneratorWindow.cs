using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Unity;
using UnityEditor;
using UnityEngine;

namespace BitBox.TerrainGeneration.Editor
{
    public sealed class TerrainGeneratorWindow : EditorWindow
    {
        private const string DefaultPresetPath = "Packages/TerrainGenerationDemo/Assets/DefaultIslandPreset.asset";
        private const string DefaultPropLibraryPath = "Packages/TerrainGenerationDemo/Assets/DefaultTerrainPropLibrary.asset";
        private static readonly GUIContent PresetLabel = new(
            "Preset",
            "ScriptableObject containing all terrain generation settings edited by this window.");
        private static readonly GUIContent CreatePresetLabel = new(
            "Create Preset",
            "Create a new TerrainGeneratorPreset asset with default values.");
        private static readonly GUIContent RandomizeSeedLabel = new(
            "Randomize Seed",
            "Change only the seed, then rebuild the preview so the same settings produce a different island.");
        private static readonly GUIContent GenerateLabel = new(
            "Generate In Open Scene",
            "Find or create a TerrainGenerationDemoRunner in the current scene and generate terrain from the selected preset.");
        private static readonly GUIContent ExportMeshLabel = new(
            "Export Mesh",
            "Generate the current terrain and save the mesh as a reusable Unity .asset file.");
        private static readonly GUIContent ExportPreviewLabel = new(
            "Export Preview PNG",
            "Generate the current terrain preview texture and save it as a PNG asset.");
        private static readonly GUIContent ExportTerrainDataLabel = new(
            "Export TerrainData",
            "Generate the current terrain and save it as a normalized Unity TerrainData asset.");
        private static readonly GUIContent PreviewModeLabel = new(
            "Preview Mode",
            "Choose whether the preview displays generated height shading or terrain zones.");

        [SerializeField] private TerrainGeneratorPreset _preset;
        [SerializeField] private TerrainPreviewMode _previewMode = TerrainPreviewMode.Zones;
        private SerializedObject _serializedPreset;
        private Texture2D _preview;
        private Vector2 _scroll;

        [MenuItem("Tools/Terrain Generation/Demo Generator")]
        public static void Open()
        {
            GetWindow<TerrainGeneratorWindow>("Terrain Generator");
        }

        private void OnEnable()
        {
            if (_preset == null)
            {
                _preset = AssetDatabase.LoadAssetAtPath<TerrainGeneratorPreset>(DefaultPresetPath);
            }

            RebuildSerializedPreset();
            RebuildPreview();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _preset = (TerrainGeneratorPreset)EditorGUILayout.ObjectField(
                PresetLabel,
                _preset,
                typeof(TerrainGeneratorPreset),
                allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                RebuildSerializedPreset();
                RebuildPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(CreatePresetLabel))
                {
                    CreatePresetAsset();
                }

                EditorGUI.BeginDisabledGroup(_preset == null);
                if (GUILayout.Button(RandomizeSeedLabel))
                {
                    Undo.RecordObject(_preset, "Randomize Terrain Seed");
                    _preset.RandomizeSeed();
                    EditorUtility.SetDirty(_preset);
                    RebuildSerializedPreset();
                    RebuildPreview();
                }
                EditorGUI.EndDisabledGroup();
            }

            if (_serializedPreset != null)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                _serializedPreset.Update();
                DrawPresetProperties(_serializedPreset);
                if (_serializedPreset.ApplyModifiedProperties())
                {
                    RebuildPreview();
                }
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("Assign or create a TerrainGeneratorPreset.", MessageType.Info);
            }

            DrawPreview();
            DrawActions();
        }

        private void DrawPresetProperties(SerializedObject serializedPreset)
        {
            SerializedProperty property = serializedPreset.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(property, includeChildren: true);
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(property, includeChildren: true);
            }
        }

        private void DrawPreview()
        {
            EditorGUI.BeginChangeCheck();
            _previewMode = (TerrainPreviewMode)EditorGUILayout.EnumPopup(PreviewModeLabel, _previewMode);
            if (EditorGUI.EndChangeCheck())
            {
                RebuildPreview();
            }

            if (_preview == null)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label(_previewMode == TerrainPreviewMode.Height ? "Height Preview" : "Zone Preview", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxHeight(256f));
            EditorGUI.DrawPreviewTexture(rect, _preview, null, ScaleMode.ScaleToFit);
        }

        private void DrawActions()
        {
            EditorGUI.BeginDisabledGroup(_preset == null);
            GUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(GenerateLabel))
                {
                    GenerateInOpenScene();
                }

                if (GUILayout.Button(ExportMeshLabel))
                {
                    ExportMesh();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(ExportPreviewLabel))
                {
                    ExportPreviewPng();
                }

                if (GUILayout.Button(ExportTerrainDataLabel))
                {
                    ExportTerrainData();
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void RebuildSerializedPreset()
        {
            _serializedPreset = _preset != null ? new SerializedObject(_preset) : null;
        }

        private void RebuildPreview()
        {
            if (_preset == null)
            {
                _preview = null;
                return;
            }

            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(_preset.ToRequest());
            if (_previewMode == TerrainPreviewMode.Height)
            {
                _preview = TexturePreviewBuilder.BuildHeightPreview(heightfield);
                return;
            }

            TerrainGenerationRequest request = _preset.ToRequest();
            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(
                heightfield,
                _preset.ToZoneSettings(),
                request.WorldSizeX,
                request.WorldSizeZ);
            _preview = TexturePreviewBuilder.BuildZonePreview(
                zoneMap,
                TerrainZoneColorPalette.Default,
                _preset.ZoneColorSmoothingPasses);
        }

        private void CreatePresetAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Terrain Generator Preset",
                "TerrainGeneratorPreset",
                "asset",
                "Choose where to save the terrain generator preset.");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var preset = CreateInstance<TerrainGeneratorPreset>();
            TerrainPropLibrary defaultPropLibrary = AssetDatabase.LoadAssetAtPath<TerrainPropLibrary>(DefaultPropLibraryPath);
            if (defaultPropLibrary != null)
            {
                var serializedNewPreset = new SerializedObject(preset);
                serializedNewPreset.FindProperty("_propLibrary").objectReferenceValue = defaultPropLibrary;
                serializedNewPreset.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            _preset = preset;
            RebuildSerializedPreset();
            RebuildPreview();
        }

        private void GenerateInOpenScene()
        {
            TerrainGenerationDemoRunner runner = Object.FindFirstObjectByType<TerrainGenerationDemoRunner>();
            if (runner == null)
            {
                var root = new GameObject("Terrain Generation Demo Runner");
                Undo.RegisterCreatedObjectUndo(root, "Create Terrain Generation Demo Runner");
                runner = Undo.AddComponent<TerrainGenerationDemoRunner>(root);
            }

            Undo.RecordObject(runner, "Assign Terrain Generator Preset");
            runner.Preset = _preset;
            runner.Generate();
            EditorUtility.SetDirty(runner);
        }

        private void ExportMesh()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export Generated Mesh",
                "GeneratedIslandMesh",
                "asset",
                "Choose where to save the generated mesh asset.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            TerrainGenerationRequest request = _preset.ToRequest();
            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(request);
            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(
                heightfield,
                _preset.ToZoneSettings(),
                request.WorldSizeX,
                request.WorldSizeZ);
            LayeredTerrainMeshes meshes = LayeredTerrainMeshBuilder.Build(
                heightfield,
                zoneMap,
                request.WorldSizeX,
                request.WorldSizeZ,
                TerrainZoneColorPalette.Default,
                _preset.ZoneColorSmoothingPasses);
            Export.MeshAssetExporter.SaveMeshAsset(meshes.Land, path);
        }

        private void ExportPreviewPng()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export Terrain Preview",
                "GeneratedIslandPreview",
                "png",
                "Choose where to save the generated terrain preview.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(_preset.ToRequest());
            if (_previewMode == TerrainPreviewMode.Height)
            {
                Export.HeightmapExporter.SavePng(heightfield, path);
                return;
            }

            TerrainGenerationRequest request = _preset.ToRequest();
            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(
                heightfield,
                _preset.ToZoneSettings(),
                request.WorldSizeX,
                request.WorldSizeZ);
            Texture2D zonePreview = TexturePreviewBuilder.BuildZonePreview(
                zoneMap,
                TerrainZoneColorPalette.Default,
                _preset.ZoneColorSmoothingPasses);
            Export.HeightmapExporter.SavePng(zonePreview, path);
        }

        private void ExportTerrainData()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export TerrainData",
                "GeneratedIslandTerrainData",
                "asset",
                "Choose where to save the generated TerrainData asset.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            TerrainGenerationRequest request = _preset.ToRequest();
            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(request);
            TerrainData terrainData = Export.TerrainDataExporter.CreateTerrainData(
                heightfield,
                request.WorldSizeX,
                request.WorldSizeZ);
            AssetDatabase.CreateAsset(terrainData, path);
            AssetDatabase.SaveAssets();
        }

        private enum TerrainPreviewMode
        {
            Height = 0,
            Zones = 1
        }
    }
}
