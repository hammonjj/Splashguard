using UnityEngine;

namespace BitBox.Library.Utilities
{
    public class HideCursor : MonoBehaviourBase
    {
        [SerializeField] private bool _hideCursorInEditor = true;
        [SerializeField] private CursorLockMode _cursorLockMode = CursorLockMode.Locked;

        protected override void OnAwakened()
        {
            if (Application.isEditor && !_hideCursorInEditor)
            {
                return;
            }

            Cursor.visible = false;
            Cursor.lockState = _cursorLockMode;

            LogInfo($"Cursor hidden and locked with mode: {_cursorLockMode}");
        }

        void OnDestroy()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
