using System;
using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "StringEventChannel", menuName = "City Builder/Events/String Event Channel")]
    public class StringEventChannelSO : ScriptableObject
    {
        public event Action<string> EventRaised;

        public void Raise(string value)
        {
            EventRaised?.Invoke(value);
        }
    }
}
