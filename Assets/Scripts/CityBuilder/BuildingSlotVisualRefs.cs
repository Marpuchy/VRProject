using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    public class BuildingSlotVisualRefs : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public Image icon;
        public Image background;
        public Image selectionFrame;
        public CanvasGroup canvasGroup;
        public LayoutElement layoutElement;

        public void AutoWire()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }

            if (icon == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] != background)
                    {
                        icon = images[i];
                        break;
                    }
                }
            }
        }

        public void Configure(
            string labelText,
            Sprite iconSprite,
            bool interactable,
            bool selected,
            Color normalColor,
            Color selectedColor,
            Color disabledColor,
            Color textColor,
            UnityAction onClick)
        {
            AutoWire();

            if (label != null)
            {
                label.text = labelText;
                label.color = textColor;
            }

            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
                icon.color = Color.white;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = interactable;

                if (interactable && onClick != null)
                {
                    button.onClick.AddListener(onClick);
                }
            }

            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = false;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = interactable ? 1f : 0.4f;
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }

            if (background != null)
            {
                background.color = interactable
                    ? (selected ? selectedColor : normalColor)
                    : disabledColor;
            }

            if (selectionFrame != null)
            {
                selectionFrame.enabled = selected;
            }
        }

        void Reset()
        {
            AutoWire();
        }
    }
}
