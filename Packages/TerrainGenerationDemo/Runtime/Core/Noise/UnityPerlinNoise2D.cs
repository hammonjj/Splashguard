using UnityEngine;

namespace BitBox.TerrainGeneration.Core.Noise
{
    public sealed class UnityPerlinNoise2D : ITerrainNoise2D
    {
        private readonly float _offsetX;
        private readonly float _offsetZ;

        public UnityPerlinNoise2D(int seed)
        {
            var random = new DeterministicRandom(seed);
            _offsetX = random.Range(-10000f, 10000f);
            _offsetZ = random.Range(-10000f, 10000f);
        }

        public float Sample(float x, float z)
        {
            return Mathf.Clamp01(Mathf.PerlinNoise(x + _offsetX, z + _offsetZ));
        }
    }
}
