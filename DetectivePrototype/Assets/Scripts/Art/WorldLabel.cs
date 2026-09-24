using Detective.UI;
using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// 월드 공간 글자표(TextMesh). 한글 폰트는 씬에 직렬화되지 않으므로 실행 시 만든다.
    /// 인물 이름표, 플레이어 "탐정" 표시, 바닥의 방 이름에 쓴다.
    /// </summary>
    public class WorldLabel : MonoBehaviour
    {
        public string text = string.Empty;
        public Vector3 localOffset = new Vector3(0f, 0.85f, 0f);
        public float characterSize = 0.045f;
        public Color color = new Color(0.94f, 0.90f, 0.80f);
        public int sortingOrder = 60;

        private TextMesh _mesh;

        private void Awake()
        {
            _mesh = Create(transform, "Label", localOffset, characterSize, color, sortingOrder);
            _mesh.text = text;
        }

        public void SetText(string value)
        {
            text = value;
            if (_mesh != null) _mesh.text = value ?? string.Empty;
        }

        /// <summary>TextMesh 하나를 만든다. NPCController도 같은 모양의 이름표를 쓴다.</summary>
        public static TextMesh Create(Transform parent, string name, Vector3 localPosition, float characterSize, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var label = go.AddComponent<TextMesh>();
            Font font = UIFontApplier.GetFont();
            label.font = font;
            label.fontSize = 64;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = sortingOrder;
            return label;
        }
    }
}
