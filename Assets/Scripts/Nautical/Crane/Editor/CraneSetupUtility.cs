using BitBox.Library.CameraUtils;
using Bitbox.Splashguard.Nautical.Crane;
using Bitbox.Splashguard.Nautical;
using Bitbox.Toymageddon.Nautical;
using UnityEditor;
using UnityEngine;

namespace Bitbox.Splashguard.Nautical.Crane.Editor
{
    public static class CraneSetupUtility
    {
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel/PlayerVessel.prefab";
        private const string FloatingCratePrefabPath = "Assets/Prefabs/Props/FloatingCrate.prefab";
        private const string CableMaterialPath = "Assets/Materials/FX/CraneCableLine.mat";
        private const string PlayerPickupTag = "PlayerPickup";
        private const string CraneModelName = "CraneModel";
        private const string CraneName = "Crane";
        private const string GrabberName = "Grabber";
        private const string GrabberMountName = "GrabberMount";
        private const string CableRendererName = "CableRenderer";
        private const string BottomSuctionSensorName = "BottomSuctionSensor";
        private const string GrabberFloaterName = "GrabberFloater";
        private const string CraneAttachmentPointName = "CraneAttachmentPoint";
        private const string CraneCameraAnchorsName = "CraneCameraAnchors";
        private const string FloatPointCenterName = "FloatPointCenter";
        private const string FloatPointForwardName = "FloatPointForward";
        private const string FloatPointBackName = "FloatPointBack";
        private const string FloatPointLeftName = "FloatPointLeft";
        private const string FloatPointRightName = "FloatPointRight";

        [MenuItem("Tools/BitBox Arcade/Configure Player Vessel Crane")]
        public static void ConfigurePlayerVesselCrane()
        {
            EnsureTag(PlayerPickupTag);
            ConfigureFloatingCrate();
            ConfigurePlayerVesselPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured player vessel crane rig and FloatingCrate pickup target.");
        }

        public static void RunFromCommandLine()
        {
            ConfigurePlayerVesselCrane();
        }

        private static void ConfigureFloatingCrate()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(FloatingCratePrefabPath);
            try
            {
                prefabRoot.tag = PlayerPickupTag;
                CranePickupTarget pickupTarget = EnsureComponent<CranePickupTarget>(prefabRoot);
                Transform attachPoint = FindChildByName(prefabRoot.transform, CraneAttachmentPointName);
                AssignSerializedEnum(pickupTarget, "_attachMode", (int)CranePickupAttachMode.ClosestColliderPoint);
                AssignObject(pickupTarget, "_rigidbody", prefabRoot.GetComponent<Rigidbody>());
                AssignObject(pickupTarget, "_attachPoint", attachPoint);
                SimpleFloater floater = prefabRoot.GetComponentInChildren<SimpleFloater>(includeInactive: true);
                if (floater != null)
                {
                    SerializedObject serializedFloater = new(floater);
                    AssignSerializedBool(serializedFloater, "_anchorInPlace", true);
                    AssignSerializedBool(serializedFloater, "_releaseAnchorWhenGrabbed", true);
                    serializedFloater.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(floater);
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, FloatingCratePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigurePlayerVesselPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerVesselPrefabPath);
            try
            {
                CargoBayControls cargoBayControls = prefabRoot.GetComponentInChildren<CargoBayControls>(includeInactive: true);
                Transform craneModel = FindChildByName(prefabRoot.transform, CraneModelName);
                if (craneModel == null)
                {
                    Debug.LogWarning($"PlayerVessel does not contain '{CraneModelName}'. Crane rig was not configured.");
                    return;
                }

                Transform craneVisual = FindChildByName(craneModel, CraneName) ?? craneModel;
                Transform grabberTransform = FindChildByName(craneModel, GrabberName);
                if (grabberTransform == null)
                {
                    Debug.LogWarning($"PlayerVessel crane model does not contain '{GrabberName}'. Crane rig was not configured.");
                    return;
                }

                CraneControlRig rig = EnsureComponent<CraneControlRig>(craneModel.gameObject);
                CraneBoomController boomController = EnsureComponent<CraneBoomController>(craneModel.gameObject);
                CraneCableController cableController = EnsureComponent<CraneCableController>(craneModel.gameObject);
                CraneGrabber grabber = EnsureComponent<CraneGrabber>(grabberTransform.gameObject);

                Rigidbody grabberRigidbody = EnsureComponent<Rigidbody>(grabberTransform.gameObject);
                grabberRigidbody.isKinematic = false;
                grabberRigidbody.useGravity = true;
                grabberRigidbody.mass = 2f;
                grabberRigidbody.linearDamping = 0.35f;
                grabberRigidbody.angularDamping = 0.85f;
                grabberRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                grabberRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                Transform cableAnchor = ResolveGrabberMount(craneVisual, craneModel, grabberTransform);
                Rigidbody cableAnchorRigidbody = EnsureComponent<Rigidbody>(cableAnchor.gameObject);
                cableAnchorRigidbody.isKinematic = true;
                cableAnchorRigidbody.useGravity = false;

                ConfigurableJoint cableJoint = EnsureComponent<ConfigurableJoint>(grabberTransform.gameObject);
                ConfigureCableJoint(cableJoint, cableAnchorRigidbody, cableAnchor, grabberTransform);
                ConfigureCableControllerTuning(cableController, cableAnchor, grabberTransform);

                Collider bottomSensor = ConfigureBottomSuctionSensor(grabberTransform);
                ConfigureGrabberFloater(grabberTransform);
                LineRenderer lineRenderer = ConfigureCableRenderer(craneModel);
                CameraTargetAnchors cameraAnchors = ConfigureCameraAnchors(craneModel, cableAnchor, grabberTransform);

                AssignObject(boomController, "_yawPivot", craneVisual);
                AssignObject(boomController, "_pitchPivot", craneVisual);
                AssignObject(cableController, "_cableAnchor", cableAnchor);
                AssignObject(cableController, "_cableAnchorRigidbody", cableAnchorRigidbody);
                AssignObject(cableController, "_grabberRigidbody", grabberRigidbody);
                AssignObject(cableController, "_cableJoint", cableJoint);
                AssignObject(cableController, "_cableRenderer", lineRenderer);
                AssignObject(grabber, "_grabberRigidbody", grabberRigidbody);
                AssignObject(grabber, "_bottomSuctionSensor", bottomSensor);
                AssignObject(rig, "_boomController", boomController);
                AssignObject(rig, "_cableController", cableController);
                AssignObject(rig, "_grabber", grabber);
                AssignObject(rig, "_cameraAnchors", cameraAnchors);

                if (cargoBayControls != null)
                {
                    AssignObject(cargoBayControls, "_craneRig", rig);
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerVesselPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Transform ResolveGrabberMount(Transform craneVisual, Transform craneModel, Transform grabberTransform)
        {
            Transform grabberMount = FindChildByName(craneVisual, GrabberMountName)
                ?? FindChildByName(craneModel, GrabberMountName);
            if (grabberMount != null)
            {
                return grabberMount;
            }

            Transform createdMount = EnsureChild(craneVisual, GrabberMountName).transform;
            createdMount.position = grabberTransform.position + Vector3.up * 3f;
            Debug.LogWarning(
                $"Crane setup could not find '{GrabberMountName}'. Created '{GrabberMountName}' under {craneVisual.name}.");
            return createdMount;
        }

        private static void ConfigureCableControllerTuning(
            CraneCableController cableController,
            Transform cableAnchor,
            Transform grabberTransform)
        {
            float tautLength = CraneCableUtility.ClampCableLength(
                CraneCableUtility.CalculateTautCableLength(cableAnchor.position, grabberTransform.position, 0f),
                CraneCableUtility.MinimumCableLengthFloor,
                8f);
            SerializedObject serializedObject = new(cableController);
            AssignSerializedFloat(serializedObject, "_minimumCableLength", CraneCableUtility.MinimumCableLengthFloor);
            AssignSerializedFloat(serializedObject, "_defaultCableLength", tautLength);
            AssignSerializedBool(serializedObject, "_captureDefaultLengthFromCurrentDistance", false);
            AssignSerializedBool(serializedObject, "_useCapturedLengthAsMinimum", false);
            AssignSerializedFloat(serializedObject, "_defaultCableSlack", 0f);
            AssignSerializedFloat(serializedObject, "_linearLimitSpring", 0f);
            AssignSerializedFloat(serializedObject, "_linearLimitDamper", 0f);
            AssignSerializedFloat(serializedObject, "_swingDampingPerSecond", 0.35f);
            AssignSerializedFloat(serializedObject, "_angularSwingDampingPerSecond", 0.55f);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cableController);
        }

        private static CameraTargetAnchors ConfigureCameraAnchors(
            Transform craneModel,
            Transform trackingTarget,
            Transform lookAtTarget)
        {
            GameObject anchorsObject = EnsureChild(craneModel, CraneCameraAnchorsName);
            CameraTargetAnchors anchors = anchorsObject.GetComponent<CameraTargetAnchors>();
            if (anchors == null)
            {
                anchors = anchorsObject.AddComponent<InteractionCameraAnchors>();
            }

            AssignObject(anchors, "_trackingTarget", trackingTarget);
            AssignObject(anchors, "_lookAtTarget", lookAtTarget);
            return anchors;
        }

        private static Collider ConfigureBottomSuctionSensor(Transform grabberTransform)
        {
            GameObject sensorObject = EnsureChild(grabberTransform, BottomSuctionSensorName);
            sensorObject.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            sensorObject.transform.localRotation = Quaternion.identity;
            sensorObject.transform.localScale = Vector3.one;

            BoxCollider sensor = sensorObject.GetComponent<BoxCollider>();
            if (sensor == null)
            {
                sensor = sensorObject.AddComponent<BoxCollider>();
            }

            sensor.isTrigger = true;
            sensor.size = new Vector3(0.7f, 0.25f, 0.7f);
            sensor.center = Vector3.zero;
            return sensor;
        }

        private static SimpleFloater ConfigureGrabberFloater(Transform grabberTransform)
        {
            GameObject floaterObject = EnsureChild(grabberTransform, GrabberFloaterName);
            floaterObject.transform.localPosition = Vector3.zero;
            floaterObject.transform.localRotation = Quaternion.identity;
            floaterObject.transform.localScale = Vector3.one;

            ConfigureFloatPoint(floaterObject.transform, FloatPointCenterName, new Vector3(0f, -0.08f, 0f));
            ConfigureFloatPoint(floaterObject.transform, FloatPointForwardName, new Vector3(0f, -0.08f, 0.28f));
            ConfigureFloatPoint(floaterObject.transform, FloatPointBackName, new Vector3(0f, -0.08f, -0.28f));
            ConfigureFloatPoint(floaterObject.transform, FloatPointLeftName, new Vector3(-0.28f, -0.08f, 0f));
            ConfigureFloatPoint(floaterObject.transform, FloatPointRightName, new Vector3(0.28f, -0.08f, 0f));

            SimpleFloater floater = EnsureComponent<SimpleFloater>(floaterObject);
            SerializedObject serializedObject = new(floater);
            AssignSerializedFloat(serializedObject, "_displacementStrength", 1.15f);
            AssignSerializedFloat(serializedObject, "_depthBeforeSubmerged", 0.28f);
            AssignSerializedFloat(serializedObject, "_waterDrag", 1.25f);
            AssignSerializedFloat(serializedObject, "_waterAngularDrag", 1.1f);
            AssignSerializedFloat(serializedObject, "_uprightTorque", 0.2f);
            AssignSerializedFloat(serializedObject, "_maxUprightTorque", 2f);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(floater);
            return floater;
        }

        private static void ConfigureFloatPoint(Transform floaterTransform, string pointName, Vector3 localPosition)
        {
            GameObject point = EnsureChild(floaterTransform, pointName);
            point.transform.localPosition = localPosition;
            point.transform.localRotation = Quaternion.identity;
            point.transform.localScale = Vector3.one;
        }

        private static LineRenderer ConfigureCableRenderer(Transform craneModel)
        {
            GameObject rendererObject = EnsureChild(craneModel, CableRendererName);
            LineRenderer lineRenderer = EnsureComponent<LineRenderer>(rendererObject);
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.035f;
            lineRenderer.endWidth = 0.035f;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = Color.white;
            Material cableMaterial = AssetDatabase.LoadAssetAtPath<Material>(CableMaterialPath);
            if (cableMaterial != null)
            {
                lineRenderer.sharedMaterial = cableMaterial;
            }
            else
            {
                Debug.LogWarning($"Crane cable material was not found at '{CableMaterialPath}'.");
            }

            return lineRenderer;
        }

        private static void ConfigureCableJoint(
            ConfigurableJoint cableJoint,
            Rigidbody cableAnchorRigidbody,
            Transform cableAnchor,
            Transform grabberTransform)
        {
            cableJoint.connectedBody = cableAnchorRigidbody;
            cableJoint.autoConfigureConnectedAnchor = false;
            cableJoint.anchor = Vector3.zero;
            cableJoint.connectedAnchor = Vector3.zero;
            cableJoint.xMotion = ConfigurableJointMotion.Limited;
            cableJoint.yMotion = ConfigurableJointMotion.Limited;
            cableJoint.zMotion = ConfigurableJointMotion.Limited;
            cableJoint.angularXMotion = ConfigurableJointMotion.Free;
            cableJoint.angularYMotion = ConfigurableJointMotion.Free;
            cableJoint.angularZMotion = ConfigurableJointMotion.Free;
            cableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            cableJoint.projectionDistance = 0.005f;
            cableJoint.enableCollision = false;
            SoftJointLimitSpring limitSpring = cableJoint.linearLimitSpring;
            limitSpring.spring = 0f;
            limitSpring.damper = 0f;
            cableJoint.linearLimitSpring = limitSpring;
            SoftJointLimit limit = cableJoint.linearLimit;
            limit.limit = CraneCableUtility.ClampCableLength(
                CraneCableUtility.CalculateTautCableLength(cableAnchor.position, grabberTransform.position, 0f),
                CraneCableUtility.MinimumCableLengthFloor,
                8f);
            cableJoint.linearLimit = limit;
        }

        private static void EnsureTag(string tag)
        {
            SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    return;
                }
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static GameObject EnsureChild(Transform parent, string childName)
        {
            Transform existing = FindDirectChildByName(parent, childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new(childName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
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

        private static Transform FindDirectChildByName(Transform root, string childName)
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
            }

            return null;
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Could not assign '{propertyName}' on {target.GetType().Name}; property was not found.");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning(
                    $"Could not assign '{propertyName}' on {serializedObject.targetObject.GetType().Name}; property was not found.");
                return;
            }

            property.floatValue = value;
        }

        private static void AssignSerializedEnum(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Could not assign '{propertyName}' on {target.GetType().Name}; property was not found.");
                return;
            }

            property.enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning(
                    $"Could not assign '{propertyName}' on {serializedObject.targetObject.GetType().Name}; property was not found.");
                return;
            }

            property.boolValue = value;
        }

    }
}
