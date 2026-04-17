using UnityEngine;
using UnityEngine.Rendering;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildingDefinitionPrefabBinder : MonoBehaviour
    {
        [SerializeField] Transform m_ModelRoot;
        [SerializeField] bool m_RenameInstanceFromDefinition = true;
        [SerializeField] bool m_ApplyRootLayerToModel = true;
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

            RebuildModel(definition);
        }

        void RebuildModel(BuildingDefinitionSO definition)
        {
            ClearRuntimeModel();

            if (definition == null)
            {
                return;
            }

            Transform modelRoot = ResolveModelRoot();
            GameObject modelInstance = CreateModelInstance(definition, modelRoot);
            if (modelInstance == null)
            {
                return;
            }

            Transform modelTransform = modelInstance.transform;
            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.identity;

            if (m_ApplyRootLayerToModel)
            {
                SetLayerRecursively(modelTransform, gameObject.layer);
            }

            AlignModel(modelTransform, definition.ModelVerticalOffset);

            if (m_SyncRootBoxColliderToModel)
            {
                SyncRootBoxCollider(modelTransform);
            }

            m_RuntimeModelInstance = modelInstance;
        }

        GameObject CreateModelInstance(BuildingDefinitionSO definition, Transform modelRoot)
        {
            if (definition == null || modelRoot == null)
            {
                return null;
            }

            if (definition.GenerateGroundPatch)
            {
                return CreateGroundPatchInstance(definition, modelRoot);
            }

            if (definition.ModelPrefab == null)
            {
                return null;
            }

            return Instantiate(definition.ModelPrefab, modelRoot);
        }

        static GameObject CreateGroundPatchInstance(BuildingDefinitionSO definition, Transform parent)
        {
            GameObject patch = new("GroundPatch");
            patch.transform.SetParent(parent, false);

            MeshFilter meshFilter = patch.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = patch.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = CreateGroundPatchMesh(definition.GroundPatchRadius, 40);
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (definition.GroundPatchMaterial != null)
            {
                meshRenderer.sharedMaterial = definition.GroundPatchMaterial;
            }

            return patch;
        }

        static Mesh CreateGroundPatchMesh(float radius, int segments)
        {
            int safeSegments = Mathf.Max(12, segments);
            Vector3[] vertices = new Vector3[safeSegments + 1];
            Vector3[] normals = new Vector3[safeSegments + 1];
            Vector2[] uvs = new Vector2[safeSegments + 1];
            int[] triangles = new int[safeSegments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            float angleStep = Mathf.PI * 2f / safeSegments;
            for (int i = 0; i < safeSegments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                int vertexIndex = i + 1;

                vertices[vertexIndex] = new Vector3(x, 0f, z);
                normals[vertexIndex] = Vector3.up;
                uvs[vertexIndex] = new Vector2((x / (radius * 2f)) + 0.5f, (z / (radius * 2f)) + 0.5f);

                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = vertexIndex;
                triangles[triangleIndex + 2] = i == safeSegments - 1 ? 1 : vertexIndex + 1;
            }

            Mesh mesh = new()
            {
                name = "GroundPatchMesh"
            };
            mesh.hideFlags = HideFlags.DontSave;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
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

            if (m_RuntimeModelInstance.TryGetComponent(out MeshFilter meshFilter))
            {
                DestroyGeneratedMesh(meshFilter.sharedMesh);
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

        static void DestroyGeneratedMesh(Mesh mesh)
        {
            if (mesh == null || (mesh.hideFlags & HideFlags.DontSave) == 0)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }

        void AlignModel(Transform modelTransform, float verticalOffset)
        {
            if (modelTransform == null)
            {
                return;
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

            localOffset.y += verticalOffset;
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
