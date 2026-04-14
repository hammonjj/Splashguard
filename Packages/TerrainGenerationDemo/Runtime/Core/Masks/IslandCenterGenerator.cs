using UnityEngine;

namespace BitBox.TerrainGeneration.Core.Masks
{
    public static class IslandCenterGenerator
    {
        public static Vector2[] Generate(int seed, int count, float minSeparation)
        {
            count = Mathf.Clamp(count, 1, 64);
            minSeparation = Mathf.Clamp01(minSeparation);

            if (count == 1)
            {
                return new[] { new Vector2(0.5f, 0.5f) };
            }

            var centers = new Vector2[count];
            var random = new DeterministicRandom(seed ^ unchecked((int)0x9e3779b9));
            int accepted = 0;
            int attempts = 0;
            int maxAttempts = count * 80;

            while (accepted < count && attempts < maxAttempts)
            {
                attempts++;
                var candidate = new Vector2(random.Range(0.12f, 0.88f), random.Range(0.12f, 0.88f));
                if (!HasMinimumSeparation(candidate, centers, accepted, minSeparation))
                {
                    continue;
                }

                centers[accepted++] = candidate;
            }

            while (accepted < count)
            {
                float angle = accepted * Mathf.PI * 2f / count;
                centers[accepted++] = new Vector2(
                    0.5f + Mathf.Cos(angle) * 0.28f,
                    0.5f + Mathf.Sin(angle) * 0.28f);
            }

            return centers;
        }

        public static bool HasMinimumSeparation(Vector2 candidate, Vector2[] centers, int centerCount, float minSeparation)
        {
            float minSqr = minSeparation * minSeparation;
            for (int i = 0; i < centerCount; i++)
            {
                if ((candidate - centers[i]).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
