using System.Collections.Generic;
using System.Text;
using Detective.Eavesdrop;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 글자로 보여 주는 모든 것. 시계·배속·지금 선 방·들은 수, 그리고 청취 모드의 대사 버블.
    ///
    /// 표기는 전부 공유 로직이 만든다 — <see cref="SonarText.Clock"/>, <see cref="SonarText.Speed"/>,
    /// <see cref="SonarText.VoiceName"/>, <see cref="SonarText.Garble"/>. 여기서 문자열을 새로
    /// 조립하지 않는다. 그래야 Unity 판과 같은 글자가 나온다.
    ///
    /// 한글은 반드시 Noto Serif KR로 그린다. Godot 기본 폰트에는 한글 글리프가 없어서
    /// 폰트를 붙이지 않으면 화면이 통째로 □로 나온다 — 조용히 깨지는 축에 속하니 못박아 둔다.
    /// </summary>
    public partial class HudOverlay : CanvasLayer
    {
        public ListeningSession Session;
        public System.Func<string> RoomNameProvider;
        public bool SonarMode;

        private Label _status;
        private Label _bubbles;
        private Label _keys;


        public override void _Ready()
        {
            Font font = KoreanFont.Load();

            _status = MakeLabel(font, Palette.SizeStatus, Palette.Paper);
            _status.Position = new Vector2(28, 22);
            AddChild(_status);

            // 대사는 종이색으로. 강조색으로 쓰면 자막이 아니라 게임 텍스트가 된다.
            _bubbles = MakeLabel(font, Palette.SizeBody, Palette.Paper);
            _bubbles.Position = new Vector2(28, 62);
            AddChild(_bubbles);

            // 조작 안내는 플레이어 막대가 들고 있으므로 여기서는 비워 둔다.
            _keys = MakeLabel(font, Palette.SizeHint, Palette.Faint);
            _keys.Position = new Vector2(28, 96);
            AddChild(_keys);
        }

        private static Label MakeLabel(Font font, int size, Color color)
        {
            return new Label { LabelSettings = Palette.Label(font, size, color) };
        }

        public void Refresh()
        {
            if (Session == null) return;

            string room = RoomNameProvider != null ? RoomNameProvider() : string.Empty;
            if (string.IsNullOrEmpty(room)) room = "문간";

            int heard = Session.FullyHeardCount;
            int total = Session.Timeline.Utterances.Count;
            int missed = SonarText.MissedSoFar(Session);

            var head = new StringBuilder();
            head.Append(SonarMode ? "[청취] " : "[탐색] ");
            head.Append(SonarText.Clock(Session.PositionMs));
            head.Append(" / ").Append(SonarText.Clock(Session.Timeline.DurationMs));
            head.Append("   ").Append(SonarText.Speed(Session.Transport.SpeedPercent));
            head.Append(Session.Transport.IsPlaying ? "  ▶" : "  ❙❙");
            head.Append("   귀: ").Append(room);
            head.Append("   들은 대사 ").Append(heard).Append('/').Append(total);
            if (missed > 0) head.Append("   놓친 대사 ").Append(missed);
            _status.Text = head.ToString();

            _bubbles.Text = BuildBubbles();
        }

        /// <summary>
        /// 지금 귀에 닿는 발화. 같은 방이면 전문, 벽 너머면 <see cref="SonarText.Garble"/>로
        /// 전부 가린 웅얼거림. 무엇이 들리는지의 판정은 <see cref="AudibilityModel"/>이 이미 했다.
        /// </summary>
        private string BuildBubbles()
        {
            IList<PerceivedUtterance> current = Session.Current;
            if (current.Count == 0) return SonarMode ? "…조용하다" : string.Empty;

            var text = new StringBuilder();
            for (int i = 0; i < current.Count; i++)
            {
                PerceivedUtterance heard = current[i];
                if (heard.Level == Audibility.None) continue;
                if (text.Length > 0) text.Append('\n');

                if (heard.Level == Audibility.Full)
                    text.Append(SonarText.VoiceName(heard.VoiceId)).Append(": ").Append(heard.Text);
                else
                    text.Append("〈벽 너머〉 ").Append(SonarText.Garble(heard.Text));
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// 한글 폰트 로딩. 임포트된 리소스를 먼저 쓰고, 없으면 파일에서 직접 읽는다
    /// (에디터가 한 번도 돌지 않은 상태에서도 글자가 나와야 한다).
    /// </summary>
    public static class KoreanFont
    {
        public const string Path = "res://fonts/NotoSerifKR-Regular.otf";
        private static Font _cached;
        private static bool _tried;

        public static Font Load()
        {
            if (_tried) return _cached;
            _tried = true;

            if (ResourceLoader.Exists(Path))
            {
                _cached = ResourceLoader.Load<Font>(Path);
                if (_cached != null) return _cached;
            }

            var file = new FontFile();
            Error err = file.LoadDynamicFont(Path);
            if (err == Error.Ok) { _cached = file; return _cached; }

            GD.PushError("[KoreanFont] " + Path + " 를 읽지 못했다(" + err + "). "
                         + "기본 폰트에는 한글 글리프가 없어 글자가 □로 나온다. "
                         + "`python tools/sync_godot.py`를 돌렸는가?");
            return null;
        }

        /// <summary>
        /// 캐시를 놓는다. 정적 필드가 Font 리소스를 붙들면 SceneTree가 사라진 뒤에도 남아
        /// 종료 시 "resources still in use" 경고가 난다. 트리를 떠날 때 Main이 불러 준다.
        /// </summary>
        public static void Release()
        {
            _cached = null;
            _tried = false;
        }
    }
}
