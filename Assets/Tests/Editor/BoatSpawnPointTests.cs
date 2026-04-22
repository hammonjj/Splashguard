#if UNITY_EDITOR
using System.Reflection;
using Bitbox;
using BitBox.Library;
using BitBox.Library.Input;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class BoatSpawnPointTests
    {
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel/PlayerVessel.prefab";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";
        private const string HubWorldScenePath = "Assets/Scenes/HubWorld.unity";

        [Test]
        public void SandboxScene_BoatSpawnPointReferencesPlayerVesselPrefabGameObject()
        {
            AssertSceneBoatSpawnPointReferencesPlayerVesselPrefab(SandboxScenePath, "Sandbox");
        }

        [Test]
        public void HubWorldScene_BoatSpawnPointReferencesPlayerVesselPrefabGameObject()
        {
            AssertSceneBoatSpawnPointReferencesPlayerVesselPrefab(HubWorldScenePath, "HubWorld");
        }

        private static void AssertSceneBoatSpawnPointReferencesPlayerVesselPrefab(string scenePath, string sceneLabel)
        {
            GameObject playerVesselPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);
            NUnitAssert.IsNotNull(playerVesselPrefab, "Expected PlayerVessel prefab to exist.");

            SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                BoatSpawnPoint[] spawnPoints =
                    Object.FindObjectsByType<BoatSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                NUnitAssert.AreEqual(1, spawnPoints.Length, $"Expected exactly one active BoatSpawnPoint in {sceneLabel}.");

                SerializedObject serializedSpawnPoint = new SerializedObject(spawnPoints[0]);
                SerializedProperty vesselPrefabProperty = serializedSpawnPoint.FindProperty("_playerVesselPrefab");
                NUnitAssert.IsNotNull(vesselPrefabProperty, "Expected BoatSpawnPoint to serialize _playerVesselPrefab.");
                NUnitAssert.IsInstanceOf<GameObject>(
                    vesselPrefabProperty.objectReferenceValue,
                    "Expected BoatSpawnPoint to reference the PlayerVessel prefab as a GameObject.");
                NUnitAssert.AreEqual(playerVesselPrefab, vesselPrefabProperty.objectReferenceValue);
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
        public void SpawnBoatInstance_PlacesPlayerVesselAtMarkerPoseAndDropsAnchor()
        {
            GameObject playerVesselPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);
            NUnitAssert.IsNotNull(playerVesselPrefab, "Expected PlayerVessel prefab to exist.");

            SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            PlayerCoordinator originalCoordinator = StaticData.PlayerInputCoordinator;
            GameObject spawnedBoat = null;
            try
            {
                Scene markerScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Scene playersScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                GameObject playerCoordinatorRoot = new("PlayerCoordinator");
                SceneManager.MoveGameObjectToScene(playerCoordinatorRoot, playersScene);
                PlayerCoordinator playerCoordinator = playerCoordinatorRoot.AddComponent<PlayerCoordinator>();
                StaticData.PlayerInputCoordinator = playerCoordinator;

                GameObject markerRoot = new("BoatSpawnPointMarker");
                SceneManager.MoveGameObjectToScene(markerRoot, markerScene);
                markerRoot.transform.SetPositionAndRotation(new Vector3(4f, 1.5f, -2f), Quaternion.Euler(0f, 33f, 0f));

                BoatSpawnPoint spawnPoint = markerRoot.AddComponent<BoatSpawnPoint>();
                SetPrivateField(spawnPoint, "_playerVesselPrefab", playerVesselPrefab);

                spawnedBoat = InvokePrivate<GameObject>(spawnPoint, "SpawnBoatInstance");

                NUnitAssert.IsNotNull(spawnedBoat, "BoatSpawnPoint should instantiate a player vessel.");
                NUnitAssert.IsNotNull(spawnedBoat.GetComponent<PlayerVesselRoot>(), "Spawned boat should include PlayerVesselRoot.");
                NUnitAssert.AreEqual(playersScene.handle, spawnedBoat.scene.handle, "Spawned boat should be moved into the Players scene.");
                NUnitAssert.AreEqual(markerRoot.transform.position, spawnedBoat.transform.position);
                NUnitAssert.AreEqual(markerRoot.transform.rotation.eulerAngles.y, spawnedBoat.transform.rotation.eulerAngles.y, 0.001f);

                Rigidbody boatRigidbody = spawnedBoat.GetComponent<Rigidbody>();
                NUnitAssert.IsNotNull(boatRigidbody, "Spawned boat should retain its root Rigidbody.");
                NUnitAssert.AreEqual(Vector3.zero, boatRigidbody.linearVelocity);
                NUnitAssert.AreEqual(Vector3.zero, boatRigidbody.angularVelocity);

                AnchorControls anchorControls = spawnedBoat.GetComponentInChildren<AnchorControls>(includeInactive: true);
                NUnitAssert.IsNotNull(anchorControls, "Spawned boat should include AnchorControls.");
                NUnitAssert.AreEqual(
                    AnchorState.Lowering,
                    anchorControls.CurrentState,
                    "BoatSpawnPoint should start lowering the anchor immediately after spawn.");
            }
            finally
            {
                StaticData.PlayerInputCoordinator = originalCoordinator;

                if (spawnedBoat != null)
                {
                    Object.DestroyImmediate(spawnedBoat);
                }

                if (originalSceneSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
                }
            }
        }

        [Test]
        public void ShouldSkipSpawn_ReturnsTrueWhenPlayerVesselAlreadyExists()
        {
            GameObject playerVesselPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);
            NUnitAssert.IsNotNull(playerVesselPrefab, "Expected PlayerVessel prefab to exist.");

            GameObject existingBoat = PrefabUtility.InstantiatePrefab(playerVesselPrefab) as GameObject;
            NUnitAssert.IsNotNull(existingBoat, "Expected to instantiate a PlayerVessel prefab instance.");

            GameObject markerRoot = new("BoatSpawnPointMarker");
            BoatSpawnPoint spawnPoint = markerRoot.AddComponent<BoatSpawnPoint>();
            SetPrivateField(spawnPoint, "_playerVesselPrefab", playerVesselPrefab);

            try
            {
                bool shouldSkip = InvokePrivate<bool>(spawnPoint, "ShouldSkipSpawn");
                NUnitAssert.IsTrue(shouldSkip, "BoatSpawnPoint should skip spawning when a player vessel already exists.");
            }
            finally
            {
                Object.DestroyImmediate(existingBoat);
                Object.DestroyImmediate(markerRoot);
            }
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] arguments)
        {
            return (T)InvokePrivate(target, methodName, arguments);
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected private method '{methodName}' on {target.GetType().Name}.");
            return method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
#endif
