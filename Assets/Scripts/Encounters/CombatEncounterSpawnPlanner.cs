using System;
using System.Collections.Generic;
using Bitbox.Splashguard.Enemies;
using UnityEngine;

namespace Bitbox.Splashguard.Encounters
{
    public readonly struct CombatEncounterSpawnCandidateResult
    {
        public CombatEncounterSpawnCandidateResult(bool isValid, Vector3 surfacePoint)
        {
            IsValid = isValid;
            SurfacePoint = surfacePoint;
        }

        public bool IsValid { get; }
        public Vector3 SurfacePoint { get; }
    }

    public static class CombatEncounterSpawnPlanner
    {
        public static bool TryChooseSpawnPose(
            Vector3 center,
            float searchRadius,
            Vector3 exclusionCenter,
            float minimumDistanceFromExclusion,
            NavalPointValidationSettings validationSettings,
            System.Random random,
            out Vector3 spawnPosition,
            out Quaternion spawnRotation)
        {
            return TryChooseSpawnPose(
                center,
                searchRadius,
                exclusionCenter,
                minimumDistanceFromExclusion,
                random,
                candidate =>
                {
                    NavalPointValidationResult validationResult =
                        NavalPointValidationUtility.ValidateCandidate(candidate, validationSettings);
                    return new CombatEncounterSpawnCandidateResult(
                        validationResult.IsValid,
                        validationResult.SurfacePoint);
                },
                out spawnPosition,
                out spawnRotation);
        }

        public static bool TryChooseSpawnPose(
            Vector3 center,
            float searchRadius,
            Vector3 exclusionCenter,
            float minimumDistanceFromExclusion,
            System.Random random,
            Func<Vector3, CombatEncounterSpawnCandidateResult> validateCandidate,
            out Vector3 spawnPosition,
            out Quaternion spawnRotation)
        {
            searchRadius = Mathf.Max(0f, searchRadius);
            return TryChooseSpawnPose(
                exclusionCenter,
                minimumDistanceFromExclusion,
                random,
                rng =>
                {
                    double angle = rng.NextDouble() * Math.PI * 2d;
                    float radius = searchRadius <= 0.01f
                        ? 0f
                        : searchRadius * Mathf.Sqrt((float)rng.NextDouble());
                    return center + new Vector3(
                        Mathf.Cos((float)angle) * radius,
                        0f,
                        Mathf.Sin((float)angle) * radius);
                },
                validateCandidate,
                out spawnPosition,
                out spawnRotation);
        }

        public static bool TryChooseSpawnPose(
            Vector3 exclusionCenter,
            float minimumDistanceFromExclusion,
            System.Random random,
            Func<System.Random, Vector3> chooseCandidate,
            Func<Vector3, CombatEncounterSpawnCandidateResult> validateCandidate,
            out Vector3 spawnPosition,
            out Quaternion spawnRotation)
        {
            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;

            if (chooseCandidate == null || validateCandidate == null)
            {
                return false;
            }

            random ??= new System.Random();
            minimumDistanceFromExclusion = Mathf.Max(0f, minimumDistanceFromExclusion);

            Vector3 candidate = chooseCandidate(random);
            if (IsInsideExclusionRadius(candidate, exclusionCenter, minimumDistanceFromExclusion))
            {
                return false;
            }

            CombatEncounterSpawnCandidateResult candidateResult = validateCandidate(candidate);
            if (!candidateResult.IsValid)
            {
                return false;
            }

            spawnPosition = candidateResult.SurfacePoint;
            Vector3 lookDirection = exclusionCenter - spawnPosition;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                lookDirection = Vector3.forward;
            }

            spawnRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            return true;
        }

        private static bool IsInsideExclusionRadius(
            Vector3 candidate,
            Vector3 exclusionCenter,
            float minimumDistanceFromExclusion)
        {
            Vector2 candidateXZ = new(candidate.x, candidate.z);
            Vector2 exclusionXZ = new(exclusionCenter.x, exclusionCenter.z);
            return Vector2.Distance(candidateXZ, exclusionXZ) < minimumDistanceFromExclusion;
        }
    }

    public static class EncounterSpawnZoneUtility
    {
        public static List<GameObject> BuildRoundRobinAssignments(IReadOnlyList<GameObject> spawnZones, int desiredSpawnCount)
        {
            List<GameObject> validZones = CollectValidZones(spawnZones);
            if (desiredSpawnCount <= 0 || validZones.Count == 0)
            {
                return new List<GameObject>(0);
            }

            List<GameObject> assignments = new(desiredSpawnCount);
            for (int spawnIndex = 0; spawnIndex < desiredSpawnCount; spawnIndex++)
            {
                assignments.Add(validZones[spawnIndex % validZones.Count]);
            }

            return assignments;
        }

        public static Vector3 SampleCandidatePoint(GameObject spawnZone, System.Random random)
        {
            if (spawnZone == null)
            {
                return Vector3.zero;
            }

            random ??= new System.Random();

            if (spawnZone.TryGetComponent(out BoxCollider boxCollider) && boxCollider.enabled)
            {
                return SampleBoxCollider(boxCollider, random);
            }

            if (spawnZone.TryGetComponent(out Collider collider) && collider.enabled)
            {
                return SampleBounds(collider.bounds, spawnZone.transform.position.y, random);
            }

            return spawnZone.transform.position;
        }

        private static List<GameObject> CollectValidZones(IReadOnlyList<GameObject> spawnZones)
        {
            List<GameObject> validZones = new();
            if (spawnZones == null)
            {
                return validZones;
            }

            for (int zoneIndex = 0; zoneIndex < spawnZones.Count; zoneIndex++)
            {
                GameObject spawnZone = spawnZones[zoneIndex];
                if (spawnZone == null || !spawnZone.activeInHierarchy)
                {
                    continue;
                }

                validZones.Add(spawnZone);
            }

            return validZones;
        }

        private static Vector3 SampleBoxCollider(BoxCollider boxCollider, System.Random random)
        {
            Vector3 halfSize = boxCollider.size * 0.5f;
            Vector3 localPoint = boxCollider.center + new Vector3(
                Mathf.Lerp(-halfSize.x, halfSize.x, (float)random.NextDouble()),
                Mathf.Lerp(-halfSize.y, halfSize.y, (float)random.NextDouble()),
                Mathf.Lerp(-halfSize.z, halfSize.z, (float)random.NextDouble()));
            return boxCollider.transform.TransformPoint(localPoint);
        }

        private static Vector3 SampleBounds(Bounds bounds, float fallbackY, System.Random random)
        {
            return new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble()),
                bounds.size.y > 0.001f ? Mathf.Lerp(bounds.min.y, bounds.max.y, (float)random.NextDouble()) : fallbackY,
                Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble()));
        }
    }
}
