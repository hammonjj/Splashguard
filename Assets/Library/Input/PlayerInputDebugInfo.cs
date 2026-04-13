using System;
using Sirenix.OdinInspector;
using UnityEngine.InputSystem;

namespace BitBox.Library.Input
{
  [Serializable]
  public struct PlayerInputDebugInfo
  {
    [ReadOnly, ShowInInspector] public string ActiveInputMap;
    [ReadOnly, ShowInInspector] public PlayerInput PlayerInput;
  }
}
