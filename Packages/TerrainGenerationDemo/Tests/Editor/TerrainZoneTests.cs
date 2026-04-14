using BitBox.TerrainGeneration.Core;
using BitBox.TerrainGeneration.Unity;
using NUnit.Framework;
using UnityEngine;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class TerrainZoneTests
    {
        [Test]
        public void GenerateZoneMap_DimensionsMatchHeightfield()
        {
            var heightfield = new Heightfield(4, 3, new float[12], seaLevel: 0f);

            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(
                heightfield,
                TerrainZoneSettings.Default,
                worldSizeX: 4f,
                worldSizeZ: 3f);

            Assert.AreEqual(heightfield.Width, zoneMap.Width);
            Assert.AreEqual(heightfield.Depth, zoneMap.Depth);
            Assert.AreEqual(heightfield.Heights.Length, zoneMap.Zones.Length);
        }

        [Test]
        public void GenerateZoneMap_ClassifiesWaterAndBeachBands()
        {
            var heightfield = new Heightfield(
                5,
                2,
                new[] { -3f, -1f, 0.25f, 2f, 5f, -3f, -1f, 0.25f, 2f, 5f },
                seaLevel: 0f);
            var settings = new TerrainZoneSettings(
                shallowWaterDepth: 2f,
                beachHeightBand: 1f,
                rockSlopeThreshold: 99f,
                mountainElevationThreshold: 0.9f);

            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(heightfield, settings, 10f, 2f);

            Assert.AreEqual(TerrainZone.DeepWater, zoneMap.GetZone(0, 0));
            Assert.AreEqual(TerrainZone.ShallowWater, zoneMap.GetZone(1, 0));
            Assert.AreEqual(TerrainZone.Beach, zoneMap.GetZone(2, 0));
        }

        [Test]
        public void GenerateZoneMap_ClassifiesSteepLandAsRock()
        {
            var heightfield = new Heightfield(
                3,
                3,
                new[] { 0f, 0f, 0f, 0f, 3f, 0f, 0f, 10f, 0f },
                seaLevel: -1f);
            var settings = new TerrainZoneSettings(
                shallowWaterDepth: 1f,
                beachHeightBand: 0.1f,
                rockSlopeThreshold: 0.2f,
                mountainElevationThreshold: 0.95f);

            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(heightfield, settings, 2f, 2f);

            Assert.AreEqual(TerrainZone.Rock, zoneMap.GetZone(1, 1));
        }

        [Test]
        public void GenerateZoneMap_ClassifiesHighLandAsMountain()
        {
            var heightfield = new Heightfield(
                3,
                3,
                new[] { 0f, 0f, 0f, 0f, 12f, 0f, 0f, 0f, 0f },
                seaLevel: 0f);
            var settings = new TerrainZoneSettings(
                shallowWaterDepth: 1f,
                beachHeightBand: 0.1f,
                rockSlopeThreshold: 99f,
                mountainElevationThreshold: 0.7f);

            TerrainZoneMap zoneMap = TerrainZoneClassifier.GenerateZoneMap(heightfield, settings, 2f, 2f);

            Assert.AreEqual(TerrainZone.Mountain, zoneMap.GetZone(1, 1));
        }

        [Test]
        public void ZonePalette_MapsEveryZoneToExpectedColor()
        {
            TerrainZoneColorPalette palette = TerrainZoneColorPalette.Default;

            Assert.AreEqual(new Color(0.02f, 0.08f, 0.18f, 1f), palette.GetColor(TerrainZone.DeepWater));
            Assert.AreEqual(new Color(0.06f, 0.48f, 0.58f, 1f), palette.GetColor(TerrainZone.ShallowWater));
            Assert.AreEqual(new Color(0.83f, 0.76f, 0.55f, 1f), palette.GetColor(TerrainZone.Beach));
            Assert.AreEqual(new Color(0.22f, 0.54f, 0.22f, 1f), palette.GetColor(TerrainZone.Grassland));
            Assert.AreEqual(new Color(0.42f, 0.42f, 0.40f, 1f), palette.GetColor(TerrainZone.Rock));
            Assert.AreEqual(new Color(0.72f, 0.72f, 0.68f, 1f), palette.GetColor(TerrainZone.Mountain));
        }

        [Test]
        public void MeshColorizer_AssignsZoneColors()
        {
            var heightfield = new Heightfield(2, 2, new[] { -2f, -0.5f, 0.5f, 5f }, seaLevel: 0f);
            var zoneMap = new TerrainZoneMap(
                2,
                2,
                new[] { TerrainZone.DeepWater, TerrainZone.ShallowWater, TerrainZone.Beach, TerrainZone.Mountain });
            MeshArrays arrays = TerrainMeshBuilder.Build(heightfield, 2f, 2f, includeClassificationColors: false);

            MeshArrays colorized = TerrainZoneMeshColorizer.Colorize(arrays, zoneMap, TerrainZoneColorPalette.Default);

            Assert.AreEqual(TerrainZoneColorPalette.Default.GetColor(TerrainZone.DeepWater), colorized.Colors[0]);
            Assert.AreEqual(TerrainZoneColorPalette.Default.GetColor(TerrainZone.Mountain), colorized.Colors[3]);
        }

        [Test]
        public void MeshColorizer_SmoothedColorsBlendZoneBoundaries()
        {
            var zoneMap = new TerrainZoneMap(
                3,
                3,
                new[]
                {
                    TerrainZone.Grassland, TerrainZone.Grassland, TerrainZone.Grassland,
                    TerrainZone.Grassland, TerrainZone.Mountain, TerrainZone.Grassland,
                    TerrainZone.Grassland, TerrainZone.Grassland, TerrainZone.Grassland
                });
            TerrainZoneColorPalette palette = TerrainZoneColorPalette.Default;

            Color[] hardColors = TerrainZoneMeshColorizer.BuildColors(zoneMap, palette);
            Color[] smoothedColors = TerrainZoneMeshColorizer.BuildSmoothedColors(zoneMap, palette, smoothingPasses: 1);
            Color expectedCenter = (palette.Grassland * 8f + palette.Mountain) / 9f;

            Assert.AreEqual(palette.Mountain, hardColors[4]);
            Assert.AreEqual(expectedCenter.r, smoothedColors[4].r, 0.0001f);
            Assert.AreEqual(expectedCenter.g, smoothedColors[4].g, 0.0001f);
            Assert.AreEqual(expectedCenter.b, smoothedColors[4].b, 0.0001f);
            Assert.AreEqual(expectedCenter.a, smoothedColors[4].a, 0.0001f);
        }

        [Test]
        public void ZonePreview_Checksum_IsStable()
        {
            var zoneMap = new TerrainZoneMap(
                2,
                2,
                new[] { TerrainZone.DeepWater, TerrainZone.ShallowWater, TerrainZone.Beach, TerrainZone.Grassland });
            Texture2D first = TexturePreviewBuilder.BuildZonePreview(zoneMap, TerrainZoneColorPalette.Default);
            Texture2D second = TexturePreviewBuilder.BuildZonePreview(zoneMap, TerrainZoneColorPalette.Default);
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
