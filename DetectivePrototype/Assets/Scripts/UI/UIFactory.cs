using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 실행 중에 uGUI 요소를 코드로 만드는 도우미.
    /// 패널 배치를 에디터에서 손으로 하지 않기 위해(§18-9) 각 UI 컴포넌트가 Awake에서 자기 자식을 직접 만든다.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.94f);
        public static readonly Color AccentColor = new Color(1f, 0.86f, 0.55f);
        public static readonly Color MutedColor = new Color(0.62f, 0.66f, 0.72f);

        public static RectTransform CreateRect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>부모를 꽉 채우고 안쪽으로 inset만큼 들어간 사각형.</summary>
        public static RectTransform CreateStretch(string name, Transform parent, float inset)
        {
            return CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
        }

        public static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text AddText(RectTransform rect, int fontSize, TextAnchor alignment, Color color)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UIFontApplier.GetFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.1f;
            text.text = string.Empty;
            return text;
        }

        /// <summary>반투명 배경 + 안쪽 여백을 가진 텍스트 패널.</summary>
        public static Text CreateTextPanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            int fontSize, TextAnchor alignment, out RectTransform panel)
        {
            panel = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            AddImage(panel, PanelColor);
            RectTransform inner = CreateStretch("Text", panel, 20f);
            return AddText(inner, fontSize, alignment, Color.white);
        }

        /// <summary>리치 텍스트 색 태그.</summary>
        public static string Colorize(string text, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
        }
    }
}
