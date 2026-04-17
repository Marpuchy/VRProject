using System.Collections.Generic;
using CityBuilder;
using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildZoneService : MonoBehaviour
    {
        [SerializeField] List<BuildZoneArea> m_Zones = new();
        MapBoundary m_MapBoundary;

        void Awake()
        {
            DiscoverZonesIfNeeded();
        }

        public bool IsBuildAllowed(Vector3 worldPosition, int currentLevel, out string reason)
        {
            DiscoverZonesIfNeeded();
            MapBoundary mapBoundary = ResolveMapBoundary();

            if (m_Zones.Count == 0)
            {
                if (mapBoundary != null && !mapBoundary.ContainsWorldPosition(worldPosition))
                {
                    reason = "This cell is outside the map boundary.";
                    return false;
                }

                reason = null;
                return true;
            }

            BuildZoneArea containingZone = null;
            for (int i = 0; i < m_Zones.Count; i++)
            {
                BuildZoneArea zone = m_Zones[i];
                if (zone != null && zone.Contains(worldPosition))
                {
                    containingZone = zone;
                    break;
                }
            }

            if (containingZone == null)
            {
                reason = "This cell is outside the unlocked build zones.";
                return false;
            }

            if (currentLevel < containingZone.RequiredLevel)
            {
                reason = $"Zone {containingZone.ZoneId} unlocks at level {containingZone.RequiredLevel}.";
                return false;
            }

            if (mapBoundary != null && !mapBoundary.ContainsWorldPosition(worldPosition))
            {
                reason = "This cell is outside the map boundary.";
                return false;
            }

            reason = null;
            return true;
        }

        public void RefreshZones(int currentLevel)
        {
            DiscoverZonesIfNeeded();

            for (int i = 0; i < m_Zones.Count; i++)
            {
                if (m_Zones[i] != null)
                {
                    m_Zones[i].SetUnlocked(currentLevel >= m_Zones[i].RequiredLevel);
                }
            }
        }

        void DiscoverZonesIfNeeded()
        {
            if (m_Zones.Count > 0)
            {
                return;
            }

            BuildZoneArea[] discoveredZones = FindObjectsByType<BuildZoneArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (discoveredZones == null || discoveredZones.Length == 0)
            {
                return;
            }

            for (int i = 0; i < discoveredZones.Length; i++)
            {
                if (discoveredZones[i] != null)
                {
                    m_Zones.Add(discoveredZones[i]);
                }
            }
        }

        MapBoundary ResolveMapBoundary()
        {
            if (m_MapBoundary == null)
            {
                MapBoundary.TryGetActiveBoundary(out m_MapBoundary);
            }

            return m_MapBoundary;
        }
    }
}
