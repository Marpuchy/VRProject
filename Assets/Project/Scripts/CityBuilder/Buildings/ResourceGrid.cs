using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityBuilder.Buildings
{
    public class ResourceGrid : MonoBehaviour
    {
        [Header("Tick Settings")]
        [SerializeField] private float tickInterval = 3f;

        private float _tickTimer;
        private readonly List<InfrastructureBuilding> _producers = new();
        private readonly List<BaseBuilding> _consumers = new();

        private float _totalWaterAvailable;
        private float _totalPowerAvailable;

        public float TotalWaterAvailable => _totalWaterAvailable;
        public float TotalPowerAvailable => _totalPowerAvailable;

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= tickInterval)
            {
                _tickTimer = 0f;
                RunSupplyTick();
            }
        }

        private void RunSupplyTick()
        {
            _totalWaterAvailable = CollectOutput(InfrastructureType.WaterTower);
            _totalPowerAvailable = CollectOutput(InfrastructureType.PowerPlant);

            float remainingWater = _totalWaterAvailable;
            float remainingPower = _totalPowerAvailable;

            var sorted = _consumers
                .Where(c => c.IsOperational)
                .OrderByDescending(c => c.Priority)
                .ToList();

            foreach (var consumer in sorted)
            {
                bool waterMet = remainingWater >= consumer.WaterDemandPerTick;
                bool powerMet = remainingPower >= consumer.PowerDemandPerTick;

                if (waterMet) remainingWater -= consumer.WaterDemandPerTick;
                if (powerMet) remainingPower -= consumer.PowerDemandPerTick;

                consumer.OnSupplyChanged(waterMet, powerMet);
                consumer.TickHealth();
            }

            foreach (var producer in _producers)
                producer.TickFuelConsumption();
        }

        private float CollectOutput(InfrastructureType type)
        {
            return _producers
                .Where(p => p.InfrastructureType == type && p.IsOperational)
                .Sum(p => p.GetOutputThisTick());
        }

        public void RegisterProducer(InfrastructureBuilding building)
        {
            if (!_producers.Contains(building))
                _producers.Add(building);
        }

        public void UnregisterProducer(InfrastructureBuilding building)
        {
            _producers.Remove(building);
        }

        public void RegisterConsumer(BaseBuilding building)
        {
            if (!_consumers.Contains(building))
                _consumers.Add(building);
        }

        public void UnregisterConsumer(BaseBuilding building)
        {
            _consumers.Remove(building);
        }
    }
}
