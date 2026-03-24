using System;
using UnityEngine;

namespace CityBuilder.Buildings
{
    public enum BuildingState
    {
        UnderConstruction,
        Operational,
        Deprived,
        Abandoned
    }

    public interface IResourceConsumer
    {
        float WaterDemandPerTick { get; }
        float PowerDemandPerTick { get; }
        int Priority { get; }
        void OnSupplyChanged(bool waterMet, bool powerMet);
    }

    public abstract class BaseBuilding : MonoBehaviour, IResourceConsumer
    {
        [Header("Identity")]
        [SerializeField] private string buildingName = "Building";
        [SerializeField] private string description = "";
        [SerializeField] private string buildingTypeId = "building_base";

        [Header("Construction")]
        [SerializeField] private float constructionTime = 5f;
        [SerializeField] private int buildCost = 100;
        [SerializeField] private int maintenanceCostPerCycle = 10;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float degradationRatePerTick = 2f;

        [Header("Resource Demands")]
        [SerializeField] private float waterDemandPerTick = 1f;
        [SerializeField] private float powerDemandPerTick = 1f;
        [SerializeField] private int resourcePriority = 5;

        [Header("VR")]
        [SerializeField] private Transform vrInfoAnchor;
        [SerializeField] private Renderer highlightRenderer;

        private float _currentHealth;
        private BuildingState _state = BuildingState.UnderConstruction;
        private float _constructionProgress;
        private bool _waterSupplied;
        private bool _powerSupplied;

        public event Action<BuildingState, BuildingState> OnStateChanged;
        public event Action<float> OnHealthChanged;
        public event Action OnConstructionComplete;

        public string BuildingName => buildingName;
        public string Description => description;
        public string BuildingTypeId => buildingTypeId;
        public int BuildCost => buildCost;
        public int MaintenanceCost => maintenanceCostPerCycle;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => _currentHealth;
        public BuildingState State => _state;
        public float ConstructionProgress => _constructionProgress;
        public bool IsOperational => _state == BuildingState.Operational;
        public bool WaterSupplied => _waterSupplied;
        public bool PowerSupplied => _powerSupplied;
        public Transform VRInfoAnchor => vrInfoAnchor;

        public float WaterDemandPerTick => waterDemandPerTick;
        public float PowerDemandPerTick => powerDemandPerTick;
        public int Priority => resourcePriority;

        protected virtual void Awake()
        {
            _currentHealth = maxHealth;
            _state = BuildingState.UnderConstruction;
        }

        protected virtual void Update()
        {
            if (_state == BuildingState.UnderConstruction)
                TickConstruction(Time.deltaTime);
        }

        private void TickConstruction(float deltaTime)
        {
            _constructionProgress += deltaTime / constructionTime;
            _constructionProgress = Mathf.Clamp01(_constructionProgress);

            if (_constructionProgress >= 1f)
                CompleteConstruction();
        }

        public void CompleteConstruction()
        {
            _constructionProgress = 1f;
            TransitionTo(BuildingState.Operational);
            OnConstructionComplete?.Invoke();
            OnConstructionCompleted();
        }

        public virtual void OnSupplyChanged(bool waterMet, bool powerMet)
        {
            _waterSupplied = waterMet;
            _powerSupplied = powerMet;

            if (_state == BuildingState.UnderConstruction || _state == BuildingState.Abandoned)
                return;

            bool fullySupplied = waterMet && powerMet;

            if (fullySupplied && _state == BuildingState.Deprived)
                TransitionTo(BuildingState.Operational);
            else if (!fullySupplied && _state == BuildingState.Operational)
                TransitionTo(BuildingState.Deprived);
        }

        public void TickHealth()
        {
            if (_state != BuildingState.Deprived) return;
            ApplyDamage(degradationRatePerTick);
        }

        public void ApplyDamage(float amount)
        {
            if (_state == BuildingState.Abandoned) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            OnHealthChanged?.Invoke(_currentHealth);

            if (_currentHealth <= 0f)
                TransitionTo(BuildingState.Abandoned);
        }

        public void Repair(float amount)
        {
            if (_state == BuildingState.Abandoned) return;

            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth);

            if (_state == BuildingState.Deprived && _waterSupplied && _powerSupplied)
                TransitionTo(BuildingState.Operational);
        }

        private void TransitionTo(BuildingState next)
        {
            if (_state == next) return;

            var prev = _state;
            _state = next;

            OnStateChanged?.Invoke(prev, next);
            OnBuildingStateChanged(prev, next);

            if (next == BuildingState.Operational && prev == BuildingState.Abandoned)
                _currentHealth = maxHealth;
        }

        public virtual void OnVRFocus()
        {
            if (highlightRenderer != null)
                highlightRenderer.enabled = true;
        }

        public virtual void OnVRFocusLost()
        {
            if (highlightRenderer != null)
                highlightRenderer.enabled = false;
        }

        public virtual void OnVRSelect()
        {
            Debug.Log($"[CityBuilder] Selected: {buildingName} | State: {_state} | HP: {_currentHealth:F0}/{maxHealth}");
        }

        public virtual void Demolish()
        {
            OnDemolished();
            Destroy(gameObject);
        }

        protected virtual void OnConstructionCompleted() { }
        protected virtual void OnBuildingStateChanged(BuildingState previous, BuildingState next) { }
        protected virtual void OnDemolished() { }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{buildingName}\n{_state} | HP {_currentHealth:F0}"
            );
        }
#endif
    }
}