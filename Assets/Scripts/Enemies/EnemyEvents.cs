using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    public sealed class EnemyAlertEvent
    {
        public EnemyAlertEvent(GameObject sourceEnemyRoot, PlayerVesselTarget target, Vector3 sourcePosition, float radius, string reason)
        {
            SourceEnemyRoot = sourceEnemyRoot;
            Target = target;
            SourcePosition = sourcePosition;
            Radius = radius;
            Reason = reason;
        }

        public GameObject SourceEnemyRoot { get; }
        public PlayerVesselTarget Target { get; }
        public Vector3 SourcePosition { get; }
        public float Radius { get; }
        public string Reason { get; }
    }

    public sealed class EnemyDamagedEvent
    {
        public EnemyDamagedEvent(GameObject enemyRoot, float currentHealth, float maxHealth, float damage, int sourcePlayerIndex)
        {
            EnemyRoot = enemyRoot;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            Damage = damage;
            SourcePlayerIndex = sourcePlayerIndex;
        }

        public GameObject EnemyRoot { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public float Damage { get; }
        public int SourcePlayerIndex { get; }
    }

    public sealed class EnemyDeathEvent
    {
        public EnemyDeathEvent(GameObject enemyRoot)
        {
            EnemyRoot = enemyRoot;
        }

        public GameObject EnemyRoot { get; }
    }
}
