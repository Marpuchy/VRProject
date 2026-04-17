using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class CityBuildingPlacementService : MonoBehaviour
    {
        [SerializeField] CitySimulationController m_SimulationController;
        [SerializeField] CityBuildingRegistry m_BuildingRegistry;
        [SerializeField] BuildZoneService m_BuildZoneService;
        [SerializeField] bool m_AllowPlacementsWithoutDefinition = true;
        [SerializeField] StringEventChannelSO m_PlacementRejectedEvent;
        [SerializeField] PlacedBuildingEventChannelSO m_BuildingPlacedEvent;

        void Awake()
        {
            ResolveReferences();
        }

        public bool CanPlace(GameObject prefab, BuildingDefinitionSO explicitDefinition, Vector3 worldPosition, out string reason)
        {
            return CanPlace(prefab, explicitDefinition, worldPosition, true, out reason);
        }

        public bool CanPlace(GameObject prefab, BuildingDefinitionSO explicitDefinition, Vector3 worldPosition, bool notifyFailure, out string reason)
        {
            ResolveReferences();

            BuildingDefinitionSO resolvedDefinition = ResolveDefinition(prefab, explicitDefinition);
            if (resolvedDefinition == null && !m_AllowPlacementsWithoutDefinition)
            {
                reason = "The selected prefab is missing a BuildingDefinitionSO.";
                if (notifyFailure)
                {
                    RaisePlacementRejected(reason);
                }
                return false;
            }

            if (m_SimulationController != null && !m_SimulationController.CanConstruct(resolvedDefinition, out reason))
            {
                if (notifyFailure)
                {
                    RaisePlacementRejected(reason);
                }
                return false;
            }

            int currentLevel = m_SimulationController != null ? m_SimulationController.CurrentLevel : 1;
            if (m_BuildZoneService != null && !m_BuildZoneService.IsBuildAllowed(worldPosition, currentLevel, out reason))
            {
                if (notifyFailure)
                {
                    RaisePlacementRejected(reason);
                }
                return false;
            }

            if (m_BuildingRegistry != null && m_BuildingRegistry.IsCellOccupied(worldPosition, out _))
            {
                reason = "That grid cell is already occupied.";
                if (notifyFailure)
                {
                    RaisePlacementRejected(reason);
                }
                return false;
            }

            reason = null;
            return true;
        }

        public bool FinalizePlacement(GameObject instance, BuildingDefinitionSO explicitDefinition, Vector3 worldPosition, out string reason)
        {
            ResolveReferences();
            PlacedBuildingRuntime runtime = null;

            if (instance == null)
            {
                reason = "Cannot finalize a null building instance.";
                RaisePlacementRejected(reason);
                return false;
            }

            BuildingDefinitionSO resolvedDefinition = ResolveDefinition(instance, explicitDefinition);
            if (resolvedDefinition == null && !m_AllowPlacementsWithoutDefinition)
            {
                reason = "The placed building instance is missing a BuildingDefinitionSO.";
                RaisePlacementRejected(reason);
                return false;
            }

            AttachCoverageDebugRenderer(instance, resolvedDefinition);

            if (m_BuildingRegistry != null && !m_BuildingRegistry.TryRegister(instance, resolvedDefinition, worldPosition, out runtime, out reason))
            {
                RaisePlacementRejected(reason);
                return false;
            }

            if (m_SimulationController != null && !m_SimulationController.ApplyConstruction(resolvedDefinition, worldPosition, out reason))
            {
                if (runtime != null)
                {
                    m_BuildingRegistry.Unregister(runtime);
                }

                RaisePlacementRejected(reason);
                return false;
            }

            m_SimulationController?.PublishCurrentState();
            if (instance.TryGetComponent(out PlacedBuildingRuntime placedBuildingRuntime))
            {
                m_BuildingPlacedEvent?.Raise(placedBuildingRuntime);
            }

            reason = null;
            return true;
        }

        BuildingDefinitionSO ResolveDefinition(GameObject source, BuildingDefinitionSO explicitDefinition)
        {
            if (explicitDefinition != null)
            {
                return explicitDefinition;
            }

            if (source != null && source.TryGetComponent(out BuildingDefinitionAuthoring authoring))
            {
                return authoring.Definition;
            }

            return null;
        }

        void ResolveReferences()
        {
            if (m_SimulationController == null)
            {
                m_SimulationController = GetComponent<CitySimulationController>();
            }

            if (m_SimulationController == null)
            {
                m_SimulationController = FindFirstObjectByType<CitySimulationController>();
            }

            if (m_BuildingRegistry == null)
            {
                m_BuildingRegistry = GetComponent<CityBuildingRegistry>();
            }

            if (m_BuildingRegistry == null)
            {
                m_BuildingRegistry = FindFirstObjectByType<CityBuildingRegistry>();
            }

            if (m_BuildZoneService == null)
            {
                m_BuildZoneService = GetComponent<BuildZoneService>();
            }

            if (m_BuildZoneService == null)
            {
                m_BuildZoneService = FindFirstObjectByType<BuildZoneService>();
            }
        }

        void RaisePlacementRejected(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                m_PlacementRejectedEvent?.Raise(reason);
            }
        }

        static void AttachCoverageDebugRenderer(GameObject instance, BuildingDefinitionSO definition)
        {
            if (instance == null || definition == null || definition.ProvidedResourceAreas == null || definition.ProvidedResourceAreas.Length == 0)
            {
                return;
            }

            BuildingCoverageDebugRenderer renderer = instance.GetComponent<BuildingCoverageDebugRenderer>();
            if (renderer == null)
            {
                renderer = instance.AddComponent<BuildingCoverageDebugRenderer>();
            }

            renderer.BindDefinition(definition);
        }
    }

    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class BuildingCoverageDebugRenderer : MonoBehaviour
    {
        [SerializeField] BuildingDefinitionSO m_Definition;
        [SerializeField] bool m_ShowRuntimeRings = true;
        [SerializeField] bool m_ShowGizmos = true;
        [SerializeField, Min(16)] int m_Segments = 56;
        [SerializeField, Min(0f)] float m_HeightOffset = 0.04f;
        [SerializeField, Min(0.005f)] float m_LineWidth = 0.06f;
        [SerializeField] Material m_LineMaterial;

        readonly List<LineRenderer> m_Rings = new();
        bool m_Dirty = true;
        static Material s_DefaultLineMaterial;

        public void BindDefinition(BuildingDefinitionSO definition)
        {
            if (m_Definition == definition)
            {
                return;
            }

            m_Definition = definition;
            m_Dirty = true;
            RebuildRuntimeRingsIfNeeded();
        }

        void Awake()
        {
            ResolveDefinition();
            m_Dirty = true;
            RebuildRuntimeRingsIfNeeded();
        }

        void OnEnable()
        {
            ResolveDefinition();
            m_Dirty = true;
            RebuildRuntimeRingsIfNeeded();
        }

        void OnDisable()
        {
            ClearRuntimeRings();
        }

        void OnValidate()
        {
            m_Segments = Mathf.Max(16, m_Segments);
            m_HeightOffset = Mathf.Max(0f, m_HeightOffset);
            m_LineWidth = Mathf.Max(0.005f, m_LineWidth);
            m_Dirty = true;
            RebuildRuntimeRingsIfNeeded();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !m_ShowRuntimeRings)
            {
                return;
            }

            RebuildRuntimeRingsIfNeeded();
            ApplyRingTransformCompensationToAll();
        }

        void RebuildRuntimeRingsIfNeeded()
        {
            if (!m_Dirty)
            {
                return;
            }

            m_Dirty = false;
            ClearRuntimeRings();

            if (!Application.isPlaying || !m_ShowRuntimeRings)
            {
                return;
            }

            ResolveDefinition();
            if (m_Definition == null || m_Definition.ProvidedResourceAreas == null || m_Definition.ProvidedResourceAreas.Length == 0)
            {
                return;
            }

            ResourceCoverageArea[] areas = m_Definition.ProvidedResourceAreas;
            for (int i = 0; i < areas.Length; i++)
            {
                ResourceCoverageArea area = areas[i];
                if (area.resourceType == null || area.radius <= 0.001f)
                {
                    continue;
                }

                LineRenderer ring = CreateRingRenderer($"CoverageRing_{area.resourceType.Id}_{i}");
                if (ring == null)
                {
                    continue;
                }

                Color color = WithAlpha(area.resourceType.DisplayColor, 0.85f);
                ring.startColor = color;
                ring.endColor = color;
                ring.startWidth = m_LineWidth;
                ring.endWidth = m_LineWidth;
                ring.positionCount = m_Segments + 1;

                for (int segment = 0; segment <= m_Segments; segment++)
                {
                    float t = (float)segment / m_Segments;
                    float angle = t * Mathf.PI * 2f;
                    Vector3 point = new(Mathf.Cos(angle) * area.radius, m_HeightOffset, Mathf.Sin(angle) * area.radius);
                    ring.SetPosition(segment, point);
                }

                ApplyRingTransformCompensation(ring);
                m_Rings.Add(ring);
            }
        }

        LineRenderer CreateRingRenderer(string objectName)
        {
            Material material = ResolveLineMaterial();
            if (material == null)
            {
                return null;
            }

            GameObject ringObject = new(objectName);
            ringObject.transform.SetParent(transform, false);

            LineRenderer lineRenderer = ringObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            lineRenderer.allowOcclusionWhenDynamic = false;
            lineRenderer.material = material;
            return lineRenderer;
        }

        void ApplyRingTransformCompensationToAll()
        {
            for (int i = 0; i < m_Rings.Count; i++)
            {
                ApplyRingTransformCompensation(m_Rings[i]);
            }
        }

        void ApplyRingTransformCompensation(LineRenderer ring)
        {
            if (ring == null)
            {
                return;
            }

            ring.transform.localScale = GetInverseLossyScale();
        }

        Vector3 GetInverseLossyScale()
        {
            Vector3 lossyScale = transform.lossyScale;
            return new Vector3(
                SafeInverse(lossyScale.x),
                SafeInverse(lossyScale.y),
                SafeInverse(lossyScale.z));
        }

        static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        void ClearRuntimeRings()
        {
            for (int i = 0; i < m_Rings.Count; i++)
            {
                LineRenderer ring = m_Rings[i];
                if (ring == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(ring.gameObject);
                }
                else
                {
                    DestroyImmediate(ring.gameObject);
                }
            }

            m_Rings.Clear();
        }

        void ResolveDefinition()
        {
            if (m_Definition != null)
            {
                return;
            }

            if (TryGetComponent(out PlacedBuildingRuntime runtime) && runtime.Definition != null)
            {
                m_Definition = runtime.Definition;
                return;
            }

            if (TryGetComponent(out BuildingDefinitionAuthoring authoring) && authoring.Definition != null)
            {
                m_Definition = authoring.Definition;
            }
        }

        Material ResolveLineMaterial()
        {
            if (m_LineMaterial != null)
            {
                return m_LineMaterial;
            }

            if (s_DefaultLineMaterial != null)
            {
                return s_DefaultLineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            s_DefaultLineMaterial = new Material(shader)
            {
                name = "CoverageDebugLineMaterial",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return s_DefaultLineMaterial;
        }

        void OnDrawGizmos()
        {
            if (!m_ShowGizmos)
            {
                return;
            }

            ResolveDefinition();
            if (m_Definition == null || m_Definition.ProvidedResourceAreas == null)
            {
                return;
            }

            ResourceCoverageArea[] areas = m_Definition.ProvidedResourceAreas;
            for (int i = 0; i < areas.Length; i++)
            {
                ResourceCoverageArea area = areas[i];
                if (area.resourceType == null || area.radius <= 0.001f)
                {
                    continue;
                }

                DrawGizmoCircle(area.radius, WithAlpha(area.resourceType.DisplayColor, 0.9f));
            }
        }

        void DrawGizmoCircle(float radius, Color color)
        {
            int segments = Mathf.Max(16, m_Segments);
            Gizmos.color = color;

            Vector3 previousPoint = transform.position + new Vector3(radius, m_HeightOffset, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                Vector3 nextPoint = transform.position + new Vector3(Mathf.Cos(angle) * radius, m_HeightOffset, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
