using BitBox.Library;
using UnityEngine;

namespace Bitbox.Splashguard.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyDestroyOnDeath : MonoBehaviourBase
    {
        [SerializeField] private GameObject _lifecycleRoot;
        [SerializeField, Min(0f)] private float _destroyDelaySeconds = 0f;
        [Header("Death VFX")]
        [SerializeField] private Transform _explosionAnchor;
        [SerializeField] private GameObject _explosionPrefab;
        [SerializeField] private Vector3 _explosionLocalOffset = new(0f, 1.2f, 0f);
        [SerializeField, Min(0.1f)] private float _explosionLifetimeSeconds = 3f;
        [SerializeField] private bool _useFallbackExplosion = true;
        [SerializeField] private bool _hideRenderersOnDeath = true;
        [SerializeField] private bool _disableCollidersOnDeath = true;

        protected override void OnEnabled()
        {
            _lifecycleRoot ??= ResolveLifecycleRoot();
            _globalMessageBus?.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus?.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
        }

        private void OnEnemyDeath(EnemyDeathEvent @event)
        {
            if (@event == null || @event.EnemyRoot != ResolveLifecycleRoot())
            {
                return;
            }

            Vector3 explosionPosition = ResolveExplosionPosition();
            DisableRuntimeComponents();
            HideDeadVessel();

            if (Application.isPlaying)
            {
                SpawnDeathExplosion(explosionPosition);
                Destroy(_lifecycleRoot, _destroyDelaySeconds);
            }
            else
            {
                DestroyImmediate(_lifecycleRoot);
            }
        }

        private void SpawnDeathExplosion(Vector3 position)
        {
            GameObject explosion = null;
            if (_explosionPrefab != null)
            {
                explosion = Instantiate(_explosionPrefab, position, Quaternion.identity);
            }
            else if (_useFallbackExplosion)
            {
                explosion = CreateFallbackExplosion(position);
            }

            if (explosion != null)
            {
                Destroy(explosion, _explosionLifetimeSeconds);
            }
        }

        private GameObject CreateFallbackExplosion(Vector3 position)
        {
            var explosionRoot = new GameObject("EnemyDeathExplosion");
            explosionRoot.transform.position = position;

            ParticleSystem fireBurst = explosionRoot.AddComponent<ParticleSystem>();
            ConfigureFireBurst(fireBurst);

            GameObject smokeObject = new("Smoke");
            smokeObject.transform.SetParent(explosionRoot.transform, false);
            ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
            ConfigureSmokeBurst(smoke);

            fireBurst.Play();
            smoke.Play();
            return explosionRoot;
        }

        private static void ConfigureFireBurst(ParticleSystem particleSystem)
        {
            PrepareParticleSystemForConfiguration(particleSystem);

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.25f, 2.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.28f, 0.04f, 1f),
                new Color(1f, 0.9f, 0.24f, 1f));
            main.gravityModifier = 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 90;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.65f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.45f, 1.25f),
                    new Keyframe(1f, 0f)));
        }

        private static void ConfigureSmokeBurst(ParticleSystem particleSystem)
        {
            PrepareParticleSystemForConfiguration(particleSystem);

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 4.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.12f, 0.1f, 0.09f, 0.65f),
                new Color(0.32f, 0.3f, 0.28f, 0.45f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 45;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.04f, 26) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.85f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.22f, 0.2f, 0.18f), 0f),
                    new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.55f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;
        }

        private static void PrepareParticleSystemForConfiguration(ParticleSystem particleSystem)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void DisableRuntimeComponents()
        {
            GameObject lifecycleRoot = ResolveLifecycleRoot();
            var brain = lifecycleRoot.GetComponentInChildren<EnemyBrain>(includeInactive: true);
            if (brain != null)
            {
                brain.enabled = false;
            }

            var motor = lifecycleRoot.GetComponentInChildren<EnemyVesselMotor>(includeInactive: true);
            if (motor != null)
            {
                motor.Stop();
                motor.enabled = false;
            }

            var weapons = lifecycleRoot.GetComponentInChildren<EnemyVesselWeaponController>(includeInactive: true);
            if (weapons != null)
            {
                weapons.ClearTarget();
                weapons.enabled = false;
            }
        }

        private void HideDeadVessel()
        {
            GameObject lifecycleRoot = ResolveLifecycleRoot();
            if (_hideRenderersOnDeath)
            {
                Renderer[] renderers = lifecycleRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].enabled = false;
                }
            }

            if (_disableCollidersOnDeath)
            {
                Collider[] colliders = lifecycleRoot.GetComponentsInChildren<Collider>(includeInactive: true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private Vector3 ResolveExplosionPosition()
        {
            if (_explosionAnchor != null)
            {
                return _explosionAnchor.position;
            }

            GameObject lifecycleRoot = ResolveLifecycleRoot();
            if (TryResolveBoundsCenter(lifecycleRoot, out Vector3 boundsCenter))
            {
                return boundsCenter + lifecycleRoot.transform.TransformVector(_explosionLocalOffset);
            }

            return lifecycleRoot.transform.TransformPoint(_explosionLocalOffset);
        }

        private static bool TryResolveBoundsCenter(GameObject root, out Vector3 center)
        {
            Bounds combinedBounds = default;
            bool hasBounds = false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasBounds)
                {
                    combinedBounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!hasBounds)
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (!hasBounds)
                    {
                        combinedBounds = colliders[i].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(colliders[i].bounds);
                    }
                }
            }

            center = hasBounds ? combinedBounds.center : root.transform.position;
            return hasBounds;
        }

        private GameObject ResolveLifecycleRoot()
        {
            if (_lifecycleRoot != null)
            {
                return _lifecycleRoot;
            }

            Rigidbody rootBody = GetComponentInParent<Rigidbody>();
            return rootBody != null ? rootBody.gameObject : gameObject;
        }
    }
}
