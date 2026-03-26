using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if ENABLE_INPUT_SYSTEM
using Keyboard = UnityEngine.InputSystem.Keyboard;
using Mouse = UnityEngine.InputSystem.Mouse;
#endif

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildingPrefabSpawnController : MonoBehaviour
    {
        [Serializable]
        public class PrefabChangedEvent : UnityEvent<GameObject>
        {
        }

        [Serializable]
        public class PrefabSpawnedEvent : UnityEvent<GameObject>
        {
        }

        [Header("References")]
        [SerializeField] BuildingPanelUI m_BuildingPanelUI;
        [SerializeField] BuildingPanelCatalogBinder m_BuildingCatalogBinder;
        [SerializeField] CityBuildingPlacementService m_BuildingPlacementService;

        [Header("Default Spawn")]
        [SerializeField] Transform m_DefaultSpawnPoint;
        [SerializeField] Transform m_DefaultSpawnParent;
        [SerializeField] bool m_SpawnOnSelection = true;
        [SerializeField] bool m_UseSpawnPointRotation = true;
        [SerializeField] bool m_ForceIdentityRotation = true;
        [SerializeField] bool m_UseSpawnPointScale;

        [Header("Placement Mode")]
        [SerializeField] bool m_UsePreviewPlacement = true;
        [SerializeField] Transform m_PreviewFollowOrigin;
        [SerializeField] LayerMask m_PreviewRaycastMask = ~0;
        [SerializeField, Min(0.5f)] float m_PreviewRayDistance = 100f;
        [SerializeField, Min(0f)] float m_PreviewLift = 0.01f;
        [SerializeField] bool m_HidePreviewWhenNoHit = true;
        [SerializeField] bool m_DisableScriptsOnPreview = true;
        [SerializeField] Color m_PreviewTint = new(0.25f, 0.75f, 1f, 0.45f);
        [SerializeField] Color m_InvalidPreviewTint = new(1f, 0.35f, 0.35f, 0.45f);

        [Header("Debug Input")]
        [SerializeField] bool m_UseDebugKeyboardShortcuts = true;
        [SerializeField] bool m_AllowMouseClickToConfirm = true;
        [SerializeField] KeyCode m_ConfirmKey = KeyCode.Return;
        [SerializeField] KeyCode m_CancelKey = KeyCode.Escape;
        [SerializeField] KeyCode m_RotateLeftKey = KeyCode.Q;
        [SerializeField] KeyCode m_RotateRightKey = KeyCode.E;
        [SerializeField, Min(1f)] float m_RotationStepDegrees = 90f;

        [Header("XR Virtual Controller Input")]
        [SerializeField] bool m_UseXRControllerButtons = true;
        [SerializeField] XRNode m_ControllerNodeForPlacement = XRNode.RightHand;
        [SerializeField, Range(0.1f, 1f)] float m_TriggerPressThreshold = 0.75f;
        [SerializeField, Range(0.1f, 1f)] float m_StickRotateDeadzone = 0.75f;

        [Header("Grid Placement")]
        [SerializeField] bool m_SetupGridPlacementOnSpawn = true;
        [SerializeField] GridDefinition m_GridDefinition;
        [SerializeField] bool m_AddRigidbodyIfMissing = true;
        [SerializeField] bool m_AddGrabInteractableIfMissing = true;
        [SerializeField] bool m_AddGridMovementConstraintIfMissing = true;
        [SerializeField] bool m_AddHorizontalGrabConstraintIfMissing = true;
        [SerializeField] bool m_ConfigurePhysicsLikeTestCube = true;
        [SerializeField] bool m_ConfigureGrabLikeTestCube = true;
        [SerializeField] bool m_EnableGravityOnSpawn = true;
        [SerializeField, Min(0.01f)] float m_SpawnedBodyMass = 1f;

        [Header("Events")]
        [SerializeField] PrefabChangedEvent m_OnSelectedPrefabChanged = new();
        [SerializeField] PrefabSpawnedEvent m_OnPrefabSpawned = new();

        int m_SelectedSlotIndex = -1;
        GameObject m_SelectedPrefab;

        GameObject m_PreviewInstance;
        bool m_PreviewHasValidCell;
        bool m_LastPreviewValidity = true;
        float m_PreviewHalfHeight;
        float m_CurrentPreviewYaw;
        Vector3 m_LastPreviewWorldPosition;
        Quaternion m_LastPreviewWorldRotation;
        bool m_LastPrimaryButtonPressed;
        bool m_LastSecondaryButtonPressed;
        bool m_StickRotateReady = true;

        public int SelectedSlotIndex => m_SelectedSlotIndex;
        public GameObject SelectedPrefab => m_SelectedPrefab;
        public bool HasSelectedPrefab => m_SelectedPrefab != null;
        public bool IsPreviewActive => m_PreviewInstance != null;
        public bool HasValidPreviewCell => m_PreviewHasValidCell;
        public PrefabChangedEvent OnSelectedPrefabChanged => m_OnSelectedPrefabChanged;
        public PrefabSpawnedEvent OnPrefabSpawned => m_OnPrefabSpawned;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void Start()
        {
            ResolveReferences();
            TryAutoAssignPreviewFollowOrigin();
        }

        void OnEnable()
        {
            if (m_BuildingPanelUI != null)
            {
                m_BuildingPanelUI.OnSlotSelected.AddListener(HandleSlotSelected);
            }
        }

        void OnDisable()
        {
            if (m_BuildingPanelUI != null)
            {
                m_BuildingPanelUI.OnSlotSelected.RemoveListener(HandleSlotSelected);
            }

            ClearPreview();
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (m_UsePreviewPlacement && m_PreviewInstance != null)
            {
                UpdatePreviewTransform();
            }

            HandleDebugInput();
            HandleXRControllerInput();
        }

        public void BindPanel(BuildingPanelUI panel)
        {
            if (m_BuildingPanelUI == panel)
            {
                return;
            }

            if (isActiveAndEnabled && m_BuildingPanelUI != null)
            {
                m_BuildingPanelUI.OnSlotSelected.RemoveListener(HandleSlotSelected);
            }

            m_BuildingPanelUI = panel;

            if (isActiveAndEnabled && m_BuildingPanelUI != null)
            {
                m_BuildingPanelUI.OnSlotSelected.AddListener(HandleSlotSelected);
            }
        }

        public void BindPlacementService(CityBuildingPlacementService placementService)
        {
            m_BuildingPlacementService = placementService;
        }

        public void SetSelectedPrefab(GameObject prefab)
        {
            m_SelectedPrefab = prefab;
            m_OnSelectedPrefabChanged.Invoke(prefab);

            if (prefab == null)
            {
                ClearPreview();
            }
        }

        public bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = m_SelectedPrefab;
            return prefab != null;
        }

        public bool TryGetSelectedBuildingDefinition(out BuildingDefinitionSO definition)
        {
            if (m_BuildingCatalogBinder != null && m_BuildingCatalogBinder.TryGetSelectedDefinition(out definition))
            {
                return definition != null;
            }

            if (m_SelectedPrefab != null && m_SelectedPrefab.TryGetComponent(out BuildingDefinitionAuthoring authoring))
            {
                definition = authoring.Definition;
                return definition != null;
            }

            definition = null;
            return false;
        }

        public void BeginPreviewForSelected()
        {
            if (!m_UsePreviewPlacement)
            {
                return;
            }

            if (m_SelectedPrefab == null)
            {
                ClearPreview();
                return;
            }

            CreatePreviewInstance(m_SelectedPrefab);
        }

        public void BeginPreviewForPrefab(GameObject prefab)
        {
            SetSelectedPrefab(prefab);
            BeginPreviewForSelected();
        }

        public void ConfirmPlacement()
        {
            if (!m_UsePreviewPlacement)
            {
                if (m_SpawnOnSelection)
                {
                    SpawnSelectedAtDefaultPoint();
                }

                return;
            }

            if (m_SelectedPrefab == null || m_PreviewInstance == null || !m_PreviewHasValidCell)
            {
                return;
            }

            Vector3 spawnPosition = m_LastPreviewWorldPosition;
            Quaternion spawnRotation = m_LastPreviewWorldRotation;

            ClearPreview();
            SpawnPrefab(m_SelectedPrefab, spawnPosition, spawnRotation, m_DefaultSpawnParent);
        }

        public void CancelPlacement()
        {
            ClearPreview();
        }

        public void RotatePreviewClockwise()
        {
            RotatePreview(m_RotationStepDegrees);
        }

        public void RotatePreviewCounterClockwise()
        {
            RotatePreview(-m_RotationStepDegrees);
        }

        public void RotatePreview(float deltaYawDegrees)
        {
            if (m_ForceIdentityRotation || m_PreviewInstance == null)
            {
                return;
            }

            m_CurrentPreviewYaw += deltaYawDegrees;
            UpdatePreviewTransform();
        }

        public GameObject SpawnSelectedAtDefaultPoint()
        {
            if (m_SelectedPrefab == null)
            {
                return null;
            }

            if (m_DefaultSpawnPoint != null)
            {
                return SpawnPrefab(m_SelectedPrefab, m_DefaultSpawnPoint.position, ResolveRotation(m_DefaultSpawnPoint), m_DefaultSpawnParent);
            }

            return SpawnPrefab(m_SelectedPrefab, transform.position, ResolveRotation(transform), m_DefaultSpawnParent);
        }

        public GameObject SpawnSelectedAt(Vector3 position, Quaternion rotation)
        {
            return SpawnSelectedAt(position, rotation, m_DefaultSpawnParent);
        }

        public GameObject SpawnSelectedAt(Vector3 position, Quaternion rotation, Transform parent)
        {
            if (m_SelectedPrefab == null)
            {
                Debug.LogWarning("No selected building prefab to spawn.", this);
                return null;
            }

            return SpawnPrefab(m_SelectedPrefab, position, rotation, parent);
        }

        public GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return SpawnPrefab(prefab, position, rotation, m_DefaultSpawnParent);
        }

        public GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null)
            {
                Debug.LogWarning("Cannot spawn a null prefab.", this);
                return null;
            }

            ResolvePlacementService();
            BuildingDefinitionSO definition = ResolveBuildingDefinition(prefab);
            if (m_BuildingPlacementService != null &&
                !m_BuildingPlacementService.CanPlace(prefab, definition, position, true, out string placementError))
            {
                Debug.LogWarning(placementError, this);
                return null;
            }

            GameObject instance = parent != null
                ? Instantiate(prefab, position, rotation, parent)
                : Instantiate(prefab, position, rotation);

            if (m_UseSpawnPointScale && m_DefaultSpawnPoint != null)
            {
                instance.transform.localScale = m_DefaultSpawnPoint.localScale;
            }

            SetupGridPlacement(instance);

            if (m_BuildingPlacementService != null &&
                !m_BuildingPlacementService.FinalizePlacement(instance, definition, position, out string finalizeError))
            {
                Debug.LogWarning(finalizeError, this);
                DestroyGameObject(instance);
                return null;
            }

            m_OnPrefabSpawned.Invoke(instance);
            return instance;
        }

        void HandleSlotSelected(int slotIndex, GameObject prefab)
        {
            m_SelectedSlotIndex = slotIndex;
            SetSelectedPrefab(prefab);

            if (m_UsePreviewPlacement)
            {
                BeginPreviewForSelected();
                return;
            }

            if (m_SpawnOnSelection)
            {
                SpawnSelectedAtDefaultPoint();
            }
        }

        void ResolveReferences()
        {
            if (m_BuildingPanelUI == null)
            {
                m_BuildingPanelUI = GetComponent<BuildingPanelUI>();
            }

            if (m_BuildingCatalogBinder == null)
            {
                m_BuildingCatalogBinder = GetComponent<BuildingPanelCatalogBinder>();
            }

            ResolvePlacementService();
        }

        void ResolvePlacementService()
        {
            if (m_BuildingPlacementService == null)
            {
                m_BuildingPlacementService = FindFirstObjectByType<CityBuildingPlacementService>();
            }
        }

        BuildingDefinitionSO ResolveBuildingDefinition(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            if (m_BuildingCatalogBinder != null &&
                m_BuildingCatalogBinder.TryGetSelectedDefinition(out BuildingDefinitionSO selectedDefinition) &&
                selectedDefinition != null &&
                selectedDefinition.Prefab == prefab)
            {
                return selectedDefinition;
            }

            if (prefab.TryGetComponent(out BuildingDefinitionAuthoring authoring))
            {
                return authoring.Definition;
            }

            return null;
        }

        void CreatePreviewInstance(GameObject prefab)
        {
            ClearPreview();
            ResetXRInputEdgeState();

            Quaternion startRotation = ResolveRotation(m_DefaultSpawnPoint != null ? m_DefaultSpawnPoint : transform);
            m_CurrentPreviewYaw = startRotation.eulerAngles.y;

            m_PreviewInstance = m_DefaultSpawnParent != null
                ? Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, m_CurrentPreviewYaw, 0f), m_DefaultSpawnParent)
                : Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, m_CurrentPreviewYaw, 0f));

            PreparePreviewInstance(m_PreviewInstance);
            m_PreviewHalfHeight = ComputeHalfHeight(m_PreviewInstance);
            m_LastPreviewValidity = true;

            UpdatePreviewTransform();
        }

        void UpdatePreviewTransform()
        {
            if (m_PreviewInstance == null)
            {
                return;
            }

            Ray placementRay = BuildPlacementRay();
            if (!TryGetPlacementPoint(placementRay, out Vector3 placementPoint))
            {
                m_PreviewHasValidCell = false;

                if (m_HidePreviewWhenNoHit)
                {
                    m_PreviewInstance.SetActive(false);
                }

                return;
            }

            if (!m_PreviewInstance.activeSelf)
            {
                m_PreviewInstance.SetActive(true);
            }

            GridDefinition grid = ResolveGridDefinition();
            if (grid != null)
            {
                placementPoint = grid.Snap(placementPoint);
            }

            placementPoint.y += m_PreviewHalfHeight + m_PreviewLift;

            Quaternion rotation = ResolvePreviewRotation();
            m_PreviewInstance.transform.SetPositionAndRotation(placementPoint, rotation);

            m_LastPreviewWorldPosition = placementPoint;
            m_LastPreviewWorldRotation = rotation;
            m_PreviewHasValidCell = CanPlaceAtPreviewPosition(placementPoint);

            if (m_LastPreviewValidity != m_PreviewHasValidCell)
            {
                ApplyPreviewVisuals(m_PreviewInstance, m_PreviewHasValidCell ? m_PreviewTint : m_InvalidPreviewTint);
                m_LastPreviewValidity = m_PreviewHasValidCell;
            }
        }

        Quaternion ResolvePreviewRotation()
        {
            if (m_ForceIdentityRotation)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(0f, m_CurrentPreviewYaw, 0f);
        }

        bool CanPlaceAtPreviewPosition(Vector3 worldPosition)
        {
            if (m_SelectedPrefab == null)
            {
                return false;
            }

            ResolvePlacementService();
            if (m_BuildingPlacementService == null)
            {
                return true;
            }

            BuildingDefinitionSO definition = ResolveBuildingDefinition(m_SelectedPrefab);
            return m_BuildingPlacementService.CanPlace(m_SelectedPrefab, definition, worldPosition, false, out _);
        }

        Ray BuildPlacementRay()
        {
            if (m_PreviewFollowOrigin != null)
            {
                return new Ray(m_PreviewFollowOrigin.position, m_PreviewFollowOrigin.forward);
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 center = new(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                return mainCamera.ScreenPointToRay(center);
            }

            Transform source = m_DefaultSpawnPoint != null ? m_DefaultSpawnPoint : transform;
            return new Ray(source.position, source.forward);
        }

        void TryAutoAssignPreviewFollowOrigin()
        {
            if (!m_UsePreviewPlacement || m_PreviewFollowOrigin != null)
            {
                return;
            }

            XRRayInteractor[] rayInteractors = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rayInteractors == null || rayInteractors.Length == 0)
            {
                return;
            }

            string handHint = m_ControllerNodeForPlacement == XRNode.LeftHand ? "left" : "right";
            for (int i = 0; i < rayInteractors.Length; i++)
            {
                XRRayInteractor interactor = rayInteractors[i];
                if (interactor != null &&
                    interactor.name.IndexOf(handHint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    m_PreviewFollowOrigin = interactor.transform;
                    return;
                }
            }

            m_PreviewFollowOrigin = rayInteractors[0].transform;
        }

        bool TryGetPlacementPoint(Ray ray, out Vector3 point)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, m_PreviewRayDistance, m_PreviewRaycastMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            GridDefinition grid = ResolveGridDefinition();
            if (grid != null)
            {
                Plane gridPlane = new(Vector3.up, grid.Origin);
                if (gridPlane.Raycast(ray, out float enterDistance))
                {
                    point = ray.GetPoint(enterDistance);
                    return true;
                }
            }

            point = Vector3.zero;
            return false;
        }

        void PreparePreviewInstance(GameObject preview)
        {
            if (preview == null)
            {
                return;
            }

            if (m_DisableScriptsOnPreview)
            {
                MonoBehaviour[] behaviours = preview.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] != null)
                    {
                        behaviours[i].enabled = false;
                    }
                }
            }

            Rigidbody[] rigidbodies = preview.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] == null)
                {
                    continue;
                }

                rigidbodies[i].useGravity = false;
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
                rigidbodies[i].linearVelocity = Vector3.zero;
                rigidbodies[i].angularVelocity = Vector3.zero;
            }

            Collider[] colliders = preview.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                SetLayerRecursively(preview.transform, ignoreRaycastLayer);
            }

            ApplyPreviewVisuals(preview, m_PreviewTint);
        }

        void ApplyPreviewVisuals(GameObject preview, Color tint)
        {
            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Material[] materials = renderer.materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                    {
                        continue;
                    }

                    MakeMaterialTransparent(material);
                    TintMaterial(material, tint);
                }
            }
        }

        void MakeMaterialTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.renderQueue < 3000)
            {
                material.renderQueue = 3000;
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        void TintMaterial(Material material, Color tint)
        {
            if (material.HasProperty("_BaseColor"))
            {
                Color baseColor = material.GetColor("_BaseColor");
                material.SetColor("_BaseColor", Color.Lerp(baseColor, tint, 0.7f));
            }

            if (material.HasProperty("_Color"))
            {
                Color color = material.GetColor("_Color");
                material.SetColor("_Color", Color.Lerp(color, tint, 0.7f));
            }
        }

        float ComputeHalfHeight(GameObject target)
        {
            if (target == null)
            {
                return 0f;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!hasBounds)
            {
                Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] == null)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        combinedBounds = colliders[i].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(colliders[i].bounds);
                    }
                }
            }

            return hasBounds ? Mathf.Max(0f, combinedBounds.extents.y) : 0f;
        }

        void ClearPreview()
        {
            if (m_PreviewInstance != null)
            {
                DestroyGameObject(m_PreviewInstance);
                m_PreviewInstance = null;
            }

            m_PreviewHasValidCell = false;
            m_PreviewHalfHeight = 0f;
            m_LastPreviewValidity = true;
            ResetXRInputEdgeState();
        }

        void SetLayerRecursively(Transform root, int layer)
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

        void SetupGridPlacement(GameObject instance)
        {
            if (!m_SetupGridPlacementOnSpawn || instance == null)
            {
                return;
            }

            GridDefinition grid = ResolveGridDefinition();
            if (grid == null)
            {
                return;
            }

            Rigidbody body = instance.GetComponent<Rigidbody>();
            bool addedBody = false;
            if (body == null && m_AddRigidbodyIfMissing)
            {
                body = instance.AddComponent<Rigidbody>();
                addedBody = true;
            }

            if (body != null && addedBody)
            {
                body.mass = Mathf.Max(0.01f, m_SpawnedBodyMass);
                body.useGravity = m_EnableGravityOnSpawn;
                body.isKinematic = false;

                if (m_ConfigurePhysicsLikeTestCube)
                {
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    body.constraints =
                        RigidbodyConstraints.FreezePositionY |
                        RigidbodyConstraints.FreezeRotationX |
                        RigidbodyConstraints.FreezeRotationZ;
                }
            }

            XRGrabInteractable grab = instance.GetComponent<XRGrabInteractable>();
            bool addedGrab = false;
            if (grab == null && m_AddGrabInteractableIfMissing)
            {
                grab = instance.AddComponent<XRGrabInteractable>();
                addedGrab = true;
            }

            if (grab != null)
            {
                grab.colliders.Clear();
                Collider[] allColliders = instance.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < allColliders.Length; i++)
                {
                    Collider collider = allColliders[i];
                    if (collider == null || collider.GetComponentInParent<XRSimpleInteractable>() != null)
                    {
                        continue;
                    }

                    grab.colliders.Add(collider);
                }
            }

            if (grab != null && addedGrab && m_ConfigureGrabLikeTestCube)
            {
                grab.useDynamicAttach = false;
                grab.matchAttachPosition = true;
                grab.matchAttachRotation = true;
                grab.snapToColliderVolume = true;
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.trackPosition = true;
                grab.trackRotation = true;
                grab.trackScale = true;
                grab.throwOnDetach = false;
                grab.forceGravityOnDetach = false;
            }

            GridMovementConstraint gridConstraint = instance.GetComponent<GridMovementConstraint>();
            if (gridConstraint == null && m_AddGridMovementConstraintIfMissing)
            {
                gridConstraint = instance.AddComponent<GridMovementConstraint>();
            }

            if (instance.GetComponent<HorizontalGrabConstraint>() == null && m_AddHorizontalGrabConstraintIfMissing)
            {
                instance.AddComponent<HorizontalGrabConstraint>();
            }

            if (gridConstraint != null)
            {
                gridConstraint.SetGrid(grid);
            }

            ScaleHandle[] scaleHandles = instance.GetComponentsInChildren<ScaleHandle>(true);
            for (int i = 0; i < scaleHandles.Length; i++)
            {
                if (scaleHandles[i] != null)
                {
                    scaleHandles[i].SetGrid(grid);
                }
            }

            XRSimpleInteractable[] handleInteractables = instance.GetComponentsInChildren<XRSimpleInteractable>(true);
            for (int i = 0; i < handleInteractables.Length; i++)
            {
                XRSimpleInteractable handle = handleInteractables[i];
                if (handle == null)
                {
                    continue;
                }

                handle.colliders.Clear();
                Collider[] ownColliders = handle.GetComponents<Collider>();
                for (int j = 0; j < ownColliders.Length; j++)
                {
                    if (ownColliders[j] != null)
                    {
                        handle.colliders.Add(ownColliders[j]);
                    }
                }
            }
        }

        void HandleDebugInput()
        {
            if (!m_UseDebugKeyboardShortcuts)
            {
                return;
            }

            if (m_UsePreviewPlacement)
            {
                if (IsDebugConfirmPressed())
                {
                    ConfirmPlacement();
                }

                if (IsDebugKeyPressed(m_CancelKey))
                {
                    CancelPlacement();
                }

                if (IsDebugKeyPressed(m_RotateLeftKey))
                {
                    RotatePreviewCounterClockwise();
                }

                if (IsDebugKeyPressed(m_RotateRightKey))
                {
                    RotatePreviewClockwise();
                }
            }
            else if (IsDebugConfirmPressed())
            {
                SpawnSelectedAtDefaultPoint();
            }
        }

        void HandleXRControllerInput()
        {
            if (!m_UseXRControllerButtons || !m_UsePreviewPlacement || m_PreviewInstance == null)
            {
                return;
            }

            if (!TryGetPlacementInputDevice(out InputDevice device))
            {
                ResetXRInputEdgeState();
                return;
            }

            bool primaryButtonPressed = GetConfirmButtonPressed(device);
            bool secondaryButtonPressed = GetCancelButtonPressed(device);

            if (primaryButtonPressed && !m_LastPrimaryButtonPressed)
            {
                ConfirmPlacement();
            }

            if (secondaryButtonPressed && !m_LastSecondaryButtonPressed)
            {
                CancelPlacement();
            }

            m_LastPrimaryButtonPressed = primaryButtonPressed;
            m_LastSecondaryButtonPressed = secondaryButtonPressed;

            if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                m_StickRotateReady = true;
                return;
            }

            if (Mathf.Abs(axis.x) < m_StickRotateDeadzone * 0.5f)
            {
                m_StickRotateReady = true;
                return;
            }

            if (!m_StickRotateReady)
            {
                return;
            }

            if (axis.x >= m_StickRotateDeadzone)
            {
                RotatePreviewClockwise();
                m_StickRotateReady = false;
            }
            else if (axis.x <= -m_StickRotateDeadzone)
            {
                RotatePreviewCounterClockwise();
                m_StickRotateReady = false;
            }
        }

        bool TryGetPlacementInputDevice(out InputDevice device)
        {
            device = InputDevices.GetDeviceAtXRNode(m_ControllerNodeForPlacement);
            if (device.isValid)
            {
                return true;
            }

            XRNode fallbackNode = m_ControllerNodeForPlacement == XRNode.LeftHand ? XRNode.RightHand : XRNode.LeftHand;
            device = InputDevices.GetDeviceAtXRNode(fallbackNode);
            return device.isValid;
        }

        bool GetConfirmButtonPressed(InputDevice device)
        {
            if (GetBoolFeature(device, CommonUsages.primaryButton) || GetBoolFeature(device, CommonUsages.triggerButton))
            {
                return true;
            }

            float threshold = Mathf.Clamp01(m_TriggerPressThreshold);
            return
                (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue >= threshold) ||
                (device.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue >= threshold);
        }

        bool GetCancelButtonPressed(InputDevice device)
        {
            return GetBoolFeature(device, CommonUsages.secondaryButton) || GetBoolFeature(device, CommonUsages.menuButton);
        }

        static bool GetBoolFeature(InputDevice device, InputFeatureUsage<bool> featureUsage)
        {
            return device.TryGetFeatureValue(featureUsage, out bool value) && value;
        }

        bool IsDebugConfirmPressed()
        {
            if (IsDebugKeyPressed(m_ConfirmKey))
            {
                return true;
            }

            if (!m_AllowMouseClickToConfirm)
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButtonDown(0))
            {
                return false;
            }
#else
            return false;
#endif

            EventSystem eventSystem = EventSystem.current;
            return eventSystem == null || !eventSystem.IsPointerOverGameObject();
        }

        bool IsDebugKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return key switch
            {
                KeyCode.Return => keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame,
                KeyCode.Escape => keyboard.escapeKey.wasPressedThisFrame,
                KeyCode.Q => keyboard.qKey.wasPressedThisFrame,
                KeyCode.E => keyboard.eKey.wasPressedThisFrame,
                KeyCode.Space => keyboard.spaceKey.wasPressedThisFrame,
                _ => false,
            };
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        void ResetXRInputEdgeState()
        {
            m_LastPrimaryButtonPressed = false;
            m_LastSecondaryButtonPressed = false;
            m_StickRotateReady = true;
        }

        GridDefinition ResolveGridDefinition()
        {
            if (m_GridDefinition == null)
            {
                m_GridDefinition = FindFirstObjectByType<GridDefinition>();
            }

            return m_GridDefinition;
        }

        Quaternion ResolveRotation(Transform source)
        {
            if (m_ForceIdentityRotation || source == null)
            {
                return Quaternion.identity;
            }

            return m_UseSpawnPointRotation ? source.rotation : Quaternion.identity;
        }

        void DestroyGameObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
