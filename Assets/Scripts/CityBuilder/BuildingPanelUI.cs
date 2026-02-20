using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityBuilderVR
{
    public class BuildingPanelUI : MonoBehaviour
    {
        [Serializable]
        public struct BuildingSlotData
        {
            public string slotName;
            public GameObject buildingPrefab;
            public Sprite icon;
        }

        [Serializable]
        public class BuildingSlotSelectedEvent : UnityEvent<int, GameObject>
        {
        }

        [Serializable]
        public class BuildingPrefabSelectedEvent : UnityEvent<GameObject>
        {
        }

        public enum ThemeVariant
        {
            Custom = 0,
            AppleLight = 1,
            AppleDark = 2
        }

        [Header("Buildings")]
        [SerializeField] List<BuildingSlotData> m_BuildingSlots = new List<BuildingSlotData>();
        [SerializeField, Min(1)] int m_EmptySlotCount = 6;
        [SerializeField] bool m_DisableButtonsWithoutPrefab = true;

        [Header("Panel UI")]
        [SerializeField] Canvas m_TargetCanvas;
        [SerializeField] string m_Title = "Buildings";
        [SerializeField] Vector2 m_PanelSize = new Vector2(920f, 220f);
        [SerializeField] Vector2 m_PanelAnchorOffset = new Vector2(0f, 24f);
        [SerializeField, Min(1)] int m_Columns = 4; // Legacy serialized value, no longer used.
        [SerializeField] Vector2 m_SlotSize = new Vector2(104f, 104f);
        [SerializeField, Range(0f, 120f)] float m_SlotSpacing = 28f;
        [SerializeField] float m_FallbackCanvasDistance = 1.7f;
        [SerializeField] float m_FallbackCanvasScale = 0.0016f;
        [SerializeField] bool m_BuildPanelOnStart = true;

        [Header("World Space Follow")]
        [SerializeField] bool m_FollowPlayerInWorldSpace = true;
        [SerializeField] Transform m_FollowTargetOverride;
        [SerializeField] bool m_UseCurrentOffsetAsFollowOffset = true;
        [SerializeField] Vector3 m_FollowLocalPositionOffset = new Vector3(0f, -0.12f, 1.7f);
        [SerializeField] Vector3 m_FollowLocalEulerOffset = Vector3.zero;

        [Header("Manual Layout (Optional)")]
        [SerializeField] RectTransform m_PanelRootOverride;
        [SerializeField] RectTransform m_SlotsRootOverride;
        [SerializeField] BuildingSlotVisualRefs m_SlotTemplate;
        [SerializeField] bool m_HideTemplateOnRuntime = true;

        [Header("Default Theme")]
        [SerializeField] ThemeVariant m_ThemeVariant = ThemeVariant.AppleLight;
        [SerializeField] Color m_PanelColor = new Color(0.96f, 0.97f, 0.98f, 0.72f);
        [SerializeField] Color m_HeaderColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] Color m_SlotColor = new Color(1f, 1f, 1f, 0.62f);
        [SerializeField] Color m_SlotDisabledColor = new Color(0.86f, 0.87f, 0.9f, 0.42f);
        [SerializeField] Color m_TextColor = new Color(0.07f, 0.07f, 0.09f, 0.96f);
        [SerializeField] Color m_IconPlaceholderColor = new Color(0.9f, 0.91f, 0.94f, 0.95f);
        [SerializeField] Color m_IconPlaceholderDisabledColor = new Color(0.8f, 0.82f, 0.86f, 0.78f);
        [SerializeField] bool m_UseRoundedStyle = true;
        [SerializeField, Min(4)] int m_PanelCornerRadius = 22;
        [SerializeField, Min(4)] int m_SlotCornerRadius = 16;
        [SerializeField] Color m_SlotBorderColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField, Min(0.5f)] float m_SlotBorderThickness = 1.8f;
        [SerializeField] Color m_PanelShadowColor = new Color(0f, 0f, 0f, 0.22f);

        [Header("Events")]
        [SerializeField] BuildingSlotSelectedEvent m_OnSlotSelected = new BuildingSlotSelectedEvent();
        [SerializeField] BuildingPrefabSelectedEvent m_OnPrefabSelected = new BuildingPrefabSelectedEvent();

        RectTransform m_PanelTransform;
        RectTransform m_SlotsRoot;
        ScrollRect m_SlotScrollRect;
        readonly List<Button> m_RuntimeButtons = new List<Button>();
        readonly List<GameObject> m_RuntimeSlotObjects = new List<GameObject>();
        Sprite m_PanelRoundedSprite;
        Sprite m_SlotRoundedSprite;
        Sprite m_IconRoundedSprite;
        int m_SelectedSlotIndex = -1;
        GameObject m_SelectedPrefab;
        bool m_FollowOffsetInitialized;
        Transform m_LastFollowTarget;
        Vector3 m_RuntimeFollowLocalPositionOffset;
        Quaternion m_RuntimeFollowLocalRotationOffset;
#if UNITY_EDITOR
        readonly Dictionary<int, Sprite> m_EditorPrefabIconCache = new Dictionary<int, Sprite>();
#endif

        public int SelectedSlotIndex => m_SelectedSlotIndex;
        public GameObject SelectedPrefab => m_SelectedPrefab;
        public bool HasSelectedPrefab => m_SelectedPrefab != null;
        public BuildingSlotSelectedEvent OnSlotSelected => m_OnSlotSelected;
        public BuildingPrefabSelectedEvent OnPrefabSelected => m_OnPrefabSelected;

        void Start()
        {
            if (m_BuildPanelOnStart)
            {
                BuildPanel();
            }
        }

        void LateUpdate()
        {
            FollowCanvasToPlayer();
        }

        [ContextMenu("Build/Rebuild Panel")]
        public void BuildPanel()
        {
            ApplyThemePreset();
            m_FollowOffsetInitialized = false;
            m_LastFollowTarget = null;
            EnsureCanvas();
            EnsurePanel();
            RebuildSlots();
        }

        [ContextMenu("Theme/Use Apple Light")]
        void SetAppleLightTheme()
        {
            m_ThemeVariant = ThemeVariant.AppleLight;
            ApplyThemePreset();
            BuildPanel();
        }

        [ContextMenu("Theme/Use Apple Dark")]
        void SetAppleDarkTheme()
        {
            m_ThemeVariant = ThemeVariant.AppleDark;
            ApplyThemePreset();
            BuildPanel();
        }

        public void RebuildSlots()
        {
            ApplyThemePreset();
            EnsureCanvas();
            EnsurePanel();
            ApplySlotLayoutSettings();
            ClearRuntimeSlots();
            PrepareSlotTemplate();

            if (m_BuildingSlots.Count == 0)
            {
                for (int i = 0; i < Mathf.Max(1, m_EmptySlotCount); i++)
                {
                    CreateSlotButton($"Empty Slot {i + 1}", null, i, false);
                }
            }
            else
            {
                for (int i = 0; i < m_BuildingSlots.Count; i++)
                {
                    BuildingSlotData slot = m_BuildingSlots[i];
                    string displayName = ResolveSlotName(slot, i);
                    Sprite displayIcon = ResolveSlotIcon(slot);
                    bool hasPrefab = slot.buildingPrefab != null;
                    bool canInteract = m_DisableButtonsWithoutPrefab ? hasPrefab : true;
                    CreateSlotButton(displayName, displayIcon, i, canInteract);
                }
            }

            RefreshScrollState();
            RefreshSelectedPrefab();
        }

        Transform ResolveFollowTarget()
        {
            if (m_FollowTargetOverride != null)
            {
                return m_FollowTargetOverride;
            }

            if (m_TargetCanvas != null && m_TargetCanvas.worldCamera != null)
            {
                return m_TargetCanvas.worldCamera.transform;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            return null;
        }

        void UpdateFollowOffsets(RectTransform canvasRect, Transform followTarget)
        {
            if (canvasRect == null || followTarget == null)
            {
                return;
            }

            if (m_FollowOffsetInitialized && m_LastFollowTarget == followTarget)
            {
                return;
            }

            if (m_UseCurrentOffsetAsFollowOffset)
            {
                m_RuntimeFollowLocalPositionOffset = followTarget.InverseTransformPoint(canvasRect.position);
                m_RuntimeFollowLocalRotationOffset = Quaternion.Inverse(followTarget.rotation) * canvasRect.rotation;
            }
            else
            {
                m_RuntimeFollowLocalPositionOffset = m_FollowLocalPositionOffset;
                m_RuntimeFollowLocalRotationOffset = Quaternion.Euler(m_FollowLocalEulerOffset);
            }

            m_LastFollowTarget = followTarget;
            m_FollowOffsetInitialized = true;
        }

        void FollowCanvasToPlayer()
        {
            if (!Application.isPlaying || !m_FollowPlayerInWorldSpace || m_TargetCanvas == null || m_TargetCanvas.renderMode != RenderMode.WorldSpace)
            {
                return;
            }

            RectTransform canvasRect = m_TargetCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return;
            }

            Transform followTarget = ResolveFollowTarget();
            if (followTarget == null)
            {
                return;
            }

            UpdateFollowOffsets(canvasRect, followTarget);

            canvasRect.position = followTarget.TransformPoint(m_RuntimeFollowLocalPositionOffset);
            canvasRect.rotation = followTarget.rotation * m_RuntimeFollowLocalRotationOffset;
        }

        void ApplyThemePreset()
        {
            switch (m_ThemeVariant)
            {
                case ThemeVariant.AppleLight:
                    m_PanelColor = new Color(0.96f, 0.97f, 0.98f, 0.72f);
                    m_HeaderColor = new Color(1f, 1f, 1f, 0.08f);
                    m_SlotColor = new Color(1f, 1f, 1f, 0.62f);
                    m_SlotDisabledColor = new Color(0.86f, 0.87f, 0.9f, 0.42f);
                    m_TextColor = new Color(0.07f, 0.07f, 0.09f, 0.96f);
                    m_SlotBorderColor = new Color(1f, 1f, 1f, 0.45f);
                    m_PanelShadowColor = new Color(0f, 0f, 0f, 0.22f);
                    m_IconPlaceholderColor = new Color(0.9f, 0.91f, 0.94f, 0.95f);
                    m_IconPlaceholderDisabledColor = new Color(0.8f, 0.82f, 0.86f, 0.78f);
                    break;

                case ThemeVariant.AppleDark:
                    m_PanelColor = new Color(0.11f, 0.12f, 0.145f, 0.8f);
                    m_HeaderColor = new Color(1f, 1f, 1f, 0.03f);
                    m_SlotColor = new Color(0.2f, 0.215f, 0.245f, 0.86f);
                    m_SlotDisabledColor = new Color(0.15f, 0.16f, 0.18f, 0.64f);
                    m_TextColor = new Color(0.94f, 0.95f, 0.98f, 0.98f);
                    m_SlotBorderColor = new Color(1f, 1f, 1f, 0.16f);
                    m_PanelShadowColor = new Color(0f, 0f, 0f, 0.45f);
                    m_IconPlaceholderColor = new Color(0.4f, 0.44f, 0.52f, 0.9f);
                    m_IconPlaceholderDisabledColor = new Color(0.3f, 0.33f, 0.39f, 0.78f);
                    break;
            }
        }

        public void SelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= m_BuildingSlots.Count)
            {
                Debug.LogWarning($"Slot index {slotIndex} is out of range.", this);
                return;
            }

            m_SelectedSlotIndex = slotIndex;
            m_SelectedPrefab = m_BuildingSlots[slotIndex].buildingPrefab;
            m_OnPrefabSelected.Invoke(m_SelectedPrefab);
            m_OnSlotSelected.Invoke(slotIndex, m_SelectedPrefab);
        }

        public bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = m_SelectedPrefab;
            return prefab != null;
        }

        public GameObject GetSlotPrefab(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= m_BuildingSlots.Count)
            {
                return null;
            }

            return m_BuildingSlots[slotIndex].buildingPrefab;
        }

        public void ClearSelection()
        {
            m_SelectedSlotIndex = -1;
            m_SelectedPrefab = null;
        }

        void RefreshSelectedPrefab()
        {
            if (m_SelectedSlotIndex < 0 || m_SelectedSlotIndex >= m_BuildingSlots.Count)
            {
                m_SelectedSlotIndex = -1;
                m_SelectedPrefab = null;
                return;
            }

            m_SelectedPrefab = m_BuildingSlots[m_SelectedSlotIndex].buildingPrefab;
        }

        void EnsureCanvas()
        {
            if (m_TargetCanvas != null)
            {
                return;
            }

            if (m_PanelRootOverride != null)
            {
                m_TargetCanvas = m_PanelRootOverride.GetComponentInParent<Canvas>();
                if (m_TargetCanvas != null)
                {
                    return;
                }
            }

            if (m_SlotsRootOverride != null)
            {
                m_TargetCanvas = m_SlotsRootOverride.GetComponentInParent<Canvas>();
                if (m_TargetCanvas != null)
                {
                    return;
                }
            }

            m_TargetCanvas = CreateFallbackCanvas();
        }

        Canvas CreateFallbackCanvas()
        {
            GameObject canvasObject = new GameObject(
                "Building Panel Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 20f;
            scaler.referencePixelsPerUnit = 100f;

            TryAddTrackedDeviceRaycaster(canvasObject);

            canvasObject.transform.SetParent(transform, false);

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1200f, 900f);

            Transform canvasTransform = canvasObject.transform;
            canvasTransform.localScale = Vector3.one * Mathf.Max(0.0001f, m_FallbackCanvasScale);

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Transform cameraTransform = mainCamera.transform;
                canvasTransform.position = cameraTransform.position + cameraTransform.forward * Mathf.Max(0.5f, m_FallbackCanvasDistance);
                canvasTransform.rotation = cameraTransform.rotation;
            }

            return canvas;
        }

        void TryAddTrackedDeviceRaycaster(GameObject canvasObject)
        {
            Type trackedRaycasterType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedRaycasterType != null && canvasObject.GetComponent(trackedRaycasterType) == null)
            {
                canvasObject.AddComponent(trackedRaycasterType);
            }
        }

        void EnsurePanel()
        {
            if (m_PanelRootOverride != null)
            {
                m_PanelTransform = m_PanelRootOverride;
            }

            if (m_SlotsRootOverride != null)
            {
                m_SlotsRoot = m_SlotsRootOverride;
                m_SlotScrollRect = m_SlotsRoot.GetComponentInParent<ScrollRect>();
            }

            if (m_PanelTransform != null && m_SlotsRoot == null)
            {
                Transform slotContent = m_PanelTransform.Find("SlotScroll/Content");
                if (slotContent != null)
                {
                    m_SlotsRoot = slotContent as RectTransform;
                    m_SlotScrollRect = slotContent.GetComponentInParent<ScrollRect>();
                }
            }

            if (m_SlotsRoot != null && m_PanelTransform == null)
            {
                m_PanelTransform = m_SlotsRoot.GetComponentInParent<RectTransform>();
            }

            if (m_PanelTransform != null && m_SlotsRoot != null)
            {
                return;
            }

            if (m_TargetCanvas == null)
            {
                return;
            }

            Transform existingPanel = m_TargetCanvas.transform.Find("Building Panel");
            if (existingPanel != null)
            {
                m_PanelTransform = existingPanel as RectTransform;
                Transform existingContent = existingPanel.Find("SlotScroll/Content");
                if (existingContent != null)
                {
                    m_SlotsRoot = existingContent as RectTransform;
                    m_SlotScrollRect = existingContent.GetComponentInParent<ScrollRect>();
                    return;
                }
            }

            CreatePanelHierarchy();
        }

        void CreatePanelHierarchy()
        {
            GameObject panelObject;
            bool createdPanel = false;
            if (m_PanelTransform != null)
            {
                panelObject = m_PanelTransform.gameObject;
                if (panelObject.transform.parent != m_TargetCanvas.transform)
                {
                    panelObject.transform.SetParent(m_TargetCanvas.transform, false);
                }
            }
            else
            {
                panelObject = new GameObject(
                    "Building Panel",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(VerticalLayoutGroup));
                panelObject.transform.SetParent(m_TargetCanvas.transform, false);
                m_PanelTransform = panelObject.GetComponent<RectTransform>();
                createdPanel = true;
            }

            if (!panelObject.TryGetComponent(out Image panelImage))
            {
                panelImage = panelObject.AddComponent<Image>();
            }

            if (!panelObject.TryGetComponent(out VerticalLayoutGroup panelLayout))
            {
                panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
            }

            for (int i = panelObject.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = panelObject.transform.GetChild(i);
                if (child != null && (child.name == "Header" || child.name == "SlotScroll" || child.name == "Slots"))
                {
                    DestroyObject(child.gameObject);
                }
            }

            m_PanelTransform = panelObject.GetComponent<RectTransform>();
            if (createdPanel)
            {
                m_PanelTransform.anchorMin = new Vector2(0.5f, 0f);
                m_PanelTransform.anchorMax = new Vector2(0.5f, 0f);
                m_PanelTransform.pivot = new Vector2(0.5f, 0f);
                m_PanelTransform.sizeDelta = m_PanelSize;
                m_PanelTransform.anchoredPosition = m_PanelAnchorOffset;
            }

            panelImage.enabled = true;
            panelImage.color = m_PanelColor;
            ApplyRoundedImage(panelImage, true);

            Shadow panelShadow = panelObject.GetComponent<Shadow>();
            if (panelShadow == null)
            {
                panelShadow = panelObject.AddComponent<Shadow>();
            }
            panelShadow.effectColor = m_PanelShadowColor;
            panelShadow.effectDistance = new Vector2(0f, -5f);
            panelShadow.useGraphicAlpha = true;

            panelLayout.padding = new RectOffset(14, 14, 10, 12);
            panelLayout.spacing = 8f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            GameObject headerObject = new GameObject(
                "Header",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));
            headerObject.transform.SetParent(panelObject.transform, false);

            Image headerImage = headerObject.GetComponent<Image>();
            headerImage.color = m_HeaderColor;
            ApplyRoundedImage(headerImage, false);

            LayoutElement headerLayout = headerObject.GetComponent<LayoutElement>();
            headerLayout.preferredHeight = 52f;
            headerLayout.flexibleHeight = 0f;

            RectTransform titleTransform = CreateText("Title", headerObject.transform, m_Title, 30f, TextAlignmentOptions.Center, true);
            titleTransform.anchorMin = Vector2.zero;
            titleTransform.anchorMax = Vector2.one;
            titleTransform.offsetMin = Vector2.zero;
            titleTransform.offsetMax = Vector2.zero;

            GameObject scrollObject = new GameObject(
                "SlotScroll",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect),
                typeof(LayoutElement));
            scrollObject.transform.SetParent(panelObject.transform, false);

            Image scrollBackground = scrollObject.GetComponent<Image>();
            scrollBackground.color = new Color(0f, 0f, 0f, 0f);
            scrollBackground.raycastTarget = true;

            LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
            float slotItemHeight = GetSlotItemHeight();
            scrollLayout.minHeight = slotItemHeight + 8f;
            scrollLayout.preferredHeight = slotItemHeight + 8f;
            scrollLayout.flexibleHeight = 0f;

            RectTransform viewportRect = scrollObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            GameObject contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(scrollObject.transform, false);
            m_SlotsRoot = contentObject.GetComponent<RectTransform>();

            m_SlotsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            m_SlotsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            m_SlotsRoot.pivot = new Vector2(0.5f, 0.5f);
            m_SlotsRoot.anchoredPosition = Vector2.zero;
            m_SlotsRoot.sizeDelta = new Vector2(0f, slotItemHeight);

            HorizontalLayoutGroup horizontalLayout = contentObject.GetComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = new RectOffset(0, 0, 0, 0);
            horizontalLayout.spacing = m_SlotSpacing;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            horizontalLayout.childAlignment = TextAnchor.MiddleCenter;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            m_SlotScrollRect = scrollObject.GetComponent<ScrollRect>();
            m_SlotScrollRect.viewport = viewportRect;
            m_SlotScrollRect.content = m_SlotsRoot;
            m_SlotScrollRect.horizontal = true;
            m_SlotScrollRect.vertical = false;
            m_SlotScrollRect.movementType = ScrollRect.MovementType.Clamped;
            m_SlotScrollRect.inertia = true;
            m_SlotScrollRect.decelerationRate = 0.135f;
            m_SlotScrollRect.scrollSensitivity = 20f;

            ApplySlotLayoutSettings();
        }

        void ApplySlotLayoutSettings()
        {
            float slotItemHeight = GetSlotItemHeight();

            if (m_SlotsRoot != null)
            {
                HorizontalLayoutGroup horizontalLayout = m_SlotsRoot.GetComponent<HorizontalLayoutGroup>();
                if (horizontalLayout != null)
                {
                    horizontalLayout.spacing = m_SlotSpacing;
                }

                m_SlotsRoot.sizeDelta = new Vector2(m_SlotsRoot.sizeDelta.x, slotItemHeight);
            }

            if (m_SlotScrollRect != null)
            {
                LayoutElement scrollLayout = m_SlotScrollRect.GetComponent<LayoutElement>();
                if (scrollLayout != null)
                {
                    scrollLayout.minHeight = slotItemHeight + 8f;
                    scrollLayout.preferredHeight = slotItemHeight + 8f;
                    scrollLayout.flexibleHeight = 0f;
                }
            }
        }

        float GetSlotLabelHeight()
        {
            return Mathf.Clamp(m_SlotSize.y * 0.28f, 20f, 34f);
        }

        float GetSlotLabelGap()
        {
            return Mathf.Clamp(m_SlotSize.y * 0.1f, 6f, 12f);
        }

        float GetSlotItemHeight()
        {
            return m_SlotSize.y + GetSlotLabelGap() + GetSlotLabelHeight();
        }

        RectTransform CreateSlotItemContainer(int index)
        {
            GameObject containerObject = new GameObject(
                $"SlotItem_{index + 1}",
                typeof(RectTransform),
                typeof(LayoutElement));
            containerObject.transform.SetParent(m_SlotsRoot, false);
            m_RuntimeSlotObjects.Add(containerObject);

            RectTransform containerTransform = containerObject.GetComponent<RectTransform>();
            float itemHeight = GetSlotItemHeight();
            containerTransform.sizeDelta = new Vector2(m_SlotSize.x, itemHeight);

            LayoutElement containerLayout = containerObject.GetComponent<LayoutElement>();
            containerLayout.preferredWidth = m_SlotSize.x;
            containerLayout.preferredHeight = itemHeight;
            containerLayout.flexibleWidth = 0f;
            containerLayout.flexibleHeight = 0f;
            return containerTransform;
        }

        void CreateSlotLabel(RectTransform parent, string displayName)
        {
            float labelHeight = GetSlotLabelHeight();
            RectTransform labelTransform = CreateText(
                "Label",
                parent,
                displayName,
                Mathf.Clamp(m_SlotSize.y * 0.22f, 14f, 20f),
                TextAlignmentOptions.Midline,
                true);
            labelTransform.anchorMin = new Vector2(0f, 0f);
            labelTransform.anchorMax = new Vector2(1f, 0f);
            labelTransform.pivot = new Vector2(0.5f, 0f);
            labelTransform.anchoredPosition = Vector2.zero;
            labelTransform.sizeDelta = new Vector2(-8f, labelHeight);
        }

        void ConfigureSlotOutline(GameObject targetObject, bool interactable)
        {
            if (targetObject == null)
            {
                return;
            }

            Outline slotOutline = targetObject.GetComponent<Outline>();
            if (slotOutline == null)
            {
                slotOutline = targetObject.AddComponent<Outline>();
            }

            slotOutline.effectColor = interactable
                ? m_SlotBorderColor
                : new Color(m_SlotBorderColor.r, m_SlotBorderColor.g, m_SlotBorderColor.b, m_SlotBorderColor.a * 0.6f);

            float borderThickness = Mathf.Max(0.5f, m_SlotBorderThickness);
            slotOutline.effectDistance = new Vector2(borderThickness, -borderThickness);
            slotOutline.useGraphicAlpha = true;
        }

        void CreateSlotButton(string displayName, Sprite icon, int index, bool interactable)
        {
            if (m_SlotTemplate != null)
            {
                CreateSlotFromTemplate(displayName, icon, index, interactable);
                return;
            }

            RectTransform containerTransform = CreateSlotItemContainer(index);
            GameObject slotObject = new GameObject(
                $"Slot_{index + 1}",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(Image),
                typeof(Button));
            slotObject.transform.SetParent(containerTransform, false);

            RectTransform slotTransform = slotObject.GetComponent<RectTransform>();
            slotTransform.anchorMin = new Vector2(0.5f, 1f);
            slotTransform.anchorMax = new Vector2(0.5f, 1f);
            slotTransform.pivot = new Vector2(0.5f, 1f);
            slotTransform.anchoredPosition = Vector2.zero;
            slotTransform.sizeDelta = m_SlotSize;

            LayoutElement slotLayout = slotObject.GetComponent<LayoutElement>();
            slotLayout.preferredWidth = m_SlotSize.x;
            slotLayout.preferredHeight = m_SlotSize.y;
            slotLayout.flexibleWidth = 0f;
            slotLayout.flexibleHeight = 0f;

            Image slotImage = slotObject.GetComponent<Image>();
            slotImage.color = interactable ? m_SlotColor : m_SlotDisabledColor;
            ApplyRoundedImage(slotImage, false);
            ConfigureSlotOutline(slotObject, interactable);

            Button slotButton = slotObject.GetComponent<Button>();
            slotButton.targetGraphic = slotImage;
            slotButton.interactable = interactable;
            m_RuntimeButtons.Add(slotButton);

            if (interactable)
            {
                int capturedIndex = index;
                slotButton.onClick.AddListener(delegate { SelectSlot(capturedIndex); });
            }

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(slotObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            float iconPadding = Mathf.Clamp(m_SlotSize.x * 0.14f, 6f, 12f);
            float iconSize = Mathf.Clamp(
                Mathf.Min(m_SlotSize.x, m_SlotSize.y) - (iconPadding * 2f),
                18f,
                m_SlotSize.x - (iconPadding * 2f));

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);

            Image iconImage = iconObject.GetComponent<Image>();
            ApplySlotIcon(iconImage, icon, interactable);

            CreateSlotLabel(containerTransform, displayName);
        }

        void CreateSlotFromTemplate(string displayName, Sprite icon, int index, bool interactable)
        {
            RectTransform containerTransform = CreateSlotItemContainer(index);
            BuildingSlotVisualRefs slotRefs = Instantiate(m_SlotTemplate, containerTransform);
            slotRefs.gameObject.name = $"Slot_{index + 1}";
            slotRefs.gameObject.SetActive(true);
            RectTransform slotTransform = slotRefs.GetComponent<RectTransform>();
            if (slotTransform != null)
            {
                slotTransform.anchorMin = new Vector2(0.5f, 1f);
                slotTransform.anchorMax = new Vector2(0.5f, 1f);
                slotTransform.pivot = new Vector2(0.5f, 1f);
                slotTransform.anchoredPosition = Vector2.zero;
                slotTransform.sizeDelta = m_SlotSize;
            }

            LayoutElement slotLayout = slotRefs.GetComponent<LayoutElement>();
            if (slotLayout == null)
            {
                slotLayout = slotRefs.gameObject.AddComponent<LayoutElement>();
            }
            slotLayout.preferredWidth = m_SlotSize.x;
            slotLayout.preferredHeight = m_SlotSize.y;
            slotLayout.flexibleWidth = 0f;
            slotLayout.flexibleHeight = 0f;

            Button slotButton = slotRefs.button != null ? slotRefs.button : slotRefs.GetComponent<Button>();
            if (slotButton != null)
            {
                slotButton.interactable = interactable;
                m_RuntimeButtons.Add(slotButton);

                if (interactable)
                {
                    int capturedIndex = index;
                    slotButton.onClick.AddListener(delegate { SelectSlot(capturedIndex); });
                }
            }

            if (slotRefs.label != null)
            {
                slotRefs.label.gameObject.SetActive(false);
            }

            if (slotRefs.icon != null)
            {
                ApplySlotIcon(slotRefs.icon, icon, interactable);
            }

            if (slotRefs.background != null)
            {
                slotRefs.background.color = interactable ? m_SlotColor : m_SlotDisabledColor;
                ApplyRoundedImage(slotRefs.background, false);
            }

            GameObject outlineTarget = slotRefs.background != null ? slotRefs.background.gameObject : slotRefs.gameObject;
            ConfigureSlotOutline(outlineTarget, interactable);

            CreateSlotLabel(containerTransform, displayName);
        }

        void ApplyRoundedImage(Image image, bool panelStyle)
        {
            if (!m_UseRoundedStyle || image == null)
            {
                return;
            }

            Sprite roundedSprite = panelStyle
                ? GetRoundedSprite(ref m_PanelRoundedSprite, 128, m_PanelCornerRadius, "PanelRoundedSprite")
                : GetRoundedSprite(ref m_SlotRoundedSprite, 96, m_SlotCornerRadius, "SlotRoundedSprite");

            if (roundedSprite == null)
            {
                return;
            }

            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        void ApplySlotIcon(Image iconImage, Sprite icon, bool interactable)
        {
            if (iconImage == null)
            {
                return;
            }

            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
                iconImage.color = Color.white;
                iconImage.enabled = true;
                return;
            }

            iconImage.sprite = GetRoundedSprite(ref m_IconRoundedSprite, 64, 14, "SlotIconRoundedSprite");
            iconImage.type = Image.Type.Sliced;
            iconImage.preserveAspect = false;
            iconImage.color = interactable ? m_IconPlaceholderColor : m_IconPlaceholderDisabledColor;
            iconImage.enabled = true;
        }

        Sprite GetRoundedSprite(ref Sprite cache, int textureSize, int cornerRadius, string spriteName)
        {
            if (cache != null)
            {
                return cache;
            }

            textureSize = Mathf.Max(16, textureSize);
            int safeRadius = Mathf.Clamp(cornerRadius, 2, (textureSize / 2) - 1);

            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.name = $"{spriteName}_Tex";

            Color32[] pixels = new Color32[textureSize * textureSize];
            float min = safeRadius;
            float max = (textureSize - 1) - safeRadius;

            for (int y = 0; y < textureSize; y++)
            {
                float fy = y + 0.5f;
                for (int x = 0; x < textureSize; x++)
                {
                    float fx = x + 0.5f;
                    float cx = Mathf.Clamp(fx, min, max);
                    float cy = Mathf.Clamp(fy, min, max);

                    float dx = fx - cx;
                    float dy = fy - cy;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = Mathf.Clamp01((safeRadius + 0.5f) - distance);
                    byte alphaByte = (byte)Mathf.RoundToInt(alpha * 255f);

                    pixels[(y * textureSize) + x] = new Color32(255, 255, 255, alphaByte);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            cache = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize,
                0,
                SpriteMeshType.FullRect,
                new Vector4(safeRadius, safeRadius, safeRadius, safeRadius),
                false);

            cache.name = spriteName;
            return cache;
        }

        RectTransform CreateText(string objectName, Transform parent, string textValue, float fontSize, TextAlignmentOptions alignment, bool useBold = false)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform textTransform = textObject.GetComponent<RectTransform>();
            textTransform.anchorMin = new Vector2(0f, 0.5f);
            textTransform.anchorMax = new Vector2(1f, 0.5f);
            textTransform.pivot = new Vector2(0.5f, 0.5f);
            textTransform.sizeDelta = new Vector2(0f, 32f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = useBold ? FontStyles.Bold : FontStyles.Normal;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = m_TextColor;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return textTransform;
        }

        void ClearRuntimeSlots()
        {
            for (int i = 0; i < m_RuntimeButtons.Count; i++)
            {
                if (m_RuntimeButtons[i] != null)
                {
                    m_RuntimeButtons[i].onClick.RemoveAllListeners();
                }
            }

            m_RuntimeButtons.Clear();

            for (int i = 0; i < m_RuntimeSlotObjects.Count; i++)
            {
                DestroyObject(m_RuntimeSlotObjects[i]);
            }
            m_RuntimeSlotObjects.Clear();
        }

        void RefreshScrollState()
        {
            if (m_SlotScrollRect == null || m_SlotsRoot == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(m_SlotsRoot);
            RectTransform viewport = m_SlotScrollRect.viewport != null ? m_SlotScrollRect.viewport : m_SlotScrollRect.GetComponent<RectTransform>();
            if (viewport == null)
            {
                return;
            }

            float contentWidth = m_SlotsRoot.rect.width;
            float viewportWidth = viewport.rect.width;
            bool shouldScroll = contentWidth > viewportWidth + 0.5f;

            m_SlotScrollRect.horizontal = shouldScroll;
            if (shouldScroll)
            {
                m_SlotScrollRect.horizontalNormalizedPosition = 0.5f;
            }
            else
            {
                m_SlotsRoot.anchoredPosition = Vector2.zero;
            }
        }

        string ResolveSlotName(BuildingSlotData slot, int index)
        {
            if (!string.IsNullOrWhiteSpace(slot.slotName))
            {
                return slot.slotName;
            }

            if (slot.buildingPrefab != null)
            {
                return slot.buildingPrefab.name;
            }

            return $"Slot {index + 1}";
        }

        Sprite ResolveSlotIcon(BuildingSlotData slot)
        {
            if (slot.icon != null)
            {
                return slot.icon;
            }

            if (slot.buildingPrefab == null)
            {
                return null;
            }

            SpriteRenderer spriteRenderer = slot.buildingPrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite;
            }

            Image prefabImage = slot.buildingPrefab.GetComponentInChildren<Image>(true);
            if (prefabImage != null && prefabImage.sprite != null)
            {
                return prefabImage.sprite;
            }

#if UNITY_EDITOR
            int prefabId = slot.buildingPrefab.GetInstanceID();
            if (m_EditorPrefabIconCache.TryGetValue(prefabId, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D previewTexture = AssetPreview.GetAssetPreview(slot.buildingPrefab);
            if (previewTexture == null)
            {
                previewTexture = AssetPreview.GetMiniThumbnail(slot.buildingPrefab) as Texture2D;
            }

            if (previewTexture == null)
            {
                return null;
            }

            Sprite previewSprite;
            try
            {
                previewSprite = Sprite.Create(
                    previewTexture,
                    new Rect(0f, 0f, previewTexture.width, previewTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            catch
            {
                return null;
            }

            previewSprite.name = $"Preview_{slot.buildingPrefab.name}";
            m_EditorPrefabIconCache[prefabId] = previewSprite;
            return previewSprite;
#else
            return null;
#endif
        }

        void DestroyObject(GameObject target)
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

        void PrepareSlotTemplate()
        {
            if (m_SlotTemplate == null)
            {
                return;
            }

            bool templateIsSceneObject = m_SlotTemplate.gameObject.scene.IsValid();
            if (m_HideTemplateOnRuntime && templateIsSceneObject && Application.isPlaying && m_SlotTemplate.gameObject.activeSelf)
            {
                m_SlotTemplate.gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            for (int i = 0; i < m_BuildingSlots.Count; i++)
            {
                BuildingSlotData slot = m_BuildingSlots[i];
                if (string.IsNullOrWhiteSpace(slot.slotName) && slot.buildingPrefab != null)
                {
                    slot.slotName = slot.buildingPrefab.name;
                    m_BuildingSlots[i] = slot;
                }
            }

            m_Columns = Mathf.Max(1, m_Columns);
            m_EmptySlotCount = Mathf.Max(1, m_EmptySlotCount);
            m_FallbackCanvasScale = Mathf.Max(0.0001f, m_FallbackCanvasScale);
            m_FallbackCanvasDistance = Mathf.Max(0.5f, m_FallbackCanvasDistance);
            m_PanelSize = new Vector2(Mathf.Max(320f, m_PanelSize.x), Mathf.Max(120f, m_PanelSize.y));
            float slotSide = Mathf.Max(72f, Mathf.Min(m_SlotSize.x, m_SlotSize.y));
            m_SlotSize = new Vector2(slotSide, slotSide);
            m_SlotSpacing = Mathf.Max(0f, m_SlotSpacing);
            m_PanelCornerRadius = Mathf.Max(4, m_PanelCornerRadius);
            m_SlotCornerRadius = Mathf.Max(4, m_SlotCornerRadius);
            m_SlotBorderThickness = Mathf.Max(0.5f, m_SlotBorderThickness);
            ApplyThemePreset();
            ApplySlotLayoutSettings();
            RefreshSelectedPrefab();
            m_FollowOffsetInitialized = false;
            m_LastFollowTarget = null;
            m_PanelRoundedSprite = null;
            m_SlotRoundedSprite = null;
            m_IconRoundedSprite = null;
        }
#endif
    }
}
