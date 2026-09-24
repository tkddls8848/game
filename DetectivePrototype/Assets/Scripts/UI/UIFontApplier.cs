using Detective.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// UI 폰트 결정. 순서: art.json의 폰트(저장소에 포함된 Noto Serif KR) → OS 한글 폰트 → 내장 폰트.
    /// 씬에 저장할 수 있는 내장 폰트(LegacyRuntime.ttf)에는 한글 글리프가 없으므로 실행 시점에 바꿔 끼운다.
    /// 실행 중에 코드로 만드는 UI(UIFactory)와 NPC 이름표도 같은 폰트를 쓰도록 결과를 정적으로 캐시한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class UIFontApplier : MonoBehaviour
    {
        public static readonly string[] PreferredFonts =
            { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "NanumGothic", "Noto Sans CJK KR", "Noto Sans KR", "Arial Unicode MS" };

        public const int DynamicFontSize = 32;

        private static Font _cached;
        private static bool _resolved;

        private void Awake()
        {
            Font font = GetFont();

            Text[] labels = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].font = font;
            }
        }

        /// <summary>한글을 표시할 수 있는 폰트. 못 찾으면 내장 폰트(한글은 깨진다).</summary>
        public static Font GetFont()
        {
            if (_resolved && _cached != null) return _cached;
            _resolved = true;

            _cached = ArtLibrary.Instance.LoadUiFont();
            if (_cached == null) _cached = ResolveOsFont();
            if (_cached == null)
            {
                Debug.LogWarning("[UIFontApplier] 한글 폰트를 찾지 못했다. 기본 폰트로 표시된다.");
                _cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _cached;
        }

        private static Font ResolveOsFont()
        {
            string[] installed = Font.GetOSInstalledFontNames();
            if (installed == null) return null;

            for (int i = 0; i < PreferredFonts.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (installed[j] != PreferredFonts[i]) continue;

                    Font font = Font.CreateDynamicFontFromOSFont(PreferredFonts[i], DynamicFontSize);
                    if (font != null) return font;
                }
            }
            return null;
        }
    }
}
