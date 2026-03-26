using UnityEngine;

namespace XRMultiplayer.Combat
{
    /// <summary>
    /// Applies damage to the first IDamageable found on collision/trigger.
    /// Useful for projectiles, melee hitboxes, traps, etc.
    /// </summary>
    public class DamageOnCollision : MonoBehaviour
    {
        [SerializeField, Min(0f)] float m_DamageAmount = 10f;
        [SerializeField] LayerMask m_DamageableLayers = ~0;
        [SerializeField] bool m_UseCollisionEvents = true;
        [SerializeField] bool m_UseTriggerEvents = true;
        [SerializeField] bool m_SearchInParents = true;
        [SerializeField] bool m_OnlyApplyOnce = true;
        [SerializeField] bool m_DestroySelfAfterSuccessfulHit;
        [SerializeField] GameObject m_DamageSourceOverride;

        bool m_HasAppliedDamage;

        void OnCollisionEnter(Collision collision)
        {
            if (!m_UseCollisionEvents)
                return;

            Vector3 hitPoint = transform.position;
            Vector3 hitNormal = transform.forward;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                hitPoint = contact.point;
                hitNormal = contact.normal;
            }

            TryApplyDamage(collision.collider, hitPoint, hitNormal);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!m_UseTriggerEvents)
                return;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = -transform.forward;
            TryApplyDamage(other, hitPoint, hitNormal);
        }

        bool TryApplyDamage(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (m_OnlyApplyOnce && m_HasAppliedDamage)
                return false;

            if (!IsOnDamageableLayer(other.gameObject.layer))
                return false;

            IDamageable damageable = FindDamageable(other);
            if (damageable == null)
                return false;

            GameObject source = m_DamageSourceOverride != null ? m_DamageSourceOverride : gameObject;
            DamageData damageData = new DamageData(m_DamageAmount, source, hitPoint, hitNormal);

            if (!damageable.TryReceiveDamage(damageData, out _))
                return false;

            m_HasAppliedDamage = true;

            if (m_DestroySelfAfterSuccessfulHit)
            {
                Destroy(gameObject);
            }

            return true;
        }

        IDamageable FindDamageable(Collider other)
        {
            if (m_SearchInParents)
            {
                MonoBehaviour[] components = other.GetComponentsInParent<MonoBehaviour>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IDamageable damageable)
                        return damageable;
                }

                return null;
            }

            MonoBehaviour[] localComponents = other.GetComponents<MonoBehaviour>();
            for (int i = 0; i < localComponents.Length; i++)
            {
                if (localComponents[i] is IDamageable damageable)
                    return damageable;
            }

            return null;
        }

        bool IsOnDamageableLayer(int layer)
        {
            return (m_DamageableLayers.value & (1 << layer)) != 0;
        }

        public void SetDamageAmount(float value)
        {
            m_DamageAmount = Mathf.Max(0f, value);
        }
    }
}
