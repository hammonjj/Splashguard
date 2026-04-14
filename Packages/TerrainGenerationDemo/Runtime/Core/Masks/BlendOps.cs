using UnityEngine;

namespace BitBox.TerrainGeneration.Core.Masks
{
    public static class BlendOps
    {
        public static float BlendIslandness(float current, float next, MultiIslandBlendMode mode)
        {
            switch (mode)
            {
                case MultiIslandBlendMode.SumClamp:
                    return Mathf.Clamp01(current + next);
                case MultiIslandBlendMode.SmoothUnion:
                    const float k = 8f;
                    float currentExp = Mathf.Exp(k * current);
                    float nextExp = Mathf.Exp(k * next);
                    return Mathf.Clamp01(Mathf.Log(currentExp + nextExp) / k);
                default:
                    return Mathf.Max(current, next);
            }
        }
    }
}
