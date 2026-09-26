using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// 카메라에 붙는 후처리. "가볍다"를 고치는 마지막 한 겹이고, Godot 2.5D에서 무게를 만든
    /// 항목(채도 내림 · 대비 · 비네팅 · 입자)을 Unity 쪽에 옮긴 것이다.
    ///
    /// 이 프로젝트는 <b>내장 파이프라인</b>이다 — URP도 PostProcessing 패키지도 없다.
    /// 그래서 <c>OnRenderImage</c>로 직접 그린다. 패키지를 새로 들이지 않는 이유는 둘이다:
    /// 검증된 씬·빌드를 건드리지 않아도 되고, 셰이더 한 장은 텍스트라서 에이전트가 안전하게 쓴다.
    ///
    /// 셰이더가 없으면 <b>아무것도 하지 않고 원본을 그대로 통과시킨다.</b> 빌드에서 셰이더가
    /// 벗겨지면(§Always Included Shaders) 화면이 자홍색으로 죽는 대신 후처리만 사라진다 —
    /// 이 프로젝트는 그 사고를 이미 한 번 겪었다.
    ///
    /// UI는 Screen Space Overlay 캔버스라 이 효과를 받지 않는다. 의도한 것이다:
    /// 세계는 가라앉고 종이 패널의 글자는 읽혀야 한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraGrade : MonoBehaviour
    {
        public const string ShaderName = "Hidden/Detective/Grade";

        [Header("색")]
        [Tooltip("1 = 원본. 내리면 색이 튀지 않고 사진처럼 앉는다.")]
        [Range(0f, 2f)] public float saturation = 0.72f;

        [Tooltip("중간값을 축으로 한 대비.")]
        [Range(0.5f, 2f)] public float contrast = 1.10f;

        [Tooltip("검정을 들어 올린다. 순검정은 화면을 납작하게 만든다.")]
        [Range(0f, 0.2f)] public float blackLift = 0.035f;

        [Header("비네팅")]
        [Range(0f, 1f)] public float vignetteStrength = 0.82f;
        [Range(0f, 1f)] public float vignetteInner = 0.30f;
        [Range(0f, 1.5f)] public float vignetteOuter = 0.86f;

        [Header("입자")]
        [Tooltip("정지한 필름 입자. 매 프레임 흔들면 TV 노이즈가 되어 산만하다.")]
        [Range(0f, 0.15f)] public float grain = 0.032f;

        private Material _material;
        private bool _unavailable;

        private void OnDisable()
        {
            if (_material == null) return;
            // 실행 중에 만든 머티리얼은 실행 중에 지운다. 에디터에서 누적되면 새는 것으로 잡힌다.
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
            _material = null;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!EnsureMaterial())
            {
                Graphics.Blit(source, destination);
                return;
            }

            _material.SetFloat("_Saturation", saturation);
            _material.SetFloat("_Contrast", contrast);
            _material.SetFloat("_Lift", blackLift);
            _material.SetFloat("_VignetteStrength", vignetteStrength);
            _material.SetFloat("_VignetteInner", vignetteInner);
            _material.SetFloat("_VignetteOuter", vignetteOuter);
            _material.SetFloat("_Grain", grain);

            Graphics.Blit(source, destination, _material);
        }

        private bool EnsureMaterial()
        {
            if (_material != null) return true;
            if (_unavailable) return false;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
            {
                // 한 번만 알리고 그 뒤로는 조용히 통과시킨다. 매 프레임 경고하면 로그가 못 쓰게 된다.
                Debug.LogWarning("[CameraGrade] " + ShaderName + " 를 찾지 못해 후처리를 건너뛴다. "
                                 + "Tools/Detective/Fix Always Included Shaders 를 실행했는가?");
                _unavailable = true;
                return false;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }
    }
}
