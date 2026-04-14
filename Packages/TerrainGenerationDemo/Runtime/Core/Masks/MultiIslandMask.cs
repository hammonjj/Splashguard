using UnityEngine;

namespace BitBox.TerrainGeneration.Core.Masks
{
    public sealed class MultiIslandMask : IMask2D
    {
        private readonly Vector2[] _centers;
        private readonly float _radius;
        private readonly float _exponent;
        private readonly MultiIslandBlendMode _blendMode;

        public MultiIslandMask(Vector2[] centers, float radius, float exponent, MultiIslandBlendMode blendMode)
        {
            _centers = centers;
            _radius = Mathf.Max(0.01f, radius);
            _exponent = Mathf.Max(0.01f, exponent);
            _blendMode = blendMode;
        }

        public float Evaluate(float u, float v)
        {
            var p = new Vector2(u, v);
            float islandness = 0f;

            for (int i = 0; i < _centers.Length; i++)
            {
                float normalizedDistance = Vector2.Distance(p, _centers[i]) / _radius;
                float localIslandness = Mathf.Clamp01(1f - normalizedDistance);
                localIslandness *= localIslandness * (3f - 2f * localIslandness);
                islandness = BlendOps.BlendIslandness(islandness, localIslandness, _blendMode);
            }

            return Mathf.Pow(1f - Mathf.Clamp01(islandness), _exponent);
        }
    }
}
