using System.Text;
using TMPro;
using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class CityStatsHudPresenter : MonoBehaviour
    {
        const string k_ResourceSeparator = "   |   ";

        [Header("Data Source")]
        [SerializeField] CitySimulationController m_SimulationController;
        [SerializeField] CitySimulationSnapshotEventChannelSO m_CityStateChangedEvent;

        [Header("Text References")]
        [SerializeField] TMP_Text m_MoneyText;
        [SerializeField] TMP_Text m_PopulationText;
        [SerializeField] TMP_Text m_HappinessText;
        [SerializeField] TMP_Text m_LevelText;
        [SerializeField] TMP_Text m_ResourcesText;
        [SerializeField] TMP_Text m_BuildingsText;

        readonly StringBuilder m_StringBuilder = new();
        bool m_SubscribedToEventChannel;
        bool m_SubscribedToController;

        void OnEnable()
        {
            ResolveSimulationController();
            SubscribeToSimulation();
            m_SimulationController?.PublishCurrentState();
        }

        void OnDisable()
        {
            UnsubscribeFromSimulation();
        }

        [ContextMenu("Refresh Current State")]
        public void RefreshCurrentState()
        {
            ResolveSimulationController();
            m_SimulationController?.PublishCurrentState();
        }

        void SubscribeToSimulation()
        {
            UnsubscribeFromSimulation();

            if (m_CityStateChangedEvent != null)
            {
                m_CityStateChangedEvent.EventRaised += HandleSnapshot;
                m_SubscribedToEventChannel = true;
                return;
            }

            if (m_SimulationController != null)
            {
                m_SimulationController.StateChanged += HandleSnapshot;
                m_SubscribedToController = true;
            }
        }

        void UnsubscribeFromSimulation()
        {
            if (m_SubscribedToEventChannel && m_CityStateChangedEvent != null)
            {
                m_CityStateChangedEvent.EventRaised -= HandleSnapshot;
            }

            if (m_SubscribedToController && m_SimulationController != null)
            {
                m_SimulationController.StateChanged -= HandleSnapshot;
            }

            m_SubscribedToEventChannel = false;
            m_SubscribedToController = false;
        }

        void ResolveSimulationController()
        {
            if (m_SimulationController == null)
            {
                m_SimulationController = FindFirstObjectByType<CitySimulationController>();
            }
        }

        void HandleSnapshot(CitySimulationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (m_MoneyText != null)
            {
                string moneyDeltaPrefix = snapshot.NetMoneyDelta > 0 ? "+" : string.Empty;
                m_MoneyText.text = $"{snapshot.Money} ({moneyDeltaPrefix}{snapshot.NetMoneyDelta}/tick)";
            }

            if (m_PopulationText != null)
            {
                m_PopulationText.text = $"{snapshot.Population}/{snapshot.HousingCapacity}";
            }

            if (m_HappinessText != null)
            {
                float maxHappiness = 100f;
                if (m_SimulationController != null && m_SimulationController.Balance != null)
                {
                    maxHappiness = m_SimulationController.Balance.MaxHappiness;
                }

                float happinessPercent = maxHappiness > 0.001f
                    ? Mathf.Clamp01(snapshot.Happiness / maxHappiness) * 100f
                    : 0f;
                m_HappinessText.text = $"{happinessPercent:0}%";
            }

            if (m_LevelText != null)
            {
                string experience = snapshot.ExperienceToNextLevel > 0
                    ? $"{snapshot.Experience}/{snapshot.Experience + snapshot.ExperienceToNextLevel} XP"
                    : $"{snapshot.Experience} XP";
                m_LevelText.text = $"{snapshot.Level} | {experience}";
            }

            if (m_BuildingsText != null)
            {
                m_BuildingsText.text = snapshot.BuildingCount.ToString();
            }

            if (m_ResourcesText != null)
            {
                m_ResourcesText.text = BuildResourcesText(snapshot);
            }
        }

        string BuildResourcesText(CitySimulationSnapshot snapshot)
        {
            if (snapshot.Resources == null || snapshot.Resources.Count == 0)
            {
                return "Sin datos";
            }

            m_StringBuilder.Clear();
            for (int i = 0; i < snapshot.Resources.Count; i++)
            {
                CityResourceSnapshot resource = snapshot.Resources[i];
                if (resource == null)
                {
                    continue;
                }

                if (m_StringBuilder.Length > 0)
                {
                    m_StringBuilder.Append(k_ResourceSeparator);
                }

                string resourceName = resource.ResourceType != null
                    ? resource.ResourceType.DisplayName
                    : $"Recurso {i + 1}";

                m_StringBuilder.Append(resourceName);
                m_StringBuilder.Append(": ");

                if (resource.HasRequirements)
                {
                    m_StringBuilder.Append(resource.CoveredBuildings);
                    m_StringBuilder.Append('/');
                    m_StringBuilder.Append(resource.RequiredBuildings);
                    m_StringBuilder.Append(" (");
                    m_StringBuilder.Append((resource.ClampedCoverage * 100f).ToString("0"));
                    m_StringBuilder.Append("%)");
                }
                else
                {
                    m_StringBuilder.Append(resource.ProviderCount);
                    m_StringBuilder.Append(" prov. / sin demanda");
                }
            }

            return m_StringBuilder.Length > 0
                ? m_StringBuilder.ToString()
                : "Sin datos";
        }
    }
}
