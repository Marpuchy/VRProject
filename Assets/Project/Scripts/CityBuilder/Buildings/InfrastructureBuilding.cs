using System;
using UnityEngine;

namespace CityBuilder.Buildings
{
    public enum InfrastructureType { WaterTower, PowerPlant }

    public class InfrastructureBuilding : BaseBuilding
    {
        [Header("Infrastructure")]
        [SerializeField] private InfrastructureType infrastructureType;
        [SerializeField] private float outputPerTick = 50f;
        [SerializeField] private float coverageRadius = 30f;
        [SerializeField] private float fuelCapacity = 200f;
        [SerializeField] private float fuelConsumptionPerTick = 5f;

        private float _currentFuel;

        public event Action OnFuelDepleted;
        public event Action<float> OnFuelChanged;

        public InfrastructureType InfrastructureType => infrastructureType;
        public float CoverageRadius => coverageRadius;
        public float CurrentFuel => _currentFuel;
        public float FuelCapacity => fuelCapacity;
        public bool HasFuel => _currentFuel > 0f;

        public float GetOutputThisTick()
        {
            if (!IsOperational) return 0f;
            if (infrastructureType == InfrastructureType.PowerPlant && !HasFuel) return 0f;

            return outputPerTick;
        }

        public void TickFuelConsumption()
        {
            if (!IsOperational || infrastructureType != InfrastructureType.PowerPlant) return;

            _currentFuel = Mathf.Max(0, _currentFuel - fuelConsumptionPerTick);
            OnFuelChanged?.Invoke(_currentFuel);

            if (_currentFuel <= 0f)
                OnFuelDepleted?.Invoke();
        }

        public void Refuel(float amount)
        {
            _currentFuel = Mathf.Min(fuelCapacity, _currentFuel + amount);
            OnFuelChanged?.Invoke(_currentFuel);
        }

        public bool IsWithinCoverage(Vector3 position)
        {
            return Vector3.Distance(transform.position, position) <= coverageRadius;
        }

        protected override void OnConstructionCompleted()
        {
            _currentFuel = fuelCapacity;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = infrastructureType == InfrastructureType.WaterTower
                ? new Color(0.2f, 0.5f, 1f, 0.25f)
                : new Color(1f, 0.8f, 0.1f, 0.25f);

            Gizmos.DrawSphere(transform.position, coverageRadius);
        }
#endif
    }
}
