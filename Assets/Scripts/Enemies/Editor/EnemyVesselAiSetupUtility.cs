#if UNITY_EDITOR
using BitBox.Toymageddon.Weapons;
using Bitbox;
using Bitbox.Toymageddon.CameraUtils;
using DamageNumbersPro;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Bitbox.Splashguard.Enemies.Editor
{
    public static class EnemyVesselAiSetupUtility
    {
        private const string EnemyDataFolder = "Assets/Data/Enemies";
        private const string EnemyDataPath = EnemyDataFolder + "/EnemyVesselData.asset";
        private const string BrainConfigPath = EnemyDataFolder + "/EnemyBrainConfig.asset";
        private const string UiPrefabFolder = "Assets/Prefabs/UI";
        private const string EnemyDamageNumberPrefabPath = UiPrefabFolder + "/EnemyDamageNumber.prefab";
        private const string EnemyVesselPrefabPath = "Assets/Prefabs/Enemies/EnemyVessel.prefab";
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel.prefab";
        private const string GatlingWeaponPath = "Assets/Data/Weapons/GatlingGunWeapon.asset";
        private const string WeaponsRootName = "Weapons";
        private const string PortDeckGunName = "PortDeckGun";
        private const string StarboardDeckGunName = "StarboardDeckGun";
        private const string PortTurretName = "ProjectileTurret_1";
        private const string StarboardTurretName = "ProjectileTurret";
        private static readonly Vector3 PortDeckGunLocalPosition = new(-0.78900146f, 0.35140002f, 0.2989998f);
        private static readonly Vector3 StarboardDeckGunLocalPosition = new(0.78900146f, 0.35140002f, 0.2989998f);

        [MenuItem("Tools/BitBox Arcade/Configure Enemy Vessel AI")]
        public static void ConfigureEnemyVesselAi()
        {
            EnsureFolder("Assets/Data", "Enemies");
            EnemyVesselData enemyData = LoadOrCreateAsset<EnemyVesselData>(EnemyDataPath);
            EnemyBrainConfig brainConfig = LoadOrCreateAsset<EnemyBrainConfig>(BrainConfigPath);
            WeaponDefinition gatlingWeapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(GatlingWeaponPath);
            DamageNumber damageNumberPrefab = LoadOrCreateEnemyDamageNumberPrefab();

            ConfigureEnemyVesselPrefab(enemyData, brainConfig, gatlingWeapon, damageNumberPrefab);
            ConfigurePlayerVesselTarget();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured EnemyVessel naval AI data, prefab components, and PlayerVessel targeting marker.");
        }

        public static void RunFromCommandLine()
        {
            ConfigureEnemyVesselAi();
        }

        private static void ConfigureEnemyVesselPrefab(
            EnemyVesselData enemyData,
            EnemyBrainConfig brainConfig,
            WeaponDefinition weaponDefinition,
            DamageNumber damageNumberPrefab)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EnemyVesselPrefabPath);
            try
            {
                GameObject brainObject = EnsureChild(prefabRoot.transform, "EnemyBrain");
                GameObject healthObject = EnsureChild(prefabRoot.transform, "EnemyHealth");
                RemoveComponent<Rigidbody>(brainObject);

                EnemyBrain brain = EnsureExclusiveComponent<EnemyBrain>(prefabRoot, brainObject);
                EnemyTargetTracker targetTracker = EnsureExclusiveComponent<EnemyTargetTracker>(prefabRoot, brainObject);
                EnemyVesselMotor motor = EnsureExclusiveComponent<EnemyVesselMotor>(prefabRoot, brainObject);
                EnemyVesselWeaponController weaponController = EnsureExclusiveComponent<EnemyVesselWeaponController>(prefabRoot, brainObject);
                EnemyDestroyOnDeath destroyOnDeath = EnsureExclusiveComponent<EnemyDestroyOnDeath>(prefabRoot, brainObject);
                EnsureExclusiveComponent<EnemyPatrolAction>(prefabRoot, brainObject);
                EnsureExclusiveComponent<EnemyEngageAction>(prefabRoot, brainObject);
                EnemyHealth health = EnsureExclusiveComponent<EnemyHealth>(prefabRoot, healthObject);

                AssignObject(brain, "_enemyData", enemyData);
                AssignObject(brain, "_brainConfig", brainConfig);
                AssignObject(targetTracker, "_enemyData", enemyData);
                AssignObject(targetTracker, "_enemyRoot", prefabRoot);
                AssignObject(motor, "_enemyData", enemyData);
                AssignObject(motor, "_driveTransform", prefabRoot.transform);
                AssignObject(weaponController, "_enemyData", enemyData);
                AssignObject(weaponController, "_targetTracker", targetTracker);
                AssignObject(health, "_enemyData", enemyData);
                AssignObject(health, "_targetTracker", targetTracker);
                AssignObject(health, "_enemyRoot", prefabRoot);
                AssignObject(destroyOnDeath, "_lifecycleRoot", prefabRoot);
                AssignVector3(destroyOnDeath, "_explosionLocalOffset", new Vector3(0f, 1.2f, 0f));
                AssignFloat(destroyOnDeath, "_explosionLifetimeSeconds", 3f);
                AssignBool(destroyOnDeath, "_useFallbackExplosion", true);
                AssignBool(destroyOnDeath, "_hideRenderersOnDeath", true);
                AssignBool(destroyOnDeath, "_disableCollidersOnDeath", true);

                EnemyHealthWorldDisplay healthDisplay = ConfigureHealthDisplay(healthObject, damageNumberPrefab);
                AssignObject(health, "_worldDisplay", healthDisplay);

                EnemyProjectileWeaponMount[] mounts = ConfigureWeaponMounts(prefabRoot, enemyData, weaponDefinition);
                AssignObjectArray(weaponController, "_weaponMounts", mounts);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyVesselPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static DamageNumber LoadOrCreateEnemyDamageNumberPrefab()
        {
            DamageNumber existing = AssetDatabase.LoadAssetAtPath<DamageNumber>(EnemyDamageNumberPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/Prefabs", "UI");

            var root = new GameObject("EnemyDamageNumber");
            try
            {
                DamageNumberMesh damageNumber = root.AddComponent<DamageNumberMesh>();
                damageNumber.enable3DGame = true;
                damageNumber.faceCameraView = true;
                damageNumber.lookAtCamera = false;
                damageNumber.renderThroughWalls = false;
                damageNumber.consistentScreenSize = true;
                damageNumber.enableVelocity = true;
                damageNumber.enableStartRotation = true;
                damageNumber.minRotation = -6f;
                damageNumber.maxRotation = 6f;
                damageNumber.lifetime = 1.25f;
                damageNumber.SetScale(0.45f);

                TextSettings numberSettings = damageNumber.numberSettings;
                numberSettings.customColor = true;
                numberSettings.color = new Color(1f, 0.34f, 0.16f, 1f);
                numberSettings.size = 0.2f;
                numberSettings.bold = true;
                damageNumber.numberSettings = numberSettings;

                GameObject tmpObject = new("TMP");
                tmpObject.transform.SetParent(root.transform, false);
                TextMeshPro text = tmpObject.AddComponent<TextMeshPro>();
                text.text = "10";
                text.fontSize = 2.5f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(1f, 0.34f, 0.16f, 1f);
                text.rectTransform.sizeDelta = new Vector2(4f, 1f);

                GameObject meshA = new("MeshA");
                meshA.transform.SetParent(root.transform, false);
                meshA.AddComponent<MeshFilter>();
                meshA.AddComponent<MeshRenderer>();

                GameObject meshB = new("MeshB");
                meshB.transform.SetParent(root.transform, false);
                meshB.AddComponent<MeshFilter>();
                meshB.AddComponent<MeshRenderer>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyDamageNumberPrefabPath);
                return prefab != null ? prefab.GetComponent<DamageNumber>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static EnemyHealthWorldDisplay ConfigureHealthDisplay(GameObject healthObject, DamageNumber damageNumberPrefab)
        {
            GameObject displayObject = EnsureChild(healthObject.transform, "WorldHealthDisplay");
            displayObject.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            displayObject.transform.localRotation = Quaternion.identity;
            displayObject.transform.localScale = Vector3.one;
            displayObject.SetActive(true);

            EnemyHealthWorldDisplay display = EnsureComponent<EnemyHealthWorldDisplay>(displayObject);
            LookAtCamera lookAtCamera = EnsureComponent<LookAtCamera>(displayObject);
            AssignBool(lookAtCamera, "_gameCamerasOnly", true);
            AssignBool(lookAtCamera, "_yawOnly", false);
            AssignBool(lookAtCamera, "_invertFacing", false);

            GameObject canvasObject = EnsureUiChild(displayObject.transform, "Canvas");
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one;
            Canvas canvas = EnsureComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
            scaler.dynamicPixelsPerUnit = 100f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            ConfigureRect(canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2.4f, 0.28f));

            Slider healthSlider = ConfigureHealthSlider(canvasObject.transform);

            GameObject damageAnchorObject = EnsureChild(displayObject.transform, "DamageTextAnchor");
            damageAnchorObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            damageAnchorObject.transform.localRotation = Quaternion.identity;
            damageAnchorObject.transform.localScale = Vector3.one;

            AssignObject(display, "_displayRoot", displayObject);
            AssignObject(display, "_canvas", canvas);
            AssignObject(display, "_healthSlider", healthSlider);
            AssignObject(display, "_damageTextAnchor", damageAnchorObject.transform);
            AssignObject(display, "_damageNumberPrefab", damageNumberPrefab);

            healthSlider.SetValueWithoutNotify(1f);
            displayObject.SetActive(false);
            return display;
        }

        private static Slider ConfigureHealthSlider(Transform canvasTransform)
        {
            GameObject sliderObject = EnsureUiChild(canvasTransform, "HealthSlider");
            Slider slider = EnsureComponent<Slider>(sliderObject);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;

            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            ConfigureRect(sliderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2.4f, 0.28f));

            GameObject backgroundObject = EnsureUiChild(sliderObject.transform, "Background");
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            ConfigureRect(backgroundRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image backgroundImage = EnsureComponent<Image>(backgroundObject);
            backgroundImage.color = new Color(0.04f, 0.025f, 0.025f, 0.88f);

            GameObject fillAreaObject = EnsureUiChild(sliderObject.transform, "Fill Area");
            RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            ConfigureRect(fillAreaRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject fillObject = EnsureUiChild(fillAreaObject.transform, "Fill");
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            ConfigureRect(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fillImage = EnsureComponent<Image>(fillObject);
            fillImage.color = new Color(0.82f, 0.08f, 0.05f, 1f);

            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.SetValueWithoutNotify(1f);
            return slider;
        }

        private static EnemyProjectileWeaponMount[] ConfigureWeaponMounts(
            GameObject prefabRoot,
            EnemyVesselData enemyData,
            WeaponDefinition weaponDefinition)
        {
            GameObject weaponsRoot = EnsureChild(prefabRoot.transform, WeaponsRootName);
            GameObject portDeckGun = EnsureChild(weaponsRoot.transform, PortDeckGunName);
            GameObject starboardDeckGun = EnsureChild(weaponsRoot.transform, StarboardDeckGunName);

            ConfigureDeckGunRoot(portDeckGun.transform, PortDeckGunLocalPosition, Quaternion.Euler(0f, 270f, 0f));
            ConfigureDeckGunRoot(starboardDeckGun.transform, StarboardDeckGunLocalPosition, Quaternion.Euler(0f, 90f, 0f));

            Transform portVisual = ResolveDeckGunVisual(prefabRoot.transform, portDeckGun.transform, PortTurretName);
            Transform starboardVisual = ResolveDeckGunVisual(prefabRoot.transform, starboardDeckGun.transform, StarboardTurretName);
            ConfigureDeckGunVisual(portVisual);
            ConfigureDeckGunVisual(starboardVisual);

            var mounts = new EnemyProjectileWeaponMount[2];
            mounts[0] = ConfigureMount(portDeckGun.transform, enemyData, weaponDefinition);
            mounts[1] = ConfigureMount(starboardDeckGun.transform, enemyData, weaponDefinition);
            return mounts;
        }

        private static EnemyProjectileWeaponMount ConfigureMount(
            Transform deckGunRoot,
            EnemyVesselData enemyData,
            WeaponDefinition weaponDefinition)
        {
            if (deckGunRoot == null)
            {
                Debug.LogWarning("EnemyVessel prefab is missing an expected deck gun root.");
                return null;
            }

            DeckMountedGunControl[] playerGunControls = deckGunRoot.GetComponentsInChildren<DeckMountedGunControl>(includeInactive: true);
            for (int i = 0; i < playerGunControls.Length; i++)
            {
                SetComponentEnabled(playerGunControls[i], false);
            }

            PlayerWeaponController[] playerWeaponControllers = deckGunRoot.GetComponentsInChildren<PlayerWeaponController>(includeInactive: true);
            for (int i = 0; i < playerWeaponControllers.Length; i++)
            {
                SetComponentEnabled(playerWeaponControllers[i], false);
            }

            EnemyProjectileWeaponMount mount = EnsureComponent<EnemyProjectileWeaponMount>(deckGunRoot.gameObject);
            RemoveChildMounts(deckGunRoot, mount);
            AssignObject(mount, "_weaponDefinition", weaponDefinition);
            AssignFloat(mount, "_arcHalfAngleDegrees", enemyData != null ? enemyData.WeaponArcHalfAngleDegrees : 90f);
            AssignObject(mount, "_rotationPivot", FindChildByName(deckGunRoot, "RotationPivot"));
            AssignObject(mount, "_pitchPivot", FindChildByName(deckGunRoot, "PitchlPivot") ?? FindChildByName(deckGunRoot, "PitchPivot"));
            AssignObject(mount, "_firePoint", FindChildByName(deckGunRoot, "FirePoint.001") ?? FindChildByNamePrefix(deckGunRoot, "FirePoint"));
            return mount;
        }

        private static void ConfigureDeckGunRoot(Transform deckGunRoot, Vector3 localPosition, Quaternion localRotation)
        {
            deckGunRoot.localPosition = localPosition;
            deckGunRoot.localRotation = localRotation;
            deckGunRoot.localScale = Vector3.one;
        }

        private static Transform ResolveDeckGunVisual(Transform prefabRoot, Transform deckGunRoot, string visualName)
        {
            Transform visual = FindChildByName(deckGunRoot, visualName);
            if (visual != null)
            {
                return visual;
            }

            visual = FindChildByName(prefabRoot, visualName);
            if (visual == null)
            {
                Debug.LogWarning($"EnemyVessel prefab is missing expected weapon visual '{visualName}'.");
                return null;
            }

            visual.SetParent(deckGunRoot, worldPositionStays: false);
            return visual;
        }

        private static void ConfigureDeckGunVisual(Transform visual)
        {
            if (visual == null)
            {
                return;
            }

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        private static void RemoveChildMounts(Transform deckGunRoot, EnemyProjectileWeaponMount rootMount)
        {
            EnemyProjectileWeaponMount[] mounts = deckGunRoot.GetComponentsInChildren<EnemyProjectileWeaponMount>(includeInactive: true);
            for (int i = 0; i < mounts.Length; i++)
            {
                EnemyProjectileWeaponMount mount = mounts[i];
                if (mount != null && mount != rootMount)
                {
                    Object.DestroyImmediate(mount, allowDestroyingAssets: true);
                }
            }
        }

        private static void ConfigurePlayerVesselTarget()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerVesselPrefabPath);
            try
            {
                PlayerVesselTarget target = EnsureComponent<PlayerVesselTarget>(prefabRoot);
                AssignObject(target, "_rootTransform", prefabRoot.transform);
                AssignObject(target, "_aimTransform", prefabRoot.transform);
                AssignBool(target, "_useColliderBoundsAimPoint", true);
                AssignFloat(target, "_colliderAimHeightNormalized", 0.45f);
                AssignFloat(target, "_aimVerticalWorldOffset", 0f);
                AssignVector3(target, "_aimLocalOffset", new Vector3(0f, 0.45f, 0f));
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerVesselPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static T EnsureExclusiveComponent<T>(GameObject prefabRoot, GameObject owner) where T : Component
        {
            T[] components = prefabRoot.GetComponentsInChildren<T>(includeInactive: true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject != owner)
                {
                    Object.DestroyImmediate(components[i], allowDestroyingAssets: true);
                }
            }

            return EnsureComponent<T>(owner);
        }

        private static void RemoveComponent<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component, allowDestroyingAssets: true);
            }
        }

        private static GameObject EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child.gameObject;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject;
        }

        private static GameObject EnsureUiChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                if (child is RectTransform)
                {
                    return child.gameObject;
                }

                Object.DestroyImmediate(child.gameObject, allowDestroyingAssets: true);
            }

            var childObject = new GameObject(childName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject;
        }

        private static void ConfigureRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignObjectArray(Object target, string propertyName, Object[] values)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBool(Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignVector3(Object target, string propertyName, Vector3 value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetComponentEnabled(Behaviour component, bool enabled)
        {
            var serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty("m_Enabled");
            if (property != null)
            {
                property.boolValue = enabled;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(component);
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildByName(root.GetChild(i), childName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindChildByNamePrefix(Transform root, string childNamePrefix)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name.StartsWith(childNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildByNamePrefix(root.GetChild(i), childNamePrefix);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
#endif
