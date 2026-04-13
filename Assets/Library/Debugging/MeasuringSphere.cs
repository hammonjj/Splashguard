using UnityEngine;

namespace BitBox.Library.Debugging
{
  [ExecuteAlways]
  [DisallowMultipleComponent]
  [AddComponentMenu("Debug/Measuring Sphere")]
  public sealed class MeasuringSphere : MonoBehaviourBase
  {
    [Header("Rendering")]
    [SerializeField] private bool _drawSolid = true;
    [SerializeField] private bool _drawOnlyWhenSelected = false;
    [SerializeField] private float _radius = 1f;
    [SerializeField] private Color _color = new Color(0.05f, 0.85f, 1f, 0.16f);

    private void OnDrawGizmos()
    {
      if (_drawOnlyWhenSelected)
      {
        return;
      }

      DrawColliderGizmos();
    }

    private void DrawColliderGizmos()
    {
      Gizmos.color = _color;
      if (_drawSolid)
      {
        Gizmos.DrawSphere(transform.position, _radius);
      }
      else
      {
        Gizmos.DrawWireSphere(transform.position, _radius);
      }
    }
  }
}
