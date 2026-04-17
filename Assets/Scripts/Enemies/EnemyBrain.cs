using System;
using BitBox.Library;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Toymageddon.Debugging;
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
        private float _nextDiagnosticTime;
        private bool _isPaused;
        private bool _debugFrozenApplied;

        public EnemyVesselData EnemyData => _enemyData;
        public EnemyBrainConfig BrainConfig => _brainConfig;
        public EnemyActionBase CurrentAction => _currentAction;

        protected override void OnEnabled()
        {
            CacheReferences();
            BindActions();
            _globalMessageBus?.Subscribe<PauseGameEvent>(OnPauseGame);
            LogInfo(
                $"Enemy brain enabled. root={ResolveOwnerRoot().name}, data={_enemyData?.name ?? "None"}, actions={_actions.Length}, targetTracker={DescribeObject(_targetTracker)}, movement={DescribeObject(_motor)}, weapons={DescribeObject(_weaponController)}, health={DescribeObject(_health)}.");
            if (DebugContext.EnemiesFrozen)
            {
                ApplyDebugFrozenMode();
                return;
            }

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
                LogBrainDiagnostic(_enemyData == null ? "Enemy brain has no EnemyVesselData assigned." : "Enemy brain is paused.");
                return;
            }

            if (DebugContext.EnemiesFrozen)
            {
                ApplyDebugFrozenMode();
                return;
            }

            if (_debugFrozenApplied)
            {
                _debugFrozenApplied = false;
                _nextEvaluationTime = 0f;
                LogInfo("Enemy brain resumed from debug frozen mode.");
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
                    LogInfo($"Enemy brain exiting action {_currentAction.GetType().Name}; no action scored above zero.");
                    _currentAction.Exit();
                    _currentAction = null;
                }
                else
                {
                    LogBrainDiagnostic("Enemy brain has no runnable action. Check enabled actions and target tracker state.");
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

        private void ApplyDebugFrozenMode()
        {
            if (_debugFrozenApplied)
            {
                return;
            }

            _currentAction?.Exit();
            _currentAction = null;
            _weaponController?.ClearTarget();
            _targetTracker?.ClearTarget();
            _motor?.Stop("debug_frozen_enemies");
            _debugFrozenApplied = true;
            LogInfo("Enemy brain entered debug frozen mode.");
        }

        private void SwitchTo(EnemyActionBase nextAction)
        {
            if (nextAction == _currentAction)
            {
                return;
            }

            _currentAction?.Exit();
            _currentAction = nextAction;
            LogInfo(
                $"Enemy brain switching action. next={nextAction.GetType().Name}, status={nextAction.DebugStatus}, hasTarget={_targetTracker != null && _targetTracker.HasTarget}, movementState={_motor?.CurrentStatus.State.ToString() ?? "None"}.");
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
            LogInfo($"Enemy brain pause changed. paused={_isPaused}.");
        }

        private GameObject ResolveOwnerRoot()
        {
            Rigidbody rootBody = GetComponentInParent<Rigidbody>();
            return rootBody != null ? rootBody.gameObject : gameObject;
        }

        private void LogBrainDiagnostic(string message)
        {
            float interval = ResolveReevaluationInterval() * 10f;
            float now = Time.time;
            if (now < _nextDiagnosticTime)
            {
                return;
            }

            _nextDiagnosticTime = now + Mathf.Max(1f, interval);
            LogWarning(
                $"{message} currentAction={_currentAction?.GetType().Name ?? "None"}, actions={_actions.Length}, targetTracker={DescribeObject(_targetTracker)}, movement={DescribeObject(_motor)}.");
        }

        private static string DescribeObject(UnityEngine.Object value)
        {
            return value != null ? value.name : "None";
        }
    }
}
