using System;
using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using DetectiveGodot.Platform;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 뿌리 노드. 데이터를 읽고 공유 로직을 세우고, 거기서 나온 결과를 화면에 붙인다.
    ///
    /// 여기에 **게임 규칙이 하나도 없다**는 점이 이 이식의 요지다. 시간·가청 판정·들었는지
    /// 여부·표기 문자열은 전부 Unity 판과 같은 파일(<c>Assets/Scripts</c>의 순수 C#)이 맡는다.
    /// 이 클래스가 하는 일은 셋뿐이다 — 데이터 읽기, 노드 만들기, 키 입력 전달.
    ///
    /// 씬에 노드를 박아 두지 않고 rooms.json에서 만들어 낸다. Unity 판의 §18-2와 같은 원칙이다
    /// (Godot은 씬이 텍스트라 손으로 써도 안전하지만, 데이터의 파생물을 두 곳에 두지 않는다는
    /// 이유는 엔진과 무관하게 유효하다).
    /// </summary>
    public partial class Main : Node2D
    {
        /// <summary>월드 유닛 1 = 픽셀 몇. 공유 데이터는 유닛, Godot은 픽셀이다.</summary>
        public const float Ppu = 48f;

        /// <summary>
        /// 월드 좌표 → Godot 픽셀. **Y를 뒤집는 곳은 여기 한 곳뿐이다.**
        /// 공유 데이터는 Y가 위로 자라고 Godot 2D는 아래로 자란다. 변환을 흩뿌리면
        /// 벽 하나가 반대편에 서는 식으로 조용히 틀어지므로 단일 경계로 둔다.
        /// </summary>
        public static Vector2 ToPx(float worldX, float worldY)
        {
            return new Vector2(worldX * Ppu, -worldY * Ppu);
        }

        private const float WalkSpeed = 3.2f * Ppu;   // 유닛/초 → 픽셀/초
        private const int SeekStepMs = 5000;

        private RoomLayout _layout;
        private ScriptDefinition _script;
        private ListeningSession _session;
        private MansionView _view;
        private CharacterBody2D _detective;
        private HudOverlay _hud;
        private Camera2D _camera;
        private AudioDirector _audio;
        private NpcTokens _tokens;
        private ArtManifest _art;

        private string _room = string.Empty;
        private bool _sonar;

        public override void _Ready()
        {
            // `-- --selfcheck`로 띄우면 데이터만 검사하고 종료 코드로 알린다(게임을 만들지 않는다).
            if (SelfCheck.Requested())
            {
                int failures = SelfCheck.Run();
                GetTree().Quit(failures == 0 ? 0 : 1);
                return;
            }

            RoomTable table = GodotDataLoader.LoadRoomTable();
            _layout = RoomLayout.FromTable(table);
            if (_layout.RoomCount == 0)
            {
                GD.PushError("[Main] 방을 하나도 읽지 못했다. res://data/rooms.json 을 확인하라.");
                return;
            }

            // 참조 무결성은 공유 검사기가 본다 — Unity 판 씬 빌더와 같은 게이트다.
            foreach (string problem in RoomLayoutValidator.Validate(table))
                GD.PushWarning("[rooms.json] " + problem);

            _script = GodotDataLoader.LoadScript(GodotDataLoader.EavesdropCaseId);
            if (_script == null)
            {
                GD.PushError("[Main] 대본을 읽지 못했다.");
                return;
            }

            var timeline = new ScriptTimeline(_script);
            _session = new ListeningSession(timeline, new AudibilityModel(_layout));

            _art = GodotDataLoader.LoadArtManifest();

            BuildView();
            BuildWalls();
            BuildDetective();
            BuildTokens();
            BuildCamera();
            BuildHud();
            BuildAudio();

            // 회차는 바로 흐른다. 되돌려 듣는 것은 플레이어의 선택이다(공유 규칙).
            _session.Transport.Play();

            GD.Print($"[Main] 방 {_layout.RoomCount}개 · 발화 {timeline.Utterances.Count}개 · "
                     + $"회차 {SonarText.Clock(timeline.DurationMs)} · 공유 로직 그대로 작동");
        }

        // ── 만들기 ────────────────────────────────────────────

        private void BuildView()
        {
            _view = new MansionView { Layout = _layout, Session = _session, Art = _art, Name = "Mansion" };
            AddChild(_view);
        }

        /// <summary>
        /// 벽은 손으로 두지 않는다. 공유 <see cref="RoomLayout.BuildAllWallSegments"/>가
        /// 방 테두리에서 문을 빼고 같은 직선 위 조각을 병합해 만든 것을 그대로 물리 몸으로 세운다.
        /// </summary>
        private void BuildWalls()
        {
            var walls = new Node2D { Name = "Walls" };
            AddChild(walls);

            foreach (WallSegment segment in _layout.BuildAllWallSegments())
            {
                var body = new StaticBody2D { Position = ToPx(segment.CenterX, segment.CenterY) };
                var shape = new CollisionShape2D
                {
                    Shape = new RectangleShape2D
                    {
                        Size = new Vector2(segment.Width * Ppu, segment.Height * Ppu)
                    }
                };
                body.AddChild(shape);
                walls.AddChild(body);
            }
        }

        private void BuildDetective()
        {
            _detective = new CharacterBody2D { Name = "Detective" };
            var shape = new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(0.55f * Ppu, 0.55f * Ppu) }
            };
            _detective.AddChild(shape);

            // 보드게임 말 같은 토큰. 연출은 Unity 판과 같은 방향을 따른다.
            var token = new Polygon2D
            {
                Polygon = CirclePoints(0.30f * Ppu, 20),
                Color = new Color(0.91f, 0.69f, 0.42f)
            };
            _detective.AddChild(token);

            if (_layout.TryGetSpawnPosition(out float x, out float y))
                _detective.Position = ToPx(x, y);

            AddChild(_detective);
        }

        private static Vector2[] CirclePoints(float radius, int count)
        {
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Tau * i / count;
                points[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            return points;
        }

        /// <summary>
        /// 저택 안을 돌아다니는 사람들. tracks.json이 없으면 그냥 없는 채로 돈다 —
        /// 대본만으로도 게임은 성립한다.
        /// </summary>
        private void BuildTokens()
        {
            MovementTrackTable table = GodotDataLoader.LoadTracks(GodotDataLoader.EavesdropCaseId);
            if (table == null) return;

            _tokens = new NpcTokens
            {
                Layout = _layout,
                Tracks = MovementTracks.FromTable(table),
                Session = _session,
                Name = "Npcs"
            };
            AddChild(_tokens);
        }

        private void BuildAudio()
        {
            _audio = new AudioDirector { Name = "Audio" };
            AddChild(_audio);
            _audio.Setup(_art);
        }

        /// <summary>저택 전체가 화면에 들어오도록 확대율을 데이터에서 계산한다.</summary>
        private void BuildCamera()
        {
            _camera = new Camera2D { Name = "Camera" };
            AddChild(_camera);

            if (_layout.TryGetBounds(out float minX, out float minY, out float maxX, out float maxY))
            {
                Vector2 topLeft = ToPx(minX, maxY);
                Vector2 bottomRight = ToPx(maxX, minY);
                Vector2 size = bottomRight - topLeft;
                _camera.Position = topLeft + size * 0.5f;

                Vector2 viewport = GetViewport().GetVisibleRect().Size;
                float margin = 1.18f;   // 테두리 여유
                float zoom = Mathf.Min(viewport.X / (size.X * margin), viewport.Y / (size.Y * margin));
                _camera.Zoom = new Vector2(zoom, zoom);
            }
            _camera.MakeCurrent();
        }

        private void BuildHud()
        {
            _hud = new HudOverlay
            {
                Session = _session,
                RoomNameProvider = () => _layout.DisplayNameOf(_room),
                Name = "Hud"
            };
            AddChild(_hud);
        }

        // ── 매 프레임 ─────────────────────────────────────────

        public override void _PhysicsProcess(double delta)
        {
            if (_session == null) return;

            MoveDetective();
            TrackRoom();

            // 회차 진행. 배속·드리프트 없는 누적은 공유 PlaybackTransport가 맡는다.
            _session.Advance((int)Math.Round(delta * 1000.0));

            _view.QueueRedraw();
            if (_tokens != null) _tokens.QueueRedraw();
            _hud.Refresh();
        }

        private void MoveDetective()
        {
            var dir = new Vector2(
                (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1f : 0f)
                    - (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? 1f : 0f),
                (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) ? 1f : 0f)
                    - (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) ? 1f : 0f));

            // 청취 모드에서는 ←→가 시각 탐색이므로 좌우 이동을 막는다.
            if (_sonar) dir.X = 0f;

            _detective.Velocity = dir.LengthSquared() > 0f ? dir.Normalized() * WalkSpeed : Vector2.Zero;
            _detective.MoveAndSlide();
        }

        /// <summary>
        /// 청취점 = 탐정이 선 방. Unity 판 <c>PlayerRoomTracker</c>와 같은 역할이고,
        /// 방 사이(문간)에서는 빈 문자열이다 — 그때는 아무것도 온전히 들리지 않는다.
        /// </summary>
        private void TrackRoom()
        {
            Vector2 px = _detective.Position;
            RoomDefinition room = _layout.FindRoomAt(px.X / Ppu, -px.Y / Ppu);
            string id = room != null ? room.id : string.Empty;
            if (id == _room) return;
            _room = id;
            _session.MoveTo(id);
            if (_audio != null) _audio.OnRoomChanged(id);
        }

        public override void _ExitTree()
        {
            // 정적 폰트 캐시는 SceneTree보다 오래 살므로 여기서 끊는다.
            KoreanFont.Release();
        }

        // ── 키 입력 ───────────────────────────────────────────

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (_session == null) return;
            if (!(@event is InputEventKey key) || !key.Pressed || key.Echo) return;

            switch (key.Keycode)
            {
                case Key.Tab:
                    _sonar = !_sonar;
                    _view.SonarMode = _sonar;
                    _hud.SonarMode = _sonar;
                    if (_audio != null && _art != null)
                        _audio.PlayBgm(_sonar ? _art.audio.bgmTimeline : _art.audio.bgmExplore);
                    break;
                case Key.Space:
                    _session.Transport.TogglePlay();
                    break;
                case Key.Left:
                    if (_sonar) _session.Transport.SeekBy(-SeekStepMs);
                    break;
                case Key.Right:
                    if (_sonar) _session.Transport.SeekBy(SeekStepMs);
                    break;
                case Key.R:
                    _session.Restart();
                    break;
                case Key.Bracketleft:
                    _session.Transport.SpeedPercent -= 25;
                    break;
                case Key.Bracketright:
                    _session.Transport.SpeedPercent += 25;
                    break;
                case Key.Escape:
                    GetTree().Quit();
                    break;
            }
            GetViewport().SetInputAsHandled();
        }
    }
}
