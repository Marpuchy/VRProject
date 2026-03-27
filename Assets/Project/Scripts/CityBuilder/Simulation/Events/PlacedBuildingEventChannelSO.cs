using System;
using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "PlacedBuildingEventChannel", menuName = "City Builder/Events/Placed Building Event Channel")]
    public class PlacedBuildingEventChannelSO : ScriptableObject
    {
        public event Action<PlacedBuildingRuntime> EventRaised;

        public void Raise(PlacedBuildingRuntime runtime)
        {
            EventRaised?.Invoke(runtime);
        }
    }
}
