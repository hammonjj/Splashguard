using Sirenix.OdinInspector;
using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [CreateAssetMenu(fileName = "AmmoDefinition", menuName = "Weapons/Ammo")]
    public sealed class AmmoDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private int _damage = 5;
        [SerializeField, Required, InlineEditor] private ProjectileDefinition _projectile;

        public int Damage => Mathf.Max(0, _damage);
        public ProjectileDefinition Projectile => _projectile;

        private void OnValidate()
        {
            _damage = Mathf.Max(0, _damage);
        }
    }
}
