using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Unity;
using UnityEditor;
using UnityEngine;

namespace BitBox.TerrainGeneration.Editor
{
    public sealed class TerraForgeWindow : EditorWindow
    {
        private const string DefaultPresetPath = "Packages/com.bitboxarcade.terraforge/Assets/DefaultWaterParkPreset.asset";
        private const string DefaultPropLibraryPath = "Packages/com.bitboxarcade.terraforge/Assets/DefaultTerrainPropLibrary.asset";
        private static readonly GUIContent PresetLabel = new(
            "Preset",
            "ScriptableObject containing all terrain generation settings edited by this window.");
        private static readonly GUIContent CreatePresetLabel = new(
            "Create Preset",
            "Create a new TerrainGeneratorPreset asset with default values.");
        private static readonly GUIContent RandomizeSeedLabel = new(
            "Randomize Seed",
            "Change only the seed, then rebuild the preview so the same settings produce different terrain.");
        private static readonly GUIContent GenerateLabel = new(
            "Generate In Open Scene",
            "Find or create the TerraForge runner in the current scene and generate terrain from the selected preset.");
        private static readonly GUIContent ExportMeshLabel = new(
            "Export Mesh",
            "Generate the current terrain and save the mesh as a reusable Unity .asset file.");
        private static readonly GUIContent ExportObjLabel = new(
            "Export OBJ",
            "Generate the current terrain and save a Blender-friendly OBJ with separate land and water objects.");
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

        [MenuItem("Tools/BitBox Arcade/TerraForge")]
        public static void Open()
        {
            GetWindow<TerraForgeWindow>("TerraForge");
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

                if (GUILayout.Button(ExportObjLabel))
                {
                    ExportObj();
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
                _preset.ToZoneColorPalette(),
                _preset.ZoneColorSmoothingPasses);
        }

        private void CreatePresetAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create TerraForge Preset",
                "TerraForgePreset",
                "asset",
                "Choose where to save the TerraForge preset.");

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

            Undo.RecordObject(runner, "Assign TerraForge Preset");
            runner.Preset = _preset;
            runner.Generate();
            EditorUtility.SetDirty(runner);
        }

        private void ExportMesh()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export TerraForge Mesh",
                "TerraForgeMesh",
                "asset",
                "Choose where to save the generated mesh asset.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            LayeredTerrainMeshes meshes = BuildLayeredMeshes();
            Export.MeshAssetExporter.SaveMeshAsset(meshes.Land, path);
        }

        private void ExportObj()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export TerraForge OBJ",
                Application.dataPath,
                "TerraForgeTerrain",
                "obj");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            LayeredTerrainMeshes meshes = BuildLayeredMeshes();
            Export.ObjMeshExporter.SaveObj(meshes, path);
        }

        private void ExportPreviewPng()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export TerraForge Preview",
                "TerraForgePreview",
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
                _preset.ToZoneColorPalette(),
                _preset.ZoneColorSmoothingPasses);
            Export.HeightmapExporter.SavePng(zonePreview, path);
        }

        private void ExportTerrainData()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export TerraForge TerrainData",
                "TerraForgeTerrainData",
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

        private LayeredTerrainMeshes BuildLayeredMeshes()
        {
            TerrainGenerationRequest request = _preset.ToRequest();
            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(request);
            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(
                heightfield,
                _preset.ToZoneSettings(),
                request.WorldSizeX,
                request.WorldSizeZ);
            return LayeredTerrainMeshBuilder.Build(
                heightfield,
                zoneMap,
                request.WorldSizeX,
                request.WorldSizeZ,
                _preset.ToZoneColorPalette(),
                _preset.ZoneColorSmoothingPasses,
                request);
        }

        private enum TerrainPreviewMode
        {
            Height = 0,
            Zones = 1
        }
    }
}
