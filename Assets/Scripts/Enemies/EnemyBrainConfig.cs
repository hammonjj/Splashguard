using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [CreateAssetMenu(fileName = "EnemyBrainConfig", menuName = "Enemies/Enemy Brain Config")]
    public sealed class EnemyBrainConfig : ScriptableObject
    {
        [SerializeField, Min(0.02f)] private float _reevaluationInterval = 0.15f;
        [SerializeField, Min(0f)] private float _switchHysteresis = 0.05f;

        public float ReevaluationInterval => Mathf.Max(0.02f, _reevaluationInterval);
        public float SwitchHysteresis => Mathf.Max(0f, _switchHysteresis);

        private void OnValidate()
        {
            _reevaluationInterval = Mathf.Max(0.02f, _reevaluationInterval);
            _switchHysteresis = Mathf.Max(0f, _switchHysteresis);
        }
    }
}
