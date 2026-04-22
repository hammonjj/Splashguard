using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Input;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public sealed class BoatSpawnPoint : MonoBehaviourBase
    {
        private const string PlayersSceneName = "Players";
        private const string PlayersScenePathSuffix = "/Players.unity";

        [SerializeField, Required] private GameObject _playerVesselPrefab;

        protected override void OnEnabled()
        {
            _globalMessageBus?.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
        }

        protected override void OnStarted()
        {
            TrySpawnForCurrentMacroScene();
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            if (@event == null || !@event.SceneType.IsGameplayScene())
            {
                return;
            }

            TrySpawnBoat();
        }

        private void TrySpawnForCurrentMacroScene()
        {
            MacroSceneType currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

            if (!currentMacroScene.IsGameplayScene())
            {
                return;
            }

            TrySpawnBoat();
        }

        private void TrySpawnBoat()
        {
            if (_playerVesselPrefab == null)
            {
                LogError($"{nameof(BoatSpawnPoint)} requires a player vessel prefab reference.");
                return;
            }

            List<BoatSpawnPoint> activeSpawnPoints = GetActiveSpawnPoints();
            if (activeSpawnPoints.Count > 1)
            {
                LogError(
                    $"Multiple enabled {nameof(BoatSpawnPoint)} components are loaded. count={activeSpawnPoints.Count}. Only one player vessel will be spawned.");
            }

            BoatSpawnPoint primarySpawnPoint = ResolvePrimarySpawnPoint(activeSpawnPoints);
            if (primarySpawnPoint != this)
            {
                return;
            }

            if (ShouldSkipSpawn())
            {
                LogInfo("Skipped player vessel spawn because a PlayerVesselRoot already exists in the loaded scene set.");
                return;
            }

            GameObject spawnedBoat = SpawnBoatInstance();
            if (spawnedBoat == null)
            {
                return;
            }

            LogInfo(
                $"Spawned player vessel '{spawnedBoat.name}' at marker '{name}'. position={transform.position}, rotation={transform.rotation.eulerAngles}.");
        }

        private bool ShouldSkipSpawn()
        {
            return FindObjectsByType<PlayerVesselRoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length > 0;
        }

        private GameObject SpawnBoatInstance()
        {
            Object spawnedObject = Object.Instantiate(
                (Object)_playerVesselPrefab,
                transform.position,
                transform.rotation);
            if (spawnedObject is not GameObject spawnedBoat)
            {
                LogError(
                    $"Player vessel prefab '{_playerVesselPrefab.name}' instantiated as '{spawnedObject?.GetType().FullName ?? "null"}' instead of {nameof(GameObject)}.");

                if (spawnedObject != null)
                {
                    Destroy(spawnedObject);
                }

                return null;
            }

            Scene spawnScene = ResolveSpawnScene();
            if (spawnScene.IsValid() && spawnScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(spawnedBoat, spawnScene);
            }

            if (spawnedBoat.TryGetComponent(out Rigidbody boatRigidbody))
            {
                boatRigidbody.linearVelocity = Vector3.zero;
                boatRigidbody.angularVelocity = Vector3.zero;
            }

            AnchorControls anchorControls = spawnedBoat.GetComponentInChildren<AnchorControls>(includeInactive: true);
            if (anchorControls != null)
            {
                anchorControls.DropAnchor();
            }
            else
            {
                LogWarning($"Spawned player vessel '{spawnedBoat.name}' does not contain {nameof(AnchorControls)}.");
            }

            return spawnedBoat;
        }

        private Scene ResolveSpawnScene()
        {
            if (TryResolvePlayersScene(out Scene playersScene))
            {
                return playersScene;
            }

            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            {
                LogWarning(
                    $"Could not resolve the loaded {PlayersSceneName} scene for player vessel spawning. Falling back to marker scene '{gameObject.scene.name}'.");
                return gameObject.scene;
            }

            return default;
        }

        private static bool TryResolvePlayersScene(out Scene playersScene)
        {
            PlayerCoordinator playerCoordinator = StaticData.PlayerInputCoordinator;
            if (playerCoordinator != null)
            {
                Scene coordinatorScene = playerCoordinator.gameObject.scene;
                if (coordinatorScene.IsValid() && coordinatorScene.isLoaded)
                {
                    playersScene = coordinatorScene;
                    return true;
                }
            }

            Scene sceneByName = SceneManager.GetSceneByName(PlayersSceneName);
            if (sceneByName.IsValid() && sceneByName.isLoaded)
            {
                playersScene = sceneByName;
                return true;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                {
                    continue;
                }

                if (loadedScene.path.EndsWith(PlayersScenePathSuffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    playersScene = loadedScene;
                    return true;
                }
            }

            playersScene = default;
            return false;
        }

        private static List<BoatSpawnPoint> GetActiveSpawnPoints()
        {
            BoatSpawnPoint[] discoveredSpawnPoints =
                FindObjectsByType<BoatSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var activeSpawnPoints = new List<BoatSpawnPoint>(discoveredSpawnPoints.Length);

            for (int i = 0; i < discoveredSpawnPoints.Length; i++)
            {
                BoatSpawnPoint spawnPoint = discoveredSpawnPoints[i];
                if (spawnPoint != null
                    && spawnPoint.enabled
                    && spawnPoint.gameObject.activeInHierarchy
                    && spawnPoint.gameObject.scene.IsValid()
                    && spawnPoint.gameObject.scene.isLoaded)
                {
                    activeSpawnPoints.Add(spawnPoint);
                }
            }

            return activeSpawnPoints;
        }

        private static BoatSpawnPoint ResolvePrimarySpawnPoint(List<BoatSpawnPoint> activeSpawnPoints)
        {
            BoatSpawnPoint primarySpawnPoint = null;
            int bestInstanceId = int.MaxValue;

            for (int i = 0; i < activeSpawnPoints.Count; i++)
            {
                BoatSpawnPoint candidate = activeSpawnPoints[i];
                if (candidate == null)
                {
                    continue;
                }

                int candidateInstanceId = candidate.GetInstanceID();
                if (primarySpawnPoint == null || candidateInstanceId < bestInstanceId)
                {
                    primarySpawnPoint = candidate;
                    bestInstanceId = candidateInstanceId;
                }
            }

            return primarySpawnPoint;
        }
    }
}
