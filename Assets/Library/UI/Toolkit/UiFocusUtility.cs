using UnityEngine.UIElements;

namespace BitBox.Library.UI.Toolkit
{
    public static class UiFocusUtility
    {
        public static void FocusFirstFocusable(VisualElement root, string preferredElementName = null)
        {
            if (root == null)
            {
                return;
            }

            root.schedule.Execute(() =>
            {
                VisualElement target = null;

                if (!string.IsNullOrWhiteSpace(preferredElementName))
                {
                    target = root.Q<VisualElement>(preferredElementName);
                }

                target ??= FindFirstFocusable(root);
                target?.Focus();
            });
        }

        private static VisualElement FindFirstFocusable(VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            if (root.focusable && root.visible && root.resolvedStyle.display != DisplayStyle.None)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                VisualElement child = root[i];
                VisualElement focusable = FindFirstFocusable(child);
                if (focusable != null)
                {
                    return focusable;
                }
            }

            return null;
        }
    }
}
