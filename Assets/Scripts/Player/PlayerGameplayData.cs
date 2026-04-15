using UnityEngine;

namespace Bitbox
{
    [CreateAssetMenu(fileName = "PlayerGameplayData", menuName = "Player/Player Gameplay Data")]
    public sealed class PlayerGameplayData : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _walkSpeed = 8f;
        [SerializeField, Min(0f)] private float _rotationSharpness = 14f;
        [SerializeField, Range(0f, 1f)] private float _moveInputDeadZone = 0.2f;
        [SerializeField, Min(0f)] private float _gravity = 30f;
        [SerializeField, Min(0f)] private float _jumpHeight = 2.25f;
        [SerializeField, Min(0.01f)] private float _jumpRiseGravityMultiplier = 0.35f;
        [SerializeField, Min(0.01f)] private float _jumpFallGravityMultiplier = 0.5f;
        [SerializeField] private float _groundedVerticalVelocity = -2f;

        [Header("Water Floating")]
        [SerializeField] private bool _waterFloatEnabled = true;
        [SerializeField, Range(0f, 1f)] private float _waterMoveSpeedMultiplier = 0.45f;
        [SerializeField, Min(0f)] private float _waterEnterDepth = 0.05f;
        [SerializeField, Min(0f)] private float _waterExitHeight = 0.18f;
        [SerializeField] private float _waterSurfaceRootOffset = -0.05f;
        [SerializeField, Min(0f)] private float _waterFloatCorrectionSharpness = 10f;
        [SerializeField, Min(0f)] private float _waterFloatRiseSpeed = 3f;
        [SerializeField, Min(0f)] private float _waterFloatSinkSpeed = 1f;
        [SerializeField, Min(0f)] private float _waterJumpHeightMultiplier = 0.6f;

        public float WalkSpeed => _walkSpeed;
        public float RotationSharpness => _rotationSharpness;
        public float MoveInputDeadZone => _moveInputDeadZone;
        public float Gravity => _gravity;
        public float JumpHeight => _jumpHeight;
        public float JumpRiseGravityMultiplier => _jumpRiseGravityMultiplier;
        public float JumpFallGravityMultiplier => _jumpFallGravityMultiplier;
        public float GroundedVerticalVelocity => _groundedVerticalVelocity;
        public bool WaterFloatEnabled => _waterFloatEnabled;
        public float WaterMoveSpeedMultiplier => Mathf.Clamp01(_waterMoveSpeedMultiplier);
        public float WaterEnterDepth => Mathf.Max(0f, _waterEnterDepth);
        public float WaterExitHeight => Mathf.Max(0f, _waterExitHeight);
        public float WaterSurfaceRootOffset => _waterSurfaceRootOffset;
        public float WaterFloatCorrectionSharpness => Mathf.Max(0f, _waterFloatCorrectionSharpness);
        public float WaterFloatRiseSpeed => Mathf.Max(0f, _waterFloatRiseSpeed);
        public float WaterFloatSinkSpeed => Mathf.Max(0f, _waterFloatSinkSpeed);
        public float WaterJumpHeightMultiplier => Mathf.Max(0f, _waterJumpHeightMultiplier);
    }
}
