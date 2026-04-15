using System.IO;
using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Editor.Export;
using BitBox.TerrainGeneration.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class PresetPreviewExportTests
    {
        private const string TestOutputFolder = "Assets/TerrainGenerationDemoTestOutput";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestOutputFolder))
            {
                AssetDatabase.DeleteAsset(TestOutputFolder);
            }
        }

        [Test]
        public void Preset_ToRequest_UsesSerializedValues()
        {
            var preset = ScriptableObject.CreateInstance<TerrainGeneratorPreset>();
            try
            {
                var serializedPreset = new SerializedObject(preset);
                serializedPreset.FindProperty("_seed").intValue = 9001;
                serializedPreset.FindProperty("_resolutionX").intValue = 17;
                serializedPreset.FindProperty("_resolutionZ").intValue = 19;
                serializedPreset.FindProperty("_beachHeightBand").floatValue = 1.75f;
                serializedPreset.FindProperty("_zoneColorSmoothingPasses").intValue = 4;
                serializedPreset.FindProperty("_beachColor").colorValue = new Color(0.25f, 0.2f, 0.12f, 1f);
                serializedPreset.FindProperty("_grasslandColor").colorValue = new Color(0.05f, 0.18f, 0.07f, 1f);
                serializedPreset.FindProperty("_treeDensity").floatValue = 11f;
                serializedPreset.ApplyModifiedPropertiesWithoutUndo();

                TerrainGenerationRequest request = preset.ToRequest();
                TerrainZoneSettings zoneSettings = preset.ToZoneSettings();
                TerrainZoneColorPalette colorPalette = preset.ToZoneColorPalette();
                TerrainPropPlacementSettings propSettings = preset.ToPropPlacementSettings();

                Assert.AreEqual(9001, request.Seed);
                Assert.AreEqual(17, request.ResolutionX);
                Assert.AreEqual(19, request.ResolutionZ);
                Assert.AreEqual(1.75f, zoneSettings.BeachHeightBand);
                Assert.AreEqual(4, preset.ZoneColorSmoothingPasses);
                Assert.AreEqual(new Color(0.25f, 0.2f, 0.12f, 1f), colorPalette.Beach);
                Assert.AreEqual(new Color(0.05f, 0.18f, 0.07f, 1f), colorPalette.Grassland);
                Assert.AreEqual(11f, propSettings.TreeDensity);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void Preset_RandomizeSeed_ChangesSeed()
        {
            var preset = ScriptableObject.CreateInstance<TerrainGeneratorPreset>();
            try
            {
                int originalSeed = preset.Seed;
                preset.RandomizeSeed();
                Assert.AreNotEqual(originalSeed, preset.Seed);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void PreviewTexture_Checksum_IsStableForFixedRequest()
        {
            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(TerrainGenerationRequest.Default);
            Texture2D first = TexturePreviewBuilder.BuildHeightPreview(heightfield);
            Texture2D second = TexturePreviewBuilder.BuildHeightPreview(heightfield);
            try
            {
                Assert.AreEqual(TextureChecksum(first), TextureChecksum(second));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void MeshExporter_SavesReloadableMeshAsset()
        {
            EnsureTestOutputFolder();
            string path = $"{TestOutputFolder}/GeneratedMesh.asset";
            TerrainGenerationRequest request = new TerrainGenerationRequest(
                seed: 8,
                resolutionX: 16,
                resolutionZ: 16,
                worldSizeX: 24f,
                worldSizeZ: 24f,
                heightScale: 5f,
                seaLevel: 0f,
                noiseScale: 12f,
                octaves: 3,
                persistence: 0.5f,
                lacunarity: 2f,
                noiseMode: TerrainNoiseMode.Smooth,
                maskMode: TerrainMaskMode.Radial,
                falloffStrength: 4f,
                falloffExponent: 1.5f,
                islandCount: 1,
                islandRadius: 0.4f,
                minIslandSeparation: 0.2f,
                blendMode: MultiIslandBlendMode.Max);

            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(request);
            MeshArrays arrays = TerrainMeshBuilder.Build(heightfield, request.WorldSizeX, request.WorldSizeZ, true);

            MeshAssetExporter.SaveMeshAsset(arrays, path);
            Mesh loadedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            Assert.IsNotNull(loadedMesh);
            Assert.AreEqual(arrays.Vertices.Length, loadedMesh.vertexCount);
        }

        [Test]
        public void HeightmapExporter_SavesReloadablePng()
        {
            EnsureTestOutputFolder();
            string path = $"{TestOutputFolder}/Preview.png";
            Heightfield heightfield = TerrainGenerator.GenerateHeightfield(TerrainGenerationRequest.Default);

            HeightmapExporter.SavePng(heightfield, path);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            Assert.IsNotNull(texture);
        }

        [Test]
        public void TerrainDataExporter_NormalizesSourceHeights()
        {
            var heightfield = new Heightfield(
                3,
                3,
                new[]
                {
                    -1f, 0f, 1f,
                    2f, 3f, 4f,
                    5f, 6f, 7f
                },
                seaLevel: 0f);

            TerrainData terrainData = TerrainDataExporter.CreateTerrainData(heightfield, 12f, 12f);
            float[,] heights = terrainData.GetHeights(0, 0, 3, 3);

            Assert.AreEqual(0f, heights[0, 0], 0.0001f);
            Assert.AreEqual(1f, heights[2, 2], 0.0001f);
        }

        private static void EnsureTestOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder(TestOutputFolder))
            {
                AssetDatabase.CreateFolder("Assets", "TerrainGenerationDemoTestOutput");
            }
        }

        private static int TextureChecksum(Texture2D texture)
        {
            unchecked
            {
                int hash = 17;
                Color32[] pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    hash = hash * 31 + pixel.r;
                    hash = hash * 31 + pixel.g;
                    hash = hash * 31 + pixel.b;
                    hash = hash * 31 + pixel.a;
                }

                return hash;
            }
        }
    }
}
