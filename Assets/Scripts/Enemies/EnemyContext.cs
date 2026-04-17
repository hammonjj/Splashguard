using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public sealed class EnemyContext
    {
        public EnemyContext(
            GameObject enemyRoot,
            EnemyVesselData enemyData,
            EnemyTargetTracker targetTracker,
            EnemyVesselMotor motor,
            EnemyVesselWeaponController weaponController,
            EnemyHealth health)
        {
            EnemyRoot = enemyRoot;
            EnemyData = enemyData;
            TargetTracker = targetTracker;
            Motor = motor;
            WeaponController = weaponController;
            Health = health;
        }

        public GameObject EnemyRoot { get; }
        public EnemyVesselData EnemyData { get; }
        public EnemyTargetTracker TargetTracker { get; }
        public EnemyVesselMotor Motor { get; }
        public EnemyVesselWeaponController WeaponController { get; }
        public EnemyHealth Health { get; }
    }
}
