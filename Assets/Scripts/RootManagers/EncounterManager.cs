using BitBox.Library;
using BitBox.Library.Constants.Enums;
using BitBox.Toymageddon.SceneManagement;
using Bitbox.Splashguard.Enemies;
using Bitbox.Splashguard.Encounters;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public sealed class EncounterManager : MonoBehaviourBase, IGameplaySceneStartupTask
    {
        private const string TerrainLayerName = "Terrain";
        private const float DefaultTerrainProbeHeight = 3f;
        private const float DefaultTerrainProbeDepth = 6f;
        private const float DefaultTerrainClearance = 0.15f;

        [SerializeField, Required, InlineEditor] private EncounterDefinition _encounterDefinition;
        [SerializeField] private GameObject[] _spawnZones;
        [SerializeField, Min(0)] private int _desiredEnemyCount = 3;
        [SerializeField, Min(1)] private int _waveCount = 1;
        [SerializeField, Min(0f)] private float _waveSpawnDelaySeconds = 1f;
        [SerializeField, Min(1f)] private float _spawnSearchRadius = 60f;
        [SerializeField, Min(0f)] private float _minimumDistanceFromPlayerLoad = 24f;
        [SerializeField, Min(0f)] private float _shorelinePerimeterRadius = 10f;
        [SerializeField, Min(4)] private int _perimeterSampleCount = 12;
        [SerializeField, Min(1)] private int _maxSpawnAttempts = 64;

        private readonly HashSet<GameObject> _trackedEnemyRoots = new();
        private System.Random _random;
        private int _terrainLayerMask;
        private Coroutine _queuedWaveSpawn;
        private int _currentWaveIndex;
        private bool _isSpawningWave;
        private bool _encounterComplete;

        public bool IsEncounterComplete => _encounterComplete;
        public int ActiveTrackedEnemyCount => _trackedEnemyRoots.Count;

        protected override void OnAwakened()
        {
            _terrainLayerMask = LayerMask.GetMask(TerrainLayerName);
            _random = new System.Random(GetInstanceID() ^ System.Environment.TickCount);
        }

        protected override void OnEnabled()
        {
            _globalMessageBus?.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            StopQueuedWaveSpawn();
            _trackedEnemyRoots.Clear();
        }

        public bool ShouldRunForScene(MacroSceneType sceneType)
        {
            return sceneType == MacroSceneType.CombatArena
                && enabled
                && gameObject.activeInHierarchy;
        }

        public IEnumerator ExecuteStartup(GameplaySceneStartupContext context)
        {
            _random ??= new System.Random(GetInstanceID() ^ System.Environment.TickCount);
            if (_terrainLayerMask == 0)
            {
                _terrainLayerMask = LayerMask.GetMask(TerrainLayerName);
            }

            ResetEncounterState();
            context.ReportProgress(0f, "Planning encounter...");
            yield return null;

            yield return SpawnWave(context, 1, reportProgress: true);
        }

        private IEnumerator SpawnWave(GameplaySceneStartupContext context, int waveIndex, bool reportProgress)
        {
            int waveCount = ResolveWaveCount();
            _currentWaveIndex = Mathf.Clamp(waveIndex, 1, waveCount);
            _isSpawningWave = true;
            _trackedEnemyRoots.Clear();

            if (_desiredEnemyCount <= 0)
            {
                LogWarning("Encounter wave skipped because desired enemy count is zero.");
                MarkEncounterComplete("desired_enemy_count_zero");
                ReportStartupProgress(context, reportProgress, 1f, "Encounter ready.");
                _isSpawningWave = false;
                yield break;
            }

            string validationError = string.Empty;
            if (_encounterDefinition == null || !_encounterDefinition.IsValidDefinition(out validationError))
            {
                LogWarning(
                    $"Encounter wave completed without spawning enemies because the definition is invalid. reason={validationError}");
                MarkEncounterComplete("invalid_definition");
                ReportStartupProgress(context, reportProgress, 1f, "Encounter ready.");
                _isSpawningWave = false;
                yield break;
            }

            Vector3 exclusionCenter = ResolveEncounterExclusionCenter();
            int spawnedEnemyCount = 0;
            IReadOnlyList<GameObject> spawnZoneAssignments =
                EncounterSpawnZoneUtility.BuildRoundRobinAssignments(_spawnZones, _desiredEnemyCount);

            if (spawnZoneAssignments.Count > 0)
            {
                for (int assignmentIndex = 0;
                     assignmentIndex < spawnZoneAssignments.Count && spawnedEnemyCount < _desiredEnemyCount;
                     assignmentIndex++)
                {
                    GameObject spawnZone = spawnZoneAssignments[assignmentIndex];
                    if (!TrySpawnEnemyForZone(spawnZone, exclusionCenter, out GameObject spawnedEnemy))
                    {
                        LogWarning(
                            $"Encounter wave {_currentWaveIndex}/{waveCount} failed to spawn an enemy in zone '{spawnZone?.name ?? "null"}' after {_maxSpawnAttempts} attempts.");
                        continue;
                    }

                    TrackSpawnedEnemy(spawnedEnemy);
                    spawnedEnemyCount++;
                    ReportStartupProgress(
                        context,
                        reportProgress,
                        (float)spawnedEnemyCount / _desiredEnemyCount,
                        $"Spawning wave {_currentWaveIndex}/{waveCount} enemies {spawnedEnemyCount}/{_desiredEnemyCount}...");
                    yield return null;
                }
            }
            else
            {
                for (int attempt = 0; attempt < _maxSpawnAttempts && spawnedEnemyCount < _desiredEnemyCount; attempt++)
                {
                    if (!TrySpawnEnemyForArena(exclusionCenter, out GameObject spawnedEnemy))
                    {
                        continue;
                    }

                    TrackSpawnedEnemy(spawnedEnemy);
                    spawnedEnemyCount++;
                    ReportStartupProgress(
                        context,
                        reportProgress,
                        (float)spawnedEnemyCount / _desiredEnemyCount,
                        $"Spawning wave {_currentWaveIndex}/{waveCount} enemies {spawnedEnemyCount}/{_desiredEnemyCount}...");
                    yield return null;
                }
            }

            if (spawnedEnemyCount == 0)
            {
                LogWarning($"Encounter wave {_currentWaveIndex}/{waveCount} found no valid enemy spawn points. Marking encounter complete to avoid a soft lock.");
                MarkEncounterComplete("zero_spawned_enemies");
                ReportStartupProgress(context, reportProgress, 1f, "Encounter ready.");
                _isSpawningWave = false;
                yield break;
            }

            if (spawnedEnemyCount < _desiredEnemyCount)
            {
                LogWarning(
                    $"Encounter wave {_currentWaveIndex}/{waveCount} spawned {spawnedEnemyCount}/{_desiredEnemyCount} enemies after {_maxSpawnAttempts} attempts.");
            }

            _encounterComplete = false;
            LogInfo(
                $"Encounter wave spawned. wave={_currentWaveIndex}/{waveCount}, spawnedEnemies={spawnedEnemyCount}, trackedEnemies={_trackedEnemyRoots.Count}.");
            ReportStartupProgress(
                context,
                reportProgress,
                1f,
                _currentWaveIndex < waveCount ? $"Wave {_currentWaveIndex} ready." : "Encounter ready.");
            _isSpawningWave = false;

            if (_trackedEnemyRoots.Count == 0)
            {
                HandleWaveCleared();
            }
        }

        private void OnEnemyDeath(EnemyDeathEvent @event)
        {
            if (@event?.EnemyRoot == null || !_trackedEnemyRoots.Remove(@event.EnemyRoot))
            {
                return;
            }

            if (_trackedEnemyRoots.Count == 0)
            {
                HandleWaveCleared();
            }
        }

        private void HandleWaveCleared()
        {
            if (_isSpawningWave)
            {
                return;
            }

            int waveCount = ResolveWaveCount();
            if (_currentWaveIndex < waveCount)
            {
                QueueNextWave(_currentWaveIndex + 1, waveCount);
                return;
            }

            MarkEncounterComplete("all_waves_defeated");
        }

        private void QueueNextWave(int nextWaveIndex, int waveCount)
        {
            if (_queuedWaveSpawn != null)
            {
                return;
            }

            LogInfo(
                $"Encounter wave cleared. wave={_currentWaveIndex}/{waveCount}, nextWave={nextWaveIndex}, spawnDelay={_waveSpawnDelaySeconds:0.##}.");
            _queuedWaveSpawn = StartCoroutine(SpawnQueuedWave(nextWaveIndex));
        }

        private IEnumerator SpawnQueuedWave(int waveIndex)
        {
            if (_waveSpawnDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(_waveSpawnDelaySeconds);
            }

            _queuedWaveSpawn = null;
            yield return SpawnWave(default, waveIndex, reportProgress: false);
        }

        private static void ReportStartupProgress(
            GameplaySceneStartupContext context,
            bool shouldReport,
            float progress,
            string progressText)
        {
            if (!shouldReport)
            {
                return;
            }

            context.ReportProgress(progress, progressText);
        }

        private void TrackSpawnedEnemy(GameObject enemyRoot)
        {
            if (enemyRoot == null)
            {
                return;
            }

            _trackedEnemyRoots.Add(enemyRoot);
            _encounterComplete = false;
        }

        private bool TrySpawnEnemyForArena(Vector3 exclusionCenter, out GameObject spawnedEnemy)
        {
            spawnedEnemy = null;

            GameObject enemyPrefab = SelectRandomEnemyPrefab();
            if (enemyPrefab == null)
            {
                return false;
            }

            NavalPointValidationSettings validationSettings = ResolveValidationSettings(enemyPrefab);
            bool foundSpawnPose = CombatEncounterSpawnPlanner.TryChooseSpawnPose(
                transform.position,
                _spawnSearchRadius,
                exclusionCenter,
                _minimumDistanceFromPlayerLoad,
                validationSettings,
                _random,
                out Vector3 spawnPosition,
                out Quaternion spawnRotation);

            if (!foundSpawnPose || EncounterSpawnCollisionValidator.HasBlockingOverlap(enemyPrefab, spawnPosition, spawnRotation))
            {
                return false;
            }

            spawnedEnemy = InstantiateEncounterEnemy(enemyPrefab, spawnPosition, spawnRotation);
            return spawnedEnemy != null;
        }

        private bool TrySpawnEnemyForZone(GameObject spawnZone, Vector3 exclusionCenter, out GameObject spawnedEnemy)
        {
            spawnedEnemy = null;
            Collider[] ignoredZoneColliders = spawnZone != null
                ? spawnZone.GetComponentsInChildren<Collider>(includeInactive: false)
                : Array.Empty<Collider>();

            for (int attempt = 0; attempt < _maxSpawnAttempts; attempt++)
            {
                GameObject enemyPrefab = SelectRandomEnemyPrefab();
                if (enemyPrefab == null)
                {
                    continue;
                }

                NavalPointValidationSettings validationSettings = ResolveValidationSettings(enemyPrefab);
                bool foundSpawnPose = CombatEncounterSpawnPlanner.TryChooseSpawnPose(
                    exclusionCenter,
                    _minimumDistanceFromPlayerLoad,
                    _random,
                    random => EncounterSpawnZoneUtility.SampleCandidatePoint(spawnZone, random),
                    candidate =>
                    {
                        NavalPointValidationResult validationResult =
                            NavalPointValidationUtility.ValidateCandidate(candidate, validationSettings);
                        return new CombatEncounterSpawnCandidateResult(
                            validationResult.IsValid,
                            validationResult.SurfacePoint);
                    },
                    out Vector3 spawnPosition,
                    out Quaternion spawnRotation);

                if (!foundSpawnPose
                    || EncounterSpawnCollisionValidator.HasBlockingOverlap(
                        enemyPrefab,
                        spawnPosition,
                        spawnRotation,
                        ignoredZoneColliders))
                {
                    continue;
                }

                spawnedEnemy = InstantiateEncounterEnemy(enemyPrefab, spawnPosition, spawnRotation);
                if (spawnedEnemy != null)
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject InstantiateEncounterEnemy(GameObject enemyPrefab, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            GameObject spawnedEnemy = UnityEngine.Object.Instantiate(enemyPrefab, spawnPosition, spawnRotation);
            if (spawnedEnemy == null)
            {
                return null;
            }

            if (gameObject.scene.IsValid()
                && gameObject.scene.isLoaded
                && spawnedEnemy.scene.handle != gameObject.scene.handle)
            {
                SceneManager.MoveGameObjectToScene(spawnedEnemy, gameObject.scene);
            }

            return spawnedEnemy;
        }

        private void ResetEncounterState()
        {
            StopQueuedWaveSpawn();
            _trackedEnemyRoots.Clear();
            _currentWaveIndex = 0;
            _isSpawningWave = false;
            _encounterComplete = false;
        }

        private void StopQueuedWaveSpawn()
        {
            if (_queuedWaveSpawn == null)
            {
                return;
            }

            StopCoroutine(_queuedWaveSpawn);
            _queuedWaveSpawn = null;
        }

        private int ResolveWaveCount()
        {
            return Mathf.Max(1, _waveCount);
        }

        private void MarkEncounterComplete(string reason)
        {
            if (_encounterComplete)
            {
                return;
            }

            _encounterComplete = true;
            LogInfo($"Encounter completed. reason={reason}, trackedEnemiesRemaining={_trackedEnemyRoots.Count}.");
        }

        private GameObject SelectRandomEnemyPrefab()
        {
            if (_encounterDefinition?.EnemyPrefabs == null || _encounterDefinition.EnemyPrefabs.Length == 0)
            {
                return null;
            }

            int randomIndex = _random.Next(0, _encounterDefinition.EnemyPrefabs.Length);
            return _encounterDefinition.EnemyPrefabs[randomIndex];
        }

        private NavalPointValidationSettings ResolveValidationSettings(GameObject enemyPrefab)
        {
            EnemyBrain enemyBrain = enemyPrefab != null
                ? enemyPrefab.GetComponentInChildren<EnemyBrain>(includeInactive: true)
                : null;
            EnemyVesselData enemyData = enemyBrain != null ? enemyBrain.EnemyData : null;

            return new NavalPointValidationSettings(
                _terrainLayerMask,
                enemyData != null ? enemyData.TerrainProbeHeight : DefaultTerrainProbeHeight,
                enemyData != null ? enemyData.TerrainProbeDepth : DefaultTerrainProbeDepth,
                enemyData != null ? enemyData.TerrainClearance : DefaultTerrainClearance,
                _shorelinePerimeterRadius,
                _perimeterSampleCount);
        }

        private Vector3 ResolveEncounterExclusionCenter()
        {
            PlayerVesselRoot[] vesselRoots =
                FindObjectsByType<PlayerVesselRoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < vesselRoots.Length; i++)
            {
                PlayerVesselRoot vesselRoot = vesselRoots[i];
                if (vesselRoot != null
                    && vesselRoot.gameObject.activeInHierarchy
                    && vesselRoot.gameObject.scene.IsValid()
                    && vesselRoot.gameObject.scene.isLoaded)
                {
                    return vesselRoot.transform.position;
                }
            }

            if (TryResolvePlayerCentroid(out Vector3 playerCentroid))
            {
                return playerCentroid;
            }

            BoatSpawnPoint[] spawnPoints =
                FindObjectsByType<BoatSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                BoatSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint != null
                    && spawnPoint.gameObject.activeInHierarchy
                    && spawnPoint.gameObject.scene.IsValid()
                    && spawnPoint.gameObject.scene.isLoaded)
                {
                    return spawnPoint.transform.position;
                }
            }

            return transform.position;
        }

        private static bool TryResolvePlayerCentroid(out Vector3 playerCentroid)
        {
            playerCentroid = Vector3.zero;
            int playerCount = 0;

            IReadOnlyList<PlayerInput> playerInputs = StaticData.PlayerInputCoordinator?.PlayerInputs;
            if (playerInputs != null)
            {
                for (int i = 0; i < playerInputs.Count; i++)
                {
                    PlayerInput playerInput = playerInputs[i];
                    if (playerInput == null || !playerInput.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    playerCentroid += playerInput.transform.position;
                    playerCount++;
                }
            }

            if (playerCount == 0)
            {
                PlayerInput[] discoveredPlayers =
                    FindObjectsByType<PlayerInput>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < discoveredPlayers.Length; i++)
                {
                    PlayerInput playerInput = discoveredPlayers[i];
                    if (playerInput == null || !playerInput.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    playerCentroid += playerInput.transform.position;
                    playerCount++;
                }
            }

            if (playerCount <= 0)
            {
                return false;
            }

            playerCentroid /= playerCount;
            return true;
        }

        protected override void OnDrawnGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _spawnSearchRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(ResolveEncounterExclusionCenter(), _minimumDistanceFromPlayerLoad);
        }
    }
}
