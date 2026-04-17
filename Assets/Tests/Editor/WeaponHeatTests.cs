#if UNITY_EDITOR
using System;
using System.Reflection;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.WeaponEvents;
using BitBox.Toymageddon.UserInterface;
using BitBox.Toymageddon.Weapons;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class WeaponHeatTests
    {
        private const string GatlingWeaponPath = "Assets/Data/Weapons/GatlingGunWeapon.asset";
        private const string GatlingHeatPath = "Assets/Data/Weapons/GatlingHeat.asset";
        private const string GatlingCanonPrefabPath = "Assets/Prefabs/Weapons/GattlingCanon.prefab";
        private const string PlayerContainerPrefabPath = "Assets/Prefabs/PlayerContainer.prefab";

        [Test]
        public void HeatUtility_AccumulatesClampsAndCools()
        {
            WeaponHeatDefinition heat = CreateHeatDefinition();
            try
            {
                float currentHeat = 0f;
                for (int i = 0; i < 80; i++)
                {
                    currentHeat = WeaponHeatUtility.AddShotHeat(heat, currentHeat);
                }

                NUnitAssert.AreEqual(100f, currentHeat, 0.001f);
                NUnitAssert.IsTrue(WeaponHeatUtility.IsAtOverheatThreshold(heat, currentHeat));

                currentHeat = WeaponHeatUtility.Cool(heat, currentHeat, isOverheated: false, deltaTime: 1f);
                NUnitAssert.AreEqual(85f, currentHeat, 0.001f);

                currentHeat = WeaponHeatUtility.Cool(heat, currentHeat, isOverheated: true, deltaTime: 1f);
                NUnitAssert.AreEqual(60f, currentHeat, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(heat);
            }
        }

        [Test]
        public void GatlingWeaponAsset_HasHeatDefinition()
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(GatlingWeaponPath);
            WeaponHeatDefinition heat = AssetDatabase.LoadAssetAtPath<WeaponHeatDefinition>(GatlingHeatPath);

            NUnitAssert.IsNotNull(weapon);
            NUnitAssert.IsNotNull(heat);
            NUnitAssert.AreSame(heat, weapon.Heat);
            NUnitAssert.AreEqual(100f, heat.MaxHeat, 0.001f);
            NUnitAssert.AreEqual(1.8f, heat.HeatPerShot, 0.001f);
            NUnitAssert.AreEqual(15f, heat.CoolRatePerSecond, 0.001f);
            NUnitAssert.AreEqual(25f, heat.OverheatedCoolRatePerSecond, 0.001f);
            NUnitAssert.AreEqual(0f, heat.RecoverHeat, 0.001f);
        }

        [Test]
        public void PlayerWeaponController_PublishesHeatEventsOnLocalBusOnly()
        {
            using TestMessageBusScope busScope = new();
            GameObject gatlingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatlingCanonPrefabPath);
            NUnitAssert.IsNotNull(gatlingPrefab);

            GameObject gatling = PrefabUtility.InstantiatePrefab(gatlingPrefab) as GameObject;
            GameObject playerRoot = new("PlayerRoot");
            GameObject ownerRoot = new("OwnerRoot");
            try
            {
                NUnitAssert.IsNotNull(gatling);
                MessageBus localBus = gatling.GetComponent<MessageBus>();
                PlayerWeaponController controller = gatling.GetComponent<PlayerWeaponController>();
                NUnitAssert.IsNotNull(localBus);
                NUnitAssert.IsNotNull(controller);

                int localHeatChanges = 0;
                int localOverheats = 0;
                int globalHeatChanges = 0;
                WeaponHeatChangedEvent lastHeatEvent = null;

                localBus.Subscribe<WeaponHeatChangedEvent>(@event =>
                {
                    localHeatChanges++;
                    lastHeatEvent = @event;
                });
                localBus.Subscribe<WeaponOverheatedEvent>(_ => localOverheats++);
                busScope.GlobalBus.Subscribe<WeaponHeatChangedEvent>(_ => globalHeatChanges++);

                localBus.Publish(new WeaponControlAcquiredEvent(0, playerRoot, gatling, ownerRoot));
                MethodInfo addShotHeat = GetPrivateMethod("AddShotHeat");
                for (int i = 0; i < 60; i++)
                {
                    addShotHeat.Invoke(controller, Array.Empty<object>());
                }

                NUnitAssert.Greater(localHeatChanges, 0);
                NUnitAssert.AreEqual(1, localOverheats);
                NUnitAssert.AreEqual(0, globalHeatChanges);
                NUnitAssert.IsNotNull(lastHeatEvent);
                NUnitAssert.AreEqual(0, lastHeatEvent.PlayerIndex);
                NUnitAssert.IsTrue(lastHeatEvent.IsOverheated);
                NUnitAssert.AreEqual(1f, lastHeatEvent.NormalizedHeat, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gatling);
                UnityEngine.Object.DestroyImmediate(playerRoot);
                UnityEngine.Object.DestroyImmediate(ownerRoot);
            }
        }

        [Test]
        public void PlayerWeaponController_DryFireDoesNotAddHeat()
        {
            using TestMessageBusScope busScope = new();
            GameObject gatlingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatlingCanonPrefabPath);
            NUnitAssert.IsNotNull(gatlingPrefab);

            GameObject gatling = PrefabUtility.InstantiatePrefab(gatlingPrefab) as GameObject;
            GameObject playerRoot = new("PlayerRoot");
            GameObject ownerRoot = new("OwnerRoot");
            try
            {
                NUnitAssert.IsNotNull(gatling);
                MessageBus localBus = gatling.GetComponent<MessageBus>();
                PlayerWeaponController controller = gatling.GetComponent<PlayerWeaponController>();
                NUnitAssert.IsNotNull(localBus);
                NUnitAssert.IsNotNull(controller);

                WeaponHeatChangedEvent lastHeatEvent = null;
                localBus.Subscribe<WeaponHeatChangedEvent>(@event => lastHeatEvent = @event);
                localBus.Publish(new WeaponControlAcquiredEvent(0, playerRoot, gatling, ownerRoot));

                SetPrivateField(controller, "_currentAmmo", 0);
                MethodInfo tryFireShot = GetPrivateMethod("TryFireShot");
                bool fired = (bool)tryFireShot.Invoke(controller, Array.Empty<object>());

                NUnitAssert.IsFalse(fired);
                NUnitAssert.IsNotNull(lastHeatEvent);
                NUnitAssert.AreEqual(0f, lastHeatEvent.CurrentHeat, 0.001f);
                NUnitAssert.IsFalse(lastHeatEvent.IsOverheated);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gatling);
                UnityEngine.Object.DestroyImmediate(playerRoot);
                UnityEngine.Object.DestroyImmediate(ownerRoot);
            }
        }

        [Test]
        public void BoatGunnerHud_BuildsHeatSlider()
        {
            using TestMessageBusScope busScope = new();
            GameObject playerContainerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerContainerPrefabPath);
            NUnitAssert.IsNotNull(playerContainerPrefab);

            GameObject playerContainer = PrefabUtility.InstantiatePrefab(playerContainerPrefab) as GameObject;
            try
            {
                NUnitAssert.IsNotNull(playerContainer);
                BoatGunnerHudController hud = playerContainer.GetComponent<BoatGunnerHudController>();
                NUnitAssert.IsNotNull(hud);

                Slider heatSlider = playerContainer.GetComponentInChildren<Slider>(includeInactive: true);
                NUnitAssert.IsNotNull(heatSlider, "Boat gunner HUD should build a heat slider under the ammo panel.");
                NUnitAssert.AreEqual("HeatSlider", heatSlider.name);
                NUnitAssert.AreEqual(0f, heatSlider.minValue, 0.001f);
                NUnitAssert.AreEqual(1f, heatSlider.maxValue, 0.001f);
                NUnitAssert.IsFalse(heatSlider.interactable);

                RectTransform heatSliderRect = heatSlider.GetComponent<RectTransform>();
                NUnitAssert.AreEqual(18f, heatSliderRect.sizeDelta.y, 0.001f);

                Text heatStatus = FindTextByName(playerContainer, "HeatStatus");
                NUnitAssert.IsNotNull(heatStatus, "Boat gunner HUD should build an overheated status label.");
                StringAssert.Contains("OVERHEATED", heatStatus.text);
                NUnitAssert.IsFalse(heatStatus.gameObject.activeSelf);

                MethodInfo setHeatSliderState = typeof(BoatGunnerHudController).GetMethod(
                    "SetHeatSliderState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                NUnitAssert.IsNotNull(setHeatSliderState);
                setHeatSliderState.Invoke(hud, new object[] { true, 1f, true });

                NUnitAssert.IsTrue(heatSlider.gameObject.activeSelf);
                NUnitAssert.IsTrue(heatStatus.gameObject.activeSelf);

                Image heatBackground = heatSlider.GetComponent<Image>();
                NUnitAssert.IsNotNull(heatBackground);
                NUnitAssert.Greater(heatBackground.color.r, heatBackground.color.g);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerContainer);
            }
        }

        private static WeaponHeatDefinition CreateHeatDefinition()
        {
            WeaponHeatDefinition heat = ScriptableObject.CreateInstance<WeaponHeatDefinition>();
            SetSerializedFloat(heat, "_maxHeat", 100f);
            SetSerializedFloat(heat, "_heatPerShot", 1.8f);
            SetSerializedFloat(heat, "_coolRatePerSecond", 15f);
            SetSerializedFloat(heat, "_overheatedCoolRatePerSecond", 25f);
            SetSerializedFloat(heat, "_recoverHeat", 0f);
            return heat;
        }

        private static MethodInfo GetPrivateMethod(string methodName)
        {
            MethodInfo method = typeof(PlayerWeaponController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected private method {methodName} on {nameof(PlayerWeaponController)}.");
            return method;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected private field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetSerializedFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            NUnitAssert.IsNotNull(property, $"Expected serialized property {propertyName}.");
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text FindTextByName(GameObject root, string name)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(includeInactive: true);
            foreach (Text text in texts)
            {
                if (text.name == name)
                {
                    return text;
                }
            }

            return null;
        }

        private sealed class TestMessageBusScope : IDisposable
        {
            private readonly GameObject _globalBusHost;
            private readonly MessageBus _previousGlobalBus;

            public TestMessageBusScope()
            {
                _previousGlobalBus = GlobalStaticData.GlobalMessageBus;
                _globalBusHost = new GameObject("GlobalMessageBus");
                GlobalBus = _globalBusHost.AddComponent<MessageBus>();
                GlobalStaticData.GlobalMessageBus = GlobalBus;
            }

            public MessageBus GlobalBus { get; }

            public void Dispose()
            {
                GlobalStaticData.GlobalMessageBus = _previousGlobalBus;
                UnityEngine.Object.DestroyImmediate(_globalBusHost);
            }
        }
    }
}
#endif
