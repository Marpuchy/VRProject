using System;
using UnityEngine;

namespace XRMultiplayer.Combat
{
    /// <summary>
    /// Damage payload sent by weapons/projectiles/abilities.
    /// </summary>
    [Serializable]
    public struct DamageData
    {
        [Min(0f)] public float amount;
        public GameObject source;
        public Vector3 hitPoint;
        public Vector3 hitNormal;

        public DamageData(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)
        {
            this.amount = Mathf.Max(0f, amount);
            this.source = source;
            this.hitPoint = hitPoint;
            this.hitNormal = hitNormal;
        }
    }

    /// <summary>
    /// Data raised by an IDamageable after processing incoming damage.
    /// </summary>
    public struct DamageEvent
    {
        public IDamageable target;
        public DamageData damageData;
        public float previousHealth;
        public float currentHealth;

        public bool wasFatal => currentHealth <= 0f;

        public DamageEvent(IDamageable target, DamageData damageData, float previousHealth, float currentHealth)
        {
            this.target = target;
            this.damageData = damageData;
            this.previousHealth = previousHealth;
            this.currentHealth = currentHealth;
        }
    }

    /// <summary>
    /// Generic contract for any character-like object that can receive damage.
    /// </summary>
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }

        /// <summary>
        /// Fired every time damage is successfully applied.
        /// </summary>
        event Action<DamageEvent> OnHit;

        /// <summary>
        /// Alias de OnHit para equipos que prefieren el naming OnDamage.
        /// </summary>
        event Action<DamageEvent> OnDamage;

        /// <summary>
        /// Fired when damage makes health reach zero.
        /// </summary>
        event Action<DamageEvent> OnDeath;

        /// <summary>
        /// Applies incoming damage. Returns false if no damage was applied.
        /// </summary>
        bool TryReceiveDamage(DamageData damageData, out DamageEvent damageEvent);
    }
}
