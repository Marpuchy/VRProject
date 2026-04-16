using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class CityStatsHudPresenter : MonoBehaviour
    {
        [SerializeField] CitySimulationController m_SimulationController;
        [SerializeField] CitySimulationSnapshotEventChannelSO m_CityStateChangedEvent;
        [SerializeField] TMP_Text m_MoneyText;
        [SerializeField] TMP_Text m_PopulationText;
        [SerializeField] TMP_Text m_HappinessText;
        [SerializeField] TMP_Text m_LevelText;
        [SerializeField] TMP_Text m_ResourcesText;
        [SerializeField] bool m_AutoResolveMissingTextReferences = true;
        [SerializeField] bool m_ClampTextInsideParentRect = true;
        [SerializeField, Min(0f)] float m_TextClampPadding = 14f;

        [Header("Existing UI Layout")]
        [SerializeField] Canvas m_TargetCanvas;
        [SerializeField] RectTransform m_PanelRootOverride;
        [SerializeField] bool m_OrganizeExistingPanelOnAwake = true;
        [SerializeField] bool m_CreateMissingRowsIfNeeded = true;
        [SerializeField] bool m_CreateMissingIconPlaceholders = true;
        [SerializeField] bool m_ApplyPanelBackground;
        [SerializeField] Color m_PanelColor = new(0.08f, 0.1f, 0.13f, 0.86f);
        [SerializeField] Color m_TextColor = new(0.95f, 0.96f, 0.98f, 1f);
        [SerializeField] RectOffset m_PanelPadding;
        [SerializeField, Min(0f)] float m_RowSpacing = 6f;
        [SerializeField, Min(18f)] float m_RowHeight = 34f;
        [SerializeField, Min(48f)] float m_ResourcesRowHeight = 130f;
        [SerializeField] Vector2 m_IconSize = new(28f, 28f);

        [Header("Icon References")]
        [SerializeField] Image m_MoneyIcon;
        [SerializeField] Image m_HappinessIcon;
        [SerializeField] Image m_PopulationIcon;
        [SerializeField] Image m_LevelIcon;
        [SerializeField] Image m_ResourcesIcon;

        [Header("Icon Sprites")]
        [SerializeField] Sprite m_MoneyIconSprite;
        [SerializeField] Sprite m_HappinessIconSprite;
        [SerializeField] Sprite m_PopulationIconSprite;
        [SerializeField] Sprite m_LevelIconSprite;
        [SerializeField] Sprite m_ResourcesIconSprite;
        [SerializeField] Color m_IconTint = Color.white;
        [SerializeField] Color m_EmptyIconTint = new(1f, 1f, 1f, 0.35f);

        [Header("VR HUD Follow")]
        [SerializeField] bool m_PinHudToPlayerEyes = true;
        [SerializeField] bool m_UseWorldSpaceCanvas = true;
        [SerializeField] Vector2 m_WorldCanvasSize = new(1200f, 380f);
        [SerializeField, Min(0.0001f)] float m_WorldCanvasScale = 0.0012f;
        [SerializeField] Vector3 m_EyeLocalPositionOffset = new(0f, -0.1f, 0.62f);
        [SerializeField] Vector3 m_EyeLocalEulerOffset = Vector3.zero;

        Canvas m_HudCanvas;
        Camera m_MainCamera;
        Transform m_EyeAnchor;

        readonly StringBuilder m_StringBuilder = new();

        void Awake()
        {
            ResolveSimulationController();
            ResolveMissingTextReferences();
            if (m_OrganizeExistingPanelOnAwake)
            {
                EnsureExistingPanelLayout();
            }

            NormalizeTextVisibilityAndLayout();
            TryAttachHudToPlayerEyes();
        }

        void OnEnable()
        {
            ResolveSimulationController();
            ResolveMissingTextReferences();
            if (m_OrganizeExistingPanelOnAwake)
            {
                EnsureExistingPanelLayout();
            }

            NormalizeTextVisibilityAndLayout();

            if (m_CityStateChangedEvent != null)
            {
                m_CityStateChangedEvent.EventRaised += HandleSnapshot;
            }
            else if (m_SimulationController != null)
            {
                m_SimulationController.StateChanged += HandleSnapshot;
            }

            m_SimulationController?.PublishCurrentState();
        }

        void OnDisable()
        {
            if (m_CityStateChangedEvent != null)
            {
                m_CityStateChangedEvent.EventRaised -= HandleSnapshot;
            }
            else if (m_SimulationController != null)
            {
                m_SimulationController.StateChanged -= HandleSnapshot;
            }
        }

        void HandleSnapshot(CitySimulationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (m_MoneyText != null)
            {
                string moneyDeltaPrefix = snapshot.NetMoneyDelta > 0 ? "+" : string.Empty;
                m_MoneyText.text = $"Dinero: {snapshot.Money} ({moneyDeltaPrefix}{snapshot.NetMoneyDelta}/tick)";
            }

            if (m_PopulationText != null)
            {
                m_PopulationText.text = $"Poblacion: {snapshot.Population}/{snapshot.HousingCapacity}";
            }

            if (m_HappinessText != null)
            {
                float maxHappiness = 100f;
                if (m_SimulationController != null && m_SimulationController.Balance != null)
                {
                    maxHappiness = m_SimulationController.Balance.MaxHappiness;
                }

                float currentPercent = maxHappiness > 0.001f
                    ? Mathf.Clamp01(snapshot.Happiness / maxHappiness) * 100f
                    : 0f;
                float targetPercent = maxHappiness > 0.001f
                    ? Mathf.Clamp01(snapshot.TargetHappiness / maxHappiness) * 100f
                    : 0f;

                m_HappinessText.text = $"Felicidad: {currentPercent:0}% (objetivo {targetPercent:0}%)";
            }

            if (m_LevelText != null)
            {
                string nextLevel = snapshot.ExperienceToNextLevel > 0
                    ? $" (+{snapshot.ExperienceToNextLevel} XP)"
                    : string.Empty;
                m_LevelText.text = $"Nivel: {snapshot.Level} | XP: {snapshot.Experience}{nextLevel}";
            }

            if (m_ResourcesText != null)
            {
                m_StringBuilder.Clear();

                bool showMoneyInResources = !IsUsableLabel(m_MoneyText);
                bool showLevelInResources = !IsUsableLabel(m_LevelText);
                if (showMoneyInResources)
                {
                    m_StringBuilder.AppendLine($"Dinero: {snapshot.Money}");
                }

                if (showLevelInResources)
                {
                    string nextLevel = snapshot.ExperienceToNextLevel > 0
                        ? $" (+{snapshot.ExperienceToNextLevel} XP)"
                        : string.Empty;
                    m_StringBuilder.AppendLine($"Nivel: {snapshot.Level} | XP: {snapshot.Experience}{nextLevel}");
                }

                if (snapshot.Resources.Count == 0)
                {
                    if (m_StringBuilder.Length > 0)
                    {
                        m_StringBuilder.AppendLine();
                    }

                    m_StringBuilder.Append("Recursos: sin datos");
                }

                for (int i = 0; i < snapshot.Resources.Count; i++)
                {
                    CityResourceSnapshot resource = snapshot.Resources[i];
                    if (resource == null)
                    {
                        continue;
                    }

                    if (m_StringBuilder.Length > 0)
                    {
                        m_StringBuilder.AppendLine();
                    }

                    string resourceName = resource.ResourceType != null
                        ? resource.ResourceType.DisplayName
                        : $"Resource {i + 1}";
                    m_StringBuilder.Append(resourceName);
                    m_StringBuilder.Append(": ");

                    if (!resource.HasRequirements)
                    {
                        m_StringBuilder.Append(resource.ProviderCount);
                        m_StringBuilder.Append(" proveedores | sin demanda");
                    }
                    else
                    {
                        m_StringBuilder.Append(resource.CoveredBuildings);
                        m_StringBuilder.Append('/');
                        m_StringBuilder.Append(resource.RequiredBuildings);
                        m_StringBuilder.Append(" edificios cubiertos (");
                        m_StringBuilder.Append((resource.ClampedCoverage * 100f).ToString("0"));
                        m_StringBuilder.Append("%)");
                    }
                }

                m_ResourcesText.text = m_StringBuilder.ToString();
            }
        }

        void EnsureExistingPanelLayout()
        {
            m_TargetCanvas ??= GetComponentInParent<Canvas>(true);

            RectTransform panelRoot = ResolveExistingPanelRoot();
            if (panelRoot == null)
            {
                return;
            }

            m_PanelRootOverride = panelRoot;
            ConfigureExistingPanelRoot(panelRoot);

            m_MoneyText = EnsureStatRow(panelRoot, m_MoneyText, ref m_MoneyIcon, "MoneyRow", "MoneyLabel", m_MoneyIconSprite, 0, false);
            m_HappinessText = EnsureStatRow(panelRoot, m_HappinessText, ref m_HappinessIcon, "HappinessRow", "HappinessLabel", m_HappinessIconSprite, 1, false);
            m_PopulationText = EnsureStatRow(panelRoot, m_PopulationText, ref m_PopulationIcon, "PopulationRow", "PopulationLabel", m_PopulationIconSprite, 2, false);
            m_LevelText = EnsureStatRow(panelRoot, m_LevelText, ref m_LevelIcon, "LevelRow", "LevelLabel", m_LevelIconSprite, 3, false);
            m_ResourcesText = EnsureStatRow(panelRoot, m_ResourcesText, ref m_ResourcesIcon, "ResourcesRow", "ResourcesLabel", m_ResourcesIconSprite, 4, true);
        }

        RectTransform ResolveExistingPanelRoot()
        {
            if (m_PanelRootOverride != null)
            {
                return m_PanelRootOverride;
            }

            if (TryGetComponent(out RectTransform ownRect))
            {
                return ownRect;
            }

            TMP_Text firstText = m_MoneyText != null ? m_MoneyText :
                m_HappinessText != null ? m_HappinessText :
                m_PopulationText != null ? m_PopulationText :
                m_LevelText != null ? m_LevelText :
                m_ResourcesText;

            return firstText != null ? firstText.rectTransform.parent as RectTransform : null;
        }

        void ConfigureExistingPanelRoot(RectTransform panelRoot)
        {
            if (m_ApplyPanelBackground)
            {
                if (!panelRoot.TryGetComponent(out Image panelImage))
                {
                    panelImage = panelRoot.gameObject.AddComponent<Image>();
                }

                panelImage.color = m_PanelColor;
                panelImage.raycastTarget = false;
            }

            if (!panelRoot.TryGetComponent(out VerticalLayoutGroup layout))
            {
                layout = panelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = m_PanelPadding ?? new RectOffset(18, 18, 14, 14);
            layout.spacing = m_RowSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        TMP_Text EnsureStatRow(
            RectTransform panelRoot,
            TMP_Text label,
            ref Image icon,
            string rowName,
            string fallbackLabelName,
            Sprite iconSprite,
            int siblingIndex,
            bool multiline)
        {
            if (label == null)
            {
                label = FindChildText(panelRoot, fallbackLabelName);
            }

            if (label == null && !m_CreateMissingRowsIfNeeded)
            {
                return null;
            }

            RectTransform row = ResolveOrCreateRow(panelRoot, label, rowName, siblingIndex);
            if (row == null)
            {
                return label;
            }

            ConfigureRow(row, multiline);

            if (label == null)
            {
                GameObject labelObject = new(fallbackLabelName, typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(row, false);
                label = labelObject.GetComponent<TMP_Text>();
            }
            else if (label.transform.parent != row)
            {
                label.transform.SetParent(row, false);
            }

            icon = EnsureIcon(row, icon, iconSprite);
            ConfigureLabel(label, multiline);
            row.SetSiblingIndex(siblingIndex);
            icon?.transform.SetAsFirstSibling();
            label.transform.SetAsLastSibling();
            return label;
        }

        RectTransform ResolveOrCreateRow(RectTransform panelRoot, TMP_Text label, string rowName, int siblingIndex)
        {
            Transform existingRow = panelRoot.Find(rowName);
            if (existingRow is RectTransform existingRowRect)
            {
                return existingRowRect;
            }

            if (label != null && label.transform.parent is RectTransform currentParent && currentParent != panelRoot)
            {
                currentParent.gameObject.name = rowName;
                return currentParent;
            }

            GameObject rowObject = new(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(panelRoot, false);
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.SetSiblingIndex(siblingIndex);
            return row;
        }

        void ConfigureRow(RectTransform row, bool multiline)
        {
            if (!row.TryGetComponent(out HorizontalLayoutGroup layout))
            {
                layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (!row.TryGetComponent(out LayoutElement layoutElement))
            {
                layoutElement = row.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = multiline ? m_ResourcesRowHeight : m_RowHeight;
            layoutElement.flexibleWidth = 1f;
        }

        Image EnsureIcon(RectTransform row, Image currentIcon, Sprite iconSprite)
        {
            if (currentIcon == null)
            {
                Transform existingIcon = row.Find("Icon");
                if (existingIcon != null)
                {
                    currentIcon = existingIcon.GetComponent<Image>();
                }
            }

            if (currentIcon == null && m_CreateMissingIconPlaceholders)
            {
                GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObject.transform.SetParent(row, false);
                currentIcon = iconObject.GetComponent<Image>();
            }

            if (currentIcon == null)
            {
                return null;
            }

            currentIcon.sprite = iconSprite;
            currentIcon.preserveAspect = true;
            currentIcon.color = iconSprite != null ? m_IconTint : m_EmptyIconTint;
            currentIcon.raycastTarget = false;

            if (!currentIcon.TryGetComponent(out LayoutElement layoutElement))
            {
                layoutElement = currentIcon.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = Mathf.Max(1f, m_IconSize.x);
            layoutElement.preferredHeight = Mathf.Max(1f, m_IconSize.y);
            layoutElement.minWidth = layoutElement.preferredWidth;
            layoutElement.minHeight = layoutElement.preferredHeight;

            RectTransform rect = currentIcon.rectTransform;
            rect.sizeDelta = m_IconSize;
            return currentIcon;
        }

        void ConfigureLabel(TMP_Text label, bool multiline)
        {
            if (label == null)
            {
                return;
            }

            label.color = m_TextColor;
            label.fontSize = multiline ? 20f : 24f;
            label.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
            label.textWrappingMode = multiline ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = multiline ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            if (!label.TryGetComponent(out LayoutElement layoutElement))
            {
                layoutElement = label.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = multiline ? m_ResourcesRowHeight : m_RowHeight;
            layoutElement.flexibleWidth = 1f;
        }

        static TMP_Text FindChildText(RectTransform parent, string objectName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && string.Equals(text.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        void ResolveSimulationController()
        {
            if (m_SimulationController == null)
            {
                m_SimulationController = FindFirstObjectByType<CitySimulationController>();
            }
        }

        void ResolveMissingTextReferences()
        {
            if (!m_AutoResolveMissingTextReferences)
            {
                return;
            }

            m_MoneyText ??= FindTextByName("money");
            m_PopulationText ??= FindTextByName("population");
            m_HappinessText ??= FindTextByName("happiness");
            m_LevelText ??= FindTextByName("level", "xp");
            m_ResourcesText ??= FindTextByName("resources", "resource");
        }

        void TryAttachHudToPlayerEyes()
        {
            if (!Application.isPlaying || !m_PinHudToPlayerEyes)
            {
                return;
            }

            m_HudCanvas ??= m_TargetCanvas != null ? m_TargetCanvas : GetComponentInParent<Canvas>();
            if (m_HudCanvas == null)
            {
                return;
            }

            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
            }

            if (m_MainCamera == null)
            {
                return;
            }

            m_EyeAnchor = m_MainCamera.transform;
            if (m_UseWorldSpaceCanvas)
            {
                m_HudCanvas.renderMode = RenderMode.WorldSpace;
            }
            else
            {
                m_HudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            m_HudCanvas.worldCamera = m_MainCamera;

            RectTransform canvasRect = m_HudCanvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            if (canvasRect.parent != m_EyeAnchor)
            {
                canvasRect.SetParent(m_EyeAnchor, false);
            }

            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = m_WorldCanvasSize;
            canvasRect.localPosition = m_EyeLocalPositionOffset;
            canvasRect.localRotation = Quaternion.Euler(m_EyeLocalEulerOffset);
            canvasRect.localScale = Vector3.one * m_WorldCanvasScale;
        }

        TMP_Text FindTextByName(params string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return null;
            }

            TMP_Text[] localTexts = GetComponentsInChildren<TMP_Text>(true);
            TMP_Text match = FindMatch(localTexts, tokens);
            if (match != null)
            {
                return match;
            }

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                return null;
            }

            TMP_Text[] canvasTexts = parentCanvas.GetComponentsInChildren<TMP_Text>(true);
            return FindMatch(canvasTexts, tokens);
        }

        static TMP_Text FindMatch(TMP_Text[] candidates, string[] tokens)
        {
            if (candidates == null || tokens == null)
            {
                return null;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                TMP_Text text = candidates[i];
                if (text == null)
                {
                    continue;
                }

                string name = text.gameObject.name;
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    string token = tokens[tokenIndex];
                    if (!string.IsNullOrWhiteSpace(token) &&
                        name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        void NormalizeTextVisibilityAndLayout()
        {
            EnsureVisible(m_MoneyText);
            EnsureVisible(m_PopulationText);
            EnsureVisible(m_HappinessText);
            EnsureVisible(m_LevelText);
            EnsureVisible(m_ResourcesText);

            if (!m_ClampTextInsideParentRect || m_OrganizeExistingPanelOnAwake)
            {
                return;
            }

            ClampToParent(m_MoneyText);
            ClampToParent(m_PopulationText);
            ClampToParent(m_HappinessText);
            ClampToParent(m_LevelText);
            ClampToParent(m_ResourcesText);
        }

        static void EnsureVisible(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            if (!label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(true);
            }

            label.enabled = true;
            Color color = label.color;
            if (color.a <= 0.01f)
            {
                color.a = 1f;
                label.color = color;
            }
        }

        void ClampToParent(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            RectTransform rect = label.rectTransform;
            RectTransform parentRect = rect.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            Rect parentBounds = parentRect.rect;
            Rect labelBounds = rect.rect;
            float maxX = Mathf.Max(0f, (parentBounds.width * 0.5f) - (labelBounds.width * 0.5f) - m_TextClampPadding);
            float maxY = Mathf.Max(0f, (parentBounds.height * 0.5f) - (labelBounds.height * 0.5f) - m_TextClampPadding);

            Vector2 clamped = new(
                Mathf.Clamp(rect.anchoredPosition.x, -maxX, maxX),
                Mathf.Clamp(rect.anchoredPosition.y, -maxY, maxY));

            if ((rect.anchoredPosition - clamped).sqrMagnitude > 0.0001f)
            {
                rect.anchoredPosition = clamped;
            }
        }

        static bool IsUsableLabel(TMP_Text label)
        {
            return label != null && label.enabled && label.gameObject.activeInHierarchy;
        }
    }
}
