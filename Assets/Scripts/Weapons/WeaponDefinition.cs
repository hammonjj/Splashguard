using BitBox.Toymageddon.Debugging;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Weapons/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private DebugWeaponType _weaponType = DebugWeaponType.GatlingGun;
        [SerializeField] private string _displayName = "Gatling Gun";
        [SerializeField, Required, InlineEditor] private AutomaticFireModeDefinition _fireMode;
        [SerializeField, Required, InlineEditor] private MagazineDefinition _magazine;
        [SerializeField, Required, InlineEditor] private ReloadDefinition _reload;
        [SerializeField, Required, InlineEditor] private AmmoDefinition _ammo;
        [SerializeField, InlineEditor] private WeaponHeatDefinition _heat;

        public DebugWeaponType WeaponType => _weaponType;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _weaponType.ToString() : _displayName;
        public AutomaticFireModeDefinition FireMode => _fireMode;
        public MagazineDefinition Magazine => _magazine;
        public ReloadDefinition Reload => _reload;
        public AmmoDefinition Ammo => _ammo;
        public WeaponHeatDefinition Heat => _heat;
    }
}
