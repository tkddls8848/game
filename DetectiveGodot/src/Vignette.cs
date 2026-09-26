using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 화면 가장자리를 어둡게 눌러 주는 겹. 여기까지가 "가볍다"를 고치는 마지막 한 겹이다.
    ///
    /// 왜 이게 무게를 만드는가: 균일하게 밝은 화면은 <b>그래픽</b>으로 읽히고, 가장자리가
    /// 떨어지는 화면은 <b>사진</b>으로 읽힌다. 렌즈로 본 것 같아지기 때문이다. 여기에
    /// 아주 옅은 입자를 얹으면 인쇄물·필름의 질감이 생긴다 — 이 게임의 연출 방향인
    /// "사건 파일"에 정확히 맞는 방향이다.
    ///
    /// 입자는 <b>정지</b>해 있다. 매 프레임 흔들면 TV 노이즈처럼 시끄러워지고, 조용한
    /// 저택에서 그건 산만하다. 한 번 뿌려 두고 그대로 둔다.
    ///
    /// 셰이더는 문자열로 들고 있는다 — .gdshader 파일을 따로 두면 씬·리소스가 늘어나고,
    /// 이 한 겹은 코드와 붙어 있는 편이 읽기 쉽다.
    /// </summary>
    public partial class Vignette : CanvasLayer
    {
        private const string ShaderSource = @"
shader_type canvas_item;

// 가장자리가 얼마나 떨어지는가
uniform float strength : hint_range(0.0, 1.0) = 0.86;
// 어디서부터 떨어지기 시작하는가
uniform float inner : hint_range(0.0, 1.0) = 0.30;
uniform float outer : hint_range(0.0, 1.5) = 0.82;
// 입자 세기
uniform float grain : hint_range(0.0, 0.15) = 0.035;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
    vec2 d = SCREEN_UV - vec2(0.5);
    // 가로가 넓으므로 세로를 조금 눌러 타원으로 만든다. 정원은 화면 비율과 어긋나 보인다.
    float r = length(d * vec2(1.0, 1.18));
    float fall = smoothstep(inner, outer, r) * strength;

    // 정지한 입자. 어두운 쪽에서만 도드라지게 섞는다.
    float g = (hash(floor(SCREEN_UV * 1400.0)) - 0.5) * grain;

    COLOR = vec4(0.0, 0.0, 0.0, clamp(fall + g, 0.0, 0.95));
}
";

        public override void _Ready()
        {
            var shader = new Shader { Code = ShaderSource };

            var rect = new ColorRect
            {
                Name = "Overlay",
                Material = new ShaderMaterial { Shader = shader },
                // 글자를 가리지 않도록 입력은 통과시킨다.
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(rect);
        }
    }
}
