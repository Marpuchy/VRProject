using System;
using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class SimulationTickSystem : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float m_TickIntervalSeconds = 1f;
        [SerializeField] GameBalanceSO m_Balance;
        [SerializeField] IntEventChannelSO m_TickOccurredEvent;

        float m_AccumulatedTime;

        public event Action<int> TickOccurred;

        public int TickIndex { get; private set; }
        public float TickIntervalSeconds => m_Balance != null ? m_Balance.TickIntervalSeconds : Mathf.Max(0.1f, m_TickIntervalSeconds);

        public void BindBalance(GameBalanceSO balance)
        {
            m_Balance = balance;
        }

        void Update()
        {
            float tickInterval = TickIntervalSeconds;
            m_AccumulatedTime += Time.deltaTime;

            while (m_AccumulatedTime >= tickInterval)
            {
                m_AccumulatedTime -= tickInterval;
                TickIndex++;
                TickOccurred?.Invoke(TickIndex);
                m_TickOccurredEvent?.Raise(TickIndex);
            }
        }
    }
}
