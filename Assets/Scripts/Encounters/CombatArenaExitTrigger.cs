using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox.Splashguard.Encounters
{
    public enum CombatArenaExitActionType
    {
        ReloadCurrentScene,
        LoadMacroScene
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CombatArenaExitTrigger : MonoBehaviourBase
    {
        private const int OverlapBufferSize = 32;

        [SerializeField] private CombatArenaExitActionType _actionType = CombatArenaExitActionType.ReloadCurrentScene;
        [SerializeField] private MacroSceneType _targetScene = MacroSceneType.HubWorld;
        [SerializeField] private bool _requireEncounterComplete = true;
        [SerializeField] private EncounterManager _encounterManager;

        private readonly Collider[] _overlapResults = new Collider[OverlapBufferSize];
        private readonly List<Collider> _triggerVolumes = new();

        private Rigidbody _rigidbody;
        private bool _transitionRequested;

        protected override void OnEnabled()
        {
            _transitionRequested = false;
            ConfigureRigidbody();
            CacheTriggerVolumes();
        }

        protected override void OnUpdated()
        {
            if (_transitionRequested)
            {
                return;
            }

            PollTriggerVolumesForOverlap();
        }

        protected override void OnTriggerEntered(Collider other)
        {
            TryHandleExitTrigger(other);
        }

        private void ConfigureRigidbody()
        {
            _rigidbody ??= GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                LogError($"{nameof(CombatArenaExitTrigger)} requires a {nameof(Rigidbody)}.");
                enabled = false;
                return;
            }

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        private void CacheTriggerVolumes()
        {
            _triggerVolumes.Clear();
            Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.isTrigger)
                {
                    continue;
                }

                _triggerVolumes.Add(collider);
            }
        }

        private void PollTriggerVolumesForOverlap()
        {
            for (int i = 0; i < _triggerVolumes.Count; i++)
            {
                Collider triggerVolume = _triggerVolumes[i];
                if (triggerVolume == null || !triggerVolume.enabled || !triggerVolume.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int overlapCount = QueryTriggerOverlaps(triggerVolume);
                for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
                {
                    Collider other = _overlapResults[overlapIndex];
                    if (other == null || other == triggerVolume)
                    {
                        continue;
                    }

                    if (TryHandleExitTrigger(other))
                    {
                        return;
                    }
                }
            }
        }

        private int QueryTriggerOverlaps(Collider triggerVolume)
        {
            if (triggerVolume is BoxCollider boxCollider)
            {
                Vector3 halfExtents = Vector3.Scale(
                    boxCollider.size * 0.5f,
                    GetAbsoluteScale(boxCollider.transform.lossyScale));
                Vector3 center = boxCollider.transform.TransformPoint(boxCollider.center);
                return Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    _overlapResults,
                    boxCollider.transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Collide);
            }

            Bounds bounds = triggerVolume.bounds;
            return Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                _overlapResults,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);
        }

        private bool TryHandleExitTrigger(Collider other)
        {
            if (_transitionRequested || other == null || !TryResolveExitSource(other, out string sourceDescription))
            {
                return false;
            }

            if (_requireEncounterComplete && !IsEncounterComplete())
            {
                return false;
            }

            if (_globalMessageBus == null)
            {
                LogError($"{nameof(CombatArenaExitTrigger)} could not publish a scene transition because the global message bus was unavailable.");
                return false;
            }

            _transitionRequested = true;
            switch (_actionType)
            {
                case CombatArenaExitActionType.ReloadCurrentScene:
                    LogInfo($"Combat arena exit triggered by {sourceDescription}. Reloading current macro scene.");
                    _globalMessageBus.Publish(new ReloadCurrentSceneEvent());
                    return true;

                case CombatArenaExitActionType.LoadMacroScene:
                    LogInfo($"Combat arena exit triggered by {sourceDescription}. Loading {_targetScene}.");
                    _globalMessageBus.Publish(new LoadMacroSceneEvent(_targetScene));
                    return true;

                default:
                    LogError($"Unsupported combat arena exit action '{_actionType}'.");
                    _transitionRequested = false;
                    return false;
            }
        }

        private bool IsEncounterComplete()
        {
            _encounterManager ??= ResolveEncounterManager();
            return _encounterManager != null && _encounterManager.IsEncounterComplete;
        }

        private static EncounterManager ResolveEncounterManager()
        {
            EncounterManager[] managers =
                FindObjectsByType<EncounterManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                EncounterManager manager = managers[i];
                if (manager != null
                    && manager.gameObject.activeInHierarchy
                    && manager.gameObject.scene.IsValid()
                    && manager.gameObject.scene.isLoaded)
                {
                    return manager;
                }
            }

            return null;
        }

        private static bool TryResolveExitSource(Collider other, out string sourceDescription)
        {
            sourceDescription = string.Empty;
            if (other == null)
            {
                return false;
            }

            PlayerInput playerInput = other.GetComponentInParent<PlayerInput>();
            if (playerInput != null)
            {
                sourceDescription = $"player:{playerInput.playerIndex}:{playerInput.name}";
                return true;
            }

            PlayerVesselRoot vesselRoot = other.GetComponentInParent<PlayerVesselRoot>();
            if (vesselRoot != null)
            {
                sourceDescription = $"player_vessel:{vesselRoot.name}";
                return true;
            }

            return false;
        }

        private static Vector3 GetAbsoluteScale(Vector3 scale)
        {
            return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
