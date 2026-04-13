using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using BitBox.Library.UI;

namespace BitBox.Library.Utilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public class CameraViewportCanvasScaler : MonoBehaviourBase
    {
        [SerializeField] private Camera _overrideCamera;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasScaler _canvasScaler;
        [SerializeField] private RectTransform _viewportRoot;

        private Camera _lastCamera;
        private Rect _lastPixelRect;
        private Rect _lastViewportRect;
        private Vector2 _lastReferenceResolution;
        private CanvasScaler.ScreenMatchMode _lastScreenMatchMode;
        private float _lastMatchWidthOrHeight = -1f;
        private int _lastTargetDisplay = -1;
        private float _lastUserInterfaceScale = -1f;
        private bool _loggedViewportFailure;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            SyncScale(force: true);
        }

        private void OnEnable()
        {
            SyncScale(force: true);
        }

        private void LateUpdate()
        {
            SyncScale();
        }

        private void CacheReferences()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_canvasScaler == null)
            {
                _canvasScaler = GetComponent<CanvasScaler>();
            }
        }

        private void SyncScale(bool force = false)
        {
            if (!TryGetViewport(out var targetCamera, out var pixelRect, out var viewportRect))
            {
                LogViewportFailure();
                return;
            }

            _loggedViewportFailure = false;

            if (!force && !NeedsSync(targetCamera, pixelRect, viewportRect))
            {
                return;
            }

            float userInterfaceScale = UiScaleRuntime.ResolveScale();

            _canvas.worldCamera = targetCamera;
            _canvas.targetDisplay = targetCamera.targetDisplay;
            _canvas.planeDistance = 1f;

            SyncViewportRoot(viewportRect);

            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _canvasScaler.scaleFactor = Mathf.Max(0.01f, CalculateScaleFactor(pixelRect.size) * userInterfaceScale);

            _lastCamera = targetCamera;
            _lastPixelRect = pixelRect;
            _lastViewportRect = viewportRect;
            _lastReferenceResolution = _canvasScaler.referenceResolution;
            _lastScreenMatchMode = _canvasScaler.screenMatchMode;
            _lastMatchWidthOrHeight = _canvasScaler.matchWidthOrHeight;
            _lastTargetDisplay = targetCamera.targetDisplay;
            _lastUserInterfaceScale = userInterfaceScale;

            LogDebug(
                $"[CameraViewportCanvasScaler] {BuildHierarchyPath(transform)} synced canvas '{_canvas.name}' to camera '{targetCamera.name}'. " +
                $"cameraRect={FormatRect(viewportRect)}, pixelRect={FormatRect(pixelRect)}, targetDisplay={targetCamera.targetDisplay}, " +
                $"viewportRootMin={FormatVector2(_viewportRoot != null ? _viewportRoot.anchorMin : Vector2.zero)}, " +
                $"viewportRootMax={FormatVector2(_viewportRoot != null ? _viewportRoot.anchorMax : Vector2.one)}, " +
                $"scaleFactor={_canvasScaler.scaleFactor:F2}");
        }

        private bool TryGetViewport(out Camera targetCamera, out Rect pixelRect, out Rect viewportRect)
        {
            CacheReferences();

            targetCamera = ResolveCamera();
            pixelRect = default;
            viewportRect = default;

            if (_canvas == null || _canvasScaler == null || targetCamera == null)
            {
                return false;
            }

            if (_canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                Debug.LogWarning(
                    $"[CameraViewportCanvasScaler] {BuildHierarchyPath(transform)} forcing canvas render mode from {_canvas.renderMode} to {RenderMode.ScreenSpaceCamera}.");
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            pixelRect = targetCamera.pixelRect;
            viewportRect = targetCamera.rect;
            return pixelRect.width > 0f && pixelRect.height > 0f;
        }

        private Camera ResolveCamera()
        {
            if (_overrideCamera != null)
            {
                return _overrideCamera;
            }

            var playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null && playerInput.camera != null)
            {
                return playerInput.camera;
            }

            if (_canvas != null && _canvas.worldCamera != null)
            {
                return _canvas.worldCamera;
            }

            var root = transform.root;
            return root != null ? root.GetComponentInChildren<Camera>(true) : null;
        }

        private bool NeedsSync(Camera targetCamera, Rect pixelRect, Rect viewportRect)
        {
            return targetCamera != _lastCamera
                   || pixelRect != _lastPixelRect
                   || viewportRect != _lastViewportRect
                   || _canvasScaler.referenceResolution != _lastReferenceResolution
                   || _canvasScaler.screenMatchMode != _lastScreenMatchMode
                   || !Mathf.Approximately(_canvasScaler.matchWidthOrHeight, _lastMatchWidthOrHeight)
                   || targetCamera.targetDisplay != _lastTargetDisplay
                   || !Mathf.Approximately(UiScaleRuntime.ResolveScale(), _lastUserInterfaceScale);
        }

        private void SyncViewportRoot(Rect viewportRect)
        {
            if (_viewportRoot == null)
            {
                return;
            }

            if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _viewportRoot.anchorMin = viewportRect.min;
                _viewportRoot.anchorMax = viewportRect.max;
            }
            else
            {
                // Screen-space camera canvases already render inside the player camera rect.
                // Keeping the viewport root full-stretch prevents the split from being applied twice.
                _viewportRoot.anchorMin = Vector2.zero;
                _viewportRoot.anchorMax = Vector2.one;
            }

            _viewportRoot.offsetMin = Vector2.zero;
            _viewportRoot.offsetMax = Vector2.zero;
        }

        private void LogViewportFailure()
        {
            if (_loggedViewportFailure)
            {
                return;
            }

            string cameraName = ResolveCamera() != null ? ResolveCamera().name : "None";
            string renderMode = _canvas != null ? _canvas.renderMode.ToString() : "None";

            Debug.LogWarning(
                $"[CameraViewportCanvasScaler] {BuildHierarchyPath(transform)} could not resolve a viewport. " +
                $"canvasPresent={_canvas != null}, canvasScalerPresent={_canvasScaler != null}, renderMode={renderMode}, " +
                $"resolvedCamera={cameraName}, viewportRootPresent={_viewportRoot != null}");

            _loggedViewportFailure = true;
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = $"{target.name}/{path}";
            }

            return path;
        }

        private static string FormatRect(Rect rect)
        {
            return $"({rect.x:F2}, {rect.y:F2}, {rect.width:F2}, {rect.height:F2})";
        }

        private static string FormatVector2(Vector2 value)
        {
            return $"({value.x:F2}, {value.y:F2})";
        }

        // Unity's Scale With Screen Size uses the full display size, not the camera viewport.
        private float CalculateScaleFactor(Vector2 viewportSize)
        {
            var referenceResolution = _canvasScaler.referenceResolution;
            referenceResolution.x = Mathf.Max(1f, referenceResolution.x);
            referenceResolution.y = Mathf.Max(1f, referenceResolution.y);

            var widthScale = viewportSize.x / referenceResolution.x;
            var heightScale = viewportSize.y / referenceResolution.y;

            return _canvasScaler.screenMatchMode switch
            {
                CanvasScaler.ScreenMatchMode.Expand => Mathf.Min(widthScale, heightScale),
                CanvasScaler.ScreenMatchMode.Shrink => Mathf.Max(widthScale, heightScale),
                _ => Mathf.Pow(
                    2f,
                    Mathf.Lerp(
                        Mathf.Log(widthScale, 2f),
                        Mathf.Log(heightScale, 2f),
                        _canvasScaler.matchWidthOrHeight))
            };
        }
    }
}
