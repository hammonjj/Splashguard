using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Constants;
using Bitbox.Splashguard.Nautical;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace Bitbox
{
    public enum AnchorState
    {
        Raised,
        Lowering,
        Dropped,
        Raising
    }

    [DisallowMultipleComponent]
    public sealed class AnchorControls : MonoBehaviourBase
    {
        private const string InteractionTriggerName = "InteractionTrigger";
        private const string AnchorPathName = "AnchorPath";
        private const string RuntimeVisualRootName = "RuntimeAnchorVisuals";
        private static readonly Dictionary<PlayerInput, int> AnchorRangeCountsByPlayer = new();

        [Header("References")]
        [SerializeField, Required] private GameObject _anchorPrefab;
        [SerializeField, Required] private GameObject _chainLinkPrefab;
        [SerializeField] private Collider _interactionTrigger;
        [SerializeField] private Transform _dropPoint;
        [SerializeField] private SplineContainer _anchorPath;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float _lowerDuration = 3f;
        [SerializeField, Min(0.01f)] private float _raiseDuration = 2f;

        [Header("Spawn Behavior")]
        [SerializeField] private bool _dropAnchorOnSpawn = true;
        [SerializeField, Min(0f)] private float _spawnAutoDropDelay = 0.5f;

        [Header("Visual")]
        [SerializeField, Min(0.01f)] private float _fixedDropDepth = 3f;
        [SerializeField, Tooltip("World-space spacing between chain links. Lower values create a denser chain."), Min(0.01f)]
        private float _chainLinkSpacing = 0.075f;
        [SerializeField, Min(0)] private int _maxChainLinks = 48;
        [SerializeField] private bool _alignAnchorToPath = true;
        [SerializeField] private float _chainLinkBaseTwistDegrees;
        [SerializeField] private float _chainLinkAlternateTwistDegrees = 90f;

        [Header("Hold Physics")]
        [SerializeField, Min(0f)] private float _slackRadius = 0.35f;
        [SerializeField, Min(0.01f)] private float _horizontalStopTime = 4f;
        [SerializeField, Min(0f)] private float _holdSpringAcceleration = 1.25f;
        [SerializeField, Min(0f)] private float _maxAnchorAcceleration = 6.5f;

        private readonly Dictionary<PlayerInput, int> _overlappingPlayers = new();
        private readonly List<GameObject> _chainLinkInstances = new();
        private readonly List<PlayerInput> _staleOverlappingPlayers = new();

        private Rigidbody _boatRigidbody;
        private Transform _boatTransform;
        private Transform _visualRoot;
        private GameObject _anchorInstance;
        private Vector3 _anchorHoldPoint;
        private Vector3 _anchorBottomPoint;
        private Vector3 _raiseStartPoint;
        private float _transitionElapsed;
        private bool _spawnAutoDropHandled;
        private float _spawnAutoDropElapsed;

        public AnchorState CurrentState { get; private set; } = AnchorState.Raised;
        public bool IsAnchorDropped => CurrentState == AnchorState.Dropped;
        public bool IsTransitioning => CurrentState == AnchorState.Lowering || CurrentState == AnchorState.Raising;
        public Vector3 AnchorHoldPoint => _anchorHoldPoint;

        public static bool IsPlayerInAnchorControlRange(PlayerInput playerInput)
        {
            return playerInput != null && AnchorRangeCountsByPlayer.ContainsKey(playerInput);
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            _spawnAutoDropHandled = false;
            _spawnAutoDropElapsed = 0f;
        }

        protected override void OnDisabled()
        {
            ClearAnchorRangeRegistrations();
            _overlappingPlayers.Clear();
            DestroyAnchorVisuals();
            CurrentState = AnchorState.Raised;
            _transitionElapsed = 0f;
            _spawnAutoDropHandled = false;
            _spawnAutoDropElapsed = 0f;
        }

        protected override void OnUpdated()
        {
            TryHandleSpawnAutoDrop(Time.deltaTime);
            RefreshInteractionState();
            TryHandleAnchorToggleRequest();
            UpdateAnchorTransition(Time.deltaTime);
            UpdateAnchorVisuals();
        }

        protected override void OnFixedUpdated()
        {
            ApplyAnchorHold();
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
            AddPlayerToAnchorRange(playerInput);
            LogInfo(
                $"Player entered anchor controls range. controls={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
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
            RemovePlayerFromAnchorRange(playerInput);
            LogInfo(
                $"Player left anchor controls range. controls={name}, player={DescribePlayer(playerInput)}, overlappingPlayers={_overlappingPlayers.Count}.");
        }

        protected override void OnDrawnGizmos()
        {
            Vector3 top = GetAnchorRaisedPosition();
            Vector3 bottom = GetAnchorLoweredPosition();

            Gizmos.color = Color.yellow;
            if (HasUsableAnchorPath())
            {
                DrawAnchorPathGizmo();
            }
            else
            {
                Gizmos.DrawLine(top, bottom);
            }

            Gizmos.DrawWireSphere(bottom, 0.08f);

            if (CurrentState == AnchorState.Lowering || CurrentState == AnchorState.Dropped)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_anchorHoldPoint, _slackRadius);
            }
        }

        public bool DropAnchor()
        {
            if (CurrentState != AnchorState.Raised || _boatRigidbody == null)
            {
                return false;
            }

            Vector3 dropPoint = GetAnchorRaisedPosition();
            _anchorHoldPoint = _boatRigidbody.position;
            _anchorBottomPoint = GetAnchorLoweredPosition();
            _transitionElapsed = 0f;
            CurrentState = AnchorState.Lowering;
            EnsureAnchorVisuals();
            SetAnchorVisualPose(ResolveAnchorVisualPoseAtPathT(0f, dropPoint));
            UpdateAnchorVisuals();
            LogInfo($"Anchor lowering. controls={name}, holdPoint={_anchorHoldPoint}, bottomPoint={_anchorBottomPoint}.");
            return true;
        }

        public bool RaiseAnchor()
        {
            if (CurrentState != AnchorState.Dropped)
            {
                return false;
            }

            _raiseStartPoint = GetAnchorVisualPosition();
            _transitionElapsed = 0f;
            CurrentState = AnchorState.Raising;
            LogInfo($"Anchor raising. controls={name}, startPoint={_raiseStartPoint}.");
            return true;
        }

        public bool ToggleAnchor()
        {
            return CurrentState switch
            {
                AnchorState.Raised => DropAnchor(),
                AnchorState.Dropped => RaiseAnchor(),
                _ => false
            };
        }

        private void CacheReferences()
        {
            ConfigureTriggerRigidbody();

            if (_boatRigidbody == null)
            {
                _boatRigidbody = ResolveBoatRigidbody();
            }

            if (_boatRigidbody == null)
            {
                LogError(
                    $"Anchor controls could not resolve a parent rigidbody. controls={name}, parent={transform.parent?.name ?? "None"}, root={transform.root?.name ?? "None"}.");
                enabled = false;
                return;
            }

            _boatTransform = _boatRigidbody.transform;
            _dropPoint ??= transform;
            _anchorPath ??= ResolveAnchorPath();
            _interactionTrigger = ResolveInteractionTrigger();
            if (_interactionTrigger == null)
            {
                LogError(
                    $"Anchor controls could not resolve a trigger collider. controls={name}, root={_boatTransform.name}. Assign a trigger collider on the anchor interaction volume.");
                enabled = false;
                return;
            }

            Assert.IsNotNull(_anchorPrefab, $"{nameof(AnchorControls)} requires an anchor prefab.");
            Assert.IsNotNull(_chainLinkPrefab, $"{nameof(AnchorControls)} requires a chain link prefab.");
            DisablePhysicalAnchorControlColliders();
            IgnorePhysicalColliderContactsWithBoat();
        }

        private Rigidbody ResolveBoatRigidbody()
        {
            Rigidbody[] parentRigidbodies = GetComponentsInParent<Rigidbody>(includeInactive: true);
            for (int i = 0; i < parentRigidbodies.Length; i++)
            {
                Rigidbody parentRigidbody = parentRigidbodies[i];
                if (parentRigidbody != null && parentRigidbody.transform != transform)
                {
                    return parentRigidbody;
                }
            }

            return null;
        }

        private void ConfigureTriggerRigidbody()
        {
            Rigidbody triggerRigidbody = GetComponent<Rigidbody>();
            if (triggerRigidbody == null)
            {
                triggerRigidbody = gameObject.AddComponent<Rigidbody>();
            }

            triggerRigidbody.useGravity = false;
            triggerRigidbody.isKinematic = true;
        }

        private Collider ResolveInteractionTrigger()
        {
            if (_interactionTrigger != null && _interactionTrigger.isTrigger)
            {
                return _interactionTrigger;
            }

            Transform triggerTransform = FindChildByName(transform, InteractionTriggerName);
            if (triggerTransform != null
                && triggerTransform.TryGetComponent(out Collider namedTrigger)
                && namedTrigger.isTrigger)
            {
                return namedTrigger;
            }

            Collider[] candidateColliders = GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < candidateColliders.Length; i++)
            {
                Collider candidateCollider = candidateColliders[i];
                if (candidateCollider != null && candidateCollider.isTrigger)
                {
                    return candidateCollider;
                }
            }

            return null;
        }

        private SplineContainer ResolveAnchorPath()
        {
            Transform pathTransform = FindChildByName(transform, AnchorPathName);
            if (pathTransform != null && pathTransform.TryGetComponent(out SplineContainer namedPath))
            {
                return namedPath;
            }

            SplineContainer[] candidatePaths = GetComponentsInChildren<SplineContainer>(includeInactive: true);
            return candidatePaths.Length > 0 ? candidatePaths[0] : null;
        }

        private void IgnorePhysicalColliderContactsWithBoat()
        {
            if (_boatTransform == null || _boatTransform == transform)
            {
                return;
            }

            Collider[] anchorColliders = GetComponentsInChildren<Collider>(includeInactive: true);
            Collider[] boatColliders = _boatTransform.GetComponentsInChildren<Collider>(includeInactive: true);

            for (int anchorIndex = 0; anchorIndex < anchorColliders.Length; anchorIndex++)
            {
                Collider anchorCollider = anchorColliders[anchorIndex];
                if (!IsPhysicalAnchorControlCollider(anchorCollider))
                {
                    continue;
                }

                for (int boatIndex = 0; boatIndex < boatColliders.Length; boatIndex++)
                {
                    Collider boatCollider = boatColliders[boatIndex];
                    if (!IsPhysicalBoatCollider(boatCollider))
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(anchorCollider, boatCollider, true);
                }
            }
        }

        private void DisablePhysicalAnchorControlColliders()
        {
            Collider[] anchorColliders = GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < anchorColliders.Length; i++)
            {
                Collider anchorCollider = anchorColliders[i];
                if (IsPhysicalAnchorControlCollider(anchorCollider))
                {
                    anchorCollider.enabled = false;
                }
            }
        }

        private void TryHandleSpawnAutoDrop(float deltaTime)
        {
            if (!_dropAnchorOnSpawn || _spawnAutoDropHandled)
            {
                return;
            }

            if (CurrentState != AnchorState.Raised)
            {
                _spawnAutoDropHandled = true;
                return;
            }

            _spawnAutoDropElapsed += Mathf.Max(0f, deltaTime);
            if (_spawnAutoDropElapsed < _spawnAutoDropDelay)
            {
                return;
            }

            _spawnAutoDropHandled = true;
            if (DropAnchor())
            {
                LogInfo($"Anchor auto-dropped after spawn delay. controls={name}, delay={_spawnAutoDropDelay:0.###}.");
            }
        }

        private void ClearAnchorRangeRegistrations()
        {
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                RemovePlayerFromAnchorRange(overlappingEntry.Key);
            }
        }

        private void RefreshInteractionState()
        {
            if (_interactionTrigger == null)
            {
                return;
            }

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
                PlayerInput playerInput = _staleOverlappingPlayers[i];
                _overlappingPlayers.Remove(playerInput);
                RemovePlayerFromAnchorRange(playerInput);
            }
        }

        private void TryHandleAnchorToggleRequest()
        {
            foreach (var overlappingEntry in _overlappingPlayers)
            {
                PlayerInput playerInput = overlappingEntry.Key;
                if (playerInput == null
                    || !IsPlayerOverlappingInteractionTrigger(playerInput)
                    || !CanPlayerUseAnchor(playerInput))
                {
                    continue;
                }

                if (!WasAnchorActionPressedThisFrame(playerInput))
                {
                    continue;
                }

                LogInfo($"Player toggled anchor. controls={name}, player={DescribePlayer(playerInput)}, state={CurrentState}.");
                ToggleAnchor();
                return;
            }
        }

        private void UpdateAnchorTransition(float deltaTime)
        {
            if (!IsTransitioning)
            {
                return;
            }

            float duration = CurrentState == AnchorState.Lowering ? _lowerDuration : _raiseDuration;
            _transitionElapsed = Mathf.Min(_transitionElapsed + Mathf.Max(0f, deltaTime), duration);
            if (_transitionElapsed < duration)
            {
                return;
            }

            if (CurrentState == AnchorState.Lowering)
            {
                CurrentState = AnchorState.Dropped;
                _transitionElapsed = 0f;
                SetAnchorVisualPose(ResolveAnchorVisualPoseAtPathT(1f, _anchorBottomPoint));
                LogInfo($"Anchor dropped. controls={name}, holdPoint={_anchorHoldPoint}.");
                return;
            }

            CurrentState = AnchorState.Raised;
            _transitionElapsed = 0f;
            DestroyAnchorVisuals();
            LogInfo($"Anchor raised. controls={name}.");
        }

        private void ApplyAnchorHold()
        {
            if (_boatRigidbody == null
                || CurrentState == AnchorState.Raised
                || CurrentState == AnchorState.Raising
                || _maxAnchorAcceleration <= 0f)
            {
                return;
            }

            float strength = CurrentState == AnchorState.Lowering
                ? Mathf.Clamp01(_transitionElapsed / Mathf.Max(0.01f, _lowerDuration))
                : 1f;
            if (strength <= 0f)
            {
                return;
            }

            Vector3 anchorAcceleration = ResolveAnchorAcceleration();
            if (anchorAcceleration.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            _boatRigidbody.AddForce(anchorAcceleration * strength, ForceMode.Acceleration);
        }

        private Vector3 ResolveAnchorAcceleration()
        {
            if (_boatRigidbody == null)
            {
                return Vector3.zero;
            }

            Vector3 planarError = _anchorHoldPoint - _boatRigidbody.position;
            planarError.y = 0f;

            Vector3 springAcceleration = Vector3.zero;
            float planarDistance = planarError.magnitude;
            if (planarDistance > _slackRadius && planarDistance > Mathf.Epsilon)
            {
                springAcceleration = planarError.normalized * ((planarDistance - _slackRadius) * _holdSpringAcceleration);
            }

            Vector3 horizontalVelocity = _boatRigidbody.linearVelocity;
            horizontalVelocity.y = 0f;
            Vector3 dampingAcceleration = -horizontalVelocity / Mathf.Max(0.01f, _horizontalStopTime);

            Vector3 anchorAcceleration = Vector3.ClampMagnitude(
                springAcceleration + dampingAcceleration,
                _maxAnchorAcceleration);
            anchorAcceleration.y = 0f;
            return anchorAcceleration;
        }

        private void EnsureAnchorVisuals()
        {
            if (_visualRoot == null)
            {
                var visualRootObject = new GameObject(RuntimeVisualRootName);
                _visualRoot = visualRootObject.transform;
            }

            if (_anchorInstance == null && _anchorPrefab != null)
            {
                _anchorInstance = Instantiate(_anchorPrefab, _visualRoot);
                _anchorInstance.name = _anchorPrefab.name;
                SanitizeVisualPhysics(_anchorInstance);
                CenterRenderersOnRoot(_anchorInstance);
            }
        }

        private void DestroyAnchorVisuals()
        {
            if (_anchorInstance != null)
            {
                DestroyUnityObject(_anchorInstance);
                _anchorInstance = null;
            }

            for (int i = 0; i < _chainLinkInstances.Count; i++)
            {
                if (_chainLinkInstances[i] != null)
                {
                    DestroyUnityObject(_chainLinkInstances[i]);
                }
            }

            _chainLinkInstances.Clear();

            if (_visualRoot != null)
            {
                DestroyUnityObject(_visualRoot.gameObject);
                _visualRoot = null;
            }
        }

        private void UpdateAnchorVisuals()
        {
            if (CurrentState == AnchorState.Raised)
            {
                return;
            }

            EnsureAnchorVisuals();
            float pathT = ResolveCurrentAnchorPathT();
            AnchorVisualPose anchorPose = ResolveCurrentAnchorVisualPose(pathT);
            SetAnchorVisualPose(anchorPose);

            if (HasUsableAnchorPath())
            {
                UpdatePathChainVisuals(pathT);
            }
            else
            {
                UpdateStraightChainVisuals(GetAnchorRaisedPosition(), anchorPose.Position);
            }
        }

        private AnchorVisualPose ResolveCurrentAnchorVisualPose(float pathT)
        {
            if (HasUsableAnchorPath())
            {
                return ResolveAnchorVisualPoseAtPathT(pathT, GetAnchorRaisedPosition());
            }

            if (CurrentState == AnchorState.Raising)
            {
                float t = Mathf.Clamp01(_transitionElapsed / Mathf.Max(0.01f, _raiseDuration));
                Vector3 fallbackPosition = Vector3.Lerp(_raiseStartPoint, GetAnchorRaisedPosition(), t);
                return new AnchorVisualPose(fallbackPosition, GetAnchorFallbackRotation());
            }

            if (CurrentState == AnchorState.Lowering)
            {
                float t = Mathf.Clamp01(_transitionElapsed / Mathf.Max(0.01f, _lowerDuration));
                Vector3 fallbackPosition = Vector3.Lerp(GetAnchorRaisedPosition(), _anchorBottomPoint, t);
                return new AnchorVisualPose(fallbackPosition, GetAnchorFallbackRotation());
            }

            return new AnchorVisualPose(_anchorBottomPoint, GetAnchorFallbackRotation());
        }

        private float ResolveCurrentAnchorPathT()
        {
            return CurrentState switch
            {
                AnchorState.Lowering => Mathf.Clamp01(_transitionElapsed / Mathf.Max(0.01f, _lowerDuration)),
                AnchorState.Raising => 1f - Mathf.Clamp01(_transitionElapsed / Mathf.Max(0.01f, _raiseDuration)),
                AnchorState.Dropped => 1f,
                _ => 0f
            };
        }

        private void UpdateStraightChainVisuals(Vector3 top, Vector3 bottom)
        {
            if (_chainLinkPrefab == null || _visualRoot == null)
            {
                return;
            }

            float distance = Vector3.Distance(top, bottom);
            int targetLinkCount = distance > Mathf.Epsilon
                ? Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(0.01f, _chainLinkSpacing)), 0, _maxChainLinks)
                : 0;

            EnsureChainLinkCount(targetLinkCount);

            Vector3 direction = bottom - top;

            for (int i = 0; i < _chainLinkInstances.Count; i++)
            {
                GameObject chainLink = _chainLinkInstances[i];
                bool shouldBeActive = i < targetLinkCount;
                if (chainLink == null)
                {
                    continue;
                }

                chainLink.SetActive(shouldBeActive);
                if (!shouldBeActive)
                {
                    continue;
                }

                float t = (i + 0.5f) / targetLinkCount;
                chainLink.transform.SetPositionAndRotation(
                    Vector3.Lerp(top, bottom, t),
                    ResolveChainLinkRotation(direction, i));
            }
        }

        private void UpdatePathChainVisuals(float endPathT)
        {
            if (_chainLinkPrefab == null || _visualRoot == null)
            {
                return;
            }

            endPathT = Mathf.Clamp01(endPathT);
            float distance = EstimateAnchorPathLength(endPathT);
            int targetLinkCount = distance > Mathf.Epsilon
                ? Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(0.01f, _chainLinkSpacing)), 0, _maxChainLinks)
                : 0;

            EnsureChainLinkCount(targetLinkCount);

            for (int i = 0; i < _chainLinkInstances.Count; i++)
            {
                GameObject chainLink = _chainLinkInstances[i];
                bool shouldBeActive = i < targetLinkCount;
                if (chainLink == null)
                {
                    continue;
                }

                chainLink.SetActive(shouldBeActive);
                if (!shouldBeActive)
                {
                    continue;
                }

                float normalizedChainT = (i + 0.5f) / targetLinkCount;
                float pathT = Mathf.Clamp01(normalizedChainT * endPathT);
                AnchorVisualPose linkPose = ResolveAnchorVisualPoseAtPathT(pathT, GetAnchorRaisedPosition());
                chainLink.transform.SetPositionAndRotation(
                    linkPose.Position,
                    ResolveChainLinkRotation(linkPose.Tangent, i));
            }
        }

        private void EnsureChainLinkCount(int targetLinkCount)
        {
            while (_chainLinkInstances.Count < targetLinkCount)
            {
                GameObject chainLink = Instantiate(_chainLinkPrefab, _visualRoot);
                chainLink.name = _chainLinkPrefab.name;
                SanitizeVisualPhysics(chainLink);
                CenterRenderersOnRoot(chainLink);
                _chainLinkInstances.Add(chainLink);
            }
        }

        private void SanitizeVisualPhysics(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody visualRigidbody = rigidbodies[i];
                if (visualRigidbody == null)
                {
                    continue;
                }

                visualRigidbody.linearVelocity = Vector3.zero;
                visualRigidbody.angularVelocity = Vector3.zero;
                visualRigidbody.useGravity = false;
                visualRigidbody.isKinematic = true;
                visualRigidbody.detectCollisions = false;
                DestroyUnityObject(visualRigidbody);
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider visualCollider = colliders[i];
                if (visualCollider == null)
                {
                    continue;
                }

                visualCollider.enabled = false;
                DestroyUnityObject(visualCollider);
            }
        }

        private void CenterRenderersOnRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 delta = root.transform.position - bounds.center;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                root.transform.GetChild(i).position += delta;
            }
        }

        private Vector3 GetDropPointPosition()
        {
            return _dropPoint != null ? _dropPoint.position : transform.position;
        }

        private Vector3 GetAnchorRaisedPosition()
        {
            return TryEvaluateAnchorPath(0f, out AnchorVisualPose pose)
                ? pose.Position
                : GetDropPointPosition();
        }

        private Vector3 GetAnchorLoweredPosition()
        {
            return TryEvaluateAnchorPath(1f, out AnchorVisualPose pose)
                ? pose.Position
                : GetDropPointPosition() - (Vector3.up * _fixedDropDepth);
        }

        private Vector3 GetAnchorVisualPosition()
        {
            return _anchorInstance != null ? _anchorInstance.transform.position : _anchorBottomPoint;
        }

        private void SetAnchorVisualPose(AnchorVisualPose pose)
        {
            if (_anchorInstance != null)
            {
                _anchorInstance.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            }
        }

        private AnchorVisualPose ResolveAnchorVisualPoseAtPathT(float pathT, Vector3 fallbackPosition)
        {
            return TryEvaluateAnchorPath(pathT, out AnchorVisualPose pose)
                ? pose
                : new AnchorVisualPose(fallbackPosition, GetAnchorFallbackRotation());
        }

        private bool TryEvaluateAnchorPath(float pathT, out AnchorVisualPose pose)
        {
            pose = default;
            if (!HasUsableAnchorPath())
            {
                return false;
            }

            pathT = Mathf.Clamp01(pathT);
            if (!_anchorPath.Evaluate(pathT, out var position, out var tangent, out var upVector))
            {
                return false;
            }

            Vector3 worldPosition = new(position.x, position.y, position.z);
            Vector3 worldTangent = new(tangent.x, tangent.y, tangent.z);
            Vector3 worldUp = new(upVector.x, upVector.y, upVector.z);
            Quaternion rotation = GetAnchorFallbackRotation();
            if (_alignAnchorToPath && worldTangent.sqrMagnitude > Mathf.Epsilon)
            {
                rotation = Quaternion.LookRotation(worldTangent.normalized, ResolveSafeUp(worldTangent, worldUp));
            }

            pose = new AnchorVisualPose(worldPosition, rotation, worldTangent);
            return true;
        }

        private bool HasUsableAnchorPath()
        {
            return _anchorPath != null
                && _anchorPath.Splines != null
                && _anchorPath.Splines.Count > 0
                && _anchorPath.Splines[0] != null
                && _anchorPath.Splines[0].Count >= 2;
        }

        private float EstimateAnchorPathLength(float endPathT)
        {
            endPathT = Mathf.Clamp01(endPathT);
            if (endPathT <= Mathf.Epsilon || !TryEvaluateAnchorPath(0f, out AnchorVisualPose previousPose))
            {
                return 0f;
            }

            const int SampleCount = 18;
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(SampleCount * endPathT));
            float length = 0f;
            for (int i = 1; i <= sampleCount; i++)
            {
                float sampleT = endPathT * (i / (float)sampleCount);
                if (!TryEvaluateAnchorPath(sampleT, out AnchorVisualPose currentPose))
                {
                    continue;
                }

                length += Vector3.Distance(previousPose.Position, currentPose.Position);
                previousPose = currentPose;
            }

            return length;
        }

        private Quaternion ResolveChainLinkRotation(Vector3 chainDirection, int linkIndex)
        {
            if (chainDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return Quaternion.identity;
            }

            Vector3 normalizedDirection = chainDirection.normalized;
            Quaternion axisRotation = Quaternion.FromToRotation(Vector3.up, normalizedDirection);
            float twistDegrees = _chainLinkBaseTwistDegrees
                + ((linkIndex & 1) == 0 ? 0f : _chainLinkAlternateTwistDegrees);
            return Quaternion.AngleAxis(twistDegrees, normalizedDirection) * axisRotation;
        }

        private Quaternion GetAnchorFallbackRotation()
        {
            return _anchorInstance != null ? _anchorInstance.transform.rotation : Quaternion.identity;
        }

        private static Vector3 ResolveSafeUp(Vector3 forward, Vector3 up)
        {
            if (up.sqrMagnitude <= Mathf.Epsilon)
            {
                up = Vector3.up;
            }

            if (Vector3.Cross(forward, up).sqrMagnitude <= 0.0001f)
            {
                up = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.95f
                    ? Vector3.forward
                    : Vector3.up;
            }

            return up.normalized;
        }

        private void DrawAnchorPathGizmo()
        {
            if (!TryEvaluateAnchorPath(0f, out AnchorVisualPose previousPose))
            {
                return;
            }

            const int SampleCount = 24;
            for (int i = 1; i <= SampleCount; i++)
            {
                float t = i / (float)SampleCount;
                if (!TryEvaluateAnchorPath(t, out AnchorVisualPose currentPose))
                {
                    continue;
                }

                Gizmos.DrawLine(previousPose.Position, currentPose.Position);
                previousPose = currentPose;
            }
        }

        private bool IsPhysicalAnchorControlCollider(Collider candidateCollider)
        {
            return candidateCollider != null
                && candidateCollider != _interactionTrigger
                && !candidateCollider.isTrigger
                && candidateCollider.transform.IsChildOf(transform);
        }

        private bool IsPhysicalBoatCollider(Collider candidateCollider)
        {
            return candidateCollider != null
                && !candidateCollider.isTrigger
                && !candidateCollider.transform.IsChildOf(transform);
        }

        private static bool TryResolvePlayerInput(Collider other, out PlayerInput playerInput)
        {
            playerInput = other != null ? other.GetComponentInParent<PlayerInput>() : null;
            return playerInput != null;
        }

        private bool IsPlayerOverlappingInteractionTrigger(PlayerInput playerInput)
        {
            if (playerInput == null || _interactionTrigger == null)
            {
                return false;
            }

            Collider[] playerColliders = playerInput.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider != null
                    && playerCollider.enabled
                    && playerCollider.bounds.Intersects(_interactionTrigger.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanPlayerUseAnchor(PlayerInput playerInput)
        {
            return playerInput.currentActionMap != null
                && playerInput.currentActionMap.name == Strings.ThirdPersonControls
                && !HelmControl.TryGetActiveHelm(playerInput.playerIndex, out _)
                && !DeckMountedGunControl.TryGetActiveGun(playerInput.playerIndex, out _)
                && !CargoBayControls.TryGetActiveCargoBay(playerInput.playerIndex, out _);
        }

        private static bool WasAnchorActionPressedThisFrame(PlayerInput playerInput)
        {
            InputActionMap thirdPersonMap = playerInput.actions.FindActionMap(Strings.ThirdPersonControls, throwIfNotFound: false);
            InputAction actionAction = thirdPersonMap?.FindAction(Strings.ActionAction, throwIfNotFound: false);
            return actionAction != null && actionAction.WasPressedThisFrame();
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

        private static void AddPlayerToAnchorRange(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return;
            }

            AnchorRangeCountsByPlayer.TryGetValue(playerInput, out int rangeCount);
            AnchorRangeCountsByPlayer[playerInput] = rangeCount + 1;
        }

        private static void RemovePlayerFromAnchorRange(PlayerInput playerInput)
        {
            if (playerInput == null || !AnchorRangeCountsByPlayer.TryGetValue(playerInput, out int rangeCount))
            {
                return;
            }

            if (rangeCount <= 1)
            {
                AnchorRangeCountsByPlayer.Remove(playerInput);
                return;
            }

            AnchorRangeCountsByPlayer[playerInput] = rangeCount - 1;
        }

        private static void DestroyUnityObject(Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(unityObject);
                return;
            }

            DestroyImmediate(unityObject);
        }

        private static string DescribePlayer(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return "null";
            }

            return $"{playerInput.name}[index={playerInput.playerIndex}, scheme={playerInput.currentControlScheme}]";
        }

        private readonly struct AnchorVisualPose
        {
            public AnchorVisualPose(Vector3 position, Quaternion rotation)
                : this(position, rotation, Vector3.down)
            {
            }

            public AnchorVisualPose(Vector3 position, Quaternion rotation, Vector3 tangent)
            {
                Position = position;
                Rotation = rotation;
                Tangent = tangent;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Tangent { get; }
        }
    }
}
