#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Bitbox;
using Bitbox.Toymageddon.Nautical;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using StormBreakers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class StormOceanMigrationTests
    {
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";
        private const string StormOceanPrefabPath = "Assets/Prefabs/StormOcean.prefab";
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel.prefab";
        private const string PlayerBoatModelPath = "Assets/Models/PlayerBoat/PlayerBoat.fbx";

        private const string StormOceanPrefabGuid = "f9f03a601c6147c7a17f8b2148f96331";
        private const string StormOceanWaterSamplerGuid = "7c3dd66ccbb149178d88e6b06897a762";
        private const string LegacyWaterSurfaceScriptGuid = "a34be13f78aeb49f28d3a9212d0c2c5c";
        private const string LegacyWaterMaterialGuid = "4e4691c8c28e34abeb7795bcfa987d6d";
        private const string LegacyBoyancyControllerGuid = "4934308fded51460cb02f5d49958bf0b";

        [Test]
        public void EnabledBuildScenes_DoNotReferenceLegacyWaterSurface()
        {
            EditorBuildSettingsScene[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .ToArray();

            NUnitAssert.IsNotEmpty(enabledScenes, "Expected at least one enabled build scene to validate.");

            foreach (EditorBuildSettingsScene scene in enabledScenes)
            {
                if (!File.Exists(scene.path))
                {
                    continue;
                }

                string sceneText = File.ReadAllText(scene.path);
                AssertNoLegacyWaterReferences(sceneText, scene.path);
            }
        }

        [Test]
        public void Sandbox_ContainsExactlyOneActiveStormOcean()
        {
            string sceneText = File.ReadAllText(SandboxScenePath);
            AssertNoLegacyWaterReferences(sceneText, SandboxScenePath);
            NUnitAssert.AreEqual(
                1,
                CountOccurrences(sceneText, $"m_SourcePrefab: {{fileID: 100100000, guid: {StormOceanPrefabGuid}, type: 3}}"),
                "Expected Sandbox to instantiate the project-local StormOcean prefab exactly once.");

            SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);

                OceanController[] oceanControllers =
                    UnityEngine.Object.FindObjectsByType<OceanController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                NUnitAssert.AreEqual(1, oceanControllers.Length, "Expected exactly one active Storm OceanController in Sandbox.");
                NUnitAssert.IsNotNull(
                    oceanControllers[0].GetComponent<StormOceanWaterSampler>(),
                    "Expected the active Storm ocean to provide the local water query facade.");
            }
            finally
            {
                if (originalSceneSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
                }
            }
        }

        [Test]
        public void StormOceanPrefab_DisablesDeferredVfxAndAudioSystems()
        {
            var stormOcean = LoadRequiredPrefab(StormOceanPrefabPath);
            var oceanController = stormOcean.GetComponent<OceanController>();

            NUnitAssert.IsNotNull(oceanController, "Expected StormOcean to own the active OceanController.");
            NUnitAssert.AreEqual(50f, oceanController.fixedFrameRate, 0.001f);
            NUnitAssert.IsNull(oceanController.terrain, "Terrain should remain unset until shoreline integration is in scope.");
            NUnitAssert.IsNotNull(stormOcean.GetComponent<StormOceanWaterSampler>());

            var oceanVfx = stormOcean.GetComponent<VisualEffect>();
            var audioSource = stormOcean.GetComponent<AudioSource>();
            var breakersAudio = stormOcean.GetComponent<BreakersAudio>();

            NUnitAssert.IsNotNull(oceanVfx);
            NUnitAssert.IsFalse(oceanVfx.enabled, "Ocean VFX should stay disabled for this pass.");
            NUnitAssert.IsNotNull(audioSource);
            NUnitAssert.IsFalse(audioSource.enabled, "Ocean AudioSource should stay disabled for this pass.");
            NUnitAssert.IsFalse(audioSource.playOnAwake, "Ocean AudioSource must not start playback.");
            NUnitAssert.IsNotNull(breakersAudio);
            NUnitAssert.IsFalse(breakersAudio.enabled, "BreakersAudio should stay disabled for this pass.");

            string prefabText = File.ReadAllText(StormOceanPrefabPath);
            NUnitAssert.IsTrue(
                prefabText.Contains($"guid: {StormOceanWaterSamplerGuid}", StringComparison.Ordinal),
                "Expected StormOcean to serialize the water sampler component.");
        }

        [Test]
        public void PlayerVessel_UsesStormWaterInteractionAndExistingHullCollider()
        {
            var playerVessel = LoadRequiredPrefab(PlayerVesselPrefabPath);

            NUnitAssert.IsNull(
                playerVessel.GetComponentInChildren<BoyancyController>(true),
                "PlayerVessel should no longer use the custom buoyancy controller.");

            var waterInteraction = playerVessel.GetComponent<WaterInteraction>();
            NUnitAssert.IsNotNull(waterInteraction, "PlayerVessel root should own Storm WaterInteraction.");

            var serializedWaterInteraction = new SerializedObject(waterInteraction);
            var simulationMeshCollider = serializedWaterInteraction.FindProperty("simulationMeshCollider")
                .objectReferenceValue as MeshCollider;

            NUnitAssert.IsNotNull(simulationMeshCollider, "WaterInteraction should use the existing hull mesh collider.");
            NUnitAssert.AreEqual("Hull", simulationMeshCollider.name);
            NUnitAssert.IsNotNull(simulationMeshCollider.sharedMesh, "Hull mesh collider should have a simulation mesh.");
            NUnitAssert.IsTrue(
                simulationMeshCollider.sharedMesh.isReadable,
                "Storm WaterInteraction reads hull vertices and triangles at runtime, so the hull mesh must keep Read/Write enabled.");

            var playerBoatImporter = AssetImporter.GetAtPath(PlayerBoatModelPath) as ModelImporter;
            NUnitAssert.IsNotNull(playerBoatImporter, "Expected PlayerBoat model importer to exist.");
            NUnitAssert.IsTrue(playerBoatImporter.isReadable, "PlayerBoat.fbx must keep Read/Write enabled for Storm buoyancy.");

            NUnitAssert.IsTrue(serializedWaterInteraction.FindProperty("generateWaterForces").boolValue);
            NUnitAssert.IsTrue(serializedWaterInteraction.FindProperty("useJobs").boolValue);
            NUnitAssert.IsFalse(serializedWaterInteraction.FindProperty("simulateWakeWave").boolValue);
            NUnitAssert.IsFalse(serializedWaterInteraction.FindProperty("generateParticles").boolValue);
            NUnitAssert.IsFalse(serializedWaterInteraction.FindProperty("generateAudio").boolValue);
            NUnitAssert.IsFalse(serializedWaterInteraction.FindProperty("drawForce").boolValue);
            NUnitAssert.AreEqual(0.35f, serializedWaterInteraction.FindProperty("relativeDensity").floatValue, 0.001f);
            NUnitAssert.AreEqual(new Vector3(0.1f, 2f, 0.5f), serializedWaterInteraction.FindProperty("dragCoefficients").vector3Value);
            NUnitAssert.AreEqual(new Vector3(2f, 1f, 1.2f), serializedWaterInteraction.FindProperty("inertiaFactors").vector3Value);
            NUnitAssert.AreEqual(new Vector3(0f, -0.5f, 0f), serializedWaterInteraction.FindProperty("gravityCenterShift").vector3Value);

            string prefabText = File.ReadAllText(PlayerVesselPrefabPath);
            NUnitAssert.IsFalse(
                prefabText.Contains($"guid: {LegacyBoyancyControllerGuid}", StringComparison.Ordinal),
                "PlayerVessel should not serialize the legacy buoyancy controller.");
        }

        [Test]
        public void PlayerVesselStormWaterInteraction_ComputesMassFromHullVolume()
        {
            var playerVesselPrefab = LoadRequiredPrefab(PlayerVesselPrefabPath);
            var playerVessel = PrefabUtility.InstantiatePrefab(playerVesselPrefab) as GameObject;
            NUnitAssert.IsNotNull(playerVessel, "Expected to instantiate PlayerVessel prefab.");

            try
            {
                var rigidbody = playerVessel.GetComponent<Rigidbody>();
                var waterInteraction = playerVessel.GetComponent<WaterInteraction>();
                NUnitAssert.IsNotNull(rigidbody, "PlayerVessel should keep its root Rigidbody.");
                NUnitAssert.IsNotNull(waterInteraction, "PlayerVessel root should own Storm WaterInteraction.");

                float serializedMass = rigidbody.mass;
                var serializedWaterInteraction = new SerializedObject(waterInteraction);
                float relativeDensity = serializedWaterInteraction.FindProperty("relativeDensity").floatValue;
                var simulationMeshCollider = serializedWaterInteraction.FindProperty("simulationMeshCollider")
                    .objectReferenceValue as MeshCollider;
                NUnitAssert.IsNotNull(simulationMeshCollider, "WaterInteraction should serialize its hull mesh collider.");
                NUnitAssert.IsNotNull(simulationMeshCollider.sharedMesh, "Hull mesh collider should have a simulation mesh.");

                var initializePhysic = typeof(WaterInteraction).GetMethod(
                    "InitializePhysic",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                NUnitAssert.IsNotNull(initializePhysic, "Expected Storm WaterInteraction.InitializePhysic to exist.");

                var initialized = (bool)initializePhysic.Invoke(waterInteraction, Array.Empty<object>());
                NUnitAssert.IsTrue(initialized, "Expected Storm WaterInteraction physics initialization to succeed.");

                float expectedMass = ComputeMeshVolume(simulationMeshCollider.sharedMesh, playerVessel.transform.localScale)
                    * 1000f
                    * relativeDensity;
                NUnitAssert.Greater(expectedMass, serializedMass, "Storm density should make the vessel heavier than the old placeholder mass.");
                NUnitAssert.AreEqual(expectedMass, rigidbody.mass, expectedMass * 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        private static GameObject LoadRequiredPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            NUnitAssert.IsNotNull(prefab, $"Expected prefab to exist at {prefabPath}.");
            return prefab;
        }

        private static void AssertNoLegacyWaterReferences(string serializedText, string assetPath)
        {
            NUnitAssert.IsFalse(
                serializedText.Contains($"guid: {LegacyWaterSurfaceScriptGuid}", StringComparison.Ordinal),
                $"{assetPath} still references the deleted WaterSurface component.");
            NUnitAssert.IsFalse(
                serializedText.Contains($"guid: {LegacyWaterMaterialGuid}", StringComparison.Ordinal),
                $"{assetPath} still references the deleted WaterMaterial asset.");
            NUnitAssert.IsFalse(
                serializedText.Contains("GeneratedWaterSurfaceMesh", StringComparison.Ordinal),
                $"{assetPath} still references generated legacy water mesh data.");
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static float ComputeMeshVolume(Mesh mesh, Vector3 localScale)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            double signedVolume = 0d;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 vertex1 = Vector3.Scale(localScale, vertices[triangles[i]]);
                Vector3 vertex2 = Vector3.Scale(localScale, vertices[triangles[i + 1]]);
                Vector3 vertex3 = Vector3.Scale(localScale, vertices[triangles[i + 2]]);

                signedVolume += Vector3.Dot(vertex1, Vector3.Cross(vertex2, vertex3)) / 6d;
            }

            return Mathf.Abs((float)signedVolume);
        }
    }
}
#endif
