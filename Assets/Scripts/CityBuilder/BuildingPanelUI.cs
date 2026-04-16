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
    public enum BuildingPanelCategory
    {
        Housing,
        Resources,
        Decoration,
    }

    [DisallowMultipleComponent]
    public class BuildingPanelUI : MonoBehaviour
    {
        [Serializable]
        public struct BuildingSlotData
        {
            public string slotName;
            public GameObject buildingPrefab;
            public Sprite icon;
            public BuildingPanelCategory category;
        }

        [Serializable]
        public class BuildingSlotSelectedEvent : UnityEvent<int, GameObject>
        {
        }

        [Serializable]
        public class BuildingPrefabSelectedEvent : UnityEvent<GameObject>
        {
        }

        [Header("Buildings")]
        [SerializeField] List<BuildingSlotData> m_BuildingSlots = new();
        [SerializeField] bool m_UseQuickPrefabList = true;
        [SerializeField] bool m_AutoSyncSlotsFromQuickList = true;
        [SerializeField] bool m_OverwriteSlotNamesFromQuickList = true;
        [SerializeField] List<GameObject> m_QuickPrefabList = new();
        [SerializeField, Min(1)] int m_EmptySlotCount = 4;
        [SerializeField] bool m_DisableButtonsWithoutPrefab = true;

        [Header("Categories")]
        [SerializeField] bool m_EnableCategoryMenu = true;
        [SerializeField] string m_HousingCategoryLabel = "Viviendas";
        [SerializeField] string m_ResourcesCategoryLabel = "Recursos";
        [SerializeField] string m_DecorationCategoryLabel = "Decoracion";
        [SerializeField] string m_BackButtonLabel = "Atras";
        [SerializeField] string m_EmptyCategorySlotLabel = "Sin edificios";

        [Header("Canvas References")]
        [SerializeField] Canvas m_TargetCanvas;
        [SerializeField] string m_Title = "Buildings";
        [SerializeField] RectTransform m_PanelRootOverride;
        [SerializeField] RectTransform m_SlotsRootOverride;
        [SerializeField] ScrollRect m_SlotScrollRect;
        [SerializeField] BuildingSlotVisualRefs m_SlotTemplate;
        [SerializeField] Button m_BackButton;
        [SerializeField] bool m_BuildPanelOnStart = true;
        [SerializeField] bool m_CreateFallbackLayoutIfMissing = true;

        [Header("Layout")]
        [SerializeField] Vector2 m_PanelSize = new(920f, 220f);
        [SerializeField] Vector2 m_PanelAnchorOffset = new(0f, 24f);
        [SerializeField] Vector2 m_SlotSize = new(104f, 104f);
        [SerializeField, Min(0f)] float m_SlotSpacing = 18f;
        [SerializeField] float m_FallbackCanvasDistance = 1.7f;
        [SerializeField] float m_FallbackCanvasScale = 0.0016f;

        [Header("Visuals")]
        [SerializeField] Color m_PanelColor = new(0.08f, 0.1f, 0.13f, 0.86f);
        [SerializeField] Color m_SlotColor = new(0.18f, 0.2f, 0.24f, 0.95f);
        [SerializeField] Color m_SelectedSlotColor = new(0.18f, 0.45f, 0.72f, 0.98f);
        [SerializeField] Color m_DisabledSlotColor = new(0.13f, 0.14f, 0.16f, 0.72f);
        [SerializeField] Color m_TextColor = new(0.95f, 0.96f, 0.98f, 1f);

        [Header("World Space Follow")]
        [SerializeField] bool m_FollowPlayerInWorldSpace = true;
        [SerializeField] Transform m_FollowTargetOverride;
        [SerializeField] bool m_UseCurrentOffsetAsFollowOffset = true;
        [SerializeField] Vector3 m_FollowLocalPositionOffset = new(0f, -0.12f, 1.7f);
        [SerializeField] Vector3 m_FollowLocalEulerOffset = Vector3.zero;

        [Header("Events")]
        [SerializeField] BuildingSlotSelectedEvent m_OnSlotSelected = new();
        [SerializeField] BuildingPrefabSelectedEvent m_OnPrefabSelected = new();

        readonly List<BuildingSlotVisualRefs> m_RuntimeSlots = new();
        readonly List<int> m_RuntimeSlotIndices = new();
        readonly Dictionary<int, Sprite> m_EditorPrefabIconCache = new();

        bool m_UsingExternalSlotsData;
        bool m_ShowingCategoryMenu = true;
        BuildingPanelCategory m_CurrentCategory = BuildingPanelCategory.Housing;
        int m_SelectedSlotIndex = -1;
        GameObject m_SelectedPrefab;
        TMP_Text m_TitleLabel;
        TMP_Text m_BackButtonText;
        bool m_FollowOffsetInitialized;
        Transform m_LastFollowTarget;
        Vector3 m_RuntimeFollowLocalPositionOffset;
        Quaternion m_RuntimeFollowLocalRotationOffset;

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
            if (m_UseQuickPrefabList && m_AutoSyncSlotsFromQuickList && !m_UsingExternalSlotsData)
            {
                SyncSlotsFromQuickPrefabList();
            }

            EnsureCanvas();
            if (!EnsureRuntimeLayout())
            {
                Debug.LogWarning("BuildingPanelUI could not create a valid runtime layout.", this);
                return;
            }

            RebuildSlots();
        }

        [ContextMenu("Buildings/Sync Slots From Quick Prefab List")]
        public void SyncSlotsFromQuickPrefabList()
        {
            m_UsingExternalSlotsData = false;

            if (m_QuickPrefabList == null)
            {
                m_QuickPrefabList = new List<GameObject>();
            }

            m_BuildingSlots.Clear();
            for (int i = 0; i < m_QuickPrefabList.Count; i++)
            {
                GameObject prefab = m_QuickPrefabList[i];
                m_BuildingSlots.Add(new BuildingSlotData
                {
                    slotName = prefab != null && m_OverwriteSlotNamesFromQuickList ? prefab.name : $"Slot {i + 1}",
                    buildingPrefab = prefab,
                    icon = null,
                    category = ResolvePrefabCategory(prefab),
                });
            }

            RefreshSelectedPrefab();
        }

        public void SetBuildingSlots(IReadOnlyList<BuildingSlotData> slots, bool rebuildPanel = true, bool suppressQuickPrefabSyncOnRebuild = true)
        {
            m_BuildingSlots.Clear();
            m_UsingExternalSlotsData = suppressQuickPrefabSyncOnRebuild;

            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    m_BuildingSlots.Add(slots[i]);
                }
            }

            RefreshSelectedPrefab();

            if (rebuildPanel)
            {
                BuildPanel();
            }
        }

        public void ShowCategoryMenu()
        {
            if (!m_EnableCategoryMenu)
            {
                return;
            }

            m_ShowingCategoryMenu = true;
            RebuildSlots();
        }

        public void ShowHousing()
        {
            ShowCategory(BuildingPanelCategory.Housing);
        }

        public void ShowResources()
        {
            ShowCategory(BuildingPanelCategory.Resources);
        }

        public void ShowDecoration()
        {
            ShowCategory(BuildingPanelCategory.Decoration);
        }

        public void ShowCategory(BuildingPanelCategory category)
        {
            m_CurrentCategory = category;
            m_ShowingCategoryMenu = false;
            RebuildSlots();
        }

        public void RebuildSlots()
        {
            if (!EnsureRuntimeLayout())
            {
                return;
            }

            ClearRuntimeSlots();

            if (m_EnableCategoryMenu && m_ShowingCategoryMenu)
            {
                CreateCategoryRuntimeSlot(BuildingPanelCategory.Housing);
                CreateCategoryRuntimeSlot(BuildingPanelCategory.Resources);
                CreateCategoryRuntimeSlot(BuildingPanelCategory.Decoration);
            }
            else if (m_BuildingSlots.Count == 0)
            {
                for (int i = 0; i < Mathf.Max(1, m_EmptySlotCount); i++)
                {
                    CreateRuntimeSlot(new BuildingSlotData { slotName = $"Empty Slot {i + 1}" }, i, false);
                }
            }
            else
            {
                int visibleCount = 0;
                for (int i = 0; i < m_BuildingSlots.Count; i++)
                {
                    BuildingSlotData slot = m_BuildingSlots[i];
                    if (m_EnableCategoryMenu && !IsSlotVisibleInCurrentCategory(slot))
                    {
                        continue;
                    }

                    bool hasPrefab = slot.buildingPrefab != null;
                    bool interactable = m_DisableButtonsWithoutPrefab ? hasPrefab : true;
                    CreateRuntimeSlot(slot, i, interactable);
                    visibleCount++;
                }

                if (visibleCount == 0)
                {
                    CreateMessageRuntimeSlot(string.IsNullOrWhiteSpace(m_EmptyCategorySlotLabel) ? "Sin edificios" : m_EmptyCategorySlotLabel);
                }
            }

            UpdateSelectionVisuals();
            RefreshScrollState();
            UpdateHeaderState();
        }

        public void SelectSlot(int slotIndex)
        {
            if (!TryGetSlotData(slotIndex, out BuildingSlotData slot))
            {
                return;
            }

            m_SelectedSlotIndex = slotIndex;
            m_SelectedPrefab = slot.buildingPrefab;
            UpdateSelectionVisuals();
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
            return TryGetSlotData(slotIndex, out BuildingSlotData slot) ? slot.buildingPrefab : null;
        }

        public bool TryGetSlotData(int slotIndex, out BuildingSlotData slot)
        {
            if (slotIndex < 0 || slotIndex >= m_BuildingSlots.Count)
            {
                slot = default;
                return false;
            }

            slot = m_BuildingSlots[slotIndex];
            return true;
        }

        public void ClearSelection()
        {
            m_SelectedSlotIndex = -1;
            m_SelectedPrefab = null;
            UpdateSelectionVisuals();
        }

        void RefreshSelectedPrefab()
        {
            if (!TryGetSlotData(m_SelectedSlotIndex, out BuildingSlotData slot))
            {
                m_SelectedSlotIndex = -1;
                m_SelectedPrefab = null;
                return;
            }

            m_SelectedPrefab = slot.buildingPrefab;
        }

        void EnsureCanvas()
        {
            if (m_TargetCanvas != null)
            {
                return;
            }

            m_TargetCanvas = GetComponentInParent<Canvas>(true);
            if (m_TargetCanvas == null)
            {
                m_TargetCanvas = CreateFallbackCanvas();
            }
        }

        bool EnsureRuntimeLayout()
        {
            RectTransform panelRoot = ResolvePanelRoot();
            if (panelRoot == null)
            {
                return false;
            }

            m_PanelRootOverride = panelRoot;
            ConfigurePanelRoot(panelRoot);
            EnsureHeader(panelRoot);
            EnsureScrollHierarchy(panelRoot);
            EnsureSlotTemplate();

            return m_PanelRootOverride != null && m_SlotsRootOverride != null && m_SlotTemplate != null;
        }

        RectTransform ResolvePanelRoot()
        {
            if (m_PanelRootOverride != null)
            {
                return m_PanelRootOverride;
            }

            if (TryGetComponent(out RectTransform ownRect))
            {
                return ownRect;
            }

            if (!m_CreateFallbackLayoutIfMissing || m_TargetCanvas == null)
            {
                return null;
            }

            GameObject panelRootObject = new("Building Panel Root", typeof(RectTransform));
            panelRootObject.transform.SetParent(m_TargetCanvas.transform, false);
            return panelRootObject.GetComponent<RectTransform>();
        }

        void ConfigurePanelRoot(RectTransform panelRoot)
        {
            panelRoot.anchorMin = new Vector2(0.5f, 0f);
            panelRoot.anchorMax = new Vector2(0.5f, 0f);
            panelRoot.pivot = new Vector2(0.5f, 0f);
            panelRoot.sizeDelta = m_PanelSize;
            panelRoot.anchoredPosition = m_PanelAnchorOffset;

            if (!panelRoot.TryGetComponent(out Image panelImage))
            {
                panelImage = panelRoot.gameObject.AddComponent<Image>();
            }

            panelImage.color = m_PanelColor;
            panelImage.raycastTarget = false;

            if (!panelRoot.TryGetComponent(out VerticalLayoutGroup layout))
            {
                layout = panelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        void EnsureHeader(RectTransform panelRoot)
        {
            Transform header = panelRoot.Find("Header");
            RectTransform headerRect;

            if (header == null)
            {
                GameObject headerObject = new("Header", typeof(RectTransform), typeof(LayoutElement));
                headerObject.transform.SetParent(panelRoot, false);
                headerRect = headerObject.GetComponent<RectTransform>();

                LayoutElement layout = headerObject.GetComponent<LayoutElement>();
                layout.preferredHeight = 34f;

                GameObject titleObject = new("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObject.transform.SetParent(headerObject.transform, false);

                RectTransform titleRect = titleObject.GetComponent<RectTransform>();
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;

                m_TitleLabel = titleObject.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                headerRect = header as RectTransform;
                m_TitleLabel = header.GetComponentInChildren<TMP_Text>(true);
            }

            if (m_TitleLabel != null)
            {
                m_TitleLabel.color = m_TextColor;
                m_TitleLabel.fontSize = 26f;
                m_TitleLabel.alignment = TextAlignmentOptions.Center;
            }

            EnsureBackButton(headerRect);
            UpdateHeaderState();

            if (headerRect != null)
            {
                headerRect.SetAsFirstSibling();
            }
        }

        void EnsureBackButton(RectTransform headerRect)
        {
            if (headerRect == null)
            {
                return;
            }

            if (m_BackButton == null)
            {
                Transform existingBackButton = headerRect.Find("BackButton");
                if (existingBackButton != null)
                {
                    m_BackButton = existingBackButton.GetComponent<Button>();
                }
            }

            if (m_BackButton == null)
            {
                GameObject backObject = new("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
                backObject.transform.SetParent(headerRect, false);

                RectTransform backRect = backObject.GetComponent<RectTransform>();
                backRect.anchorMin = new Vector2(0f, 0f);
                backRect.anchorMax = new Vector2(0f, 1f);
                backRect.pivot = new Vector2(0f, 0.5f);
                backRect.anchoredPosition = Vector2.zero;
                backRect.sizeDelta = new Vector2(124f, 0f);

                Image backImage = backObject.GetComponent<Image>();
                backImage.color = m_SlotColor;

                m_BackButton = backObject.GetComponent<Button>();
                m_BackButton.targetGraphic = backImage;

                GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(backObject.transform, false);

                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);

                m_BackButtonText = labelObject.GetComponent<TMP_Text>();
            }

            if (m_BackButtonText == null && m_BackButton != null)
            {
                m_BackButtonText = m_BackButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (m_BackButtonText == null && m_BackButton != null)
            {
                GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(m_BackButton.transform, false);

                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);

                m_BackButtonText = labelObject.GetComponent<TMP_Text>();
            }

            if (m_BackButtonText != null)
            {
                m_BackButtonText.text = string.IsNullOrWhiteSpace(m_BackButtonLabel) ? "Atras" : m_BackButtonLabel;
                m_BackButtonText.color = m_TextColor;
                m_BackButtonText.fontSize = 18f;
                m_BackButtonText.alignment = TextAlignmentOptions.Center;
                m_BackButtonText.textWrappingMode = TextWrappingModes.NoWrap;
                m_BackButtonText.overflowMode = TextOverflowModes.Ellipsis;
            }

            m_BackButton.onClick.RemoveAllListeners();
            m_BackButton.onClick.AddListener(ShowCategoryMenu);
        }

        void UpdateHeaderState()
        {
            bool categoryViewActive = m_EnableCategoryMenu && !m_ShowingCategoryMenu;

            if (m_TitleLabel != null)
            {
                m_TitleLabel.text = categoryViewActive
                    ? ResolveCategoryLabel(m_CurrentCategory)
                    : (string.IsNullOrWhiteSpace(m_Title) ? "Buildings" : m_Title);

                RectTransform titleRect = m_TitleLabel.rectTransform;
                if (titleRect != null)
                {
                    titleRect.anchorMin = Vector2.zero;
                    titleRect.anchorMax = Vector2.one;
                    titleRect.offsetMin = categoryViewActive ? new Vector2(132f, 0f) : Vector2.zero;
                    titleRect.offsetMax = Vector2.zero;
                }
            }

            if (m_BackButton != null)
            {
                m_BackButton.gameObject.SetActive(categoryViewActive);
                m_BackButton.interactable = categoryViewActive;
            }
        }

        void EnsureScrollHierarchy(RectTransform panelRoot)
        {
            if (m_SlotScrollRect == null)
            {
                m_SlotScrollRect = panelRoot.GetComponentInChildren<ScrollRect>(true);
            }

            if (m_SlotScrollRect == null)
            {
                GameObject scrollObject = new(
                    "SlotScroll",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(ScrollRect),
                    typeof(LayoutElement));
                scrollObject.transform.SetParent(panelRoot, false);

                Image scrollImage = scrollObject.GetComponent<Image>();
                scrollImage.color = Color.clear;
                scrollImage.raycastTarget = false;

                LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
                scrollLayout.preferredHeight = m_SlotSize.y + 12f;

                m_SlotScrollRect = scrollObject.GetComponent<ScrollRect>();
                m_SlotScrollRect.horizontal = true;
                m_SlotScrollRect.vertical = false;
                m_SlotScrollRect.movementType = ScrollRect.MovementType.Clamped;
                m_SlotScrollRect.scrollSensitivity = 20f;

                GameObject viewportObject = new("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewportObject.transform.SetParent(scrollObject.transform, false);

                RectTransform viewport = viewportObject.GetComponent<RectTransform>();
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = Vector2.zero;

                Image viewportImage = viewportObject.GetComponent<Image>();
                viewportImage.color = Color.clear;
                viewportImage.raycastTarget = false;

                GameObject contentObject = new(
                    "Content",
                    typeof(RectTransform),
                    typeof(HorizontalLayoutGroup),
                    typeof(ContentSizeFitter));
                contentObject.transform.SetParent(viewportObject.transform, false);

                RectTransform content = contentObject.GetComponent<RectTransform>();
                content.anchorMin = new Vector2(0f, 0.5f);
                content.anchorMax = new Vector2(0f, 0.5f);
                content.pivot = new Vector2(0f, 0.5f);
                content.anchoredPosition = Vector2.zero;

                HorizontalLayoutGroup contentLayout = contentObject.GetComponent<HorizontalLayoutGroup>();
                contentLayout.spacing = m_SlotSpacing;
                contentLayout.padding = new RectOffset(0, 0, 0, 0);
                contentLayout.childAlignment = TextAnchor.MiddleLeft;
                contentLayout.childControlWidth = false;
                contentLayout.childControlHeight = false;
                contentLayout.childForceExpandWidth = false;
                contentLayout.childForceExpandHeight = false;

                ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                m_SlotScrollRect.viewport = viewport;
                m_SlotScrollRect.content = content;
                m_SlotsRootOverride = content;
                return;
            }

            if (m_SlotsRootOverride == null)
            {
                m_SlotsRootOverride = m_SlotScrollRect.content;
            }

            if (m_SlotsRootOverride == null)
            {
                Transform content = m_SlotScrollRect.transform.Find("Viewport/Content");
                if (content != null)
                {
                    m_SlotsRootOverride = content as RectTransform;
                    m_SlotScrollRect.content = m_SlotsRootOverride;
                }
            }
        }

        void EnsureSlotTemplate()
        {
            if (m_SlotsRootOverride == null)
            {
                return;
            }

            if (m_SlotTemplate == null)
            {
                BuildingSlotVisualRefs existingTemplate = m_SlotsRootOverride.GetComponentInChildren<BuildingSlotVisualRefs>(true);
                if (existingTemplate != null)
                {
                    m_SlotTemplate = existingTemplate;
                }
            }

            if (m_SlotTemplate == null)
            {
                m_SlotTemplate = CreateSlotTemplate(m_SlotsRootOverride);
            }

            if (m_SlotTemplate == null)
            {
                return;
            }

            m_SlotTemplate.AutoWire();
            m_SlotTemplate.gameObject.SetActive(false);
        }

        BuildingSlotVisualRefs CreateSlotTemplate(RectTransform parent)
        {
            GameObject slotObject = new(
                "SlotTemplate",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(Image),
                typeof(Button),
                typeof(CanvasGroup),
                typeof(VerticalLayoutGroup),
                typeof(BuildingSlotVisualRefs));
            slotObject.transform.SetParent(parent, false);
            slotObject.SetActive(false);

            RectTransform rect = slotObject.GetComponent<RectTransform>();
            rect.sizeDelta = m_SlotSize;

            LayoutElement layoutElement = slotObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = m_SlotSize.x;
            layoutElement.preferredHeight = m_SlotSize.y;

            VerticalLayoutGroup layout = slotObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Image background = slotObject.GetComponent<Image>();
            background.color = m_SlotColor;

            Button button = slotObject.GetComponent<Button>();
            button.targetGraphic = background;

            GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(slotObject.transform, false);

            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;

            LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
            iconLayout.preferredWidth = m_SlotSize.x - 20f;
            iconLayout.preferredHeight = m_SlotSize.y - 46f;

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(slotObject.transform, false);

            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            LayoutElement labelLayout = labelObject.GetComponent<LayoutElement>();
            labelLayout.preferredHeight = 24f;

            BuildingSlotVisualRefs refs = slotObject.GetComponent<BuildingSlotVisualRefs>();
            refs.button = button;
            refs.label = label;
            refs.icon = icon;
            refs.background = background;
            refs.canvasGroup = slotObject.GetComponent<CanvasGroup>();
            refs.layoutElement = layoutElement;
            return refs;
        }

        void CreateCategoryRuntimeSlot(BuildingPanelCategory category)
        {
            if (m_SlotTemplate == null || m_SlotsRootOverride == null)
            {
                return;
            }

            BuildingSlotVisualRefs runtimeSlot = Instantiate(m_SlotTemplate, m_SlotsRootOverride);
            runtimeSlot.gameObject.name = $"Category_{category}";
            runtimeSlot.gameObject.SetActive(true);
            runtimeSlot.AutoWire();

            if (runtimeSlot.layoutElement != null)
            {
                runtimeSlot.layoutElement.preferredWidth = m_SlotSize.x;
                runtimeSlot.layoutElement.preferredHeight = m_SlotSize.y;
            }

            BuildingPanelCategory capturedCategory = category;
            runtimeSlot.Configure(
                ResolveCategoryLabel(category),
                null,
                true,
                false,
                m_SlotColor,
                m_SelectedSlotColor,
                m_DisabledSlotColor,
                m_TextColor,
                () => ShowCategory(capturedCategory));

            m_RuntimeSlots.Add(runtimeSlot);
            m_RuntimeSlotIndices.Add(-1);
        }

        void CreateMessageRuntimeSlot(string message)
        {
            if (m_SlotTemplate == null || m_SlotsRootOverride == null)
            {
                return;
            }

            BuildingSlotVisualRefs runtimeSlot = Instantiate(m_SlotTemplate, m_SlotsRootOverride);
            runtimeSlot.gameObject.name = "EmptyCategoryMessage";
            runtimeSlot.gameObject.SetActive(true);
            runtimeSlot.AutoWire();

            if (runtimeSlot.layoutElement != null)
            {
                runtimeSlot.layoutElement.preferredWidth = m_SlotSize.x * 1.6f;
                runtimeSlot.layoutElement.preferredHeight = m_SlotSize.y;
            }

            runtimeSlot.Configure(
                message,
                null,
                false,
                false,
                m_SlotColor,
                m_SelectedSlotColor,
                m_DisabledSlotColor,
                m_TextColor,
                null);

            m_RuntimeSlots.Add(runtimeSlot);
            m_RuntimeSlotIndices.Add(-1);
        }

        void CreateRuntimeSlot(BuildingSlotData slot, int index, bool interactable)
        {
            if (m_SlotTemplate == null || m_SlotsRootOverride == null)
            {
                return;
            }

            BuildingSlotVisualRefs runtimeSlot = Instantiate(m_SlotTemplate, m_SlotsRootOverride);
            runtimeSlot.gameObject.name = $"Slot_{index + 1}";
            runtimeSlot.gameObject.SetActive(true);
            runtimeSlot.AutoWire();

            if (runtimeSlot.layoutElement != null)
            {
                runtimeSlot.layoutElement.preferredWidth = m_SlotSize.x;
                runtimeSlot.layoutElement.preferredHeight = m_SlotSize.y;
            }

            int capturedIndex = index;
            runtimeSlot.Configure(
                ResolveSlotName(slot, index),
                ResolveSlotIcon(slot),
                interactable,
                capturedIndex == m_SelectedSlotIndex,
                m_SlotColor,
                m_SelectedSlotColor,
                m_DisabledSlotColor,
                m_TextColor,
                () => SelectSlot(capturedIndex));

            m_RuntimeSlots.Add(runtimeSlot);
            m_RuntimeSlotIndices.Add(index);
        }

        void ClearRuntimeSlots()
        {
            for (int i = 0; i < m_RuntimeSlots.Count; i++)
            {
                if (m_RuntimeSlots[i] != null)
                {
                    DestroyObject(m_RuntimeSlots[i].gameObject);
                }
            }

            m_RuntimeSlots.Clear();
            m_RuntimeSlotIndices.Clear();

            if (m_SlotsRootOverride == null)
            {
                return;
            }

            Transform templateTransform = m_SlotTemplate != null ? m_SlotTemplate.transform : null;
            for (int i = m_SlotsRootOverride.childCount - 1; i >= 0; i--)
            {
                Transform child = m_SlotsRootOverride.GetChild(i);
                if (child == templateTransform)
                {
                    continue;
                }

                if (child.GetComponent<BuildingSlotVisualRefs>() != null)
                {
                    DestroyObject(child.gameObject);
                }
            }
        }

        void UpdateSelectionVisuals()
        {
            for (int i = 0; i < m_RuntimeSlots.Count; i++)
            {
                BuildingSlotVisualRefs slot = m_RuntimeSlots[i];
                if (slot == null)
                {
                    continue;
                }

                int slotIndex = i < m_RuntimeSlotIndices.Count ? m_RuntimeSlotIndices[i] : i;
                if (slotIndex < 0)
                {
                    continue;
                }

                bool selected = slotIndex == m_SelectedSlotIndex;
                bool interactable = slot.button == null || slot.button.interactable;
                slot.Configure(
                    slot.label != null ? slot.label.text : $"Slot {i + 1}",
                    slot.icon != null ? slot.icon.sprite : null,
                    interactable,
                    selected,
                    m_SlotColor,
                    m_SelectedSlotColor,
                    m_DisabledSlotColor,
                    m_TextColor,
                    slot.button != null && interactable ? (() => SelectSlot(slotIndex)) : null);
            }
        }

        void RefreshScrollState()
        {
            if (m_SlotScrollRect == null || m_SlotsRootOverride == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(m_SlotsRootOverride);

            RectTransform viewport = m_SlotScrollRect.viewport != null
                ? m_SlotScrollRect.viewport
                : m_SlotScrollRect.GetComponent<RectTransform>();

            if (viewport == null)
            {
                return;
            }

            bool shouldScroll = m_SlotsRootOverride.rect.width > viewport.rect.width + 0.5f;
            m_SlotScrollRect.horizontal = shouldScroll;

            if (!shouldScroll)
            {
                m_SlotsRootOverride.anchoredPosition = Vector2.zero;
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

        bool IsSlotVisibleInCurrentCategory(BuildingSlotData slot)
        {
            return slot.category == m_CurrentCategory;
        }

        string ResolveCategoryLabel(BuildingPanelCategory category)
        {
            return category switch
            {
                BuildingPanelCategory.Housing => string.IsNullOrWhiteSpace(m_HousingCategoryLabel) ? "Viviendas" : m_HousingCategoryLabel,
                BuildingPanelCategory.Resources => string.IsNullOrWhiteSpace(m_ResourcesCategoryLabel) ? "Recursos" : m_ResourcesCategoryLabel,
                BuildingPanelCategory.Decoration => string.IsNullOrWhiteSpace(m_DecorationCategoryLabel) ? "Decoracion" : m_DecorationCategoryLabel,
                _ => category.ToString(),
            };
        }

        BuildingPanelCategory ResolvePrefabCategory(GameObject prefab)
        {
            if (prefab != null &&
                prefab.TryGetComponent(out BuildingDefinitionAuthoring authoring) &&
                authoring.Definition != null)
            {
                return FromSimulationCategory(authoring.Definition.Category);
            }

            return BuildingPanelCategory.Housing;
        }

        public static BuildingPanelCategory FromSimulationCategory(BuildingSimulationCategory category)
        {
            return category switch
            {
                BuildingSimulationCategory.People => BuildingPanelCategory.Housing,
                BuildingSimulationCategory.Decoration => BuildingPanelCategory.Decoration,
                _ => BuildingPanelCategory.Resources,
            };
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

            Sprite previewSprite = Sprite.Create(
                previewTexture,
                new Rect(0f, 0f, previewTexture.width, previewTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            previewSprite.name = $"Preview_{slot.buildingPrefab.name}";
            m_EditorPrefabIconCache[prefabId] = previewSprite;
            return previewSprite;
#else
            return null;
#endif
        }

        Canvas CreateFallbackCanvas()
        {
            GameObject canvasObject = new(
                "Building Panel Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 20f;
            scaler.referencePixelsPerUnit = 100f;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1400f, 900f);
            rect.localScale = Vector3.one * Mathf.Max(0.0001f, m_FallbackCanvasScale);

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                rect.position = mainCamera.transform.position + mainCamera.transform.forward * Mathf.Max(0.5f, m_FallbackCanvasDistance);
                rect.rotation = mainCamera.transform.rotation;
            }

            return canvas;
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
            return mainCamera != null ? mainCamera.transform : null;
        }

        void FollowCanvasToPlayer()
        {
            if (!Application.isPlaying ||
                !m_FollowPlayerInWorldSpace ||
                m_TargetCanvas == null ||
                m_TargetCanvas.renderMode != RenderMode.WorldSpace)
            {
                return;
            }

            RectTransform canvasRect = m_TargetCanvas.GetComponent<RectTransform>();
            Transform followTarget = ResolveFollowTarget();
            if (canvasRect == null || followTarget == null)
            {
                return;
            }

            UpdateFollowOffsets(canvasRect, followTarget);
            canvasRect.position = followTarget.TransformPoint(m_RuntimeFollowLocalPositionOffset);
            canvasRect.rotation = followTarget.rotation * m_RuntimeFollowLocalRotationOffset;
        }

        void UpdateFollowOffsets(RectTransform canvasRect, Transform followTarget)
        {
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

        void DestroyObject(GameObject target)
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

#if UNITY_EDITOR
        void OnValidate()
        {
            m_EmptySlotCount = Mathf.Max(1, m_EmptySlotCount);
            m_SlotSpacing = Mathf.Max(0f, m_SlotSpacing);
            m_PanelSize.x = Mathf.Max(320f, m_PanelSize.x);
            m_PanelSize.y = Mathf.Max(120f, m_PanelSize.y);
            float slotSide = Mathf.Max(72f, Mathf.Min(m_SlotSize.x, m_SlotSize.y));
            m_SlotSize = new Vector2(slotSide, slotSide);
            m_FallbackCanvasScale = Mathf.Max(0.0001f, m_FallbackCanvasScale);
            m_FallbackCanvasDistance = Mathf.Max(0.5f, m_FallbackCanvasDistance);
        }
#endif
    }
}
