using BitBox.TerrainGeneration.Core;
using UnityEngine;

namespace BitBox.TerrainGeneration.Unity
{
    [DisallowMultipleComponent]
    public sealed class TerrainPlaceholderProp : MonoBehaviour
    {
        private const string VisualRootName = "Placeholder Visuals";

        [SerializeField] private TerrainPropType _propType;
        [SerializeField] private Material _primaryMaterial;
        [SerializeField] private Material _secondaryMaterial;

        public TerrainPropType PropType => _propType;

        [ContextMenu("Rebuild Placeholder Visuals")]
        public void EnsureVisuals()
        {
            Transform existing = transform.Find(VisualRootName);
            if (existing != null)
            {
                DestroyUnityObject(existing.gameObject);
            }

            var visualRoot = new GameObject(VisualRootName);
            visualRoot.transform.SetParent(transform, worldPositionStays: false);

            switch (_propType)
            {
                case TerrainPropType.Tree:
                    BuildTree(visualRoot.transform);
                    break;
                case TerrainPropType.Rock:
                    BuildRock(visualRoot.transform);
                    break;
                case TerrainPropType.GrassPatch:
                    BuildGrassPatch(visualRoot.transform);
                    break;
                case TerrainPropType.Driftwood:
                    BuildDriftwood(visualRoot.transform);
                    break;
            }
        }

        private void BuildTree(Transform parent)
        {
            GameObject trunk = CreatePrimitive("Trunk", PrimitiveType.Cylinder, parent, _secondaryMaterial);
            trunk.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            trunk.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);

            GameObject canopy = CreatePrimitive("Canopy", PrimitiveType.Sphere, parent, _primaryMaterial);
            canopy.transform.localPosition = new Vector3(0f, 1.28f, 0f);
            canopy.transform.localScale = new Vector3(0.86f, 0.72f, 0.86f);
        }

        private void BuildRock(Transform parent)
        {
            GameObject rock = CreatePrimitive("Rock", PrimitiveType.Cube, parent, _primaryMaterial);
            rock.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            rock.transform.localRotation = Quaternion.Euler(8f, 25f, 12f);
            rock.transform.localScale = new Vector3(0.82f, 0.42f, 0.62f);
        }

        private void BuildGrassPatch(Transform parent)
        {
            for (int i = 0; i < 5; i++)
            {
                GameObject blade = CreatePrimitive($"Blade {i + 1}", PrimitiveType.Cube, parent, _primaryMaterial);
                float angle = i * 72f;
                blade.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.16f, 0.18f, 0f);
                blade.transform.localRotation = Quaternion.Euler(0f, angle, i % 2 == 0 ? 12f : -12f);
                blade.transform.localScale = new Vector3(0.06f, 0.36f, 0.04f);
            }
        }

        private void BuildDriftwood(Transform parent)
        {
            GameObject log = CreatePrimitive("Driftwood", PrimitiveType.Cylinder, parent, _primaryMaterial);
            log.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            log.transform.localScale = new Vector3(0.14f, 0.62f, 0.14f);
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Transform parent, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, worldPositionStays: false);

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyUnityObject(collider);
            }

            var renderer = primitive.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
