using Sirenix.OdinInspector;
using UnityEngine;

namespace Bitbox
{
    [DisallowMultipleComponent]
    public sealed class PlayerDataReference : MonoBehaviour
    {
        [SerializeField, Required, InlineEditor] private PlayerGameplayData _gameplayData;
        [SerializeField, Required] private Transform _visualFacingTarget;
        [SerializeField, Required] private Transform _cameraTarget;
        [SerializeField, Required] private Camera _gameplayCamera;

        public PlayerGameplayData GameplayData => _gameplayData;
        public Transform VisualFacingTarget => _visualFacingTarget;
        public Transform CameraTarget => _cameraTarget;
        public Camera GameplayCamera => _gameplayCamera;
    }
}
