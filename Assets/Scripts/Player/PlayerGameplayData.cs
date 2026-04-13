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

        public float WalkSpeed => _walkSpeed;
        public float RotationSharpness => _rotationSharpness;
        public float MoveInputDeadZone => _moveInputDeadZone;
        public float Gravity => _gravity;
        public float JumpHeight => _jumpHeight;
        public float JumpRiseGravityMultiplier => _jumpRiseGravityMultiplier;
        public float JumpFallGravityMultiplier => _jumpFallGravityMultiplier;
        public float GroundedVerticalVelocity => _groundedVerticalVelocity;
    }
}
