using System;
using UnityEngine;

namespace BitBox.Library.UI
{
    public static class UiScaleRuntime
    {
        private static Func<float> _scaleResolver;

        public static void SetScaleResolver(Func<float> scaleResolver)
        {
            _scaleResolver = scaleResolver;
        }

        public static float ResolveScale()
        {
            float resolvedScale = _scaleResolver != null ? _scaleResolver() : 1f;
            if (float.IsNaN(resolvedScale) || float.IsInfinity(resolvedScale))
            {
                return 1f;
            }

            return Mathf.Max(0.01f, resolvedScale);
        }
    }
}
