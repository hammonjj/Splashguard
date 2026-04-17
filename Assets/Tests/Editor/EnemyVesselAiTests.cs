#if UNITY_EDITOR
using BitBox.Library.Eventing.WeaponEvents;
using BitBox.Toymageddon.Weapons;
using Bitbox;
using Bitbox.Splashguard.Enemies;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using StormBreakers;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class EnemyVesselAiTests
    {
        private const string EnemyVesselPrefabPath = "Assets/Prefabs/Enemies/EnemyVessel.prefab";
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel.prefab";
        private const string EnemyVesselDataPath = "Assets/Data/Enemies/EnemyVesselData.asset";
        private const string EnemyBrainConfigPath = "Assets/Data/Enemies/EnemyBrainConfig.asset";
        private const string GatlingBulletAmmoPath = "Assets/Data/Weapons/GatlingBulletAmmo.asset";

        [Test]
        public void PatrolPlanner_IsDeterministicAndStaysInsideRadius()
        {
            Vector3 anchor = new(10f, 0f, -4f);
            Vector3 currentPosition = anchor + Vector3.forward * 2f;

            bool firstFound = EnemyPatrolPlanner.TryChooseWaypoint(
                anchor,
                currentPosition,
                patrolRadius: 40f,
                minimumDistance: 6f,
                seed: 1234,
                sequence: 5,
                candidateAttempts: 12,
                isCandidateValid: _ => true,
                out Vector3 firstWaypoint);
            bool secondFound = EnemyPatrolPlanner.TryChooseWaypoint(
                anchor,
                currentPosition,
                patrolRadius: 40f,
                minimumDistance: 6f,
                seed: 1234,
                sequence: 5,
                candidateAttempts: 12,
                isCandidateValid: _ => true,
                out Vector3 secondWaypoint);

            NUnitAssert.IsTrue(firstFound);
            NUnitAssert.IsTrue(secondFound);
            NUnitAssert.LessOrEqual(Vector3.Distance(anchor, firstWaypoint), 40f);
            NUnitAssert.GreaterOrEqual(Vector3.Distance(currentPosition, firstWaypoint), 6f);
            NUnitAssert.AreEqual(firstWaypoint.x, secondWaypoint.x, 0.001f);
            NUnitAssert.AreEqual(firstWaypoint.z, secondWaypoint.z, 0.001f);
        }

        [Test]
        public void EngagementPlanner_ChoosesChaseOrbitAndRetreatBands()
        {
            Vector3 target = Vector3.zero;

            EnemyEngagementDecision chase = EnemyEngagementPlanner.ResolveDestination(
                new Vector3(100f, 0f, 0f),
                Vector3.forward,
                target,
                attackRange: 70f,
                idealStandoffDistance: 35f,
                retreatDistance: 22f,
                orbitStepDegrees: 35f,
                orbitDirection: 1);
            EnemyEngagementDecision orbit = EnemyEngagementPlanner.ResolveDestination(
                new Vector3(40f, 0f, 0f),
                Vector3.forward,
                target,
                attackRange: 70f,
                idealStandoffDistance: 35f,
                retreatDistance: 22f,
                orbitStepDegrees: 35f,
                orbitDirection: 1);
            EnemyEngagementDecision retreat = EnemyEngagementPlanner.ResolveDestination(
                new Vector3(10f, 0f, 0f),
                Vector3.forward,
                target,
                attackRange: 70f,
                idealStandoffDistance: 35f,
                retreatDistance: 22f,
                orbitStepDegrees: 35f,
                orbitDirection: 1);

            NUnitAssert.AreEqual(EnemyEngagementMode.Chase, chase.Mode);
            NUnitAssert.AreEqual(EnemyEngagementMode.Orbit, orbit.Mode);
            NUnitAssert.AreEqual(EnemyEngagementMode.Retreat, retreat.Mode);
            NUnitAssert.AreEqual(35f, Vector3.Distance(target, chase.Destination), 0.001f);
            NUnitAssert.AreEqual(35f, Vector3.Distance(target, retreat.Destination), 0.001f);
        }

        [Test]
        public void WeaponArc_ChecksSideCoverage()
        {
            Vector3 origin = Vector3.zero;
            Vector3 starboardForward = Vector3.right;

            NUnitAssert.IsTrue(EnemyWeaponMath.IsTargetInsideArc(origin, starboardForward, Vector3.right * 10f, 90f));
            NUnitAssert.IsTrue(EnemyWeaponMath.IsTargetInsideArc(origin, starboardForward, Vector3.forward * 10f, 90f));
            NUnitAssert.IsFalse(EnemyWeaponMath.IsTargetInsideArc(origin, starboardForward, Vector3.left * 10f, 90f));
        }

        [Test]
        public void BurstScheduler_FiresBurstThenWaitsForCooldown()
        {
            var scheduler = new EnemyBurstScheduler(shotsPerBurst: 3, secondsPerShot: 0.1f, cooldownSeconds: 1f);

            NUnitAssert.IsTrue(scheduler.TryConsumeShot(0f));
            NUnitAssert.IsFalse(scheduler.TryConsumeShot(0.05f));
            NUnitAssert.IsTrue(scheduler.TryConsumeShot(0.1f));
            NUnitAssert.IsTrue(scheduler.TryConsumeShot(0.2f));
            NUnitAssert.IsFalse(scheduler.TryConsumeShot(1.19f));
            NUnitAssert.IsTrue(scheduler.TryConsumeShot(1.2f));
        }

        [Test]
        public void AlertUtility_OnlyAcceptsEnemiesInsideRadius()
        {
            Vector3 source = Vector3.zero;

            NUnitAssert.IsTrue(EnemyAlertUtility.IsWithinAlertRadius(new Vector3(5f, 0f, 0f), source, 5f));
            NUnitAssert.IsFalse(EnemyAlertUtility.IsWithinAlertRadius(new Vector3(5.1f, 0f, 0f), source, 5f));
        }

        [Test]
        public void SteeringUtility_ReturnsBoundedSteeringAndSlowTurnThrottle()
        {
            float steering = EnemyVesselSteeringUtility.ComputeSteeringInput(
                Vector3.forward,
                Vector3.right,
                fullSteerAngleDegrees: 70f);
            float throttle = EnemyVesselSteeringUtility.ComputeThrottleInput(
                Vector3.forward,
                Vector3.right,
                slowTurnAngleDegrees: 45f,
                slowTurnThrottle: 0.45f);

            NUnitAssert.AreEqual(1f, steering, 0.001f);
            NUnitAssert.AreEqual(0.45f, throttle, 0.001f);
        }

        [Test]
        public void EnemyHealth_AppliesPlayerProjectileDamageAndIgnoresEnemyProjectiles()
        {
            EnemyVesselData data = AssetDatabase.LoadAssetAtPath<EnemyVesselData>(EnemyVesselDataPath);
            AmmoDefinition ammo = AssetDatabase.LoadAssetAtPath<AmmoDefinition>(GatlingBulletAmmoPath);
            NUnitAssert.IsNotNull(data);
            NUnitAssert.IsNotNull(ammo);

            GameObject enemy = new("EnemyHealthTestRoot");
            try
            {
                enemy.SetActive(false);
                EnemyHealth health = enemy.AddComponent<EnemyHealth>();
                AssignObject(health, "_enemyData", data);

                GameObject hitChild = new("HitChild");
                hitChild.transform.SetParent(enemy.transform);
                hitChild.AddComponent<BoxCollider>();

                enemy.SetActive(true);
                health.ResetHealth();

                var playerImpact = new ProjectileImpactEvent(
                    playerIndex: 0,
                    weapon: null,
                    ammo,
                    ammo.Projectile,
                    projectileInstance: null,
                    hitChild,
                    Vector3.zero,
                    Vector3.up);
                var enemyImpact = new ProjectileImpactEvent(
                    playerIndex: -1,
                    weapon: null,
                    ammo,
                    ammo.Projectile,
                    projectileInstance: null,
                    hitChild,
                    Vector3.zero,
                    Vector3.up);

                NUnitAssert.IsTrue(health.TryApplyProjectileImpact(playerImpact));
                NUnitAssert.AreEqual(data.MaxHealth - ammo.Damage, health.CurrentHealth, 0.001f);
                NUnitAssert.IsFalse(health.TryApplyProjectileImpact(enemyImpact));
                NUnitAssert.AreEqual(data.MaxHealth - ammo.Damage, health.CurrentHealth, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void EnemyHealth_DeathStateIsAppliedOnce()
        {
            EnemyVesselData data = AssetDatabase.LoadAssetAtPath<EnemyVesselData>(EnemyVesselDataPath);
            NUnitAssert.IsNotNull(data);

            GameObject enemy = new("EnemyDeathTestRoot");
            try
            {
                enemy.SetActive(false);
                EnemyHealth health = enemy.AddComponent<EnemyHealth>();
                AssignObject(health, "_enemyData", data);
                enemy.SetActive(true);
                health.ResetHealth();

                health.ApplyDamage(data.MaxHealth + 10f, sourcePlayerIndex: 0, sourceTarget: null, reason: "test");
                NUnitAssert.IsTrue(health.IsDead);
                NUnitAssert.AreEqual(0f, health.CurrentHealth, 0.001f);

                health.ApplyDamage(10f, sourcePlayerIndex: 0, sourceTarget: null, reason: "test");
                NUnitAssert.AreEqual(0f, health.CurrentHealth, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void EnemyVesselPrefab_IsConfiguredForNavalAi()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyVesselPrefabPath);
            NUnitAssert.IsNotNull(prefab, $"Expected EnemyVessel prefab at {EnemyVesselPrefabPath}.");

            Transform brainObject = prefab.transform.Find("EnemyBrain");
            Transform healthObject = prefab.transform.Find("EnemyHealth");
            NUnitAssert.IsNotNull(brainObject, "Enemy brain components should live on an EnemyBrain child object.");
            NUnitAssert.IsNotNull(healthObject, "EnemyHealth should live on its own child object.");

            NUnitAssert.IsNull(prefab.GetComponent<EnemyBrain>(), "EnemyBrain should not be on the vessel root.");
            NUnitAssert.IsNull(prefab.GetComponent<EnemyHealth>(), "EnemyHealth should not be on the vessel root.");
            NUnitAssert.IsNotNull(brainObject.GetComponent<EnemyBrain>());
            NUnitAssert.IsNotNull(brainObject.GetComponent<EnemyTargetTracker>());
            NUnitAssert.IsNotNull(brainObject.GetComponent<EnemyVesselMotor>());
            NUnitAssert.IsNotNull(brainObject.GetComponent<EnemyVesselWeaponController>());
            NUnitAssert.IsNotNull(brainObject.GetComponent<EnemyDestroyOnDeath>());
            NUnitAssert.IsNotNull(brainObject.GetComponent<EnemyPatrolAction>());
            NUnitAssert.IsNotNull(brainObject.GetComponent<EnemyEngageAction>());
            NUnitAssert.IsNotNull(healthObject.GetComponent<EnemyHealth>());
            Rigidbody rootRigidbody = prefab.GetComponent<Rigidbody>();
            Rigidbody[] rigidbodies = prefab.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            NUnitAssert.IsNotNull(rootRigidbody, "EnemyVessel root should own the Rigidbody.");
            NUnitAssert.AreEqual(1, rigidbodies.Length, "EnemyVessel should have exactly one Rigidbody in the full hierarchy.");
            NUnitAssert.AreSame(rootRigidbody, rigidbodies[0], "EnemyBrain and child objects should not own a second Rigidbody.");
            NUnitAssert.AreEqual(1, prefab.GetComponentsInChildren<WaterInteraction>(includeInactive: true).Length);

            EnemyProjectileWeaponMount[] mounts = prefab.GetComponentsInChildren<EnemyProjectileWeaponMount>(includeInactive: true);
            NUnitAssert.AreEqual(2, mounts.Length, "EnemyVessel should have exactly two AI projectile mounts.");
            for (int i = 0; i < mounts.Length; i++)
            {
                NUnitAssert.AreEqual(90f, mounts[i].ArcHalfAngleDegrees, 0.001f);
                NUnitAssert.IsNotNull(mounts[i].WeaponDefinition);
                NUnitAssert.IsNotNull(mounts[i].FirePoint);
            }

            float sideMountDot = Vector3.Dot(mounts[0].transform.forward.normalized, mounts[1].transform.forward.normalized);
            NUnitAssert.Less(sideMountDot, -0.5f, "Enemy side mounts should face generally opposite sides.");
        }

        [Test]
        public void EnemyVesselPrefab_DisablesPlayerOnlyTurretControls()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyVesselPrefabPath);
            NUnitAssert.IsNotNull(prefab);

            DeckMountedGunControl[] gunControls = prefab.GetComponentsInChildren<DeckMountedGunControl>(includeInactive: true);
            PlayerWeaponController[] weaponControllers = prefab.GetComponentsInChildren<PlayerWeaponController>(includeInactive: true);
            NUnitAssert.AreEqual(2, gunControls.Length);
            NUnitAssert.AreEqual(2, weaponControllers.Length);

            for (int i = 0; i < gunControls.Length; i++)
            {
                NUnitAssert.IsFalse(gunControls[i].enabled, "Enemy turrets should not be player-mountable.");
            }

            for (int i = 0; i < weaponControllers.Length; i++)
            {
                NUnitAssert.IsFalse(weaponControllers[i].enabled, "Enemy turrets should not use player weapon input.");
            }
        }

        [Test]
        public void PlayerVesselPrefab_HasTargetMarker()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);
            NUnitAssert.IsNotNull(prefab, $"Expected PlayerVessel prefab at {PlayerVesselPrefabPath}.");

            PlayerVesselTarget target = prefab.GetComponent<PlayerVesselTarget>();
            NUnitAssert.IsNotNull(target, "PlayerVessel needs a target marker for enemy acquisition.");

            var serializedTarget = new SerializedObject(target);
            NUnitAssert.IsNotNull(serializedTarget.FindProperty("_rootTransform").objectReferenceValue);
            NUnitAssert.IsNotNull(serializedTarget.FindProperty("_aimTransform").objectReferenceValue);
            NUnitAssert.IsTrue(serializedTarget.FindProperty("_useColliderBoundsAimPoint").boolValue);
            NUnitAssert.AreEqual(0.45f, serializedTarget.FindProperty("_colliderAimHeightNormalized").floatValue, 0.001f);
            NUnitAssert.AreEqual(new Vector3(0f, 0.45f, 0f), serializedTarget.FindProperty("_aimLocalOffset").vector3Value);
        }

        [Test]
        public void PlayerVesselTarget_UsesColliderBoundsAimPoint()
        {
            GameObject targetRoot = new("TargetRoot");
            try
            {
                BoxCollider collider = targetRoot.AddComponent<BoxCollider>();
                collider.center = new Vector3(2f, 1f, -3f);
                collider.size = new Vector3(4f, 2f, 6f);
                PlayerVesselTarget target = targetRoot.AddComponent<PlayerVesselTarget>();

                Vector3 aimPoint = target.AimPoint;

                NUnitAssert.AreEqual(2f, aimPoint.x, 0.001f);
                NUnitAssert.AreEqual(0.9f, aimPoint.y, 0.001f);
                NUnitAssert.AreEqual(-3f, aimPoint.z, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void EnemyDataAssets_HaveSaneDefaults()
        {
            EnemyVesselData vesselData = AssetDatabase.LoadAssetAtPath<EnemyVesselData>(EnemyVesselDataPath);
            EnemyBrainConfig brainConfig = AssetDatabase.LoadAssetAtPath<EnemyBrainConfig>(EnemyBrainConfigPath);

            NUnitAssert.IsNotNull(vesselData);
            NUnitAssert.IsNotNull(brainConfig);
            NUnitAssert.Greater(vesselData.MaxHealth, 0f);
            NUnitAssert.AreEqual(85f, vesselData.DetectionRange, 0.001f);
            NUnitAssert.AreEqual(120f, vesselData.AlertRadius, 0.001f);
            NUnitAssert.AreEqual(40f, vesselData.PatrolRadius, 0.001f);
            NUnitAssert.AreEqual(70f, vesselData.AttackRange, 0.001f);
            NUnitAssert.AreEqual(35f, vesselData.IdealStandoffDistance, 0.001f);
            NUnitAssert.AreEqual(22f, vesselData.RetreatDistance, 0.001f);
            NUnitAssert.AreEqual(4.5f, vesselData.EngagementWaypointAcceptanceRadius, 0.001f);
            NUnitAssert.AreEqual(65f, vesselData.OrbitStepDegrees, 0.001f);
            NUnitAssert.AreEqual(10.5f, vesselData.MaxForwardSpeed, 0.001f);
            NUnitAssert.AreEqual(0.65f, vesselData.SlowTurnThrottle, 0.001f);
            NUnitAssert.AreEqual(5, vesselData.BurstShots);
            NUnitAssert.AreEqual(1.25f, vesselData.BurstCooldownSeconds, 0.001f);
            NUnitAssert.Greater(brainConfig.ReevaluationInterval, 0f);
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            NUnitAssert.IsNotNull(property, $"Expected serialized property {propertyName} on {target}.");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
