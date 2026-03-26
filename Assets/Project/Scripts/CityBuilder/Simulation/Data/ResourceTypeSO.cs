using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "ResourceType", menuName = "City Builder/Simulation/Resource Type")]
    public class ResourceTypeSO : ScriptableObject
    {
        [SerializeField] string m_Id = "resource";
        [SerializeField] string m_DisplayName = "Resource";
        [SerializeField] Color m_DisplayColor = Color.white;

        public string Id => string.IsNullOrWhiteSpace(m_Id) ? name : m_Id;
        public string DisplayName => string.IsNullOrWhiteSpace(m_DisplayName) ? name : m_DisplayName;
        public Color DisplayColor => m_DisplayColor;

        public static ResourceTypeSO CreateRuntime(string id, string displayName, Color displayColor)
        {
            ResourceTypeSO instance = CreateInstance<ResourceTypeSO>();
            instance.hideFlags = HideFlags.DontSave;
            instance.name = displayName;
            instance.m_Id = id;
            instance.m_DisplayName = displayName;
            instance.m_DisplayColor = displayColor;
            return instance;
        }
    }
}
