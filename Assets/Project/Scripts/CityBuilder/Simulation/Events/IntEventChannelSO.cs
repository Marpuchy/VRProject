using System;
using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "IntEventChannel", menuName = "City Builder/Events/Int Event Channel")]
    public class IntEventChannelSO : ScriptableObject
    {
        public event Action<int> EventRaised;

        public void Raise(int value)
        {
            EventRaised?.Invoke(value);
        }
    }
}
