using UnityEngine;

namespace BitBox.Toymageddon.Settings
{
    internal static class UiScaleSettings
    {
        public const float DefaultScale = 1f;
        public const float MinimumScale = 0.8f;
        public const float MaximumScale = 1.4f;
        public const float SliderMinimumValue = 80f;
        public const float SliderMaximumValue = 140f;

        public static float Clamp(float scale)
        {
            return Mathf.Clamp(scale, MinimumScale, MaximumScale);
        }

        public static float SliderValueToScale(float sliderValue)
        {
            return Clamp(sliderValue / 100f);
        }

        public static float ScaleToSliderValue(float scale)
        {
            return Clamp(scale) * 100f;
        }

        public static string FormatPercentLabel(float scale)
        {
            return $"{Mathf.RoundToInt(ScaleToSliderValue(scale))}%";
        }

        public static float ResolveCurrentScale()
        {
            return GameSettingsService.Instance != null
                ? Clamp(GameSettingsService.Instance.CurrentSettings.UiScale)
                : DefaultScale;
        }
    }
}
