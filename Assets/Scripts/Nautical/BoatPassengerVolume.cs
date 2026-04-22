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
        [SerializeField] private Collider _volumeTrigger;
        [SerializeField] private PlayerVesselRoot _playerVesselRoot;

        private readonly Dictionary<PlayerInput, Transform> _originalParentsByPlayer = new();
        private readonly List<PlayerInput> _attachedPlayers = new();
        private readonly List<PlayerInput> _detachCandidates = new();

        protected override void OnEnabled()
        {
            CacheReferences();
        }

        protected override void OnUpdated()
        {
            RefreshPassengers();
        }

        protected override void OnDisabled()
        {
            DetachAllPlayers();
        }

        protected override void OnDestroyed()
        {
            DetachAllPlayers();
        }

        private void CacheReferences()
        {
            _volumeTrigger ??= GetComponent<Collider>();
            _playerVesselRoot ??= GetComponentInParent<PlayerVesselRoot>();

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
                DetachAllPlayers();
                return;
            }

            _detachCandidates.Clear();
            for (int i = 0; i < _attachedPlayers.Count; i++)
            {
                _detachCandidates.Add(_attachedPlayers[i]);
            }

            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                PlayerInput playerInput = players[playerIndex];
                if (!ShouldAttachPlayer(playerInput))
                {
                    continue;
                }

                AttachPlayer(playerInput);
                _detachCandidates.Remove(playerInput);
            }

            for (int i = 0; i < _detachCandidates.Count; i++)
            {
                DetachPlayer(_detachCandidates[i]);
            }
        }

        private bool ShouldAttachPlayer(PlayerInput playerInput)
        {
            return playerInput != null
                && playerInput.gameObject.activeInHierarchy
                && IsFreeRoamPlayer(playerInput)
                && IsPlayerOverlappingVolume(playerInput);
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

        private void AttachPlayer(PlayerInput playerInput)
        {
            if (playerInput == null || _playerVesselRoot == null)
            {
                return;
            }

            Transform playerTransform = playerInput.transform;
            if (!_originalParentsByPlayer.ContainsKey(playerInput))
            {
                _originalParentsByPlayer[playerInput] = playerTransform.parent;
            }

            if (playerTransform.parent != _playerVesselRoot.transform)
            {
                playerTransform.SetParent(_playerVesselRoot.transform, true);
            }

            if (!_attachedPlayers.Contains(playerInput))
            {
                _attachedPlayers.Add(playerInput);
            }
        }

        private void DetachPlayer(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                _attachedPlayers.Remove(playerInput);
                _originalParentsByPlayer.Remove(playerInput);
                return;
            }

            Transform playerTransform = playerInput.transform;
            Transform targetParent = ResolveDetachParent(playerInput);
            if (_playerVesselRoot != null && playerTransform.parent == _playerVesselRoot.transform)
            {
                playerTransform.SetParent(targetParent, true);
            }

            _attachedPlayers.Remove(playerInput);
            _originalParentsByPlayer.Remove(playerInput);
        }

        private void DetachAllPlayers()
        {
            if (_attachedPlayers.Count == 0)
            {
                return;
            }

            _detachCandidates.Clear();
            for (int i = 0; i < _attachedPlayers.Count; i++)
            {
                _detachCandidates.Add(_attachedPlayers[i]);
            }

            for (int i = 0; i < _detachCandidates.Count; i++)
            {
                DetachPlayer(_detachCandidates[i]);
            }

            _detachCandidates.Clear();
        }

        private Transform ResolveDetachParent(PlayerInput playerInput)
        {
            if (_originalParentsByPlayer.TryGetValue(playerInput, out Transform originalParent)
                && originalParent != null
                && originalParent != _playerVesselRoot.transform)
            {
                return originalParent;
            }

            return StaticData.PlayerInputCoordinator != null
                ? StaticData.PlayerInputCoordinator.transform
                : null;
        }
    }
}
