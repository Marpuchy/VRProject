using System;
using UnityEngine;

namespace CityBuilder.Buildings
{
    public class ResidentialBuilding : BaseBuilding
    {
        [Header("Residential")]
        [SerializeField] private int maxCapacity = 10;
        [SerializeField] private float happinessDecayRate = 5f;
        [SerializeField] private float happinessRecoveryRate = 3f;
        [SerializeField] private float abandonThreshold = 0f;

        private int _currentOccupants;
        private float _happiness = 100f;

        public event Action<float> OnHappinessChanged;
        public event Action<int> OnOccupancyChanged;

        public int MaxCapacity => maxCapacity;
        public int CurrentOccupants => _currentOccupants;
        public float Happiness => _happiness;
        public bool IsFull => _currentOccupants >= maxCapacity;

        public override void OnSupplyChanged(bool waterMet, bool powerMet)
        {
            base.OnSupplyChanged(waterMet, powerMet);
        }

        public void TickHappiness()
        {
            if (!IsOperational) return;

            bool needsMet = WaterSupplied && PowerSupplied;
            float delta = needsMet ? happinessRecoveryRate : -happinessDecayRate;

            _happiness = Mathf.Clamp(_happiness + delta, 0f, 100f);
            OnHappinessChanged?.Invoke(_happiness);

            if (_happiness <= abandonThreshold && _currentOccupants > 0)
                EvictAll();
        }

        public bool TryAddOccupant()
        {
            if (!IsOperational || IsFull) return false;

            _currentOccupants++;
            OnOccupancyChanged?.Invoke(_currentOccupants);
            return true;
        }

        public void RemoveOccupant()
        {
            if (_currentOccupants <= 0) return;

            _currentOccupants--;
            OnOccupancyChanged?.Invoke(_currentOccupants);
        }

        private void EvictAll()
        {
            _currentOccupants = 0;
            OnOccupancyChanged?.Invoke(_currentOccupants);
        }

        protected override void OnBuildingStateChanged(BuildingState previous, BuildingState next)
        {
            if (next == BuildingState.Abandoned)
                EvictAll();

            if (next == BuildingState.Operational && previous == BuildingState.Abandoned)
                _happiness = 50f;
        }
    }
}