using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "City Builder/Simulation/Level Definition")]
    public class LevelDefinitionSO : ScriptableObject
    {
        [SerializeField, Min(1)] int m_Level = 1;
        [SerializeField, Min(0)] int m_TotalExperienceRequired;
        [SerializeField] string m_DisplayName = "Level 1";

        public int Level => Mathf.Max(1, m_Level);
        public int TotalExperienceRequired => Mathf.Max(0, m_TotalExperienceRequired);
        public string DisplayName => string.IsNullOrWhiteSpace(m_DisplayName) ? $"Level {Level}" : m_DisplayName;

        public static LevelDefinitionSO CreateRuntime(int level, int totalExperienceRequired)
        {
            LevelDefinitionSO instance = CreateInstance<LevelDefinitionSO>();
            instance.hideFlags = HideFlags.DontSave;
            instance.name = $"Runtime Level {level}";
            instance.m_Level = Mathf.Max(1, level);
            instance.m_TotalExperienceRequired = Mathf.Max(0, totalExperienceRequired);
            instance.m_DisplayName = $"Level {instance.m_Level}";
            return instance;
        }
    }
}
