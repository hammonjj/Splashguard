using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    public enum ReloadType
    {
        NoReload
    }

    [CreateAssetMenu(fileName = "ReloadDefinition", menuName = "Weapons/Reload")]
    public sealed class ReloadDefinition : ScriptableObject
    {
        [SerializeField] private ReloadType _reloadType = ReloadType.NoReload;

        public ReloadType ReloadType => _reloadType;
        public bool CanReload => _reloadType != ReloadType.NoReload;
    }
}
