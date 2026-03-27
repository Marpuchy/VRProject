using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityBuilderVR
{
    public enum BuildingSimulationCategory
    {
        People,
        Resource,
        Service,
        Decoration,
    }

    [Serializable]
    public struct ResourceAmount
    {
        public ResourceTypeSO resourceType;
        [Min(0f)] public float amount;
    }

    [Serializable]
    public struct ResourceCoverageArea
    {
        public ResourceTypeSO resourceType;
        [Min(0f)] public float radius;
    }

    [Serializable]
    public struct ResourceCoverageHappinessWeight
    {
        public ResourceTypeSO resourceType;
        [Min(0f)] public float deficitPenalty;
        [Min(0f)] public float surplusBonus;
    }

    public sealed class CityResourceSnapshot
    {
        public CityResourceSnapshot(ResourceTypeSO resourceType, int coveredBuildings, int requiredBuildings, int providerCount)
        {
            ResourceType = resourceType;
            RequiredBuildings = Mathf.Max(0, requiredBuildings);
            CoveredBuildings = RequiredBuildings <= 0
                ? 0
                : Mathf.Clamp(coveredBuildings, 0, RequiredBuildings);
            ProviderCount = Mathf.Max(0, providerCount);
            Coverage = RequiredBuildings <= 0
                ? 1f
                : (float)CoveredBuildings / RequiredBuildings;
        }

        public ResourceTypeSO ResourceType { get; }
        public int CoveredBuildings { get; }
        public int RequiredBuildings { get; }
        public int ProviderCount { get; }
        public float Coverage { get; }
        public bool HasRequirements => RequiredBuildings > 0;
        public int MissingBuildings => Mathf.Max(0, RequiredBuildings - CoveredBuildings);
        public float ClampedCoverage => Mathf.Clamp01(Coverage);
    }

    public sealed class CitySimulationSnapshot
    {
        public CitySimulationSnapshot(
            int tickIndex,
            int money,
            int population,
            int housingCapacity,
            float happiness,
            float targetHappiness,
            int level,
            int experience,
            int experienceToNextLevel,
            float crisisSeconds,
            bool isGameOver,
            int buildingCount,
            int netMoneyDelta,
            int experienceGainedLastTick,
            IReadOnlyList<CityResourceSnapshot> resources)
        {
            TickIndex = tickIndex;
            Money = money;
            Population = population;
            HousingCapacity = housingCapacity;
            Happiness = happiness;
            TargetHappiness = targetHappiness;
            Level = level;
            Experience = experience;
            ExperienceToNextLevel = experienceToNextLevel;
            CrisisSeconds = crisisSeconds;
            IsGameOver = isGameOver;
            BuildingCount = buildingCount;
            NetMoneyDelta = netMoneyDelta;
            ExperienceGainedLastTick = experienceGainedLastTick;
            Resources = resources ?? Array.Empty<CityResourceSnapshot>();
        }

        public int TickIndex { get; }
        public int Money { get; }
        public int Population { get; }
        public int HousingCapacity { get; }
        public float Happiness { get; }
        public float TargetHappiness { get; }
        public int Level { get; }
        public int Experience { get; }
        public int ExperienceToNextLevel { get; }
        public float CrisisSeconds { get; }
        public bool IsGameOver { get; }
        public int BuildingCount { get; }
        public int NetMoneyDelta { get; }
        public int ExperienceGainedLastTick { get; }
        public IReadOnlyList<CityResourceSnapshot> Resources { get; }
    }
}
