#if UNITY_EDITOR
using BitBox.Library.Eventing;
using BitBox.Toymageddon.Debugging;
using BitBox.Toymageddon.Weapons;
using Bitbox;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BitBox.Toymageddon.Weapons.Editor
{
    public static class PlayerWeaponAssetSetupUtility
    {
        private const string FirePointName = "FirePoint";
        private const string FirePoint001Name = "FirePoint.001";
        private const string WeaponDataDirectory = "Assets/Data/Weapons";
        private const string WeaponPrefabDirectory = "Assets/Prefabs/Weapons";
        private const string PlayerVesselPrefabPath = "Assets/Prefabs/PlayerVessel.prefab";
        private const string BulletPrefabPath = WeaponPrefabDirectory + "/GatlingBulletProjectile.prefab";
        private const string FireModePath = WeaponDataDirectory + "/GatlingAutomaticFireMode.asset";
        private const string MagazinePath = WeaponDataDirectory + "/GatlingMagazine.asset";
        private const string ReloadPath = WeaponDataDirectory + "/NoReload.asset";
        private const string ProjectilePath = WeaponDataDirectory + "/GatlingBulletProjectile.asset";
        private const string AmmoPath = WeaponDataDirectory + "/GatlingBulletAmmo.asset";
        private const string WeaponPath = WeaponDataDirectory + "/GatlingGunWeapon.asset";
        private static readonly Vector3 VisibleBulletScale = new(0.16f, 0.16f, 0.16f);

        [MenuItem("Tools/Weapons/Configure Default Gatling Gun")]
        public static void ConfigureDefaultGatlingGun()
        {
            EnsureDirectories();

            PhysicalProjectile bulletPrefab = CreateOrUpdateBulletPrefab();
            AutomaticFireModeDefinition fireMode = CreateOrUpdateAsset<AutomaticFireModeDefinition>(FireModePath);
            MagazineDefinition magazine = CreateOrUpdateAsset<MagazineDefinition>(MagazinePath);
            ReloadDefinition reload = CreateOrUpdateAsset<ReloadDefinition>(ReloadPath);
            ProjectileDefinition projectile = CreateOrUpdateAsset<ProjectileDefinition>(ProjectilePath);
            AmmoDefinition ammo = CreateOrUpdateAsset<AmmoDefinition>(AmmoPath);
            WeaponDefinition weapon = CreateOrUpdateAsset<WeaponDefinition>(WeaponPath);

            SetFloat(fireMode, "_roundsPerSecond", 12f);
            SetFloat(fireMode, "_spinUpSeconds", 0.35f);
            SetInt(fireMode, "_maxCatchUpShotsPerFrame", 2);

            SetInt(magazine, "_clipCapacity", 250);
            SetInt(magazine, "_ammoConsumedPerShot", 1);
            SetBool(magazine, "_startsFull", true);

            SetEnum(reload, "_reloadType", (int)ReloadType.NoReload);

            SetObject(projectile, "_projectilePrefab", bulletPrefab);
            SetFloat(projectile, "_speed", 80f);
            SetFloat(projectile, "_lifetimeSeconds", 3f);
            SetInt(projectile, "_collisionMask", ~0);
            SetInt(projectile, "_prewarmCount", 32);
            SetInt(projectile, "_maxPoolSize", 256);

            SetInt(ammo, "_damage", 5);
            SetObject(ammo, "_projectile", projectile);

            SetEnum(weapon, "_weaponType", (int)DebugWeaponType.GatlingGun);
            SetString(weapon, "_displayName", "Gatling Gun");
            SetObject(weapon, "_fireMode", fireMode);
            SetObject(weapon, "_magazine", magazine);
            SetObject(weapon, "_reload", reload);
            SetObject(weapon, "_ammo", ammo);

            WirePlayerVessel(weapon);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured default Gatling weapon data, projectile prefab, and PlayerVessel wiring.");
        }

        public static void RunFromCommandLine()
        {
            ConfigureDefaultGatlingGun();
        }

        private static void EnsureDirectories()
        {
            EnsureDirectory("Assets/Data");
            EnsureDirectory(WeaponDataDirectory);
            EnsureDirectory("Assets/Prefabs");
            EnsureDirectory(WeaponPrefabDirectory);
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureDirectory(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static T CreateOrUpdateAsset<T>(string path) where T : ScriptableObject
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

        private static PhysicalProjectile CreateOrUpdateBulletPrefab()
        {
            Material projectileMaterial = ResolveVisibleProjectileMaterial();
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
            GameObject bulletRoot = prefabAsset != null
                ? PrefabUtility.LoadPrefabContents(BulletPrefabPath)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            bulletRoot.name = "GatlingBulletProjectile";
            bulletRoot.transform.localScale = VisibleBulletScale;

            ConfigureProjectileRenderer(bulletRoot, projectileMaterial);
            ConfigureProjectileTrail(bulletRoot, projectileMaterial);

            Collider collider = bulletRoot.GetComponent<Collider>();
            if (collider == null)
            {
                collider = bulletRoot.AddComponent<SphereCollider>();
            }

            collider.isTrigger = true;

            Rigidbody rigidbody = bulletRoot.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = bulletRoot.AddComponent<Rigidbody>();
            }

            rigidbody.useGravity = false;
            rigidbody.mass = 0.05f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            PhysicalProjectile projectile = bulletRoot.GetComponent<PhysicalProjectile>();
            if (projectile == null)
            {
                projectile = bulletRoot.AddComponent<PhysicalProjectile>();
            }

            PrefabUtility.SaveAsPrefabAsset(bulletRoot, BulletPrefabPath);

            if (prefabAsset != null)
            {
                PrefabUtility.UnloadPrefabContents(bulletRoot);
            }
            else
            {
                Object.DestroyImmediate(bulletRoot);
            }

            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
            return savedPrefab != null ? savedPrefab.GetComponent<PhysicalProjectile>() : null;
        }

        private static void ConfigureProjectileRenderer(GameObject bulletRoot, Material projectileMaterial)
        {
            Renderer renderer = bulletRoot.GetComponent<Renderer>();
            if (renderer != null && projectileMaterial != null)
            {
                renderer.sharedMaterial = projectileMaterial;
            }
        }

        private static void ConfigureProjectileTrail(GameObject bulletRoot, Material projectileMaterial)
        {
            TrailRenderer trailRenderer = bulletRoot.GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = bulletRoot.AddComponent<TrailRenderer>();
            }

            trailRenderer.sharedMaterial = projectileMaterial;
            trailRenderer.time = 0.12f;
            trailRenderer.startWidth = 0.12f;
            trailRenderer.endWidth = 0.01f;
            trailRenderer.minVertexDistance = 0.025f;
            trailRenderer.alignment = LineAlignment.View;
            trailRenderer.textureMode = LineTextureMode.Stretch;
            trailRenderer.numCornerVertices = 2;
            trailRenderer.numCapVertices = 2;
            trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.emitting = false;
            trailRenderer.startColor = new Color(1f, 0.86f, 0.2f, 1f);
            trailRenderer.endColor = new Color(1f, 0.42f, 0.05f, 0f);
        }

        private static Material ResolveVisibleProjectileMaterial()
        {
            return AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat")
                ?? AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        }

        private static void WirePlayerVessel(WeaponDefinition weapon)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerVesselPrefabPath);
            try
            {
                Transform gunTransform = FindChildByName(prefabRoot.transform, "GattlingCanon");
                Assert.IsNotNull(gunTransform, "PlayerVessel prefab is missing GattlingCanon.");
                WireGun(gunTransform.gameObject, weapon);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerVesselPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void WireGun(GameObject gun, WeaponDefinition weapon)
        {
            if (gun.GetComponent<MessageBus>() == null)
            {
                gun.AddComponent<MessageBus>();
            }

            Transform pitchPivot = ResolvePitchPivot(gun);
            Assert.IsNotNull(pitchPivot, "GattlingCanon is missing a pitch pivot.");

            Transform firePoint = FindPreferredFirePoint(gun.transform);
            Assert.IsNotNull(firePoint, "GattlingCanon is missing FirePoint.001.");
            RemoveGeneratedMuzzlePoints(gun.transform, firePoint);

            PlayerWeaponController weaponController = gun.GetComponent<PlayerWeaponController>();
            if (weaponController == null)
            {
                weaponController = gun.AddComponent<PlayerWeaponController>();
            }

            var serializedController = new SerializedObject(weaponController);
            serializedController.FindProperty("_weaponDefinition").objectReferenceValue = weapon;
            serializedController.FindProperty("_useDebugWeaponSelection").boolValue = true;
            SerializedProperty debugWeapons = serializedController.FindProperty("_debugWeaponDefinitions");
            debugWeapons.arraySize = 1;
            debugWeapons.GetArrayElementAtIndex(0).objectReferenceValue = weapon;
            serializedController.FindProperty("_firePoint").objectReferenceValue = firePoint;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform ResolvePitchPivot(GameObject gun)
        {
            DeckMountedGunControl gunControl = gun.GetComponent<DeckMountedGunControl>();
            if (gunControl != null)
            {
                var serializedGunControl = new SerializedObject(gunControl);
                Transform pitchPivot = serializedGunControl.FindProperty("_pitchPivot").objectReferenceValue as Transform;
                if (pitchPivot != null)
                {
                    return pitchPivot;
                }
            }

            return FindChildByName(gun.transform, "PitchPivot")
                ?? FindChildByName(gun.transform, "PitchlPivot");
        }

        private static Transform FindPreferredFirePoint(Transform root)
        {
            return FindChildByName(root, FirePoint001Name)
                ?? FindChildByName(root, FirePointName)
                ?? FindChildByNamePrefix(root, FirePointName);
        }

        private static void RemoveGeneratedMuzzlePoints(Transform root, Transform firePoint)
        {
            if (root == null)
            {
                return;
            }

            for (int childIndex = root.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform child = root.GetChild(childIndex);
                RemoveGeneratedMuzzlePoints(child, firePoint);
            }

            if (root != firePoint && root.name == "MuzzlePoint")
            {
                Object.DestroyImmediate(root.gameObject);
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

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform result = FindChildByName(root.GetChild(childIndex), childName);
                if (result != null)
                {
                    return result;
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

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform result = FindChildByNamePrefix(root.GetChild(childIndex), childNamePrefix);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetInt(Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetString(Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetEnum(Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
