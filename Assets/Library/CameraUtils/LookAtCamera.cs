using BitBox.Library;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bitbox.Toymageddon.CameraUtils
{
  [DisallowMultipleComponent]
  public sealed class LookAtCamera : MonoBehaviourBase
  {
    [Header("Targeting")]
    [SerializeField] private Camera _overrideCamera;
    [SerializeField] private bool _gameCamerasOnly = true;

    [Header("Orientation")]
    [SerializeField] private bool _yawOnly = true;
    [SerializeField] private bool _invertFacing = false;
    [SerializeField] private Vector3 _rotationOffsetEuler;

    [ShowInInspector, ReadOnly]
    private Camera LastCamera => _lastCamera;

    private static Camera[] _cameraBuffer = new Camera[4];
    private Camera _lastCamera;
    private Quaternion _rotationOffset = Quaternion.identity;

    protected override void OnAwakened()
    {
      _rotationOffset = Quaternion.Euler(_rotationOffsetEuler);
    }

    protected override void OnEnabled()
    {
      RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    protected override void OnDisabled()
    {
      RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    protected override void OnLateUpdated()
    {
      Camera fallbackCamera = ResolveFallbackCamera();
      if (fallbackCamera == null)
      {
        return;
      }

      RotateTowardCamera(fallbackCamera);
    }

    private void OnValidate()
    {
      _rotationOffset = Quaternion.Euler(_rotationOffsetEuler);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext _, Camera renderingCamera)
    {
      if (!isActiveAndEnabled || !ShouldUseCamera(renderingCamera))
      {
        return;
      }

      RotateTowardCamera(renderingCamera);
    }

    private bool ShouldUseCamera(Camera camera)
    {
      if (camera == null || !camera.isActiveAndEnabled)
      {
        return false;
      }

      if (_overrideCamera != null && camera != _overrideCamera)
      {
        return false;
      }

      if (_gameCamerasOnly && camera.cameraType != CameraType.Game)
      {
        return false;
      }

      int targetLayerMask = 1 << gameObject.layer;
      if ((camera.cullingMask & targetLayerMask) == 0)
      {
        return false;
      }

      return true;
    }

    private Camera ResolveFallbackCamera()
    {
      if (ShouldUseCamera(_overrideCamera))
      {
        return _overrideCamera;
      }

      int cameraCount = Camera.allCamerasCount;
      if (cameraCount <= 0)
      {
        return null;
      }

      EnsureBufferSize(cameraCount);
      int resolvedCount = Camera.GetAllCameras(_cameraBuffer);

      Camera bestCamera = null;
      float bestDistanceSq = float.MaxValue;
      Vector3 pivot = transform.position;

      for (int i = 0; i < resolvedCount; i++)
      {
        Camera candidate = _cameraBuffer[i];
        if (!ShouldUseCamera(candidate))
        {
          continue;
        }

        float distanceSq = (candidate.transform.position - pivot).sqrMagnitude;
        if (distanceSq >= bestDistanceSq)
        {
          continue;
        }

        bestDistanceSq = distanceSq;
        bestCamera = candidate;
      }

      return bestCamera;
    }

    private void RotateTowardCamera(Camera targetCamera)
    {
      if (targetCamera == null)
      {
        return;
      }

      Vector3 toCamera = targetCamera.transform.position - transform.position;
      if (_yawOnly)
      {
        toCamera.y = 0f;
      }

      if (_invertFacing)
      {
        toCamera = -toCamera;
      }

      if (toCamera.sqrMagnitude <= 0.0001f)
      {
        return;
      }

      transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up) * _rotationOffset;
      _lastCamera = targetCamera;
    }

    private static void EnsureBufferSize(int requiredSize)
    {
      if (_cameraBuffer != null && _cameraBuffer.Length >= requiredSize)
      {
        return;
      }

      _cameraBuffer = new Camera[Mathf.NextPowerOfTwo(requiredSize)];
    }
  }
}
