using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [CreateAssetMenu(fileName = "AutomaticFireModeDefinition", menuName = "Weapons/Fire Modes/Automatic")]
    public sealed class AutomaticFireModeDefinition : ScriptableObject
    {
        private const float MinimumRoundsPerSecond = 0.01f;

        [SerializeField, Min(MinimumRoundsPerSecond)] private float _roundsPerSecond = 12f;
        [SerializeField, Min(0f)] private float _spinUpSeconds = 0.35f;
        [SerializeField, Min(1)] private int _maxCatchUpShotsPerFrame = 2;

        public float RoundsPerSecond => Mathf.Max(MinimumRoundsPerSecond, _roundsPerSecond);
        public float SpinUpSeconds => Mathf.Max(0f, _spinUpSeconds);
        public int MaxCatchUpShotsPerFrame => Mathf.Max(1, _maxCatchUpShotsPerFrame);
        public float SecondsPerShot => 1f / RoundsPerSecond;

        private void OnValidate()
        {
            _roundsPerSecond = Mathf.Max(MinimumRoundsPerSecond, _roundsPerSecond);
            _spinUpSeconds = Mathf.Max(0f, _spinUpSeconds);
            _maxCatchUpShotsPerFrame = Mathf.Max(1, _maxCatchUpShotsPerFrame);
        }
    }
}
