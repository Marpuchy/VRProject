using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildingDefinitionPrefabBinder : MonoBehaviour
    {
        [SerializeField] Transform m_ModelRoot;
        [SerializeField] bool m_RenameInstanceFromDefinition = true;
        [SerializeField] bool m_ApplyRootLayerToModel = true;
        [SerializeField] bool m_NormalizeModelBounds = true;
        [SerializeField, Min(0.01f)] float m_TargetMaxDimension = 0.2f;
        [SerializeField] bool m_CenterModelHorizontally = true;
        [SerializeField] bool m_AlignModelToGround = true;
        [SerializeField] bool m_SyncRootBoxColliderToModel = true;

        GameObject m_RuntimeModelInstance;

        public void ApplyDefinition(BuildingDefinitionSO definition)
        {
            if (definition == null)
            {
                return;
            }

            if (TryGetComponent(out BuildingDefinitionAuthoring authoring))
            {
                authoring.SetDefinition(definition);
            }

            if (m_RenameInstanceFromDefinition && !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                gameObject.name = definition.DisplayName;
            }

            RebuildModel(definition.ModelPrefab);
        }

        void RebuildModel(GameObject modelPrefab)
        {
            ClearRuntimeModel();

            if (modelPrefab == null)
            {
                return;
            }

            Transform modelRoot = ResolveModelRoot();
            GameObject modelInstance = Instantiate(modelPrefab, modelRoot);
            Transform modelTransform = modelInstance.transform;
            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.identity;
            modelTransform.localScale = Vector3.one;

            if (m_ApplyRootLayerToModel)
            {
                SetLayerRecursively(modelTransform, gameObject.layer);
            }

            if (m_NormalizeModelBounds)
            {
                NormalizeModel(modelTransform);
            }

            if (m_SyncRootBoxColliderToModel)
            {
                SyncRootBoxCollider(modelTransform);
            }

            m_RuntimeModelInstance = modelInstance;
        }

        Transform ResolveModelRoot()
        {
            if (m_ModelRoot != null)
            {
                return m_ModelRoot;
            }

            Transform existingRoot = transform.Find("ModelRoot");
            if (existingRoot != null)
            {
                m_ModelRoot = existingRoot;
                return m_ModelRoot;
            }

            GameObject modelRoot = new("ModelRoot");
            modelRoot.transform.SetParent(transform, false);
            m_ModelRoot = modelRoot.transform;
            return m_ModelRoot;
        }

        void ClearRuntimeModel()
        {
            if (m_RuntimeModelInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(m_RuntimeModelInstance);
            }
            else
            {
                DestroyImmediate(m_RuntimeModelInstance);
            }

            m_RuntimeModelInstance = null;
        }

        void NormalizeModel(Transform modelTransform)
        {
            if (modelTransform == null || !TryCalculateWorldBounds(modelTransform, out Bounds worldBounds))
            {
                return;
            }

            float maxDimension = Mathf.Max(worldBounds.size.x, worldBounds.size.y, worldBounds.size.z);
            if (maxDimension > 0.0001f)
            {
                float normalizationScale = m_TargetMaxDimension / maxDimension;
                modelTransform.localScale *= normalizationScale;
            }

            if (!TryCalculateLocalBounds(modelTransform, transform, out Bounds localBounds))
            {
                return;
            }

            Vector3 localOffset = Vector3.zero;

            if (m_CenterModelHorizontally)
            {
                localOffset.x = -localBounds.center.x;
                localOffset.z = -localBounds.center.z;
            }

            if (m_AlignModelToGround)
            {
                localOffset.y = -localBounds.min.y;
            }

            modelTransform.localPosition += localOffset;
        }

        void SyncRootBoxCollider(Transform modelTransform)
        {
            if (modelTransform == null || !TryGetComponent(out BoxCollider boxCollider))
            {
                return;
            }

            if (!TryCalculateLocalBounds(modelTransform, transform, out Bounds localBounds))
            {
                return;
            }

            boxCollider.center = localBounds.center;
            boxCollider.size = new Vector3(
                Mathf.Max(0.0001f, localBounds.size.x),
                Mathf.Max(0.0001f, localBounds.size.y),
                Mathf.Max(0.0001f, localBounds.size.z));
        }

        static bool TryCalculateWorldBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        static bool TryCalculateLocalBounds(Transform root, Transform relativeTo, out Bounds bounds)
        {
            if (!TryCalculateWorldBounds(root, out Bounds worldBounds))
            {
                bounds = default;
                return false;
            }

            Vector3[] corners =
            {
                new(worldBounds.min.x, worldBounds.min.y, worldBounds.min.z),
                new(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z),
                new(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z),
                new(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z),
                new(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z),
                new(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z),
                new(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z),
                new(worldBounds.max.x, worldBounds.max.y, worldBounds.max.z),
            };

            Vector3 localCorner = relativeTo.InverseTransformPoint(corners[0]);
            bounds = new Bounds(localCorner, Vector3.zero);

            for (int i = 1; i < corners.Length; i++)
            {
                bounds.Encapsulate(relativeTo.InverseTransformPoint(corners[i]));
            }

            return true;
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}
