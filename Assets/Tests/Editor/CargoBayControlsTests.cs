#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.GlobalEvents;
using Bitbox;
using Bitbox.Splashguard.Nautical;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class CargoBayControlsTests
    {
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel/PlayerVessel.prefab";
        private const string CargoBayControlsPrefabPath = "Assets/Prefabs/PlayerVessel/CargoBayControls.prefab";
        private const string InputActionsPath = "Assets/Settings/Input/InputSystem_Actions.inputactions";
        private const string PlayerContainerPrefabPath = "Assets/Prefabs/PlayerContainer.prefab";

        [Test]
        public void CargoBayControls_ResolvesOwnInteractionTrigger()
        {
            GameObject playerVessel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);
            NUnitAssert.IsNotNull(playerVessel, "Expected PlayerVessel prefab to exist.");

            CargoBayControls controls = playerVessel.GetComponentInChildren<CargoBayControls>(includeInactive: true);
            NUnitAssert.IsNotNull(controls, "PlayerVessel should contain CargoBayControls.");

            InvokePrivate(controls, "CacheReferences");
            Collider interactionTrigger = GetPrivateField<Collider>(controls, "_interactionTrigger");

            NUnitAssert.IsNotNull(interactionTrigger, "CargoBayControls should resolve an interaction trigger.");
            NUnitAssert.IsTrue(interactionTrigger.isTrigger);
            NUnitAssert.AreEqual("InteractionTrigger", interactionTrigger.transform.name);
            NUnitAssert.IsTrue(
                interactionTrigger.transform.IsChildOf(controls.transform),
                "CargoBayControls should not fall back to another station trigger.");
        }

        [Test]
        public void TakingCargoControl_OpensDoorsAndSwitchesToCraneControls()
        {
            CargoBayControls controls = CreateConfiguredCargoControls(out GameObject root, out GameObject portDoor, out GameObject starboardDoor);
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);

            try
            {
                InvokePrivate(controls, "AssumeControl", playerInput);
                InvokePrivate(controls, "AnimateDoors", 1f);

                NUnitAssert.IsTrue(controls.HasControllingPlayer);
                NUnitAssert.IsTrue(controls.DoorsOpen);
                NUnitAssert.IsTrue(CargoBayControls.TryGetActiveCargoBay(playerInput.playerIndex, out CargoBayControls active));
                NUnitAssert.AreSame(controls, active);
                NUnitAssert.IsNotNull(playerInput.currentActionMap);
                NUnitAssert.AreEqual(Strings.CraneControls, playerInput.currentActionMap.name);
                NUnitAssert.AreEqual(90f, NormalizeAngle(portDoor.transform.localEulerAngles.z), 0.1f);
                NUnitAssert.AreEqual(-90f, NormalizeAngle(starboardDoor.transform.localEulerAngles.z), 0.1f);
            }
            finally
            {
                InvokePrivate(controls, "ReleaseControl", false, "test_cleanup");
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReleasingCargoControl_ClosesDoorsAndRestoresThirdPersonControls()
        {
            CargoBayControls controls = CreateConfiguredCargoControls(out GameObject root, out GameObject portDoor, out _);
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);

            try
            {
                InvokePrivate(controls, "AssumeControl", playerInput);
                InvokePrivate(controls, "AnimateDoors", 0.25f);
                float partialOpenAngle = Mathf.Abs(NormalizeAngle(portDoor.transform.localEulerAngles.z));
                NUnitAssert.Greater(partialOpenAngle, 0f);
                NUnitAssert.Less(partialOpenAngle, 90f);

                InvokePrivate(controls, "ReleaseControl", true, "test_release");
                InvokePrivate(controls, "AnimateDoors", 1f);

                NUnitAssert.IsFalse(controls.HasControllingPlayer);
                NUnitAssert.IsFalse(controls.DoorsOpen);
                NUnitAssert.IsFalse(CargoBayControls.TryGetActiveCargoBay(playerInput.playerIndex, out _));
                NUnitAssert.IsNotNull(playerInput.currentActionMap);
                NUnitAssert.AreEqual(Strings.ThirdPersonControls, playerInput.currentActionMap.name);
                BoatPassengerVolume passengerVolume = root.GetComponentInChildren<BoatPassengerVolume>(includeInactive: true);
                NUnitAssert.IsNotNull(passengerVolume, "Configured cargo controls root should include a BoatPassengerVolume.");
                NUnitAssert.IsTrue(
                    passengerVolume.IsRiderAttached(playerInput),
                    "Releasing cargo controls should return the player to tracked boat-rider state so they stay with the vessel.");
                NUnitAssert.AreEqual(0f, NormalizeAngle(portDoor.transform.localEulerAngles.z), 0.1f);
            }
            finally
            {
                InvokePrivate(controls, "ReleaseControl", false, "test_cleanup");
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CargoBaySceneTransitionReset_ReleasesControlAndPublishesExitEvent()
        {
            using TestMessageBusScope busScope = new();
            CargoBayControls controls = CreateConfiguredCargoControls(out GameObject root, out _, out _);
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            int exitedPlayerIndex = -1;

            GlobalStaticData.GlobalMessageBus.Subscribe<PlayerExitedCraneEvent>(@event => exitedPlayerIndex = @event.PlayerIndex);

            try
            {
                InvokePrivate(controls, "AssumeControl", playerInput);
                CargoBayControls.ReleaseAllForSceneTransition();

                NUnitAssert.IsFalse(controls.HasControllingPlayer);
                NUnitAssert.IsFalse(CargoBayControls.TryGetActiveCargoBay(playerInput.playerIndex, out _));
                NUnitAssert.IsNotNull(playerInput.currentActionMap);
                NUnitAssert.AreEqual(
                    Strings.ThirdPersonControls,
                    playerInput.currentActionMap.name,
                    "Scene transitions should return crane players to the normal gameplay input map.");
                BoatPassengerVolume passengerVolume = root.GetComponentInChildren<BoatPassengerVolume>(includeInactive: true);
                NUnitAssert.IsNotNull(passengerVolume, "Configured cargo controls root should include a BoatPassengerVolume.");
                NUnitAssert.IsTrue(
                    passengerVolume.IsRiderAttached(playerInput),
                    "Scene-transition cleanup should restore released crane players to tracked boat-rider state so they carry forward with the vessel.");
                NUnitAssert.AreEqual(
                    playerInput.playerIndex,
                    exitedPlayerIndex,
                    "Scene-transition cleanup should publish the crane exit event so cameras and HUD reset.");
            }
            finally
            {
                InvokePrivate(controls, "ReleaseControl", false, "test_cleanup");
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DoorOpenTargets_UseLocalZDefaults()
        {
            CargoBayControls controls = CreateConfiguredCargoControls(out GameObject root, out _, out _);
            try
            {
                Quaternion portOpen = InvokePrivate<Quaternion>(controls, "ResolvePortOpenRotation");
                Quaternion starboardOpen = InvokePrivate<Quaternion>(controls, "ResolveStarboardOpenRotation");

                NUnitAssert.AreEqual(90f, NormalizeAngle(portOpen.eulerAngles.z), 0.1f);
                NUnitAssert.AreEqual(-90f, NormalizeAngle(starboardOpen.eulerAngles.z), 0.1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ActiveCargoControl_BlocksOtherStations()
        {
            CargoBayControls controls = CreateConfiguredCargoControls(out GameObject root, out _, out _);
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);

            try
            {
                InvokePrivate(controls, "AssumeControl", playerInput);
                playerInput.SwitchCurrentActionMap(Strings.ThirdPersonControls);

                bool canTakeHelm = InvokePrivateStatic<bool>(
                    typeof(HelmControl),
                    "CanPlayerTakeHelm",
                    playerInput);
                bool canTakeGun = InvokePrivateStatic<bool>(
                    typeof(DeckMountedGunControl),
                    "CanPlayerTakeGun",
                    playerInput);
                bool canUseAnchor = InvokePrivateStatic<bool>(
                    typeof(AnchorControls),
                    "CanPlayerUseAnchor",
                    playerInput);

                NUnitAssert.IsFalse(canTakeHelm);
                NUnitAssert.IsFalse(canTakeGun);
                NUnitAssert.IsFalse(canUseAnchor);
            }
            finally
            {
                InvokePrivate(controls, "ReleaseControl", false, "test_cleanup");
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerVesselCargoBay_HasDoorReferences()
        {
            GameObject playerVessel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);
            CargoBayControls controls = playerVessel.GetComponentInChildren<CargoBayControls>(includeInactive: true);
            var serializedControls = new SerializedObject(controls);

            NUnitAssert.IsNotNull(serializedControls.FindProperty("_portDoor").objectReferenceValue);
            NUnitAssert.IsNotNull(serializedControls.FindProperty("_starboardDoor").objectReferenceValue);
        }

        [Test]
        public void CargoBayControlsPrefab_HasTriggerAndKinematicRigidbody()
        {
            GameObject cargoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoBayControlsPrefabPath);

            NUnitAssert.IsNotNull(cargoPrefab, "Expected CargoBayControls prefab to exist.");
            NUnitAssert.IsTrue(
                cargoPrefab.GetComponentsInChildren<Collider>(includeInactive: true)
                    .Any(collider => collider != null && collider.isTrigger),
                "CargoBayControls prefab should contain an interaction trigger.");

            Rigidbody rigidbody = cargoPrefab.GetComponent<Rigidbody>();
            NUnitAssert.IsNotNull(rigidbody, "CargoBayControls prefab root should own a kinematic Rigidbody for child trigger callbacks.");
            NUnitAssert.IsTrue(rigidbody.isKinematic);
            NUnitAssert.IsFalse(rigidbody.useGravity);
        }

        [Test]
        public void CraneControlsInputMap_HasActionAndPause()
        {
            string json = File.ReadAllText(InputActionsPath);
            InputActionAsset inputActions = InputActionAsset.FromJson(json);
            try
            {
                InputActionMap craneControls = inputActions.FindActionMap(Strings.CraneControls, throwIfNotFound: false);

                NUnitAssert.IsNotNull(craneControls);
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.ActionAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.MoveAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.HoistAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.SuctionAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.PauseAction, throwIfNotFound: false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputActions);
            }
        }

        [Test]
        public void PlayerContainerPrefab_CanBindCranePause()
        {
            GameObject playerContainerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerContainerPrefabPath);
            GameObject playerContainer = PrefabUtility.InstantiatePrefab(playerContainerPrefab) as GameObject;
            NUnitAssert.IsNotNull(playerContainer, "Expected to instantiate PlayerContainer prefab.");

            try
            {
                PlayerContainer container = playerContainer.GetComponent<PlayerContainer>();
                NUnitAssert.IsNotNull(container);
                InvokePrivate(container, "BindPauseAction");
                InputAction cranePause = GetPrivateField<InputAction>(container, "_cranePauseAction");
                NUnitAssert.IsNotNull(cranePause);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerContainer);
            }
        }

        private static CargoBayControls CreateConfiguredCargoControls(
            out GameObject root,
            out GameObject portDoor,
            out GameObject starboardDoor)
        {
            root = new GameObject("CargoBayControlsTestRoot");
            root.SetActive(false);
            root.AddComponent<PlayerVesselRoot>();
            root.AddComponent<Rigidbody>().isKinematic = true;

            GameObject passengerVolumeObject = new("BoatPassengerVolume");
            passengerVolumeObject.transform.SetParent(root.transform, false);
            BoxCollider passengerTrigger = passengerVolumeObject.AddComponent<BoxCollider>();
            passengerTrigger.isTrigger = true;
            passengerTrigger.size = new Vector3(4f, 4f, 4f);
            BoatPassengerVolume passengerVolume = passengerVolumeObject.AddComponent<BoatPassengerVolume>();

            GameObject triggerObject = new("InteractionTrigger");
            triggerObject.transform.SetParent(root.transform);
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = Vector3.one;

            portDoor = new GameObject("PortDoor");
            starboardDoor = new GameObject("StarboardDoor");
            portDoor.transform.SetParent(root.transform);
            starboardDoor.transform.SetParent(root.transform);

            CargoBayControls controls = root.AddComponent<CargoBayControls>();
            SetPrivateField(controls, "_portDoor", portDoor);
            SetPrivateField(controls, "_starboardDoor", starboardDoor);
            SetPrivateField(controls, "_interactionTrigger", trigger);
            root.SetActive(true);
            InvokePrivate(passengerVolume, "CacheReferences");
            InvokePrivate(controls, "CacheReferences");
            InvokePrivate(controls, "CacheClosedDoorRotations");
            return controls;
        }

        private static PlayerInput CreatePlayerInput(string currentActionMap, out InputActionAsset inputActions)
        {
            var playerObject = new GameObject("TestPlayerInput");
            var playerInput = playerObject.AddComponent<PlayerInput>();
            playerObject.AddComponent<BoxCollider>();

            inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
            inputActions.AddActionMap(Strings.ThirdPersonControls).AddAction(Strings.ActionAction);
            InputActionMap craneControlsMap = inputActions.AddActionMap(Strings.CraneControls);
            craneControlsMap.AddAction(Strings.ActionAction);
            craneControlsMap.AddAction(Strings.MoveAction, InputActionType.Value);
            craneControlsMap.AddAction(Strings.HoistAction, InputActionType.Value);
            craneControlsMap.AddAction(Strings.SuctionAction);
            craneControlsMap.AddAction(Strings.PauseAction);
            inputActions.AddActionMap(Strings.NavalNavigation).AddAction(Strings.ActionAction);
            inputActions.AddActionMap(Strings.BoatGunner).AddAction(Strings.ActionAction);

            playerInput.actions = inputActions;
            playerInput.defaultActionMap = currentActionMap;
            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(currentActionMap);
            playerInput.ActivateInput();
            return playerInput;
        }

        private static float NormalizeAngle(float angleDegrees)
        {
            return angleDegrees > 180f
                ? angleDegrees - 360f
                : angleDegrees;
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }

        private static T InvokePrivateStatic<T>(Type targetType, string methodName, params object[] args)
        {
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected static method '{methodName}' on {targetType.Name}.");
            return (T)method.Invoke(null, args);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class TestMessageBusScope : IDisposable
        {
            private readonly GameObject _globalBusHost;
            private readonly MessageBus _previousGlobalBus;

            public TestMessageBusScope()
            {
                _previousGlobalBus = GlobalStaticData.GlobalMessageBus;
                _globalBusHost = new GameObject("GlobalMessageBus");
                GlobalStaticData.GlobalMessageBus = _globalBusHost.AddComponent<MessageBus>();
            }

            public void Dispose()
            {
                GlobalStaticData.GlobalMessageBus = _previousGlobalBus;
                UnityEngine.Object.DestroyImmediate(_globalBusHost);
            }
        }
    }
}
#endif
