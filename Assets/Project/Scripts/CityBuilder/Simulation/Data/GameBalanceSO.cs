using System.Collections.Generic;
using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "City Builder/Simulation/Game Balance")]
    public class GameBalanceSO : ScriptableObject
    {
        [Header("Tick")]
        [SerializeField, Min(0.1f)] float m_TickIntervalSeconds = 1f;

        [Header("Starting State")]
        [SerializeField] int m_StartingMoney = 500;
        [SerializeField, Min(0)] int m_StartingPopulation;
        [SerializeField, Range(0f, 100f)] float m_StartingHappiness = 75f;
        [SerializeField, Min(0)] int m_StartingExperience;

        [Header("Economy")]
        [SerializeField, Min(0f)] float m_TaxIncomePerCitizenPerTick = 2f;
        [SerializeField, Min(0f)] float m_PopulationGrowthPerTick = 1f;
        [SerializeField, Min(0f)] float m_PopulationDeclinePerTick = 2f;
        [SerializeField, Range(0f, 1f)] float m_NewHousingInitialOccupancy = 0.35f;
        [SerializeField, Min(0)] int m_MinPopulationPerNewHousing = 1;
        [SerializeField, Min(0)] int m_StableTickExperience = 1;
        [SerializeField, Min(0)] int m_GrowthTickExperienceBonus = 1;

        [Header("Happiness")]
        [SerializeField, Range(0f, 100f)] float m_BaseHappiness = 65f;
        [SerializeField, Range(1f, 100f)] float m_MaxHappiness = 100f;
        [SerializeField, Min(0.1f)] float m_HappinessChangePerTick = 5f;
        [SerializeField, Range(0f, 100f)] float m_GrowthHappinessThreshold = 55f;
        [SerializeField, Range(0f, 100f)] float m_LowHappinessThreshold = 30f;
        [SerializeField, Min(0f)] float m_NegativeBalanceHappinessPenalty = 10f;
        [SerializeField, Min(0f)] float m_FullHousingPenalty = 5f;
        [SerializeField, Range(0f, 1.5f)] float m_MinimumCoverageForGrowth = 0.9f;
        [SerializeField, Min(0.5f)] float m_GameOverGraceSeconds = 10f;

        [Header("Resources")]
        [SerializeField] List<ResourceTypeSO> m_TrackedResources = new();
        [SerializeField] List<ResourceCoverageHappinessWeight> m_ResourceHappinessWeights = new();

        public float TickIntervalSeconds => Mathf.Max(0.1f, m_TickIntervalSeconds);
        public int StartingMoney => m_StartingMoney;
        public int StartingPopulation => Mathf.Max(0, m_StartingPopulation);
        public float StartingHappiness => Mathf.Clamp(m_StartingHappiness, 0f, MaxHappiness);
        public int StartingExperience => Mathf.Max(0, m_StartingExperience);
        public float TaxIncomePerCitizenPerTick => Mathf.Max(0f, m_TaxIncomePerCitizenPerTick);
        public float PopulationGrowthPerTick => Mathf.Max(0f, m_PopulationGrowthPerTick);
        public float PopulationDeclinePerTick => Mathf.Max(0f, m_PopulationDeclinePerTick);
        public float NewHousingInitialOccupancy => Mathf.Clamp01(m_NewHousingInitialOccupancy);
        public int MinPopulationPerNewHousing => Mathf.Max(0, m_MinPopulationPerNewHousing);
        public int StableTickExperience => Mathf.Max(0, m_StableTickExperience);
        public int GrowthTickExperienceBonus => Mathf.Max(0, m_GrowthTickExperienceBonus);
        public float BaseHappiness => Mathf.Clamp(m_BaseHappiness, 0f, MaxHappiness);
        public float MaxHappiness => Mathf.Max(1f, m_MaxHappiness);
        public float HappinessChangePerTick => Mathf.Max(0.1f, m_HappinessChangePerTick);
        public float GrowthHappinessThreshold => Mathf.Clamp(m_GrowthHappinessThreshold, 0f, MaxHappiness);
        public float LowHappinessThreshold => Mathf.Clamp(m_LowHappinessThreshold, 0f, GrowthHappinessThreshold);
        public float NegativeBalanceHappinessPenalty => Mathf.Max(0f, m_NegativeBalanceHappinessPenalty);
        public float FullHousingPenalty => Mathf.Max(0f, m_FullHousingPenalty);
        public float MinimumCoverageForGrowth => Mathf.Max(0f, m_MinimumCoverageForGrowth);
        public float GameOverGraceSeconds => Mathf.Max(0.5f, m_GameOverGraceSeconds);
        public IReadOnlyList<ResourceTypeSO> TrackedResources => m_TrackedResources;

        public ResourceCoverageHappinessWeight GetCoverageWeight(ResourceTypeSO resourceType)
        {
            for (int i = 0; i < m_ResourceHappinessWeights.Count; i++)
            {
                if (m_ResourceHappinessWeights[i].resourceType == resourceType)
                {
                    return m_ResourceHappinessWeights[i];
                }
            }

            return new ResourceCoverageHappinessWeight
            {
                resourceType = resourceType,
                deficitPenalty = 10f,
                surplusBonus = 2f,
            };
        }

        public static GameBalanceSO CreateRuntimeDefault(IReadOnlyList<ResourceTypeSO> trackedResources)
        {
            GameBalanceSO instance = CreateInstance<GameBalanceSO>();
            instance.hideFlags = HideFlags.DontSave;
            instance.name = "Runtime Game Balance";

            instance.m_TickIntervalSeconds = 1f;
            instance.m_StartingMoney = 600;
            instance.m_StartingPopulation = 0;
            instance.m_StartingHappiness = 75f;
            instance.m_StartingExperience = 0;
            instance.m_TaxIncomePerCitizenPerTick = 2f;
            instance.m_PopulationGrowthPerTick = 1f;
            instance.m_PopulationDeclinePerTick = 2f;
            instance.m_NewHousingInitialOccupancy = 0.35f;
            instance.m_MinPopulationPerNewHousing = 1;
            instance.m_StableTickExperience = 1;
            instance.m_GrowthTickExperienceBonus = 1;
            instance.m_BaseHappiness = 65f;
            instance.m_MaxHappiness = 100f;
            instance.m_HappinessChangePerTick = 5f;
            instance.m_GrowthHappinessThreshold = 55f;
            instance.m_LowHappinessThreshold = 30f;
            instance.m_NegativeBalanceHappinessPenalty = 10f;
            instance.m_FullHousingPenalty = 5f;
            instance.m_MinimumCoverageForGrowth = 0.9f;
            instance.m_GameOverGraceSeconds = 10f;

            if (trackedResources != null)
            {
                for (int i = 0; i < trackedResources.Count; i++)
                {
                    ResourceTypeSO resource = trackedResources[i];
                    if (resource == null)
                    {
                        continue;
                    }

                    instance.m_TrackedResources.Add(resource);
                    instance.m_ResourceHappinessWeights.Add(new ResourceCoverageHappinessWeight
                    {
                        resourceType = resource,
                        deficitPenalty = GetDefaultDeficitPenaltyFor(resource.Id),
                        surplusBonus = GetDefaultSurplusBonusFor(resource.Id),
                    });
                }
            }

            return instance;
        }

        static float GetDefaultDeficitPenaltyFor(string resourceId)
        {
            return resourceId.ToLowerInvariant() switch
            {
                "water" => 18f,
                "electricity" => 18f,
                "employment" => 12f,
                "education" => 8f,
                _ => 10f,
            };
        }

        static float GetDefaultSurplusBonusFor(string resourceId)
        {
            return resourceId.ToLowerInvariant() switch
            {
                "education" => 6f,
                "employment" => 4f,
                _ => 3f,
            };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            m_TickIntervalSeconds = Mathf.Max(0.1f, m_TickIntervalSeconds);
            m_StartingPopulation = Mathf.Max(0, m_StartingPopulation);
            m_StartingHappiness = Mathf.Clamp(m_StartingHappiness, 0f, 100f);
            m_StartingExperience = Mathf.Max(0, m_StartingExperience);
            m_TaxIncomePerCitizenPerTick = Mathf.Max(0f, m_TaxIncomePerCitizenPerTick);
            m_PopulationGrowthPerTick = Mathf.Max(0f, m_PopulationGrowthPerTick);
            m_PopulationDeclinePerTick = Mathf.Max(0f, m_PopulationDeclinePerTick);
            m_NewHousingInitialOccupancy = Mathf.Clamp01(m_NewHousingInitialOccupancy);
            m_MinPopulationPerNewHousing = Mathf.Max(0, m_MinPopulationPerNewHousing);
            m_StableTickExperience = Mathf.Max(0, m_StableTickExperience);
            m_GrowthTickExperienceBonus = Mathf.Max(0, m_GrowthTickExperienceBonus);
            m_BaseHappiness = Mathf.Clamp(m_BaseHappiness, 0f, 100f);
            m_MaxHappiness = Mathf.Clamp(m_MaxHappiness, 1f, 100f);
            m_HappinessChangePerTick = Mathf.Max(0.1f, m_HappinessChangePerTick);
            m_GrowthHappinessThreshold = Mathf.Clamp(m_GrowthHappinessThreshold, 0f, m_MaxHappiness);
            m_LowHappinessThreshold = Mathf.Clamp(m_LowHappinessThreshold, 0f, m_GrowthHappinessThreshold);
            m_NegativeBalanceHappinessPenalty = Mathf.Max(0f, m_NegativeBalanceHappinessPenalty);
            m_FullHousingPenalty = Mathf.Max(0f, m_FullHousingPenalty);
            m_MinimumCoverageForGrowth = Mathf.Max(0f, m_MinimumCoverageForGrowth);
            m_GameOverGraceSeconds = Mathf.Max(0.5f, m_GameOverGraceSeconds);
        }
#endif
    }
}
