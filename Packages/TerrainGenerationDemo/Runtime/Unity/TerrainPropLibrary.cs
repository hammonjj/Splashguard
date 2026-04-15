using System;
using BitBox.TerrainGeneration.Core;
using UnityEngine;

namespace BitBox.TerrainGeneration.Unity
{
    [CreateAssetMenu(
        fileName = "TerraForgePropLibrary",
        menuName = "BitBox Arcade/TerraForge Prop Library")]
    public sealed class TerrainPropLibrary : ScriptableObject
    {
        [SerializeField] private TerrainPropPrefabEntry[] _prefabs = Array.Empty<TerrainPropPrefabEntry>();

        public bool TryGetPrefab(TerrainPropType type, out GameObject prefab)
        {
            for (int i = 0; i < _prefabs.Length; i++)
            {
                TerrainPropPrefabEntry entry = _prefabs[i];
                if (entry.Type == type && entry.Prefab != null)
                {
                    prefab = entry.Prefab;
                    return true;
                }
            }

            prefab = null;
            return false;
        }
    }

    [Serializable]
    public struct TerrainPropPrefabEntry
    {
        public TerrainPropType Type;
        public GameObject Prefab;
    }
}
