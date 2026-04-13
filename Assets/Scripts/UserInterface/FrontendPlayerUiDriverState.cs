using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace BitBox.Toymageddon.UserInterface
{
    internal sealed class FrontendPlayerUiDriverState
    {
        public MultiplayerEventSystem EventSystem;
        public InputSystemUIInputModule InputModule;
        public GameObject OriginalPlayerRoot;
        public bool EventSystemWasEnabled;
        public bool InputModuleWasEnabled;
    }
}
