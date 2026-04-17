using BitBox.Library;
using DamageNumbersPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealthWorldDisplay : MonoBehaviourBase
    {
        [SerializeField] private GameObject _displayRoot;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Transform _damageTextAnchor;
        [SerializeField] private DamageNumber _damageNumberPrefab;
        [SerializeField, Min(0f)] private float _damageTextRandomHorizontalRadius = 0.25f;
        [SerializeField] private bool _warnWhenDamageNumberPrefabMissing = true;

        private bool _warnedMissingDamageNumberPrefab;

        public bool IsVisible => ResolveDisplayRoot().activeSelf;
        public Slider HealthSlider => _healthSlider;
        public DamageNumber DamageNumberPrefab => _damageNumberPrefab;

        protected override void OnAwakened()
        {
            CacheReferences();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
        }

        public void Initialize(float currentHealth, float maxHealth)
        {
            CacheReferences();
            SetHealth01(CalculateHealth01(currentHealth, maxHealth));
            SetVisible(false);
            _warnedMissingDamageNumberPrefab = false;
        }

        public void ShowDamage(float currentHealth, float maxHealth, float damage, Vector3? spawnPosition)
        {
            CacheReferences();
            SetHealth01(CalculateHealth01(currentHealth, maxHealth));
            SetVisible(true);
            SpawnDamageText(damage, spawnPosition);
        }

        public void HandleDeath(float currentHealth, float maxHealth)
        {
            CacheReferences();
            SetHealth01(CalculateHealth01(currentHealth, maxHealth));
            SetVisible(false);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void CacheReferences()
        {
            _displayRoot ??= gameObject;
            _canvas ??= GetComponentInChildren<Canvas>(includeInactive: true);
            _healthSlider ??= GetComponentInChildren<Slider>(includeInactive: true);

            if (_damageTextAnchor == null)
            {
                Transform anchor = transform.Find("DamageTextAnchor");
                _damageTextAnchor = anchor != null ? anchor : transform;
            }
        }

        private void SetHealth01(float health01)
        {
            if (_healthSlider == null)
            {
                return;
            }

            _healthSlider.minValue = 0f;
            _healthSlider.maxValue = 1f;
            _healthSlider.interactable = false;
            _healthSlider.SetValueWithoutNotify(Mathf.Clamp01(health01));
        }

        private void SpawnDamageText(float damage, Vector3? spawnPosition)
        {
            if (damage <= 0f)
            {
                return;
            }

            if (_damageNumberPrefab == null)
            {
                if (_warnWhenDamageNumberPrefabMissing && !_warnedMissingDamageNumberPrefab)
                {
                    LogWarning("Enemy damage number prefab is not assigned. Health bar updates will continue without floating damage text.");
                    _warnedMissingDamageNumberPrefab = true;
                }

                return;
            }

            Vector3 resolvedPosition = spawnPosition ?? ResolveAnchorPosition();
            if (_damageTextRandomHorizontalRadius > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * _damageTextRandomHorizontalRadius;
                resolvedPosition += new Vector3(offset.x, 0f, offset.y);
            }

            _damageNumberPrefab.Spawn(resolvedPosition, damage);
        }

        private Vector3 ResolveAnchorPosition()
        {
            return _damageTextAnchor != null ? _damageTextAnchor.position : transform.position;
        }

        private void SetVisible(bool visible)
        {
            GameObject root = ResolveDisplayRoot();
            if (root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }

        private GameObject ResolveDisplayRoot()
        {
            return _displayRoot != null ? _displayRoot : gameObject;
        }

        private static float CalculateHealth01(float currentHealth, float maxHealth)
        {
            return maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        }
    }
}
