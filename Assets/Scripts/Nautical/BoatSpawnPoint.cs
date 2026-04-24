using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Input;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public sealed class BoatSpawnPoint : MonoBehaviourBase
    {
        private readonly struct RiderPoseSnapshot
        {
            public RiderPoseSnapshot(PlayerInput playerInput, Vector3 localPosition, Quaternion localRotation)
            {
                PlayerInput = playerInput;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
            }

            public PlayerInput PlayerInput { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
        }

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
                    $"Multiple enabled {nameof(BoatSpawnPoint)} components are loaded. count={activeSpawnPoints.Count}. Only one player vessel will be positioned.");
            }

            BoatSpawnPoint primarySpawnPoint = ResolvePrimarySpawnPoint(activeSpawnPoints);
            if (primarySpawnPoint != this)
            {
                return;
            }

            GameObject existingBoat = ResolveExistingBoat();
            bool reusedExistingBoat = existingBoat != null;
            GameObject boat = existingBoat ?? SpawnBoatInstance();
            if (boat == null)
            {
                return;
            }

            PlaceBoatAtMarkerPose(boat);

            LogInfo(
                $"{(reusedExistingBoat ? "Positioned existing" : "Spawned")} player vessel '{boat.name}' at marker '{name}'. position={transform.position}, rotation={transform.rotation.eulerAngles}.");
        }

        private GameObject ResolveExistingBoat()
        {
            PlayerVesselRoot[] vesselRoots =
                FindObjectsByType<PlayerVesselRoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (vesselRoots.Length == 0)
            {
                return null;
            }

            PlayerVesselRoot primaryBoat = null;
            int bestInstanceId = int.MaxValue;

            for (int i = 0; i < vesselRoots.Length; i++)
            {
                PlayerVesselRoot candidate = vesselRoots[i];
                if (candidate == null
                    || !candidate.gameObject.scene.IsValid()
                    || !candidate.gameObject.scene.isLoaded)
                {
                    continue;
                }

                int candidateInstanceId = candidate.GetInstanceID();
                if (primaryBoat == null || candidateInstanceId < bestInstanceId)
                {
                    primaryBoat = candidate;
                    bestInstanceId = candidateInstanceId;
                }
            }

            if (vesselRoots.Length > 1)
            {
                LogWarning(
                    $"Multiple active {nameof(PlayerVesselRoot)} instances are loaded. count={vesselRoots.Length}. Repositioning '{primaryBoat?.name ?? "None"}'.");
            }

            return primaryBoat != null ? primaryBoat.gameObject : null;
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

            return spawnedBoat;
        }

        private void PlaceBoatAtMarkerPose(GameObject boat)
        {
            if (boat == null)
            {
                return;
            }

            List<RiderPoseSnapshot> riderPoseSnapshots = CaptureRiderPoseSnapshots(boat);
            LogBoatPlacementSnapshot(boat, "before_place");
            Scene spawnScene = ResolveSpawnScene();
            if (spawnScene.IsValid() && spawnScene.isLoaded && boat.scene.handle != spawnScene.handle)
            {
                SceneManager.MoveGameObjectToScene(boat, spawnScene);
            }

            AnchorControls anchorControls = boat.GetComponentInChildren<AnchorControls>(includeInactive: true);
            bool shouldReenableAnchorControls = anchorControls != null && anchorControls.enabled;
            if (shouldReenableAnchorControls)
            {
                anchorControls.enabled = false;
            }

            boat.transform.SetPositionAndRotation(transform.position, transform.rotation);

            if (boat.TryGetComponent(out Rigidbody boatRigidbody))
            {
                boatRigidbody.position = transform.position;
                boatRigidbody.rotation = transform.rotation;
                boatRigidbody.linearVelocity = Vector3.zero;
                boatRigidbody.angularVelocity = Vector3.zero;
                boatRigidbody.Sleep();
            }

            if (anchorControls != null)
            {
                if (shouldReenableAnchorControls)
                {
                    anchorControls.enabled = true;
                    anchorControls.DropAnchor();
                }
            }
            else
            {
                LogWarning($"Player vessel '{boat.name}' does not contain {nameof(AnchorControls)}.");
            }

            RestoreRiderPoseSnapshots(boat, riderPoseSnapshots);
            LogBoatPlacementSnapshot(boat, "after_place");
        }

        private static List<RiderPoseSnapshot> CaptureRiderPoseSnapshots(GameObject boat)
        {
            var riderPoseSnapshots = new List<RiderPoseSnapshot>();
            if (boat == null)
            {
                return riderPoseSnapshots;
            }

            BoatPassengerVolume passengerVolume = boat.GetComponentInChildren<BoatPassengerVolume>(includeInactive: true);
            if (passengerVolume == null)
            {
                return riderPoseSnapshots;
            }

            PlayerInput[] players = ResolvePlayersSnapshot();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerInput playerInput = players[i];
                if (playerInput == null || !passengerVolume.IsRiderTracked(playerInput))
                {
                    continue;
                }

                riderPoseSnapshots.Add(
                    new RiderPoseSnapshot(
                        playerInput,
                        boat.transform.InverseTransformPoint(playerInput.transform.position),
                        Quaternion.Inverse(boat.transform.rotation) * playerInput.transform.rotation));
            }

            return riderPoseSnapshots;
        }

        private static void RestoreRiderPoseSnapshots(GameObject boat, List<RiderPoseSnapshot> riderPoseSnapshots)
        {
            if (boat == null || riderPoseSnapshots == null)
            {
                return;
            }

            for (int i = 0; i < riderPoseSnapshots.Count; i++)
            {
                RiderPoseSnapshot riderPoseSnapshot = riderPoseSnapshots[i];
                PlayerInput playerInput = riderPoseSnapshot.PlayerInput;
                if (playerInput == null)
                {
                    continue;
                }

                SetPlayerTransform(
                    playerInput,
                    boat.transform.TransformPoint(riderPoseSnapshot.LocalPosition),
                    boat.transform.rotation * riderPoseSnapshot.LocalRotation);
            }
        }

        private static void SetPlayerTransform(PlayerInput playerInput, Vector3 position, Quaternion rotation)
        {
            if (playerInput == null)
            {
                return;
            }

            CharacterController characterController = playerInput.GetComponent<CharacterController>();
            bool controllerWasEnabled = characterController != null && characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            playerInput.transform.SetPositionAndRotation(position, rotation);

            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }
        }

        private void LogBoatPlacementSnapshot(GameObject boat, string phase)
        {
            if (boat == null)
            {
                return;
            }

            BoatPassengerVolume passengerVolume = boat.GetComponentInChildren<BoatPassengerVolume>(includeInactive: true);
            bool hasRigidbody = boat.TryGetComponent(out Rigidbody boatRigidbody);
            Vector3 boatVelocity = hasRigidbody ? boatRigidbody.linearVelocity : Vector3.zero;
            Vector3 boatAngularVelocity = hasRigidbody ? boatRigidbody.angularVelocity : Vector3.zero;
            LogInfo(
                $"Boat placement snapshot. phase={phase}, marker={name}, boat={boat.name}, boatScene={boat.scene.name}, boatPosition={boat.transform.position}, boatRotation={boat.transform.rotation.eulerAngles}, boatVelocity={boatVelocity}, boatAngularVelocity={boatAngularVelocity}, passengerVolume={passengerVolume?.name ?? "None"}.");

            PlayerInput[] players = ResolvePlayersSnapshot();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerInput playerInput = players[i];
                if (playerInput == null)
                {
                    continue;
                }

                bool isChildOfBoat = playerInput.transform.IsChildOf(boat.transform);
                Vector3 localPosition = boat.transform.InverseTransformPoint(playerInput.transform.position);
                bool isRiderAttached = passengerVolume != null && passengerVolume.IsRiderAttached(playerInput);
                bool isRiderSuspended = passengerVolume != null && passengerVolume.IsRiderSuspended(playerInput);
                bool isWithinVolume = passengerVolume != null && passengerVolume.IsPlayerWithinVolume(playerInput);
                LogInfo(
                    $"Boat placement player snapshot. phase={phase}, marker={name}, player={DescribePlayerForLogs(playerInput)}, actionMap={playerInput.currentActionMap?.name ?? "None"}, parent={playerInput.transform.parent?.name ?? "None"}, isChildOfBoat={isChildOfBoat}, isRiderAttached={isRiderAttached}, isRiderSuspended={isRiderSuspended}, isWithinPassengerVolume={isWithinVolume}, worldPosition={playerInput.transform.position}, localPosition={localPosition}.");
            }
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

        private static PlayerInput[] ResolvePlayersSnapshot()
        {
            PlayerCoordinator playerCoordinator = StaticData.PlayerInputCoordinator;
            if (playerCoordinator != null && playerCoordinator.PlayerInputs != null)
            {
                List<PlayerInput> players = new();
                for (int i = 0; i < playerCoordinator.PlayerInputs.Count; i++)
                {
                    PlayerInput playerInput = playerCoordinator.PlayerInputs[i];
                    if (playerInput != null)
                    {
                        players.Add(playerInput);
                    }
                }

                return players.ToArray();
            }

            return FindObjectsByType<PlayerInput>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private static string DescribePlayerForLogs(PlayerInput playerInput)
        {
            return playerInput == null
                ? "None"
                : $"{playerInput.name}[index={playerInput.playerIndex}, scheme={playerInput.currentControlScheme}]";
        }
    }
}
