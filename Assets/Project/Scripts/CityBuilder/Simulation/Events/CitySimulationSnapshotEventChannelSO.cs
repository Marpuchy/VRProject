using System;
using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "CitySimulationSnapshotEventChannel", menuName = "City Builder/Events/City Snapshot Event Channel")]
    public class CitySimulationSnapshotEventChannelSO : ScriptableObject
    {
        public event Action<CitySimulationSnapshot> EventRaised;

        public void Raise(CitySimulationSnapshot snapshot)
        {
            EventRaised?.Invoke(snapshot);
        }
    }
}
