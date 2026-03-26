using System;
using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class PlacedBuildingRuntime : MonoBehaviour
    {
        [SerializeField] string m_InstanceId;
        [SerializeField] BuildingDefinitionSO m_Definition;
        [SerializeField] Vector2Int m_GridCell;
        [SerializeField] Vector3 m_SnappedWorldPosition;

        CityBuildingRegistry m_OwnerRegistry;

        public string InstanceId => m_InstanceId;
        public BuildingDefinitionSO Definition => m_Definition;
        public Vector2Int GridCell => m_GridCell;
        public Vector3 SnappedWorldPosition => m_SnappedWorldPosition;

        public void Initialize(CityBuildingRegistry ownerRegistry, BuildingDefinitionSO definition, Vector2Int gridCell, Vector3 snappedWorldPosition)
        {
            m_OwnerRegistry = ownerRegistry;
            m_Definition = definition;
            m_GridCell = gridCell;
            m_SnappedWorldPosition = snappedWorldPosition;

            if (string.IsNullOrWhiteSpace(m_InstanceId))
            {
                m_InstanceId = Guid.NewGuid().ToString("N");
            }
        }

        public void ClearOwnerRegistry()
        {
            m_OwnerRegistry = null;
        }

        void OnDestroy()
        {
            if (m_OwnerRegistry != null)
            {
                m_OwnerRegistry.Unregister(this);
            }
        }
    }
}
