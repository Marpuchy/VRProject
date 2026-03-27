using System.Collections.Generic;
using UnityEngine;

namespace CityBuilderVR
{
    [CreateAssetMenu(fileName = "BuildingCatalog", menuName = "City Builder/Buildings/Building Catalog")]
    public class BuildingCatalogSO : ScriptableObject
    {
        [SerializeField] List<BuildingDefinitionSO> m_Buildings = new();

        public IReadOnlyList<BuildingDefinitionSO> Buildings => m_Buildings;
    }
}
