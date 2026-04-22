#if UNITY_EDITOR
using System.Reflection;
using Bitbox;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class PlayerMovementSupportTests
    {
        private const string PlayerContainerPrefabPath = "Assets/Prefabs/PlayerContainer.prefab";

        [Test]
        public void PlayerVesselSupport_DoesNotApplySupportDisplacement()
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
                NUnitAssert.AreEqual(Vector3.zero, supportDisplacement);
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
    }
}
#endif
