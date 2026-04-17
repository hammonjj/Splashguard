#if UNITY_EDITOR
using BitBox.Toymageddon.Weapons;
using Bitbox;
using UnityEditor;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies.Editor
{
    public static class EnemyVesselAiSetupUtility
    {
        private const string EnemyDataFolder = "Assets/Data/Enemies";
        private const string EnemyDataPath = EnemyDataFolder + "/EnemyVesselData.asset";
        private const string BrainConfigPath = EnemyDataFolder + "/EnemyBrainConfig.asset";
        private const string EnemyVesselPrefabPath = "Assets/Prefabs/Enemies/EnemyVessel.prefab";
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel.prefab";
        private const string GatlingWeaponPath = "Assets/Data/Weapons/GatlingGunWeapon.asset";

        [MenuItem("Tools/BitBox Arcade/Configure Enemy Vessel AI")]
        public static void ConfigureEnemyVesselAi()
        {
            EnsureFolder("Assets/Data", "Enemies");
            EnemyVesselData enemyData = LoadOrCreateAsset<EnemyVesselData>(EnemyDataPath);
            EnemyBrainConfig brainConfig = LoadOrCreateAsset<EnemyBrainConfig>(BrainConfigPath);
            WeaponDefinition gatlingWeapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(GatlingWeaponPath);

            ConfigureEnemyVesselPrefab(enemyData, brainConfig, gatlingWeapon);
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
            WeaponDefinition weaponDefinition)
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
                AssignObject(weaponController, "_ownerRoot", prefabRoot);
                AssignObject(health, "_enemyData", enemyData);
                AssignObject(health, "_targetTracker", targetTracker);
                AssignObject(health, "_enemyRoot", prefabRoot);
                AssignObject(destroyOnDeath, "_lifecycleRoot", prefabRoot);

                EnemyProjectileWeaponMount[] mounts = ConfigureWeaponMounts(prefabRoot, enemyData, weaponDefinition);
                AssignObjectArray(weaponController, "_weaponMounts", mounts);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyVesselPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static EnemyProjectileWeaponMount[] ConfigureWeaponMounts(
            GameObject prefabRoot,
            EnemyVesselData enemyData,
            WeaponDefinition weaponDefinition)
        {
            Transform portTurret = FindChildByName(prefabRoot.transform, "ProjectileTurret");
            Transform starboardTurret = FindChildByName(prefabRoot.transform, "ProjectileTurret_1");
            var mounts = new EnemyProjectileWeaponMount[2];
            mounts[0] = ConfigureMount(portTurret, enemyData, weaponDefinition);
            mounts[1] = ConfigureMount(starboardTurret, enemyData, weaponDefinition);
            return mounts;
        }

        private static EnemyProjectileWeaponMount ConfigureMount(
            Transform turret,
            EnemyVesselData enemyData,
            WeaponDefinition weaponDefinition)
        {
            if (turret == null)
            {
                Debug.LogWarning("EnemyVessel prefab is missing an expected ProjectileTurret child.");
                return null;
            }

            DeckMountedGunControl playerGunControl = turret.GetComponent<DeckMountedGunControl>();
            if (playerGunControl != null)
            {
                SetComponentEnabled(playerGunControl, false);
            }

            PlayerWeaponController playerWeaponController = turret.GetComponent<PlayerWeaponController>();
            if (playerWeaponController != null)
            {
                SetComponentEnabled(playerWeaponController, false);
            }

            EnemyProjectileWeaponMount mount = EnsureComponent<EnemyProjectileWeaponMount>(turret.gameObject);
            AssignObject(mount, "_weaponDefinition", weaponDefinition);
            AssignFloat(mount, "_arcHalfAngleDegrees", enemyData != null ? enemyData.WeaponArcHalfAngleDegrees : 90f);
            AssignObject(mount, "_rotationPivot", FindChildByName(turret, "RotationPivot"));
            AssignObject(mount, "_pitchPivot", FindChildByName(turret, "PitchlPivot") ?? FindChildByName(turret, "PitchPivot"));
            AssignObject(mount, "_firePoint", FindChildByName(turret, "FirePoint.001") ?? FindChildByNamePrefix(turret, "FirePoint"));
            return mount;
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
