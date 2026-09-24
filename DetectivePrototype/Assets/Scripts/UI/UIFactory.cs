using Detective.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 실행 중에 uGUI 요소를 코드로 만드는 도우미.
    /// 패널 배치를 에디터에서 손으로 하지 않기 위해(§18-9) 각 UI 컴포넌트가 Awake에서 자기 자식을 직접 만든다.
    ///
    /// 연출 방향 "사건 파일": 어두운 저택 위에 바랜 종이 패널을 올리고, 글은 검은 잉크, 강조는 붉은 잉크로 쓴다.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>종이 패널에 곱하는 색(질감은 ParchmentSprite).</summary>
        public static readonly Color PanelColor = new Color(0.93f, 0.87f, 0.74f, 0.97f);

        /// <summary>종이 위 본문 잉크.</summary>
        public static readonly Color Ink = new Color(0.16f, 0.13f, 0.10f);

        /// <summary>종이 위 흐린 잉크(보조 설명).</summary>
        public static readonly Color MutedColor = new Color(0.42f, 0.37f, 0.32f);

        /// <summary>붉은 잉크(강조·선택).</summary>
        public static readonly Color AccentColor = new Color(0.54f, 0.18f, 0.16f);

        /// <summary>어두운 배경(저택·전체 화면) 위에 얹는 크림색 글자.</summary>
        public static readonly Color Cream = new Color(0.94f, 0.90f, 0.80f);

        /// <summary>어두운 배경 위 흐린 글자.</summary>
        public static readonly Color CreamMuted = new Color(0.66f, 0.62f, 0.54f);

        /// <summary>전체 화면(시작·결과)용 거의 검은 배경.</summary>
        public static readonly Color Night = new Color(0.03f, 0.025f, 0.02f, 0.98f);

        /// <summary>종이 패널 안에서 칸을 나눌 때 쓰는 옅은 잉크 면.</summary>
        public static readonly Color InkWash = new Color(0.16f, 0.13f, 0.10f, 0.06f);

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

        /// <summary>바랜 종이 질감의 패널 배경. 가장자리에 가는 잉크 테두리를 두른다.</summary>
        public static Image AddPaper(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtLibrary.Instance.ParchmentSprite();
            image.type = Image.Type.Simple;
            image.color = PanelColor;
            image.raycastTarget = false;

            // 종이 가장자리: 얇은 잉크 선 + 안쪽 여백선(서류 양식처럼).
            AddFrame(rect, 0f, 2f, new Color(0.16f, 0.13f, 0.10f, 0.85f));
            AddFrame(rect, 8f, 1f, new Color(0.16f, 0.13f, 0.10f, 0.25f));
            return image;
        }

        /// <summary>사각 테두리 선 4개.</summary>
        public static void AddFrame(RectTransform rect, float inset, float thickness, Color color)
        {
            AddImage(CreateRect("FrameTop", rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(inset, -inset - thickness), new Vector2(-inset, -inset)), color);
            AddImage(CreateRect("FrameBottom", rect, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(inset, inset), new Vector2(-inset, inset + thickness)), color);
            AddImage(CreateRect("FrameLeft", rect, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(inset, inset), new Vector2(inset + thickness, -inset)), color);
            AddImage(CreateRect("FrameRight", rect, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-inset - thickness, inset), new Vector2(-inset, -inset)), color);
        }

        /// <summary>화면 가장자리를 어둡게 하는 비네팅. 캔버스 맨 아래(첫 자식)에 둔다.</summary>
        public static Image AddVignette(Transform canvas)
        {
            RectTransform rect = CreateStretch("Vignette", canvas, 0f);
            rect.SetAsFirstSibling();
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtLibrary.Instance.VignetteSprite();
            image.type = Image.Type.Simple;
            image.color = Color.white;
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
            text.lineSpacing = 1.15f;
            text.text = string.Empty;
            return text;
        }

        /// <summary>종이 패널 + 안쪽 여백을 가진 잉크 글.</summary>
        public static Text CreateTextPanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            int fontSize, TextAnchor alignment, out RectTransform panel)
        {
            panel = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            AddPaper(panel);
            RectTransform inner = CreateStretch("Text", panel, 24f);
            return AddText(inner, fontSize, alignment, Ink);
        }

        /// <summary>액자에 넣은 그림. 초상화·단서 이미지용. 그림은 액자 안에 맞춰 잘리지 않고 들어간다.</summary>
        public static Image AddFramedImage(RectTransform rect, Sprite sprite)
        {
            AddImage(rect, new Color(0.10f, 0.08f, 0.06f, 1f));
            AddFrame(rect, 0f, 3f, new Color(0.35f, 0.27f, 0.15f, 1f)); // 낡은 금박 액자
            RectTransform inner = CreateStretch("Picture", rect, 6f);
            var image = inner.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = new Color(0.92f, 0.86f, 0.76f, 1f); // 살짝 세피아
            image.raycastTarget = false;
            return image;
        }

        /// <summary>리치 텍스트 색 태그.</summary>
        public static string Colorize(string text, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
        }
    }
}
