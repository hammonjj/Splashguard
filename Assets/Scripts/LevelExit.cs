using BitBox.Library;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LevelExit : MonoBehaviourBase
    {
        private const int OverlapBufferSize = 32;

        private readonly Collider[] _overlapResults = new Collider[OverlapBufferSize];
        private readonly System.Collections.Generic.List<Collider> _triggerVolumes = new();

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

        private bool TryResolveExitSource(Collider other, out string sourceDescription)
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

        private void ConfigureRigidbody()
        {
            _rigidbody ??= GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                LogError($"Level exit '{name}' requires a {nameof(Rigidbody)}.");
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

            if (_triggerVolumes.Count == 0)
            {
                LogWarning($"Level exit '{name}' did not find any enabled trigger colliders in its hierarchy.");
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
                Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, GetAbsoluteScale(boxCollider.transform.lossyScale));
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
            if (_transitionRequested || other == null)
            {
                return false;
            }

            if (!TryResolveExitSource(other, out string sourceDescription))
            {
                return false;
            }

            if (_globalMessageBus == null)
            {
                LogError($"Level exit '{name}' could not publish its scene transition because the global message bus was unavailable.");
                return false;
            }

            _transitionRequested = true;

            LogInfo($"Level exit triggered by {sourceDescription}. Loading {MacroSceneType.CombatArena}.");
            _globalMessageBus.Publish(new LoadMacroSceneEvent(MacroSceneType.CombatArena));
            return true;
        }

        private static Vector3 GetAbsoluteScale(Vector3 scale)
        {
            return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
