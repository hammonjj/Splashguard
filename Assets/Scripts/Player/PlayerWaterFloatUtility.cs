using UnityEngine;

namespace Bitbox
{
    public static class PlayerWaterFloatUtility
    {
        public static float CalculateControllerBottomY(Vector3 controllerWorldCenter, float controllerHeight, float controllerRadius)
        {
            return controllerWorldCenter.y - Mathf.Max(controllerHeight * 0.5f, controllerRadius);
        }

        public static bool ShouldEnterFloat(
            bool waterFloatEnabled,
            bool hasWaterSample,
            bool isGrounded,
            float verticalVelocity,
            float controllerBottomY,
            float waterHeight,
            float waterEnterDepth)
        {
            return waterFloatEnabled
                && hasWaterSample
                && !isGrounded
                && verticalVelocity <= 0f
                && controllerBottomY <= waterHeight + Mathf.Max(0f, waterEnterDepth);
        }

        public static bool ShouldExitFloat(
            bool isFloating,
            bool hasWaterSample,
            bool isGrounded,
            float playerRootY,
            float waterHeight,
            float waterExitHeight)
        {
            return isFloating
                && (isGrounded
                    || !hasWaterSample
                    || playerRootY > waterHeight + Mathf.Max(0f, waterExitHeight));
        }

        public static float CalculateFloatVerticalVelocity(
            float playerRootY,
            float waterHeight,
            float waterSurfaceRootOffset,
            float correctionSharpness,
            float maxRiseSpeed,
            float maxSinkSpeed)
        {
            float targetRootY = waterHeight + waterSurfaceRootOffset;
            float correctionVelocity = (targetRootY - playerRootY) * Mathf.Max(0f, correctionSharpness);
            return Mathf.Clamp(
                correctionVelocity,
                -Mathf.Max(0f, maxSinkSpeed),
                Mathf.Max(0f, maxRiseSpeed));
        }

        public static float CalculateWaterJumpVelocity(
            float jumpHeight,
            float gravity,
            float jumpRiseGravityMultiplier,
            float waterJumpHeightMultiplier)
        {
            float effectiveJumpHeight = Mathf.Max(0f, jumpHeight) * Mathf.Max(0f, waterJumpHeightMultiplier);
            float effectiveGravity = Mathf.Max(0f, gravity) * Mathf.Max(0f, jumpRiseGravityMultiplier);
            return Mathf.Sqrt(effectiveJumpHeight * 2f * effectiveGravity);
        }
    }
}
