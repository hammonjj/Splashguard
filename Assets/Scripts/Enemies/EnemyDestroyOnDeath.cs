using BitBox.Library;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyDestroyOnDeath : MonoBehaviourBase
    {
        [SerializeField] private GameObject _lifecycleRoot;
        [SerializeField, Min(0f)] private float _destroyDelaySeconds = 0f;

        protected override void OnEnabled()
        {
            _lifecycleRoot ??= ResolveLifecycleRoot();
            _globalMessageBus?.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        private void OnEnemyDeath(EnemyDeathEvent @event)
        {
            if (@event == null || @event.EnemyRoot != ResolveLifecycleRoot())
            {
                return;
            }

            DisableRuntimeComponents();
            if (Application.isPlaying)
            {
                Destroy(_lifecycleRoot, _destroyDelaySeconds);
            }
            else
            {
                DestroyImmediate(_lifecycleRoot);
            }
        }

        private void DisableRuntimeComponents()
        {
            GameObject lifecycleRoot = ResolveLifecycleRoot();
            var brain = lifecycleRoot.GetComponentInChildren<EnemyBrain>(includeInactive: true);
            if (brain != null)
            {
                brain.enabled = false;
            }

            var motor = lifecycleRoot.GetComponentInChildren<EnemyVesselMotor>(includeInactive: true);
            if (motor != null)
            {
                motor.Stop();
                motor.enabled = false;
            }

            var weapons = lifecycleRoot.GetComponentInChildren<EnemyVesselWeaponController>(includeInactive: true);
            if (weapons != null)
            {
                weapons.ClearTarget();
                weapons.enabled = false;
            }
        }

        private GameObject ResolveLifecycleRoot()
        {
            if (_lifecycleRoot != null)
            {
                return _lifecycleRoot;
            }

            Rigidbody rootBody = GetComponentInParent<Rigidbody>();
            return rootBody != null ? rootBody.gameObject : gameObject;
        }
    }
}
