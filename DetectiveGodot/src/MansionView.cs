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
        public ArtManifest Art;
        public bool SonarMode;

        private static readonly Color Ink = Palette.Ink;
        private static readonly Color WallColor = Palette.Wall;
        private static readonly Color DoorColor = Palette.Door;
        private static readonly Color Amber = Palette.Lamp;
        private static readonly Color Muffled = Palette.Muffled;
        private static readonly Color SonarOutline = new Color(0.175f, 0.170f, 0.190f);

        private List<WallSegment> _walls;
        private readonly Dictionary<string, RoomArt> _roomArt = new Dictionary<string, RoomArt>();
        private readonly Dictionary<string, Texture2D> _floors = new Dictionary<string, Texture2D>();
        private Font _font;

        public override void _Ready()
        {
            _walls = Layout.BuildAllWallSegments();
            _font = KoreanFont.Load();

            // 바닥 질감은 반복해서 깔아야 하므로 이 노드에 반복을 켠다.
            TextureRepeat = TextureRepeatEnum.Enabled;

            if (Art == null) return;
            Art.Normalized();
            foreach (RoomArt art in Art.rooms)
            {
                if (string.IsNullOrEmpty(art.roomId)) continue;
                _roomArt[art.roomId] = art;
                if (string.IsNullOrEmpty(art.floorTexture)) continue;

                // art.json은 확장자 없는 Resources 경로를 적는다. 실제 파일을 찾아 붙인다.
                foreach (string ext in new[] { ".jpg", ".png" })
                {
                    string path = AudioDirector.MediaRoot + art.floorTexture + ext;
                    if (!ResourceLoader.Exists(path)) continue;
                    _floors[art.roomId] = ResourceLoader.Load<Texture2D>(path);
                    break;
                }
            }
        }

        public override void _ExitTree()
        {
            // 붙들고 있던 질감·폰트를 놓는다. 노드가 사라져도 필드가 남아 있으면 리소스가 산다.
            _floors.Clear();
            _roomArt.Clear();
            _font = null;
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
                DrawFloor(room);

            // 문은 벽보다 먼저 — 벽 조각이 문 자리를 비워 두므로 겹치지 않는다.
            foreach (DoorDefinition door in Layout.Doors)
                DrawRect(RectOf(door.x, door.y, door.width, door.height), DoorColor);

            foreach (WallSegment wall in _walls)
                DrawRect(CenteredRect(wall), WallColor);

            DrawRoomNames();
        }

        /// <summary>
        /// 방 바닥. art.json에 질감이 있으면 <c>tileSize</c>(월드 유닛)만큼의 크기로 반복해 깔고
        /// <c>tint</c>로 톤을 맞춘다. 질감이 없으면 rooms.json의 단색으로 물러선다
        /// (연출 에셋 규칙: 파일이 없으면 대체물을 쓰고 멈추지 않는다).
        /// </summary>
        private void DrawFloor(RoomDefinition room)
        {
            Rect2 rect = RectOf(room.x, room.y, room.width, room.height);

            if (!_floors.TryGetValue(room.id, out Texture2D texture) || texture == null)
            {
                DrawRect(rect, FloorColorOf(room));
                return;
            }

            RoomArt art = _roomArt[room.id];
            float tileUnits = art.tileSize > 0.01f ? art.tileSize : 4f;
            // 질감은 자기 픽셀 크기대로 반복된다. 무늬 한 칸이 tileUnits 유닛으로 보이게 하려면
            // 좌표계를 줄여 놓고 그린 뒤 되돌린다 — 반복 간격을 직접 지정할 방법이 없다.
            float scale = tileUnits * Main.Ppu / texture.GetWidth();
            if (scale <= 0f) { DrawRect(rect, FloorColorOf(room)); return; }

            Color tint = TintOf(art);
            DrawSetTransform(Vector2.Zero, 0f, new Vector2(scale, scale));
            DrawTextureRect(texture, new Rect2(rect.Position / scale, rect.Size / scale), true, tint);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        private static Color TintOf(RoomArt art)
        {
            if (!string.IsNullOrEmpty(art.tint))
            {
                string hex = art.tint.StartsWith("#") ? art.tint.Substring(1) : art.tint;
                if (hex.Length == 6 || hex.Length == 8) return Color.FromHtml(art.tint);
            }
            return Colors.White;
        }

        /// <summary>
        /// 방 이름. 이름은 퍼즐이 아니므로 그냥 보여 준다 — 어디가 서재인지 모르면
        /// "서재에서 들린 말"이라는 단서를 쓸 수 없다.
        /// </summary>
        private void DrawRoomNames()
        {
            if (_font == null) return;
            foreach (RoomDefinition room in Layout.Rooms)
            {
                string name = Layout.DisplayNameOf(room.id);
                if (string.IsNullOrEmpty(name)) continue;
                Vector2 at = Main.ToPx(room.CenterX, room.MaxY) + new Vector2(0f, 22f);
                Vector2 size = _font.GetStringSize(name, HorizontalAlignment.Center, -1f,
                                                   Palette.SizeRoomName);
                DrawString(_font, at - new Vector2(size.X * 0.5f, 0f), name,
                           HorizontalAlignment.Left, -1f, Palette.SizeRoomName,
                           new Color(Palette.Paper, 0.58f));
            }
        }

        private static Color FloorColorOf(RoomDefinition room)
        {
            if (!string.IsNullOrEmpty(room.floorColor))
            {
                // Godot은 잘못된 문자열에 경고를 내고 흰색을 주므로 미리 형태를 본다.
                string hex = room.floorColor.StartsWith("#") ? room.floorColor.Substring(1) : room.floorColor;
                if (hex.Length == 6 || hex.Length == 8) return Color.FromHtml(room.floorColor);
            }
            return Palette.Ink;
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

            // 어디가 어느 방인지는 알려 준다. 모르면 "서재에서 들렸다"는 단서를 쓸 수 없다.
            DrawRoomNames();

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
