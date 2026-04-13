using UnityEngine;

namespace Bitbox
{
    [CreateAssetMenu(fileName = "BoatGunData", menuName = "Nautical/Boat Gun Data")]
    public sealed class BoatGunData : ScriptableObject
    {
        [Header("Mouse Aim")]
        [SerializeField, Min(0f)] private float _mouseYawDegreesPerPixel = 0.08f;
        [SerializeField, Min(0f)] private float _mousePitchDegreesPerPixel = 0.08f;

        [Header("Gamepad Aim")]
        [SerializeField, Min(0f)] private float _gamepadYawDegreesPerSecond = 95f;
        [SerializeField, Min(0f)] private float _gamepadPitchDegreesPerSecond = 70f;
        [SerializeField, Range(0f, 1f)] private float _aimDeadZone = 0.08f;

        [Header("Pitch Limits")]
        [SerializeField] private float _minPitch = -22f;
        [SerializeField] private float _maxPitch = 3f;

        [Header("Camera")]
        [SerializeField, Range(1f, 179f)] private float _defaultFieldOfView = 60f;
        [SerializeField, Range(1f, 179f)] private float _zoomFieldOfView = 48f;
        [SerializeField, Min(0f)] private float _zoomTransitionSpeed = 12f;
        [SerializeField, Range(0.01f, 1f)] private float _zoomAimMultiplier = 0.75f;

        public float MouseYawDegreesPerPixel => _mouseYawDegreesPerPixel;
        public float MousePitchDegreesPerPixel => _mousePitchDegreesPerPixel;
        public float GamepadYawDegreesPerSecond => _gamepadYawDegreesPerSecond;
        public float GamepadPitchDegreesPerSecond => _gamepadPitchDegreesPerSecond;
        public float AimDeadZone => _aimDeadZone;
        public float MinPitch => _minPitch;
        public float MaxPitch => _maxPitch;
        public float DefaultFieldOfView => _defaultFieldOfView;
        public float ZoomFieldOfView => _zoomFieldOfView;
        public float ZoomTransitionSpeed => _zoomTransitionSpeed;
        public float ZoomAimMultiplier => _zoomAimMultiplier;

        public float ClampPitch(float pitchDegrees)
        {
            return Mathf.Clamp(pitchDegrees, _minPitch, _maxPitch);
        }

        private void OnValidate()
        {
            if (_minPitch > _maxPitch)
            {
                (_minPitch, _maxPitch) = (_maxPitch, _minPitch);
            }

            _defaultFieldOfView = Mathf.Clamp(_defaultFieldOfView, 1f, 179f);
            _zoomFieldOfView = Mathf.Clamp(_zoomFieldOfView, 1f, 179f);
            _zoomTransitionSpeed = Mathf.Max(0f, _zoomTransitionSpeed);
            _zoomAimMultiplier = Mathf.Clamp(_zoomAimMultiplier, 0.01f, 1f);
        }
    }
}
