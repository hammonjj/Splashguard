using UnityEngine;

namespace BitBox.TerrainGeneration.Core.Masks
{
    public sealed class NoFalloffMask : IMask2D
    {
        public float Evaluate(float u, float v)
        {
            return 0f;
        }
    }

    public sealed class RadialFalloffMask : IMask2D
    {
        private readonly float _exponent;

        public RadialFalloffMask(float exponent)
        {
            _exponent = Mathf.Max(0.01f, exponent);
        }

        public float Evaluate(float u, float v)
        {
            float dx = u - 0.5f;
            float dz = v - 0.5f;
            float normalizedDistance = Mathf.Sqrt(dx * dx + dz * dz) / 0.70710678f;
            return Mathf.Pow(Mathf.Clamp01(normalizedDistance), _exponent);
        }
    }

    public sealed class DistanceToEdgeFalloffMask : IMask2D
    {
        private readonly float _exponent;

        public DistanceToEdgeFalloffMask(float exponent)
        {
            _exponent = Mathf.Max(0.01f, exponent);
        }

        public float Evaluate(float u, float v)
        {
            float distanceToEdge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float falloff = 1f - Mathf.Clamp01(distanceToEdge * 2f);
            return Mathf.Pow(falloff, _exponent);
        }
    }
}
