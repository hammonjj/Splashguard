#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Input;
using Bitbox;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class BoatPassengerVolumeTests
    {
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel/PlayerVessel.prefab";

        [Test]
        public void PlayerVesselPrefab_HasBoatPassengerVolumeConfigured()
        {
            GameObject playerVessel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);
            NUnitAssert.IsNotNull(playerVessel, "Expected PlayerVessel prefab to exist.");

            PlayerVesselRoot vesselRoot = playerVessel.GetComponent<PlayerVesselRoot>();
            BoatPassengerVolume passengerVolume = playerVessel.GetComponentInChildren<BoatPassengerVolume>(includeInactive: true);

            NUnitAssert.IsNotNull(vesselRoot, "PlayerVessel should include PlayerVesselRoot on the root.");
            NUnitAssert.IsNotNull(passengerVolume, "PlayerVessel should include a BoatPassengerVolume.");

            Collider volumeTrigger = passengerVolume.GetComponent<Collider>();
            NUnitAssert.IsNotNull(volumeTrigger, "BoatPassengerVolume should live on a trigger collider.");
            NUnitAssert.IsTrue(volumeTrigger.isTrigger);
            NUnitAssert.AreSame(vesselRoot, passengerVolume.GetComponentInParent<PlayerVesselRoot>());
        }

        [Test]
        public void RefreshPassengers_ParentsFreeRoamPlayersAndRestoresWorldPoseOnExit()
        {
            PlayerCoordinator previousCoordinator = StaticData.PlayerInputCoordinator;
            CreatePassengerVolume(out GameObject boatRoot, out BoatPassengerVolume passengerVolume);
            GameObject originalParent = new("OriginalParent");
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            BoxCollider playerCollider = playerInput.gameObject.AddComponent<BoxCollider>();
            playerCollider.size = Vector3.one * 0.25f;
            playerInput.transform.SetParent(originalParent.transform, true);
            playerInput.transform.position = new Vector3(0f, 0.75f, 0f);

            PlayerCoordinator coordinator = CreateCoordinator(playerInput);
            StaticData.PlayerInputCoordinator = coordinator;

            try
            {
                InvokePrivate(passengerVolume, "RefreshPassengers");

                Vector3 attachedWorldPosition = playerInput.transform.position;
                NUnitAssert.AreSame(boatRoot.transform, playerInput.transform.parent);
                NUnitAssert.AreEqual(new Vector3(0f, 0.75f, 0f), attachedWorldPosition);

                playerInput.transform.position = new Vector3(6f, 0.75f, 0f);
                Vector3 detachedExpectedPosition = playerInput.transform.position;
                Quaternion detachedExpectedRotation = playerInput.transform.rotation;

                InvokePrivate(passengerVolume, "RefreshPassengers");

                NUnitAssert.AreSame(originalParent.transform, playerInput.transform.parent);
                NUnitAssert.AreEqual(detachedExpectedPosition, playerInput.transform.position);
                NUnitAssert.AreEqual(detachedExpectedRotation.eulerAngles, playerInput.transform.rotation.eulerAngles);
            }
            finally
            {
                StaticData.PlayerInputCoordinator = previousCoordinator;
                Object.DestroyImmediate(coordinator.gameObject);
                Object.DestroyImmediate(playerInput.gameObject);
                Object.DestroyImmediate(inputActions);
                Object.DestroyImmediate(originalParent);
                Object.DestroyImmediate(boatRoot);
            }
        }

        [Test]
        public void OnDisabled_DetachesAttachedPlayers()
        {
            PlayerCoordinator previousCoordinator = StaticData.PlayerInputCoordinator;
            CreatePassengerVolume(out GameObject boatRoot, out BoatPassengerVolume passengerVolume);
            GameObject originalParent = new("OriginalParent");
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            BoxCollider playerCollider = playerInput.gameObject.AddComponent<BoxCollider>();
            playerCollider.size = Vector3.one * 0.25f;
            playerInput.transform.SetParent(originalParent.transform, true);
            playerInput.transform.position = new Vector3(0f, 0.75f, 0f);

            PlayerCoordinator coordinator = CreateCoordinator(playerInput);
            StaticData.PlayerInputCoordinator = coordinator;

            try
            {
                InvokePrivate(passengerVolume, "RefreshPassengers");
                NUnitAssert.AreSame(boatRoot.transform, playerInput.transform.parent);

                passengerVolume.enabled = false;

                NUnitAssert.AreSame(originalParent.transform, playerInput.transform.parent);
            }
            finally
            {
                StaticData.PlayerInputCoordinator = previousCoordinator;
                Object.DestroyImmediate(coordinator.gameObject);
                Object.DestroyImmediate(playerInput.gameObject);
                Object.DestroyImmediate(inputActions);
                Object.DestroyImmediate(originalParent);
                Object.DestroyImmediate(boatRoot);
            }
        }

        [Test]
        public void OnDestroyed_DetachesAttachedPlayers()
        {
            PlayerCoordinator previousCoordinator = StaticData.PlayerInputCoordinator;
            CreatePassengerVolume(out GameObject boatRoot, out BoatPassengerVolume passengerVolume);
            GameObject originalParent = new("OriginalParent");
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            BoxCollider playerCollider = playerInput.gameObject.AddComponent<BoxCollider>();
            playerCollider.size = Vector3.one * 0.25f;
            playerInput.transform.SetParent(originalParent.transform, true);
            playerInput.transform.position = new Vector3(0f, 0.75f, 0f);

            PlayerCoordinator coordinator = CreateCoordinator(playerInput);
            StaticData.PlayerInputCoordinator = coordinator;

            try
            {
                InvokePrivate(passengerVolume, "RefreshPassengers");
                NUnitAssert.AreSame(boatRoot.transform, playerInput.transform.parent);

                Object.DestroyImmediate(passengerVolume.gameObject);

                NUnitAssert.AreSame(originalParent.transform, playerInput.transform.parent);
            }
            finally
            {
                StaticData.PlayerInputCoordinator = previousCoordinator;
                Object.DestroyImmediate(coordinator.gameObject);
                Object.DestroyImmediate(playerInput.gameObject);
                Object.DestroyImmediate(inputActions);
                Object.DestroyImmediate(originalParent);
                Object.DestroyImmediate(boatRoot);
            }
        }

        [Test]
        public void RefreshPassengers_ReattachesPlayersAfterStationReleaseWhenStillInside()
        {
            PlayerCoordinator previousCoordinator = StaticData.PlayerInputCoordinator;
            CreatePassengerVolume(out GameObject boatRoot, out BoatPassengerVolume passengerVolume);
            GameObject originalParent = new("OriginalParent");
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            BoxCollider playerCollider = playerInput.gameObject.AddComponent<BoxCollider>();
            playerCollider.size = Vector3.one * 0.25f;
            playerInput.transform.SetParent(originalParent.transform, true);
            playerInput.transform.position = new Vector3(0f, 0.75f, 0f);

            PlayerCoordinator coordinator = CreateCoordinator(playerInput);
            StaticData.PlayerInputCoordinator = coordinator;

            try
            {
                InvokePrivate(passengerVolume, "RefreshPassengers");
                NUnitAssert.AreSame(boatRoot.transform, playerInput.transform.parent);

                playerCollider.enabled = false;
                playerInput.SwitchCurrentActionMap(Strings.BoatGunner);
                InvokePrivate(passengerVolume, "RefreshPassengers");
                NUnitAssert.AreSame(originalParent.transform, playerInput.transform.parent);

                playerCollider.enabled = true;
                playerInput.SwitchCurrentActionMap(Strings.ThirdPersonControls);
                InvokePrivate(passengerVolume, "RefreshPassengers");
                NUnitAssert.AreSame(boatRoot.transform, playerInput.transform.parent);
            }
            finally
            {
                StaticData.PlayerInputCoordinator = previousCoordinator;
                Object.DestroyImmediate(coordinator.gameObject);
                Object.DestroyImmediate(playerInput.gameObject);
                Object.DestroyImmediate(inputActions);
                Object.DestroyImmediate(originalParent);
                Object.DestroyImmediate(boatRoot);
            }
        }

        private static void CreatePassengerVolume(out GameObject boatRoot, out BoatPassengerVolume passengerVolume)
        {
            boatRoot = new GameObject("PlayerVessel");
            boatRoot.AddComponent<PlayerVesselRoot>();

            GameObject volumeObject = new("BoatPassengerVolume");
            volumeObject.transform.SetParent(boatRoot.transform, false);
            volumeObject.transform.localPosition = Vector3.zero;

            BoxCollider trigger = volumeObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4f, 4f, 4f);

            passengerVolume = volumeObject.AddComponent<BoatPassengerVolume>();
            InvokePrivate(passengerVolume, "CacheReferences");
        }

        private static PlayerCoordinator CreateCoordinator(params PlayerInput[] playerInputs)
        {
            GameObject coordinatorObject = new("PlayerCoordinator");
            coordinatorObject.SetActive(false);
            PlayerCoordinator coordinator = coordinatorObject.AddComponent<PlayerCoordinator>();
            SetPrivateField(coordinator, "_playerInputs", new List<PlayerInput>(playerInputs));
            return coordinator;
        }

        private static PlayerInput CreatePlayerInput(string currentActionMap, out InputActionAsset inputActions)
        {
            GameObject playerObject = new("TestPlayerInput");
            PlayerInput playerInput = playerObject.AddComponent<PlayerInput>();
            inputActions = ScriptableObject.CreateInstance<InputActionAsset>();

            inputActions.AddActionMap(Strings.ThirdPersonControls).AddAction(Strings.ActionAction);
            inputActions.AddActionMap(Strings.BoatGunner).AddAction(Strings.ActionAction);
            inputActions.AddActionMap(Strings.NavalNavigation).AddAction(Strings.ActionAction);
            inputActions.AddActionMap(Strings.CraneControls).AddAction(Strings.ActionAction);

            playerInput.actions = inputActions;
            playerInput.defaultActionMap = currentActionMap;
            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(currentActionMap);
            playerInput.ActivateInput();
            return playerInput;
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected private method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
#endif
