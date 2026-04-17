using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "City Builder/Buildings/Building Definition")]
    public class BuildingDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string m_Id = "building_id";
        [SerializeField] string m_DisplayName = "New Building";

        [Header("Presentation")]
        [SerializeField] Sprite m_Icon;
        [SerializeField] GameObject m_ModelPrefab;
        [SerializeField] GameObject m_BuildPrefab;
        [SerializeField] float m_ModelVerticalOffset;

        [Header("Legacy Direct Prefab")]
        [SerializeField] GameObject m_Prefab;

        [Header("Progression")]
        [SerializeField] bool m_StartUnlocked = true;
        [SerializeField, Min(1)] int m_RequiredLevel = 1;
        [SerializeField, Min(0)] int m_BuildCost = 100;
        [SerializeField, Min(0)] int m_MaintenanceCostPerTick = 1;
        [SerializeField, Min(0)] int m_ExperienceReward = 5;

        [Header("Simulation")]
        [SerializeField] BuildingSimulationCategory m_Category = BuildingSimulationCategory.People;
        [SerializeField, Min(0)] int m_PopulationCapacity;
        [SerializeField, Min(0)] int m_IncomePerTick;
        [SerializeField] float m_FlatHappinessBonus;
        [SerializeField] ResourceTypeSO[] m_RequiredResources = System.Array.Empty<ResourceTypeSO>();
        [SerializeField] ResourceCoverageArea[] m_ProvidedResourceAreas = System.Array.Empty<ResourceCoverageArea>();

        [Header("Legacy (read-only compatibility)")]
        [SerializeField, HideInInspector] ResourceAmount[] m_ResourceOutputs = System.Array.Empty<ResourceAmount>();
        [SerializeField, HideInInspector] ResourceAmount[] m_ResourceDemands = System.Array.Empty<ResourceAmount>();

        public string Id => string.IsNullOrWhiteSpace(m_Id) ? name : m_Id;
        public string DisplayName => string.IsNullOrWhiteSpace(m_DisplayName) ? name : m_DisplayName;
        public Sprite Icon => m_Icon;
        public GameObject ModelPrefab => ResolveModelPrefab();
        public GameObject BuildPrefab => m_BuildPrefab;
        public GameObject Prefab => ResolveSpawnPrefab();
        public float ModelVerticalOffset => m_ModelVerticalOffset;
        public bool StartUnlocked => m_StartUnlocked;
        public int RequiredLevel => Mathf.Max(1, m_RequiredLevel);
        public int BuildCost => m_BuildCost;
        public int MaintenanceCostPerTick => m_MaintenanceCostPerTick;
        public int ExperienceReward => m_ExperienceReward;
        public BuildingSimulationCategory Category => m_Category;
        public int PopulationCapacity => Mathf.Max(0, m_PopulationCapacity);
        public int IncomePerTick => Mathf.Max(0, m_IncomePerTick);
        public float FlatHappinessBonus => m_FlatHappinessBonus;
        public ResourceTypeSO[] RequiredResources => m_RequiredResources;
        public ResourceCoverageArea[] ProvidedResourceAreas => m_ProvidedResourceAreas;

        public bool UsesPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            return prefab == Prefab || prefab == m_BuildPrefab || prefab == m_Prefab || prefab == m_ModelPrefab;
        }

        public bool IsUnlockedAtLevel(int level)
        {
            return m_StartUnlocked || level >= RequiredLevel;
        }

        public bool RequiresResource(ResourceTypeSO resourceType)
        {
            if (resourceType == null)
            {
                return false;
            }

            if (ContainsResource(m_RequiredResources, resourceType))
            {
                return true;
            }

            return GetAmountForResource(m_ResourceDemands, resourceType) > 0.001f;
        }

        public float GetCoverageRadius(ResourceTypeSO resourceType)
        {
            float radius = GetCoverageRadiusForResource(m_ProvidedResourceAreas, resourceType);
            if (radius > 0.001f)
            {
                return radius;
            }

            return GetAmountForResource(m_ResourceOutputs, resourceType);
        }

        static float GetAmountForResource(ResourceAmount[] values, ResourceTypeSO resourceType)
        {
            if (resourceType == null || values == null)
            {
                return 0f;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].resourceType == resourceType)
                {
                    return Mathf.Max(0f, values[i].amount);
                }
            }

            return 0f;
        }

        static bool ContainsResource(ResourceTypeSO[] values, ResourceTypeSO resourceType)
        {
            if (values == null || resourceType == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == resourceType)
                {
                    return true;
                }
            }

            return false;
        }

        static float GetCoverageRadiusForResource(ResourceCoverageArea[] values, ResourceTypeSO resourceType)
        {
            if (values == null || resourceType == null)
            {
                return 0f;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].resourceType == resourceType)
                {
                    return Mathf.Max(0f, values[i].radius);
                }
            }

            return 0f;
        }

        GameObject ResolveSpawnPrefab()
        {
            if (m_BuildPrefab != null)
            {
                return m_BuildPrefab;
            }

            if (m_Prefab != null)
            {
                return m_Prefab;
            }

            return m_ModelPrefab;
        }

        GameObject ResolveModelPrefab()
        {
            if (m_ModelPrefab != null)
            {
                return m_ModelPrefab;
            }

            return m_Prefab;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            m_RequiredLevel = Mathf.Max(1, m_RequiredLevel);
            m_BuildCost = Mathf.Max(0, m_BuildCost);
            m_MaintenanceCostPerTick = Mathf.Max(0, m_MaintenanceCostPerTick);
            m_ExperienceReward = Mathf.Max(0, m_ExperienceReward);
            m_PopulationCapacity = Mathf.Max(0, m_PopulationCapacity);
            m_IncomePerTick = Mathf.Max(0, m_IncomePerTick);

            if (m_ProvidedResourceAreas != null)
            {
                for (int i = 0; i < m_ProvidedResourceAreas.Length; i++)
                {
                    ResourceCoverageArea area = m_ProvidedResourceAreas[i];
                    area.radius = Mathf.Max(0f, area.radius);
                    m_ProvidedResourceAreas[i] = area;
                }
            }

            if (m_ResourceOutputs != null)
            {
                for (int i = 0; i < m_ResourceOutputs.Length; i++)
                {
                    ResourceAmount amount = m_ResourceOutputs[i];
                    amount.amount = Mathf.Max(0f, amount.amount);
                    m_ResourceOutputs[i] = amount;
                }
            }

            if (m_ResourceDemands != null)
            {
                for (int i = 0; i < m_ResourceDemands.Length; i++)
                {
                    ResourceAmount amount = m_ResourceDemands[i];
                    amount.amount = Mathf.Max(0f, amount.amount);
                    m_ResourceDemands[i] = amount;
                }
            }
        }
#endif
    }
}
