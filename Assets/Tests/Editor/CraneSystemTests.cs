#if UNITY_EDITOR
using System.Reflection;
using System.Linq;
using BitBox.Library.Constants;
using Bitbox.Splashguard.Nautical.Crane;
using Bitbox.Toymageddon.Nautical;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class CraneSystemTests
    {
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel/PlayerVessel.prefab";
        private const string FloatingCratePrefabPath = "Assets/Prefabs/Props/FloatingCrate.prefab";
        private const string InputActionsPath = "Assets/Settings/Input/InputSystem_Actions.inputactions";
        private const string CableMaterialPath = "Assets/Materials/FX/CraneCableLine.mat";

        [Test]
        public void BoomUtility_AppliesInputAndClamps()
        {
            float result = CraneBoomUtility.ApplyAxisInput(0f, 1f, 90f, 2f, -45f, 120f);
            NUnitAssert.AreEqual(120f, result, 0.001f);

            result = CraneBoomUtility.ApplyAxisInput(0f, -1f, 90f, 2f, -45f, 120f);
            NUnitAssert.AreEqual(-45f, result, 0.001f);
        }

        [Test]
        public void CableUtility_HoistPositiveInputShortensCable()
        {
            float raised = CraneCableUtility.ApplyHoistInput(4f, 1f, 2f, 1f, 1f, 8f);
            float lowered = CraneCableUtility.ApplyHoistInput(4f, -1f, 2f, 1f, 1f, 8f);

            NUnitAssert.AreEqual(2f, raised, 0.001f);
            NUnitAssert.AreEqual(6f, lowered, 0.001f);
        }

        [Test]
        public void CableController_AllowsMillimeterMinimumCableLength()
        {
            FieldInfo minimumCableLengthField = typeof(CraneCableController).GetField(
                "_minimumCableLength",
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(minimumCableLengthField);
            MinAttribute minAttribute = minimumCableLengthField.GetCustomAttribute<MinAttribute>();
            NUnitAssert.IsNotNull(minAttribute);
            NUnitAssert.AreEqual(0.001f, CraneCableUtility.MinimumCableLengthFloor, 0.0001f);
            NUnitAssert.AreEqual(0.001f, minAttribute.min, 0.0001f);
            NUnitAssert.AreEqual(
                0.001f,
                CraneCableUtility.ClampCableLength(0.001f, 0.001f, 8f),
                0.0001f);
            NUnitAssert.AreEqual(
                0.001f,
                CraneCableUtility.CalculateTautCableLength(Vector3.zero, Vector3.zero, 0.001f),
                0.0001f);

            GameObject root = new("CraneCableMinimumTestRoot");
            CraneCableController cable = root.AddComponent<CraneCableController>();
            try
            {
                SetPrivateField(cable, "_minimumCableLength", 0.001f);

                MethodInfo onValidate = typeof(CraneCableController).GetMethod(
                    "OnValidate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                NUnitAssert.IsNotNull(onValidate);
                onValidate.Invoke(cable, null);

                NUnitAssert.AreEqual(0.001f, cable.MinimumCableLength, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CableUtility_TautLengthUsesAnchorToGrabberDistance()
        {
            float tautLength = CraneCableUtility.CalculateTautCableLength(
                Vector3.zero,
                new Vector3(0f, -2.5f, 0f),
                0f);
            float tautLengthWithSlack = CraneCableUtility.CalculateTautCableLength(
                Vector3.zero,
                new Vector3(0f, -2.5f, 0f),
                0.1f);

            NUnitAssert.AreEqual(2.5f, tautLength, 0.001f);
            NUnitAssert.AreEqual(2.6f, tautLengthWithSlack, 0.001f);
        }

        [Test]
        public void CableUtility_ProjectsOverextendedGrabberToCableLength()
        {
            bool projected = CraneCableUtility.TryProjectToCableLength(
                Vector3.zero,
                new Vector3(0f, -2f, 0f),
                0.5f,
                0.01f,
                out Vector3 projectedPosition,
                out Vector3 cableDirection,
                out float actualDistance);

            NUnitAssert.IsTrue(projected);
            NUnitAssert.AreEqual(2f, actualDistance, 0.001f);
            NUnitAssert.AreEqual(0.5f, projectedPosition.magnitude, 0.001f);
            NUnitAssert.AreEqual(Vector3.down, cableDirection);

            projected = CraneCableUtility.TryProjectToCableLength(
                Vector3.zero,
                new Vector3(0f, -0.5f, 0f),
                0.5f,
                0.01f,
                out projectedPosition,
                out _,
                out _);

            NUnitAssert.IsFalse(projected);
            NUnitAssert.AreEqual(new Vector3(0f, -0.5f, 0f), projectedPosition);
        }

        [Test]
        public void CableUtility_ExponentialDampingReducesVelocity()
        {
            Vector3 velocity = new(10f, -3f, 1f);

            Vector3 damped = CraneCableUtility.ApplyExponentialDamping(velocity, 0.5f, 1f);
            Vector3 unchanged = CraneCableUtility.ApplyExponentialDamping(velocity, 0f, 1f);

            NUnitAssert.Less(damped.magnitude, velocity.magnitude);
            NUnitAssert.AreEqual(velocity, unchanged);
            NUnitAssert.IsFalse(float.IsNaN(damped.x));
            NUnitAssert.IsFalse(float.IsInfinity(damped.x));
        }

        [Test]
        public void BoomController_ReturnsToCapturedRestPose()
        {
            GameObject root = new("CraneBoomTestRoot");
            GameObject yawPivot = new("YawPivot");
            GameObject pitchPivot = new("PitchPivot");
            yawPivot.transform.SetParent(root.transform);
            pitchPivot.transform.SetParent(yawPivot.transform);
            CraneBoomController boom = root.AddComponent<CraneBoomController>();
            SetPrivateField(boom, "_yawPivot", yawPivot.transform);
            SetPrivateField(boom, "_pitchPivot", pitchPivot.transform);

            try
            {
                boom.CaptureRestPose();
                boom.ApplyControlInput(Vector2.one, 1f);
                NUnitAssert.Greater(Quaternion.Angle(Quaternion.identity, yawPivot.transform.localRotation), 1f);
                NUnitAssert.Greater(Quaternion.Angle(Quaternion.identity, pitchPivot.transform.localRotation), 1f);

                boom.BeginReturnToRest();
                boom.EvaluateReturnToRest(1f);

                NUnitAssert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, yawPivot.transform.localRotation), 0.1f);
                NUnitAssert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, pitchPivot.transform.localRotation), 0.1f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BoomController_DefaultPitchInputIsInverted()
        {
            GameObject root = new("CraneBoomPitchInputTestRoot");
            CraneBoomController boom = root.AddComponent<CraneBoomController>();
            SetPrivateField(boom, "_yawPivot", root.transform);
            SetPrivateField(boom, "_pitchPivot", root.transform);

            try
            {
                boom.CaptureRestPose();
                boom.ApplyControlInput(Vector2.up, 1f);

                NUnitAssert.Less(
                    boom.PitchDegrees,
                    0f,
                    "Positive vertical stick input should invert before applying pitch so stick down no longer raises the boom.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CableController_CapturesTautRestLengthAsMinimum()
        {
            GameObject root = new("CraneCableTestRoot");
            GameObject anchorObject = new("GrabberMount");
            GameObject grabberObject = new("Grabber");
            anchorObject.transform.SetParent(root.transform);
            grabberObject.transform.SetParent(root.transform);
            anchorObject.transform.position = Vector3.up * 2.5f;
            grabberObject.transform.position = Vector3.zero;
            Rigidbody grabberRigidbody = grabberObject.AddComponent<Rigidbody>();
            ConfigurableJoint cableJoint = grabberObject.AddComponent<ConfigurableJoint>();
            CraneCableController cable = root.AddComponent<CraneCableController>();
            SetPrivateField(cable, "_cableAnchor", anchorObject.transform);
            SetPrivateField(cable, "_grabberRigidbody", grabberRigidbody);
            SetPrivateField(cable, "_cableJoint", cableJoint);
            SetPrivateField(cable, "_minimumCableLength", 0.75f);
            SetPrivateField(cable, "_maximumCableLength", 8f);
            SetPrivateField(cable, "_hoistMetersPerSecond", 1f);
            SetPrivateField(cable, "_captureDefaultLengthFromCurrentDistance", true);
            SetPrivateField(cable, "_useCapturedLengthAsMinimum", true);
            SetPrivateField(cable, "_defaultCableSlack", 0f);

            try
            {
                cable.CaptureRestLength();

                NUnitAssert.AreEqual(2.5f, cable.DefaultCableLength, 0.001f);
                NUnitAssert.AreEqual(2.5f, cable.MinimumCableLength, 0.001f);
                NUnitAssert.AreEqual(2.5f, cable.CurrentCableLength, 0.001f);

                cable.ApplyHoistInput(1f, 1f);
                NUnitAssert.AreEqual(2.5f, cable.CurrentCableLength, 0.001f, "Raising at rest should not shorten past the taut cable length.");

                cable.ApplyHoistInput(-1f, 1f);
                NUnitAssert.AreEqual(3.5f, cable.CurrentCableLength, 0.001f, "Lowering should extend the cable from the taut rest length.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Grabber_AttachesOnlyWhileSuctionHeld()
        {
            GameObject grabberObject = new("Grabber");
            Rigidbody grabberRigidbody = grabberObject.AddComponent<Rigidbody>();
            grabberRigidbody.useGravity = true;
            CraneGrabber grabber = grabberObject.AddComponent<CraneGrabber>();
            GameObject sensorObject = new("BottomSuctionSensor");
            sensorObject.transform.SetParent(grabberObject.transform);
            sensorObject.transform.localPosition = Vector3.zero;
            BoxCollider sensor = sensorObject.AddComponent<BoxCollider>();
            sensor.isTrigger = true;
            sensor.size = Vector3.one;

            GameObject pickupObject = new("Pickup");
            Rigidbody pickupRigidbody = pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            pickupObject.tag = "PlayerPickup";
            CranePickupTarget pickupTarget = pickupObject.AddComponent<CranePickupTarget>();
            pickupObject.transform.position = Vector3.down * 0.25f;
            SetPrivateField(grabber, "_bottomSuctionSensor", sensor);

            try
            {
                Physics.SyncTransforms();
                grabber.SetSuctionHeld(true);

                NUnitAssert.IsTrue(grabber.HasHeldPickup);
                NUnitAssert.AreSame(pickupRigidbody, grabber.HeldRigidbody);
                NUnitAssert.IsNotNull(grabberObject.GetComponent<FixedJoint>());
                NUnitAssert.IsTrue(pickupTarget.IsGrabbedByCrane);
                NUnitAssert.AreEqual(sensor.transform.position.x, pickupRigidbody.position.x, 0.001f);
                NUnitAssert.AreEqual(sensor.transform.position.y, pickupRigidbody.position.y, 0.001f);
                NUnitAssert.AreEqual(sensor.transform.position.z, pickupRigidbody.position.z, 0.001f);

                grabber.SetSuctionHeld(false);

                NUnitAssert.IsFalse(grabber.HasHeldPickup);
                NUnitAssert.IsNull(grabberObject.GetComponent<FixedJoint>());
                NUnitAssert.IsNotNull(pickupTarget);
                NUnitAssert.IsFalse(pickupTarget.IsGrabbedByCrane);
            }
            finally
            {
                Object.DestroyImmediate(pickupObject);
                Object.DestroyImmediate(grabberObject);
            }
        }

        [Test]
        public void Grabber_UsesSensorVolumeAsPickupRule()
        {
            GameObject grabberObject = new("Grabber");
            grabberObject.AddComponent<Rigidbody>();
            CraneGrabber grabber = grabberObject.AddComponent<CraneGrabber>();
            GameObject sensorObject = new("BottomSuctionSensor");
            sensorObject.transform.SetParent(grabberObject.transform);
            sensorObject.transform.localPosition = Vector3.zero;
            BoxCollider sensor = sensorObject.AddComponent<BoxCollider>();
            sensor.isTrigger = true;
            sensor.size = Vector3.one;

            GameObject pickupObject = new("Pickup");
            pickupObject.tag = "PlayerPickup";
            pickupObject.transform.position = Vector3.right * 0.45f;
            Rigidbody pickupRigidbody = pickupObject.AddComponent<Rigidbody>();
            BoxCollider pickupCollider = pickupObject.AddComponent<BoxCollider>();
            pickupCollider.size = Vector3.one * 0.2f;
            pickupObject.AddComponent<CranePickupTarget>();
            SetPrivateField(grabber, "_bottomSuctionSensor", sensor);

            try
            {
                Physics.SyncTransforms();
                grabber.SetSuctionHeld(true);

                NUnitAssert.IsTrue(grabber.HasHeldPickup);
                NUnitAssert.AreSame(pickupRigidbody, grabber.HeldRigidbody);
                NUnitAssert.AreEqual(sensor.transform.position.x, pickupRigidbody.position.x, 0.001f);
                NUnitAssert.AreEqual(sensor.transform.position.y, pickupRigidbody.position.y, 0.001f);
                NUnitAssert.AreEqual(sensor.transform.position.z, pickupRigidbody.position.z, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(pickupObject);
                Object.DestroyImmediate(grabberObject);
            }
        }

        [Test]
        public void Grabber_AttachesClosestColliderPointWhenPickupTargetUsesClosestMode()
        {
            GameObject grabberObject = new("Grabber");
            grabberObject.AddComponent<Rigidbody>();
            CraneGrabber grabber = grabberObject.AddComponent<CraneGrabber>();
            GameObject sensorObject = new("BottomSuctionSensor");
            sensorObject.transform.SetParent(grabberObject.transform);
            sensorObject.transform.localPosition = Vector3.zero;
            BoxCollider sensor = sensorObject.AddComponent<BoxCollider>();
            sensor.isTrigger = true;
            sensor.size = Vector3.one;

            GameObject pickupObject = new("Pickup");
            pickupObject.tag = "PlayerPickup";
            pickupObject.transform.position = new Vector3(0.55f, -0.65f, 0f);
            Rigidbody pickupRigidbody = pickupObject.AddComponent<Rigidbody>();
            BoxCollider pickupCollider = pickupObject.AddComponent<BoxCollider>();
            pickupCollider.size = Vector3.one;
            CranePickupTarget pickupTarget = pickupObject.AddComponent<CranePickupTarget>();
            GameObject topAttachObject = new("CraneAttachmentPoint");
            topAttachObject.transform.SetParent(pickupObject.transform);
            topAttachObject.transform.localPosition = Vector3.up * 1.05f;
            SetPrivateField(pickupTarget, "_attachMode", CranePickupAttachMode.ClosestColliderPoint);
            SetPrivateField(pickupTarget, "_attachPoint", topAttachObject.transform);
            SetPrivateField(grabber, "_bottomSuctionSensor", sensor);

            try
            {
                Physics.SyncTransforms();
                Vector3 pickupPositionBeforeGrab = pickupRigidbody.position;
                Vector3 attachPositionBeforeGrab = pickupCollider.ClosestPoint(sensor.transform.position);

                grabber.SetSuctionHeld(true);

                FixedJoint heldJoint = grabberObject.GetComponent<FixedJoint>();
                NUnitAssert.IsTrue(grabber.HasHeldPickup);
                NUnitAssert.AreSame(pickupRigidbody, grabber.HeldRigidbody);
                NUnitAssert.IsNotNull(heldJoint);
                Vector3 worldConnectedAnchor = pickupRigidbody.transform.TransformPoint(heldJoint.connectedAnchor);
                Vector3 expectedPickupPosition = pickupPositionBeforeGrab
                    + sensor.transform.position
                    - attachPositionBeforeGrab;
                NUnitAssert.AreEqual(sensor.transform.position.x, worldConnectedAnchor.x, 0.001f);
                NUnitAssert.AreEqual(sensor.transform.position.y, worldConnectedAnchor.y, 0.001f);
                NUnitAssert.AreEqual(sensor.transform.position.z, worldConnectedAnchor.z, 0.001f);
                NUnitAssert.AreEqual(expectedPickupPosition.x, pickupRigidbody.position.x, 0.001f);
                NUnitAssert.AreEqual(expectedPickupPosition.y, pickupRigidbody.position.y, 0.001f);
                NUnitAssert.AreEqual(expectedPickupPosition.z, pickupRigidbody.position.z, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(pickupObject);
                Object.DestroyImmediate(grabberObject);
            }
        }

        [Test]
        public void CraneControlsInputMap_HasCraneActions()
        {
            string json = System.IO.File.ReadAllText(InputActionsPath);
            InputActionAsset inputActions = InputActionAsset.FromJson(json);
            try
            {
                InputActionMap craneControls = inputActions.FindActionMap(Strings.CraneControls, throwIfNotFound: false);

                NUnitAssert.IsNotNull(craneControls);
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.MoveAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.HoistAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.SuctionAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.ActionAction, throwIfNotFound: false));
                NUnitAssert.IsNotNull(craneControls.FindAction(Strings.PauseAction, throwIfNotFound: false));

                InputBinding[] craneBindings = craneControls.bindings.ToArray();
                InputBinding[] hoistBindings = craneBindings
                    .Where(binding => binding.action == Strings.HoistAction)
                    .ToArray();
                InputBinding[] suctionBindings = craneBindings
                    .Where(binding => binding.action == Strings.SuctionAction)
                    .ToArray();

                NUnitAssert.IsFalse(
                    craneBindings.Any(binding => binding.path != null && binding.path.Contains("<Gamepad>/rightStick")),
                    "CraneControls should leave the right stick free for camera control.");
                NUnitAssert.IsTrue(
                    hoistBindings.Any(binding => binding.path == "<Gamepad>/rightTrigger"
                        && binding.name == "negative"
                        && binding.isPartOfComposite),
                    "Right trigger should lower the grabber by contributing negative hoist input.");
                NUnitAssert.IsTrue(
                    hoistBindings.Any(binding => binding.path == "<Gamepad>/leftTrigger"
                        && binding.name == "positive"
                        && binding.isPartOfComposite),
                    "Left trigger should raise the grabber by contributing positive hoist input.");
                NUnitAssert.IsTrue(
                    hoistBindings.Any(binding => binding.path == "<Keyboard>/q"
                        && binding.name == "negative"
                        && binding.isPartOfComposite),
                    "Keyboard Q should lower the grabber.");
                NUnitAssert.IsTrue(
                    hoistBindings.Any(binding => binding.path == "<Keyboard>/e"
                        && binding.name == "positive"
                        && binding.isPartOfComposite),
                    "Keyboard E should raise the grabber.");
                NUnitAssert.IsTrue(
                    suctionBindings.Any(binding => binding.path == "<Gamepad>/buttonWest"),
                    "Gamepad X / button west should hold suction.");
                NUnitAssert.IsTrue(
                    suctionBindings.Any(binding => binding.path == "<Keyboard>/x"),
                    "Keyboard X should hold suction.");
            }
            finally
            {
                Object.DestroyImmediate(inputActions);
            }
        }

        [Test]
        public void FloatingCratePrefab_IsCranePickup()
        {
            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloatingCratePrefabPath);

            NUnitAssert.IsNotNull(cratePrefab, "Expected FloatingCrate prefab to exist.");
            NUnitAssert.AreEqual("PlayerPickup", cratePrefab.tag);
            CranePickupTarget pickupTarget = cratePrefab.GetComponent<CranePickupTarget>();
            NUnitAssert.IsNotNull(pickupTarget);
            NUnitAssert.AreEqual(CranePickupAttachMode.ClosestColliderPoint, pickupTarget.AttachMode);
            NUnitAssert.IsNotNull(
                cratePrefab.GetComponentsInChildren<Transform>(includeInactive: true)
                    .FirstOrDefault(transform => transform.name == "CraneAttachmentPoint"));
        }

        [Test]
        public void PlayerVesselPrefab_HasCraneModelReadyForSetup()
        {
            GameObject playerVessel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVesselPrefabPath);

            NUnitAssert.IsNotNull(playerVessel, "Expected PlayerVessel prefab to exist.");
            NUnitAssert.IsNotNull(
                playerVessel.GetComponentsInChildren<Transform>(includeInactive: true)
                    .FirstOrDefault(transform => transform.name == "CraneModel"),
                "PlayerVessel should contain the crane model root.");
            NUnitAssert.IsNotNull(
                playerVessel.GetComponentsInChildren<Transform>(includeInactive: true)
                    .FirstOrDefault(transform => transform.name == "Grabber"),
                "PlayerVessel should contain the crane grabber model.");
            Bitbox.Splashguard.Nautical.CargoBayControls cargoBayControls =
                playerVessel.GetComponentInChildren<Bitbox.Splashguard.Nautical.CargoBayControls>(includeInactive: true);
            NUnitAssert.IsNotNull(cargoBayControls, "PlayerVessel should contain CargoBayControls.");
            SerializedObject serializedCargoControls = new(cargoBayControls);
            NUnitAssert.IsNotNull(
                serializedCargoControls.FindProperty("_craneRig").objectReferenceValue,
                "CargoBayControls should have the crane rig assigned directly. Runtime crane bootstrapping is not allowed.");

            CraneControlRig rig = playerVessel.GetComponentInChildren<CraneControlRig>(includeInactive: true);
            NUnitAssert.IsNotNull(rig, "PlayerVessel should contain a serialized CraneControlRig in the prefab hierarchy.");

            NUnitAssert.IsNotNull(playerVessel.GetComponentInChildren<CraneBoomController>(includeInactive: true));
            CraneCableController cableController = playerVessel.GetComponentInChildren<CraneCableController>(includeInactive: true);
            NUnitAssert.IsNotNull(cableController);
            NUnitAssert.AreEqual(
                CraneCableUtility.MinimumCableLengthFloor,
                cableController.MinimumCableLength,
                0.0001f,
                "Crane cable should allow raising to the authored 1mm minimum.");
            NUnitAssert.GreaterOrEqual(
                cableController.DefaultCableLength,
                cableController.MinimumCableLength,
                "Crane cable should start no shorter than the authored minimum.");
            NUnitAssert.LessOrEqual(
                cableController.DefaultCableLength,
                cableController.MaximumCableLength,
                "Crane cable should start no longer than the authored maximum.");
            NUnitAssert.IsNotNull(rig.CameraAnchors, "CraneControlRig should have camera anchors assigned.");
            NUnitAssert.IsNotNull(rig.CameraAnchors.TrackingTarget, "Crane camera tracking target should be assigned.");
            NUnitAssert.IsNotNull(rig.CameraLookAtTarget, "Crane camera look-at target should resolve to the grabber.");

            CraneGrabber grabber = playerVessel.GetComponentInChildren<CraneGrabber>(includeInactive: true);
            NUnitAssert.IsNotNull(grabber, "PlayerVessel crane should contain a CraneGrabber.");
            NUnitAssert.IsNotNull(grabber.GetComponent<Rigidbody>(), "Grabber should have a dynamic Rigidbody.");
            Rigidbody grabberRigidbody = grabber.GetComponent<Rigidbody>();
            NUnitAssert.GreaterOrEqual(grabberRigidbody.linearDamping, 0.3f, "Grabber should have mild damping to keep cable swing readable.");
            NUnitAssert.GreaterOrEqual(grabberRigidbody.angularDamping, 0.5f, "Grabber should have mild angular damping.");

            ConfigurableJoint cableJoint = grabber.GetComponent<ConfigurableJoint>();
            NUnitAssert.IsNotNull(cableJoint, "Grabber should have a cable ConfigurableJoint.");
            NUnitAssert.AreEqual(0f, cableJoint.linearLimitSpring.spring, 0.001f, "Cable joint should use a hard linear limit so cable length only changes through hoist input.");
            NUnitAssert.AreEqual(0f, cableJoint.linearLimitSpring.damper, 0.001f, "Swing damping is handled by the cable controller, not by a soft stretchable limit.");
            NUnitAssert.IsTrue(
                grabber.GetComponentsInChildren<Collider>(includeInactive: true)
                    .Any(collider => collider != null && collider.isTrigger && collider.name == "BottomSuctionSensor"),
                "Grabber should have a bottom trigger sensor.");
            SimpleFloater grabberFloater = grabber.GetComponentInChildren<SimpleFloater>(includeInactive: true);
            NUnitAssert.IsNotNull(grabberFloater, "Grabber should have an authored SimpleFloater child so it floats in water.");
            NUnitAssert.GreaterOrEqual(
                grabberFloater.transform.childCount,
                4,
                "Grabber floater should have multiple float points for stable water behavior.");

            Material cableMaterial = AssetDatabase.LoadAssetAtPath<Material>(CableMaterialPath);
            NUnitAssert.IsNotNull(cableMaterial, "Crane cable material should exist.");

            LineRenderer cableRenderer = playerVessel.GetComponentsInChildren<LineRenderer>(includeInactive: true)
                .FirstOrDefault(renderer => renderer.name == "CableRenderer");
            NUnitAssert.IsNotNull(cableRenderer, "PlayerVessel crane should contain a cable LineRenderer.");
            NUnitAssert.AreSame(cableMaterial, cableRenderer.sharedMaterial);
            NUnitAssert.AreEqual(LineTextureMode.Tile, cableRenderer.textureMode);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
#endif
