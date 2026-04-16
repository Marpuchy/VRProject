using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildingDefinitionAuthoring : MonoBehaviour
    {
        [SerializeField] BuildingDefinitionSO m_Definition;

        public BuildingDefinitionSO Definition => m_Definition;

        public void SetDefinition(BuildingDefinitionSO definition)
        {
            m_Definition = definition;
        }
    }
}
