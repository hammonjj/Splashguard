using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Unity;
using UnityEditor;
using UnityEngine;

namespace BitBox.TerrainGeneration.Editor
{
    public sealed class TerrainGeneratorWindow : EditorWindow
    {
        private const string DefaultPresetPath = "Packages/TerrainGenerationDemo/Assets/DefaultIslandPreset.asset";

        [SerializeField] private TerrainGeneratorPreset _preset;
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
                "Preset",
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
                if (GUILayout.Button("Create Preset"))
                {
                    CreatePresetAsset();
                }

                EditorGUI.BeginDisabledGroup(_preset == null);
                if (GUILayout.Button("Randomize Seed"))
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
            if (_preview == null)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Height / Land Mask Preview", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxHeight(256f));
            EditorGUI.DrawPreviewTexture(rect, _preview, null, ScaleMode.ScaleToFit);
        }

        private void DrawActions()
        {
            EditorGUI.BeginDisabledGroup(_preset == null);
            GUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate In Open Scene"))
                {
                    GenerateInOpenScene();
                }

                if (GUILayout.Button("Export Mesh"))
                {
                    ExportMesh();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export Preview PNG"))
                {
                    ExportPreviewPng();
                }

                if (GUILayout.Button("Export TerrainData"))
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
            _preview = TexturePreviewBuilder.BuildHeightPreview(heightfield);
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
            MeshArrays arrays = TerrainMeshBuilder.Build(heightfield, request.WorldSizeX, request.WorldSizeZ, true);
            Export.MeshAssetExporter.SaveMeshAsset(arrays, path);
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
            Export.HeightmapExporter.SavePng(heightfield, path);
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
    }
}
