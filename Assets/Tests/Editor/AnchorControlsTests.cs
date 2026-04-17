#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Bitbox;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Eventing;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class AnchorControlsTests
    {
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel.prefab";
        private const string AnchorPrefabPath = "Assets/Prefabs/Props/Anchor.prefab";
        private const string ChainLinkPrefabPath = "Assets/Prefabs/Props/ChainLink.prefab";

        [Test]
        public void PlayerVesselAnchorControls_AreConfiguredForInteraction()
        {
            var playerVessel = LoadRequiredPrefab(PlayerVesselPrefabPath);
            AnchorControls[] anchorControls = playerVessel.GetComponentsInChildren<AnchorControls>(true);

            NUnitAssert.AreEqual(1, anchorControls.Length, "PlayerVessel should contain exactly one AnchorControls.");
            NUnitAssert.IsTrue(anchorControls[0].enabled, "AnchorControls should be enabled on PlayerVessel.");

            Rigidbody triggerRigidbody = anchorControls[0].GetComponent<Rigidbody>();
            NUnitAssert.IsNotNull(triggerRigidbody, "AnchorControls should own a trigger Rigidbody.");
            NUnitAssert.IsTrue(triggerRigidbody.isKinematic, "AnchorControls Rigidbody should not simulate independently.");
            NUnitAssert.IsFalse(triggerRigidbody.useGravity, "AnchorControls Rigidbody should not apply gravity.");

            var serializedAnchorControls = new SerializedObject(anchorControls[0]);
            var anchorPrefab = serializedAnchorControls.FindProperty("_anchorPrefab").objectReferenceValue as GameObject;
            var chainLinkPrefab = serializedAnchorControls.FindProperty("_chainLinkPrefab").objectReferenceValue as GameObject;
            var interactionTrigger =
                serializedAnchorControls.FindProperty("_interactionTrigger").objectReferenceValue as Collider;
            var anchorPath =
                serializedAnchorControls.FindProperty("_anchorPath").objectReferenceValue as SplineContainer;

            NUnitAssert.AreEqual(LoadRequiredPrefab(AnchorPrefabPath), anchorPrefab);
            NUnitAssert.AreEqual(LoadRequiredPrefab(ChainLinkPrefabPath), chainLinkPrefab);
            NUnitAssert.IsNotNull(interactionTrigger, "AnchorControls should serialize its interaction trigger.");
            NUnitAssert.IsTrue(interactionTrigger.isTrigger, "AnchorControls interaction collider should be a trigger.");
            NUnitAssert.IsNotNull(anchorPath, "AnchorControls should serialize the AnchorPath spline.");
            NUnitAssert.AreEqual("AnchorPath", anchorPath.name);
            NUnitAssert.GreaterOrEqual(anchorPath.Splines[0].Count, 2, "AnchorPath should contain a lower/raise path.");

            Collider[] anchorColliders = anchorControls[0].GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < anchorColliders.Length; i++)
            {
                Collider anchorCollider = anchorColliders[i];
                if (anchorCollider != null && !anchorCollider.isTrigger)
                {
                    NUnitAssert.IsFalse(
                        anchorCollider.enabled,
                        "AnchorControls should not keep non-trigger colliders enabled on the boat.");
                }
            }

            NUnitAssert.AreEqual(3f, serializedAnchorControls.FindProperty("_lowerDuration").floatValue, 0.001f);
            NUnitAssert.AreEqual(2f, serializedAnchorControls.FindProperty("_raiseDuration").floatValue, 0.001f);
            NUnitAssert.IsTrue(serializedAnchorControls.FindProperty("_dropAnchorOnSpawn").boolValue);
            NUnitAssert.AreEqual(0.5f, serializedAnchorControls.FindProperty("_spawnAutoDropDelay").floatValue, 0.001f);
            NUnitAssert.AreEqual(3f, serializedAnchorControls.FindProperty("_fixedDropDepth").floatValue, 0.001f);
            NUnitAssert.AreEqual(0.075f, serializedAnchorControls.FindProperty("_chainLinkSpacing").floatValue, 0.001f);
            NUnitAssert.AreEqual(60, serializedAnchorControls.FindProperty("_maxChainLinks").intValue);
            NUnitAssert.IsTrue(serializedAnchorControls.FindProperty("_alignAnchorToPath").boolValue);
            NUnitAssert.AreEqual(
                90f,
                serializedAnchorControls.FindProperty("_chainLinkAlternateTwistDegrees").floatValue,
                0.001f);
            NUnitAssert.AreEqual(0.35f, serializedAnchorControls.FindProperty("_slackRadius").floatValue, 0.001f);
            NUnitAssert.AreEqual(4f, serializedAnchorControls.FindProperty("_horizontalStopTime").floatValue, 0.001f);
            NUnitAssert.AreEqual(1.25f, serializedAnchorControls.FindProperty("_holdSpringAcceleration").floatValue, 0.001f);
            NUnitAssert.AreEqual(6.5f, serializedAnchorControls.FindProperty("_maxAnchorAcceleration").floatValue, 0.001f);
        }

        [Test]
        public void PlayerVesselHullCollider_UsesRootRigidbody()
        {
            var playerVessel = LoadRequiredPrefab(PlayerVesselPrefabPath);
            Rigidbody rootRigidbody = playerVessel.GetComponent<Rigidbody>();
            NUnitAssert.IsNotNull(rootRigidbody, "PlayerVessel should own the single simulated boat Rigidbody.");

            Transform collidersRoot = FindChildByName(playerVessel.transform, "Colliders");
            NUnitAssert.IsNotNull(collidersRoot, "PlayerVessel should keep its hull collider under the Colliders root.");
            NUnitAssert.IsNull(
                collidersRoot.GetComponent<Rigidbody>(),
                "The Colliders root must not have its own Rigidbody, or the hull separates from Storm buoyancy.");

            Transform hullTransform = FindChildByName(playerVessel.transform, "Hull");
            NUnitAssert.IsNotNull(hullTransform, "PlayerVessel should include a Hull collider.");

            MeshCollider hullCollider = hullTransform.GetComponent<MeshCollider>();
            NUnitAssert.IsNotNull(hullCollider, "Hull should use the mesh collider assigned to Storm buoyancy.");
            NUnitAssert.AreEqual(
                rootRigidbody,
                hullCollider.GetComponentInParent<Rigidbody>(),
                "Hull collider should be attached to the root boat Rigidbody.");
        }

        [Test]
        public void PlayerVesselMountedChildren_DoNotOwnDynamicRigidbodies()
        {
            var playerVessel = LoadRequiredPrefab(PlayerVesselPrefabPath);
            Rigidbody rootRigidbody = playerVessel.GetComponent<Rigidbody>();
            NUnitAssert.IsNotNull(rootRigidbody, "PlayerVessel should own the simulated boat Rigidbody.");

            Rigidbody[] rigidbodies = playerVessel.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody childRigidbody = rigidbodies[i];
                if (childRigidbody == null || childRigidbody == rootRigidbody)
                {
                    continue;
                }

                NUnitAssert.IsTrue(
                    childRigidbody.isKinematic,
                    $"Mounted child '{GetTransformPath(childRigidbody.transform)}' must not simulate as a separate dynamic body.");
                NUnitAssert.IsFalse(
                    childRigidbody.useGravity,
                    $"Mounted child '{GetTransformPath(childRigidbody.transform)}' should not apply its own gravity.");
            }
        }

        [Test]
        public void AnchorVisuals_AreCosmeticOnly()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            try
            {
                NUnitAssert.IsTrue(anchorControls.DropAnchor(), "Expected DropAnchor to start lowering from raised state.");
                InvokePrivate(anchorControls, "UpdateAnchorTransition", 1.5f);
                InvokePrivate(anchorControls, "UpdateAnchorVisuals");

                var anchorInstance = GetPrivateField<GameObject>(anchorControls, "_anchorInstance");
                var chainLinkInstances =
                    GetPrivateField<List<GameObject>>(anchorControls, "_chainLinkInstances");

                NUnitAssert.IsNotNull(anchorInstance, "DropAnchor should instantiate an anchor visual.");
                AssertNoActivePhysics(anchorInstance);
                NUnitAssert.IsNotEmpty(chainLinkInstances, "A partially lowered anchor should instantiate visible chain links.");

                for (int i = 0; i < chainLinkInstances.Count; i++)
                {
                    GameObject chainLink = chainLinkInstances[i];
                    if (chainLink != null && chainLink.activeSelf)
                    {
                        AssertNoActivePhysics(chainLink);
                    }
                }
            }
            finally
            {
                InvokePrivate(anchorControls, "DestroyAnchorVisuals");
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void AnchorVisuals_FollowAnchorPathSplineDuringTransition()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            try
            {
                var anchorPath = GetPrivateField<SplineContainer>(anchorControls, "_anchorPath");
                NUnitAssert.IsNotNull(anchorPath, "AnchorControls should resolve an AnchorPath spline.");

                NUnitAssert.IsTrue(anchorControls.DropAnchor(), "Expected DropAnchor to start lowering from raised state.");
                InvokePrivate(anchorControls, "UpdateAnchorTransition", 1.5f);
                InvokePrivate(anchorControls, "UpdateAnchorVisuals");

                var anchorInstance = GetPrivateField<GameObject>(anchorControls, "_anchorInstance");
                NUnitAssert.IsNotNull(anchorInstance, "Lowering should instantiate an anchor visual.");

                Vector3 expectedMidpoint = ToVector3(anchorPath.EvaluatePosition(0.5f));
                NUnitAssert.LessOrEqual(
                    Vector3.Distance(expectedMidpoint, anchorInstance.transform.position),
                    0.001f,
                    "Anchor visual should move along AnchorPath rather than a straight vertical line.");
            }
            finally
            {
                InvokePrivate(anchorControls, "DestroyAnchorVisuals");
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void SpawnAutoDrop_WaitsForConfiguredDelay()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            try
            {
                InvokePrivate(anchorControls, "TryHandleSpawnAutoDrop", 0.49f);
                NUnitAssert.AreEqual(
                    AnchorState.Raised,
                    anchorControls.CurrentState,
                    "Anchor should stay raised until the spawn delay has elapsed.");

                InvokePrivate(anchorControls, "TryHandleSpawnAutoDrop", 0.02f);
                NUnitAssert.AreEqual(
                    AnchorState.Lowering,
                    anchorControls.CurrentState,
                    "Anchor should start lowering once the spawn delay has elapsed.");
            }
            finally
            {
                InvokePrivate(anchorControls, "DestroyAnchorVisuals");
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void AnchorRange_BlocksHelmTakeoverForSamePlayer()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            var playerCollider = playerInput.gameObject.AddComponent<BoxCollider>();

            try
            {
                NUnitAssert.IsNotNull(
                    playerInput.currentActionMap,
                    "Test player should have an active third-person action map.");
                NUnitAssert.AreEqual(Strings.ThirdPersonControls, playerInput.currentActionMap.name);

                Collider interactionTrigger = GetPrivateField<Collider>(anchorControls, "_interactionTrigger");
                PlacePlayerColliderAt(playerCollider, interactionTrigger.bounds.center);

                InvokePrivate(anchorControls, "OnTriggerEntered", playerCollider);
                NUnitAssert.IsTrue(
                    AnchorControls.IsPlayerInAnchorControlRange(playerInput),
                    "AnchorControls should register players inside its interaction trigger.");

                bool canTakeHelm = InvokePrivateStatic<bool>(
                    typeof(HelmControl),
                    "CanPlayerTakeHelm",
                    playerInput);
                NUnitAssert.IsFalse(
                    canTakeHelm,
                    "HelmControl should not consume the same action while the player is in anchor controls range.");
            }
            finally
            {
                InvokePrivate(anchorControls, "OnTriggerExited", playerCollider);
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void AnchorRange_DoesNotBlockDeckGunTakeoverForSamePlayer()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            var playerCollider = playerInput.gameObject.AddComponent<BoxCollider>();

            try
            {
                NUnitAssert.IsNotNull(
                    playerInput.currentActionMap,
                    "Test player should have an active third-person action map.");
                NUnitAssert.AreEqual(Strings.ThirdPersonControls, playerInput.currentActionMap.name);

                Collider interactionTrigger = GetPrivateField<Collider>(anchorControls, "_interactionTrigger");
                PlacePlayerColliderAt(playerCollider, interactionTrigger.bounds.center);

                InvokePrivate(anchorControls, "OnTriggerEntered", playerCollider);
                NUnitAssert.IsTrue(
                    AnchorControls.IsPlayerInAnchorControlRange(playerInput),
                    "AnchorControls should register players inside its interaction trigger.");

                bool canTakeGun = InvokePrivateStatic<bool>(
                    typeof(DeckMountedGunControl),
                    "CanPlayerTakeGun",
                    playerInput);
                NUnitAssert.IsTrue(
                    canTakeGun,
                    "Anchor range alone should not prevent a player from taking a deck gun with its own interaction trigger.");
            }
            finally
            {
                InvokePrivate(anchorControls, "OnTriggerExited", playerCollider);
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void PlayerVesselHelmControl_UsesHelmInteractionTrigger()
        {
            var playerVessel = LoadRequiredPrefab(PlayerVesselPrefabPath);
            var helmControl = playerVessel.GetComponent<HelmControl>();
            NUnitAssert.IsNotNull(helmControl, "PlayerVessel should own HelmControl.");

            var serializedHelmControl = new SerializedObject(helmControl);
            var interactionTrigger =
                serializedHelmControl.FindProperty("_interactionTrigger").objectReferenceValue as Collider;

            NUnitAssert.IsNotNull(interactionTrigger, "HelmControl should serialize its own interaction trigger.");
            NUnitAssert.IsTrue(interactionTrigger.isTrigger, "HelmControl interaction collider should be a trigger.");
            NUnitAssert.AreEqual(
                "HelmControl",
                interactionTrigger.transform.name,
                "HelmControl must not fall back to another station's trigger.");
        }

        [Test]
        public void HelmRange_IgnoresTriggerCallbacksOutsideHelmInteractionVolume()
        {
            var prefab = LoadRequiredPrefab(PlayerVesselPrefabPath);
            var playerVessel = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            NUnitAssert.IsNotNull(playerVessel, "Expected to instantiate PlayerVessel prefab.");

            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);
            var playerCollider = playerInput.gameObject.AddComponent<BoxCollider>();

            try
            {
                var helmControl = playerVessel.GetComponent<HelmControl>();
                NUnitAssert.IsNotNull(helmControl, "PlayerVessel should own HelmControl.");
                InvokePrivate(helmControl, "CacheReferences");

                Collider helmTrigger = GetPrivateField<Collider>(helmControl, "_interactionTrigger");
                NUnitAssert.IsNotNull(helmTrigger, "HelmControl should resolve an interaction trigger.");

                PlacePlayerColliderAt(playerCollider, helmTrigger.bounds.center + (Vector3.right * 10f));
                InvokePrivate(helmControl, "OnTriggerEntered", playerCollider);

                var overlappingPlayers =
                    GetPrivateField<Dictionary<PlayerInput, int>>(helmControl, "_overlappingPlayers");
                NUnitAssert.IsFalse(
                    overlappingPlayers.ContainsKey(playerInput),
                    "HelmControl should ignore trigger callbacks unless the player overlaps the helm trigger.");

                PlacePlayerColliderAt(playerCollider, helmTrigger.bounds.center);
                InvokePrivate(helmControl, "OnTriggerEntered", playerCollider);
                NUnitAssert.IsTrue(
                    overlappingPlayers.ContainsKey(playerInput),
                    "HelmControl should register players that actually overlap the helm trigger.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void HelmRelease_RestoresThirdPersonControlsEvenWithThrottleSet()
        {
            using TestMessageBusScope busScope = new();
            var prefab = LoadRequiredPrefab(PlayerVesselPrefabPath);
            var playerVessel = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            PlayerInput playerInput = CreatePlayerInput(Strings.ThirdPersonControls, out InputActionAsset inputActions);

            try
            {
                NUnitAssert.IsNotNull(playerVessel, "Expected to instantiate PlayerVessel prefab.");
                var helmControl = playerVessel.GetComponent<HelmControl>();
                NUnitAssert.IsNotNull(helmControl, "PlayerVessel should own HelmControl.");
                InvokePrivate(helmControl, "CacheReferences");

                InvokePrivate(helmControl, "AssumeControl", playerInput);
                NUnitAssert.IsNotNull(playerInput.currentActionMap);
                NUnitAssert.AreEqual(
                    Strings.NavalNavigation,
                    playerInput.currentActionMap.name,
                    "Taking the helm should place the player in the naval input map without relying on scene listeners.");

                SetPrivateField(helmControl, "_throttleSetting", 0.65f);
                InvokePrivate(helmControl, "ReleaseControl", false, "test_release");

                NUnitAssert.IsNotNull(playerInput.currentActionMap);
                NUnitAssert.AreEqual(
                    Strings.ThirdPersonControls,
                    playerInput.currentActionMap.name,
                    "Releasing the helm should fully return the player to normal interaction controls, even if the boat is still moving.");
                NUnitAssert.IsFalse(
                    HelmControl.TryGetActiveHelm(playerInput.playerIndex, out _),
                    "Releasing the helm should clear the active station lookup for the player.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerInput.gameObject);
                UnityEngine.Object.DestroyImmediate(inputActions);
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void ChainLinkRotation_AlternatesNinetyDegreesForInterlockingLinks()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            try
            {
                Quaternion firstLinkRotation = InvokePrivate<Quaternion>(
                    anchorControls,
                    "ResolveChainLinkRotation",
                    Vector3.down,
                    0);
                Quaternion secondLinkRotation = InvokePrivate<Quaternion>(
                    anchorControls,
                    "ResolveChainLinkRotation",
                    Vector3.down,
                    1);

                NUnitAssert.AreEqual(
                    90f,
                    Quaternion.Angle(firstLinkRotation, secondLinkRotation),
                    0.001f,
                    "Adjacent chain links should alternate by ninety degrees around the chain direction.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void AnchorTransition_IgnoresToggleUntilLoweringCompletes()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            try
            {
                NUnitAssert.IsTrue(anchorControls.DropAnchor(), "Expected DropAnchor to start lowering from raised state.");
                NUnitAssert.AreEqual(AnchorState.Lowering, anchorControls.CurrentState);
                NUnitAssert.IsFalse(anchorControls.ToggleAnchor(), "Toggle should be ignored while the anchor is lowering.");
                NUnitAssert.AreEqual(AnchorState.Lowering, anchorControls.CurrentState);

                InvokePrivate(anchorControls, "UpdateAnchorTransition", 3.1f);
                NUnitAssert.AreEqual(AnchorState.Dropped, anchorControls.CurrentState);
                NUnitAssert.IsTrue(anchorControls.ToggleAnchor(), "Toggle should raise the anchor after lowering completes.");
                NUnitAssert.AreEqual(AnchorState.Raising, anchorControls.CurrentState);
            }
            finally
            {
                InvokePrivate(anchorControls, "DestroyAnchorVisuals");
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        [Test]
        public void AnchorHold_ResolvesPlanarClampedAccelerationOnly()
        {
            AnchorControls anchorControls = InstantiateConfiguredAnchorControls(out GameObject playerVessel);
            try
            {
                Rigidbody boatRigidbody = playerVessel.GetComponent<Rigidbody>();
                NUnitAssert.IsNotNull(boatRigidbody, "PlayerVessel should own a root Rigidbody.");

                NUnitAssert.IsTrue(anchorControls.DropAnchor(), "Expected DropAnchor to start lowering from raised state.");
                InvokePrivate(anchorControls, "UpdateAnchorTransition", 3.1f);
                NUnitAssert.AreEqual(AnchorState.Dropped, anchorControls.CurrentState);

                boatRigidbody.position += new Vector3(3f, 5f, -1f);
                boatRigidbody.linearVelocity = new Vector3(4f, 6f, -2f);
                boatRigidbody.angularVelocity = new Vector3(1f, 2f, 3f);

                Vector3 acceleration = InvokePrivate<Vector3>(anchorControls, "ResolveAnchorAcceleration");
                NUnitAssert.AreEqual(0f, acceleration.y, 0.0001f, "Anchor acceleration should never pull vertically.");
                NUnitAssert.LessOrEqual(
                    acceleration.magnitude,
                    6.5f + 0.0001f,
                    "Anchor acceleration should respect the configured max acceleration.");

                Vector3 velocityBefore = boatRigidbody.linearVelocity;
                Vector3 angularVelocityBefore = boatRigidbody.angularVelocity;
                Quaternion rotationBefore = boatRigidbody.rotation;

                InvokePrivate(anchorControls, "ApplyAnchorHold");

                NUnitAssert.AreEqual(velocityBefore.y, boatRigidbody.linearVelocity.y, 0.0001f);
                NUnitAssert.AreEqual(angularVelocityBefore, boatRigidbody.angularVelocity);
                NUnitAssert.AreEqual(rotationBefore, boatRigidbody.rotation);
            }
            finally
            {
                InvokePrivate(anchorControls, "DestroyAnchorVisuals");
                UnityEngine.Object.DestroyImmediate(playerVessel);
            }
        }

        private static AnchorControls InstantiateConfiguredAnchorControls(out GameObject playerVessel)
        {
            var prefab = LoadRequiredPrefab(PlayerVesselPrefabPath);
            playerVessel = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            NUnitAssert.IsNotNull(playerVessel, "Expected to instantiate PlayerVessel prefab.");

            AnchorControls anchorControls = playerVessel.GetComponentInChildren<AnchorControls>(includeInactive: true);
            NUnitAssert.IsNotNull(anchorControls, "PlayerVessel should include AnchorControls.");
            InvokePrivate(anchorControls, "CacheReferences");
            return anchorControls;
        }

        private static GameObject LoadRequiredPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            NUnitAssert.IsNotNull(prefab, $"Expected prefab to exist at {prefabPath}.");
            return prefab;
        }

        private static PlayerInput CreatePlayerInput(string currentActionMap, out InputActionAsset inputActions)
        {
            var playerObject = new GameObject("TestPlayerInput");
            var playerInput = playerObject.AddComponent<PlayerInput>();
            inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
            inputActions.AddActionMap(Strings.ThirdPersonControls).AddAction(Strings.ActionAction);
            InputActionMap navalNavigationMap = inputActions.AddActionMap(Strings.NavalNavigation);
            navalNavigationMap.AddAction(Strings.ActionAction);
            navalNavigationMap.AddAction(Strings.ThrottleAction);
            navalNavigationMap.AddAction(Strings.SteeringAction);
            navalNavigationMap.AddAction(Strings.KillThrottleAction);
            inputActions.AddActionMap(Strings.BoatGunner).AddAction(Strings.ActionAction);

            playerInput.actions = inputActions;
            playerInput.defaultActionMap = currentActionMap;
            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(currentActionMap);
            playerInput.ActivateInput();
            return playerInput;
        }

        private static void PlacePlayerColliderAt(BoxCollider playerCollider, Vector3 position)
        {
            playerCollider.size = Vector3.one * 0.05f;
            playerCollider.center = Vector3.zero;
            playerCollider.transform.position = position;
            Physics.SyncTransforms();
        }

        private static void AssertNoActivePhysics(GameObject root)
        {
            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                NUnitAssert.IsTrue(
                    rigidbodies[i] == null || rigidbodies[i].isKinematic,
                    $"Visual '{root.name}' should not contain a dynamic Rigidbody.");
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                NUnitAssert.IsFalse(
                    colliders[i] != null && colliders[i].enabled,
                    $"Visual '{root.name}' should not contain enabled colliders.");
            }
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo methodInfo = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            NUnitAssert.IsNotNull(methodInfo, $"Expected private method '{methodName}' to exist.");
            return methodInfo.Invoke(target, arguments);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] arguments)
        {
            return (T)InvokePrivate(target, methodName, arguments);
        }

        private static T InvokePrivateStatic<T>(Type targetType, string methodName, params object[] arguments)
        {
            MethodInfo methodInfo = targetType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            NUnitAssert.IsNotNull(methodInfo, $"Expected private static method '{methodName}' to exist.");
            return (T)methodInfo.Invoke(null, arguments);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo fieldInfo = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            NUnitAssert.IsNotNull(fieldInfo, $"Expected private field '{fieldName}' to exist.");
            return (T)fieldInfo.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            NUnitAssert.IsNotNull(fieldInfo, $"Expected private field '{fieldName}' to exist.");
            fieldInfo.SetValue(target, value);
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
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

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nestedChild = FindChildByName(child, childName);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }
}
#endif
