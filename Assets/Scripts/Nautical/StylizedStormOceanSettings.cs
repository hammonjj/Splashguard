using StormBreakers;
using UnityEngine;
using UnityEngine.VFX;

namespace Bitbox.Toymageddon.Nautical
{
    [ExecuteAlways]
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OceanController))]
    public sealed class StylizedStormOceanSettings : MonoBehaviour
    {
        private static readonly int WaterColorId = Shader.PropertyToID("_waterColor");
        private static readonly int LegacyWaterColorId = Shader.PropertyToID("waterColor");
        private static readonly int MinimalTransparencyId = Shader.PropertyToID("_minimalTransparency");
        private static readonly int MinimalTransparancyId = Shader.PropertyToID("_minimalTransparancy");
        private static readonly int MinAlphaId = Shader.PropertyToID("_minAlpha");
        private static readonly int TransparencyId = Shader.PropertyToID("_transparency");
        private static readonly int TransparancyId = Shader.PropertyToID("_transparancy");
        private static readonly int TransparencyFactorId = Shader.PropertyToID("_transparencyFactor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_smoothness");
        private static readonly int WaterSmoothnessId = Shader.PropertyToID("_waterSmoothness");
        private static readonly int SpecColorId = Shader.PropertyToID("_SpecColor");
        private static readonly int OceanIntensityId = Shader.PropertyToID("_oceanIntensity");
        private static readonly int WavesIntensityId = Shader.PropertyToID("wavesIntensity");
        private static readonly int RipplesIntensityId = Shader.PropertyToID("_ripplesIntensity");
        private static readonly int RippleIntensityId = Shader.PropertyToID("_rippleIntensity");
        private static readonly int Ripples1IntensityId = Shader.PropertyToID("_ripples1Intensity");
        private static readonly int Ripples2IntensityId = Shader.PropertyToID("_ripples2Intensity");
        private static readonly int Ripples3IntensityId = Shader.PropertyToID("_ripples3Intensity");
        private static readonly int Ripples1StrengthId = Shader.PropertyToID("_ripples1Strength");
        private static readonly int Ripples2StrengthId = Shader.PropertyToID("_ripples2Strength");
        private static readonly int Ripples1SizeId = Shader.PropertyToID("_ripples1Size");
        private static readonly int Ripples2SizeId = Shader.PropertyToID("_ripples2Size");
        private static readonly int Ripples3SizeId = Shader.PropertyToID("_ripples3Size");
        private static readonly int FakeHorizonWavesIntensityId = Shader.PropertyToID("_fakeHorizonWavesIntensity");
        private static readonly int FakeHorizonWaveDensityId = Shader.PropertyToID("_fakeHorizonWaveDensity");
        private static readonly int HorizonMaximalSlopeId = Shader.PropertyToID("_horizonMaximalSlope");
        private static readonly int SubSurfaceScatteringId = Shader.PropertyToID("_subSurfaceScattering");
        private static readonly int SubSurfaceScateringId = Shader.PropertyToID("_subSurfaceScatering");
        private static readonly int GroundColorId = Shader.PropertyToID("_groundColor");

        [SerializeField] private bool _applyEveryFrame = true;
        [SerializeField] private bool _disableOceanVfx = true;
        [SerializeField] private Color _waterColor = new(0.03f, 0.25f, 0.43f, 1f);
        [SerializeField, Range(0f, 1f)] private float _waveIntensity = 0.42f;
        [SerializeField] private Vector4 _wavelengths = new(28f, 16f, 8f, 4f);
        [SerializeField] private Vector4 _waveSteepness = new(0.55f, 0.35f, 0.18f, 0.08f);
        [SerializeField] private Vector4 _waveDirections = new(0f, 35f, -40f, 75f);
        [SerializeField] private Vector4 _waveDensities = new(0.96f, 0.96f, 0.95f, 0.95f);
        [SerializeField] private Vector4 _waveSetCounts = new(1.1f, 1f, 1f, 1f);
        [SerializeField, Range(0f, 5f)] private float _breakerSpeedFactor = 0.15f;
        [SerializeField, Range(0f, 5f)] private float _breakerTorqueFactor = 0.08f;
        [SerializeField, Range(0f, 350f)] private float _cameraDisplacement = 45f;
        [SerializeField, Range(0f, 1f)] private float _minimalTransparency = 0.82f;
        [SerializeField, Range(0f, 1f)] private float _surfaceSmoothness = 0.58f;
        [SerializeField, Range(0f, 1f)] private float _waterSmoothness = 0.16f;
        [SerializeField, Range(0f, 1f)] private float _rippleIntensity = 0.08f;
        [SerializeField, Range(0f, 2f)] private float _shaderWaveIntensity = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _subSurfaceScattering = 0.28f;
        [SerializeField] private Color _specularColor = new(0.08f, 0.12f, 0.16f, 1f);
        [SerializeField] private Color _groundColor = new(0.16f, 0.72f, 0.64f, 0f);

        public void Apply()
        {
            OceanController controller = GetComponent<OceanController>();
            if (controller == null)
            {
                return;
            }

            ApplyControllerSettings(controller);
            DisableVfxIfRequested();

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (controller.isActiveAndEnabled && meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                Ocean.ConstructStaticData();
                Ocean.sharedMaterial = meshRenderer.sharedMaterial;
                controller.UpdateWaves();
                controller.UpdateWind();
                controller.UpdateLighting();
            }

            ApplyMaterialSettings();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void LateUpdate()
        {
            if (_applyEveryFrame)
            {
                ApplyMaterialSettings();
            }
        }

        private void ApplyControllerSettings(OceanController controller)
        {
            controller.waterColor = _waterColor;
            controller.waveIntensity = _waveIntensity;
            controller.wavelength0 = Mathf.Max(1f, _wavelengths.x);
            controller.wavelength1 = Mathf.Max(1f, _wavelengths.y);
            controller.wavelength2 = Mathf.Max(1f, _wavelengths.z);
            controller.wavelength3 = Mathf.Max(1f, _wavelengths.w);
            controller.intensity0 = Mathf.Clamp(_waveSteepness.x, 0f, 1.5f);
            controller.intensity1 = Mathf.Clamp(_waveSteepness.y, 0f, 1.5f);
            controller.intensity2 = Mathf.Clamp(_waveSteepness.z, 0f, 1.5f);
            controller.intensity3 = Mathf.Clamp(_waveSteepness.w, 0f, 1.5f);
            controller.direction0 = _waveDirections.x;
            controller.direction1 = _waveDirections.y;
            controller.direction2 = _waveDirections.z;
            controller.direction3 = _waveDirections.w;
            controller.waveDensity0 = Mathf.Clamp(_waveDensities.x, 0.7f, 1f);
            controller.waveDensity1 = Mathf.Clamp(_waveDensities.y, 0.7f, 1f);
            controller.waveDensity2 = Mathf.Clamp(_waveDensities.z, 0.7f, 1f);
            controller.waveDensity3 = Mathf.Clamp(_waveDensities.w, 0.7f, 1f);
            controller.setNumber0 = Mathf.Clamp(_waveSetCounts.x, 1f, 5f);
            controller.setNumber1 = Mathf.Clamp(_waveSetCounts.y, 1f, 5f);
            controller.setNumber2 = Mathf.Clamp(_waveSetCounts.z, 1f, 5f);
            controller.setNumber3 = Mathf.Clamp(_waveSetCounts.w, 1f, 5f);
            controller.breakersExtraSpeedFactor = _breakerSpeedFactor;
            controller.breakersTorqueFactor = _breakerTorqueFactor;
            controller.oceanObjectDisplacementWithCameraAngle = _cameraDisplacement;
        }

        private void DisableVfxIfRequested()
        {
            if (!_disableOceanVfx)
            {
                return;
            }

            VisualEffect visualEffect = GetComponent<VisualEffect>();
            if (visualEffect != null)
            {
                visualEffect.enabled = false;
            }
        }

        private void ApplyMaterialSettings()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            Material material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (material == null)
            {
                return;
            }

            SetColor(material, WaterColorId, _waterColor);
            SetColor(material, LegacyWaterColorId, _waterColor);
            SetColor(material, SpecColorId, _specularColor);
            SetColor(material, GroundColorId, _groundColor);
            SetFloat(material, MinimalTransparencyId, _minimalTransparency);
            SetFloat(material, MinimalTransparancyId, _minimalTransparency);
            SetFloat(material, MinAlphaId, _minimalTransparency);
            SetFloat(material, TransparencyId, 1f - _minimalTransparency);
            SetFloat(material, TransparancyId, 1f - _minimalTransparency);
            SetFloat(material, TransparencyFactorId, 1f - _minimalTransparency);
            SetFloat(material, SmoothnessId, _surfaceSmoothness);
            SetFloat(material, WaterSmoothnessId, _waterSmoothness);
            SetFloat(material, OceanIntensityId, _shaderWaveIntensity);
            SetFloat(material, WavesIntensityId, _shaderWaveIntensity);
            SetFloat(material, RipplesIntensityId, _rippleIntensity);
            SetFloat(material, RippleIntensityId, _rippleIntensity);
            SetFloat(material, Ripples1IntensityId, _rippleIntensity);
            SetFloat(material, Ripples2IntensityId, _rippleIntensity * 0.55f);
            SetFloat(material, Ripples3IntensityId, _rippleIntensity * 0.25f);
            SetFloat(material, Ripples1StrengthId, 0.04f);
            SetFloat(material, Ripples2StrengthId, -0.035f);
            SetFloat(material, Ripples1SizeId, 18f);
            SetFloat(material, Ripples2SizeId, 36f);
            SetFloat(material, Ripples3SizeId, 48f);
            SetFloat(material, FakeHorizonWavesIntensityId, 0.02f);
            SetFloat(material, FakeHorizonWaveDensityId, 0.03f);
            SetFloat(material, HorizonMaximalSlopeId, 0.18f);
            SetFloat(material, SubSurfaceScatteringId, _subSurfaceScattering);
            SetFloat(material, SubSurfaceScateringId, _subSurfaceScattering);
        }

        private static void SetColor(Material material, int propertyId, Color value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetColor(propertyId, value);
            }
        }

        private static void SetFloat(Material material, int propertyId, float value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }
    }
}
