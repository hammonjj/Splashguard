using Sirenix.OdinInspector;
using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "Weapons/Projectile")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        [SerializeField, Required] private PhysicalProjectile _projectilePrefab;
        [SerializeField, Min(0.01f)] private float _speed = 80f;
        [SerializeField, Min(0.01f)] private float _lifetimeSeconds = 3f;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField, Min(0)] private int _prewarmCount = 32;
        [SerializeField, Min(1)] private int _maxPoolSize = 256;

        public PhysicalProjectile ProjectilePrefab => _projectilePrefab;
        public float Speed => Mathf.Max(0.01f, _speed);
        public float LifetimeSeconds => Mathf.Max(0.01f, _lifetimeSeconds);
        public LayerMask CollisionMask => _collisionMask;
        public int PrewarmCount => Mathf.Max(0, _prewarmCount);
        public int MaxPoolSize => Mathf.Max(1, _maxPoolSize);

        private void OnValidate()
        {
            _speed = Mathf.Max(0.01f, _speed);
            _lifetimeSeconds = Mathf.Max(0.01f, _lifetimeSeconds);
            _prewarmCount = Mathf.Max(0, _prewarmCount);
            _maxPoolSize = Mathf.Max(1, _maxPoolSize);
        }
    }
}
