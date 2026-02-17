using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityBuilderVR
{
    public class BuildingSlotVisualRefs : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public Image icon;
        public Image background;

        void Reset()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
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
                    if (images[i] != GetComponent<Image>())
                    {
                        icon = images[i];
                        break;
                    }
                }
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }
        }
    }
}
