using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildingPanelCatalogBinder : MonoBehaviour
    {
        [Serializable]
        public class BuildingDefinitionSelectedEvent : UnityEvent<BuildingDefinitionSO>
        {
        }

        [SerializeField] BuildingPanelUI m_Panel;
        [SerializeField] BuildingCatalogSO m_Catalog;
        [SerializeField] bool m_ApplyCatalogOnStart = true;
        [SerializeField] bool m_RebuildPanelWhenApplying = true;
        [SerializeField] bool m_IncludeDefinitionsWithoutPrefab;
        [SerializeField] BuildingDefinitionSelectedEvent m_OnDefinitionSelected = new();

        readonly List<BuildingDefinitionSO> m_SlotDefinitions = new();

        public BuildingDefinitionSelectedEvent OnDefinitionSelected => m_OnDefinitionSelected;
        public BuildingCatalogSO Catalog => m_Catalog;

        public BuildingDefinitionSO SelectedDefinition
        {
            get
            {
                return TryGetDefinitionForSlot(m_Panel != null ? m_Panel.SelectedSlotIndex : -1, out BuildingDefinitionSO definition)
                    ? definition
                    : null;
            }
        }

        void Reset()
        {
            if (m_Panel == null)
            {
                m_Panel = GetComponent<BuildingPanelUI>();
            }
        }

        void OnEnable()
        {
            if (m_Panel == null)
            {
                m_Panel = GetComponent<BuildingPanelUI>();
            }

            if (m_Panel != null)
            {
                m_Panel.OnSlotSelected.AddListener(HandleSlotSelected);
            }
        }

        void OnDisable()
        {
            if (m_Panel != null)
            {
                m_Panel.OnSlotSelected.RemoveListener(HandleSlotSelected);
            }
        }

        void Start()
        {
            if (m_ApplyCatalogOnStart)
            {
                ApplyCatalogToPanel();
            }
        }

        [ContextMenu("Apply Catalog To Panel")]
        public void ApplyCatalogToPanel()
        {
            if (m_Panel == null)
            {
                m_Panel = GetComponent<BuildingPanelUI>();
            }

            if (m_Panel == null)
            {
                Debug.LogWarning("BuildingPanelCatalogBinder requires a BuildingPanelUI reference.", this);
                return;
            }

            List<BuildingPanelUI.BuildingSlotData> slots = new();
            m_SlotDefinitions.Clear();
            int skippedWithoutPrefabCount = 0;

            if (m_Catalog != null)
            {
                IReadOnlyList<BuildingDefinitionSO> definitions = m_Catalog.Buildings;
                for (int i = 0; i < definitions.Count; i++)
                {
                    BuildingDefinitionSO definition = definitions[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    if (!m_IncludeDefinitionsWithoutPrefab && definition.Prefab == null)
                    {
                        skippedWithoutPrefabCount++;
                        continue;
                    }

                    slots.Add(new BuildingPanelUI.BuildingSlotData
                    {
                        slotName = definition.DisplayName,
                        buildingPrefab = definition.Prefab,
                        icon = definition.Icon,
                        category = BuildingPanelUI.FromSimulationCategory(definition.Category)
                    });
                    m_SlotDefinitions.Add(definition);
                }
            }
            else
            {
                Debug.LogWarning("BuildingPanelCatalogBinder has no catalog assigned.", this);
            }

            m_Panel.SetBuildingSlots(slots, m_RebuildPanelWhenApplying);

            if (slots.Count == 0)
            {
                if (skippedWithoutPrefabCount > 0)
                {
                    Debug.LogWarning(
                        $"BuildingPanelCatalogBinder produced 0 visible slots. {skippedWithoutPrefabCount} building definitions were skipped because they do not have a prefab assigned.",
                        this);
                }
                else if (m_Catalog != null)
                {
                    Debug.LogWarning("BuildingPanelCatalogBinder produced 0 visible slots because the catalog is empty.", this);
                }
            }

            EmitCurrentSelection();
        }

        public bool TryGetSelectedDefinition(out BuildingDefinitionSO definition)
        {
            return TryGetDefinitionForSlot(m_Panel != null ? m_Panel.SelectedSlotIndex : -1, out definition);
        }

        public bool TryGetDefinitionForSlot(int slotIndex, out BuildingDefinitionSO definition)
        {
            if (slotIndex < 0 || slotIndex >= m_SlotDefinitions.Count)
            {
                definition = null;
                return false;
            }

            definition = m_SlotDefinitions[slotIndex];
            return definition != null;
        }

        void HandleSlotSelected(int slotIndex, GameObject prefab)
        {
            if (TryGetDefinitionForSlot(slotIndex, out BuildingDefinitionSO definition))
            {
                m_OnDefinitionSelected.Invoke(definition);
            }
        }

        void EmitCurrentSelection()
        {
            if (TryGetSelectedDefinition(out BuildingDefinitionSO definition))
            {
                m_OnDefinitionSelected.Invoke(definition);
            }
        }
    }
}
