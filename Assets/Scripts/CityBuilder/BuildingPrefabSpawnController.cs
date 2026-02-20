using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
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
        [SerializeField] Color m_PreviewTint = new Color(0.25f, 0.75f, 1f, 0.45f);

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
        [SerializeField] PrefabChangedEvent m_OnSelectedPrefabChanged = new PrefabChangedEvent();
        [SerializeField] PrefabSpawnedEvent m_OnPrefabSpawned = new PrefabSpawnedEvent();

        int m_SelectedSlotIndex = -1;
        GameObject m_SelectedPrefab;

        GameObject m_PreviewInstance;
        bool m_PreviewHasValidCell;
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
            if (m_BuildingPanelUI == null)
            {
                m_BuildingPanelUI = GetComponent<BuildingPanelUI>();
            }
        }

        void Awake()
        {
            if (m_BuildingPanelUI == null)
            {
                m_BuildingPanelUI = GetComponent<BuildingPanelUI>();
            }
        }

        void Start()
        {
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

            if (!m_UseDebugKeyboardShortcuts || !m_UsePreviewPlacement)
            {
                HandleXRControllerInput();
                return;
            }

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

            HandleXRControllerInput();
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

        public void SetSelectedPrefab(GameObject prefab)
        {
            m_SelectedPrefab = prefab;
            m_OnSelectedPrefabChanged.Invoke(prefab);
        }

        public bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = m_SelectedPrefab;
            return prefab != null;
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
            if (m_ForceIdentityRotation)
            {
                return;
            }

            if (m_PreviewInstance == null)
            {
                return;
            }

            m_CurrentPreviewYaw += deltaYawDegrees;
            UpdatePreviewTransform();
        }

        public GameObject SpawnSelectedAtDefaultPoint()
        {
            if (m_DefaultSpawnPoint != null)
            {
                return SpawnSelectedAt(m_DefaultSpawnPoint.position, ResolveRotation(m_DefaultSpawnPoint), m_DefaultSpawnParent);
            }

            return SpawnSelectedAt(transform.position, transform.rotation, m_DefaultSpawnParent);
        }

        public GameObject SpawnSelectedAt(Transform spawnPoint)
        {
            if (spawnPoint == null)
            {
                return SpawnSelectedAtDefaultPoint();
            }

            return SpawnSelectedAt(spawnPoint.position, ResolveRotation(spawnPoint), m_DefaultSpawnParent);
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

            parent = null;

            GameObject instance = parent != null
                ? Instantiate(prefab, position, rotation, parent)
                : Instantiate(prefab, position, rotation, null);

            if (m_UseSpawnPointScale && m_DefaultSpawnPoint != null)
            {
                instance.transform.localScale = m_DefaultSpawnPoint.localScale;
            }

            SetupGridPlacement(instance);

            m_OnPrefabSpawned.Invoke(instance);
            return instance;
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
                if (m_HidePreviewWhenNoHit && m_PreviewInstance.activeSelf)
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

            Quaternion rotation = Quaternion.Euler(0f, m_CurrentPreviewYaw, 0f);
            if (m_ForceIdentityRotation)
            {
                rotation = Quaternion.identity;
            }
            m_PreviewInstance.transform.SetPositionAndRotation(placementPoint, rotation);

            m_LastPreviewWorldPosition = placementPoint;
            m_LastPreviewWorldRotation = rotation;
            m_PreviewHasValidCell = true;
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
                Vector3 center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                return mainCamera.ScreenPointToRay(center);
            }

            if (m_DefaultSpawnPoint != null)
            {
                return new Ray(m_DefaultSpawnPoint.position, m_DefaultSpawnPoint.forward);
            }

            return new Ray(transform.position, transform.forward);
        }

        void TryAutoAssignPreviewFollowOrigin()
        {
            if (!m_UsePreviewPlacement || m_PreviewFollowOrigin != null)
            {
                return;
            }

            XRRayInteractor[] rayInteractors = FindObjectsOfType<XRRayInteractor>(true);
            if (rayInteractors == null || rayInteractors.Length == 0)
            {
                return;
            }

            string handNameHint = m_ControllerNodeForPlacement == XRNode.LeftHand ? "left" : "right";
            for (int i = 0; i < rayInteractors.Length; i++)
            {
                XRRayInteractor interactor = rayInteractors[i];
                if (interactor == null)
                {
                    continue;
                }

                string interactorName = interactor.name;
                if (!string.IsNullOrEmpty(interactorName) &&
                    interactorName.IndexOf(handNameHint, StringComparison.OrdinalIgnoreCase) >= 0)
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
                Plane gridPlane = new Plane(Vector3.up, grid.Origin);
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
                Rigidbody body = rigidbodies[i];
                if (body == null)
                {
                    continue;
                }

                body.useGravity = false;
                body.isKinematic = true;
                body.detectCollisions = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Collider[] colliders = preview.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                SetLayerRecursively(preview.transform, ignoreRaycastLayer);
            }

            ApplyPreviewVisuals(preview);
        }

        void ApplyPreviewVisuals(GameObject preview)
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
                    TintMaterial(material, m_PreviewTint);
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
                Color color = material.GetColor("_BaseColor");
                material.SetColor("_BaseColor", Color.Lerp(color, tint, 0.65f));
            }

            if (material.HasProperty("_Color"))
            {
                Color color = material.GetColor("_Color");
                material.SetColor("_Color", Color.Lerp(color, tint, 0.65f));
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

            if (!hasBounds)
            {
                return 0f;
            }

            return Mathf.Max(0f, combinedBounds.extents.y);
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

            GridDefinition resolvedGrid = ResolveGridDefinition();
            if (resolvedGrid == null)
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

            if (body != null)
            {
                if (addedBody)
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
                // Prevent root grab from hijacking handle colliders on child objects.
                grab.colliders.Clear();
                Collider[] allColliders = instance.GetComponentsInChildren<Collider>(true);
                grab.colliders.Clear();

                for (int i = 0; i < allColliders.Length; i++)
                {
                    Collider col = allColliders[i];

                    if (col == null)
                        continue;

                    // Si el collider pertenece a un handle, lo ignoramos
                    if (col.GetComponentInParent<XRSimpleInteractable>() != null)
                        continue;

                    grab.colliders.Add(col);
                }
            }

            // Keep prefab-authored grab settings for objects that already have XRGrabInteractable.
            // This avoids changing behavior of prefab instances like PerfectCube.
            if (grab != null && m_ConfigureGrabLikeTestCube && addedGrab)
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
                gridConstraint.SetGrid(resolvedGrid);
            }

            ScaleHandle[] scaleHandles = instance.GetComponentsInChildren<ScaleHandle>(true);
            for (int i = 0; i < scaleHandles.Length; i++)
            {
                if (scaleHandles[i] != null)
                {
                    scaleHandles[i].SetGrid(resolvedGrid);
                }
            }

            XRSimpleInteractable[] simpleHandles = instance.GetComponentsInChildren<XRSimpleInteractable>(true);
            for (int i = 0; i < simpleHandles.Length; i++)
            {
                XRSimpleInteractable handleInteractable = simpleHandles[i];
                if (handleInteractable == null)
                {
                    continue;
                }

                // Ensure each handle targets only its own colliders.
                handleInteractable.colliders.Clear();
                Collider[] ownColliders = handleInteractable.GetComponents<Collider>();
                for (int j = 0; j < ownColliders.Length; j++)
                {
                    if (ownColliders[j] != null)
                    {
                        handleInteractable.colliders.Add(ownColliders[j]);
                    }
                }
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

            float stickX = axis.x;
            if (Mathf.Abs(stickX) < m_StickRotateDeadzone * 0.5f)
            {
                m_StickRotateReady = true;
                return;
            }

            if (!m_StickRotateReady)
            {
                return;
            }

            if (stickX >= m_StickRotateDeadzone)
            {
                RotatePreviewClockwise();
                m_StickRotateReady = false;
            }
            else if (stickX <= -m_StickRotateDeadzone)
            {
                RotatePreviewCounterClockwise();
                m_StickRotateReady = false;
            }
        }

        bool GetBoolFeature(InputDevice device, InputFeatureUsage<bool> featureUsage)
        {
            if (device.TryGetFeatureValue(featureUsage, out bool value))
            {
                return value;
            }

            return false;
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
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue >= threshold)
            {
                return true;
            }

            if (device.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue >= threshold)
            {
                return true;
            }

            return false;
        }

        bool GetCancelButtonPressed(InputDevice device)
        {
            return GetBoolFeature(device, CommonUsages.secondaryButton) || GetBoolFeature(device, CommonUsages.menuButton);
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

            return IsMouseLeftPressedOutsideUI();
        }

        bool IsMouseLeftPressedOutsideUI()
        {
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

            switch (key)
            {
                case KeyCode.Return:
                    return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                case KeyCode.Escape:
                    return keyboard.escapeKey.wasPressedThisFrame;
                case KeyCode.Q:
                    return keyboard.qKey.wasPressedThisFrame;
                case KeyCode.E:
                    return keyboard.eKey.wasPressedThisFrame;
                case KeyCode.Space:
                    return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Tab:
                    return keyboard.tabKey.wasPressedThisFrame;
                case KeyCode.LeftArrow:
                    return keyboard.leftArrowKey.wasPressedThisFrame;
                case KeyCode.RightArrow:
                    return keyboard.rightArrowKey.wasPressedThisFrame;
                case KeyCode.UpArrow:
                    return keyboard.upArrowKey.wasPressedThisFrame;
                case KeyCode.DownArrow:
                    return keyboard.downArrowKey.wasPressedThisFrame;
                default:
                    return false;
            }
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
            if (m_GridDefinition != null)
            {
                return m_GridDefinition;
            }

            m_GridDefinition = FindFirstObjectByType<GridDefinition>();
            if (m_GridDefinition == null)
            {
                Debug.LogWarning("No GridDefinition found in scene. Objects will spawn without grid snapping.", this);
            }

            return m_GridDefinition;
        }

        Quaternion ResolveRotation(Transform spawnPoint)
        {
            if (m_ForceIdentityRotation)
            {
                return Quaternion.identity;
            }

            if (spawnPoint == null)
            {
                return Quaternion.identity;
            }

            return m_UseSpawnPointRotation ? spawnPoint.rotation : Quaternion.identity;
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
                return;
            }

            DestroyImmediate(component);
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
                return;
            }

            DestroyImmediate(target);
        }
    }
}
