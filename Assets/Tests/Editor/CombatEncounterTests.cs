#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BitBox.Library;
using BitBox.Library.Constants.Enums;
using BitBox.Toymageddon.SceneManagement;
using Bitbox;
using Bitbox.Splashguard.Encounters;
using Bitbox.Splashguard.Enemies;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class CombatEncounterTests
    {
        private const string CombatArenaScenePath = "Assets/Scenes/CombatArena_1.unity";
        private const string EncounterDefinitionAssetPath = "Assets/Data/Encounters/CombatArenaEncounterDefinition.asset";
        private const string EnemyVesselPrefabPath = "Assets/Prefabs/Enemies/EnemyVessel.prefab";

        [Test]
        public void EncounterDefinition_RejectsEmptyAndNullEnemyArrays()
        {
            EncounterDefinition definition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                NUnitAssert.IsFalse(definition.IsValidDefinition(out string emptyValidationError));
                StringAssert.Contains("Assign at least one enemy prefab", emptyValidationError);

                SerializedObject serializedDefinition = new(definition);
                SerializedProperty prefabsProperty = serializedDefinition.FindProperty("_enemyPrefabs");
                prefabsProperty.arraySize = 1;
                prefabsProperty.GetArrayElementAtIndex(0).objectReferenceValue = null;
                serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

                NUnitAssert.IsFalse(definition.IsValidDefinition(out string nullValidationError));
                StringAssert.Contains("is null", nullValidationError);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CombatArenaEncounterDefinition_ReferencesEnemyPrefabAndValidates()
        {
            EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(EncounterDefinitionAssetPath);
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyVesselPrefabPath);

            NUnitAssert.IsNotNull(definition);
            NUnitAssert.IsNotNull(enemyPrefab);
            NUnitAssert.IsTrue(definition.IsValidDefinition(out string validationError), validationError);
            NUnitAssert.AreEqual(1, definition.EnemyPrefabs.Length);
            NUnitAssert.AreEqual(enemyPrefab, definition.EnemyPrefabs[0]);
        }

        [Test]
        public void CombatArenaScene_ContainsConfiguredEncounterManagerAndGatedExitTriggers()
        {
            SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(CombatArenaScenePath, OpenSceneMode.Single);

                EncounterManager[] managers =
                    Object.FindObjectsByType<EncounterManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                NUnitAssert.AreEqual(1, managers.Length, "Expected exactly one active EncounterManager in CombatArena_1.");

                SerializedObject serializedManager = new(managers[0]);
                SerializedProperty definitionProperty = serializedManager.FindProperty("_encounterDefinition");
                SerializedProperty spawnZonesProperty = serializedManager.FindProperty("_spawnZones");
                NUnitAssert.IsNotNull(definitionProperty);
                NUnitAssert.IsNotNull(spawnZonesProperty);
                NUnitAssert.IsNotNull(definitionProperty.objectReferenceValue);
                NUnitAssert.IsInstanceOf<EncounterDefinition>(definitionProperty.objectReferenceValue);
                NUnitAssert.IsTrue(
                    ((EncounterDefinition)definitionProperty.objectReferenceValue).IsValidDefinition(out string validationError),
                    validationError);
                NUnitAssert.AreEqual(4, spawnZonesProperty.arraySize, "Expected CombatArena_1 to assign four encounter spawn zones.");
                for (int zoneIndex = 0; zoneIndex < spawnZonesProperty.arraySize; zoneIndex++)
                {
                    NUnitAssert.IsNotNull(
                        spawnZonesProperty.GetArrayElementAtIndex(zoneIndex).objectReferenceValue,
                        $"Expected encounter spawn zone {zoneIndex} to be assigned.");
                }

                CombatArenaExitTrigger[] exits =
                    Object.FindObjectsByType<CombatArenaExitTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                NUnitAssert.AreEqual(2, exits.Length, "Expected CombatArena_1 to contain replay and hub exits.");

                bool foundReloadExit = false;
                bool foundHubExit = false;
                for (int i = 0; i < exits.Length; i++)
                {
                    SerializedObject serializedExit = new(exits[i]);
                    NUnitAssert.IsTrue(serializedExit.FindProperty("_requireEncounterComplete").boolValue);

                    CombatArenaExitActionType actionType =
                        (CombatArenaExitActionType)serializedExit.FindProperty("_actionType").enumValueIndex;
                    MacroSceneType targetScene =
                        (MacroSceneType)serializedExit.FindProperty("_targetScene").enumValueIndex;

                    if (actionType == CombatArenaExitActionType.ReloadCurrentScene)
                    {
                        foundReloadExit = true;
                    }

                    if (actionType == CombatArenaExitActionType.LoadMacroScene
                        && targetScene == MacroSceneType.HubWorld)
                    {
                        foundHubExit = true;
                    }
                }

                NUnitAssert.IsTrue(foundReloadExit, "Expected a reload-current-scene exit in CombatArena_1.");
                NUnitAssert.IsTrue(foundHubExit, "Expected a hub-return exit in CombatArena_1.");
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
        public void SpawnPlanner_RejectsCandidateInsidePlayerExclusionRadius()
        {
            bool found = CombatEncounterSpawnPlanner.TryChooseSpawnPose(
                Vector3.zero,
                0f,
                Vector3.zero,
                5f,
                new System.Random(1),
                _ => new CombatEncounterSpawnCandidateResult(true, Vector3.zero),
                out _,
                out _);

            NUnitAssert.IsFalse(found);
        }

        [Test]
        public void SpawnPlanner_RejectsCandidatesWithoutWaterSamplesOrLandClearance()
        {
            bool noWaterFound = CombatEncounterSpawnPlanner.TryChooseSpawnPose(
                Vector3.zero,
                0f,
                Vector3.forward * 20f,
                1f,
                new System.Random(2),
                _ => default,
                out _,
                out _);

            bool shorelineRejected = CombatEncounterSpawnPlanner.TryChooseSpawnPose(
                Vector3.zero,
                0f,
                Vector3.forward * 20f,
                1f,
                new System.Random(3),
                _ => default,
                out _,
                out _);

            NUnitAssert.IsFalse(noWaterFound);
            NUnitAssert.IsFalse(shorelineRejected);
        }

        [Test]
        public void SpawnPlanner_UsesValidatedSurfacePointAndFacesTowardExclusionCenter()
        {
            Vector3 expectedSurfacePoint = new(12f, 3f, -4f);

            bool found = CombatEncounterSpawnPlanner.TryChooseSpawnPose(
                Vector3.zero,
                0f,
                Vector3.zero,
                1f,
                new System.Random(4),
                _ => new CombatEncounterSpawnCandidateResult(true, expectedSurfacePoint),
                out Vector3 spawnPosition,
                out Quaternion spawnRotation);

            NUnitAssert.IsTrue(found);
            NUnitAssert.AreEqual(expectedSurfacePoint, spawnPosition);

            Vector3 expectedForward = (Vector3.zero - expectedSurfacePoint);
            expectedForward.y = 0f;
            expectedForward.Normalize();

            Vector3 actualForward = spawnRotation * Vector3.forward;
            actualForward.y = 0f;
            actualForward.Normalize();

            NUnitAssert.AreEqual(expectedForward.x, actualForward.x, 0.001f);
            NUnitAssert.AreEqual(expectedForward.z, actualForward.z, 0.001f);
        }

        [Test]
        public void SpawnZoneUtility_BuildsRoundRobinAssignmentsAcrossZones()
        {
            GameObject zoneA = new("ZoneA");
            GameObject zoneB = new("ZoneB");
            GameObject zoneC = new("ZoneC");

            try
            {
                IReadOnlyList<GameObject> assignments = EncounterSpawnZoneUtility.BuildRoundRobinAssignments(
                    new[] { zoneA, zoneB, zoneC },
                    8);

                NUnitAssert.AreEqual(8, assignments.Count);
                NUnitAssert.AreSame(zoneA, assignments[0]);
                NUnitAssert.AreSame(zoneB, assignments[1]);
                NUnitAssert.AreSame(zoneC, assignments[2]);
                NUnitAssert.AreSame(zoneA, assignments[3]);
                NUnitAssert.AreSame(zoneB, assignments[4]);
                NUnitAssert.AreSame(zoneC, assignments[5]);
                NUnitAssert.AreSame(zoneA, assignments[6]);
                NUnitAssert.AreSame(zoneB, assignments[7]);
            }
            finally
            {
                Object.DestroyImmediate(zoneA);
                Object.DestroyImmediate(zoneB);
                Object.DestroyImmediate(zoneC);
            }
        }

        [Test]
        public void SpawnZoneUtility_FiltersNullAndInactiveZones()
        {
            GameObject activeZone = new("ActiveZone");
            GameObject inactiveZone = new("InactiveZone");
            inactiveZone.SetActive(false);

            try
            {
                IReadOnlyList<GameObject> assignments = EncounterSpawnZoneUtility.BuildRoundRobinAssignments(
                    new[] { activeZone, null, inactiveZone },
                    3);

                NUnitAssert.AreEqual(3, assignments.Count);
                NUnitAssert.AreSame(activeZone, assignments[0]);
                NUnitAssert.AreSame(activeZone, assignments[1]);
                NUnitAssert.AreSame(activeZone, assignments[2]);
            }
            finally
            {
                Object.DestroyImmediate(activeZone);
                Object.DestroyImmediate(inactiveZone);
            }
        }

        [Test]
        public void TerrainValidation_DetectsTerrainAboveWater_WhenTerrainExceedsConfiguredProbeHeight()
        {
            int terrainLayer = LayerMask.NameToLayer("Terrain");
            NUnitAssert.AreNotEqual(-1, terrainLayer, "Expected project layer 'Terrain' to exist.");

            GameObject terrainRoot = new("TerrainBlocker");
            terrainRoot.layer = terrainLayer;
            BoxCollider terrainCollider = terrainRoot.AddComponent<BoxCollider>();
            terrainCollider.center = new Vector3(0f, 12f, 0f);
            terrainCollider.size = new Vector3(8f, 4f, 8f);

            try
            {
                NavalPointValidationSettings settings = new(
                    LayerMask.GetMask("Terrain"),
                    3f,
                    6f,
                    0.15f,
                    0f,
                    0);

                bool blocked = NavalPointValidationUtility.IsTerrainAboveWater(Vector3.zero, 0f, settings);
                NUnitAssert.IsTrue(blocked);
            }
            finally
            {
                Object.DestroyImmediate(terrainRoot);
            }
        }

        [Test]
        public void EncounterManager_CompletesImmediatelyWhenDefinitionIsInvalid()
        {
            GameObject managerRoot = new("EncounterManagerTest");
            EncounterManager manager = managerRoot.AddComponent<EncounterManager>();
            EncounterDefinition invalidDefinition = ScriptableObject.CreateInstance<EncounterDefinition>();

            try
            {
                SetSerializedObjectReference(manager, "_encounterDefinition", invalidDefinition);
                RunEnumeratorToCompletion(manager.ExecuteStartup(new GameplaySceneStartupContext(MacroSceneType.CombatArena, null)));

                NUnitAssert.IsTrue(manager.IsEncounterComplete);
                NUnitAssert.AreEqual(0, manager.ActiveTrackedEnemyCount);
            }
            finally
            {
                Object.DestroyImmediate(invalidDefinition);
                Object.DestroyImmediate(managerRoot);
            }
        }

        [Test]
        public void EncounterManager_CompletesWhenTrackedEnemiesAreDefeated()
        {
            GameObject managerRoot = new("EncounterManagerTrackTest");
            EncounterManager manager = managerRoot.AddComponent<EncounterManager>();
            GameObject enemyA = new("EnemyA");
            GameObject enemyB = new("EnemyB");

            try
            {
                InvokePrivate(manager, "TrackSpawnedEnemy", enemyA);
                InvokePrivate(manager, "TrackSpawnedEnemy", enemyB);

                InvokePrivate(manager, "OnEnemyDeath", new EnemyDeathEvent(enemyA));
                NUnitAssert.IsFalse(manager.IsEncounterComplete);
                NUnitAssert.AreEqual(1, manager.ActiveTrackedEnemyCount);

                InvokePrivate(manager, "OnEnemyDeath", new EnemyDeathEvent(enemyB));
                NUnitAssert.IsTrue(manager.IsEncounterComplete);
                NUnitAssert.AreEqual(0, manager.ActiveTrackedEnemyCount);
            }
            finally
            {
                Object.DestroyImmediate(enemyA);
                Object.DestroyImmediate(enemyB);
                Object.DestroyImmediate(managerRoot);
            }
        }

        private static void SetSerializedObjectReference(Object target, string propertyName, Object referenceValue)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            NUnitAssert.IsNotNull(property, $"Expected property '{propertyName}' to exist on {target.name}.");
            property.objectReferenceValue = referenceValue;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RunEnumeratorToCompletion(IEnumerator enumerator)
        {
            while (enumerator != null && enumerator.MoveNext())
            {
            }
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected private method '{methodName}' on {target.GetType().Name}.");
            return method.Invoke(target, arguments);
        }
    }
}
#endif
