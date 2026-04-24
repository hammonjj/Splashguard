using UnityEngine;

namespace Bitbox.Splashguard.Encounters
{
    [CreateAssetMenu(
        fileName = "EncounterDefinition",
        menuName = "Splashguard/Encounters/Encounter Definition")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [SerializeField] private GameObject[] _enemyPrefabs = System.Array.Empty<GameObject>();

        public GameObject[] EnemyPrefabs => _enemyPrefabs;

        public bool IsValidDefinition(out string validationError)
        {
            if (_enemyPrefabs == null || _enemyPrefabs.Length == 0)
            {
                validationError = "Assign at least one enemy prefab.";
                return false;
            }

            for (int i = 0; i < _enemyPrefabs.Length; i++)
            {
                if (_enemyPrefabs[i] != null)
                {
                    continue;
                }

                validationError = $"Enemy prefab entry {i} is null.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }
    }
}
