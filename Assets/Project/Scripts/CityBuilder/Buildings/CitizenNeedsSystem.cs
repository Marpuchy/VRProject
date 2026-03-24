using System.Collections.Generic;
using UnityEngine;

namespace CityBuilder.Buildings
{
    public class CitizenNeedsSystem : MonoBehaviour
    {
        [Header("Tick Settings")]
        [SerializeField] private float tickInterval = 5f;
        [SerializeField] private float attractInterval = 10f;

        private float _tickTimer;
        private float _attractTimer;

        private readonly List<ResidentialBuilding> _residentials = new();
        private readonly List<CommercialBuilding> _commercials = new();

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            _attractTimer += Time.deltaTime;

            if (_tickTimer >= tickInterval)
            {
                _tickTimer = 0f;
                RunNeedsTick();
            }

            if (_attractTimer >= attractInterval)
            {
                _attractTimer = 0f;
                RunAttractionTick();
            }
        }

        private void RunNeedsTick()
        {
            foreach (var r in _residentials)
                r.TickHappiness();

            foreach (var c in _commercials)
                c.TickRevenue();
        }

        private void RunAttractionTick()
        {
            foreach (var r in _residentials)
            {
                if (!r.IsOperational || r.IsFull) continue;

                if (r.Happiness > 50f)
                    r.TryAddOccupant();
            }

            foreach (var r in _residentials)
            {
                if (!r.IsOperational || r.CurrentOccupants == 0) continue;

                foreach (var c in _commercials)
                {
                    if (!c.IsOperational || c.IsFullyStaffed) continue;
                    c.TryAssignWorker();
                    break;
                }
            }
        }

        public void Register(ResidentialBuilding building)
        {
            if (!_residentials.Contains(building))
                _residentials.Add(building);
        }

        public void Unregister(ResidentialBuilding building)
        {
            _residentials.Remove(building);
        }

        public void Register(CommercialBuilding building)
        {
            if (!_commercials.Contains(building))
                _commercials.Add(building);
        }

        public void Unregister(CommercialBuilding building)
        {
            _commercials.Remove(building);
        }
    }
}
