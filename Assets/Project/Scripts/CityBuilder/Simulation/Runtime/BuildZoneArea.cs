using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildZoneArea : MonoBehaviour
    {
        [SerializeField] string m_ZoneId = "zone_01";
        [SerializeField, Min(1)] int m_RequiredLevel = 1;
        [SerializeField] Collider m_BoundsCollider;
        [SerializeField] GameObject[] m_LockedStateObjects;
        [SerializeField] GameObject[] m_UnlockedStateObjects;

        public string ZoneId => string.IsNullOrWhiteSpace(m_ZoneId) ? gameObject.name : m_ZoneId;
        public int RequiredLevel => Mathf.Max(1, m_RequiredLevel);

        void Reset()
        {
            if (m_BoundsCollider == null)
            {
                m_BoundsCollider = GetComponent<Collider>();
            }
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (m_BoundsCollider == null)
            {
                return false;
            }

            Vector3 closestPoint = m_BoundsCollider.ClosestPoint(worldPosition);
            return (closestPoint - worldPosition).sqrMagnitude <= 0.0001f;
        }

        public void SetUnlocked(bool unlocked)
        {
            SetObjectsActive(m_LockedStateObjects, !unlocked);
            SetObjectsActive(m_UnlockedStateObjects, unlocked);
        }

        static void SetObjectsActive(GameObject[] targets, bool isActive)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].SetActive(isActive);
                }
            }
        }
    }
}
