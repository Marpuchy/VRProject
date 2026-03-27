using System.Collections.Generic;
using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class CityBuildingRegistry : MonoBehaviour
    {
        [SerializeField] GridDefinition m_GridDefinition;

        readonly List<PlacedBuildingRuntime> m_Buildings = new();
        readonly Dictionary<Vector2Int, PlacedBuildingRuntime> m_BuildingsByCell = new();

        public IReadOnlyList<PlacedBuildingRuntime> Buildings => m_Buildings;

        void Awake()
        {
            if (m_GridDefinition == null)
            {
                m_GridDefinition = FindFirstObjectByType<GridDefinition>();
            }
        }

        public bool IsCellOccupied(Vector3 worldPosition, out PlacedBuildingRuntime runtime)
        {
            CleanupDestroyedEntries();
            Vector2Int cell = WorldToCell(worldPosition);
            return m_BuildingsByCell.TryGetValue(cell, out runtime) && runtime != null;
        }

        public bool TryRegister(GameObject instance, BuildingDefinitionSO definition, Vector3 worldPosition, out PlacedBuildingRuntime runtime, out string reason)
        {
            CleanupDestroyedEntries();

            if (instance == null)
            {
                runtime = null;
                reason = "Cannot register a null building instance.";
                return false;
            }

            Vector2Int cell = WorldToCell(worldPosition);
            if (m_BuildingsByCell.TryGetValue(cell, out PlacedBuildingRuntime existing) && existing != null && existing.gameObject != instance)
            {
                runtime = null;
                reason = $"Grid cell {cell} is already occupied by {existing.gameObject.name}.";
                return false;
            }

            runtime = instance.GetComponent<PlacedBuildingRuntime>();
            if (runtime == null)
            {
                runtime = instance.AddComponent<PlacedBuildingRuntime>();
            }

            Vector3 snappedPosition = CellToWorld(cell, worldPosition.y);
            runtime.Initialize(this, definition, cell, snappedPosition);

            if (!m_Buildings.Contains(runtime))
            {
                m_Buildings.Add(runtime);
            }

            m_BuildingsByCell[cell] = runtime;
            reason = null;
            return true;
        }

        public void Unregister(PlacedBuildingRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            m_Buildings.Remove(runtime);

            if (m_BuildingsByCell.TryGetValue(runtime.GridCell, out PlacedBuildingRuntime stored) && stored == runtime)
            {
                m_BuildingsByCell.Remove(runtime.GridCell);
            }

            runtime.ClearOwnerRegistry();
        }

        void CleanupDestroyedEntries()
        {
            for (int i = m_Buildings.Count - 1; i >= 0; i--)
            {
                if (m_Buildings[i] != null)
                {
                    continue;
                }

                m_Buildings.RemoveAt(i);
            }

            List<Vector2Int> keysToRemove = null;
            foreach (KeyValuePair<Vector2Int, PlacedBuildingRuntime> pair in m_BuildingsByCell)
            {
                if (pair.Value != null)
                {
                    continue;
                }

                keysToRemove ??= new List<Vector2Int>();
                keysToRemove.Add(pair.Key);
            }

            if (keysToRemove == null)
            {
                return;
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                m_BuildingsByCell.Remove(keysToRemove[i]);
            }
        }

        Vector2Int WorldToCell(Vector3 worldPosition)
        {
            if (m_GridDefinition == null)
            {
                return new Vector2Int(
                    Mathf.RoundToInt(worldPosition.x * 10f),
                    Mathf.RoundToInt(worldPosition.z * 10f));
            }

            float cellSize = Mathf.Max(0.01f, m_GridDefinition.CellSize);
            Vector3 origin = m_GridDefinition.Origin;
            return new Vector2Int(
                Mathf.RoundToInt((worldPosition.x - origin.x) / cellSize),
                Mathf.RoundToInt((worldPosition.z - origin.z) / cellSize));
        }

        Vector3 CellToWorld(Vector2Int cell, float originalY)
        {
            if (m_GridDefinition == null)
            {
                return new Vector3(cell.x * 0.1f, originalY, cell.y * 0.1f);
            }

            float cellSize = Mathf.Max(0.01f, m_GridDefinition.CellSize);
            Vector3 origin = m_GridDefinition.Origin;
            return new Vector3(
                origin.x + cell.x * cellSize,
                originalY,
                origin.z + cell.y * cellSize);
        }
    }
}
