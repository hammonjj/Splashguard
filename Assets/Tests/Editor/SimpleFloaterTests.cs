#if UNITY_EDITOR
using System.Linq;
using Bitbox;
using Bitbox.Toymageddon.Nautical;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using StormBreakers;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class SimpleFloaterTests
    {
        private const string FloaterPrefabPath = "Assets/Prefabs/Props/Floater.prefab";
        private const string FloatingCratePrefabPath = "Assets/Prefabs/Props/FloatingCrate.prefab";

        [Test]
        public void SubmergedFloatPoint_ProducesUpwardBuoyancy()
        {
            float submersion = SimpleFloaterUtility.CalculateSubmersionFraction(
                pointY: -0.1f,
                waterHeight: 0f,
                depthBeforeSubmerged: 0.2f);
            float acceleration = SimpleFloaterUtility.CalculateBuoyancyAcceleration(
                gravityMagnitude: Physics.gravity.y,
                displacementStrength: 1.4f,
                submersionFraction: submersion,
                pointShare: 0.25f);

            NUnitAssert.Greater(submersion, 0f);
            NUnitAssert.Greater(acceleration, 0f);
            NUnitAssert.IsTrue(float.IsFinite(acceleration));
        }

        [Test]
        public void FloatPointAboveWater_ProducesNoBuoyancy()
        {
            float submersion = SimpleFloaterUtility.CalculateSubmersionFraction(
                pointY: 0.2f,
                waterHeight: 0f,
                depthBeforeSubmerged: 0.2f);
            float acceleration = SimpleFloaterUtility.CalculateBuoyancyAcceleration(
                gravityMagnitude: Physics.gravity.y,
                displacementStrength: 1.4f,
                submersionFraction: submersion,
                pointShare: 0.25f);

            NUnitAssert.AreEqual(0f, submersion);
            NUnitAssert.AreEqual(0f, acceleration);
        }

        [Test]
        public void SubmersionFraction_ClampsAtConfiguredDepth()
        {
            float submersion = SimpleFloaterUtility.CalculateSubmersionFraction(
                pointY: -10f,
                waterHeight: 0f,
                depthBeforeSubmerged: 0.2f);

            NUnitAssert.AreEqual(1f, submersion);
        }

        [Test]
        public void DragAndUprightTorque_AreFiniteAndBounded()
        {
            Vector3 drag = SimpleFloaterUtility.CalculateWaterDragVelocityChange(
                velocity: new Vector3(5f, -2f, 3f),
                drag: 2f,
                submersionFraction: 0.5f,
                deltaTime: 0.02f);
            Vector3 torque = SimpleFloaterUtility.CalculateUprightTorque(
                objectUp: new Vector3(0.8f, 0.2f, 0f),
                targetUp: Vector3.up,
                torqueStrength: 20f,
                submersionFraction: 1f,
                maxTorque: 3f);

            AssertFinite(drag);
            AssertFinite(torque);
            NUnitAssert.LessOrEqual(torque.magnitude, 3f + 0.0001f);
            NUnitAssert.Less(Vector3.Dot(drag, new Vector3(5f, -2f, 3f)), 0f);
        }

        [Test]
        public void MissingWaterSampleMath_ProducesNoForce()
        {
            float acceleration = SimpleFloaterUtility.CalculateBuoyancyAcceleration(
                gravityMagnitude: Physics.gravity.y,
                displacementStrength: 1.4f,
                submersionFraction: 0f,
                pointShare: 0.25f);

            NUnitAssert.AreEqual(0f, acceleration);
        }

        [Test]
        public void FloaterPrefab_HasSimpleFloaterAndFloatPoints()
        {
            GameObject floaterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloaterPrefabPath);

            NUnitAssert.IsNotNull(floaterPrefab, "Expected reusable Floater prefab to exist.");
            NUnitAssert.IsNotNull(floaterPrefab.GetComponent<SimpleFloater>());
            NUnitAssert.GreaterOrEqual(CountFloatPointChildren(floaterPrefab.transform), 4);
        }

        [Test]
        public void FloatingCratePrefab_HasRigidbodyColliderAndFloater()
        {
            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloatingCratePrefabPath);

            NUnitAssert.IsNotNull(cratePrefab, "Expected FloatingCrate prefab to exist.");
            NUnitAssert.IsNotNull(cratePrefab.GetComponent<Rigidbody>(), "FloatingCrate root should own the Rigidbody.");
            NUnitAssert.IsTrue(
                cratePrefab.GetComponentsInChildren<Collider>(includeInactive: true)
                    .Any(collider => collider != null && !collider.isTrigger),
                "FloatingCrate should keep at least one non-trigger collider.");

            Transform floater = cratePrefab.transform.Find("Floater");
            NUnitAssert.IsNotNull(floater, "FloatingCrate should have a Floater child.");
            NUnitAssert.IsNotNull(floater.GetComponent<SimpleFloater>());
            NUnitAssert.GreaterOrEqual(CountFloatPointChildren(floater), 4);
        }

        [Test]
        public void FloatingCratePrefab_UsesPropFloaterOnly()
        {
            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloatingCratePrefabPath);

            NUnitAssert.IsNotNull(cratePrefab, "Expected FloatingCrate prefab to exist.");
            NUnitAssert.IsNull(
                cratePrefab.GetComponentInChildren<WaterInteraction>(includeInactive: true),
                "Simple floating props should not use Storm-Breakers WaterInteraction.");
            NUnitAssert.IsNull(
                cratePrefab.GetComponentInChildren<BoyancyController>(includeInactive: true),
                "Simple floating props should not use the legacy BoyancyController.");
        }

        [Test]
        public void FloatingCrateFloater_ResolvesParentRigidbody()
        {
            GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloatingCratePrefabPath);
            GameObject crate = PrefabUtility.InstantiatePrefab(cratePrefab) as GameObject;
            NUnitAssert.IsNotNull(crate, "Expected to instantiate FloatingCrate prefab.");

            try
            {
                Rigidbody rootRigidbody = crate.GetComponent<Rigidbody>();
                SimpleFloater floater = crate.GetComponentInChildren<SimpleFloater>(includeInactive: true);

                NUnitAssert.IsNotNull(rootRigidbody);
                NUnitAssert.IsNotNull(floater);
                NUnitAssert.AreSame(rootRigidbody, floater.GetComponentInParent<Rigidbody>());
            }
            finally
            {
                Object.DestroyImmediate(crate);
            }
        }

        private static int CountFloatPointChildren(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(includeInactive: true)
                .Count(candidate => candidate != null && candidate != root);
        }

        private static void AssertFinite(Vector3 value)
        {
            NUnitAssert.IsTrue(float.IsFinite(value.x));
            NUnitAssert.IsTrue(float.IsFinite(value.y));
            NUnitAssert.IsTrue(float.IsFinite(value.z));
        }
    }
}
#endif
