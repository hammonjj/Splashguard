using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bitbox.Toymageddon.Nautical
{
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class StormOceanLayerMaterialSync : MonoBehaviour
    {
        private static readonly int WaterColorId = Shader.PropertyToID("_waterColor");
        private static readonly int MinimalTransparencyId = Shader.PropertyToID("_minimalTransparency");

        [SerializeField] private MeshRenderer _sourceOceanRenderer;
        [SerializeField] private MeshRenderer _shallowOceanRenderer;
        [SerializeField] private MeshRenderer _deepOceanRenderer;
        [SerializeField] private MeshRenderer[] _blendOceanRenderers = Array.Empty<MeshRenderer>();
        [SerializeField] private Color _shallowWaterColor = new(0.1f, 0.62f, 0.7f, 1f);
        [SerializeField] private Color _deepWaterColor = new(0.03f, 0.25f, 0.43f, 1f);
        [SerializeField] private Color[] _blendWaterColors = Array.Empty<Color>();
        [SerializeField] private float _blendMinimalTransparency = 0.68f;
        [SerializeField] private bool _syncEveryFrame = true;

        public Color ShallowWaterColor => _shallowWaterColor;
        public Color DeepWaterColor => _deepWaterColor;

        public void Configure(
            MeshRenderer sourceOceanRenderer,
            MeshRenderer shallowOceanRenderer,
            MeshRenderer deepOceanRenderer,
            Color shallowWaterColor,
            Color deepWaterColor)
        {
            Configure(
                sourceOceanRenderer,
                shallowOceanRenderer,
                deepOceanRenderer,
                shallowWaterColor,
                deepWaterColor,
                Array.Empty<MeshRenderer>(),
                Array.Empty<Color>(),
                _blendMinimalTransparency);
        }

        public void Configure(
            MeshRenderer sourceOceanRenderer,
            MeshRenderer shallowOceanRenderer,
            MeshRenderer deepOceanRenderer,
            Color shallowWaterColor,
            Color deepWaterColor,
            MeshRenderer[] blendOceanRenderers,
            Color[] blendWaterColors,
            float blendMinimalTransparency)
        {
            _sourceOceanRenderer = sourceOceanRenderer;
            _shallowOceanRenderer = shallowOceanRenderer;
            _deepOceanRenderer = deepOceanRenderer;
            _shallowWaterColor = shallowWaterColor;
            _deepWaterColor = deepWaterColor;
            _blendOceanRenderers = blendOceanRenderers ?? Array.Empty<MeshRenderer>();
            _blendWaterColors = blendWaterColors ?? Array.Empty<Color>();
            _blendMinimalTransparency = blendMinimalTransparency;
            SyncNow();
        }

        public void SyncNow()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && IsPersistentPrefabAsset())
            {
                return;
            }
#endif

            Material sourceMaterial = _sourceOceanRenderer != null ? _sourceOceanRenderer.sharedMaterial : null;
            if (sourceMaterial == null)
            {
                return;
            }

            SyncLayer(_shallowOceanRenderer, sourceMaterial, _shallowWaterColor);
            SyncLayer(_deepOceanRenderer, sourceMaterial, _deepWaterColor);

            int blendLayerCount = Mathf.Min(_blendOceanRenderers.Length, _blendWaterColors.Length);
            for (int i = 0; i < blendLayerCount; i++)
            {
                SyncLayer(_blendOceanRenderers[i], sourceMaterial, _blendWaterColors[i], true, _blendMinimalTransparency);
            }
        }

        private void OnEnable()
        {
            SyncNow();
        }

        private void OnValidate()
        {
            SyncNow();
        }

        private void LateUpdate()
        {
            if (_syncEveryFrame)
            {
                SyncNow();
            }
        }

        private static void SyncLayer(MeshRenderer targetRenderer, Material sourceMaterial, Color waterColor)
        {
            SyncLayer(targetRenderer, sourceMaterial, waterColor, false, 0f);
        }

        private static void SyncLayer(
            MeshRenderer targetRenderer,
            Material sourceMaterial,
            Color waterColor,
            bool overrideMinimalTransparency,
            float minimalTransparency)
        {
            Material targetMaterial = targetRenderer != null ? targetRenderer.sharedMaterial : null;
            if (targetMaterial == null)
            {
                return;
            }

            targetMaterial.CopyPropertiesFromMaterial(sourceMaterial);
            if (targetMaterial.HasProperty(WaterColorId))
            {
                targetMaterial.SetColor(WaterColorId, waterColor);
            }

            if (overrideMinimalTransparency && targetMaterial.HasProperty(MinimalTransparencyId))
            {
                targetMaterial.SetFloat(MinimalTransparencyId, minimalTransparency);
            }
        }

#if UNITY_EDITOR
        private bool IsPersistentPrefabAsset()
        {
            return EditorUtility.IsPersistent(this)
                || EditorUtility.IsPersistent(gameObject)
                || PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }
#endif
    }
}
