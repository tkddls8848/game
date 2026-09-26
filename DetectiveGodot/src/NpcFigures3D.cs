using System.Collections.Generic;
using Detective.Core;
using Detective.Eavesdrop;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 3D 저택 안을 걸어 다니는 사람들. 2D판 <see cref="NpcTokens"/>와 **같은 규칙**이다.
    ///
    /// 이름을 붙이지 않는다. 누가 어느 방에 있었는지가 플레이어가 소리로 알아내야 하는 것이다.
    /// 보이는 것은 정체 모를 실루엣뿐이고, 말이나 소리가 나는 방의 실루엣만 머리 위에 표시가 뜬다 —
    /// 그건 이미 귀로 아는 정보라 알려 줘도 퍼즐이 깨지지 않고, 어느 실루엣이 그 목소리인지
    /// 붙여 보는 일이 곧 게임이 된다.
    ///
    /// 위치는 매 프레임 <see cref="MovementTracks"/>에서 다시 읽는다. 회차를 뒤로 감으면
    /// 사람들도 거꾸로 걸어간다 — 재생 위치의 함수이므로 따로 처리할 것이 없다.
    /// 이게 "영상처럼 다룬다"가 실제로 작동하는 모습이다.
    /// </summary>
    public partial class NpcFigures3D : Node3D
    {
        public RoomLayout Layout;
        public MovementTracks Tracks;
        public ListeningSession Session;

        /// <summary>실루엣 키. 벽(0.9)보다 조금 낮게 둬야 위에서 방 안이 보인다.</summary>
        private const float BodyHeight = 0.66f;

        /// <summary>
        /// 가늘게. 처음에는 0.15로 뚱뚱해서 보드게임 말처럼 보였다 — 사람은 위에서 보면
        /// 어깨보다 좁고, 그 비례가 무게를 만든다.
        /// </summary>
        private const float BodyRadius = 0.105f;
        private const float Spread = 0.45f;

        /// <summary>자리를 옮길 때 튀지 않게 하는 따라감 속도(초당 비율).</summary>
        private const float FollowLerp = 9f;


        private readonly List<string> _ids = new List<string>();
        private readonly Dictionary<string, Node3D> _figures = new Dictionary<string, Node3D>();
        private readonly Dictionary<string, OmniLight3D> _markers = new Dictionary<string, OmniLight3D>();
        private readonly Dictionary<string, StandardMaterial3D> _materials = new Dictionary<string, StandardMaterial3D>();

        public override void _Ready()
        {
            if (Tracks == null) return;
            foreach (MovementTrack track in Tracks.All)
                if (!string.IsNullOrEmpty(track.npcId)) _ids.Add(track.npcId);
            _ids.Sort();

            foreach (string id in _ids) _figures[id] = BuildFigure(id);
        }

        private Node3D BuildFigure(string npcId)
        {
            var root = new Node3D { Name = "Figure_" + npcId, Visible = false };

            // 거친 무광. 반짝이면 플라스틱이 되고, 플라스틱은 가볍다.
            var material = new StandardMaterial3D
            {
                AlbedoColor = Palette.Figure,
                Roughness = 1.0f,
                Metallic = 0f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled
            };
            _materials[npcId] = material;

            // 보드게임 말 같은 실루엣 — 연출 방향("보드게임 말 같은 인물 토큰")을 3D로 옮긴 것.
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = BodyRadius, Height = BodyHeight },
                Position = new Vector3(0f, BodyHeight * 0.5f, 0f),
                MaterialOverride = material
            });
            // 어깨. 위에서 볼 때 사람 형태를 알아보게 하는 것은 이 한 덩이다.
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(BodyRadius * 3.4f, 0.10f, BodyRadius * 1.7f) },
                Position = new Vector3(0f, BodyHeight * 0.80f, 0f),
                MaterialOverride = material
            });

            // 소리가 나는 방의 사람에게만 드는 빛.
            //
            // 처음에는 머리 위에 빛나는 호박색 구슬을 띄웠다. 그것 하나가 화면을 게임으로
            // 만들었다 — 떠 있는 발광 아이콘은 UI이고, UI는 가볍다. 대신 **실제 광원**을
            // 세워 발밑에 빛이 고이게 한다. 같은 정보를 주면서 장면 안에 머문다.
            var glow = new OmniLight3D
            {
                LightColor = Palette.Lamp,
                LightEnergy = 1.5f,
                OmniRange = 1.9f,
                OmniAttenuation = 1.8f,
                Position = new Vector3(0f, 0.42f, 0f),
                ShadowEnabled = false,
                Visible = false
            };
            root.AddChild(glow);
            _markers[npcId] = glow;

            AddChild(root);
            return root;
        }

        public override void _Process(double delta)
        {
            if (Layout == null || Tracks == null || Session == null || _ids.Count == 0) return;

            int ms = Session.PositionMs;
            HashSet<string> noisy = NoisyRooms();

            // 방마다 몇 명인지 먼저 세어 겹치지 않게 벌려 놓는다.
            var occupants = new Dictionary<string, int>();
            foreach (string id in _ids)
            {
                TrackPosition at = Tracks.SampleAt(id, ms);
                if (at.InTransit || !at.HasPlace) continue;
                occupants.TryGetValue(at.Room, out int n);
                occupants[at.Room] = n + 1;
            }

            var seats = new Dictionary<string, int>();
            float follow = Mathf.Min(1f, (float)delta * FollowLerp);

            foreach (string id in _ids)
            {
                Node3D figure = _figures[id];
                TrackPosition at = Tracks.SampleAt(id, ms);

                if (!TryPlace(at, seats, occupants, out Vector3 target))
                {
                    figure.Visible = false;
                    continue;
                }

                // 처음 나타날 때는 바로 그 자리에, 그 뒤에는 부드럽게 따라간다.
                figure.Position = figure.Visible ? figure.Position.Lerp(target, follow) : target;
                figure.Visible = true;

                _materials[id].AlbedoColor = at.InTransit ? Palette.FigureTransit : Palette.Figure;
                _markers[id].Visible = !at.InTransit && at.HasPlace && noisy.Contains(at.Room);
            }
        }

        /// <summary>
        /// 지금 소리가 나고 있는 방들 — 말과 이벤트 둘 다. 들리는지 여부와는 무관하다
        /// (저쪽 방에서 무언가 일어나고 있다는 것은 화면으로 보여 주고, 내용은 귀로만 준다).
        /// </summary>
        private HashSet<string> NoisyRooms()
        {
            var rooms = new HashSet<string>();
            foreach (Utterance u in Session.Timeline.ActiveAt(Session.PositionMs))
                if (!string.IsNullOrEmpty(u.room)) rooms.Add(u.room);

            if (Session.Events != null)
            {
                foreach (ScriptEvent e in Session.Events.ActiveAt(Session.PositionMs))
                    if (!string.IsNullOrEmpty(e.room)) rooms.Add(e.room);
            }
            return rooms;
        }

        private bool TryPlace(TrackPosition at, Dictionary<string, int> seats,
                              Dictionary<string, int> occupants, out Vector3 where)
        {
            where = Vector3.Zero;

            if (at.InTransit)
            {
                if (!Layout.TryGetRoomCenter(at.FromRoom, out float fx, out float fy)) return false;
                if (!Layout.TryGetRoomCenter(at.ToRoom, out float tx, out float ty)) return false;
                float t = Mathf.Clamp(at.ProgressPermille / 1000f, 0f, 1f);
                where = MansionView3D.ToWorld3(Mathf.Lerp(fx, tx, t), Mathf.Lerp(fy, ty, t));
                return true;
            }

            if (!at.HasPlace) return false;
            if (!Layout.TryGetRoomCenter(at.Room, out float cx, out float cy)) return false;

            occupants.TryGetValue(at.Room, out int total);
            seats.TryGetValue(at.Room, out int seat);
            seats[at.Room] = seat + 1;

            if (total <= 1)
            {
                where = MansionView3D.ToWorld3(cx, cy);
                return true;
            }

            float angle = Mathf.Tau * seat / total - Mathf.Pi * 0.5f;
            where = MansionView3D.ToWorld3(cx + Mathf.Cos(angle) * Spread,
                                           cy + Mathf.Sin(angle) * Spread);
            return true;
        }
    }
}
