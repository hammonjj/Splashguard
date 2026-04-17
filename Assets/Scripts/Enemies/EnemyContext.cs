using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public sealed class EnemyContext
    {
        public EnemyContext(
            GameObject enemyRoot,
            EnemyVesselData enemyData,
            EnemyTargetTracker targetTracker,
            IEnemyMovementAgent movementAgent,
            EnemyVesselWeaponController weaponController,
            EnemyHealth health)
        {
            EnemyRoot = enemyRoot;
            EnemyData = enemyData;
            TargetTracker = targetTracker;
            MovementAgent = movementAgent;
            WeaponController = weaponController;
            Health = health;
        }

        public GameObject EnemyRoot { get; }
        public EnemyVesselData EnemyData { get; }
        public EnemyTargetTracker TargetTracker { get; }
        public IEnemyMovementAgent MovementAgent { get; }
        public EnemyVesselMotor Motor => MovementAgent as EnemyVesselMotor;
        public EnemyVesselWeaponController WeaponController { get; }
        public EnemyHealth Health { get; }
    }
}
