using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [CreateAssetMenu(fileName = "WeaponHeatDefinition", menuName = "Weapons/Heat")]
    public sealed class WeaponHeatDefinition : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _maxHeat = 100f;
        [SerializeField, Min(0f)] private float _heatPerShot = 1.8f;
        [SerializeField, Min(0f)] private float _coolRatePerSecond = 15f;
        [SerializeField, Min(0f)] private float _overheatedCoolRatePerSecond = 25f;
        [SerializeField, Min(0f)] private float _recoverHeat = 0f;

        public float MaxHeat => Mathf.Max(0.01f, _maxHeat);
        public float HeatPerShot => Mathf.Max(0f, _heatPerShot);
        public float CoolRatePerSecond => Mathf.Max(0f, _coolRatePerSecond);
        public float OverheatedCoolRatePerSecond => Mathf.Max(0f, _overheatedCoolRatePerSecond);
        public float RecoverHeat => Mathf.Clamp(_recoverHeat, 0f, MaxHeat);

        private void OnValidate()
        {
            _maxHeat = Mathf.Max(0.01f, _maxHeat);
            _heatPerShot = Mathf.Max(0f, _heatPerShot);
            _coolRatePerSecond = Mathf.Max(0f, _coolRatePerSecond);
            _overheatedCoolRatePerSecond = Mathf.Max(0f, _overheatedCoolRatePerSecond);
            _recoverHeat = Mathf.Clamp(_recoverHeat, 0f, _maxHeat);
        }
    }

    public static class WeaponHeatUtility
    {
        public static float AddShotHeat(WeaponHeatDefinition heatDefinition, float currentHeat)
        {
            if (heatDefinition == null)
            {
                return 0f;
            }

            return Mathf.Clamp(currentHeat + heatDefinition.HeatPerShot, 0f, heatDefinition.MaxHeat);
        }

        public static float Cool(
            WeaponHeatDefinition heatDefinition,
            float currentHeat,
            bool isOverheated,
            float deltaTime)
        {
            if (heatDefinition == null)
            {
                return 0f;
            }

            float rate = isOverheated
                ? heatDefinition.OverheatedCoolRatePerSecond
                : heatDefinition.CoolRatePerSecond;
            return Mathf.Clamp(currentHeat - (Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime)), 0f, heatDefinition.MaxHeat);
        }

        public static bool IsAtOverheatThreshold(WeaponHeatDefinition heatDefinition, float currentHeat)
        {
            return heatDefinition != null && currentHeat >= heatDefinition.MaxHeat - 0.0001f;
        }
    }
}
