#if UNITY_EDITOR
using Bitbox;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class LadderControlTests
    {
        private const string LadderPrefabPath = "Assets/Prefabs/Ladder.prefab";

        [Test]
        public void ProjectNormalized_ClampsPositionsToLadderSegment()
        {
            Vector3 bottom = new(0f, 0f, 0f);
            Vector3 top = new(0f, 10f, 0f);

            NUnitAssert.AreEqual(0f, LadderClimbUtility.ProjectNormalized(bottom, top, new Vector3(0f, -5f, 0f)), 0.001f);
            NUnitAssert.AreEqual(0.5f, LadderClimbUtility.ProjectNormalized(bottom, top, new Vector3(0f, 5f, 0f)), 0.001f);
            NUnitAssert.AreEqual(1f, LadderClimbUtility.ProjectNormalized(bottom, top, new Vector3(0f, 15f, 0f)), 0.001f);
        }

        [Test]
        public void AdvanceNormalized_ReportsTopAndBottomAutoExit()
        {
            float topT = LadderClimbUtility.AdvanceNormalized(
                0.95f,
                1f,
                1.6f,
                1f,
                0.1f,
                out LadderExitDirection topExit);
            float bottomT = LadderClimbUtility.AdvanceNormalized(
                0.05f,
                -1f,
                1.6f,
                1f,
                0.1f,
                out LadderExitDirection bottomExit);

            NUnitAssert.AreEqual(1f, topT, 0.001f);
            NUnitAssert.AreEqual(LadderExitDirection.Top, topExit);
            NUnitAssert.AreEqual(0f, bottomT, 0.001f);
            NUnitAssert.AreEqual(LadderExitDirection.Bottom, bottomExit);
        }

        [Test]
        public void EvaluatePosition_FollowsMovingAnchors()
        {
            Vector3 firstPosition = LadderClimbUtility.EvaluatePosition(
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 10f, 0f),
                0.25f);
            Vector3 movedPosition = LadderClimbUtility.EvaluatePosition(
                new Vector3(10f, 0f, 5f),
                new Vector3(10f, 10f, 5f),
                0.25f);

            NUnitAssert.AreEqual(new Vector3(0f, 2.5f, 0f), firstPosition);
            NUnitAssert.AreEqual(new Vector3(10f, 2.5f, 5f), movedPosition);
        }

        [Test]
        public void LadderPrefab_IsConfiguredForTwoWayInteraction()
        {
            GameObject ladderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LadderPrefabPath);
            NUnitAssert.IsNotNull(ladderPrefab, "Ladder prefab should exist.");

            LadderControl ladderControl = ladderPrefab.GetComponent<LadderControl>();
            NUnitAssert.IsNotNull(ladderControl, "Ladder prefab should include LadderControl on the root.");

            Rigidbody triggerRigidbody = ladderPrefab.GetComponent<Rigidbody>();
            NUnitAssert.IsNotNull(triggerRigidbody, "Ladder root should own the trigger Rigidbody.");
            NUnitAssert.IsTrue(triggerRigidbody.isKinematic, "Ladder trigger Rigidbody should be kinematic.");
            NUnitAssert.IsFalse(triggerRigidbody.useGravity, "Ladder trigger Rigidbody should not use gravity.");

            Transform lowerTrigger = FindChildByName(ladderPrefab.transform, "InteractionTrigger");
            Transform upperTrigger = FindChildByName(ladderPrefab.transform, "UpperInteractionTrigger");
            NUnitAssert.IsNotNull(lowerTrigger, "Ladder should have a lower/water-side interaction trigger.");
            NUnitAssert.IsNotNull(upperTrigger, "Ladder should have an upper/deck-side interaction trigger.");
            AssertTriggerCollider(lowerTrigger);
            AssertTriggerCollider(upperTrigger);

            var serializedControl = new SerializedObject(ladderControl);
            NUnitAssert.IsNotNull(serializedControl.FindProperty("_bottomAnchor").objectReferenceValue, "BottomAnchor should be assigned.");
            NUnitAssert.IsNotNull(serializedControl.FindProperty("_topAnchor").objectReferenceValue, "TopAnchor should be assigned.");
            NUnitAssert.IsNotNull(serializedControl.FindProperty("_bottomExitAnchor").objectReferenceValue, "BottomExitAnchor should be assigned.");
            NUnitAssert.IsNotNull(serializedControl.FindProperty("_topExitAnchor").objectReferenceValue, "TopExitAnchor should be assigned.");
            NUnitAssert.IsTrue(serializedControl.FindProperty("_autoDismountAtEnds").boolValue, "Auto dismount should be enabled for v1.");
            NUnitAssert.AreEqual(1.6f, serializedControl.FindProperty("_climbSpeed").floatValue, 0.001f);
            NUnitAssert.AreEqual(0.35f, serializedControl.FindProperty("_mountSnapDistance").floatValue, 0.001f);

            SerializedProperty triggersProperty = serializedControl.FindProperty("_interactionTriggers");
            NUnitAssert.AreEqual(2, triggersProperty.arraySize, "Ladder should serialize lower and upper interaction triggers.");
        }

        private static void AssertTriggerCollider(Transform triggerTransform)
        {
            Collider triggerCollider = triggerTransform.GetComponent<Collider>();
            NUnitAssert.IsNotNull(triggerCollider, $"{triggerTransform.name} should have a collider.");
            NUnitAssert.IsTrue(triggerCollider.isTrigger, $"{triggerTransform.name} collider should be a trigger.");
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
    }
}
#endif
