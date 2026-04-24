using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Constants;
using Bitbox.Splashguard.Nautical;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public sealed class BoatPassengerVolume : MonoBehaviourBase
    {
        private sealed class RiderState
        {
            public Transform OriginalParent;
            public bool IsSuspended;
        }

        [SerializeField] private Collider _volumeTrigger;
        [SerializeField] private PlayerVesselRoot _playerVesselRoot;

        private readonly Dictionary<PlayerInput, RiderState> _riderStatesByPlayer = new();
        private readonly Dictionary<PlayerInput, int> _overlapCountsByPlayer = new();
        private readonly List<PlayerInput> _scratchPlayers = new();
        private readonly List<PlayerInput> _playersToDetach = new();

        private Rigidbody _boatRigidbody;

        public bool IsRiderAttached(PlayerInput playerInput)
        {
            return playerInput != null
                && _riderStatesByPlayer.TryGetValue(playerInput, out RiderState riderState)
                && !riderState.IsSuspended;
        }

        public bool IsRiderTracked(PlayerInput playerInput)
        {
            return playerInput != null && _riderStatesByPlayer.ContainsKey(playerInput);
        }

        public bool IsRiderSuspended(PlayerInput playerInput)
        {
            return playerInput != null
                && _riderStatesByPlayer.TryGetValue(playerInput, out RiderState riderState)
                && riderState.IsSuspended;
        }

        public bool IsPlayerWithinVolume(PlayerInput playerInput)
        {
            return IsPlayerOverlappingVolume(playerInput);
        }

        public bool TryAttachRider(PlayerInput playerInput)
        {
            return SetRiderState(playerInput, isSuspended: false, operation: "attach");
        }

        public bool TrySuspendRider(PlayerInput playerInput)
        {
            return SetRiderState(playerInput, isSuspended: true, operation: "suspend");
        }

        public bool ResumeRider(PlayerInput playerInput)
        {
            return SetRiderState(playerInput, isSuspended: false, operation: "resume");
        }

        public void DetachRiderWithMomentum(PlayerInput playerInput)
        {
            DetachRider(playerInput, applyMomentum: true, reason: "manual_detach_with_momentum");
        }

        public static bool TryResolveForPlayer(PlayerInput playerInput, out BoatPassengerVolume passengerVolume)
        {
            passengerVolume = null;
            if (playerInput == null)
            {
                return false;
            }

            PlayerVesselRoot attachedVesselRoot = playerInput.GetComponentInParent<PlayerVesselRoot>();
            if (attachedVesselRoot != null)
            {
                passengerVolume = attachedVesselRoot.GetComponentInChildren<BoatPassengerVolume>(includeInactive: true);
                if (passengerVolume != null)
                {
                    return true;
                }
            }

            BoatPassengerVolume[] activeVolumes =
                FindObjectsByType<BoatPassengerVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < activeVolumes.Length; i++)
            {
                BoatPassengerVolume candidate = activeVolumes[i];
                if (candidate != null && candidate.IsRiderTracked(playerInput))
                {
                    passengerVolume = candidate;
                    return true;
                }
            }

            for (int i = 0; i < activeVolumes.Length; i++)
            {
                BoatPassengerVolume candidate = activeVolumes[i];
                if (candidate != null && candidate.IsPlayerWithinVolume(playerInput))
                {
                    passengerVolume = candidate;
                    return true;
                }
            }

            return false;
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            RefreshPassengers();
        }

        protected override void OnStarted()
        {
            RefreshPassengers();
        }

        protected override void OnUpdated()
        {
            RefreshPassengers();
        }

        protected override void OnDisabled()
        {
            DetachAllPlayers(applyMomentum: false);
            _overlapCountsByPlayer.Clear();
        }

        protected override void OnDestroyed()
        {
            DetachAllPlayers(applyMomentum: false);
            _overlapCountsByPlayer.Clear();
        }

        protected override void OnTriggerEntered(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput))
            {
                return;
            }

            IncrementOverlapCount(playerInput);
            LogInfo(
                $"Passenger volume trigger entered. volume={name}, player={DescribePlayerState(playerInput)}, overlapCount={GetOverlapCount(playerInput)}, collider={other.name}, withinVolume={IsPlayerOverlappingVolume(playerInput)}.");
            RefreshPassenger(playerInput);
        }

        protected override void OnTriggerExited(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput))
            {
                return;
            }

            DecrementOverlapCount(playerInput);
            LogInfo(
                $"Passenger volume trigger exited. volume={name}, player={DescribePlayerState(playerInput)}, overlapCount={GetOverlapCount(playerInput)}, collider={other.name}, withinVolume={IsPlayerOverlappingVolume(playerInput)}.");
            RefreshPassenger(playerInput);
        }

        private void CacheReferences()
        {
            _volumeTrigger ??= GetComponent<Collider>();
            _playerVesselRoot ??= GetComponentInParent<PlayerVesselRoot>();
            _boatRigidbody ??= _playerVesselRoot != null
                ? _playerVesselRoot.GetComponent<Rigidbody>()
                : GetComponentInParent<Rigidbody>();

            if (_volumeTrigger == null || !_volumeTrigger.isTrigger)
            {
                LogError($"{nameof(BoatPassengerVolume)} requires a trigger collider on the same GameObject.");
                enabled = false;
                return;
            }

            if (_playerVesselRoot == null)
            {
                LogError($"{nameof(BoatPassengerVolume)} could not resolve a {nameof(PlayerVesselRoot)} in its parent hierarchy.");
                enabled = false;
            }
        }

        private bool SetRiderState(PlayerInput playerInput, bool isSuspended, string operation)
        {
            CacheReferences();
            if (playerInput == null || _playerVesselRoot == null)
            {
                return false;
            }

            if (TryResolveForPlayer(playerInput, out BoatPassengerVolume currentPassengerVolume)
                && currentPassengerVolume != null
                && currentPassengerVolume != this)
            {
                currentPassengerVolume.DetachRider(playerInput, applyMomentum: false, reason: "transfer_to_other_volume");
            }

            Transform playerTransform = playerInput.transform;
            bool wasTracked = _riderStatesByPlayer.TryGetValue(playerInput, out RiderState riderState);
            bool wasSuspended = wasTracked && riderState.IsSuspended;
            Transform previousParent = playerTransform.parent;
            if (!wasTracked)
            {
                riderState = new RiderState
                {
                    OriginalParent = ResolveOriginalParent(playerTransform.parent),
                };
                _riderStatesByPlayer[playerInput] = riderState;
            }
            else if (riderState.OriginalParent == null || riderState.OriginalParent == _playerVesselRoot.transform)
            {
                riderState.OriginalParent = ResolveOriginalParent(playerTransform.parent);
            }

            riderState.IsSuspended = isSuspended;
            EnsureFreeRoamParent(playerInput, riderState);
            ApplyInheritedVelocity(playerInput, Vector3.zero);
            ReconcilePlayerOverlap(playerInput);
            LogInfo(
                $"Rider state changed. volume={name}, operation={operation}, player={DescribePlayerState(playerInput)}, wasTracked={wasTracked}, wasSuspended={wasSuspended}, previousParent={previousParent?.name ?? "None"}, currentParent={playerTransform.parent?.name ?? "None"}, originalParent={riderState.OriginalParent?.name ?? "None"}, overlapCount={GetOverlapCount(playerInput)}, localPosition={ResolveBoatLocalPosition(playerTransform)}, boatVelocity={ResolveBoatLinearVelocity()}.");
            return true;
        }

        private void RefreshPassengers()
        {
            if (_playerVesselRoot == null)
            {
                return;
            }

            var playerCoordinator = StaticData.PlayerInputCoordinator;
            IReadOnlyList<PlayerInput> players = playerCoordinator != null ? playerCoordinator.PlayerInputs : null;
            if (players == null)
            {
                DetachAllPlayers(applyMomentum: false);
                _overlapCountsByPlayer.Clear();
                return;
            }

            ReconcileTrackedOverlaps(players);

            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                RefreshPassenger(players[playerIndex]);
            }

            _playersToDetach.Clear();
            foreach (var riderEntry in _riderStatesByPlayer)
            {
                PlayerInput playerInput = riderEntry.Key;
                if (playerInput == null || !playerInput.gameObject.activeInHierarchy || !ContainsPlayer(players, playerInput))
                {
                    _playersToDetach.Add(playerInput);
                }
            }

            for (int i = 0; i < _playersToDetach.Count; i++)
            {
                DetachRider(_playersToDetach[i], applyMomentum: false, reason: "player_missing_from_coordinator");
            }
        }

        private void RefreshPassenger(PlayerInput playerInput)
        {
            if (playerInput == null || !playerInput.gameObject.activeInHierarchy)
            {
                DetachRider(playerInput, applyMomentum: false, reason: "inactive_player");
                return;
            }

            bool isOverlapping = HasTrackedOverlap(playerInput);
            bool isFreeRoamPlayer = IsFreeRoamPlayer(playerInput);

            if (isFreeRoamPlayer && isOverlapping)
            {
                if (!IsRiderAttached(playerInput) || IsRiderSuspended(playerInput))
                {
                    TryAttachRider(playerInput);
                }
                else if (_riderStatesByPlayer.TryGetValue(playerInput, out RiderState activeRiderState))
                {
                    EnsureFreeRoamParent(playerInput, activeRiderState);
                }

                return;
            }

            if (!_riderStatesByPlayer.TryGetValue(playerInput, out RiderState riderState))
            {
                return;
            }

            if (riderState.IsSuspended || isOverlapping)
            {
                EnsureFreeRoamParent(playerInput, riderState);
                return;
            }

            DetachRider(playerInput, applyMomentum: true, reason: "left_passenger_volume");
        }

        private bool HasTrackedOverlap(PlayerInput playerInput)
        {
            return playerInput != null
                && _overlapCountsByPlayer.TryGetValue(playerInput, out int overlapCount)
                && overlapCount > 0;
        }

        private void ReconcileTrackedOverlaps(IReadOnlyList<PlayerInput> players)
        {
            _scratchPlayers.Clear();
            foreach (var trackedPlayer in _overlapCountsByPlayer.Keys)
            {
                _scratchPlayers.Add(trackedPlayer);
            }

            for (int i = 0; i < _scratchPlayers.Count; i++)
            {
                PlayerInput trackedPlayer = _scratchPlayers[i];
                if (trackedPlayer == null || !ContainsPlayer(players, trackedPlayer))
                {
                    _overlapCountsByPlayer.Remove(trackedPlayer);
                }
            }

            for (int i = 0; i < players.Count; i++)
            {
                ReconcilePlayerOverlap(players[i]);
            }
        }

        private void ReconcilePlayerOverlap(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return;
            }

            if (IsPlayerOverlappingVolume(playerInput))
            {
                _overlapCountsByPlayer[playerInput] = Mathf.Max(
                    1,
                    _overlapCountsByPlayer.TryGetValue(playerInput, out int overlapCount)
                        ? overlapCount
                        : 0);
                return;
            }

            _overlapCountsByPlayer.Remove(playerInput);
        }

        private bool IsFreeRoamPlayer(PlayerInput playerInput)
        {
            return playerInput.currentActionMap != null
                && playerInput.currentActionMap.name == Strings.ThirdPersonControls
                && !HelmControl.TryGetActiveHelm(playerInput.playerIndex, out _)
                && !DeckMountedGunControl.TryGetActiveGun(playerInput.playerIndex, out _)
                && !CargoBayControls.TryGetActiveCargoBay(playerInput.playerIndex, out _)
                && !LadderControl.TryGetActiveLadder(playerInput.playerIndex, out _);
        }

        private bool IsPlayerOverlappingVolume(PlayerInput playerInput)
        {
            if (playerInput == null
                || _volumeTrigger == null
                || !_volumeTrigger.enabled
                || !_volumeTrigger.gameObject.activeInHierarchy)
            {
                return false;
            }

            Collider[] playerColliders = playerInput.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider != null
                    && playerCollider.enabled
                    && playerCollider.gameObject.activeInHierarchy
                    && playerCollider.bounds.Intersects(_volumeTrigger.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private void IncrementOverlapCount(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return;
            }

            if (_overlapCountsByPlayer.TryGetValue(playerInput, out int overlapCount))
            {
                _overlapCountsByPlayer[playerInput] = overlapCount + 1;
                return;
            }

            _overlapCountsByPlayer[playerInput] = 1;
        }

        private void DecrementOverlapCount(PlayerInput playerInput)
        {
            if (playerInput == null || !_overlapCountsByPlayer.TryGetValue(playerInput, out int overlapCount))
            {
                return;
            }

            if (overlapCount <= 1)
            {
                _overlapCountsByPlayer.Remove(playerInput);
                return;
            }

            _overlapCountsByPlayer[playerInput] = overlapCount - 1;
        }

        private void EnsureFreeRoamParent(PlayerInput playerInput, RiderState riderState)
        {
            if (playerInput == null)
            {
                return;
            }

            Transform targetParent = ResolveDetachParent(riderState);
            if (playerInput.transform.parent != targetParent)
            {
                playerInput.transform.SetParent(targetParent, true);
            }
        }

        private void DetachRider(PlayerInput playerInput, bool applyMomentum, string reason)
        {
            if (playerInput == null)
            {
                return;
            }

            if (!_riderStatesByPlayer.TryGetValue(playerInput, out RiderState riderState))
            {
                _overlapCountsByPlayer.Remove(playerInput);
                return;
            }

            Vector3 inheritedVelocity = applyMomentum
                ? ResolveBoatPointVelocity(playerInput.transform.position)
                : Vector3.zero;
            Transform targetParent = ResolveDetachParent(riderState);
            if (playerInput.transform.parent != targetParent)
            {
                playerInput.transform.SetParent(targetParent, true);
            }

            ApplyInheritedVelocity(playerInput, inheritedVelocity);
            LogInfo(
                $"Rider detached. volume={name}, reason={reason}, player={DescribePlayerState(playerInput)}, targetParent={targetParent?.name ?? "None"}, overlapCount={GetOverlapCount(playerInput)}, inheritedVelocity={inheritedVelocity}, localPosition={ResolveBoatLocalPosition(playerInput.transform)}, boatVelocity={ResolveBoatLinearVelocity()}.");
            _riderStatesByPlayer.Remove(playerInput);
            _overlapCountsByPlayer.Remove(playerInput);
        }

        private void DetachAllPlayers(bool applyMomentum)
        {
            if (_riderStatesByPlayer.Count == 0)
            {
                return;
            }

            _playersToDetach.Clear();
            foreach (var riderEntry in _riderStatesByPlayer)
            {
                _playersToDetach.Add(riderEntry.Key);
            }

            for (int i = 0; i < _playersToDetach.Count; i++)
            {
                DetachRider(_playersToDetach[i], applyMomentum, reason: "detach_all_players");
            }
        }

        private Transform ResolveOriginalParent(Transform currentParent)
        {
            if (currentParent != null && currentParent != _playerVesselRoot.transform)
            {
                return currentParent;
            }

            return StaticData.PlayerInputCoordinator != null
                ? StaticData.PlayerInputCoordinator.transform
                : null;
        }

        private Transform ResolveDetachParent(RiderState riderState)
        {
            if (riderState != null
                && riderState.OriginalParent != null
                && riderState.OriginalParent != _playerVesselRoot.transform)
            {
                return riderState.OriginalParent;
            }

            return StaticData.PlayerInputCoordinator != null
                ? StaticData.PlayerInputCoordinator.transform
                : null;
        }

        private Vector3 ResolveBoatPointVelocity(Vector3 worldPosition)
        {
            return _boatRigidbody != null
                ? _boatRigidbody.GetPointVelocity(worldPosition)
                : Vector3.zero;
        }

        private static void ApplyInheritedVelocity(PlayerInput playerInput, Vector3 inheritedVelocity)
        {
            if (playerInput == null || !playerInput.TryGetComponent(out PlayerMovement playerMovement))
            {
                return;
            }

            playerMovement.ApplyInheritedWorldVelocity(inheritedVelocity);
        }

        private static bool TryResolvePlayerInput(Collider other, out PlayerInput playerInput)
        {
            playerInput = other != null ? other.GetComponentInParent<PlayerInput>() : null;
            return playerInput != null;
        }

        private int GetOverlapCount(PlayerInput playerInput)
        {
            return playerInput != null && _overlapCountsByPlayer.TryGetValue(playerInput, out int overlapCount)
                ? overlapCount
                : 0;
        }

        private Vector3 ResolveBoatLinearVelocity()
        {
            return _boatRigidbody != null ? _boatRigidbody.linearVelocity : Vector3.zero;
        }

        private Vector3 ResolveBoatLocalPosition(Transform playerTransform)
        {
            return _playerVesselRoot != null && playerTransform != null
                ? _playerVesselRoot.transform.InverseTransformPoint(playerTransform.position)
                : Vector3.zero;
        }

        private string DescribePlayerState(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return "None";
            }

            return $"{DescribePlayerForLogs(playerInput)}, actionMap={playerInput.currentActionMap?.name ?? "None"}, parent={playerInput.transform.parent?.name ?? "None"}, worldPosition={playerInput.transform.position}";
        }

        private static string DescribePlayerForLogs(PlayerInput playerInput)
        {
            return playerInput == null
                ? "None"
                : $"{playerInput.name}[index={playerInput.playerIndex}, scheme={playerInput.currentControlScheme}]";
        }

        private static bool ContainsPlayer(IReadOnlyList<PlayerInput> players, PlayerInput playerInput)
        {
            if (players == null || playerInput == null)
            {
                return false;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == playerInput)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
