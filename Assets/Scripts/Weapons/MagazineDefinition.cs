using UnityEngine;

namespace BitBox.Toymageddon.Weapons
{
    [CreateAssetMenu(fileName = "MagazineDefinition", menuName = "Weapons/Magazine")]
    public sealed class MagazineDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int _clipCapacity = 250;
        [SerializeField, Min(1)] private int _ammoConsumedPerShot = 1;
        [SerializeField] private bool _startsFull = true;

        public int ClipCapacity => Mathf.Max(1, _clipCapacity);
        public int AmmoConsumedPerShot => Mathf.Max(1, _ammoConsumedPerShot);
        public bool StartsFull => _startsFull;

        private void OnValidate()
        {
            _clipCapacity = Mathf.Max(1, _clipCapacity);
            _ammoConsumedPerShot = Mathf.Clamp(_ammoConsumedPerShot, 1, _clipCapacity);
        }
    }
}
