using System;
using UnityEngine.UIElements;

namespace BitBox.Library.UI.Toolkit
{
    public static class UiElementQueryExtensions
    {
        public static T Require<T>(this VisualElement root, string name) where T : VisualElement
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            T element = root.Q<T>(name);
            if (element != null)
            {
                return element;
            }

            throw new InvalidOperationException(
                $"Required UI Toolkit element '{name}' of type '{typeof(T).Name}' was not found under '{root.name}'.");
        }
    }
}
