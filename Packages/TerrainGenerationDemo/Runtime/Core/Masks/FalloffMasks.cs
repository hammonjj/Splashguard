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

    public sealed class RoundedBasinMask : IMask2D
    {
        private readonly float _halfWidth;
        private readonly float _halfDepth;
        private readonly float _cornerRadius;
        private readonly float _edgeSoftness;
        private readonly float _exponent;

        public RoundedBasinMask(
            float width,
            float depth,
            float cornerRadius,
            float edgeSoftness,
            float exponent)
        {
            _halfWidth = Mathf.Clamp(width, 0.01f, 1.5f) * 0.5f;
            _halfDepth = Mathf.Clamp(depth, 0.01f, 1.5f) * 0.5f;
            _cornerRadius = Mathf.Min(Mathf.Clamp01(cornerRadius), Mathf.Min(_halfWidth, _halfDepth));
            _edgeSoftness = Mathf.Clamp(edgeSoftness, 0.001f, 0.5f);
            _exponent = Mathf.Max(0.01f, exponent);
        }

        public float Evaluate(float u, float v)
        {
            float px = Mathf.Abs(u - 0.5f) - (_halfWidth - _cornerRadius);
            float pz = Mathf.Abs(v - 0.5f) - (_halfDepth - _cornerRadius);
            float outsideX = Mathf.Max(px, 0f);
            float outsideZ = Mathf.Max(pz, 0f);
            float outsideDistance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
            float insideDistance = Mathf.Min(Mathf.Max(px, pz), 0f);
            float signedDistance = outsideDistance + insideDistance - _cornerRadius;
            float basinAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_edgeSoftness, -_edgeSoftness, signedDistance));
            return Mathf.Pow(Mathf.Clamp01(basinAmount), _exponent);
        }
    }
}
