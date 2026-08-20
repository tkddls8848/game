using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 한글이 네모(두부)로 보이는 것을 막는다.
    /// 씬에 저장할 수 있는 내장 폰트(LegacyRuntime.ttf)에는 한글 글리프가 없으므로,
    /// 실행 시점에 OS 폰트로 교체한다. OS 폰트 참조는 직렬화할 수 없어서 코드로 처리한다.
    /// </summary>
    public class UIFontApplier : MonoBehaviour
    {
        [Tooltip("앞에서부터 설치되어 있는 첫 번째 폰트를 쓴다.")]
        public string[] preferredFonts = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Noto Sans KR", "Arial Unicode MS" };

        public int fontSize = 32;

        private void Awake()
        {
            Font font = ResolveFont();
            if (font == null) return;

            Text[] labels = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].font = font;
            }
        }

        private Font ResolveFont()
        {
            string[] installed = Font.GetOSInstalledFontNames();
            if (installed == null) return null;

            for (int i = 0; i < preferredFonts.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (installed[j] != preferredFonts[i]) continue;

                    Font font = Font.CreateDynamicFontFromOSFont(preferredFonts[i], fontSize);
                    if (font != null) return font;
                }
            }

            Debug.LogWarning("[UIFontApplier] 한글 폰트를 찾지 못했다. 기본 폰트로 표시된다.");
            return null;
        }
    }
}
