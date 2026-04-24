using System.Collections.Generic;
using UnityEngine;

namespace Bitbox.Splashguard.Encounters
{
    public static class EncounterSpawnCollisionValidator
    {
        private const int OverlapBufferSize = 64;
        private static readonly Collider[] OverlapResults = new Collider[OverlapBufferSize];

        public static bool HasBlockingOverlap(GameObject enemyPrefab, Vector3 position, Quaternion rotation)
        {
            return HasBlockingOverlap(enemyPrefab, position, rotation, ignoredColliders: null);
        }

        public static bool HasBlockingOverlap(
            GameObject enemyPrefab,
            Vector3 position,
            Quaternion rotation,
            IReadOnlyCollection<Collider> ignoredColliders)
        {
            if (enemyPrefab == null)
            {
                return false;
            }

            Collider[] colliders = enemyPrefab.GetComponentsInChildren<Collider>(includeInactive: true);
            if (colliders == null || colliders.Length == 0)
            {
                return false;
            }

            Transform rootTransform = enemyPrefab.transform;
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider collider = colliders[colliderIndex];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (HasBlockingOverlapForCollider(rootTransform, collider, position, rotation, ignoredColliders))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBlockingOverlapForCollider(
            Transform rootTransform,
            Collider sourceCollider,
            Vector3 position,
            Quaternion rotation,
            IReadOnlyCollection<Collider> ignoredColliders)
        {
            Matrix4x4 colliderMatrix = BuildColliderWorldMatrix(rootTransform, sourceCollider.transform, position, rotation);

            int overlapCount;
            switch (sourceCollider)
            {
                case BoxCollider boxCollider:
                    overlapCount = QueryBoxOverlaps(colliderMatrix, boxCollider);
                    break;

                case SphereCollider sphereCollider:
                    overlapCount = QuerySphereOverlaps(colliderMatrix, sphereCollider);
                    break;

                case CapsuleCollider capsuleCollider:
                    overlapCount = QueryCapsuleOverlaps(colliderMatrix, capsuleCollider);
                    break;

                case MeshCollider meshCollider:
                    overlapCount = QueryMeshBoundsOverlaps(colliderMatrix, meshCollider);
                    break;

                default:
                    Bounds bounds = sourceCollider.bounds;
                    overlapCount = Physics.OverlapBoxNonAlloc(
                        bounds.center,
                        bounds.extents,
                        OverlapResults,
                        Quaternion.identity,
                        ~0,
                        QueryTriggerInteraction.Ignore);
                    break;
            }

            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider other = OverlapResults[overlapIndex];
                if (IsBlockingEnvironmentCollider(other, ignoredColliders))
                {
                    return true;
                }
            }

            return false;
        }

        private static int QueryBoxOverlaps(Matrix4x4 colliderMatrix, BoxCollider boxCollider)
        {
            DecomposeMatrix(colliderMatrix, out Vector3 scale, out Quaternion boxRotation);
            Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, Abs(scale));
            Vector3 center = colliderMatrix.MultiplyPoint3x4(boxCollider.center);

            return Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                OverlapResults,
                boxRotation,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private static int QuerySphereOverlaps(Matrix4x4 colliderMatrix, SphereCollider sphereCollider)
        {
            DecomposeMatrix(colliderMatrix, out Vector3 scale, out _);
            float radius = sphereCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 center = colliderMatrix.MultiplyPoint3x4(sphereCollider.center);

            return Physics.OverlapSphereNonAlloc(
                center,
                radius,
                OverlapResults,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private static int QueryCapsuleOverlaps(Matrix4x4 colliderMatrix, CapsuleCollider capsuleCollider)
        {
            DecomposeMatrix(colliderMatrix, out Vector3 scale, out Quaternion capsuleRotation);
            Vector3 center = colliderMatrix.MultiplyPoint3x4(capsuleCollider.center);

            float axisScale;
            float radialScale;
            Vector3 localAxis;
            switch (capsuleCollider.direction)
            {
                case 0:
                    axisScale = Mathf.Abs(scale.x);
                    radialScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    localAxis = Vector3.right;
                    break;

                case 2:
                    axisScale = Mathf.Abs(scale.z);
                    radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    localAxis = Vector3.forward;
                    break;

                default:
                    axisScale = Mathf.Abs(scale.y);
                    radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    localAxis = Vector3.up;
                    break;
            }

            float radius = capsuleCollider.radius * radialScale;
            float height = Mathf.Max(capsuleCollider.height * axisScale, radius * 2f);
            float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius);
            Vector3 axis = capsuleRotation * localAxis;
            Vector3 point0 = center + (axis * halfSegment);
            Vector3 point1 = center - (axis * halfSegment);

            return Physics.OverlapCapsuleNonAlloc(
                point0,
                point1,
                radius,
                OverlapResults,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private static int QueryMeshBoundsOverlaps(Matrix4x4 colliderMatrix, MeshCollider meshCollider)
        {
            Mesh sharedMesh = meshCollider.sharedMesh;
            if (sharedMesh == null)
            {
                return 0;
            }

            DecomposeMatrix(colliderMatrix, out Vector3 scale, out Quaternion meshRotation);
            Bounds localBounds = sharedMesh.bounds;
            Vector3 center = colliderMatrix.MultiplyPoint3x4(localBounds.center);
            Vector3 halfExtents = Vector3.Scale(localBounds.extents, Abs(scale));

            return Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                OverlapResults,
                meshRotation,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private static bool IsBlockingEnvironmentCollider(Collider other, IReadOnlyCollection<Collider> ignoredColliders)
        {
            if (other == null || !other.enabled || other.isTrigger)
            {
                return false;
            }

            if (ignoredColliders != null)
            {
                foreach (Collider ignoredCollider in ignoredColliders)
                {
                    if (ignoredCollider == other)
                    {
                        return false;
                    }
                }
            }

            Rigidbody attachedRigidbody = other.attachedRigidbody;
            if (attachedRigidbody != null && !attachedRigidbody.isKinematic)
            {
                return false;
            }

            return true;
        }

        private static Matrix4x4 BuildColliderWorldMatrix(
            Transform rootTransform,
            Transform colliderTransform,
            Vector3 position,
            Quaternion rotation)
        {
            Vector3 rootScale = rootTransform != null ? rootTransform.localScale : Vector3.one;
            Matrix4x4 rootMatrix = Matrix4x4.TRS(position, rotation, rootScale);
            return rootMatrix * BuildRelativeMatrix(rootTransform, colliderTransform);
        }

        private static Matrix4x4 BuildRelativeMatrix(Transform rootTransform, Transform currentTransform)
        {
            Matrix4x4 relativeMatrix = Matrix4x4.identity;
            Transform current = currentTransform;
            while (current != null && current != rootTransform)
            {
                relativeMatrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * relativeMatrix;
                current = current.parent;
            }

            return relativeMatrix;
        }

        private static void DecomposeMatrix(Matrix4x4 matrix, out Vector3 scale, out Quaternion rotation)
        {
            Vector3 axisX = matrix.GetColumn(0);
            Vector3 axisY = matrix.GetColumn(1);
            Vector3 axisZ = matrix.GetColumn(2);

            scale = new Vector3(axisX.magnitude, axisY.magnitude, axisZ.magnitude);

            if (scale.x > 0.0001f)
            {
                axisX /= scale.x;
            }

            if (scale.y > 0.0001f)
            {
                axisY /= scale.y;
            }

            if (scale.z > 0.0001f)
            {
                axisZ /= scale.z;
            }

            rotation = Quaternion.LookRotation(axisZ, axisY);
        }

        private static Vector3 Abs(Vector3 vector)
        {
            return new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
        }
    }
}
