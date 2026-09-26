using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 저택 평면도를 그린다. 두 모드를 한 노드가 맡는다.
    ///
    ///   탐색 모드 — 방 바닥과 벽. 어두운 저택 도면.
    ///   청취(소나) 모드 — 저택은 어둠에 가라앉고 소리만 파문으로 보인다. 귀가 선 방은 호박색,
    ///                     벽 너머는 회색, 그 밖은 무음이라 아무것도 그리지 않는다.
    ///
    /// 그릴 내용은 전부 공유 로직에서 온다 — <see cref="RoomLayout"/>(방·벽)과
    /// <see cref="ListeningSession"/>(지금 무엇이 어떻게 들리는가). 이 클래스는 좌표 변환과
    /// 색만 안다. 즉 Unity 판의 <c>SonarView</c>와 **같은 판정 결과**를 다른 방식으로 그린다.
    ///
    /// 좌표계 주의: 공유 데이터는 Y가 위로 자라는 월드 유닛이고 (x, y)는 좌하단 모서리다.
    /// Godot 2D는 Y가 아래로 자라는 픽셀이다. 변환은 <see cref="Main.ToPx"/> 한 곳에만 둔다.
    /// </summary>
    public partial class MansionView : Node2D
    {
        public RoomLayout Layout;
        public ListeningSession Session;
        public bool SonarMode;

        private static readonly Color Ink = new Color(0.08f, 0.08f, 0.10f);
        private static readonly Color WallColor = new Color(0.16f, 0.14f, 0.13f);
        private static readonly Color DoorColor = new Color(0.45f, 0.33f, 0.20f);
        private static readonly Color Amber = new Color(0.91f, 0.69f, 0.42f);
        private static readonly Color Muffled = new Color(0.42f, 0.42f, 0.45f);
        private static readonly Color SonarOutline = new Color(0.22f, 0.22f, 0.26f);

        private List<WallSegment> _walls;

        public override void _Ready()
        {
            _walls = Layout.BuildAllWallSegments();
        }

        public override void _Draw()
        {
            if (Layout == null) return;
            if (SonarMode) DrawSonar();
            else DrawFloorplan();
        }

        // ── 탐색 모드 ──────────────────────────────────────────

        private void DrawFloorplan()
        {
            foreach (RoomDefinition room in Layout.Rooms)
                DrawRect(RectOf(room.x, room.y, room.width, room.height), FloorColorOf(room));

            // 문은 벽보다 먼저 — 벽 조각이 문 자리를 비워 두므로 겹치지 않는다.
            foreach (DoorDefinition door in Layout.Doors)
                DrawRect(RectOf(door.x, door.y, door.width, door.height), DoorColor);

            foreach (WallSegment wall in _walls)
                DrawRect(CenteredRect(wall), WallColor);
        }

        private static Color FloorColorOf(RoomDefinition room)
        {
            if (!string.IsNullOrEmpty(room.floorColor))
            {
                // Godot은 잘못된 문자열에 경고를 내고 흰색을 주므로 미리 형태를 본다.
                string hex = room.floorColor.StartsWith("#") ? room.floorColor.Substring(1) : room.floorColor;
                if (hex.Length == 6 || hex.Length == 8) return Color.FromHtml(room.floorColor);
            }
            return new Color(0.15f, 0.13f, 0.12f);
        }

        // ── 청취(소나) 모드 ────────────────────────────────────

        private void DrawSonar()
        {
            string ear = Session != null ? Session.ListenerRoom : string.Empty;

            // 저택 윤곽만 남긴다. 바닥은 그리지 않는다 — 보는 게임이 아니라 듣는 게임이다.
            foreach (RoomDefinition room in Layout.Rooms)
            {
                Rect2 rect = RectOf(room.x, room.y, room.width, room.height);
                bool isEar = room.id == ear;
                DrawRect(rect, new Color(Ink, isEar ? 0.85f : 0.55f));
                DrawRect(rect, isEar ? Amber : SonarOutline, false, isEar ? 2.5f : 1.0f);
            }

            if (Session == null) return;

            // 지금 들리는 발화마다 그 방에서 파문이 번진다. 파문 위상은 재생 위치(ms)에 묶여
            // 있어서 정지하면 같이 멈춘다 — 화면이 시간을 정직하게 보여 준다.
            foreach (PerceivedUtterance heard in Session.Current)
            {
                if (heard.Level == Audibility.None) continue;
                if (!Layout.TryGetRoomCenter(heard.Room, out float cx, out float cy)) continue;

                Vector2 center = Main.ToPx(cx, cy);
                bool full = heard.Level == Audibility.Full;
                Color color = full ? Amber : Muffled;
                const int rings = 3;
                const float maxRadius = 2.2f * Main.Ppu;
                for (int ring = 0; ring < rings; ring++)
                {
                    // progress 0 = 중심에서 막 출발, 1 = 사라짐. 반지름은 비례해 커지고 알파는 반대로 준다.
                    if (!SonarText.RipplePhase(Session.PositionMs, 1200, ring, rings, out float progress))
                        continue;
                    float alpha = (1f - progress) * (full ? 0.9f : 0.45f);
                    DrawArc(center, progress * maxRadius, 0f, Mathf.Tau, 48,
                            new Color(color, alpha), full ? 2.0f : 1.2f);
                }
            }
        }

        // ── 좌표 변환 ──────────────────────────────────────────

        /// <summary>월드 좌하단 기준 사각형 → Godot 픽셀 사각형(Y 뒤집기).</summary>
        private static Rect2 RectOf(float x, float y, float w, float h)
        {
            Vector2 topLeft = Main.ToPx(x, y + h);
            return new Rect2(topLeft, new Vector2(w * Main.Ppu, h * Main.Ppu));
        }

        private static Rect2 CenteredRect(WallSegment wall)
        {
            return RectOf(wall.CenterX - wall.Width * 0.5f,
                          wall.CenterY - wall.Height * 0.5f,
                          wall.Width, wall.Height);
        }
    }
}
