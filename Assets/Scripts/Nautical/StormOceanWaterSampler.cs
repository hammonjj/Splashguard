using StormBreakers;
using UnityEngine;

namespace Bitbox.Toymageddon.Nautical
{
    public static class WaterQuery
    {
        private static StormOceanWaterSampler _activeSampler;

        public static bool TrySample(Vector3 worldPoint, out WaterSample sample)
        {
            if (_activeSampler != null && _activeSampler.isActiveAndEnabled)
            {
                return _activeSampler.TrySample(worldPoint, out sample);
            }

            sample = default;
            return false;
        }

        internal static void Register(StormOceanWaterSampler sampler)
        {
            if (sampler != null)
            {
                _activeSampler = sampler;
            }
        }

        internal static void Unregister(StormOceanWaterSampler sampler)
        {
            if (_activeSampler == sampler)
            {
                _activeSampler = null;
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(OceanController))]
    public sealed class StormOceanWaterSampler : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _defaultGroundDepth = 200f;
        [SerializeField, Min(0.01f)] private float _normalPrecision = 0.25f;
        [SerializeField] private bool _sampleNormals = true;

        private void OnEnable()
        {
            WaterQuery.Register(this);
        }

        private void OnDisable()
        {
            WaterQuery.Unregister(this);
        }

        public bool TrySample(Vector3 worldPoint, out WaterSample sample)
        {
            if (!IsOceanInitialized())
            {
                sample = default;
                return false;
            }

            Vector3 undeformedPosition = worldPoint;
            float groundDepth = GetGroundDepth(undeformedPosition);
            float height = Ocean.GetHeight(
                Time.time,
                worldPoint,
                ref undeformedPosition,
                out Vector3 deformation,
                groundDepth);

            Vector3 normal = _sampleNormals
                ? Ocean.GetNormal(Time.time, undeformedPosition, deformation, groundDepth, _normalPrecision)
                : Vector3.up;
            Vector3 surfacePoint = undeformedPosition + deformation;
            surfacePoint.y = height;
            sample = new WaterSample(surfacePoint, normal, height);
            return true;
        }

        private float GetGroundDepth(Vector3 undeformedPosition)
        {
            return Ocean.useTerrain && Ocean.terrain != null
                ? -Ocean.terrain.SampleHeight(undeformedPosition) - Ocean.terrain.transform.position.y
                : _defaultGroundDepth;
        }

        private static bool IsOceanInitialized()
        {
            return Ocean.wavelength != null && Ocean.wavelength.Length >= 4
                && Ocean.directionVector != null && Ocean.directionVector.Length >= 4
                && Ocean.iVector != null && Ocean.iVector.Length >= 4
                && Ocean.jVector != null && Ocean.jVector.Length >= 4;
        }
    }
}
