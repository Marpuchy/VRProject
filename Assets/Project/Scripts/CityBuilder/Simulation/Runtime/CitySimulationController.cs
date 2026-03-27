using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class CitySimulationController : MonoBehaviour
    {
        [SerializeField] GameBalanceSO m_Balance;
        [SerializeField] List<LevelDefinitionSO> m_LevelDefinitions = new();
        [SerializeField] SimulationTickSystem m_TickSystem;
        [SerializeField] CityBuildingRegistry m_BuildingRegistry;
        [SerializeField] BuildZoneService m_BuildZoneService;
        [SerializeField] CitySimulationSnapshotEventChannelSO m_CityStateChangedEvent;
        [SerializeField] CitySimulationSnapshotEventChannelSO m_GameOverEvent;
        [SerializeField] IntEventChannelSO m_LevelChangedEvent;

        readonly List<LevelDefinitionSO> m_RuntimeLevelCache = new();
        readonly List<ResourceTypeSO> m_RuntimeResourceCache = new();

        bool m_StateInitialized;
        int m_Money;
        int m_Population;
        float m_Happiness;
        float m_TargetHappiness;
        int m_Experience;
        int m_Level = 1;
        float m_CrisisSeconds;
        bool m_IsGameOver;

        readonly struct ResourceCoverageProvider
        {
            public readonly Vector3 position;
            public readonly float radiusSqr;

            public ResourceCoverageProvider(Vector3 position, float radius)
            {
                this.position = position;
                radiusSqr = Mathf.Max(0f, radius * radius);
            }
        }

        public event Action<CitySimulationSnapshot> StateChanged;

        public int CurrentMoney => m_Money;
        public int CurrentPopulation => m_Population;
        public float CurrentHappiness => m_Happiness;
        public float CurrentTargetHappiness => m_TargetHappiness;
        public int CurrentExperience => m_Experience;
        public int CurrentLevel => m_Level;
        public bool IsGameOver => m_IsGameOver;
        public GameBalanceSO Balance => m_Balance;

        void Awake()
        {
            EnsureConfiguration();
            InitializeStateIfNeeded();
        }

        void OnEnable()
        {
            EnsureConfiguration();
            InitializeStateIfNeeded();

            if (m_TickSystem != null)
            {
                m_TickSystem.TickOccurred += HandleSimulationTick;
            }

            m_BuildZoneService?.RefreshZones(m_Level);
            PublishCurrentState();
        }

        void OnDisable()
        {
            if (m_TickSystem != null)
            {
                m_TickSystem.TickOccurred -= HandleSimulationTick;
            }
        }

        public bool CanConstruct(BuildingDefinitionSO definition, out string reason)
        {
            if (m_IsGameOver)
            {
                reason = "The city is already in game over state.";
                return false;
            }

            if (definition == null)
            {
                reason = null;
                return true;
            }

            if (!definition.IsUnlockedAtLevel(m_Level))
            {
                reason = $"{definition.DisplayName} unlocks at level {definition.RequiredLevel}.";
                return false;
            }

            if (m_Money < definition.BuildCost)
            {
                reason = $"Not enough money to build {definition.DisplayName}. Cost: {definition.BuildCost}.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool ApplyConstruction(BuildingDefinitionSO definition, Vector3 worldPosition, out string reason)
        {
            if (!CanConstruct(definition, out reason))
            {
                return false;
            }

            if (definition == null)
            {
                return true;
            }

            m_Money -= definition.BuildCost;
            m_Experience += definition.ExperienceReward;

            // People buildings should immediately attract residents, then simulation ticks can
            // adjust population up/down based on city conditions.
            if (definition.PopulationCapacity > 0 && CanPeopleBuildingReceiveRequiredResources(definition, worldPosition))
            {
                int moveInCount = Mathf.Max(
                    m_Balance.MinPopulationPerNewHousing,
                    Mathf.RoundToInt(definition.PopulationCapacity * m_Balance.NewHousingInitialOccupancy));
                int housingCapacity = CalculateHousingCapacityFromRegistry();
                m_Population = Mathf.Clamp(m_Population + moveInCount, 0, housingCapacity);
            }

            UpdateLevelFromExperience();
            return true;
        }

        public void PublishCurrentState()
        {
            CitySimulationSnapshot snapshot = BuildSnapshot(
                m_TickSystem != null ? m_TickSystem.TickIndex : 0,
                0,
                0,
                EvaluateResources(out _, out _, out _));
            EmitSnapshot(snapshot);
        }

        void HandleSimulationTick(int tickIndex)
        {
            if (m_IsGameOver)
            {
                return;
            }

            IReadOnlyList<CityResourceSnapshot> resources = EvaluateResources(
                out int housingCapacity,
                out int totalMaintenance,
                out int totalBuildingIncome);

            int predictedIncome = Mathf.RoundToInt(m_Population * m_Balance.TaxIncomePerCitizenPerTick) + totalBuildingIncome;
            int netMoneyDelta = predictedIncome - totalMaintenance;
            int predictedMoney = m_Money + netMoneyDelta;

            m_TargetHappiness = CalculateTargetHappiness(resources, housingCapacity, predictedMoney);
            m_Happiness = Mathf.MoveTowards(m_Happiness, m_TargetHappiness, m_Balance.HappinessChangePerTick);
            m_Population = CalculateNextPopulation(resources, housingCapacity, predictedMoney, m_Happiness);
            m_Money = predictedMoney;

            int experienceGained = CalculateTickExperience(resources, netMoneyDelta);
            if (experienceGained > 0)
            {
                m_Experience += experienceGained;
                UpdateLevelFromExperience();
            }

            UpdateGameOverState();

            CitySimulationSnapshot snapshot = BuildSnapshot(tickIndex, netMoneyDelta, experienceGained, resources);
            EmitSnapshot(snapshot);

            if (m_IsGameOver)
            {
                m_GameOverEvent?.Raise(snapshot);
            }
        }

        IReadOnlyList<CityResourceSnapshot> EvaluateResources(
            out int housingCapacity,
            out int totalMaintenance,
            out int totalBuildingIncome)
        {
            EnsureConfiguration();

            List<CityResourceSnapshot> snapshots = new();
            List<ResourceCoverageProvider> providerBuffer = new();
            IReadOnlyList<ResourceTypeSO> trackedResources = m_Balance.TrackedResources;
            IReadOnlyList<PlacedBuildingRuntime> buildings = m_BuildingRegistry != null
                ? m_BuildingRegistry.Buildings
                : Array.Empty<PlacedBuildingRuntime>();

            housingCapacity = 0;
            totalMaintenance = 0;
            totalBuildingIncome = 0;

            for (int i = 0; i < buildings.Count; i++)
            {
                PlacedBuildingRuntime building = buildings[i];
                if (building == null || !building.gameObject.activeInHierarchy)
                {
                    continue;
                }

                BuildingDefinitionSO definition = building.Definition;
                if (definition == null)
                {
                    continue;
                }

                housingCapacity += definition.PopulationCapacity;
                totalMaintenance += definition.MaintenanceCostPerTick;
                totalBuildingIncome += definition.IncomePerTick;
            }

            for (int i = 0; i < trackedResources.Count; i++)
            {
                ResourceTypeSO resource = trackedResources[i];
                if (resource == null)
                {
                    continue;
                }

                providerBuffer.Clear();

                for (int buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
                {
                    PlacedBuildingRuntime building = buildings[buildingIndex];
                    if (building == null || !building.gameObject.activeInHierarchy || building.Definition == null)
                    {
                        continue;
                    }

                    float coverageRadius = building.Definition.GetCoverageRadius(resource);
                    if (coverageRadius > 0.001f)
                    {
                        providerBuffer.Add(new ResourceCoverageProvider(building.transform.position, coverageRadius));
                    }
                }

                int requiredBuildings = 0;
                int coveredBuildings = 0;
                for (int buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
                {
                    PlacedBuildingRuntime building = buildings[buildingIndex];
                    if (building == null || !building.gameObject.activeInHierarchy || building.Definition == null)
                    {
                        continue;
                    }

                    if (!building.Definition.RequiresResource(resource))
                    {
                        continue;
                    }

                    requiredBuildings++;
                    if (IsPositionCoveredByAnyProvider(building.transform.position, providerBuffer))
                    {
                        coveredBuildings++;
                    }
                }

                snapshots.Add(new CityResourceSnapshot(resource, coveredBuildings, requiredBuildings, providerBuffer.Count));
            }

            return snapshots;
        }

        int CalculateNextPopulation(IReadOnlyList<CityResourceSnapshot> resources, int housingCapacity, int predictedMoney, float simulatedHappiness)
        {
            if (housingCapacity <= 0)
            {
                return 0;
            }

            float averageCoverage = CalculateAverageCoverage(resources);
            if (m_Population <= 0 &&
                averageCoverage >= m_Balance.MinimumCoverageForGrowth &&
                simulatedHappiness >= m_Balance.GrowthHappinessThreshold)
            {
                return Mathf.Clamp(Mathf.Max(1, m_Balance.MinPopulationPerNewHousing), 0, housingCapacity);
            }

            bool canGrow =
                m_Population < housingCapacity &&
                averageCoverage >= m_Balance.MinimumCoverageForGrowth &&
                predictedMoney >= 0 &&
                simulatedHappiness >= m_Balance.GrowthHappinessThreshold;

            if (canGrow)
            {
                float happinessFactor = Mathf.Clamp01(simulatedHappiness / m_Balance.MaxHappiness);
                int growth = Mathf.Max(1, Mathf.RoundToInt(m_Balance.PopulationGrowthPerTick * Mathf.Lerp(0.75f, 1.75f, happinessFactor)));
                return Mathf.Clamp(m_Population + growth, 0, housingCapacity);
            }

            float declineCoverageThreshold = Mathf.Clamp01(m_Balance.MinimumCoverageForGrowth * 0.6f);
            bool shouldDecline =
                m_Population > housingCapacity ||
                averageCoverage < declineCoverageThreshold ||
                simulatedHappiness <= m_Balance.LowHappinessThreshold ||
                predictedMoney < 0;

            if (!shouldDecline)
            {
                return Mathf.Clamp(m_Population, 0, housingCapacity);
            }

            float declineSeverity = 0f;

            if (averageCoverage < declineCoverageThreshold && declineCoverageThreshold > 0.001f)
            {
                declineSeverity = Mathf.Max(declineSeverity, (declineCoverageThreshold - averageCoverage) / declineCoverageThreshold);
            }

            if (simulatedHappiness <= m_Balance.LowHappinessThreshold && m_Balance.LowHappinessThreshold > 0.01f)
            {
                declineSeverity = Mathf.Max(declineSeverity, (m_Balance.LowHappinessThreshold - simulatedHappiness) / m_Balance.LowHappinessThreshold);
            }

            if (predictedMoney < 0)
            {
                declineSeverity = Mathf.Max(declineSeverity, 0.5f);
            }

            if (m_Population > housingCapacity)
            {
                declineSeverity = Mathf.Max(declineSeverity, 1f);
            }

            int decline = Mathf.Max(1, Mathf.RoundToInt(m_Balance.PopulationDeclinePerTick * Mathf.Lerp(0.75f, 1.75f, Mathf.Clamp01(declineSeverity))));

            if (m_Population > housingCapacity)
            {
                decline = Mathf.Max(decline, m_Population - housingCapacity);
            }

            return Mathf.Clamp(m_Population - decline, 0, housingCapacity);
        }

        float CalculateTargetHappiness(IReadOnlyList<CityResourceSnapshot> resources, int housingCapacity, int predictedMoney)
        {
            float target = m_Balance.BaseHappiness;

            IReadOnlyList<PlacedBuildingRuntime> buildings = m_BuildingRegistry != null
                ? m_BuildingRegistry.Buildings
                : Array.Empty<PlacedBuildingRuntime>();

            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].Definition != null)
                {
                    target += buildings[i].Definition.FlatHappinessBonus;
                }
            }

            for (int i = 0; i < resources.Count; i++)
            {
                CityResourceSnapshot resource = resources[i];
                ResourceCoverageHappinessWeight weight = m_Balance.GetCoverageWeight(resource.ResourceType);

                if (!resource.HasRequirements)
                {
                    continue;
                }

                if (resource.Coverage < 1f)
                {
                    target -= (1f - resource.ClampedCoverage) * weight.deficitPenalty;
                    continue;
                }

                target += weight.surplusBonus;
            }

            if (predictedMoney < 0)
            {
                target -= m_Balance.NegativeBalanceHappinessPenalty;
            }

            if (housingCapacity > 0 && m_Population >= housingCapacity)
            {
                target -= m_Balance.FullHousingPenalty;
            }

            return Mathf.Clamp(target, 0f, m_Balance.MaxHappiness);
        }

        int CalculateTickExperience(IReadOnlyList<CityResourceSnapshot> resources, int netMoneyDelta)
        {
            if (m_Population <= 0)
            {
                return 0;
            }

            float averageCoverage = CalculateAverageCoverage(resources);
            if (averageCoverage < m_Balance.MinimumCoverageForGrowth || m_Happiness < m_Balance.GrowthHappinessThreshold || netMoneyDelta < 0)
            {
                return 0;
            }

            int experience = m_Balance.StableTickExperience;
            if (m_Population > 0)
            {
                experience += m_Balance.GrowthTickExperienceBonus;
            }

            return experience;
        }

        float CalculateAverageCoverage(IReadOnlyList<CityResourceSnapshot> resources)
        {
            if (resources == null || resources.Count == 0)
            {
                return 1f;
            }

            float total = 0f;
            int count = 0;
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] == null || !resources[i].HasRequirements)
                {
                    continue;
                }

                total += resources[i].ClampedCoverage;
                count++;
            }

            if (count <= 0)
            {
                return 1f;
            }

            return total / count;
        }

        bool CanPeopleBuildingReceiveRequiredResources(BuildingDefinitionSO definition, Vector3 worldPosition)
        {
            if (definition == null)
            {
                return false;
            }

            IReadOnlyList<ResourceTypeSO> trackedResources = m_Balance != null
                ? m_Balance.TrackedResources
                : Array.Empty<ResourceTypeSO>();

            if (trackedResources == null || trackedResources.Count == 0)
            {
                return true;
            }

            IReadOnlyList<PlacedBuildingRuntime> buildings = m_BuildingRegistry != null
                ? m_BuildingRegistry.Buildings
                : Array.Empty<PlacedBuildingRuntime>();

            for (int i = 0; i < trackedResources.Count; i++)
            {
                ResourceTypeSO resource = trackedResources[i];
                if (resource == null || !definition.RequiresResource(resource))
                {
                    continue;
                }

                if (!IsPositionCoveredByAnyProvider(worldPosition, resource, buildings))
                {
                    return false;
                }
            }

            return true;
        }

        bool IsPositionCoveredByAnyProvider(Vector3 worldPosition, ResourceTypeSO resourceType, IReadOnlyList<PlacedBuildingRuntime> buildings)
        {
            if (resourceType == null || buildings == null || buildings.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                PlacedBuildingRuntime provider = buildings[i];
                if (provider == null || !provider.gameObject.activeInHierarchy || provider.Definition == null)
                {
                    continue;
                }

                float radius = provider.Definition.GetCoverageRadius(resourceType);
                if (radius <= 0.001f)
                {
                    continue;
                }

                float sqrDistance = SqrDistanceXZ(provider.transform.position, worldPosition);
                if (sqrDistance <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsPositionCoveredByAnyProvider(Vector3 worldPosition, List<ResourceCoverageProvider> providers)
        {
            if (providers == null || providers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < providers.Count; i++)
            {
                float sqrDistance = SqrDistanceXZ(providers[i].position, worldPosition);
                if (sqrDistance <= providers[i].radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        static float SqrDistanceXZ(Vector3 left, Vector3 right)
        {
            float dx = left.x - right.x;
            float dz = left.z - right.z;
            return (dx * dx) + (dz * dz);
        }

        void UpdateGameOverState()
        {
            if (m_Happiness > 0.01f)
            {
                m_CrisisSeconds = 0f;
                return;
            }

            m_CrisisSeconds += m_Balance.TickIntervalSeconds;
            if (m_CrisisSeconds >= m_Balance.GameOverGraceSeconds)
            {
                m_IsGameOver = true;
            }
        }

        void UpdateLevelFromExperience()
        {
            int previousLevel = m_Level;
            IReadOnlyList<LevelDefinitionSO> definitions = GetSortedLevels();

            int resolvedLevel = 1;
            for (int i = 0; i < definitions.Count; i++)
            {
                LevelDefinitionSO definition = definitions[i];
                if (definition != null && m_Experience >= definition.TotalExperienceRequired)
                {
                    resolvedLevel = definition.Level;
                }
            }

            m_Level = Mathf.Max(1, resolvedLevel);
            if (m_Level == previousLevel)
            {
                return;
            }

            m_BuildZoneService?.RefreshZones(m_Level);
            m_LevelChangedEvent?.Raise(m_Level);
        }

        CitySimulationSnapshot BuildSnapshot(int tickIndex, int netMoneyDelta, int experienceGainedLastTick, IReadOnlyList<CityResourceSnapshot> resources)
        {
            resources ??= Array.Empty<CityResourceSnapshot>();

            int housingCapacity = 0;
            IReadOnlyList<PlacedBuildingRuntime> buildings = m_BuildingRegistry != null
                ? m_BuildingRegistry.Buildings
                : Array.Empty<PlacedBuildingRuntime>();

            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].Definition != null)
                {
                    housingCapacity += buildings[i].Definition.PopulationCapacity;
                }
            }

            return new CitySimulationSnapshot(
                tickIndex,
                m_Money,
                m_Population,
                housingCapacity,
                m_Happiness,
                m_TargetHappiness,
                m_Level,
                m_Experience,
                GetExperienceToNextLevel(),
                m_CrisisSeconds,
                m_IsGameOver,
                buildings.Count,
                netMoneyDelta,
                experienceGainedLastTick,
                resources);
        }

        int GetExperienceToNextLevel()
        {
            IReadOnlyList<LevelDefinitionSO> definitions = GetSortedLevels();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].Level > m_Level)
                {
                    return Mathf.Max(0, definitions[i].TotalExperienceRequired - m_Experience);
                }
            }

            return 0;
        }

        IReadOnlyList<LevelDefinitionSO> GetSortedLevels()
        {
            if (m_LevelDefinitions != null && m_LevelDefinitions.Count > 0)
            {
                m_LevelDefinitions.Sort((left, right) =>
                {
                    if (left == null && right == null)
                    {
                        return 0;
                    }

                    if (left == null)
                    {
                        return 1;
                    }

                    if (right == null)
                    {
                        return -1;
                    }

                    return left.TotalExperienceRequired.CompareTo(right.TotalExperienceRequired);
                });

                return m_LevelDefinitions;
            }

            if (m_RuntimeLevelCache.Count == 0)
            {
                m_RuntimeLevelCache.Add(LevelDefinitionSO.CreateRuntime(1, 0));
                m_RuntimeLevelCache.Add(LevelDefinitionSO.CreateRuntime(2, 40));
                m_RuntimeLevelCache.Add(LevelDefinitionSO.CreateRuntime(3, 120));
                m_RuntimeLevelCache.Add(LevelDefinitionSO.CreateRuntime(4, 260));
            }

            return m_RuntimeLevelCache;
        }

        void EnsureConfiguration()
        {
            if (m_BuildingRegistry == null)
            {
                m_BuildingRegistry = GetComponent<CityBuildingRegistry>();
            }

            if (m_BuildingRegistry == null)
            {
                m_BuildingRegistry = FindFirstObjectByType<CityBuildingRegistry>();
            }

            if (m_BuildingRegistry == null)
            {
                m_BuildingRegistry = gameObject.AddComponent<CityBuildingRegistry>();
            }

            if (m_BuildZoneService == null)
            {
                m_BuildZoneService = GetComponent<BuildZoneService>();
            }

            if (m_BuildZoneService == null)
            {
                m_BuildZoneService = FindFirstObjectByType<BuildZoneService>();
            }

            if (m_BuildZoneService == null)
            {
                m_BuildZoneService = gameObject.AddComponent<BuildZoneService>();
            }

            if (m_Balance == null)
            {
                EnsureRuntimeResourceDefaults();
                m_Balance = GameBalanceSO.CreateRuntimeDefault(m_RuntimeResourceCache);
            }

            if (m_TickSystem == null)
            {
                m_TickSystem = GetComponent<SimulationTickSystem>();
            }

            if (m_TickSystem == null)
            {
                m_TickSystem = FindFirstObjectByType<SimulationTickSystem>();
            }

            if (m_TickSystem == null)
            {
                m_TickSystem = gameObject.AddComponent<SimulationTickSystem>();
            }

            m_TickSystem.BindBalance(m_Balance);
        }

        void EnsureRuntimeResourceDefaults()
        {
            if (m_RuntimeResourceCache.Count > 0)
            {
                return;
            }

            m_RuntimeResourceCache.Add(ResourceTypeSO.CreateRuntime("electricity", "Electricity", new Color(1f, 0.86f, 0.2f)));
            m_RuntimeResourceCache.Add(ResourceTypeSO.CreateRuntime("water", "Water", new Color(0.25f, 0.65f, 1f)));
            m_RuntimeResourceCache.Add(ResourceTypeSO.CreateRuntime("employment", "Employment", new Color(0.4f, 0.92f, 0.5f)));
            m_RuntimeResourceCache.Add(ResourceTypeSO.CreateRuntime("education", "Education", new Color(1f, 0.55f, 0.2f)));
        }

        void InitializeStateIfNeeded()
        {
            if (m_StateInitialized)
            {
                return;
            }

            m_Money = m_Balance.StartingMoney;
            m_Population = m_Balance.StartingPopulation;
            m_Happiness = m_Balance.StartingHappiness;
            m_TargetHappiness = m_Happiness;
            m_Experience = m_Balance.StartingExperience;
            m_CrisisSeconds = 0f;
            m_IsGameOver = false;
            UpdateLevelFromExperience();
            m_StateInitialized = true;
        }

        int CalculateHousingCapacityFromRegistry()
        {
            IReadOnlyList<PlacedBuildingRuntime> buildings = m_BuildingRegistry != null
                ? m_BuildingRegistry.Buildings
                : Array.Empty<PlacedBuildingRuntime>();

            int housingCapacity = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].Definition != null)
                {
                    housingCapacity += buildings[i].Definition.PopulationCapacity;
                }
            }

            return Mathf.Max(0, housingCapacity);
        }

        void EmitSnapshot(CitySimulationSnapshot snapshot)
        {
            StateChanged?.Invoke(snapshot);
            m_CityStateChangedEvent?.Raise(snapshot);
        }
    }
}
