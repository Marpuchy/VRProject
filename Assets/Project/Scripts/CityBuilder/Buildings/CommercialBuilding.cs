using System;
using UnityEngine;

namespace CityBuilder.Buildings
{
    public class CommercialBuilding : BaseBuilding
    {
        [Header("Commercial")]
        [SerializeField] private int jobSlots = 5;
        [SerializeField] private float revenuePerTick = 20f;
        [SerializeField] private float revenueMultiplierWhenFullyStaffed = 1.5f;

        private int _staffedJobs;

        public event Action<float> OnRevenueGenerated;
        public event Action<int> OnStaffingChanged;

        public int JobSlots => jobSlots;
        public int StaffedJobs => _staffedJobs;
        public bool IsFullyStaffed => _staffedJobs >= jobSlots;

        public void TickRevenue()
        {
            if (!IsOperational) return;

            float multiplier = IsFullyStaffed ? revenueMultiplierWhenFullyStaffed : (float)_staffedJobs / jobSlots;
            float revenue = revenuePerTick * multiplier;

            OnRevenueGenerated?.Invoke(revenue);
        }

        public bool TryAssignWorker()
        {
            if (!IsOperational || IsFullyStaffed) return false;

            _staffedJobs++;
            OnStaffingChanged?.Invoke(_staffedJobs);
            return true;
        }

        public void RemoveWorker()
        {
            if (_staffedJobs <= 0) return;

            _staffedJobs--;
            OnStaffingChanged?.Invoke(_staffedJobs);
        }

        protected override void OnBuildingStateChanged(BuildingState previous, BuildingState next)
        {
            if (next == BuildingState.Abandoned || next == BuildingState.Deprived)
            {
                _staffedJobs = 0;
                OnStaffingChanged?.Invoke(_staffedJobs);
            }
        }
    }
}
