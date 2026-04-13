using UnityEngine;
using UnityEngine.UI;

namespace BitBox.Library.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class UiScaleCanvasSync : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _baseScaleFactor = 1f;
        [SerializeField] private CanvasScaler _canvasScaler;

        private float _lastAppliedUiScale = -1f;

        private void Reset()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            ApplyCurrentScale(force: true);
        }

        private void LateUpdate()
        {
            ApplyCurrentScale();
        }

        private void OnDisable()
        {
            _lastAppliedUiScale = -1f;
        }

        private void CacheReferences()
        {
            if (_canvasScaler == null)
            {
                _canvasScaler = GetComponent<CanvasScaler>();
            }
        }

        private void ApplyCurrentScale(bool force = false)
        {
            ApplyScale(UiScaleRuntime.ResolveScale(), force);
        }

        private void ApplyScale(float uiScale, bool force = false)
        {
            CacheReferences();
            if (_canvasScaler == null)
            {
                return;
            }

            float resolvedScale = Mathf.Max(0.01f, uiScale);
            if (!force && Mathf.Approximately(_lastAppliedUiScale, resolvedScale))
            {
                return;
            }

            _canvasScaler.scaleFactor = Mathf.Max(0.01f, _baseScaleFactor * resolvedScale);
            _lastAppliedUiScale = resolvedScale;
        }
    }
}
