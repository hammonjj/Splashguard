using UnityEngine;
using UnityEngine.UIElements;

namespace BitBox.Library.UI.Toolkit
{
    public static class UiScaleVisualElementUtility
    {
        public static void ApplyScale(VisualElement element, float scaleMultiplier)
        {
            if (element == null)
            {
                return;
            }

            float clampedScale = Mathf.Max(0.01f, scaleMultiplier);
            element.style.transformOrigin = new TransformOrigin(
                new Length(50f, LengthUnit.Percent),
                new Length(50f, LengthUnit.Percent),
                0f);
            element.style.scale = new Scale(new Vector2(clampedScale, clampedScale));
        }
    }
}
