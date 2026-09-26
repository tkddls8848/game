using System.Collections.Generic;
using System.Text;
using Detective.Eavesdrop;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 영상 플레이어의 조작 막대. 이 게임의 컨셉이 "녹화된 한 시간을 재생해 보는 것"이므로
    /// 화면 아래쪽은 실제 영상 플레이어처럼 생겨야 한다.
    ///
    /// 담는 것:
    ///   * 진행 바 — 끌어서 아무 지점으로. 눌러 끄는 동안은 재생을 멈추고, 놓으면 되돌린다
    ///     (영상 플레이어의 몸에 익은 동작이다).
    ///   * <b>이벤트 눈금</b> — 문소리·발소리·깨지는 소리가 난 지점을 바 위에 찍는다.
    ///     이게 이 게임의 진행 바가 보통 영상과 다른 점이다. 어디를 다시 들어야 하는지
    ///     바만 보고 알 수 있다. 다만 **귀에 닿은 것만** 찍는다 — 안 들은 것까지 찍으면
    ///     듣지 않고 지도를 읽는 게임이 된다.
    ///   * 재생/정지, ◀◀/▶▶ 방향, 배속 단계, 시각 표기.
    ///
    /// 시각·배속 표기는 공유 <see cref="SonarText"/>가 만든다. 여기서 문자열을 새로 조립하지
    /// 않는 것은 2D판·3D판·Unity판이 같은 글자를 보여 주게 하려는 것이다.
    /// </summary>
    public partial class PlayerBar : CanvasLayer
    {
        public ListeningSession Session;

        /// <summary>눈금을 찍을지 판단할 때 쓴다 — 이 방에서 들렸는가.</summary>
        public System.Func<string> ListenerRoomProvider;

        private const int BarHeight = 8;
        private const int BarMargin = 130;
        private const int BarBottom = 74;
        private const int SeekStepMs = 5000;

        private static readonly Color Track = new Color(0.155f, 0.148f, 0.142f);
        private static readonly Color Filled = Palette.Lamp;
        private static readonly Color Head = Palette.Paper;
        private static readonly Color MarkSpeech = Palette.Slate;
        private static readonly Color MarkEvent = Palette.Oxblood;

        private Bar _bar;
        private Label _clock;
        private Label _speed;
        private Label _hint;

        /// <summary>바를 끄는 동안 재생을 멈췄다면, 놓을 때 되돌리기 위한 기억.</summary>
        private bool _wasPlayingBeforeScrub;
        private bool _scrubbing;

        public override void _Ready()
        {
            Font font = KoreanFont.Load();

            _bar = new Bar { Owner3 = this, MouseFilter = Control.MouseFilterEnum.Stop };
            AddChild(_bar);

            _clock = MakeLabel(font, Palette.SizeStatus, Palette.Paper);
            AddChild(_clock);

            _speed = MakeLabel(font, Palette.SizeStatus, Palette.Lamp);
            AddChild(_speed);

            _hint = MakeLabel(font, Palette.SizeHint, Palette.Faint);
            _hint.Text = "Space 재생·정지 · ←→ 5초 · J/L 역재생·정주행 · [ ] 배속 · 바를 끌어 이동 · R 처음 · Tab 청취 · Esc 종료";
            AddChild(_hint);

            Layout();
            GetViewport().SizeChanged += Layout;
        }

        private static Label MakeLabel(Font font, int size, Color color)
        {
            return new Label { LabelSettings = Palette.Label(font, size, color) };
        }

        private void Layout()
        {
            Vector2 view = GetViewport().GetVisibleRect().Size;
            _bar.Position = new Vector2(BarMargin, view.Y - BarBottom);
            _bar.Size = new Vector2(Mathf.Max(40f, view.X - BarMargin * 2), BarHeight + 18);
            _clock.Position = new Vector2(20, view.Y - BarBottom - 6);
            _speed.Position = new Vector2(view.X - BarMargin + 14, view.Y - BarBottom - 6);
            _hint.Position = new Vector2(20, view.Y - 30);
        }

        public void Refresh()
        {
            if (Session == null) return;
            PlaybackTransport transport = Session.Transport;

            string state = transport.IsRewinding ? "◀◀" : transport.IsPlaying ? "▶" : "❙❙";
            _clock.Text = state + "  " + SonarText.Clock(transport.PositionMs)
                        + " / " + SonarText.Clock(transport.DurationMs);

            string sign = transport.Direction == PlayDirection.Backward ? "−" : "";
            _speed.Text = sign + SonarText.Speed(transport.SpeedPercent);

            _bar.QueueRedraw();
        }

        // ── 입력 ──────────────────────────────────────────────

        /// <summary>바 위에서의 마우스. 끌면 그 지점으로 옮기고, 끄는 동안은 멈춘다.</summary>
        internal void OnBarInput(InputEvent @event, float width)
        {
            if (Session == null) return;

            if (@event is InputEventMouseButton click && click.ButtonIndex == MouseButton.Left)
            {
                if (click.Pressed)
                {
                    _scrubbing = true;
                    _wasPlayingBeforeScrub = Session.Transport.IsPlaying;
                    Session.Transport.Pause();
                    SeekToFraction(click.Position.X / width);
                }
                else if (_scrubbing)
                {
                    _scrubbing = false;
                    if (_wasPlayingBeforeScrub) Session.Transport.Play();
                }
                _bar.AcceptEvent();
            }
            else if (@event is InputEventMouseMotion motion && _scrubbing)
            {
                SeekToFraction(motion.Position.X / width);
                _bar.AcceptEvent();
            }
        }

        private void SeekToFraction(float fraction)
        {
            int duration = Session.Transport.DurationMs;
            Session.SeekTo((int)(Mathf.Clamp(fraction, 0f, 1f) * duration));
        }

        /// <summary>플레이어 단축키. Main이 키를 받아 넘긴다.</summary>
        public bool HandleKey(Key key)
        {
            if (Session == null) return false;
            PlaybackTransport transport = Session.Transport;

            switch (key)
            {
                case Key.Space:
                    transport.TogglePlay();
                    return true;
                case Key.J:                                   // 영상 편집기의 관습: J 역재생, L 정주행
                    transport.Play(PlayDirection.Backward);
                    return true;
                case Key.L:
                    transport.Play(PlayDirection.Forward);
                    return true;
                case Key.Left:
                    Session.SeekTo(transport.PositionMs - SeekStepMs);
                    return true;
                case Key.Right:
                    Session.SeekTo(transport.PositionMs + SeekStepMs);
                    return true;
                case Key.Bracketleft:
                    transport.SpeedPercent = PrevPreset(transport.SpeedPercent);
                    return true;
                case Key.Bracketright:
                    transport.SpeedPercent = NextPreset(transport.SpeedPercent);
                    return true;
                case Key.R:
                    Session.Restart();
                    return true;
            }
            return false;
        }

        private static int NextPreset(int current)
        {
            int[] presets = PlaybackTransport.SpeedPresets;
            for (int i = 0; i < presets.Length; i++)
                if (presets[i] > current) return presets[i];
            return presets[presets.Length - 1];
        }

        private static int PrevPreset(int current)
        {
            int[] presets = PlaybackTransport.SpeedPresets;
            for (int i = presets.Length - 1; i >= 0; i--)
                if (presets[i] < current) return presets[i];
            return presets[0];
        }

        // ── 진행 바 그리기 ────────────────────────────────────

        /// <summary>
        /// 바를 그리는 자식. Control로 둬야 마우스 입력을 자기 좌표로 받는다.
        /// </summary>
        private partial class Bar : Control
        {
            public PlayerBar Owner3;

            public override void _GuiInput(InputEvent @event)
            {
                Owner3.OnBarInput(@event, Size.X);
            }

            public override void _Draw()
            {
                ListeningSession session = Owner3.Session;
                if (session == null) return;

                int duration = session.Transport.DurationMs;
                if (duration <= 0) return;

                float width = Size.X;
                float top = 14f;

                DrawRect(new Rect2(0f, top, width, BarHeight), Track);

                float played = width * session.Transport.PositionMs / duration;
                DrawRect(new Rect2(0f, top, played, BarHeight), Filled);

                DrawMarkers(session, duration, width, top);

                // 재생 머리
                DrawRect(new Rect2(played - 1.5f, top - 5f, 3f, BarHeight + 10f), Head);
            }

            /// <summary>
            /// 귀에 닿은 것만 눈금으로 찍는다. 아직 듣지 않은 것을 찍으면 듣지 않고 바를 읽는
            /// 게임이 되어 버린다 — 이 게임의 동력은 "되돌려 듣는 것"이다.
            /// </summary>
            private void DrawMarkers(ListeningSession session, int duration, float width, float top)
            {
                IList<Utterance> all = session.Timeline.Utterances;
                for (int i = 0; i < all.Count; i++)
                {
                    Audibility level = session.HeardLevel(all[i].id);
                    if (level == Audibility.None) continue;

                    float x = width * all[i].startMs / duration;
                    float height = level == Audibility.Full ? 6f : 3f;
                    DrawRect(new Rect2(x, top - height - 1f, 2f, height),
                             new Color(MarkSpeech, level == Audibility.Full ? 0.9f : 0.5f));
                }

                if (session.Events == null) return;
                string room = Owner3.ListenerRoomProvider != null ? Owner3.ListenerRoomProvider() : string.Empty;
                if (string.IsNullOrEmpty(room)) return;

                // 이벤트는 "지금 그 방에 서 있으면 들렸을 것"을 기준으로 찍는다.
                // 이벤트는 들은 기록을 따로 두지 않으므로(정황이다) 이 근사가 맞다.
                foreach (ScriptEvent e in session.Events.All)
                {
                    if (e.room != room) continue;
                    if (e.startMs > session.Transport.PositionMs) continue;   // 아직 지나지 않은 것은 안 찍는다
                    float x = width * e.startMs / duration;
                    DrawRect(new Rect2(x, top + BarHeight + 1f, 2f, 5f), MarkEvent);
                }
            }
        }
    }
}
