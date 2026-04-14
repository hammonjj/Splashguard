using UnityEngine;

namespace BitBox.TerrainGeneration.Core.Noise
{
    public sealed class FbmNoise2D : ITerrainNoise2D
    {
        private readonly ITerrainNoise2D _baseNoise;
        private readonly int _octaves;
        private readonly float _persistence;
        private readonly float _lacunarity;
        private readonly TerrainNoiseMode _mode;

        public FbmNoise2D(
            ITerrainNoise2D baseNoise,
            int octaves,
            float persistence,
            float lacunarity,
            TerrainNoiseMode mode)
        {
            _baseNoise = baseNoise;
            _octaves = Mathf.Clamp(octaves, 1, 12);
            _persistence = Mathf.Clamp01(persistence);
            _lacunarity = Mathf.Max(1f, lacunarity);
            _mode = mode;
        }

        public float Sample(float x, float z)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float amplitudeSum = 0f;

            for (int octave = 0; octave < _octaves; octave++)
            {
                float sample = _baseNoise.Sample(x * frequency, z * frequency);
                if (_mode == TerrainNoiseMode.Ridged)
                {
                    sample = 1f - Mathf.Abs((sample * 2f) - 1f);
                    sample *= sample;
                }

                sum += sample * amplitude;
                amplitudeSum += amplitude;
                amplitude *= _persistence;
                frequency *= _lacunarity;
            }

            if (amplitudeSum <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(sum / amplitudeSum);
        }
    }
}
