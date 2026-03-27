using UnityEngine;

namespace CityBuilderVR
{
    static class CitySimulationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureRuntimeServices()
        {
            if (Object.FindFirstObjectByType<BuildingPrefabSpawnController>() == null)
            {
                return;
            }

            if (Object.FindFirstObjectByType<CitySimulationController>() != null &&
                Object.FindFirstObjectByType<CityBuildingPlacementService>() != null)
            {
                return;
            }

            GameObject root = new("CityBuilder Simulation");

            if (Object.FindFirstObjectByType<CitySimulationController>() == null)
            {
                root.AddComponent<CitySimulationController>();
            }

            if (Object.FindFirstObjectByType<CityBuildingPlacementService>() == null)
            {
                root.AddComponent<CityBuildingPlacementService>();
            }
        }
    }
}
