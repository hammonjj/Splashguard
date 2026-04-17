using System;
using BitBox.Library;
using BitBox.Library.Eventing.GlobalEvents;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyBrain : MonoBehaviourBase
    {
        [Header("Data")]
        [SerializeField] private EnemyVesselData _enemyData;
        [SerializeField] private EnemyBrainConfig _brainConfig;

        private EnemyActionBase[] _actions = Array.Empty<EnemyActionBase>();
        private EnemyActionBase _currentAction;
        private EnemyTargetTracker _targetTracker;
        private EnemyVesselMotor _motor;
        private EnemyVesselWeaponController _weaponController;
        private EnemyHealth _health;
        private float _nextEvaluationTime;
        private bool _isPaused;

        public EnemyVesselData EnemyData => _enemyData;
        public EnemyBrainConfig BrainConfig => _brainConfig;
        public EnemyActionBase CurrentAction => _currentAction;

        protected override void OnEnabled()
        {
            CacheReferences();
            BindActions();
            _globalMessageBus?.Subscribe<PauseGameEvent>(OnPauseGame);
            EvaluateActions(forceSwitch: true);
        }

        protected override void OnDisabled()
        {
            _currentAction?.Exit();
            _currentAction = null;
            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
        }

        protected override void OnUpdated()
        {
            if (_isPaused || _enemyData == null)
            {
                return;
            }

            if (Time.time >= _nextEvaluationTime)
            {
                EvaluateActions(forceSwitch: false);
                _nextEvaluationTime = Time.time + ResolveReevaluationInterval();
            }

            _currentAction?.Tick(Time.deltaTime);
        }

        public void ForceEvaluateActions()
        {
            EvaluateActions(forceSwitch: true);
        }

        private void CacheReferences()
        {
            GameObject ownerRoot = ResolveOwnerRoot();
            _targetTracker ??= GetComponent<EnemyTargetTracker>() ?? ownerRoot.GetComponentInChildren<EnemyTargetTracker>(includeInactive: true);
            _motor ??= GetComponent<EnemyVesselMotor>() ?? ownerRoot.GetComponentInChildren<EnemyVesselMotor>(includeInactive: true);
            _weaponController ??= GetComponent<EnemyVesselWeaponController>() ?? ownerRoot.GetComponentInChildren<EnemyVesselWeaponController>(includeInactive: true);
            _health ??= GetComponent<EnemyHealth>() ?? ownerRoot.GetComponentInChildren<EnemyHealth>(includeInactive: true);
            _actions = GetComponents<EnemyActionBase>();
        }

        private void BindActions()
        {
            var context = new EnemyContext(ResolveOwnerRoot(), _enemyData, _targetTracker, _motor, _weaponController, _health);
            for (int i = 0; i < _actions.Length; i++)
            {
                _actions[i].BindContext(context);
            }
        }

        private void EvaluateActions(bool forceSwitch)
        {
            EnemyActionBase bestAction = null;
            float bestScore = 0f;
            float currentScore = 0f;

            for (int i = 0; i < _actions.Length; i++)
            {
                EnemyActionBase action = _actions[i];
                if (action == null || !action.enabled)
                {
                    continue;
                }

                float score = Mathf.Max(0f, action.Score());
                if (action == _currentAction)
                {
                    currentScore = score;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = action;
                }
            }

            if (bestAction == null)
            {
                if (_currentAction != null)
                {
                    _currentAction.Exit();
                    _currentAction = null;
                }

                return;
            }

            if (!forceSwitch && bestAction == _currentAction)
            {
                return;
            }

            if (!forceSwitch
                && _currentAction != null
                && !_currentAction.CanBeInterrupted)
            {
                return;
            }

            if (!forceSwitch
                && _currentAction != null
                && bestScore <= currentScore + ResolveSwitchHysteresis())
            {
                return;
            }

            SwitchTo(bestAction);
        }

        private void SwitchTo(EnemyActionBase nextAction)
        {
            if (nextAction == _currentAction)
            {
                return;
            }

            _currentAction?.Exit();
            _currentAction = nextAction;
            _currentAction.Enter();
        }

        private float ResolveReevaluationInterval()
        {
            return _brainConfig != null ? _brainConfig.ReevaluationInterval : 0.15f;
        }

        private float ResolveSwitchHysteresis()
        {
            return _brainConfig != null ? _brainConfig.SwitchHysteresis : 0.05f;
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
        }

        private GameObject ResolveOwnerRoot()
        {
            Rigidbody rootBody = GetComponentInParent<Rigidbody>();
            return rootBody != null ? rootBody.gameObject : gameObject;
        }
    }
}
