using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 저택을 3D로 세운다. 2.5D — 기하는 3D지만 카메라는 직교이고 기울어져 고정이다.
    ///
    /// 왜 2.5D인가: 이 게임은 평면도를 내려다보며 소리를 좇는 게임이다. 1인칭으로 들어가면
    /// 방 너머를 볼 수 없어 "저쪽 방에서 누가 움직인다"가 사라진다. 직교 + 기울임은
    /// 평면도의 한눈에 보임을 지키면서 벽에 높이를 줘 공간감을 얻는다.
    ///
    /// **같은 rooms.json을 쓴다.** 2D판과 좌표가 한 곳에서 나오므로 두 화면이 어긋날 수 없다.
    /// 공유 <see cref="RoomLayout.BuildAllWallSegments"/>가 만든 벽 조각을 그대로 박스로 세운다 —
    /// 문이 빠진 자리가 3D에서도 그대로 구멍이 된다.
    ///
    /// 좌표 변환: 공유 데이터는 (x, y) 평면에 Y가 위로 자란다. 3D에서는 바닥 평면이 (x, z)이고
    /// y는 높이다. 그래서 <c>world(x, y) → godot(x, 0, -y)</c>다. 부호를 뒤집는 곳은
    /// <see cref="ToWorld3"/> 한 곳뿐이다.
    /// </summary>
    public partial class MansionView3D : Node3D
    {
        public RoomLayout Layout;
        public ArtManifest Art;

        /// <summary>벽 높이(월드 유닛). 낮게 둔다 — 높으면 위에서 방 안이 안 보인다.</summary>
        public const float WallHeight = 0.9f;

        /// <summary>카메라 기울기. 45°는 방 안이 안 보이고 90°는 평면도와 같다. 그 사이를 고른다.</summary>
        private const float CameraPitchDegrees = 58f;

        /// <summary>바닥 질감을 눌러 앉히는 계수. 밝은 바닥은 그림자를 지우고 화면을 가볍게 만든다.</summary>
        private static readonly Color FloorDim = new Color(0.46f, 0.45f, 0.44f);


        private Camera3D _camera;

        /// <summary>월드 평면 좌표 → 3D. Y 뒤집기는 여기 한 곳에만 있다.</summary>
        public static Vector3 ToWorld3(float worldX, float worldY, float height = 0f)
        {
            return new Vector3(worldX, height, -worldY);
        }

        public override void _Ready()
        {
            if (Layout == null) return;
            BuildFloors();
            BuildWalls();
            BuildDoors();
            BuildLighting();
            BuildCamera();
        }

        // ── 바닥 ──────────────────────────────────────────────

        private void BuildFloors()
        {
            var roomArt = new Dictionary<string, RoomArt>();
            if (Art != null)
            {
                Art.Normalized();
                foreach (RoomArt a in Art.rooms)
                    if (!string.IsNullOrEmpty(a.roomId)) roomArt[a.roomId] = a;
            }

            foreach (RoomDefinition room in Layout.Rooms)
            {
                var plane = new MeshInstance3D
                {
                    Name = "Floor_" + room.id,
                    Mesh = new PlaneMesh { Size = new Vector2(room.width, room.height) },
                    Position = ToWorld3(room.CenterX, room.CenterY)
                };

                var material = new StandardMaterial3D { Roughness = 0.95f };
                RoomArt art;
                Texture2D texture = roomArt.TryGetValue(room.id, out art) ? LoadFloor(art) : null;
                if (texture != null)
                {
                    material.AlbedoTexture = texture;
                    material.Uv1Scale = TileScale(room, art);
                    // art.json의 tint는 2D 화면 기준으로 밝다. 3D에서는 빛이 따로 있으므로
                    // 그대로 쓰면 바닥이 떠 보인다. 눌러 앉힌다.
                    material.AlbedoColor = ParseColor(art.tint, Colors.White) * FloorDim;
                }
                else
                {
                    material.AlbedoColor = ParseColor(room.floorColor, Palette.Ink);
                }
                plane.MaterialOverride = material;
                AddChild(plane);
            }
        }

        /// <summary>
        /// art.json의 <c>tileSize</c>(월드 유닛당 무늬 한 칸)를 UV 반복수로 옮긴다.
        /// 방이 커지면 반복이 늘어야 무늬 크기가 일정하게 보인다.
        /// </summary>
        private static Vector3 TileScale(RoomDefinition room, RoomArt art)
        {
            float tile = art != null && art.tileSize > 0.01f ? art.tileSize : 4f;
            return new Vector3(room.width / tile, room.height / tile, 1f);
        }

        private static Texture2D LoadFloor(RoomArt art)
        {
            if (art == null || string.IsNullOrEmpty(art.floorTexture)) return null;
            foreach (string ext in new[] { ".jpg", ".png" })
            {
                string path = AudioDirector.MediaRoot + art.floorTexture + ext;
                if (ResourceLoader.Exists(path)) return ResourceLoader.Load<Texture2D>(path);
            }
            return null;
        }

        // ── 벽 ────────────────────────────────────────────────

        /// <summary>
        /// 벽은 손으로 두지 않는다. 공유 로직이 방 테두리에서 문을 빼고 병합해 준 조각을
        /// 그대로 세운다 — 2D판과 같은 목록이므로 두 화면의 벽이 어긋날 수 없다.
        /// </summary>
        private void BuildWalls()
        {
            var walls = new Node3D { Name = "Walls" };
            AddChild(walls);

            var material = new StandardMaterial3D
            {
                AlbedoColor = Palette.Wall,
                Roughness = 0.9f
            };

            foreach (WallSegment segment in Layout.BuildAllWallSegments())
            {
                var box = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(segment.Width, WallHeight, segment.Height) },
                    Position = ToWorld3(segment.CenterX, segment.CenterY, WallHeight * 0.5f),
                    MaterialOverride = material
                };
                walls.AddChild(box);

                // 윗면만 밝게 — 위에서 내려다보므로 이것이 벽의 윤곽을 만든다.
                var cap = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(segment.Width, 0.06f, segment.Height) },
                    Position = ToWorld3(segment.CenterX, segment.CenterY, WallHeight + 0.03f),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.WallCap, Roughness = 0.85f }
                };
                walls.AddChild(cap);
            }
        }

        /// <summary>문은 바닥에 낮은 문턱으로 남긴다 — 어디가 통로인지 보여야 한다.</summary>
        private void BuildDoors()
        {
            var doors = new Node3D { Name = "Doors" };
            AddChild(doors);
            var material = new StandardMaterial3D { AlbedoColor = Palette.Door, Roughness = 0.9f };

            foreach (DoorDefinition door in Layout.Doors)
            {
                doors.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(door.width, 0.05f, door.height) },
                    Position = ToWorld3(door.CenterX, door.CenterY, 0.025f),
                    MaterialOverride = material
                });
            }
        }

        // ── 빛 ────────────────────────────────────────────────

        /// <summary>
        /// 무게는 빛에서 나온다. 처음 구현이 장난감처럼 보인 가장 큰 원인이 여기였다 —
        /// 환경광을 0.75로 올려 놓아 <b>그림자가 전부 씻겨 나갔다</b>. 그림자 없는 3D는
        /// 납작하고, 납작하면 가볍다.
        ///
        /// 바꾼 것 넷:
        ///   * 환경광을 0.75 → 0.16으로 내렸다. 이제 달빛이 실제로 그림자를 만든다.
        ///   * <b>안개</b>를 넣었다. 어두운 저택에는 공기가 있어야 한다. 거리에 따라 방이
        ///     묻히면서 깊이가 생기고, 이것 하나가 인상을 가장 크게 바꾼다.
        ///   * 필름 톤매핑 + 채도 내림. 색이 튀지 않고 사진처럼 앉는다.
        ///   * 약한 글로우. 등불이 번지되 빛나지는 않게.
        /// </summary>
        private void BuildLighting()
        {
            var moon = new DirectionalLight3D
            {
                Name = "Moonlight",
                LightEnergy = 1.15f,
                LightColor = new Color(0.72f, 0.78f, 0.98f),
                ShadowEnabled = true,
                ShadowBlur = 1.6f,
                DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
                DirectionalShadowMaxDistance = 90f
            };
            moon.RotationDegrees = new Vector3(-58f, -34f, 0f);
            AddChild(moon);

            var environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Palette.Void,

                // 그림자를 살리기 위해 환경광을 바닥까지 내린다.
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.20f, 0.21f, 0.26f),
                AmbientLightEnergy = 0.16f,

                // 공기. 저택이 거리에 따라 묻힌다.
                FogEnabled = true,
                FogLightColor = new Color(0.115f, 0.120f, 0.145f),
                FogLightEnergy = 1.0f,
                FogDensity = 0.022f,
                FogSkyAffect = 0f,

                // 사진처럼 앉히기. 채도를 빼고 대비를 살짝 올린다.
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapExposure = 1.05f,
                TonemapWhite = 6f,
                AdjustmentEnabled = true,
                AdjustmentSaturation = 0.72f,
                AdjustmentContrast = 1.10f,
                AdjustmentBrightness = 1.0f,

                // 등불이 번지는 정도. 빛나 보이면 안 된다.
                GlowEnabled = true,
                GlowIntensity = 0.32f,
                GlowBloom = 0.10f,
                GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
                GlowHdrThreshold = 1.05f
            };
            AddChild(new WorldEnvironment { Name = "Env", Environment = environment });
        }

        // ── 카메라 ────────────────────────────────────────────

        /// <summary>
        /// 직교 카메라. 저택 전체가 들어오도록 크기를 데이터에서 계산한다 —
        /// rooms.json이 바뀌어도 화면을 다시 맞출 필요가 없다.
        /// </summary>
        private void BuildCamera()
        {
            _camera = new Camera3D
            {
                Name = "Camera",
                Projection = Camera3D.ProjectionType.Orthogonal
            };

            if (!Layout.TryGetBounds(out float minX, out float minY, out float maxX, out float maxY))
            {
                AddChild(_camera);
                _camera.MakeCurrent();
                return;
            }

            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            float span = Mathf.Max(maxX - minX, maxY - minY);

            // 기울여 보면 세로가 코사인만큼 눌리므로 그만큼 여유를 더 준다.
            float pitch = Mathf.DegToRad(CameraPitchDegrees);
            _camera.Size = span * 1.25f / Mathf.Max(0.35f, Mathf.Sin(pitch));

            float distance = span * 1.6f;
            Vector3 eye = ToWorld3(centerX, centerY)
                        + new Vector3(0f, distance * Mathf.Sin(pitch), distance * Mathf.Cos(pitch));

            // 트리에 먼저 넣는다 — LookAt은 트리 밖 노드에서 쓸 수 없다.
            AddChild(_camera);
            _camera.LookAtFromPosition(eye, ToWorld3(centerX, centerY), Vector3.Up);
            _camera.MakeCurrent();
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            string body = hex.StartsWith("#") ? hex.Substring(1) : hex;
            return body.Length == 6 || body.Length == 8 ? Color.FromHtml(hex) : fallback;
        }
    }
}
