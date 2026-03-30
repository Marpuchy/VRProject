using System;
using System.Text;
using TMPro;
using UnityEngine;

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
        [Header("VR HUD Follow")]
        [SerializeField] bool m_PinHudToPlayerEyes = true;
        [SerializeField] bool m_UseWorldSpaceCanvas = true;
        [SerializeField] Vector2 m_WorldCanvasSize = new(1200f, 380f);
        [SerializeField, Min(0.0001f)] float m_WorldCanvasScale = 0.0012f;
        [SerializeField] Vector3 m_EyeLocalPositionOffset = new(0f, -0.1f, 0.62f);
        [SerializeField] Vector3 m_EyeLocalEulerOffset = Vector3.zero;

        readonly StringBuilder m_StringBuilder = new();
        Canvas m_HudCanvas;
        Camera m_MainCamera;
        Transform m_EyeAnchor;

        void Awake()
        {
            ResolveSimulationController();
            ResolveMissingTextReferences();
            NormalizeTextVisibilityAndLayout();
            TryAttachHudToPlayerEyes();
        }

        void OnEnable()
        {
            ResolveSimulationController();
            ResolveMissingTextReferences();
            NormalizeTextVisibilityAndLayout();
            TryAttachHudToPlayerEyes();

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

        void LateUpdate()
        {
            TryAttachHudToPlayerEyes();
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
                m_MoneyText.text = $"Money: {snapshot.Money} ({moneyDeltaPrefix}{snapshot.NetMoneyDelta}/tick)";
            }

            if (m_PopulationText != null)
            {
                m_PopulationText.text = $"Population: {snapshot.Population}/{snapshot.HousingCapacity}";
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

                m_HappinessText.text = $"Happiness: {currentPercent:0}% (Target {targetPercent:0}%)";
            }

            if (m_LevelText != null)
            {
                string nextLevel = snapshot.ExperienceToNextLevel > 0
                    ? $" (+{snapshot.ExperienceToNextLevel} XP)"
                    : string.Empty;
                m_LevelText.text = $"Level: {snapshot.Level} | XP: {snapshot.Experience}{nextLevel}";
            }

            if (m_ResourcesText != null)
            {
                m_StringBuilder.Clear();

                bool showMoneyInResources = !IsUsableLabel(m_MoneyText);
                bool showLevelInResources = !IsUsableLabel(m_LevelText);
                if (showMoneyInResources)
                {
                    m_StringBuilder.AppendLine($"Money: {snapshot.Money}");
                }

                if (showLevelInResources)
                {
                    string nextLevel = snapshot.ExperienceToNextLevel > 0
                        ? $" (+{snapshot.ExperienceToNextLevel} XP)"
                        : string.Empty;
                    m_StringBuilder.AppendLine($"Level: {snapshot.Level} | XP: {snapshot.Experience}{nextLevel}");
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
                        m_StringBuilder.Append(" providers | no required buildings");
                    }
                    else
                    {
                        m_StringBuilder.Append(resource.CoveredBuildings);
                        m_StringBuilder.Append('/');
                        m_StringBuilder.Append(resource.RequiredBuildings);
                        m_StringBuilder.Append(" buildings covered (");
                        m_StringBuilder.Append((resource.ClampedCoverage * 100f).ToString("0"));
                        m_StringBuilder.Append("%)");
                    }
                }

                m_ResourcesText.text = m_StringBuilder.ToString();
            }
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

            m_HudCanvas ??= GetComponentInParent<Canvas>();
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

            if (!m_ClampTextInsideParentRect)
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
