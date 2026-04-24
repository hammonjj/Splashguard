#if UNITY_EDITOR
using System.Reflection;
using Bitbox;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class PlayerMovementSupportTests
    {
        private const string PlayerContainerPrefabPath = "Assets/Prefabs/PlayerContainer.prefab";

        [Test]
        public void PlayerVesselSupport_AppliesSupportDisplacement()
        {
            GameObject playerContainer = InstantiatePlayerContainer();
            GameObject vesselRootObject = new("PlayerVessel");
            vesselRootObject.AddComponent<PlayerVesselRoot>();
            GameObject supportObject = new("Support");
            supportObject.transform.SetParent(vesselRootObject.transform, false);

            try
            {
                PlayerMovement movement = playerContainer.GetComponent<PlayerMovement>();
                NUnitAssert.IsNotNull(movement, "PlayerContainer should include PlayerMovement.");

                playerContainer.transform.position = Vector3.zero;
                InvokePrivate(movement, "UpdateActiveSupport", true, supportObject.transform);
                supportObject.transform.position = Vector3.right * 2f;

                Vector3 supportDisplacement = InvokePrivate<Vector3>(movement, "ResolveSupportDisplacement");
                NUnitAssert.AreEqual(new Vector3(2f, 0f, 0f), supportDisplacement);
            }
            finally
            {
                Object.DestroyImmediate(supportObject);
                Object.DestroyImmediate(vesselRootObject);
                Object.DestroyImmediate(playerContainer);
            }
        }

        [Test]
        public void NonBoatSupport_StillAppliesSupportDisplacement()
        {
            GameObject playerContainer = InstantiatePlayerContainer();
            GameObject supportObject = new("MovingPlatform");

            try
            {
                PlayerMovement movement = playerContainer.GetComponent<PlayerMovement>();
                NUnitAssert.IsNotNull(movement, "PlayerContainer should include PlayerMovement.");

                playerContainer.transform.position = Vector3.zero;
                InvokePrivate(movement, "UpdateActiveSupport", true, supportObject.transform);
                supportObject.transform.position = Vector3.right * 2f;

                Vector3 supportDisplacement = InvokePrivate<Vector3>(movement, "ResolveSupportDisplacement");
                NUnitAssert.AreEqual(new Vector3(2f, 0f, 0f), supportDisplacement);
            }
            finally
            {
                Object.DestroyImmediate(supportObject);
                Object.DestroyImmediate(playerContainer);
            }
        }

        [Test]
        public void GameplaySceneTransition_PreservesPoseWhenPlayerIsAttachedToBoat()
        {
            GameObject playerContainer = InstantiatePlayerContainer();
            GameObject vesselRootObject = new("PlayerVessel");
            vesselRootObject.AddComponent<PlayerVesselRoot>();
            GameObject passengerVolumeObject = new("BoatPassengerVolume");
            passengerVolumeObject.transform.SetParent(vesselRootObject.transform, false);
            BoxCollider volumeTrigger = passengerVolumeObject.AddComponent<BoxCollider>();
            volumeTrigger.isTrigger = true;
            volumeTrigger.size = new Vector3(4f, 4f, 4f);
            BoatPassengerVolume passengerVolume = passengerVolumeObject.AddComponent<BoatPassengerVolume>();

            try
            {
                PlayerMovement movement = playerContainer.GetComponent<PlayerMovement>();
                PlayerInput playerInput = playerContainer.GetComponent<PlayerInput>();
                NUnitAssert.IsNotNull(movement, "PlayerContainer should include PlayerMovement.");
                NUnitAssert.IsNotNull(playerInput, "PlayerContainer should include PlayerInput.");

                playerContainer.transform.SetPositionAndRotation(new Vector3(4f, 1.2f, -3f), Quaternion.Euler(0f, 135f, 0f));
                InvokePrivate(passengerVolume, "CacheReferences");
                passengerVolume.TryAttachRider(playerInput);
                SetPrivateField(movement, "_currentMacroScene", MacroSceneType.HubWorld);

                InvokePrivate(movement, "OnMacroSceneLoaded", new MacroSceneLoadedEvent(MacroSceneType.CombatArena));

                NUnitAssert.AreEqual(new Vector3(4f, 1.2f, -3f), playerContainer.transform.position);
                NUnitAssert.AreEqual(135f, playerContainer.transform.rotation.eulerAngles.y, 0.001f);
                NUnitAssert.IsTrue(
                    passengerVolume.IsRiderTracked(playerInput),
                    "Gameplay scene transitions should preserve tracked boat-rider poses instead of snapping to authored spawns.");
            }
            finally
            {
                Object.DestroyImmediate(vesselRootObject);
                Object.DestroyImmediate(playerContainer);
            }
        }

        [Test]
        public void PlayerContainer_CharacterControllerIncludesDefaultAndTerrainLayers()
        {
            GameObject playerContainerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerContainerPrefabPath);
            NUnitAssert.IsNotNull(playerContainerPrefab, "Expected PlayerContainer prefab to exist.");

            CharacterController characterController = playerContainerPrefab.GetComponent<CharacterController>();
            NUnitAssert.IsNotNull(characterController, "PlayerContainer should include a CharacterController.");

            int defaultMask = 1 << LayerMask.NameToLayer("Default");
            int terrainMask = 1 << LayerMask.NameToLayer("Terrain");

            NUnitAssert.AreEqual(
                defaultMask,
                characterController.includeLayers.value & defaultMask,
                "PlayerContainer should collide with default-layer boat deck colliders.");
            NUnitAssert.AreEqual(
                terrainMask,
                characterController.includeLayers.value & terrainMask,
                "PlayerContainer should continue colliding with terrain.");
            NUnitAssert.Zero(
                characterController.excludeLayers.value & defaultMask,
                "PlayerContainer must not exclude default-layer deck collisions.");
            NUnitAssert.Zero(
                characterController.excludeLayers.value & terrainMask,
                "PlayerContainer must not exclude terrain collisions.");
        }

        [Test]
        public void BoatPassengerDetachWithMomentum_SeedsInheritedPlayerVelocity()
        {
            GameObject playerContainer = InstantiatePlayerContainer();
            GameObject vesselRootObject = new("PlayerVessel");
            GameObject originalParent = new("OriginalParent");
            vesselRootObject.AddComponent<PlayerVesselRoot>();
            Rigidbody vesselRigidbody = vesselRootObject.AddComponent<Rigidbody>();
            vesselRigidbody.linearVelocity = new Vector3(4f, 1.5f, -2f);
            vesselRigidbody.angularVelocity = new Vector3(0f, 2f, 0f);

            GameObject passengerVolumeObject = new("BoatPassengerVolume");
            passengerVolumeObject.transform.SetParent(vesselRootObject.transform, false);
            BoxCollider volumeTrigger = passengerVolumeObject.AddComponent<BoxCollider>();
            volumeTrigger.isTrigger = true;
            volumeTrigger.size = new Vector3(4f, 4f, 4f);
            BoatPassengerVolume passengerVolume = passengerVolumeObject.AddComponent<BoatPassengerVolume>();

            try
            {
                PlayerMovement movement = playerContainer.GetComponent<PlayerMovement>();
                PlayerInput playerInput = playerContainer.GetComponent<PlayerInput>();
                NUnitAssert.IsNotNull(movement, "PlayerContainer should include PlayerMovement.");
                NUnitAssert.IsNotNull(playerInput, "PlayerContainer should include PlayerInput.");

                InvokePrivate(passengerVolume, "CacheReferences");
                playerContainer.transform.SetParent(originalParent.transform, true);
                playerContainer.transform.position = new Vector3(0.5f, 0.75f, 0.5f);
                passengerVolume.TryAttachRider(playerInput);

                Vector3 expectedPointVelocity = vesselRigidbody.GetPointVelocity(playerContainer.transform.position);
                passengerVolume.DetachRiderWithMomentum(playerInput);

                NUnitAssert.AreSame(
                    originalParent.transform,
                    playerContainer.transform.parent,
                    "Detaching from the boat should restore the player's pre-boat parent.");
                NUnitAssert.AreEqual(
                    Vector3.ProjectOnPlane(expectedPointVelocity, Vector3.up),
                    GetPrivateField<Vector3>(movement, "_inheritedHorizontalVelocity"));
                NUnitAssert.AreEqual(
                    expectedPointVelocity.y,
                    GetPrivateField<float>(movement, "_verticalVelocity"),
                    0.001f);
            }
            finally
            {
                Object.DestroyImmediate(originalParent);
                Object.DestroyImmediate(vesselRootObject);
                Object.DestroyImmediate(playerContainer);
            }
        }

        private static GameObject InstantiatePlayerContainer()
        {
            GameObject playerContainerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerContainerPrefabPath);
            NUnitAssert.IsNotNull(playerContainerPrefab, "Expected PlayerContainer prefab to exist.");

            GameObject playerContainer = PrefabUtility.InstantiatePrefab(playerContainerPrefab) as GameObject;
            NUnitAssert.IsNotNull(playerContainer, "Expected to instantiate PlayerContainer prefab.");
            return playerContainer;
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] arguments)
        {
            return (T)InvokePrivate(target, methodName, arguments);
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected private method '{methodName}' on {target.GetType().Name}.");
            return method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }
    }
}
#endif
