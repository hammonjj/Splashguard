using System.Collections.Generic;
using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public static class LayeredTerrainMeshBuilder
    {
        private const int RoundedBasinSegmentsPerCorner = 24;

        public static LayeredTerrainMeshes Build(
            Heightfield heightfield,
            TerrainZoneMap zoneMap,
            float worldSizeX,
            float worldSizeZ,
            TerrainZoneColorPalette palette,
            int smoothingPasses)
        {
            return Build(
                heightfield,
                zoneMap,
                worldSizeX,
                worldSizeZ,
                palette,
                smoothingPasses,
                includeShorelineWalls: false,
                shorelineFloorHeight: heightfield.MinHeight);
        }

        public static LayeredTerrainMeshes Build(
            Heightfield heightfield,
            TerrainZoneMap zoneMap,
            float worldSizeX,
            float worldSizeZ,
            TerrainZoneColorPalette palette,
            int smoothingPasses,
            bool includeShorelineWalls,
            float shorelineFloorHeight)
        {
            return BuildInternal(
                heightfield,
                zoneMap,
                worldSizeX,
                worldSizeZ,
                palette,
                smoothingPasses,
                includeShorelineWalls,
                shorelineFloorHeight,
                default,
                useRoundedBasinPoolMesh: false);
        }

        public static LayeredTerrainMeshes Build(
            Heightfield heightfield,
            TerrainZoneMap zoneMap,
            float worldSizeX,
            float worldSizeZ,
            TerrainZoneColorPalette palette,
            int smoothingPasses,
            TerrainGenerationRequest request)
        {
            bool includeShorelineWalls = request.UnderwaterProfile == TerrainUnderwaterProfile.FlatFloor;
            float shorelineFloorHeight = request.SeaLevel - request.FlatFloorDepth;
            bool useRoundedBasinPoolMesh = includeShorelineWalls
                && request.MaskMode == TerrainMaskMode.RoundedBasin;

            return BuildInternal(
                heightfield,
                zoneMap,
                worldSizeX,
                worldSizeZ,
                palette,
                smoothingPasses,
                includeShorelineWalls,
                shorelineFloorHeight,
                request,
                useRoundedBasinPoolMesh);
        }

        private static LayeredTerrainMeshes BuildInternal(
            Heightfield heightfield,
            TerrainZoneMap zoneMap,
            float worldSizeX,
            float worldSizeZ,
            TerrainZoneColorPalette palette,
            int smoothingPasses,
            bool includeShorelineWalls,
            float shorelineFloorHeight,
            TerrainGenerationRequest request,
            bool useRoundedBasinPoolMesh)
        {
            MeshArrays baseArrays = TerrainMeshBuilder.Build(
                heightfield,
                worldSizeX,
                worldSizeZ,
                includeClassificationColors: false);
            Color[] colors = TerrainZoneMeshColorizer.BuildSmoothedColors(zoneMap, palette, smoothingPasses);
            MeshArrays land = useRoundedBasinPoolMesh
                ? BuildRoundedBasinPoolLandLayer(baseArrays, heightfield, colors, palette, request, worldSizeX, worldSizeZ)
                : includeShorelineWalls
                    ? BuildLandLayerWithShorelineWalls(baseArrays, heightfield, colors, palette.Rock, shorelineFloorHeight)
                    : BuildLayer(baseArrays, zoneMap, colors, TerrainMeshLayer.Land);

            return new LayeredTerrainMeshes(
                land,
                BuildLayer(baseArrays, zoneMap, colors, TerrainMeshLayer.ShallowWater),
                BuildLayer(baseArrays, zoneMap, colors, TerrainMeshLayer.DeepWater));
        }

        private static MeshArrays BuildRoundedBasinPoolLandLayer(
            MeshArrays baseArrays,
            Heightfield heightfield,
            Color[] colors,
            TerrainZoneColorPalette palette,
            TerrainGenerationRequest request,
            float worldSizeX,
            float worldSizeZ)
        {
            var vertices = new List<Vector3>(baseArrays.Vertices);
            var uvs = new List<Vector2>(baseArrays.Uvs);
            var meshColors = colors != null ? new List<Color>(colors) : null;
            var triangles = new List<int>(baseArrays.Triangles.Length);

            for (int z = 0; z < heightfield.Depth - 1; z++)
            {
                for (int x = 0; x < heightfield.Width - 1; x++)
                {
                    bool land0 = SeaLevelClassifier.IsLand(heightfield.Heights[heightfield.IndexOf(x, z)], heightfield.SeaLevel);
                    bool land1 = SeaLevelClassifier.IsLand(heightfield.Heights[heightfield.IndexOf(x + 1, z)], heightfield.SeaLevel);
                    bool land2 = SeaLevelClassifier.IsLand(heightfield.Heights[heightfield.IndexOf(x, z + 1)], heightfield.SeaLevel);
                    bool land3 = SeaLevelClassifier.IsLand(heightfield.Heights[heightfield.IndexOf(x + 1, z + 1)], heightfield.SeaLevel);

                    if (land0 && land1 && land2 && land3)
                    {
                        AddFullLandCellTriangles(heightfield, x, z, triangles);
                    }
                }
            }

            AddRoundedBasinPoolGeometry(
                request,
                worldSizeX,
                worldSizeZ,
                palette.Beach,
                palette.Rock,
                vertices,
                uvs,
                meshColors,
                triangles);

            return new MeshArrays(
                vertices.ToArray(),
                triangles.ToArray(),
                uvs.ToArray(),
                meshColors?.ToArray());
        }

        private static void AddRoundedBasinPoolGeometry(
            TerrainGenerationRequest request,
            float worldSizeX,
            float worldSizeZ,
            Color floorColor,
            Color borderColor,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            float seaLevel = request.SeaLevel;
            float floorHeight = request.SeaLevel - request.FlatFloorDepth;
            float borderHeight = request.SeaLevel + request.PoolBorderHeight;
            float outerWidth = Mathf.Min(0.98f, request.BasinWidth + request.PoolBorderWidth * 2f);
            float outerDepth = Mathf.Min(0.98f, request.BasinDepth + request.PoolBorderWidth * 2f);
            float outerCornerRadius = request.BasinCornerRadius + request.PoolBorderWidth;

            Vector2[] innerPath = BuildRoundedBasinPath(
                Mathf.Min(0.98f, request.BasinWidth),
                Mathf.Min(0.98f, request.BasinDepth),
                request.BasinCornerRadius,
                worldSizeX,
                worldSizeZ);
            Vector2[] outerPath = BuildRoundedBasinPath(
                outerWidth,
                outerDepth,
                outerCornerRadius,
                worldSizeX,
                worldSizeZ);

            AddRoundedBasinFloor(innerPath, floorHeight, floorColor, worldSizeX, worldSizeZ, vertices, uvs, colors, triangles);
            AddRoundedBasinWall(innerPath, seaLevel, floorHeight, borderColor, worldSizeX, worldSizeZ, vertices, uvs, colors, triangles);

            if (request.PoolBorderWidth <= 0f && request.PoolBorderHeight <= 0f)
            {
                return;
            }

            AddRoundedBasinBorder(
                innerPath,
                outerPath,
                seaLevel,
                borderHeight,
                borderColor,
                worldSizeX,
                worldSizeZ,
                vertices,
                uvs,
                colors,
                triangles);
        }

        private static Vector2[] BuildRoundedBasinPath(
            float width,
            float depth,
            float cornerRadius,
            float worldSizeX,
            float worldSizeZ)
        {
            float halfWidth = Mathf.Clamp(width, 0.01f, 0.98f) * 0.5f;
            float halfDepth = Mathf.Clamp(depth, 0.01f, 0.98f) * 0.5f;
            float radius = Mathf.Min(Mathf.Max(0.0001f, cornerRadius), Mathf.Min(halfWidth, halfDepth));
            var points = new List<Vector2>((RoundedBasinSegmentsPerCorner + 1) * 4);

            AddRoundedBasinCorner(points, halfWidth - radius, halfDepth - radius, radius, 90f, 0f, worldSizeX, worldSizeZ);
            AddRoundedBasinCorner(points, halfWidth - radius, -halfDepth + radius, radius, 0f, -90f, worldSizeX, worldSizeZ);
            AddRoundedBasinCorner(points, -halfWidth + radius, -halfDepth + radius, radius, -90f, -180f, worldSizeX, worldSizeZ);
            AddRoundedBasinCorner(points, -halfWidth + radius, halfDepth - radius, radius, 180f, 90f, worldSizeX, worldSizeZ);

            return points.ToArray();
        }

        private static void AddRoundedBasinCorner(
            List<Vector2> points,
            float centerX,
            float centerZ,
            float radius,
            float startDegrees,
            float endDegrees,
            float worldSizeX,
            float worldSizeZ)
        {
            for (int i = 0; i <= RoundedBasinSegmentsPerCorner; i++)
            {
                float t = i / (float)RoundedBasinSegmentsPerCorner;
                float angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                float x = centerX + Mathf.Cos(angle) * radius;
                float z = centerZ + Mathf.Sin(angle) * radius;
                points.Add(new Vector2(x * worldSizeX, z * worldSizeZ));
            }
        }

        private static void AddRoundedBasinFloor(
            Vector2[] innerPath,
            float floorHeight,
            Color floorColor,
            float worldSizeX,
            float worldSizeZ,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            int center = AddProceduralVertex(
                new Vector3(0f, floorHeight, 0f),
                worldSizeX,
                worldSizeZ,
                floorColor,
                vertices,
                uvs,
                colors);
            var floorIndices = new int[innerPath.Length];
            for (int i = 0; i < innerPath.Length; i++)
            {
                floorIndices[i] = AddProceduralVertex(
                    new Vector3(innerPath[i].x, floorHeight, innerPath[i].y),
                    worldSizeX,
                    worldSizeZ,
                    floorColor,
                    vertices,
                    uvs,
                    colors);
            }

            for (int i = 0; i < floorIndices.Length; i++)
            {
                int next = (i + 1) % floorIndices.Length;
                AddTriangleIfNotDegenerate(center, floorIndices[i], floorIndices[next], vertices, triangles);
            }
        }

        private static void AddRoundedBasinWall(
            Vector2[] innerPath,
            float seaLevel,
            float floorHeight,
            Color wallColor,
            float worldSizeX,
            float worldSizeZ,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            int[] top = AddPathVertices(innerPath, seaLevel, wallColor, worldSizeX, worldSizeZ, vertices, uvs, colors);
            int[] bottom = AddPathVertices(innerPath, floorHeight, wallColor, worldSizeX, worldSizeZ, vertices, uvs, colors);
            for (int i = 0; i < innerPath.Length; i++)
            {
                int next = (i + 1) % innerPath.Length;
                AddQuad(top[i], top[next], bottom[next], bottom[i], vertices, triangles, doubleSided: true);
            }
        }

        private static void AddRoundedBasinBorder(
            Vector2[] innerPath,
            Vector2[] outerPath,
            float seaLevel,
            float borderHeight,
            Color borderColor,
            float worldSizeX,
            float worldSizeZ,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            int[] innerTop = AddPathVertices(innerPath, borderHeight, borderColor, worldSizeX, worldSizeZ, vertices, uvs, colors);
            int[] outerTop = AddPathVertices(outerPath, borderHeight, borderColor, worldSizeX, worldSizeZ, vertices, uvs, colors);
            int[] innerSea = AddPathVertices(innerPath, seaLevel, borderColor, worldSizeX, worldSizeZ, vertices, uvs, colors);
            int[] outerSea = AddPathVertices(outerPath, seaLevel, borderColor, worldSizeX, worldSizeZ, vertices, uvs, colors);

            for (int i = 0; i < innerPath.Length; i++)
            {
                int next = (i + 1) % innerPath.Length;
                AddQuad(outerTop[i], outerTop[next], innerTop[next], innerTop[i], vertices, triangles, doubleSided: false);
                AddQuad(innerTop[i], innerTop[next], innerSea[next], innerSea[i], vertices, triangles, doubleSided: true);
                AddQuad(outerTop[i], outerSea[i], outerSea[next], outerTop[next], vertices, triangles, doubleSided: true);
            }
        }

        private static int[] AddPathVertices(
            Vector2[] path,
            float height,
            Color color,
            float worldSizeX,
            float worldSizeZ,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors)
        {
            var indices = new int[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                indices[i] = AddProceduralVertex(
                    new Vector3(path[i].x, height, path[i].y),
                    worldSizeX,
                    worldSizeZ,
                    color,
                    vertices,
                    uvs,
                    colors);
            }

            return indices;
        }

        private static MeshArrays BuildLandLayerWithShorelineWalls(
            MeshArrays baseArrays,
            Heightfield heightfield,
            Color[] colors,
            Color wallColor,
            float shorelineFloorHeight)
        {
            var vertices = new List<Vector3>(baseArrays.Vertices);
            var uvs = new List<Vector2>(baseArrays.Uvs);
            var meshColors = colors != null ? new List<Color>(colors) : null;
            var triangles = new List<int>(baseArrays.Triangles.Length);

            for (int z = 0; z < heightfield.Depth - 1; z++)
            {
                for (int x = 0; x < heightfield.Width - 1; x++)
                {
                    ShorelineVertex c0 = BuildShorelineVertex(baseArrays, colors, heightfield.IndexOf(x, z));
                    ShorelineVertex c1 = BuildShorelineVertex(baseArrays, colors, heightfield.IndexOf(x + 1, z));
                    ShorelineVertex c2 = BuildShorelineVertex(baseArrays, colors, heightfield.IndexOf(x, z + 1));
                    ShorelineVertex c3 = BuildShorelineVertex(baseArrays, colors, heightfield.IndexOf(x + 1, z + 1));

                    bool land0 = SeaLevelClassifier.IsLand(c0.Position.y, heightfield.SeaLevel);
                    bool land1 = SeaLevelClassifier.IsLand(c1.Position.y, heightfield.SeaLevel);
                    bool land2 = SeaLevelClassifier.IsLand(c2.Position.y, heightfield.SeaLevel);
                    bool land3 = SeaLevelClassifier.IsLand(c3.Position.y, heightfield.SeaLevel);
                    int landCount = (land0 ? 1 : 0) + (land1 ? 1 : 0) + (land2 ? 1 : 0) + (land3 ? 1 : 0);

                    if (landCount == 0)
                    {
                        continue;
                    }

                    if (landCount == 4)
                    {
                        AddFullLandCellTriangles(heightfield, x, z, triangles);
                        continue;
                    }

                    var cell = new[] { c0, c2, c3, c1 };
                    List<ShorelineVertex> polygon = ClipLandPolygon(cell, heightfield.SeaLevel);
                    AddClippedLandPolygon(polygon, vertices, uvs, meshColors, triangles);
                    AddShorelineWalls(polygon, heightfield.SeaLevel, shorelineFloorHeight, wallColor, vertices, uvs, meshColors, triangles);
                }
            }

            return new MeshArrays(
                vertices.ToArray(),
                triangles.ToArray(),
                uvs.ToArray(),
                meshColors?.ToArray());
        }

        private static MeshArrays BuildLayer(
            MeshArrays baseArrays,
            TerrainZoneMap zoneMap,
            Color[] colors,
            TerrainMeshLayer layer)
        {
            var triangles = new List<int>(baseArrays.Triangles.Length);

            for (int z = 0; z < zoneMap.Depth - 1; z++)
            {
                for (int x = 0; x < zoneMap.Width - 1; x++)
                {
                    TerrainMeshLayer quadLayer = ClassifyQuadLayer(zoneMap, x, z);
                    if (quadLayer != layer)
                    {
                        continue;
                    }

                    int i0 = zoneMap.IndexOf(x, z);
                    int i1 = i0 + 1;
                    int i2 = i0 + zoneMap.Width;
                    int i3 = i2 + 1;

                    triangles.Add(i0);
                    triangles.Add(i2);
                    triangles.Add(i1);
                    triangles.Add(i1);
                    triangles.Add(i2);
                    triangles.Add(i3);
                }
            }

            return new MeshArrays(
                baseArrays.Vertices,
                triangles.ToArray(),
                baseArrays.Uvs,
                colors);
        }

        private static TerrainMeshLayer ClassifyQuadLayer(TerrainZoneMap zoneMap, int x, int z)
        {
            int land = 0;
            int shallow = 0;
            int deep = 0;
            Count(zoneMap.GetZone(x, z), ref land, ref shallow, ref deep);
            Count(zoneMap.GetZone(x + 1, z), ref land, ref shallow, ref deep);
            Count(zoneMap.GetZone(x, z + 1), ref land, ref shallow, ref deep);
            Count(zoneMap.GetZone(x + 1, z + 1), ref land, ref shallow, ref deep);

            if (land >= shallow && land >= deep)
            {
                return TerrainMeshLayer.Land;
            }

            return shallow >= deep ? TerrainMeshLayer.ShallowWater : TerrainMeshLayer.DeepWater;
        }

        private static void Count(TerrainZone zone, ref int land, ref int shallow, ref int deep)
        {
            if (zone == TerrainZone.DeepWater)
            {
                deep++;
            }
            else if (zone == TerrainZone.ShallowWater)
            {
                shallow++;
            }
            else
            {
                land++;
            }
        }

        private static ShorelineVertex BuildShorelineVertex(MeshArrays arrays, Color[] colors, int index)
        {
            Color color = colors != null && index < colors.Length ? colors[index] : Color.white;
            return new ShorelineVertex(arrays.Vertices[index], arrays.Uvs[index], color, index);
        }

        private static void AddFullLandCellTriangles(Heightfield heightfield, int x, int z, List<int> triangles)
        {
            int i0 = heightfield.IndexOf(x, z);
            int i1 = i0 + 1;
            int i2 = i0 + heightfield.Width;
            int i3 = i2 + 1;

            triangles.Add(i0);
            triangles.Add(i2);
            triangles.Add(i1);
            triangles.Add(i1);
            triangles.Add(i2);
            triangles.Add(i3);
        }

        private static List<ShorelineVertex> ClipLandPolygon(ShorelineVertex[] cell, float seaLevel)
        {
            var polygon = new List<ShorelineVertex>(6);
            for (int i = 0; i < cell.Length; i++)
            {
                ShorelineVertex current = cell[i];
                ShorelineVertex next = cell[(i + 1) % cell.Length];
                bool currentInside = SeaLevelClassifier.IsLand(current.Position.y, seaLevel);
                bool nextInside = SeaLevelClassifier.IsLand(next.Position.y, seaLevel);

                if (currentInside && nextInside)
                {
                    polygon.Add(next);
                }
                else if (currentInside && !nextInside)
                {
                    polygon.Add(InterpolateAtSeaLevel(current, next, seaLevel));
                }
                else if (!currentInside && nextInside)
                {
                    polygon.Add(InterpolateAtSeaLevel(current, next, seaLevel));
                    polygon.Add(next);
                }
            }

            return polygon;
        }

        private static ShorelineVertex InterpolateAtSeaLevel(ShorelineVertex a, ShorelineVertex b, float seaLevel)
        {
            float denominator = b.Position.y - a.Position.y;
            float t = Mathf.Abs(denominator) <= 0.00001f
                ? 0f
                : Mathf.Clamp01((seaLevel - a.Position.y) / denominator);
            Vector3 position = Vector3.Lerp(a.Position, b.Position, t);
            position.y = seaLevel;
            return new ShorelineVertex(
                position,
                Vector2.Lerp(a.Uv, b.Uv, t),
                Color.Lerp(a.Color, b.Color, t),
                sourceIndex: -1);
        }

        private static void AddClippedLandPolygon(
            List<ShorelineVertex> polygon,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            if (polygon.Count < 3)
            {
                return;
            }

            var indices = new int[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
            {
                indices[i] = AddVertex(polygon[i], vertices, uvs, colors);
            }

            for (int i = 1; i < indices.Length - 1; i++)
            {
                AddTriangleIfNotDegenerate(
                    indices[0],
                    indices[i],
                    indices[i + 1],
                    vertices,
                    triangles);
            }
        }

        private static void AddShorelineWalls(
            List<ShorelineVertex> polygon,
            float seaLevel,
            float shorelineFloorHeight,
            Color wallColor,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            if (polygon.Count < 2)
            {
                return;
            }

            for (int i = 0; i < polygon.Count; i++)
            {
                ShorelineVertex a = polygon[i];
                ShorelineVertex b = polygon[(i + 1) % polygon.Count];
                if (!IsAtSameHeight(a.Position.y, seaLevel) || !IsAtSameHeight(b.Position.y, seaLevel))
                {
                    continue;
                }

                if (a.SourceIndex >= 0 && b.SourceIndex >= 0)
                {
                    continue;
                }

                if (Mathf.Abs(a.Position.y - shorelineFloorHeight) <= 0.0001f)
                {
                    continue;
                }

                if ((a.Position - b.Position).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                AddWallQuad(a, b, shorelineFloorHeight, wallColor, vertices, uvs, colors, triangles);
            }
        }

        private static void AddWallQuad(
            ShorelineVertex a,
            ShorelineVertex b,
            float shorelineFloorHeight,
            Color wallColor,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles)
        {
            Vector3 bottomA = a.Position;
            Vector3 bottomB = b.Position;
            bottomA.y = shorelineFloorHeight;
            bottomB.y = shorelineFloorHeight;

            int topA = AddVertex(new ShorelineVertex(a.Position, a.Uv, a.Color, -1), vertices, uvs, colors);
            int bottomAIndex = AddVertex(new ShorelineVertex(bottomA, a.Uv, wallColor, -1), vertices, uvs, colors);
            int topB = AddVertex(new ShorelineVertex(b.Position, b.Uv, b.Color, -1), vertices, uvs, colors);
            int bottomBIndex = AddVertex(new ShorelineVertex(bottomB, b.Uv, wallColor, -1), vertices, uvs, colors);

            AddTriangleIfNotDegenerate(topA, bottomAIndex, topB, vertices, triangles);
            AddTriangleIfNotDegenerate(topB, bottomAIndex, bottomBIndex, vertices, triangles);
        }

        private static int AddVertex(
            ShorelineVertex vertex,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors)
        {
            if (vertex.SourceIndex >= 0)
            {
                return vertex.SourceIndex;
            }

            int index = vertices.Count;
            vertices.Add(vertex.Position);
            uvs.Add(vertex.Uv);
            colors?.Add(vertex.Color);
            return index;
        }

        private static int AddProceduralVertex(
            Vector3 position,
            float worldSizeX,
            float worldSizeZ,
            Color color,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors)
        {
            return AddVertex(
                new ShorelineVertex(position, BuildUv(position, worldSizeX, worldSizeZ), color, -1),
                vertices,
                uvs,
                colors);
        }

        private static Vector2 BuildUv(Vector3 position, float worldSizeX, float worldSizeZ)
        {
            float halfWorldSizeX = Mathf.Max(0.001f, worldSizeX) * 0.5f;
            float halfWorldSizeZ = Mathf.Max(0.001f, worldSizeZ) * 0.5f;
            return new Vector2(
                Mathf.InverseLerp(-halfWorldSizeX, halfWorldSizeX, position.x),
                Mathf.InverseLerp(-halfWorldSizeZ, halfWorldSizeZ, position.z));
        }

        private static void AddQuad(
            int a,
            int b,
            int c,
            int d,
            List<Vector3> vertices,
            List<int> triangles,
            bool doubleSided)
        {
            AddTriangleIfNotDegenerate(a, b, c, vertices, triangles);
            AddTriangleIfNotDegenerate(a, c, d, vertices, triangles);

            if (!doubleSided)
            {
                return;
            }

            AddTriangleIfNotDegenerate(a, c, b, vertices, triangles);
            AddTriangleIfNotDegenerate(a, d, c, vertices, triangles);
        }

        private static void AddTriangleIfNotDegenerate(
            int a,
            int b,
            int c,
            List<Vector3> vertices,
            List<int> triangles)
        {
            Vector3 ab = vertices[b] - vertices[a];
            Vector3 ac = vertices[c] - vertices[a];
            if (Vector3.Cross(ab, ac).sqrMagnitude <= 0.0000001f)
            {
                return;
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static bool IsAtSameHeight(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private readonly struct ShorelineVertex
        {
            public ShorelineVertex(Vector3 position, Vector2 uv, Color color, int sourceIndex)
            {
                Position = position;
                Uv = uv;
                Color = color;
                SourceIndex = sourceIndex;
            }

            public Vector3 Position { get; }
            public Vector2 Uv { get; }
            public Color Color { get; }
            public int SourceIndex { get; }
        }
    }
}
