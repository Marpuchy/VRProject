using System;
using CityBuilder;
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
        [SerializeField] bool m_ForceIdentityRotation = false;

        [Header("Placement Mode")]
        [SerializeField] bool m_UsePreviewPlacement = true;
        [SerializeField] XRRayInteractor m_PreviewRayInteractor;
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
        [SerializeField] bool m_AddScaleHandlesIfMissing = true;
        [SerializeField, Min(0.01f)] float m_ScaleHandleVisualSize = 0.04f;
        [SerializeField, Min(0f)] float m_ScaleHandleOffset = 0.05f;
        [SerializeField] bool m_ConfigurePhysicsLikeTestCube = true;
        [SerializeField] bool m_ConfigureGrabLikeTestCube = true;
        [SerializeField] bool m_EnableGravityOnSpawn = true;
        [SerializeField, Min(0.01f)] float m_SpawnedBodyMass = 1f;

        [Header("Events")]
        [SerializeField] PrefabChangedEvent m_OnSelectedPrefabChanged = new();
        [SerializeField] PrefabSpawnedEvent m_OnPrefabSpawned = new();

        int m_SelectedSlotIndex = -1;
        GameObject m_SelectedPrefab;
        BuildingDefinitionSO m_SelectedDefinition;

        GameObject m_PreviewInstance;
        bool m_PreviewHasValidCell;
        bool m_LastPreviewValidity = true;
        float m_PreviewGroundOffset;
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
            ResolvePreviewPlacementSource();
            RetrofitPlacedBuildings();
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

            ResolvePreviewPlacementSource();

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

            if (prefab == null)
            {
                m_SelectedDefinition = null;
            }
            else if (m_SelectedDefinition == null || !m_SelectedDefinition.UsesPrefab(prefab))
            {
                m_SelectedDefinition = ResolveDefinitionFromAuthoring(prefab);
            }

            m_OnSelectedPrefabChanged.Invoke(prefab);

            if (prefab == null)
            {
                ClearPreview();
            }
        }

        public void SetSelectedDefinition(BuildingDefinitionSO definition)
        {
            m_SelectedDefinition = definition;
        }

        public bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = m_SelectedPrefab;
            return prefab != null;
        }

        public bool TryGetSelectedBuildingDefinition(out BuildingDefinitionSO definition)
        {
            if (m_SelectedDefinition != null)
            {
                definition = m_SelectedDefinition;
                return true;
            }

            if (m_BuildingCatalogBinder != null && m_BuildingCatalogBinder.TryGetSelectedDefinition(out definition))
            {
                return definition != null;
            }

            definition = ResolveDefinitionFromAuthoring(m_SelectedPrefab);
            return definition != null;
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

            m_CurrentPreviewYaw = NormalizeAngle360(SnapAngleToStep(m_CurrentPreviewYaw + deltaYawDegrees));
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
            Vector3 sanitizedPosition = SanitizeSpawnPosition(position);
            if (m_BuildingPlacementService != null &&
                !m_BuildingPlacementService.CanPlace(prefab, definition, sanitizedPosition, true, out string placementError))
            {
                Debug.LogWarning(placementError, this);
                return null;
            }

            GameObject instance = parent != null
                ? Instantiate(prefab, sanitizedPosition, rotation, parent)
                : Instantiate(prefab, sanitizedPosition, rotation);

            ApplyDefinitionToInstance(instance, definition);

            SetupGridPlacement(instance);

            if (m_BuildingPlacementService != null &&
                !m_BuildingPlacementService.FinalizePlacement(instance, definition, sanitizedPosition, out string finalizeError))
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
            m_SelectedDefinition = ResolveDefinitionForSlot(slotIndex, prefab);
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

            if (m_SelectedDefinition != null && m_SelectedDefinition.UsesPrefab(prefab))
            {
                return m_SelectedDefinition;
            }

            if (m_BuildingCatalogBinder != null &&
                m_BuildingCatalogBinder.TryGetSelectedDefinition(out BuildingDefinitionSO selectedDefinition) &&
                selectedDefinition != null &&
                selectedDefinition.UsesPrefab(prefab))
            {
                return selectedDefinition;
            }

            return ResolveDefinitionFromAuthoring(prefab);
        }

        BuildingDefinitionSO ResolveDefinitionForSlot(int slotIndex, GameObject prefab)
        {
            if (m_BuildingCatalogBinder != null &&
                m_BuildingCatalogBinder.TryGetDefinitionForSlot(slotIndex, out BuildingDefinitionSO definition) &&
                definition != null)
            {
                return definition;
            }

            return ResolveDefinitionFromAuthoring(prefab);
        }

        static BuildingDefinitionSO ResolveDefinitionFromAuthoring(GameObject prefab)
        {
            if (prefab != null && prefab.TryGetComponent(out BuildingDefinitionAuthoring authoring))
            {
                return authoring.Definition;
            }

            return null;
        }

        static void ApplyDefinitionToInstance(GameObject instance, BuildingDefinitionSO definition)
        {
            if (instance == null || definition == null)
            {
                return;
            }

            if (instance.TryGetComponent(out BuildingDefinitionPrefabBinder binder))
            {
                binder.ApplyDefinition(definition);
                return;
            }

            if (instance.TryGetComponent(out BuildingDefinitionAuthoring authoring))
            {
                authoring.SetDefinition(definition);
            }
        }

        void CreatePreviewInstance(GameObject prefab)
        {
            ClearPreview();
            ResetXRInputEdgeState();
            BuildingDefinitionSO definition = ResolveBuildingDefinition(prefab);

            Quaternion startRotation = ResolvePreviewStartRotation(m_DefaultSpawnPoint != null ? m_DefaultSpawnPoint : transform);
            m_CurrentPreviewYaw = 0f;

            m_PreviewInstance = m_DefaultSpawnParent != null
                ? Instantiate(prefab, Vector3.zero, startRotation, m_DefaultSpawnParent)
                : Instantiate(prefab, Vector3.zero, startRotation);

            ApplyDefinitionToInstance(m_PreviewInstance, definition);
            PreparePreviewInstance(m_PreviewInstance);
            m_PreviewGroundOffset = ComputeGroundOffset(m_PreviewInstance, definition);
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

            Vector3 spawnPosition = placementPoint;
            spawnPosition.y += m_PreviewGroundOffset;
            spawnPosition = SanitizeSpawnPosition(spawnPosition);

            Vector3 previewPosition = spawnPosition;
            previewPosition.y += m_PreviewLift;

            Quaternion rotation = ResolvePreviewRotation(spawnPosition);
            m_PreviewInstance.transform.SetPositionAndRotation(previewPosition, rotation);

            m_LastPreviewWorldPosition = spawnPosition;
            m_LastPreviewWorldRotation = rotation;
            m_PreviewHasValidCell = CanPlaceAtPreviewPosition(spawnPosition);

            if (m_LastPreviewValidity != m_PreviewHasValidCell)
            {
                ApplyPreviewVisuals(m_PreviewInstance, m_PreviewHasValidCell ? m_PreviewTint : m_InvalidPreviewTint);
                m_LastPreviewValidity = m_PreviewHasValidCell;
            }
        }

        Quaternion ResolvePreviewRotation(Vector3 previewWorldPosition)
        {
            if (m_ForceIdentityRotation)
            {
                return Quaternion.identity;
            }

            float facingYaw = ResolvePreviewFacingYaw(previewWorldPosition);
            return Quaternion.Euler(0f, SnapAngleToStep(facingYaw + m_CurrentPreviewYaw), 0f);
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

        Quaternion ResolvePreviewStartRotation(Transform source)
        {
            if (m_ForceIdentityRotation || source == null)
            {
                return Quaternion.identity;
            }

            return m_UseSpawnPointRotation
                ? Quaternion.Euler(0f, SnapAngleToStep(source.eulerAngles.y), 0f)
                : Quaternion.identity;
        }

        float ResolvePreviewFacingYaw(Vector3 previewWorldPosition)
        {
            Transform observer = ResolvePreviewObserver();
            if (observer == null)
            {
                Transform fallbackSource = m_DefaultSpawnPoint != null ? m_DefaultSpawnPoint : transform;
                return ResolvePreviewStartRotation(fallbackSource).eulerAngles.y;
            }

            Vector3 toObserver = observer.position - previewWorldPosition;
            toObserver.y = 0f;
            if (toObserver.sqrMagnitude <= 0.0001f)
            {
                Transform fallbackSource = m_DefaultSpawnPoint != null ? m_DefaultSpawnPoint : transform;
                return ResolvePreviewStartRotation(fallbackSource).eulerAngles.y;
            }

            return Quaternion.LookRotation(toObserver.normalized, Vector3.up).eulerAngles.y;
        }

        Transform ResolvePreviewObserver()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            if (m_PreviewFollowOrigin != null)
            {
                return m_PreviewFollowOrigin;
            }

            if (m_DefaultSpawnPoint != null)
            {
                return m_DefaultSpawnPoint;
            }

            return transform;
        }

        Ray BuildPlacementRay()
        {
            ResolvePreviewPlacementSource();
            if (IsUsablePreviewRayInteractor(m_PreviewRayInteractor))
            {
                m_PreviewRayInteractor.GetLineOriginAndDirection(out Vector3 origin, out Vector3 direction);
                if (direction.sqrMagnitude > 0.000001f)
                {
                    return new Ray(origin, direction.normalized);
                }
            }

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

        void ResolvePreviewPlacementSource()
        {
            if (!m_UsePreviewPlacement)
            {
                return;
            }

            if (m_PreviewRayInteractor == null && m_PreviewFollowOrigin != null)
            {
                m_PreviewRayInteractor = m_PreviewFollowOrigin.GetComponentInParent<XRRayInteractor>();
            }

            if (IsUsablePreviewRayInteractor(m_PreviewRayInteractor))
            {
                Transform rayOrigin = ResolvePreviewRayOrigin(m_PreviewRayInteractor);
                if (rayOrigin != null)
                {
                    m_PreviewFollowOrigin = rayOrigin;
                }

                return;
            }

            XRRayInteractor bestInteractor = FindBestPreviewRayInteractor();
            if (bestInteractor == null)
            {
                return;
            }

            m_PreviewRayInteractor = bestInteractor;
            Transform bestRayOrigin = ResolvePreviewRayOrigin(bestInteractor);
            if (bestRayOrigin != null)
            {
                m_PreviewFollowOrigin = bestRayOrigin;
            }
        }

        bool TryGetPlacementPoint(Ray ray, out Vector3 point)
        {
            ResolvePreviewPlacementSource();
            if (IsUsablePreviewRayInteractor(m_PreviewRayInteractor) &&
                m_PreviewRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit interactorHit))
            {
                point = interactorHit.point;
                return true;
            }

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

        XRRayInteractor FindBestPreviewRayInteractor()
        {
            XRRayInteractor[] rayInteractors = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rayInteractors == null || rayInteractors.Length == 0)
            {
                return null;
            }

            XRRayInteractor bestInteractor = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < rayInteractors.Length; i++)
            {
                XRRayInteractor interactor = rayInteractors[i];
                if (!IsUsablePreviewRayInteractor(interactor))
                {
                    continue;
                }

                int score = ScorePreviewRayInteractor(interactor);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestInteractor = interactor;
            }

            return bestInteractor;
        }

        int ScorePreviewRayInteractor(XRRayInteractor interactor)
        {
            if (interactor == null)
            {
                return int.MinValue;
            }

            int score = 0;
            if (interactor.isActiveAndEnabled)
            {
                score += 1000;
            }

            if (interactor.gameObject.activeInHierarchy)
            {
                score += 400;
            }

            if (InteractorMatchesDesiredHand(interactor.transform))
            {
                score += 300;
            }

            if (InteractorMatchesOppositeHand(interactor.transform))
            {
                score -= 200;
            }

            if (TransformOrAncestorsContain(interactor.transform, "controller"))
            {
                score += 80;
            }
            else if (TransformOrAncestorsContain(interactor.transform, "hand"))
            {
                score += 40;
            }

            if (interactor.rayOriginTransform != null)
            {
                score += 50;
            }

            return score;
        }

        bool InteractorMatchesDesiredHand(Transform source)
        {
            return m_ControllerNodeForPlacement == XRNode.LeftHand
                ? TransformOrAncestorsContainAny(source, "left controller", "left hand", "lefthand", "left")
                : TransformOrAncestorsContainAny(source, "right controller", "right hand", "righthand", "right");
        }

        bool InteractorMatchesOppositeHand(Transform source)
        {
            return m_ControllerNodeForPlacement == XRNode.LeftHand
                ? TransformOrAncestorsContainAny(source, "right controller", "right hand", "righthand", "right")
                : TransformOrAncestorsContainAny(source, "left controller", "left hand", "lefthand", "left");
        }

        static bool TransformOrAncestorsContainAny(Transform source, params string[] keywords)
        {
            if (keywords == null)
            {
                return false;
            }

            for (int i = 0; i < keywords.Length; i++)
            {
                if (TransformOrAncestorsContain(source, keywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TransformOrAncestorsContain(Transform source, string keyword)
        {
            if (source == null || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            for (Transform current = source; current != null; current = current.parent)
            {
                if (current.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsUsablePreviewRayInteractor(XRRayInteractor interactor)
        {
            return interactor != null && interactor.enabled && interactor.gameObject.activeInHierarchy;
        }

        static Transform ResolvePreviewRayOrigin(XRRayInteractor interactor)
        {
            if (interactor == null)
            {
                return null;
            }

            return interactor.rayOriginTransform != null
                ? interactor.rayOriginTransform
                : interactor.transform;
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

        float ComputeGroundOffset(GameObject target, BuildingDefinitionSO definition = null)
        {
            if (target == null)
            {
                return 0f;
            }

            float groundOffset = TryGetPlacementBounds(target, out Bounds combinedBounds)
                ? target.transform.position.y - combinedBounds.min.y
                : 0f;

            if (definition != null)
            {
                groundOffset += definition.ModelVerticalOffset;
            }

            return groundOffset;
        }

        static bool TryGetPlacementBounds(GameObject target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (ShouldIgnoreBoundsRenderer(renderer))
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

            if (hasBounds)
            {
                return true;
            }

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.GetComponentInParent<ScaleHandle>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        static bool ShouldIgnoreBoundsRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return true;
            }

            if (renderer is LineRenderer)
            {
                return true;
            }

            return renderer.GetComponentInParent<ScaleHandle>() != null;
        }

        void ClearPreview()
        {
            if (m_PreviewInstance != null)
            {
                DestroyGameObject(m_PreviewInstance);
                m_PreviewInstance = null;
            }

            m_PreviewHasValidCell = false;
            m_PreviewGroundOffset = 0f;
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

            EnsureScaleHandles(instance);
            RemovePlacementInteractionComponents(instance);
            ConfigureStructureBodiesAsStatic(instance);
        }

        void EnsureScaleHandles(GameObject instance)
        {
            if (!m_AddScaleHandlesIfMissing || instance == null)
            {
                return;
            }

            GridDefinition grid = ResolveGridDefinition();
            RuntimeScaleHandleFactory.EnsureHandles(
                instance,
                grid,
                m_ScaleHandleVisualSize,
                m_ScaleHandleOffset);
        }

        void RetrofitPlacedBuildings()
        {
            PlacedBuildingRuntime[] placedBuildings = FindObjectsByType<PlacedBuildingRuntime>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < placedBuildings.Length; i++)
            {
                PlacedBuildingRuntime placedBuilding = placedBuildings[i];
                if (placedBuilding == null)
                {
                    continue;
                }

                GameObject instance = placedBuilding.gameObject;
                EnsureScaleHandles(instance);
                RemovePlacementInteractionComponents(instance);
                ConfigureStructureBodiesAsStatic(instance);
            }
        }

        void RemovePlacementInteractionComponents(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            DestroyComponents(instance.GetComponentsInChildren<GridMovementConstraint>(true));
            DestroyComponents(instance.GetComponentsInChildren<HorizontalGrabConstraint>(true));
            DestroyComponents(instance.GetComponentsInChildren<XRGrabInteractable>(true));
        }

        void ConfigureStructureBodiesAsStatic(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                {
                    continue;
                }

                body.useGravity = false;
                body.isKinematic = true;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.constraints = RigidbodyConstraints.FreezeAll;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        void DestroyComponents<T>(T[] components) where T : Component
        {
            if (components == null)
            {
                return;
            }

            for (int i = 0; i < components.Length; i++)
            {
                DestroyComponent(components[i]);
            }
        }

        void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
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

        float SnapAngleToStep(float angleDegrees)
        {
            float step = Mathf.Max(1f, m_RotationStepDegrees);
            return Mathf.Round(angleDegrees / step) * step;
        }

        static float NormalizeAngle360(float angleDegrees)
        {
            return Mathf.Repeat(angleDegrees, 360f);
        }

        Vector3 SanitizeSpawnPosition(Vector3 worldPosition)
        {
            GridDefinition grid = ResolveGridDefinition();
            MapBoundary boundary = ResolveMapBoundary();
            if (grid == null && boundary == null)
            {
                return worldPosition;
            }

            Vector3 sanitizedPosition = worldPosition;
            if (boundary != null)
            {
                sanitizedPosition = boundary.ClampWorldPosition(sanitizedPosition);
            }

            if (grid == null)
            {
                return sanitizedPosition;
            }

            return SnapInsideBoundary(sanitizedPosition, grid, boundary);
        }

        static Vector3 SnapInsideBoundary(Vector3 worldPosition, GridDefinition grid, MapBoundary boundary)
        {
            Vector3 snappedPosition = grid.Snap(worldPosition);
            if (boundary == null || boundary.ContainsWorldPosition(snappedPosition))
            {
                return snappedPosition;
            }

            Vector3 clampedPosition = boundary.ClampWorldPosition(worldPosition);
            snappedPosition = grid.Snap(clampedPosition);
            if (boundary.ContainsWorldPosition(snappedPosition))
            {
                return snappedPosition;
            }

            float cellSize = Mathf.Max(0.01f, grid.CellSize);
            Vector3 bestCandidate = clampedPosition;
            float bestDistance = float.MaxValue;

            for (int xOffset = -3; xOffset <= 3; xOffset++)
            {
                for (int zOffset = -3; zOffset <= 3; zOffset++)
                {
                    Vector3 candidate = snappedPosition + new Vector3(xOffset * cellSize, 0f, zOffset * cellSize);
                    candidate.y = clampedPosition.y;
                    if (!boundary.ContainsWorldPosition(candidate))
                    {
                        continue;
                    }

                    float distance = (candidate - clampedPosition).sqrMagnitude;
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        static MapBoundary ResolveMapBoundary()
        {
            return MapBoundary.TryGetActiveBoundary(out MapBoundary boundary) ? boundary : null;
        }

        Quaternion ResolveRotation(Transform source)
        {
            if (m_ForceIdentityRotation || source == null)
            {
                return Quaternion.identity;
            }

            return m_UseSpawnPointRotation
                ? Quaternion.Euler(0f, SnapAngleToStep(source.eulerAngles.y), 0f)
                : Quaternion.identity;
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
