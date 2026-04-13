using System.Collections.Generic;
using UnityEngine;

namespace BitBox.Library.Debugging
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Debug/Collider Gizmo Visualizer")]
    public sealed class ColliderGizmoVisualizer : MonoBehaviourBase
    {
        [Header("Target Colliders")]
        [SerializeField] private bool _includeChildren = false;
        [SerializeField] private bool _includeInactiveChildren = false;
        [SerializeField] private bool _drawDisabledColliders = false;

        [Header("Rendering")]
        [SerializeField] private bool _drawOnlyWhenSelected = false;
        [SerializeField] private bool _drawSolid = true;
        [SerializeField] private bool _drawWire = true;
        [SerializeField] private bool _drawBoundsForUnsupported = true;

        [Header("Colors")]
        [SerializeField] private Color _solidColor = new Color(0.05f, 0.85f, 1f, 0.16f);
        [SerializeField] private Color _wireColor = new Color(0.05f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color _triggerSolidColor = new Color(1f, 0.55f, 0.1f, 0.14f);
        [SerializeField] private Color _triggerWireColor = new Color(1f, 0.55f, 0.1f, 0.9f);

        private readonly List<Collider> _colliders = new List<Collider>(8);

        private void OnDrawGizmos()
        {
            if (_drawOnlyWhenSelected)
            {
                return;
            }

            DrawColliderGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawOnlyWhenSelected)
            {
                return;
            }

            DrawColliderGizmos();
        }

        private void DrawColliderGizmos()
        {
            CollectColliders();
            for (int i = 0; i < _colliders.Count; i++)
            {
                Collider collider = _colliders[i];
                if (collider == null)
                {
                    continue;
                }

                Color solid = collider.isTrigger ? _triggerSolidColor : _solidColor;
                Color wire = collider.isTrigger ? _triggerWireColor : _wireColor;

                if (collider is BoxCollider boxCollider)
                {
                    DrawBoxCollider(boxCollider, solid, wire);
                    continue;
                }

                if (collider is SphereCollider sphereCollider)
                {
                    DrawSphereCollider(sphereCollider, solid, wire);
                    continue;
                }

                if (collider is CapsuleCollider capsuleCollider)
                {
                    DrawCapsuleCollider(capsuleCollider, solid, wire);
                    continue;
                }

                if (collider is MeshCollider meshCollider)
                {
                    DrawMeshCollider(meshCollider, solid, wire);
                    continue;
                }

                if (_drawBoundsForUnsupported)
                {
                    DrawBounds(collider.bounds, solid, wire);
                }
            }
        }

        private void CollectColliders()
        {
            _colliders.Clear();

            if (_includeChildren)
            {
                Collider[] found = GetComponentsInChildren<Collider>(_includeInactiveChildren);
                for (int i = 0; i < found.Length; i++)
                {
                    AddColliderIfVisible(found[i]);
                }

                return;
            }

            Collider[] own = GetComponents<Collider>();
            for (int i = 0; i < own.Length; i++)
            {
                AddColliderIfVisible(own[i]);
            }
        }

        private void AddColliderIfVisible(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            if (!_drawDisabledColliders && !collider.enabled)
            {
                return;
            }

            if (!_includeInactiveChildren && !collider.gameObject.activeInHierarchy)
            {
                return;
            }

            _colliders.Add(collider);
        }

        private void DrawBoxCollider(BoxCollider collider, Color solidColor, Color wireColor)
        {
            Vector3 worldCenter = collider.transform.TransformPoint(collider.center);
            Vector3 worldScale = Abs(collider.transform.lossyScale);
            Vector3 worldSize = Vector3.Scale(collider.size, worldScale);

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(worldCenter, collider.transform.rotation, Vector3.one);
            DrawBox(Vector3.zero, worldSize, solidColor, wireColor);
            Gizmos.matrix = previous;
        }

        private void DrawSphereCollider(SphereCollider collider, Color solidColor, Color wireColor)
        {
            Vector3 worldCenter = collider.transform.TransformPoint(collider.center);
            Vector3 worldScale = Abs(collider.transform.lossyScale);
            float radiusScale = Mathf.Max(worldScale.x, Mathf.Max(worldScale.y, worldScale.z));
            float worldRadius = collider.radius * radiusScale;

            if (_drawSolid)
            {
                Gizmos.color = solidColor;
                Gizmos.DrawSphere(worldCenter, worldRadius);
            }

            if (_drawWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireSphere(worldCenter, worldRadius);
            }
        }

        private void DrawCapsuleCollider(CapsuleCollider collider, Color solidColor, Color wireColor)
        {
            Transform t = collider.transform;
            Vector3 lossyScale = Abs(t.lossyScale);

            Vector3 axisLocal = Vector3.up;
            float radiusScale = Mathf.Max(lossyScale.x, lossyScale.z);
            float heightScale = lossyScale.y;

            if (collider.direction == 0)
            {
                axisLocal = Vector3.right;
                radiusScale = Mathf.Max(lossyScale.y, lossyScale.z);
                heightScale = lossyScale.x;
            }
            else if (collider.direction == 2)
            {
                axisLocal = Vector3.forward;
                radiusScale = Mathf.Max(lossyScale.x, lossyScale.y);
                heightScale = lossyScale.z;
            }

            Vector3 center = t.TransformPoint(collider.center);
            Vector3 axisWorld = t.TransformDirection(axisLocal).normalized;

            float radius = collider.radius * radiusScale;
            float height = Mathf.Max(collider.height * heightScale, radius * 2f);
            float cylinderHalfHeight = Mathf.Max(0f, (height * 0.5f) - radius);
            Vector3 sphereOffset = axisWorld * cylinderHalfHeight;

            Vector3 top = center + sphereOffset;
            Vector3 bottom = center - sphereOffset;

            if (_drawSolid)
            {
                Gizmos.color = solidColor;
                Gizmos.DrawSphere(top, radius);
                Gizmos.DrawSphere(bottom, radius);
            }

            if (_drawWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireSphere(top, radius);
                Gizmos.DrawWireSphere(bottom, radius);
            }

            if (cylinderHalfHeight > 0.0001f)
            {
                Quaternion axisRotation = Quaternion.FromToRotation(Vector3.up, axisWorld);
                Matrix4x4 previous = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, axisRotation, Vector3.one);
                DrawBox(
                    Vector3.zero,
                    new Vector3(radius * 2f, cylinderHalfHeight * 2f, radius * 2f),
                    solidColor,
                    wireColor
                );
                Gizmos.matrix = previous;
            }
        }

        private void DrawMeshCollider(MeshCollider collider, Color solidColor, Color wireColor)
        {
            if (collider.sharedMesh == null)
            {
                if (_drawBoundsForUnsupported)
                {
                    DrawBounds(collider.bounds, solidColor, wireColor);
                }

                return;
            }

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = collider.transform.localToWorldMatrix;

            if (_drawSolid)
            {
                Gizmos.color = solidColor;
                Gizmos.DrawMesh(collider.sharedMesh);
            }

            if (_drawWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireMesh(collider.sharedMesh);
            }

            Gizmos.matrix = previous;
        }

        private void DrawBounds(Bounds bounds, Color solidColor, Color wireColor)
        {
            if (_drawSolid)
            {
                Gizmos.color = solidColor;
                Gizmos.DrawCube(bounds.center, bounds.size);
            }

            if (_drawWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        private void DrawBox(Vector3 center, Vector3 size, Color solidColor, Color wireColor)
        {
            if (_drawSolid)
            {
                Gizmos.color = solidColor;
                Gizmos.DrawCube(center, size);
            }

            if (_drawWire)
            {
                Gizmos.color = wireColor;
                Gizmos.DrawWireCube(center, size);
            }
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }
    }
}
