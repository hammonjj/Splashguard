using BitBox.Library;
using UnityEngine;

namespace Bitbox.Splashguard.Nautical.Crane
{
    public static class CraneBoomUtility
    {
        public static float ApplyAxisInput(
            float currentDegrees,
            float input,
            float degreesPerSecond,
            float deltaTime,
            float minimumDegrees,
            float maximumDegrees)
        {
            if (deltaTime <= 0f || degreesPerSecond <= 0f)
            {
                return Mathf.Clamp(currentDegrees, minimumDegrees, maximumDegrees);
            }

            return Mathf.Clamp(
                currentDegrees + input * degreesPerSecond * deltaTime,
                minimumDegrees,
                maximumDegrees);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CraneBoomController : MonoBehaviourBase
    {
        [Header("Pivots")]
        [SerializeField] private Transform _yawPivot;
        [SerializeField] private Transform _pitchPivot;

        [Header("Axes")]
        [SerializeField] private Vector3 _yawLocalAxis = Vector3.up;
        [SerializeField] private Vector3 _pitchLocalAxis = Vector3.right;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _yawDegreesPerSecond = 75f;
        [SerializeField, Min(0f)] private float _pitchDegreesPerSecond = 45f;
        [SerializeField] private bool _invertPitchInput = true;
        [SerializeField] private float _minimumYawDegrees = -150f;
        [SerializeField] private float _maximumYawDegrees = 150f;
        [SerializeField] private float _minimumPitchDegrees = -15f;
        [SerializeField] private float _maximumPitchDegrees = 65f;

        private Quaternion _restYawLocalRotation = Quaternion.identity;
        private Quaternion _restPitchLocalRotation = Quaternion.identity;
        private Quaternion _returnStartYawLocalRotation = Quaternion.identity;
        private Quaternion _returnStartPitchLocalRotation = Quaternion.identity;
        private float _yawDegrees;
        private float _pitchDegrees;

        public Transform YawPivot => _yawPivot;
        public Transform PitchPivot => _pitchPivot;
        public float YawDegrees => _yawDegrees;
        public float PitchDegrees => _pitchDegrees;

        public void ConfigurePivots(Transform yawPivot, Transform pitchPivot)
        {
            _yawPivot = yawPivot;
            _pitchPivot = pitchPivot != null ? pitchPivot : yawPivot;
            CaptureRestPose();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            CaptureRestPose();
        }

        private void OnValidate()
        {
            _yawDegreesPerSecond = Mathf.Max(0f, _yawDegreesPerSecond);
            _pitchDegreesPerSecond = Mathf.Max(0f, _pitchDegreesPerSecond);
            if (_minimumYawDegrees > _maximumYawDegrees)
            {
                (_minimumYawDegrees, _maximumYawDegrees) = (_maximumYawDegrees, _minimumYawDegrees);
            }

            if (_minimumPitchDegrees > _maximumPitchDegrees)
            {
                (_minimumPitchDegrees, _maximumPitchDegrees) = (_maximumPitchDegrees, _minimumPitchDegrees);
            }
        }

        public void ApplyControlInput(Vector2 moveInput, float deltaTime)
        {
            CacheReferences();
            _yawDegrees = CraneBoomUtility.ApplyAxisInput(
                _yawDegrees,
                moveInput.x,
                _yawDegreesPerSecond,
                deltaTime,
                _minimumYawDegrees,
                _maximumYawDegrees);
            _pitchDegrees = CraneBoomUtility.ApplyAxisInput(
                _pitchDegrees,
                _invertPitchInput ? -moveInput.y : moveInput.y,
                _pitchDegreesPerSecond,
                deltaTime,
                _minimumPitchDegrees,
                _maximumPitchDegrees);
            ApplyCurrentRotations();
        }

        public void CaptureRestPose()
        {
            CacheReferences();
            if (_yawPivot != null)
            {
                _restYawLocalRotation = _yawPivot.localRotation;
            }

            if (_pitchPivot != null)
            {
                _restPitchLocalRotation = _pitchPivot.localRotation;
            }

            _yawDegrees = 0f;
            _pitchDegrees = 0f;
        }

        public void BeginReturnToRest()
        {
            CacheReferences();
            _returnStartYawLocalRotation = _yawPivot != null ? _yawPivot.localRotation : Quaternion.identity;
            _returnStartPitchLocalRotation = _pitchPivot != null ? _pitchPivot.localRotation : Quaternion.identity;
        }

        public void EvaluateReturnToRest(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            CacheReferences();
            if (_yawPivot != null)
            {
                _yawPivot.localRotation = Quaternion.Slerp(_returnStartYawLocalRotation, _restYawLocalRotation, t);
            }

            if (_pitchPivot != null)
            {
                _pitchPivot.localRotation = Quaternion.Slerp(_returnStartPitchLocalRotation, _restPitchLocalRotation, t);
            }

            _yawDegrees = Mathf.Lerp(_yawDegrees, 0f, t);
            _pitchDegrees = Mathf.Lerp(_pitchDegrees, 0f, t);
        }

        public void SnapToRest()
        {
            CacheReferences();
            if (_yawPivot != null)
            {
                _yawPivot.localRotation = _restYawLocalRotation;
            }

            if (_pitchPivot != null)
            {
                _pitchPivot.localRotation = _restPitchLocalRotation;
            }

            _yawDegrees = 0f;
            _pitchDegrees = 0f;
        }

        private void CacheReferences()
        {
            _yawPivot ??= transform;
            _pitchPivot ??= _yawPivot;
        }

        private void ApplyCurrentRotations()
        {
            Quaternion yawRotation = Quaternion.AngleAxis(_yawDegrees, ResolveAxis(_yawLocalAxis, Vector3.up));
            Quaternion pitchRotation = Quaternion.AngleAxis(_pitchDegrees, ResolveAxis(_pitchLocalAxis, Vector3.right));

            if (_yawPivot != null && _yawPivot == _pitchPivot)
            {
                _yawPivot.localRotation = _restYawLocalRotation * yawRotation * pitchRotation;
                return;
            }

            if (_yawPivot != null)
            {
                _yawPivot.localRotation = _restYawLocalRotation * yawRotation;
            }

            if (_pitchPivot != null)
            {
                _pitchPivot.localRotation = _restPitchLocalRotation * pitchRotation;
            }
        }

        private static Vector3 ResolveAxis(Vector3 axis, Vector3 fallback)
        {
            return axis.sqrMagnitude > 0.0001f ? axis.normalized : fallback;
        }
    }
}
