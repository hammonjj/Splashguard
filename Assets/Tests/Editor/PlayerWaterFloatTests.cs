#if UNITY_EDITOR
using Bitbox;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using StormBreakers;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class PlayerWaterFloatTests
    {
        private const string DefaultPlayerGameplayDataPath = "Assets/Data/DefaultPlayerGameplayData.asset";
        private const string PlayerContainerPrefabPath = "Assets/Prefabs/PlayerContainer.prefab";

        [Test]
        public void ShouldEnterFloat_RequiresWaterSample()
        {
            bool shouldEnter = PlayerWaterFloatUtility.ShouldEnterFloat(
                waterFloatEnabled: true,
                hasWaterSample: false,
                isGrounded: false,
                verticalVelocity: -1f,
                controllerBottomY: 0f,
                waterHeight: 1f,
                waterEnterDepth: 0.05f);

            NUnitAssert.IsFalse(shouldEnter);
        }

        [Test]
        public void ShouldEnterFloat_DoesNotEnterWhileGrounded()
        {
            bool shouldEnter = PlayerWaterFloatUtility.ShouldEnterFloat(
                waterFloatEnabled: true,
                hasWaterSample: true,
                isGrounded: true,
                verticalVelocity: -1f,
                controllerBottomY: 0.9f,
                waterHeight: 1f,
                waterEnterDepth: 0.05f);

            NUnitAssert.IsFalse(shouldEnter);
        }

        [Test]
        public void ShouldEnterFloat_EntersWhenControllerBottomIntersectsWater()
        {
            bool shouldEnter = PlayerWaterFloatUtility.ShouldEnterFloat(
                waterFloatEnabled: true,
                hasWaterSample: true,
                isGrounded: false,
                verticalVelocity: -1f,
                controllerBottomY: 1.04f,
                waterHeight: 1f,
                waterEnterDepth: 0.05f);

            NUnitAssert.IsTrue(shouldEnter);
        }

        [Test]
        public void ShouldEnterFloat_DoesNotEnterWhileRising()
        {
            bool shouldEnter = PlayerWaterFloatUtility.ShouldEnterFloat(
                waterFloatEnabled: true,
                hasWaterSample: true,
                isGrounded: false,
                verticalVelocity: 0.1f,
                controllerBottomY: 0.9f,
                waterHeight: 1f,
                waterEnterDepth: 0.05f);

            NUnitAssert.IsFalse(shouldEnter);
        }

        [Test]
        public void ShouldExitFloat_ExitsWhenGrounded()
        {
            bool shouldExit = PlayerWaterFloatUtility.ShouldExitFloat(
                isFloating: true,
                hasWaterSample: true,
                isGrounded: true,
                playerRootY: 1f,
                waterHeight: 1f,
                waterExitHeight: 0.18f);

            NUnitAssert.IsTrue(shouldExit);
        }

        [Test]
        public void ShouldExitFloat_ExitsWhenAboveWaterExitHeight()
        {
            bool shouldExit = PlayerWaterFloatUtility.ShouldExitFloat(
                isFloating: true,
                hasWaterSample: true,
                isGrounded: false,
                playerRootY: 1.19f,
                waterHeight: 1f,
                waterExitHeight: 0.18f);

            NUnitAssert.IsTrue(shouldExit);
        }

        [Test]
        public void FloatVerticalVelocity_RisesTowardSurfaceAndClamps()
        {
            float velocity = PlayerWaterFloatUtility.CalculateFloatVerticalVelocity(
                playerRootY: 0f,
                waterHeight: 2f,
                waterSurfaceRootOffset: -0.05f,
                correctionSharpness: 10f,
                maxRiseSpeed: 3f,
                maxSinkSpeed: 1f);

            NUnitAssert.AreEqual(3f, velocity, 0.001f);
        }

        [Test]
        public void FloatVerticalVelocity_SinksTowardSurfaceAndClamps()
        {
            float velocity = PlayerWaterFloatUtility.CalculateFloatVerticalVelocity(
                playerRootY: 2f,
                waterHeight: 1f,
                waterSurfaceRootOffset: -0.05f,
                correctionSharpness: 10f,
                maxRiseSpeed: 3f,
                maxSinkSpeed: 1f);

            NUnitAssert.AreEqual(-1f, velocity, 0.001f);
        }

        [Test]
        public void DefaultPlayerGameplayData_HasWaterFloatDefaults()
        {
            PlayerGameplayData gameplayData =
                AssetDatabase.LoadAssetAtPath<PlayerGameplayData>(DefaultPlayerGameplayDataPath);

            NUnitAssert.IsNotNull(gameplayData, $"Expected {nameof(PlayerGameplayData)} at {DefaultPlayerGameplayDataPath}.");
            NUnitAssert.IsTrue(gameplayData.WaterFloatEnabled);
            NUnitAssert.AreEqual(0.45f, gameplayData.WaterMoveSpeedMultiplier, 0.001f);
            NUnitAssert.AreEqual(0.05f, gameplayData.WaterEnterDepth, 0.001f);
            NUnitAssert.AreEqual(0.18f, gameplayData.WaterExitHeight, 0.001f);
            NUnitAssert.AreEqual(-0.05f, gameplayData.WaterSurfaceRootOffset, 0.001f);
            NUnitAssert.AreEqual(10f, gameplayData.WaterFloatCorrectionSharpness, 0.001f);
            NUnitAssert.AreEqual(3f, gameplayData.WaterFloatRiseSpeed, 0.001f);
            NUnitAssert.AreEqual(1f, gameplayData.WaterFloatSinkSpeed, 0.001f);
            NUnitAssert.AreEqual(0.6f, gameplayData.WaterJumpHeightMultiplier, 0.001f);
        }

        [Test]
        public void PlayerContainer_UsesCharacterControllerWithoutBuoyancyComponents()
        {
            GameObject playerContainer = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerContainerPrefabPath);

            NUnitAssert.IsNotNull(playerContainer, $"Expected PlayerContainer prefab at {PlayerContainerPrefabPath}.");
            NUnitAssert.IsNotNull(playerContainer.GetComponent<CharacterController>());
            NUnitAssert.IsNull(playerContainer.GetComponentInChildren<BoyancyController>(includeInactive: true));
            NUnitAssert.IsNull(playerContainer.GetComponentInChildren<WaterInteraction>(includeInactive: true));
        }
    }
}
#endif
