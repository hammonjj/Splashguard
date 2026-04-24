using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Eventing.GlobalEvents;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    public enum LadderExitDirection
    {
        None,
        Bottom,
        Top
    }

    public enum LadderPlayerFacingMode
    {
        OppositeLadderForward,
        LadderForward,
        TopAnchorRotation
    }

    public static class LadderClimbUtility
    {
        public static float ProjectNormalized(Vector3 bottom, Vector3 top, Vector3 position)
        {
            Vector3 ladderVector = top - bottom;
            float ladderLengthSquared = ladderVector.sqrMagnitude;
            if (ladderLengthSquared <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Mathf.Clamp01(Vector3.Dot(position - bottom, ladderVector) / ladderLengthSquared);
        }

        public static Vector3 EvaluatePosition(Vector3 bottom, Vector3 top, float normalizedPosition)
        {
            return Vector3.Lerp(bottom, top, Mathf.Clamp01(normalizedPosition));
        }

        public static float CalculateDistanceFromSegment(Vector3 bottom, Vector3 top, Vector3 position)
        {
            return Vector3.Distance(position, EvaluatePosition(bottom, top, ProjectNormalized(bottom, top, position)));
        }

        public static float AdvanceNormalized(
            float current,
            float climbInput,
            float climbSpeed,
            float ladderLength,
            float deltaTime,
            out LadderExitDirection exitDirection)
        {
            exitDirection = LadderExitDirection.None;

            if (ladderLength <= Mathf.Epsilon || climbSpeed <= 0f || deltaTime <= 0f || Mathf.Abs(climbInput) <= Mathf.Epsilon)
            {
                return Mathf.Clamp01(current);
            }

            float unbounded = current + (climbInput * climbSpeed * deltaTime / ladderLength);
            if (unbounded <= 0f && climbInput < 0f)
            {
                exitDirection = LadderExitDirection.Bottom;
                return 0f;
            }

            if (unbounded >= 1f && climbInput > 0f)
            {
                exitDirection = LadderExitDirection.Top;
                return 1f;
            }

            return Mathf.Clamp01(unbounded);
        }
    }

    [DisallowMultipleComponent]
    public sealed class LadderControl : MonoBehaviourBase
    {
        private const string LowerInteractionTriggerName = "InteractionTrigger";
        private const string UpperInteractionTriggerName = "UpperInteractionTrigger";
        private const string BottomAnchorName = "BottomAnchor";
        private const string TopAnchorName = "TopAnchor";
        private const string BottomExitAnchorName = "BottomExitAnchor";
        private const string TopExitAnchorName = "TopExitAnchor";
        private static readonly Dictionary<int, LadderControl> ActiveLaddersByPlayerIndex = new();

        [Header("References")]
        [SerializeField] private Collider[] _interactionTriggers;
        [SerializeField] private Transform _bottomAnchor;
        [SerializeField] private Transform _topAnchor;
        [SerializeField] private Transform _bottomExitAnchor;
        [SerializeField] private Transform _topExitAnchor;

        [Header("Climb")]
        [SerializeField, Min(0.01f)] private float _climbSpeed = 1.6f;
        [SerializeField, Min(0f)] private float _mountSnapDistance = 0.35f;
        [SerializeField] private LadderPlayerFacingMode _playerFacingMode = LadderPlayerFacingMode.OppositeLadderForward;
        [SerializeField] private bool _autoDismountAtEnds = true;
        [SerializeField, Range(0f, 1f)] private float _climbInputDeadZone = 0.1f;
        [SerializeField] private bool _drawGizmos = true;

        private readonly Dictionary<PlayerInput, int> _overlappingPlayers = new();
        private readonly List<PlayerInput> _staleOverlappingPlayers = new();

        private PlayerInput _climbingPlayerInput;
        private PlayerMovement _climbingPlayerMovement;
        private BoatPassengerVolume _boatPassengerVolume;
        private InputAction _moveAction;
        private Rigidbody _triggerRigidbody;
        private float _climbT;
        private bool _isPaused;

        public float NormalizedClimbPosition => _climbT;
        public bool HasActiveClimber => _climbingPlayerInput != null;

        public static bool TryGetActiveLadder(int playerIndex, out LadderControl ladder)
        {
            return ActiveLaddersByPlayerIndex.TryGetValue(playerIndex, out ladder) && ladder != null;
        }

        public static void ReleaseAllForSceneTransition()
        {
            if (ActiveLaddersByPlayerIndex.Count == 0)
            {
                return;
            }

            LadderControl[] activeLadders = new LadderControl[ActiveLaddersByPlayerIndex.Count];
            ActiveLaddersByPlayerIndex.Values.CopyTo(activeLadders, 0);

            for (int i = 0; i < activeLadders.Length; i++)
            {
                LadderControl activeLadder = activeLadders[i];
                if (activeLadder == null)
                {
                    continue;
                }

                activeLadder.ReleaseForSceneTransition();
            }
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            if (!enabled)
            {
                return;
            }

            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnDisabled()
        {
            ReleaseClimbingPlayer(ResolveNearestExitDirection(), "disabled");
            _overlappingPlayers.Clear();
            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnUpdated()
        {
            if (_climbingPlayerInput == null)
            {
                if (!_isPaused)
                {
                    RefreshInteractionState();
                    TryHandleMountRequest();
                }

                return;
            }

            if (_isPaused)
            {
                SyncClimbingPlayerPose(0f);
                return;
            }

            UpdateClimb(Time.deltaTime);
        }

        protected override void OnLateUpdated()
        {
            if (_climbingPlayerInput != null)
            {
                SyncClimbingPlayerPose(0f);
            }
        }

        protected override void OnTriggerEntered(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput))
            {
                return;
            }

            if (!IsPlayerOverlappingInteractionTrigger(playerInput))
            {
                return;
            }

            if (_overlappingPlayers.TryGetValue(playerInput, out int overlapCount))
            {
                _overlappingPlayers[playerInput] = overlapCount + 1;
                return;
            }

            _overlappingPlayers[playerInput] = 1;
            LogInfo($"Player entered ladder range. ladder={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        protected override void OnTriggerExited(Collider other)
        {
            if (!TryResolvePlayerInput(other, out PlayerInput playerInput)
                || !_overlappingPlayers.ContainsKey(playerInput))
            {
                return;
            }

            if (IsPlayerOverlappingInteractionTrigger(playerInput))
            {
                return;
            }

            _overlappingPlayers.Remove(playerInput);
            LogInfo($"Player left ladder range. ladder={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        protected override void OnDrawnGizmos()
        {
            if (!_drawGizmos)
            {
                return;
            }

            Transform bottom = _bottomAnchor != null ? _bottomAnchor : FindChildByName(transform, BottomAnchorName);
            Transform top = _topAnchor != null ? _topAnchor : FindChildByName(transform, TopAnchorName);
            Transform bottomExit = _bottomExitAnchor != null ? _bottomExitAnchor : FindChildByName(transform, BottomExitAnchorName);
            Transform topExit = _topExitAnchor != null ? _topExitAnchor : FindChildByName(transform, TopExitAnchorName);

            if (bottom != null && top != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(bottom.position, top.position);
                Gizmos.DrawWireSphere(bottom.position, 0.06f);
                Gizmos.DrawWireSphere(top.position, 0.06f);
            }

            Gizmos.color = Color.green;
            if (bottomExit != null)
            {
                Gizmos.DrawWireCube(bottomExit.position, Vector3.one * 0.12f);
            }

            if (topExit != null)
            {
                Gizmos.DrawWireCube(topExit.position, Vector3.one * 0.12f);
            }
        }

        private void CacheReferences()
        {
            ConfigureTriggerRigidbody();
            _boatPassengerVolume ??= ResolveBoatPassengerVolume();

            _bottomAnchor ??= FindChildByName(transform, BottomAnchorName);
            _topAnchor ??= FindChildByName(transform, TopAnchorName);
            _bottomExitAnchor ??= FindChildByName(transform, BottomExitAnchorName);
            _topExitAnchor ??= FindChildByName(transform, TopExitAnchorName);
            _interactionTriggers = ResolveInteractionTriggers();

            if (_bottomAnchor == null || _topAnchor == null || _bottomExitAnchor == null || _topExitAnchor == null)
            {
                LogError($"Ladder is missing required anchors. ladder={name}, bottom={_bottomAnchor?.name ?? "None"}, top={_topAnchor?.name ?? "None"}, bottomExit={_bottomExitAnchor?.name ?? "None"}, topExit={_topExitAnchor?.name ?? "None"}.");
                enabled = false;
                return;
            }

            if (_interactionTriggers.Length == 0)
            {
                LogError($"Ladder could not resolve any trigger colliders. ladder={name}. Add lower and upper interaction trigger colliders.");
                enabled = false;
                return;
            }

            float ladderLength = Vector3.Distance(_bottomAnchor.position, _topAnchor.position);
            if (ladderLength <= Mathf.Epsilon)
            {
                LogError($"Ladder climb path has zero length. ladder={name}, bottom={_bottomAnchor.position}, top={_topAnchor.position}.");
                enabled = false;
            }
        }

        private void ConfigureTriggerRigidbody()
        {
            _triggerRigidbody ??= GetComponent<Rigidbody>();
            if (_triggerRigidbody == null)
            {
                _triggerRigidbody = gameObject.AddComponent<Rigidbody>();
            }

            _triggerRigidbody.useGravity = false;
            _triggerRigidbody.isKinematic = true;
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
        }

        private void TryHandleMountRequest()
        {
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                PlayerInput playerInput = overlappingEntry.Key;
                if (playerInput == null
                    || !IsPlayerOverlappingInteractionTrigger(playerInput)
                    || !CanPlayerUseLadder(playerInput)
                    || !WasLadderActionPressedThisFrame(playerInput))
                {
                    continue;
                }

                TryBeginClimb(playerInput);
                return;
            }
        }

        private bool TryBeginClimb(PlayerInput playerInput)
        {
            if (playerInput == null || _climbingPlayerInput != null)
            {
                return false;
            }

            if (!playerInput.TryGetComponent(out PlayerMovement playerMovement))
            {
                LogError($"Player cannot use ladder because it has no {nameof(PlayerMovement)}. ladder={name}, player={DescribePlayer(playerInput)}.");
                return false;
            }

            float distanceFromLadder = LadderClimbUtility.CalculateDistanceFromSegment(
                _bottomAnchor.position,
                _topAnchor.position,
                playerInput.transform.position);
            if (_mountSnapDistance > 0f && distanceFromLadder > _mountSnapDistance)
            {
                LogInfo($"Player requested ladder mount but is outside snap distance. ladder={name}, player={DescribePlayer(playerInput)}, distance={distanceFromLadder:0.###}, snapDistance={_mountSnapDistance:0.###}.");
                return false;
            }

            if (!TryBindClimbActions(playerInput))
            {
                return false;
            }

            if (!playerMovement.TryBeginScriptedMovement(this))
            {
                _moveAction = null;
                LogInfo($"Player requested ladder mount but movement is already externally owned. ladder={name}, player={DescribePlayer(playerInput)}.");
                return false;
            }

            _climbingPlayerInput = playerInput;
            _climbingPlayerMovement = playerMovement;
            bool suspendedRider = _boatPassengerVolume != null && _boatPassengerVolume.TrySuspendRider(playerInput);
            _climbT = LadderClimbUtility.ProjectNormalized(_bottomAnchor.position, _topAnchor.position, playerInput.transform.position);
            ActiveLaddersByPlayerIndex[playerInput.playerIndex] = this;
            SyncClimbingPlayerPose(0f);
            LogInfo($"Player mounted ladder. ladder={name}, player={DescribePlayer(playerInput)}, t={_climbT:0.###}, suspendedRider={suspendedRider}, riderVolume={_boatPassengerVolume?.name ?? "None"}.");
            return true;
        }

        private void UpdateClimb(float deltaTime)
        {
            float climbInput = ReadClimbInput();
            float ladderLength = Vector3.Distance(_bottomAnchor.position, _topAnchor.position);
            float nextT = LadderClimbUtility.AdvanceNormalized(
                _climbT,
                climbInput,
                _climbSpeed,
                ladderLength,
                deltaTime,
                out LadderExitDirection exitDirection);

            _climbT = nextT;
            SyncClimbingPlayerPose(climbInput);

            if (_autoDismountAtEnds && exitDirection != LadderExitDirection.None)
            {
                ReleaseClimbingPlayer(exitDirection, $"auto_exit_{exitDirection.ToString().ToLowerInvariant()}");
            }
        }

        private float ReadClimbInput()
        {
            if (_moveAction == null)
            {
                return 0f;
            }

            float input = Mathf.Clamp(_moveAction.ReadValue<Vector2>().y, -1f, 1f);
            return Mathf.Abs(input) <= _climbInputDeadZone ? 0f : input;
        }

        private void SyncClimbingPlayerPose(float locomotionNormalized)
        {
            if (_climbingPlayerInput == null)
            {
                return;
            }

            Vector3 position = LadderClimbUtility.EvaluatePosition(_bottomAnchor.position, _topAnchor.position, _climbT);
            Quaternion rotation = ResolvePlayerRotation();

            if (_climbingPlayerMovement == null
                || !_climbingPlayerMovement.TryApplyScriptedMovementPose(this, position, rotation, Mathf.Abs(locomotionNormalized)))
            {
                SetPlayerTransform(_climbingPlayerInput, position, rotation);
            }
        }

        private void ReleaseClimbingPlayer(LadderExitDirection exitDirection, string reason)
        {
            if (_climbingPlayerInput == null)
            {
                _moveAction = null;
                _climbingPlayerMovement = null;
                return;
            }

            PlayerInput releasedPlayerInput = _climbingPlayerInput;
            PlayerMovement releasedPlayerMovement = _climbingPlayerMovement;
            Transform exitAnchor = ResolveExitAnchor(exitDirection);
            if (exitAnchor != null)
            {
                if (releasedPlayerMovement == null
                    || !releasedPlayerMovement.TryApplyScriptedMovementPose(this, exitAnchor.position, exitAnchor.rotation, 0f))
                {
                    SetPlayerTransform(releasedPlayerInput, exitAnchor.position, exitAnchor.rotation);
                }
            }

            if (ActiveLaddersByPlayerIndex.TryGetValue(releasedPlayerInput.playerIndex, out LadderControl activeLadder)
                && activeLadder == this)
            {
                ActiveLaddersByPlayerIndex.Remove(releasedPlayerInput.playerIndex);
            }

            releasedPlayerMovement?.EndScriptedMovement(this);
            bool wasWithinPassengerVolume = _boatPassengerVolume != null && _boatPassengerVolume.IsPlayerWithinVolume(releasedPlayerInput);
            bool resumedRider = false;
            bool detachedWithMomentum = false;
            if (_boatPassengerVolume != null)
            {
                if (wasWithinPassengerVolume)
                {
                    resumedRider = _boatPassengerVolume.ResumeRider(releasedPlayerInput);
                }
                else
                {
                    detachedWithMomentum = true;
                    _boatPassengerVolume.DetachRiderWithMomentum(releasedPlayerInput);
                }
            }

            LogInfo($"Player released ladder. ladder={name}, player={DescribePlayer(releasedPlayerInput)}, reason={reason}, t={_climbT:0.###}, wasWithinPassengerVolume={wasWithinPassengerVolume}, resumedRider={resumedRider}, detachedWithMomentum={detachedWithMomentum}, riderVolume={_boatPassengerVolume?.name ?? "None"}, playerParent={releasedPlayerInput.transform.parent?.name ?? "None"}, playerPosition={releasedPlayerInput.transform.position}.");
            _climbingPlayerInput = null;
            _climbingPlayerMovement = null;
            _moveAction = null;
        }

        private void ReleaseForSceneTransition()
        {
            ReleaseClimbingPlayer(ResolveNearestExitDirection(), "scene_transition");
        }

        private Transform ResolveExitAnchor(LadderExitDirection exitDirection)
        {
            return exitDirection == LadderExitDirection.Top
                ? _topExitAnchor
                : _bottomExitAnchor;
        }

        private LadderExitDirection ResolveNearestExitDirection()
        {
            return _climbT >= 0.5f ? LadderExitDirection.Top : LadderExitDirection.Bottom;
        }

        private Quaternion ResolvePlayerRotation()
        {
            if (_playerFacingMode == LadderPlayerFacingMode.TopAnchorRotation && _topAnchor != null)
            {
                return _topAnchor.rotation;
            }

            Vector3 up = _topAnchor != null && _bottomAnchor != null
                ? (_topAnchor.position - _bottomAnchor.position).normalized
                : transform.up;
            if (up.sqrMagnitude <= Mathf.Epsilon)
            {
                up = transform.up.sqrMagnitude > Mathf.Epsilon ? transform.up : Vector3.up;
            }

            Vector3 forward = _playerFacingMode == LadderPlayerFacingMode.LadderForward
                ? transform.forward
                : -transform.forward;
            forward = Vector3.ProjectOnPlane(forward, up);
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        private bool TryBindClimbActions(PlayerInput playerInput)
        {
            InputActionMap thirdPersonMap = playerInput.actions.FindActionMap(Strings.ThirdPersonControls, throwIfNotFound: false);
            _moveAction = thirdPersonMap?.FindAction(Strings.MoveAction, throwIfNotFound: false);
            if (_moveAction != null)
            {
                return true;
            }

            LogError($"Ladder requires the '{Strings.ThirdPersonControls}/{Strings.MoveAction}' input action. ladder={name}, player={DescribePlayer(playerInput)}.");
            return false;
        }

        private void RefreshInteractionState()
        {
            _staleOverlappingPlayers.Clear();
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                PlayerInput playerInput = overlappingEntry.Key;
                if (playerInput == null || !IsPlayerOverlappingInteractionTrigger(playerInput))
                {
                    _staleOverlappingPlayers.Add(playerInput);
                }
            }

            for (int i = 0; i < _staleOverlappingPlayers.Count; i++)
            {
                _overlappingPlayers.Remove(_staleOverlappingPlayers[i]);
            }
        }

        private bool IsPlayerOverlappingInteractionTrigger(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return false;
            }

            Collider[] playerColliders = playerInput.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int playerColliderIndex = 0; playerColliderIndex < playerColliders.Length; playerColliderIndex++)
            {
                Collider playerCollider = playerColliders[playerColliderIndex];
                if (playerCollider == null || !playerCollider.enabled)
                {
                    continue;
                }

                for (int triggerIndex = 0; triggerIndex < _interactionTriggers.Length; triggerIndex++)
                {
                    Collider trigger = _interactionTriggers[triggerIndex];
                    if (trigger != null
                        && trigger.enabled
                        && trigger.isTrigger
                        && playerCollider.bounds.Intersects(trigger.bounds))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Collider[] ResolveInteractionTriggers()
        {
            var triggers = new List<Collider>();
            if (_interactionTriggers != null)
            {
                for (int i = 0; i < _interactionTriggers.Length; i++)
                {
                    Collider trigger = _interactionTriggers[i];
                    if (trigger != null && trigger.isTrigger && !triggers.Contains(trigger))
                    {
                        triggers.Add(trigger);
                    }
                }
            }

            AddNamedTrigger(LowerInteractionTriggerName, triggers);
            AddNamedTrigger(UpperInteractionTriggerName, triggers);

            Collider[] childColliders = GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider candidate = childColliders[i];
                if (candidate != null && candidate.isTrigger && !triggers.Contains(candidate))
                {
                    triggers.Add(candidate);
                }
            }

            return triggers.ToArray();
        }

        private void AddNamedTrigger(string triggerName, List<Collider> triggers)
        {
            Transform triggerTransform = FindChildByName(transform, triggerName);
            if (triggerTransform == null)
            {
                return;
            }

            Collider[] namedTriggers = triggerTransform.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < namedTriggers.Length; i++)
            {
                Collider namedTrigger = namedTriggers[i];
                if (namedTrigger != null && namedTrigger.isTrigger && !triggers.Contains(namedTrigger))
                {
                    triggers.Add(namedTrigger);
                }
            }
        }

        private static bool CanPlayerUseLadder(PlayerInput playerInput)
        {
            return playerInput != null
                && playerInput.currentActionMap != null
                && playerInput.currentActionMap.name == Strings.ThirdPersonControls
                && !TryGetActiveLadder(playerInput.playerIndex, out _)
                && !HelmControl.TryGetActiveHelm(playerInput.playerIndex, out _)
                && !DeckMountedGunControl.TryGetActiveGun(playerInput.playerIndex, out _)
                && !AnchorControls.IsPlayerInAnchorControlRange(playerInput)
                && playerInput.TryGetComponent(out PlayerMovement _);
        }

        private static bool WasLadderActionPressedThisFrame(PlayerInput playerInput)
        {
            InputActionMap thirdPersonMap = playerInput.actions.FindActionMap(Strings.ThirdPersonControls, throwIfNotFound: false);
            InputAction actionAction = thirdPersonMap?.FindAction(Strings.ActionAction, throwIfNotFound: false);
            return actionAction != null && actionAction.WasPressedThisFrame();
        }

        private static bool TryResolvePlayerInput(Collider other, out PlayerInput playerInput)
        {
            playerInput = other != null ? other.GetComponentInParent<PlayerInput>() : null;
            return playerInput != null;
        }

        private BoatPassengerVolume ResolveBoatPassengerVolume()
        {
            PlayerVesselRoot vesselRoot = GetComponentInParent<PlayerVesselRoot>();
            return vesselRoot != null
                ? vesselRoot.GetComponentInChildren<BoatPassengerVolume>(includeInactive: true)
                : null;
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

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nestedChild = FindChildByName(child, childName);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private static string DescribePlayer(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return "null";
            }

            return $"{playerInput.name}[index={playerInput.playerIndex}, scheme={playerInput.currentControlScheme}]";
        }
    }
}
