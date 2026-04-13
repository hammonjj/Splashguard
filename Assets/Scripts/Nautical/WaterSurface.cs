using System.Collections.Generic;
using BitBox.Library;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bitbox.Toymageddon.Nautical
{
    //[ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class WaterSurface : MonoBehaviourBase
    {
        private const int TargetWaveCount = 4;
        private const float Gravity = 9.8f;
        private const float MinimumDimension = 0.5f;
        private const float MinimumVertexSpacing = 0.25f;

        private static readonly List<WaterSurface> ActiveSurfaces = new();
        private static readonly int[] WaveIds =
        {
            Shader.PropertyToID("_WaveA"),
            Shader.PropertyToID("_WaveB"),
            Shader.PropertyToID("_WaveC"),
            Shader.PropertyToID("_WaveD"),
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int FlowMapId = Shader.PropertyToID("_FlowMap");
        private static readonly int DerivHeightMapId = Shader.PropertyToID("_DerivHeightMap");
        private static readonly int FlowTilingId = Shader.PropertyToID("_Tiling");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_Speed");
        private static readonly int FlowStrengthId = Shader.PropertyToID("_FlowStrength");
        private static readonly int FlowOffsetId = Shader.PropertyToID("_FlowOffset");
        private static readonly int UJumpId = Shader.PropertyToID("_UJump");
        private static readonly int VJumpId = Shader.PropertyToID("_VJump");
        private static readonly int HeightScaleId = Shader.PropertyToID("_HeightScale");
        private static readonly int HeightScaleModulatedId = Shader.PropertyToID("_HeightScaleModulated");
        private static readonly int SimulationTimeId = Shader.PropertyToID("_SimulationTime");

        [Header("Geometry")]
        [SerializeField] private float _width = 100f;
        [SerializeField] private float _length = 100f;
        [SerializeField] private float _vertexSpacing = 1f;

        [Header("Appearance")]
        [SerializeField] private Color _baseColor = new(0.30588236f, 0.5137255f, 0.6627451f, 0.85f);
        [SerializeField][Range(0f, 1f)] private float _smoothness = 0.82f;
        [SerializeField][Range(0f, 1f)] private float _metallic;

        [Header("Distortion")]
        [SerializeField] private Texture2D _flowMap;
        [SerializeField] private Texture2D _derivHeightMap;
        [SerializeField][Min(0.01f)] private float _flowTiling = 3f;
        [SerializeField] private float _flowSpeed = 0.5f;
        [SerializeField] private float _flowStrength = 0.1f;
        [SerializeField] private float _flowOffset;
        [SerializeField] private Vector2 _uvJump = new(0.24f, 0.2083333f);
        [SerializeField] private float _heightScale = 0.1f;
        [SerializeField] private float _heightScaleModulated = 0.9f;

        [Header("Waves")]
        [SerializeField] private WaterWaveSettings[] _waves;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _generatedMesh;
        private MaterialPropertyBlock _propertyBlock;
        private bool _scaleWarningIssued;

        public float Width => _width;
        public float Length => _length;

        protected override void OnAwakened()
        {
            if (_waves == null || _waves.Length != TargetWaveCount)
            {
                _waves = CreateDefaultWaves();
            }
        }

        protected override void OnEnabled()
        {
            CacheComponents();
            EnsureWaveArray();
            RegisterSurface();
            EnsureGeneratedMesh();
            ApplyMaterialProperties();
            WarnIfScaled();
        }

        protected override void OnDisabled()
        {
            UnregisterSurface();
        }

        protected override void OnUpdated()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ApplyMaterialProperties();
        }

        protected override void OnDestroyed()
        {
            UnregisterSurface();
            DestroyGeneratedMesh();
        }

        private void OnValidate()
        {
            CacheComponents();
            EnsureWaveArray();
            EnsureGeneratedMesh();
            ApplyMaterialProperties();
            WarnIfScaled();
        }

        public bool Contains(Vector3 worldPoint)
        {
            var localPoint = transform.InverseTransformPoint(worldPoint);
            return ContainsLocal(localPoint);
        }

        public bool TrySample(Vector3 worldPoint, out WaterSample sample)
        {
            if (!Contains(worldPoint))
            {
                sample = default;
                return false;
            }

            sample = SampleUnchecked(worldPoint);
            return true;
        }

        public static bool TryFindSurface(Vector3 worldPoint, out WaterSurface surface, out WaterSample sample)
        {
            surface = null;
            sample = default;

            var bestScore = float.MaxValue;
            for (var i = 0; i < ActiveSurfaces.Count; i++)
            {
                var candidate = ActiveSurfaces[i];
                if (!candidate || !candidate.isActiveAndEnabled || !candidate.Contains(worldPoint))
                {
                    continue;
                }

                var candidateSample = candidate.SampleUnchecked(worldPoint);
                var score = Mathf.Abs(candidateSample.Height - worldPoint.y);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                surface = candidate;
                sample = candidateSample;
            }

            return surface;
        }

        private void CacheComponents()
        {
            if (!_meshFilter)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            if (!_meshRenderer)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void EnsureWaveArray()
        {
            _width = Mathf.Max(MinimumDimension, _width);
            _length = Mathf.Max(MinimumDimension, _length);
            _vertexSpacing = Mathf.Max(MinimumVertexSpacing, _vertexSpacing);
            _flowTiling = Mathf.Max(0.01f, _flowTiling);
            _heightScale = Mathf.Max(0f, _heightScale);
            _heightScaleModulated = Mathf.Max(0f, _heightScaleModulated);

            if (_waves == null || _waves.Length != TargetWaveCount)
            {
                var existingWaves = _waves;
                _waves = CreateDefaultWaves();

                if (existingWaves != null)
                {
                    var copyCount = Mathf.Min(existingWaves.Length, TargetWaveCount);
                    for (var i = 0; i < copyCount; i++)
                    {
                        _waves[i] = existingWaves[i].Sanitized();
                    }
                }

                return;
            }

            for (var i = 0; i < _waves.Length; i++)
            {
                _waves[i] = _waves[i].Sanitized();
            }
        }

        private void RegisterSurface()
        {
            if (!gameObject.scene.IsValid() || ActiveSurfaces.Contains(this))
            {
                return;
            }

            ActiveSurfaces.Add(this);
        }

        private void UnregisterSurface()
        {
            ActiveSurfaces.Remove(this);
        }

        private void EnsureGeneratedMesh()
        {
            if (!_meshFilter)
            {
                return;
            }

            if (_generatedMesh == null)
            {
                _generatedMesh = _meshFilter.sharedMesh;
                if (_generatedMesh == null || !_generatedMesh.name.StartsWith("GeneratedWaterSurfaceMesh"))
                {
                    _generatedMesh = new Mesh
                    {
                        name = "GeneratedWaterSurfaceMesh",
                    };
                }
            }

            RebuildMesh(_generatedMesh);
            _meshFilter.sharedMesh = _generatedMesh;
        }

        private void DestroyGeneratedMesh()
        {
            if (_generatedMesh == null)
            {
                return;
            }

            if (_meshFilter && ReferenceEquals(_meshFilter.sharedMesh, _generatedMesh))
            {
                _meshFilter.sharedMesh = null;
            }

            if (Application.isPlaying)
            {
                Destroy(_generatedMesh);
            }
            else
            {
                DestroyImmediate(_generatedMesh);
            }

            _generatedMesh = null;
        }

        private void RebuildMesh(Mesh mesh)
        {
            var xSegments = Mathf.Max(1, Mathf.CeilToInt(_width / _vertexSpacing));
            var zSegments = Mathf.Max(1, Mathf.CeilToInt(_length / _vertexSpacing));
            var vertexCount = (xSegments + 1) * (zSegments + 1);
            var triangleCount = xSegments * zSegments * 6;

            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[triangleCount];

            var halfWidth = _width * 0.5f;
            var halfLength = _length * 0.5f;
            var xStep = _width / xSegments;
            var zStep = _length / zSegments;

            var vertexIndex = 0;
            for (var z = 0; z <= zSegments; z++)
            {
                var zPos = -halfLength + (z * zStep);
                var v = z / (float)zSegments;

                for (var x = 0; x <= xSegments; x++)
                {
                    var xPos = -halfWidth + (x * xStep);
                    vertices[vertexIndex] = new Vector3(xPos, 0f, zPos);
                    normals[vertexIndex] = Vector3.up;
                    uvs[vertexIndex] = new Vector2(x / (float)xSegments, v);
                    vertexIndex++;
                }
            }

            var triangleIndex = 0;
            for (var z = 0; z < zSegments; z++)
            {
                for (var x = 0; x < xSegments; x++)
                {
                    var root = (z * (xSegments + 1)) + x;
                    var nextRow = root + xSegments + 1;

                    triangles[triangleIndex++] = root;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = root + 1;
                    triangles[triangleIndex++] = root + 1;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = nextRow + 1;
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            var displacementPadding = GetHorizontalPadding();
            var verticalPadding = Mathf.Max(2f, GetVerticalPadding() * 2f);
            mesh.bounds = new Bounds(
                Vector3.zero,
                new Vector3(_width + displacementPadding, verticalPadding, _length + displacementPadding));
        }

        private void ApplyMaterialProperties()
        {
            if (!_meshRenderer || _meshRenderer.sharedMaterial == null)
            {
                return;
            }

            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, _baseColor);
            _propertyBlock.SetFloat(SmoothnessId, _smoothness);
            _propertyBlock.SetFloat(MetallicId, _metallic);
            _propertyBlock.SetTexture(FlowMapId, _flowMap);
            _propertyBlock.SetTexture(DerivHeightMapId, _derivHeightMap);
            _propertyBlock.SetFloat(FlowTilingId, _flowTiling);
            _propertyBlock.SetFloat(FlowSpeedId, _flowSpeed);
            _propertyBlock.SetFloat(FlowStrengthId, _flowStrength);
            _propertyBlock.SetFloat(FlowOffsetId, _flowOffset);
            _propertyBlock.SetFloat(UJumpId, _uvJump.x);
            _propertyBlock.SetFloat(VJumpId, _uvJump.y);
            _propertyBlock.SetFloat(HeightScaleId, _heightScale);
            _propertyBlock.SetFloat(HeightScaleModulatedId, _heightScaleModulated);
            _propertyBlock.SetFloat(SimulationTimeId, GetSimulationTime());

            for (var i = 0; i < TargetWaveCount; i++)
            {
                _propertyBlock.SetVector(WaveIds[i], _waves[i].ToShaderVector());
            }

            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private WaterSample SampleUnchecked(Vector3 worldPoint)
        {
            var localPoint = transform.InverseTransformPoint(worldPoint);
            var gridPoint = new Vector3(localPoint.x, 0f, localPoint.z);
            var surfacePoint = gridPoint;
            var tangent = new Vector3(1f, 0f, 0f);
            var binormal = new Vector3(0f, 0f, 1f);
            var time = GetSimulationTime();

            for (var i = 0; i < TargetWaveCount; i++)
            {
                surfacePoint += EvaluateWave(_waves[i], gridPoint, time, ref tangent, ref binormal);
            }

            var surfaceNormal = Vector3.Cross(binormal, tangent).normalized;
            var worldSurfacePoint = transform.TransformPoint(surfacePoint);
            var worldSurfaceNormal = transform.TransformDirection(surfaceNormal).normalized;
            return new WaterSample(worldSurfacePoint, worldSurfaceNormal, worldSurfacePoint.y);
        }

        private bool ContainsLocal(Vector3 localPoint)
        {
            var horizontalPadding = GetHorizontalPadding() * 0.5f;
            var halfWidth = (_width * 0.5f) + horizontalPadding;
            var halfLength = (_length * 0.5f) + horizontalPadding;
            return localPoint.x >= -halfWidth
                && localPoint.x <= halfWidth
                && localPoint.z >= -halfLength
                && localPoint.z <= halfLength;
        }

        private float GetHorizontalPadding()
        {
            var padding = 0f;
            for (var i = 0; i < _waves.Length; i++)
            {
                padding += _waves[i].Amplitude * 2f;
            }

            return padding;
        }

        private float GetVerticalPadding()
        {
            var padding = 0f;
            for (var i = 0; i < _waves.Length; i++)
            {
                padding += _waves[i].Amplitude;
            }

            return Mathf.Max(1f, padding + 0.5f);
        }

        private static float GetSimulationTime()
        {
            return Application.isPlaying ? Time.time : 0f;
        }

        private static Vector3 EvaluateWave(
            WaterWaveSettings wave,
            Vector3 gridPoint,
            float time,
            ref Vector3 tangent,
            ref Vector3 binormal)
        {
            var direction = wave.NormalizedDirection;
            var steepness = wave.Steepness;
            var wavelength = wave.Wavelength;
            var waveNumber = (2f * Mathf.PI) / wavelength;
            var phaseSpeed = Mathf.Sqrt(Gravity / waveNumber);
            var phase = waveNumber * (Vector2.Dot(direction, gridPoint.XZ()) - (phaseSpeed * time));
            var amplitude = steepness / waveNumber;
            var sine = Mathf.Sin(phase);
            var cosine = Mathf.Cos(phase);

            tangent += new Vector3(
                -direction.x * direction.x * (steepness * sine),
                direction.x * (steepness * cosine),
                -direction.x * direction.y * (steepness * sine));

            binormal += new Vector3(
                -direction.x * direction.y * (steepness * sine),
                direction.y * (steepness * cosine),
                -direction.y * direction.y * (steepness * sine));

            return new Vector3(
                direction.x * (amplitude * cosine),
                amplitude * sine,
                direction.y * (amplitude * cosine));
        }

        private void WarnIfScaled()
        {
            if (_scaleWarningIssued || transform.localScale == Vector3.one)
            {
                return;
            }

            _scaleWarningIssued = true;
            LogWarning("WaterSurface expects a root local scale of (1,1,1). Use width, length, and vertex spacing instead.");
        }

        private static WaterWaveSettings[] CreateDefaultWaves()
        {
            return new[]
            {
                new WaterWaveSettings(new Vector2(1f, 0.2f), 0.05f, 18f),
                new WaterWaveSettings(new Vector2(0.65f, 0.75f), 0.035f, 10f),
                new WaterWaveSettings(new Vector2(-0.45f, 0.9f), 0.025f, 26f),
                new WaterWaveSettings(new Vector2(0.2f, -1f), 0.015f, 6.5f),
            };
        }
    }

    internal static class Vector3WaterExtensions
    {
        public static Vector2 XZ(this Vector3 value)
        {
            return new Vector2(value.x, value.z);
        }
    }
}
