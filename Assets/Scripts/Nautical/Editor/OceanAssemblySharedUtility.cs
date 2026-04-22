#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using StormBreakers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Bitbox.Toymageddon.Nautical.Editor
{
    internal static class OceanAssemblySharedUtility
    {
        internal const string DefaultOutputAssetFolder = "Assets/Data/Terrain/OceanAssemblies";
        internal const int WaterLayer = 4;
        internal const float BlendBandMinimalTransparency = 0.68f;

        private const string WaterColorPropertyName = "_waterColor";
        private const string MinimalTransparencyPropertyName = "_minimalTransparency";
        private const float BlendBandSurfaceOffset = 0.015f;

        internal static OceanAssemblyWaterOutputResult CreateWaterOutput(
            Transform root,
            GameObject sourceContext,
            GameObject stormOceanPrefab,
            string assetRoot,
            string outputName,
            float waterSurfaceHeight,
            int blendBandCount,
            Color shallowWaterColor,
            Color deepWaterColor,
            OceanAssemblyMeshSource? shallowSource,
            OceanAssemblyMeshSource? deepSource)
        {
            if (root == null || stormOceanPrefab == null || (!shallowSource.HasValue && !deepSource.HasValue))
            {
                return default;
            }

            MeshRenderer stormOceanTemplateRenderer = ResolveOceanTemplateRenderer(stormOceanPrefab);
            if (stormOceanTemplateRenderer == null || stormOceanTemplateRenderer.sharedMaterial == null)
            {
                throw new InvalidOperationException("The StormOcean prefab must have a MeshRenderer with an ocean material.");
            }

            Material baseOceanMaterial = stormOceanTemplateRenderer.sharedMaterial;
            Material oceanTemplateMaterial = CreateOrReplaceMaterialAsset(
                baseOceanMaterial,
                $"{assetRoot}/{outputName}_OceanTemplate.mat",
                $"{outputName}_OceanTemplate");
            SetWaterColor(oceanTemplateMaterial, deepColor: deepWaterColor);

            Mesh shallowMesh = null;
            Mesh deepMesh = null;
            Material shallowMaterial = null;
            Material deepMaterial = null;
            GameObject shallowObject = null;
            GameObject deepObject = null;

            if (shallowSource.HasValue)
            {
                OceanAssemblyMeshSource source = shallowSource.Value;
                shallowMesh = CreateOrReplaceFlattenedMeshAsset(
                    source.Mesh,
                    $"{assetRoot}/{outputName}_ShallowOceanMesh.asset",
                    $"{outputName}_ShallowOceanMesh",
                    waterSurfaceHeight,
                    source.LocalPosition.y);
                shallowMaterial = CreateOrReplaceMaterialAsset(
                    baseOceanMaterial,
                    $"{assetRoot}/{outputName}_ShallowOcean.mat",
                    $"{outputName}_ShallowOcean");
                SetWaterColor(shallowMaterial, shallowWaterColor);
                shallowObject = CreateCopiedMeshChild(
                    root,
                    OceanTerrainAssemblyUtility.ShallowOceanChildName,
                    source,
                    shallowMesh,
                    new[] { shallowMaterial },
                    includeCollider: false,
                    forceWaterLayer: true);
            }

            if (deepSource.HasValue)
            {
                OceanAssemblyMeshSource source = deepSource.Value;
                deepMesh = CreateOrReplaceFlattenedMeshAsset(
                    source.Mesh,
                    $"{assetRoot}/{outputName}_DeepOceanMesh.asset",
                    $"{outputName}_DeepOceanMesh",
                    waterSurfaceHeight,
                    source.LocalPosition.y);
                deepMaterial = CreateOrReplaceMaterialAsset(
                    baseOceanMaterial,
                    $"{assetRoot}/{outputName}_DeepOcean.mat",
                    $"{outputName}_DeepOcean");
                SetWaterColor(deepMaterial, deepWaterColor);
                deepObject = CreateCopiedMeshChild(
                    root,
                    OceanTerrainAssemblyUtility.DeepOceanChildName,
                    source,
                    deepMesh,
                    new[] { deepMaterial },
                    includeCollider: false,
                    forceWaterLayer: true);
            }

            OceanBlendBandAsset[] blendBands = Array.Empty<OceanBlendBandAsset>();
            GameObject gradientObject = null;
            MeshRenderer[] blendRenderers = Array.Empty<MeshRenderer>();
            if (shallowSource.HasValue && deepSource.HasValue)
            {
                blendBands = CreateOrReplaceBlendBands(
                    shallowSource.Value,
                    deepSource.Value,
                    baseOceanMaterial,
                    assetRoot,
                    outputName,
                    waterSurfaceHeight,
                    blendBandCount,
                    shallowWaterColor,
                    deepWaterColor);
                if (blendBandCount > 0 && blendBands.Length == 0)
                {
                    Debug.LogWarning(
                        "Ocean assembly could not build shallow/deep gradient bands. The shallow and deep meshes must share the same vertex topology with adjacent triangles along the boundary.",
                        sourceContext);
                }

                if (blendBands.Length > 0)
                {
                    gradientObject = CreateGradientChild(root, shallowSource.Value);
                    blendRenderers = CreateBlendBandChildren(gradientObject.transform, shallowSource.Value.Renderer, blendBands);
                }
            }

            GameObject oceanSystem = CreateOceanSystem(root, stormOceanPrefab, oceanTemplateMaterial, waterSurfaceHeight);
            var sync = root.gameObject.GetComponent<StormOceanLayerMaterialSync>();
            if (sync == null)
            {
                sync = root.gameObject.AddComponent<StormOceanLayerMaterialSync>();
            }

            sync.Configure(
                ResolveOceanTemplateRenderer(oceanSystem),
                shallowObject != null ? shallowObject.GetComponent<MeshRenderer>() : null,
                deepObject != null ? deepObject.GetComponent<MeshRenderer>() : null,
                shallowWaterColor,
                deepWaterColor,
                blendRenderers,
                ExtractBlendColors(blendBands),
                BlendBandMinimalTransparency);

            return new OceanAssemblyWaterOutputResult(
                oceanSystem,
                shallowObject,
                deepObject,
                gradientObject,
                shallowMesh,
                deepMesh,
                ExtractBlendMeshes(blendBands),
                oceanTemplateMaterial,
                shallowMaterial,
                deepMaterial,
                ExtractBlendMaterials(blendBands),
                blendRenderers);
        }

        internal static GameObject CreateCopiedMeshChild(
            Transform parent,
            string childName,
            OceanAssemblyMeshSource source,
            Mesh mesh,
            Material[] materials,
            bool includeCollider,
            bool forceWaterLayer)
        {
            return CreateMeshChild(
                parent,
                childName,
                source.LocalPosition,
                source.LocalRotation,
                source.LocalScale,
                forceWaterLayer ? WaterLayer : source.GameObject.layer,
                source.Renderer,
                mesh,
                materials,
                includeCollider);
        }

        internal static GameObject CreateMeshChild(
            Transform parent,
            string childName,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            int layer,
            MeshRenderer sourceRenderer,
            Mesh mesh,
            Material[] materials,
            bool includeCollider)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, worldPositionStays: false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;
            child.layer = layer;

            var meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = child.AddComponent<MeshRenderer>();
            if (sourceRenderer != null)
            {
                CopyRendererSettings(sourceRenderer, meshRenderer);
            }

            meshRenderer.sharedMaterials = materials ?? Array.Empty<Material>();

            if (includeCollider)
            {
                var meshCollider = child.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }

            return child;
        }

        internal static MeshRenderer ResolveOceanTemplateRenderer(GameObject stormOceanPrefab)
        {
            if (stormOceanPrefab == null)
            {
                return null;
            }

            MeshRenderer rootRenderer = stormOceanPrefab.GetComponent<MeshRenderer>();
            if (rootRenderer != null && rootRenderer.sharedMaterial != null)
            {
                return rootRenderer;
            }

            MeshRenderer[] renderers = stormOceanPrefab.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial != null)
                {
                    return renderers[i];
                }
            }

            return null;
        }

        internal static Mesh CreateOrReplaceMeshAsset(Mesh sourceMesh, string assetPath, string meshName)
        {
            DeleteExistingAsset(assetPath);
            Mesh mesh = UnityEngine.Object.Instantiate(sourceMesh);
            mesh.name = meshName;
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        internal static Mesh CreateOrReplaceFlattenedMeshAsset(
            Mesh sourceMesh,
            string assetPath,
            string meshName,
            float flattenedRootY,
            float childLocalY)
        {
            DeleteExistingAsset(assetPath);
            Mesh mesh = UnityEngine.Object.Instantiate(sourceMesh);
            mesh.name = meshName;
            float flattenedMeshY = flattenedRootY - childLocalY;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].y = flattenedMeshY;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        internal static Material CreateOrReplaceMaterialAsset(Material sourceMaterial, string assetPath, string materialName)
        {
            DeleteExistingAsset(assetPath);
            var material = new Material(sourceMaterial)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        internal static string EnsureOutputAssetFolder(string baseFolder, string outputName)
        {
            string normalizedBaseFolder = ResolveOutputAssetFolder(baseFolder);
            EnsureProjectFolder(normalizedBaseFolder);
            string outputFolder = $"{normalizedBaseFolder}/{SanitizeName(outputName, "OceanAssembly")}";
            EnsureProjectFolder(outputFolder);
            return outputFolder;
        }

        internal static string ResolveOutputAssetFolder(string folder)
        {
            return string.IsNullOrWhiteSpace(folder)
                ? DefaultOutputAssetFolder
                : folder.Replace("\\", "/").TrimEnd('/');
        }

        internal static bool IsProjectAssetFolderPath(string folder)
        {
            return !string.IsNullOrWhiteSpace(folder)
                && (folder.Equals("Assets", StringComparison.Ordinal)
                    || folder.StartsWith("Assets/", StringComparison.Ordinal));
        }

        internal static GameObject FindSceneRoot(Scene scene, string rootName)
        {
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(rootName))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        internal static string SanitizeName(string value, string defaultName)
        {
            string trimmed = string.IsNullOrWhiteSpace(value) ? defaultName : value.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
            {
                trimmed = trimmed.Replace(invalidChars[i], '_');
            }

            return trimmed.Replace(' ', '_');
        }

        internal static void CopyRendererSettings(MeshRenderer source, MeshRenderer destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        }

        private static void DeleteExistingAsset(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void EnsureProjectFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                throw new InvalidOperationException($"Invalid asset folder path '{folder}'.");
            }

            EnsureProjectFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void SetWaterColor(Material material, Color deepColor)
        {
            if (material != null && material.HasProperty(WaterColorPropertyName))
            {
                material.SetColor(WaterColorPropertyName, deepColor);
            }
        }

        private static void SetMinimalTransparency(Material material, float minimalTransparency)
        {
            if (material != null && material.HasProperty(MinimalTransparencyPropertyName))
            {
                material.SetFloat(MinimalTransparencyPropertyName, minimalTransparency);
            }
        }

        private static OceanBlendBandAsset[] CreateOrReplaceBlendBands(
            OceanAssemblyMeshSource shallowSource,
            OceanAssemblyMeshSource deepSource,
            Material baseOceanMaterial,
            string assetRoot,
            string outputName,
            float waterSurfaceHeight,
            int blendBandCount,
            Color shallowWaterColor,
            Color deepWaterColor)
        {
            int clampedBandCount = Mathf.Clamp(blendBandCount, 0, OceanTerrainAssemblyUtility.MaxBlendBandCount);
            if (clampedBandCount == 0
                || shallowSource.Mesh.vertexCount == 0
                || shallowSource.Mesh.vertexCount != deepSource.Mesh.vertexCount)
            {
                return Array.Empty<OceanBlendBandAsset>();
            }

            WaterTriangle[] waterTriangles = BuildWaterTriangles(
                shallowSource.Mesh.triangles,
                deepSource.Mesh.triangles,
                shallowSource.Mesh.vertexCount);
            if (waterTriangles.Length == 0)
            {
                return Array.Empty<OceanBlendBandAsset>();
            }

            Dictionary<EdgeKey, List<int>> edgeMap = BuildEdgeMap(waterTriangles);
            int[] ringDistances = BuildBoundaryRingDistances(waterTriangles, edgeMap, clampedBandCount);
            var blendBands = new List<OceanBlendBandAsset>(clampedBandCount * 2);
            for (int ring = 0; ring < clampedBandCount; ring++)
            {
                TryCreateBlendBand(
                    OceanBlendSide.Shallow,
                    ring,
                    clampedBandCount,
                    shallowSource.Mesh,
                    shallowSource.Transform,
                    shallowSource.Transform,
                    waterTriangles,
                    ringDistances,
                    baseOceanMaterial,
                    assetRoot,
                    outputName,
                    waterSurfaceHeight,
                    shallowWaterColor,
                    deepWaterColor,
                    blendBands);

                TryCreateBlendBand(
                    OceanBlendSide.Deep,
                    ring,
                    clampedBandCount,
                    deepSource.Mesh,
                    deepSource.Transform,
                    shallowSource.Transform,
                    waterTriangles,
                    ringDistances,
                    baseOceanMaterial,
                    assetRoot,
                    outputName,
                    waterSurfaceHeight,
                    shallowWaterColor,
                    deepWaterColor,
                    blendBands);
            }

            return blendBands.ToArray();
        }

        private static void TryCreateBlendBand(
            OceanBlendSide side,
            int ring,
            int blendBandCount,
            Mesh sourceMesh,
            Transform sourceTransform,
            Transform blendTransform,
            WaterTriangle[] waterTriangles,
            int[] ringDistances,
            Material baseOceanMaterial,
            string assetRoot,
            string outputName,
            float waterSurfaceHeight,
            Color shallowWaterColor,
            Color deepWaterColor,
            List<OceanBlendBandAsset> blendBands)
        {
            string sideName = side == OceanBlendSide.Shallow ? "Shallow" : "Deep";
            string childName = $"{sideName} Gradient {ring + 1:00}";
            string assetName = $"{outputName}_{sideName}OceanGradient{ring + 1:00}";
            Mesh blendMesh = CreateBlendBandMesh(
                sourceMesh,
                sourceTransform,
                blendTransform,
                waterTriangles,
                ringDistances,
                side,
                ring,
                waterSurfaceHeight + BlendBandSurfaceOffset + ring * 0.001f,
                assetName + "Mesh");

            if (blendMesh == null)
            {
                return;
            }

            string meshAssetPath = $"{assetRoot}/{assetName}Mesh.asset";
            DeleteExistingAsset(meshAssetPath);
            AssetDatabase.CreateAsset(blendMesh, meshAssetPath);

            Material blendMaterial = CreateOrReplaceMaterialAsset(baseOceanMaterial, $"{assetRoot}/{assetName}.mat", assetName);
            Color waterColor = CalculateBlendBandColor(side, ring, blendBandCount, shallowWaterColor, deepWaterColor);
            SetWaterColor(blendMaterial, waterColor);
            SetMinimalTransparency(blendMaterial, BlendBandMinimalTransparency);
            blendBands.Add(new OceanBlendBandAsset(childName, blendMesh, blendMaterial, waterColor));
        }

        private static Mesh CreateBlendBandMesh(
            Mesh sourceMesh,
            Transform sourceTransform,
            Transform blendTransform,
            WaterTriangle[] waterTriangles,
            int[] ringDistances,
            OceanBlendSide side,
            int ring,
            float waterSurfaceHeight,
            string meshName)
        {
            var triangles = new List<int>();
            for (int i = 0; i < waterTriangles.Length; i++)
            {
                if (ringDistances[i] == ring && waterTriangles[i].Side == side)
                {
                    triangles.Add(waterTriangles[i].A);
                    triangles.Add(waterTriangles[i].B);
                    triangles.Add(waterTriangles[i].C);
                }
            }

            if (triangles.Count == 0)
            {
                return null;
            }

            Vector3[] sourceVertices = sourceMesh.vertices;
            var vertices = new Vector3[sourceVertices.Length];
            float localWaterHeight = waterSurfaceHeight - blendTransform.localPosition.y;
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 localVertex = sourceVertices[i];
                if (sourceTransform != blendTransform)
                {
                    localVertex = blendTransform.InverseTransformPoint(sourceTransform.TransformPoint(localVertex));
                }

                localVertex.y = localWaterHeight;
                vertices[i] = localVertex;
            }

            var mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                triangles = triangles.ToArray()
            };

            Vector2[] uvs = sourceMesh.uv;
            if (uvs != null && uvs.Length == vertices.Length)
            {
                mesh.uv = uvs;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static WaterTriangle[] BuildWaterTriangles(int[] shallowTriangles, int[] deepTriangles, int vertexCount)
        {
            var triangles = new List<WaterTriangle>((shallowTriangles.Length + deepTriangles.Length) / 3);
            AppendWaterTriangles(shallowTriangles, vertexCount, OceanBlendSide.Shallow, triangles);
            AppendWaterTriangles(deepTriangles, vertexCount, OceanBlendSide.Deep, triangles);
            return triangles.ToArray();
        }

        private static void AppendWaterTriangles(
            int[] sourceTriangles,
            int vertexCount,
            OceanBlendSide side,
            List<WaterTriangle> triangles)
        {
            for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
            {
                int a = sourceTriangles[i];
                int b = sourceTriangles[i + 1];
                int c = sourceTriangles[i + 2];
                if (a >= 0 && a < vertexCount && b >= 0 && b < vertexCount && c >= 0 && c < vertexCount)
                {
                    triangles.Add(new WaterTriangle(a, b, c, side));
                }
            }
        }

        private static Dictionary<EdgeKey, List<int>> BuildEdgeMap(WaterTriangle[] waterTriangles)
        {
            var edgeMap = new Dictionary<EdgeKey, List<int>>(waterTriangles.Length * 3);
            for (int i = 0; i < waterTriangles.Length; i++)
            {
                AddTriangleEdge(edgeMap, new EdgeKey(waterTriangles[i].A, waterTriangles[i].B), i);
                AddTriangleEdge(edgeMap, new EdgeKey(waterTriangles[i].B, waterTriangles[i].C), i);
                AddTriangleEdge(edgeMap, new EdgeKey(waterTriangles[i].C, waterTriangles[i].A), i);
            }

            return edgeMap;
        }

        private static void AddTriangleEdge(Dictionary<EdgeKey, List<int>> edgeMap, EdgeKey edgeKey, int triangleIndex)
        {
            if (!edgeMap.TryGetValue(edgeKey, out List<int> triangleIndices))
            {
                triangleIndices = new List<int>(2);
                edgeMap.Add(edgeKey, triangleIndices);
            }

            triangleIndices.Add(triangleIndex);
        }

        private static int[] BuildBoundaryRingDistances(
            WaterTriangle[] waterTriangles,
            Dictionary<EdgeKey, List<int>> edgeMap,
            int blendBandCount)
        {
            var ringDistances = new int[waterTriangles.Length];
            for (int i = 0; i < ringDistances.Length; i++)
            {
                ringDistances[i] = -1;
            }
            var queue = new Queue<int>();

            foreach (KeyValuePair<EdgeKey, List<int>> edge in edgeMap)
            {
                bool hasShallow = false;
                bool hasDeep = false;
                List<int> triangleIndices = edge.Value;
                for (int i = 0; i < triangleIndices.Count; i++)
                {
                    if (waterTriangles[triangleIndices[i]].Side == OceanBlendSide.Shallow)
                    {
                        hasShallow = true;
                    }
                    else
                    {
                        hasDeep = true;
                    }
                }

                if (!hasShallow || !hasDeep)
                {
                    continue;
                }

                for (int i = 0; i < triangleIndices.Count; i++)
                {
                    EnqueueRingTriangle(triangleIndices[i], 0, ringDistances, queue);
                }
            }

            while (queue.Count > 0)
            {
                int triangleIndex = queue.Dequeue();
                int distance = ringDistances[triangleIndex];
                if (distance >= blendBandCount - 1)
                {
                    continue;
                }

                EnqueueNeighborTriangles(waterTriangles[triangleIndex].A, waterTriangles[triangleIndex].B);
                EnqueueNeighborTriangles(waterTriangles[triangleIndex].B, waterTriangles[triangleIndex].C);
                EnqueueNeighborTriangles(waterTriangles[triangleIndex].C, waterTriangles[triangleIndex].A);

                void EnqueueNeighborTriangles(int a, int b)
                {
                    if (!edgeMap.TryGetValue(new EdgeKey(a, b), out List<int> neighbors))
                    {
                        return;
                    }

                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        EnqueueRingTriangle(neighbors[i], distance + 1, ringDistances, queue);
                    }
                }
            }

            return ringDistances;
        }

        private static void EnqueueRingTriangle(int triangleIndex, int distance, int[] ringDistances, Queue<int> queue)
        {
            if (ringDistances[triangleIndex] != -1)
            {
                return;
            }

            ringDistances[triangleIndex] = distance;
            queue.Enqueue(triangleIndex);
        }

        private static Color CalculateBlendBandColor(
            OceanBlendSide side,
            int ring,
            int blendBandCount,
            Color shallowWaterColor,
            Color deepWaterColor)
        {
            float blendAmount = 0.55f * (1f - ring / Mathf.Max(1f, blendBandCount));
            return side == OceanBlendSide.Shallow
                ? Color.Lerp(shallowWaterColor, deepWaterColor, blendAmount)
                : Color.Lerp(deepWaterColor, shallowWaterColor, blendAmount);
        }

        private static GameObject CreateGradientChild(Transform parent, OceanAssemblyMeshSource blendSource)
        {
            var child = new GameObject(OceanTerrainAssemblyUtility.OceanGradientChildName);
            child.transform.SetParent(parent, worldPositionStays: false);
            child.transform.localPosition = blendSource.LocalPosition;
            child.transform.localRotation = blendSource.LocalRotation;
            child.transform.localScale = blendSource.LocalScale;
            child.layer = WaterLayer;
            return child;
        }

        private static MeshRenderer[] CreateBlendBandChildren(
            Transform parent,
            MeshRenderer sourceRenderer,
            OceanBlendBandAsset[] blendBands)
        {
            var renderers = new MeshRenderer[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                var child = new GameObject(blendBands[i].ChildName);
                child.transform.SetParent(parent, worldPositionStays: false);
                child.layer = WaterLayer;

                var meshFilter = child.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = blendBands[i].Mesh;

                var meshRenderer = child.AddComponent<MeshRenderer>();
                CopyRendererSettings(sourceRenderer, meshRenderer);
                meshRenderer.sharedMaterial = blendBands[i].Material;
                meshRenderer.sortingOrder = 10 + i;
                renderers[i] = meshRenderer;
            }

            return renderers;
        }

        private static GameObject CreateOceanSystem(
            Transform parent,
            GameObject stormOceanPrefab,
            Material oceanTemplateMaterial,
            float waterSurfaceHeight)
        {
            var oceanSystem = PrefabUtility.InstantiatePrefab(stormOceanPrefab, parent) as GameObject;
            if (oceanSystem == null)
            {
                throw new InvalidOperationException("Could not instantiate the StormOcean prefab.");
            }

            oceanSystem.name = OceanTerrainAssemblyUtility.OceanSystemChildName;
            oceanSystem.transform.localPosition = Vector3.zero;
            oceanSystem.transform.localRotation = Quaternion.identity;
            oceanSystem.transform.localScale = Vector3.one;
            SetLayerRecursively(oceanSystem, WaterLayer);

            MeshRenderer[] renderers = oceanSystem.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = oceanTemplateMaterial;
                renderers[i].enabled = false;
            }

            OceanController oceanController = oceanSystem.GetComponent<OceanController>();
            if (oceanController != null)
            {
                oceanController.waterColor = oceanTemplateMaterial.HasProperty(WaterColorPropertyName)
                    ? oceanTemplateMaterial.GetColor(WaterColorPropertyName)
                    : oceanController.waterColor;
                oceanController.waterLevel = parent.TransformPoint(new Vector3(0f, waterSurfaceHeight, 0f)).y;
                oceanController.UpdateWaves();
                oceanController.UpdateWind();
                oceanController.UpdateLighting();
            }

            return oceanSystem;
        }

        private static Mesh[] ExtractBlendMeshes(OceanBlendBandAsset[] blendBands)
        {
            var meshes = new Mesh[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                meshes[i] = blendBands[i].Mesh;
            }

            return meshes;
        }

        private static Material[] ExtractBlendMaterials(OceanBlendBandAsset[] blendBands)
        {
            var materials = new Material[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                materials[i] = blendBands[i].Material;
            }

            return materials;
        }

        private static Color[] ExtractBlendColors(OceanBlendBandAsset[] blendBands)
        {
            var colors = new Color[blendBands.Length];
            for (int i = 0; i < blendBands.Length; i++)
            {
                colors[i] = blendBands[i].WaterColor;
            }

            return colors;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }

        private enum OceanBlendSide
        {
            Shallow,
            Deep
        }

        private readonly struct OceanBlendBandAsset
        {
            public OceanBlendBandAsset(string childName, Mesh mesh, Material material, Color waterColor)
            {
                ChildName = childName;
                Mesh = mesh;
                Material = material;
                WaterColor = waterColor;
            }

            public string ChildName { get; }
            public Mesh Mesh { get; }
            public Material Material { get; }
            public Color WaterColor { get; }
        }

        private readonly struct WaterTriangle
        {
            public WaterTriangle(int a, int b, int c, OceanBlendSide side)
            {
                A = a;
                B = b;
                C = c;
                Side = side;
            }

            public int A { get; }
            public int B { get; }
            public int C { get; }
            public OceanBlendSide Side { get; }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            public int A { get; }
            public int B { get; }

            public bool Equals(EdgeKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }
    }

    internal readonly struct OceanAssemblyMeshSource
    {
        public OceanAssemblyMeshSource(
            GameObject gameObject,
            Transform transform,
            MeshRenderer renderer,
            Mesh mesh,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            string assetKey)
        {
            GameObject = gameObject;
            Transform = transform;
            Renderer = renderer;
            Mesh = mesh;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            AssetKey = assetKey;
        }

        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public MeshRenderer Renderer { get; }
        public Mesh Mesh { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
        public string AssetKey { get; }
    }

    internal readonly struct OceanAssemblyWaterOutputResult
    {
        public OceanAssemblyWaterOutputResult(
            GameObject oceanSystem,
            GameObject shallowObject,
            GameObject deepObject,
            GameObject gradientObject,
            Mesh shallowMesh,
            Mesh deepMesh,
            Mesh[] blendMeshes,
            Material oceanTemplateMaterial,
            Material shallowMaterial,
            Material deepMaterial,
            Material[] blendMaterials,
            MeshRenderer[] blendRenderers)
        {
            OceanSystem = oceanSystem;
            ShallowObject = shallowObject;
            DeepObject = deepObject;
            GradientObject = gradientObject;
            ShallowMesh = shallowMesh;
            DeepMesh = deepMesh;
            BlendMeshes = blendMeshes ?? Array.Empty<Mesh>();
            OceanTemplateMaterial = oceanTemplateMaterial;
            ShallowMaterial = shallowMaterial;
            DeepMaterial = deepMaterial;
            BlendMaterials = blendMaterials ?? Array.Empty<Material>();
            BlendRenderers = blendRenderers ?? Array.Empty<MeshRenderer>();
        }

        public GameObject OceanSystem { get; }
        public GameObject ShallowObject { get; }
        public GameObject DeepObject { get; }
        public GameObject GradientObject { get; }
        public Mesh ShallowMesh { get; }
        public Mesh DeepMesh { get; }
        public Mesh[] BlendMeshes { get; }
        public Material OceanTemplateMaterial { get; }
        public Material ShallowMaterial { get; }
        public Material DeepMaterial { get; }
        public Material[] BlendMaterials { get; }
        public MeshRenderer[] BlendRenderers { get; }
    }
}
#endif
