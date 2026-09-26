using System.Collections.Generic;
using Detective.Core;
using Detective.Eavesdrop;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 저택 안을 돌아다니는 사람들. 위치는 <c>tracks.json</c>(공유 <see cref="MovementTracks"/>)이
    /// 정하고, 회차 재생 위치(ms)로 샘플링한다 — 정지하면 사람도 멈춘다.
    ///
    /// **이름을 붙이지 않는다.** 누가 어느 방에 있었는지가 바로 플레이어가 목소리로 알아내야 하는
    /// 것이다. 토큰에 "클라라"라고 적으면 추리가 사라진다. 그래서 보이는 것은 정체 모를 그림자뿐이고,
    /// 방마다 몇 사람이 있는지·언제 움직였는지만 알 수 있다. 목소리와 사람을 맞추는 일은
    /// 여전히 귀로 해야 한다.
    ///
    /// 한 방에 여럿이 있으면 겹치지 않게 방 중심 주변으로 벌려 놓는다. 이동 중인 사람은
    /// <see cref="TrackPosition.ProgressPermille"/>로 두 방 중심 사이를 보간해 실제로 걸어간다.
    /// 지금 말하고 있는 사람은 테두리가 밝아진다 — 누가 말하는지는 이미 소리로 아는 정보라
    /// 알려 줘도 퍼즐이 깨지지 않고, 어느 그림자가 그 목소리인지 붙여 보는 재미가 생긴다.
    /// </summary>
    public partial class NpcTokens : Node2D
    {
        public RoomLayout Layout;
        public MovementTracks Tracks;
        public ListeningSession Session;

        private static readonly Color Figure = new Color(0.72f, 0.70f, 0.66f);
        private static readonly Color FigureTransit = new Color(0.55f, 0.54f, 0.52f);
        private static readonly Color SpeakingRing = new Color(0.91f, 0.69f, 0.42f);

        private const float Radius = 0.22f * Main.Ppu;
        private const float Spread = 0.42f * Main.Ppu;

        private readonly List<string> _ids = new List<string>();

        public override void _Ready()
        {
            if (Tracks == null) return;
            foreach (MovementTrack track in Tracks.All)
                if (!string.IsNullOrEmpty(track.npcId)) _ids.Add(track.npcId);
            _ids.Sort();   // 그리는 순서를 고정한다
        }

        public override void _Draw()
        {
            if (Layout == null || Tracks == null || Session == null || _ids.Count == 0) return;
            int ms = Session.PositionMs;

            // 방마다 지금 몇 명인지 먼저 세어, 겹치지 않게 벌려 놓을 자리를 정한다.
            var seatsTaken = new Dictionary<string, int>();
            var occupants = new Dictionary<string, int>();
            foreach (string id in _ids)
            {
                TrackPosition at = Tracks.SampleAt(id, ms);
                if (at.InTransit || !at.HasPlace) continue;
                occupants.TryGetValue(at.Room, out int n);
                occupants[at.Room] = n + 1;
            }

            HashSet<string> speaking = SpeakingRooms();

            foreach (string id in _ids)
            {
                TrackPosition at = Tracks.SampleAt(id, ms);
                if (!TryPlace(at, seatsTaken, occupants, out Vector2 where)) continue;

                bool transit = at.InTransit;
                DrawCircle(where, Radius, transit ? FigureTransit : Figure);

                // 그림자에 발이 있는 것처럼 보이도록 아래로 살짝 늘린 꼬리.
                DrawLine(where + new Vector2(0, Radius * 0.6f),
                         where + new Vector2(0, Radius * 1.5f),
                         new Color(transit ? FigureTransit : Figure, 0.45f), 2.0f);

                // 이 방에서 지금 누군가 말하고 있다면 테두리를 밝힌다.
                if (!transit && at.HasPlace && speaking.Contains(at.Room))
                    DrawArc(where, Radius + 3f, 0f, Mathf.Tau, 24, SpeakingRing, 1.8f);
            }
        }

        /// <summary>지금 발화가 울리고 있는 방들. 들리는지 여부와 무관하게 "말이 나고 있는" 방이다.</summary>
        private HashSet<string> SpeakingRooms()
        {
            var rooms = new HashSet<string>();
            foreach (Utterance utterance in Session.Timeline.ActiveAt(Session.PositionMs))
                if (!string.IsNullOrEmpty(utterance.room)) rooms.Add(utterance.room);
            return rooms;
        }

        /// <summary>
        /// 화면 위 자리를 구한다. 머물러 있으면 방 중심에서 벌린 자리, 이동 중이면 두 방 중심
        /// 사이를 진행도로 보간한 지점.
        /// </summary>
        private bool TryPlace(TrackPosition at, Dictionary<string, int> seatsTaken,
                              Dictionary<string, int> occupants, out Vector2 where)
        {
            where = Vector2.Zero;

            if (at.InTransit)
            {
                if (!Layout.TryGetRoomCenter(at.FromRoom, out float fx, out float fy)) return false;
                if (!Layout.TryGetRoomCenter(at.ToRoom, out float tx, out float ty)) return false;
                float t = Mathf.Clamp(at.ProgressPermille / 1000f, 0f, 1f);
                where = Main.ToPx(Mathf.Lerp(fx, tx, t), Mathf.Lerp(fy, ty, t));
                return true;
            }

            if (!at.HasPlace) return false;
            if (!Layout.TryGetRoomCenter(at.Room, out float cx, out float cy)) return false;

            Vector2 center = Main.ToPx(cx, cy);
            occupants.TryGetValue(at.Room, out int total);
            seatsTaken.TryGetValue(at.Room, out int seat);
            seatsTaken[at.Room] = seat + 1;

            if (total <= 1) { where = center; return true; }

            // 방 중심을 둘러싸고 고르게 벌린다. 사람 수가 같으면 자리도 늘 같다.
            float angle = Mathf.Tau * seat / total - Mathf.Pi * 0.5f;
            where = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Spread;
            return true;
        }
    }
}
