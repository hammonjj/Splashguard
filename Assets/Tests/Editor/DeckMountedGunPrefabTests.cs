#if UNITY_EDITOR
using System;
using System.Reflection;
using Bitbox;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class DeckMountedGunPrefabTests
    {
        private const string GattlingCanonPrefabPath = "Assets/Prefabs/Weapons/GattlingCanon.prefab";
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel.prefab";

        [Test]
        public void GattlingCanonPrefab_UsesOnlyKinematicTriggerRigidbody()
        {
            var prefab = LoadRequiredPrefab(GattlingCanonPrefabPath);
            var rigidbody = prefab.GetComponent<Rigidbody>();

            NUnitAssert.IsNotNull(
                rigidbody,
                "The deck gun root needs a Rigidbody so child trigger callbacks reach DeckMountedGunControl.");
            NUnitAssert.IsTrue(rigidbody.isKinematic, "The deck gun Rigidbody must not simulate independently from the boat.");
            NUnitAssert.IsFalse(rigidbody.useGravity, "The deck gun Rigidbody must not apply its own gravity.");
        }

        [Test]
        public void DeckMountedGunControl_AttachesPhysicalCollidersToPitchPivot()
        {
            var prefab = LoadRequiredPrefab(GattlingCanonPrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            NUnitAssert.IsNotNull(instance, "Expected to instantiate GattlingCanon prefab.");

            try
            {
                var gunControl = instance.GetComponent<DeckMountedGunControl>();
                NUnitAssert.IsNotNull(gunControl, "GattlingCanon should own DeckMountedGunControl.");

                InvokeCacheReferences(gunControl);

                var serializedGunControl = new SerializedObject(gunControl);
                var pitchPivot = serializedGunControl.FindProperty("_pitchPivot").objectReferenceValue as Transform;
                var interactionTrigger =
                    serializedGunControl.FindProperty("_interactionTrigger").objectReferenceValue as Collider;

                NUnitAssert.IsNotNull(pitchPivot, "GattlingCanon should serialize or resolve a pitch pivot.");
                NUnitAssert.IsNotNull(interactionTrigger, "GattlingCanon should serialize or resolve an interaction trigger.");
                NUnitAssert.IsFalse(
                    interactionTrigger.transform.IsChildOf(pitchPivot),
                    "The interaction trigger should remain on the mounted gun root instead of pitching with the barrel.");

                Collider[] colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
                int physicalColliderCount = 0;
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider gunCollider = colliders[i];
                    if (gunCollider == null || gunCollider == interactionTrigger || gunCollider.isTrigger)
                    {
                        continue;
                    }

                    physicalColliderCount++;
                    NUnitAssert.IsTrue(
                        gunCollider.transform.IsChildOf(pitchPivot),
                        $"Physical gun collider '{gunCollider.name}' should follow the pitch pivot.");
                }

                NUnitAssert.Greater(physicalColliderCount, 0, "Expected at least one physical gun collider to validate.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void DeckMountedGunControl_IgnoresSolidGunContactsWithBoat()
        {
            var prefab = LoadRequiredPrefab(PlayerVesselPrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            NUnitAssert.IsNotNull(instance, "Expected to instantiate PlayerVessel prefab.");

            try
            {
                Transform gunTransform = FindChildByName(instance.transform, "GattlingCanon");
                NUnitAssert.IsNotNull(gunTransform, "PlayerVessel should include a deck gun.");

                var gunControl = gunTransform.GetComponent<DeckMountedGunControl>();
                NUnitAssert.IsNotNull(gunControl, "Deck gun should own DeckMountedGunControl.");

                InvokeCacheReferences(gunControl);

                var serializedGunControl = new SerializedObject(gunControl);
                var interactionTrigger =
                    serializedGunControl.FindProperty("_interactionTrigger").objectReferenceValue as Collider;

                Collider[] gunColliders = gunTransform.GetComponentsInChildren<Collider>(includeInactive: true);
                Collider[] boatColliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
                int ignoredPairs = 0;

                for (int gunIndex = 0; gunIndex < gunColliders.Length; gunIndex++)
                {
                    Collider gunCollider = gunColliders[gunIndex];
                    if (gunCollider == null
                        || gunCollider == interactionTrigger
                        || gunCollider.isTrigger
                        || !gunCollider.transform.IsChildOf(gunTransform))
                    {
                        continue;
                    }

                    for (int boatIndex = 0; boatIndex < boatColliders.Length; boatIndex++)
                    {
                        Collider boatCollider = boatColliders[boatIndex];
                        if (boatCollider == null
                            || boatCollider.isTrigger
                            || boatCollider.transform.IsChildOf(gunTransform))
                        {
                            continue;
                        }

                        ignoredPairs++;
                        NUnitAssert.IsTrue(
                            Physics.GetIgnoreCollision(gunCollider, boatCollider),
                            $"Solid gun collider '{gunCollider.name}' should not push boat collider '{boatCollider.name}'.");
                    }
                }

                NUnitAssert.Greater(ignoredPairs, 0, "Expected at least one gun-vs-boat collider pair to validate.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject LoadRequiredPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            NUnitAssert.IsNotNull(prefab, $"Expected prefab to exist at {prefabPath}.");
            return prefab;
        }

        private static void InvokeCacheReferences(DeckMountedGunControl gunControl)
        {
            MethodInfo cacheReferences = typeof(DeckMountedGunControl).GetMethod(
                "CacheReferences",
                BindingFlags.Instance | BindingFlags.NonPublic);

            NUnitAssert.IsNotNull(cacheReferences, "Expected DeckMountedGunControl.CacheReferences to exist.");
            cacheReferences.Invoke(gunControl, Array.Empty<object>());
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform child = root.GetChild(childIndex);
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
