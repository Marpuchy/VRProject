using System;
using UnityEngine;
using UnityEngine.Events;

namespace XRMultiplayer.Combat
{
    /// <summary>
    /// Reusable health + damage receiver for player/enemy/character GameObjects.
    /// </summary>
    public class CharacterDamageable : MonoBehaviour, IDamageable
    {
        [Serializable]
        public class DamageAmountUnityEvent : UnityEvent<float> { }

        [Serializable]
        public class HealthChangedUnityEvent : UnityEvent<float, float> { }

        [Header("Health")]
        [SerializeField, Min(1f)] float m_MaxHealth = 100f;
        [SerializeField] bool m_StartAtMaxHealth = true;
        [SerializeField, Min(0f)] float m_StartingHealth = 100f;
        [SerializeField] bool m_Invulnerable;
        [SerializeField] bool m_DisableGameObjectOnDeath;

        [Header("Events")]
        [SerializeField] DamageAmountUnityEvent m_OnDamageAmount = new();
        [SerializeField] UnityEvent m_OnDeathUnityEvent = new();
        [SerializeField] HealthChangedUnityEvent m_OnHealthChanged = new();

        float m_CurrentHealth;

        public float CurrentHealth => m_CurrentHealth;
        public float MaxHealth => m_MaxHealth;
        public bool IsAlive => m_CurrentHealth > 0f;

        public event Action<DamageEvent> OnHit;
        public event Action<DamageEvent> OnDamage;
        public event Action<DamageEvent> OnDeath;

        void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            float startingHealth = m_StartAtMaxHealth ? m_MaxHealth : m_StartingHealth;
            m_CurrentHealth = Mathf.Clamp(startingHealth, 0f, m_MaxHealth);
            m_OnHealthChanged?.Invoke(m_CurrentHealth, m_MaxHealth);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive)
                return;

            float previousHealth = m_CurrentHealth;
            m_CurrentHealth = Mathf.Clamp(m_CurrentHealth + amount, 0f, m_MaxHealth);
            if (!Mathf.Approximately(previousHealth, m_CurrentHealth))
            {
                m_OnHealthChanged?.Invoke(m_CurrentHealth, m_MaxHealth);
            }
        }

        public void SetInvulnerable(bool value)
        {
            m_Invulnerable = value;
        }

        public bool TryReceiveDamage(DamageData damageData, out DamageEvent damageEvent)
        {
            damageEvent = default;

            if (m_Invulnerable || !IsAlive || damageData.amount <= 0f)
                return false;

            float previousHealth = m_CurrentHealth;
            m_CurrentHealth = Mathf.Clamp(m_CurrentHealth - damageData.amount, 0f, m_MaxHealth);

            damageEvent = new DamageEvent(this, damageData, previousHealth, m_CurrentHealth);

            OnHit?.Invoke(damageEvent);
            OnDamage?.Invoke(damageEvent);
            m_OnDamageAmount?.Invoke(damageData.amount);
            m_OnHealthChanged?.Invoke(m_CurrentHealth, m_MaxHealth);

            if (!IsAlive)
            {
                OnDeath?.Invoke(damageEvent);
                m_OnDeathUnityEvent?.Invoke();

                if (m_DisableGameObjectOnDeath)
                {
                    gameObject.SetActive(false);
                }
            }

            return true;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            m_MaxHealth = Mathf.Max(1f, m_MaxHealth);
            m_StartingHealth = Mathf.Clamp(m_StartingHealth, 0f, m_MaxHealth);
        }
#endif
    }
}
