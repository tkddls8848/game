using System;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using DetectiveGodot.Platform;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 2.5D 판의 뿌리. 2D 판(<see cref="Main"/>)과 **같은 데이터·같은 로직**을 쓰고
    /// 화면만 3D로 세운다. F1으로 두 화면을 오갈 수 있어 나란히 비교할 수 있다.
    ///
    /// 여기서도 게임 규칙은 한 줄도 없다. 시간·가청 판정·이동·이벤트는 전부 공유 로직이 맡는다.
    /// 이 클래스가 하는 일은 데이터 읽기, 노드 만들기, 키 넘기기다.
    ///
    /// 탐정은 3D에서 직접 걷지 않는다 — 청취점을 <b>방 단위로</b> 옮긴다(1~6 또는 WASD).
    /// 이 게임에서 중요한 것은 "어느 방에 귀를 두는가"뿐이고, 3D에서 사람을 조작하게 하면
    /// 벽 사이를 비집는 데 시간을 쓰게 된다. 소리를 좇는 일에서 눈을 떼게 만들 이유가 없다.
    /// </summary>
    public partial class Main25D : Node3D
    {
        private RoomLayout _layout;
        private ScriptDefinition _script;
        private ListeningSession _session;
        private ArtManifest _art;

        private MansionView3D _view;
        private NpcFigures3D _figures;
        private HudOverlay _hud;
        private PlayerBar _bar;
        private AudioDirector _audio;
        private EarMarker _ear;

        private string _room = string.Empty;

        public override void _Ready()
        {
            RoomTable table = GodotDataLoader.LoadRoomTable();
            _layout = RoomLayout.FromTable(table);
            if (_layout.RoomCount == 0)
            {
                GD.PushError("[Main25D] 방을 하나도 읽지 못했다.");
                return;
            }

            _script = GodotDataLoader.LoadScript(GodotDataLoader.EavesdropCaseId);
            if (_script == null)
            {
                GD.PushError("[Main25D] 대본을 읽지 못했다.");
                return;
            }
            _art = GodotDataLoader.LoadArtManifest();

            MovementTracks tracks = LoadTracks();
            var timeline = new ScriptTimeline(_script);
            EventTimeline events = BuildEvents(tracks);
            _session = new ListeningSession(timeline, new AudibilityModel(_layout), events);

            _view = new MansionView3D { Layout = _layout, Art = _art, Name = "Mansion" };
            AddChild(_view);

            if (tracks != null)
            {
                _figures = new NpcFigures3D
                {
                    Layout = _layout, Tracks = tracks, Session = _session, Name = "Figures"
                };
                AddChild(_figures);
            }

            _ear = new EarMarker { Name = "Ear" };
            AddChild(_ear);

            _audio = new AudioDirector { Name = "Audio" };
            AddChild(_audio);
            _audio.Setup(_art);

            AddChild(new Vignette { Name = "Vignette", Layer = 1 });

            _hud = new HudOverlay
            {
                Session = _session,
                RoomNameProvider = () => _layout.DisplayNameOf(_room),
                Name = "Hud",
                Layer = 2
            };
            AddChild(_hud);

            _bar = new PlayerBar
            {
                Layer = 3,
                Session = _session,
                ListenerRoomProvider = () => _room,
                Name = "PlayerBar"
            };
            AddChild(_bar);

            // 귀는 대본이 정한 시작 방(없으면 첫 방)에 둔다.
            MoveEar(_layout.SpawnRoomId.Length > 0 ? _layout.SpawnRoomId : _layout.Rooms[0].id);
            _session.Transport.Play();

            GD.Print($"[Main25D] 방 {_layout.RoomCount}개 · 발화 {timeline.Utterances.Count}개 · "
                     + $"이동 소리 {(events != null ? events.Count : 0)}개 · 2.5D");
        }

        private MovementTracks LoadTracks()
        {
            MovementTrackTable table = GodotDataLoader.LoadTracks(GodotDataLoader.EavesdropCaseId);
            return table != null ? MovementTracks.FromTable(table) : null;
        }

        /// <summary>
        /// 이동에서 소리를 뽑고, 대본 폴더에 손으로 적은 이벤트가 있으면 합친다.
        /// 뽑은 것과 적은 것을 합쳐 하나의 표로 만든다.
        /// </summary>
        private EventTimeline BuildEvents(MovementTracks tracks)
        {
            var all = new System.Collections.Generic.List<ScriptEvent>();
            if (tracks != null)
                all.AddRange(MovementEvents.Derive(tracks, _layout, _script.durationMs));

            EventTable authored = GodotDataLoader.LoadEvents(GodotDataLoader.EavesdropCaseId);
            if (authored != null) all.AddRange(authored.events);

            return all.Count > 0 ? new EventTimeline(all) : null;
        }

        // ── 매 프레임 ─────────────────────────────────────────

        public override void _Process(double delta)
        {
            if (_session == null) return;
            _session.Advance((int)Math.Round(delta * 1000.0));
            _hud.Refresh();
            _bar.Refresh();
        }

        // ── 청취점 ────────────────────────────────────────────

        private void MoveEar(string roomId)
        {
            if (roomId == _room) return;
            _room = roomId ?? string.Empty;
            _session.MoveTo(_room);
            if (_audio != null) _audio.OnRoomChanged(_room);
            if (_ear != null && _layout.TryGetRoomCenter(_room, out float cx, out float cy))
                _ear.Position = MansionView3D.ToWorld3(cx, cy, 0.03f);
        }

        /// <summary>WASD로는 지금 방에서 가장 가까운 이웃 방으로 옮긴다.</summary>
        private void StepEar(float dx, float dy)
        {
            if (!_layout.TryGetRoomCenter(_room, out float cx, out float cy)) return;

            string best = null;
            float bestScore = 0f;
            foreach (RoomDefinition room in _layout.Rooms)
            {
                if (room.id == _room) continue;
                float vx = room.CenterX - cx;
                float vy = room.CenterY - cy;
                float length = Mathf.Sqrt(vx * vx + vy * vy);
                if (length < 0.001f) continue;

                // 누른 방향과 얼마나 같은 쪽인가 ÷ 거리. 방향이 맞고 가까운 방을 고른다.
                float alignment = (vx / length) * dx + (vy / length) * dy;
                if (alignment <= 0.35f) continue;
                float score = alignment / length;
                if (score > bestScore) { bestScore = score; best = room.id; }
            }
            if (best != null) MoveEar(best);
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (_session == null) return;
            if (!(@event is InputEventKey key) || !key.Pressed || key.Echo) return;

            // 재생 조작은 플레이어 막대가 먼저 본다.
            if (_bar.HandleKey(key.Keycode)) { GetViewport().SetInputAsHandled(); return; }

            switch (key.Keycode)
            {
                case Key.W: case Key.Up:    StepEar(0f, 1f); break;
                case Key.S: case Key.Down:  StepEar(0f, -1f); break;
                case Key.A:                 StepEar(-1f, 0f); break;
                case Key.D:                 StepEar(1f, 0f); break;
                case Key.F1:
                    GetTree().ChangeSceneToFile("res://Main.tscn");
                    break;
                case Key.Escape:
                    GetTree().Quit();
                    break;
                default:
                    // 1~9로 방을 바로 고른다.
                    int index = (int)key.Keycode - (int)Key.Key1;
                    if (index >= 0 && index < _layout.RoomCount) MoveEar(_layout.Rooms[index].id);
                    break;
            }
            GetViewport().SetInputAsHandled();
        }

        public override void _ExitTree()
        {
            KoreanFont.Release();
        }
    }

    /// <summary>귀가 놓인 방을 알려 주는 표시. 바닥에 깔리는 호박색 원.</summary>
    public partial class EarMarker : Node3D
    {
        public override void _Ready()
        {
            var material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.72f, 0.42f, 0.30f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = new Color(0.95f, 0.72f, 0.42f),
                EmissionEnergyMultiplier = 0.7f
            };
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.95f, BottomRadius = 0.95f, Height = 0.02f },
                MaterialOverride = material
            });
        }
    }
}
