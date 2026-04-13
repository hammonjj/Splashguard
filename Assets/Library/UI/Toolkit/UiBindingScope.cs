using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace BitBox.Library.UI.Toolkit
{
    public sealed class UiBindingScope : IDisposable
    {
        private readonly List<Action> _disposals = new List<Action>();

        public void BindButton(Button button, Action callback)
        {
            if (button == null)
            {
                throw new ArgumentNullException(nameof(button));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            button.clicked += callback;
            _disposals.Add(() => button.clicked -= callback);
        }

        public void Register(Action cleanup)
        {
            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            _disposals.Add(cleanup);
        }

        public void Dispose()
        {
            for (int i = _disposals.Count - 1; i >= 0; i--)
            {
                _disposals[i]?.Invoke();
            }

            _disposals.Clear();
        }
    }
}
