namespace BitBox.Library.Eventing.PlayerEvents
{
    public sealed class PlayerLocomotionAnimationEvent
    {
        public PlayerLocomotionAnimationEvent(float locomotionNormalized, bool isGrounded, float verticalVelocity, bool jumpStartedThisFrame)
        {
            LocomotionNormalized = locomotionNormalized;
            IsGrounded = isGrounded;
            VerticalVelocity = verticalVelocity;
            JumpStartedThisFrame = jumpStartedThisFrame;
        }

        public float LocomotionNormalized { get; }
        public bool IsGrounded { get; }
        public float VerticalVelocity { get; }
        public bool JumpStartedThisFrame { get; }

        public override string ToString()
        {
            return
                $"{nameof(PlayerLocomotionAnimationEvent)}(LocomotionNormalized={LocomotionNormalized:F2}, IsGrounded={IsGrounded}, VerticalVelocity={VerticalVelocity:F2}, JumpStartedThisFrame={JumpStartedThisFrame})";
        }
    }
}
