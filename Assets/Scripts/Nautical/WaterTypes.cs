using System;
using UnityEngine;

namespace Bitbox.Toymageddon.Nautical
{
    [Serializable]
    public struct WaterWaveSettings
    {
        public Vector2 direction;

        [Range(0f, 1f)]
        public float steepness;

        [Min(0.1f)]
        public float wavelength;

        public WaterWaveSettings(Vector2 direction, float steepness, float wavelength)
        {
            this.direction = direction;
            this.steepness = steepness;
            this.wavelength = wavelength;
        }

        public Vector2 NormalizedDirection =>
            direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

        public float Steepness => Mathf.Clamp01(steepness);
        public float Wavelength => Mathf.Max(0.1f, wavelength);
        public float Amplitude => Steepness / ((2f * Mathf.PI) / Wavelength);

        public Vector4 ToShaderVector()
        {
            var normalizedDirection = NormalizedDirection;
            return new Vector4(normalizedDirection.x, normalizedDirection.y, Steepness, Wavelength);
        }

        public WaterWaveSettings Sanitized()
        {
            return new WaterWaveSettings(NormalizedDirection, Steepness, Wavelength);
        }
    }

    public readonly struct WaterSample
    {
        public WaterSample(Vector3 surfacePoint, Vector3 normal, float height)
        {
            SurfacePoint = surfacePoint;
            Normal = normal;
            Height = height;
        }

        public Vector3 SurfacePoint { get; }
        public Vector3 Normal { get; }
        public float Height { get; }
    }
}
