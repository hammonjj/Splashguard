#if UNITY_EDITOR
using Bitbox.Splashguard.Encounters;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEngine;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class EncounterSpawnCollisionValidatorTests
    {
        [Test]
        public void HasBlockingOverlap_ReturnsTrueWhenEnemyHullWouldIntersectStaticGeometry()
        {
            GameObject enemyTemplateRoot = new("EnemyTemplate");
            GameObject enemyHull = new("Hull");
            enemyHull.transform.SetParent(enemyTemplateRoot.transform, false);
            enemyHull.transform.localPosition = new Vector3(1.5f, 0f, 0f);
            BoxCollider hullCollider = enemyHull.AddComponent<BoxCollider>();
            hullCollider.size = new Vector3(4f, 2f, 2f);

            GameObject land = new("Land");
            BoxCollider landCollider = land.AddComponent<BoxCollider>();
            landCollider.size = new Vector3(4f, 4f, 4f);

            try
            {
                bool blocked = EncounterSpawnCollisionValidator.HasBlockingOverlap(
                    enemyTemplateRoot,
                    Vector3.zero,
                    Quaternion.identity);

                NUnitAssert.IsTrue(blocked);
            }
            finally
            {
                Object.DestroyImmediate(land);
                Object.DestroyImmediate(enemyTemplateRoot);
            }
        }

        [Test]
        public void HasBlockingOverlap_ReturnsFalseWhenEnemyHullIsClearOfStaticGeometry()
        {
            GameObject enemyTemplateRoot = new("EnemyTemplate");
            GameObject enemyHull = new("Hull");
            enemyHull.transform.SetParent(enemyTemplateRoot.transform, false);
            BoxCollider hullCollider = enemyHull.AddComponent<BoxCollider>();
            hullCollider.size = new Vector3(2f, 2f, 2f);

            GameObject land = new("Land");
            BoxCollider landCollider = land.AddComponent<BoxCollider>();
            landCollider.size = new Vector3(4f, 4f, 4f);
            land.transform.position = new Vector3(20f, 0f, 0f);

            try
            {
                bool blocked = EncounterSpawnCollisionValidator.HasBlockingOverlap(
                    enemyTemplateRoot,
                    Vector3.zero,
                    Quaternion.identity);

                NUnitAssert.IsFalse(blocked);
            }
            finally
            {
                Object.DestroyImmediate(land);
                Object.DestroyImmediate(enemyTemplateRoot);
            }
        }

        [Test]
        public void HasBlockingOverlap_IgnoresConfiguredZoneCollider()
        {
            GameObject enemyTemplateRoot = new("EnemyTemplate");
            GameObject enemyHull = new("Hull");
            enemyHull.transform.SetParent(enemyTemplateRoot.transform, false);
            BoxCollider hullCollider = enemyHull.AddComponent<BoxCollider>();
            hullCollider.size = new Vector3(2f, 2f, 2f);

            GameObject spawnZone = new("SpawnZone");
            BoxCollider zoneCollider = spawnZone.AddComponent<BoxCollider>();
            zoneCollider.size = new Vector3(10f, 2f, 10f);

            try
            {
                bool blocked = EncounterSpawnCollisionValidator.HasBlockingOverlap(
                    enemyTemplateRoot,
                    Vector3.zero,
                    Quaternion.identity,
                    new[] { zoneCollider });

                NUnitAssert.IsFalse(blocked);
            }
            finally
            {
                Object.DestroyImmediate(spawnZone);
                Object.DestroyImmediate(enemyTemplateRoot);
            }
        }
    }
}
#endif
