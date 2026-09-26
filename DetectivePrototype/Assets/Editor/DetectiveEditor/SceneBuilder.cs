using System.Collections.Generic;
using System.IO;
using Detective.Art;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using Detective.Investigation;
using Detective.NPC;
using Detective.Player;
using Detective.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DetectiveEditor
{
    /// <summary>
    /// Main.unity를 코드로 생성한다. 이 프로젝트에서 씬은 손으로 배치하는 산출물이 아니라
    /// rooms.json에서 파생되는 결과물이다 — JSON을 고치고 이 메뉴를 누르면 맵이 바뀐다(§18-2, §18-9).
    ///
    /// batchmode:
    ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;proj&gt;
    ///             -executeMethod DetectiveEditor.SceneBuilder.RebuildMainScene
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenesFolder = "Assets/Scenes";
        public const string ScenePath = ScenesFolder + "/Main.unity";

        // "사건 파일" 연출: 잉크로 그린 벽, 어두운 바닥 질감, 놋쇠 말(플레이어), 종이 표식(단서).
        private static readonly Color WallColor = new Color(0.09f, 0.075f, 0.06f);
        private static readonly Color DoorColor = new Color(0.28f, 0.21f, 0.13f);
        // 바닥을 눌러 앉힌다. 밝은 바닥은 그림자를 지우고 화면을 가볍게 만든다
        // (Godot 2.5D에서 같은 이유로 46%로 내렸다).
        private static readonly Color DefaultFloorColor = new Color(0.26f, 0.235f, 0.205f);
        // 탐정 토큰. 호박색을 죽였다 — 강조색은 "지금 여기"를 가리키는 데만 쓴다.
        private static readonly Color PlayerColor = new Color(0.62f, 0.535f, 0.40f);
        private static readonly Color EvidenceColor = new Color(0.66f, 0.60f, 0.48f);
        private static readonly Color PropColor = new Color(0.24f, 0.19f, 0.14f);
        private static readonly Color CameraBackground = new Color(0.02f, 0.018f, 0.015f);

        private const int SortFloor = -20;
        private const int SortDoor = -15;
        private const int SortWall = -10;
        private const int SortProp = 0;
        private const int SortNpc = 5;
        private const int SortPlayer = 10;

        [MenuItem("Tools/Detective/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SceneBuilder] Play 모드에서는 씬을 다시 만들 수 없다. Play를 멈추고 다시 실행할 것.");
                return;
            }
            // 메뉴에서 실행할 때 열려 있는 씬의 저장 안 된 변경을 말없이 버리지 않는다(batchmode에서는 그냥 통과).
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ArtLibrary.Reset(); // 같은 에디터 세션에서 art.json을 고친 뒤 다시 만들어도 새 값을 읽는다.

            // UI는 씬이 아니라 코드가 만든다(UIFactory). 그래서 씬에 참조가 없어 빌드가 UI 셰이더를 빼 버리고,
            // 플레이어에서 화면이 통째로 마젠타가 된다. 에디터 Play 로는 잡히지 않으니 여기서 못박아 둔다.
            int shaderFixes = RuntimeShaderSetup.Ensure();
            if (shaderFixes > 0)
                Debug.Log("[SceneBuilder] Always Included Shaders를 " + shaderFixes + "건 고쳤다(빌드에서 UI가 마젠타로 나오는 것을 막는다).");

            var errors = new List<string>();
            CaseDatabase database = DataValidator.LoadDatabase(errors);

            if (errors.Count > 0 || database == null)
            {
                for (int i = 0; i < errors.Count; i++) Debug.LogError("[SceneBuilder] " + errors[i]);
                // 예외를 던져야 batchmode가 0이 아닌 종료 코드로 끝나서 실패를 놓치지 않는다.
                throw new System.InvalidOperationException(
                    "[SceneBuilder] 게임 데이터 무결성 오류 " + errors.Count + "건. 씬을 만들지 않았다.");
            }

            RoomLayout layout = database.Layout;
            Sprite square = SpriteAssetFactory.GetOrCreateSquareSprite();
            if (square == null)
            {
                throw new System.InvalidOperationException("[SceneBuilder] 기본 스프라이트를 만들지 못했다.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("GameRoot");
            root.AddComponent<GameManager>();
            root.AddComponent<AudioDirector>();
            var eavesdrop = root.AddComponent<EavesdropController>(); // 대본이 없으면 조용히 비어 있는다.
            var spatial = root.AddComponent<SpatialVoiceDirector>();

            BuildMap(layout, square);
            GameObject player = BuildPlayer(layout, square);
            BuildCamera(player.transform);

            // 거리감. 청취점은 탐정이 선 **점**이다 — 같은 방 안에서도 어디 서 있느냐로 소리가 달라진다.
            // 실제 배선은 런타임에 한다(SpeakerPositions는 대본을 읽은 뒤에야 생긴다).
            var binder = root.AddComponent<SpatialAudioBinder>();
            binder.controller = eavesdrop;
            binder.spatial = spatial;
            binder.listener = player.transform;
            BuildProps(database, square);
            BuildNpcs(layout, database.Npcs.All, square);
            BuildUI(player.GetComponent<PlayerInteraction>());

            Directory.CreateDirectory(ScenesFolder);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                throw new System.InvalidOperationException("[SceneBuilder] " + ScenePath + " 저장 실패.");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] " + ScenePath + " 생성 완료 — 방 " + layout.RoomCount
                + "개, 벽 조각 " + layout.BuildAllWallSegments().Count + "개.");
        }

        // ----- 맵 -------------------------------------------------------------

        private static void BuildMap(RoomLayout layout, Sprite square)
        {
            var mapRoot = new GameObject("Map");
            mapRoot.AddComponent<RoomLabels>(); // 실행 시 바닥에 방 이름을 적는다.

            var floorsRoot = new GameObject("Floors");
            floorsRoot.transform.SetParent(mapRoot.transform, false);

            ArtManifest art = ArtLibrary.Instance.Manifest;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                RoomDefinition room = layout.Rooms[i];
                RoomArt roomArt = art.RoomOf(room.id);
                GameObject floor = CreateFloor(room, roomArt, floorsRoot.transform, square);

                // 방 이름을 하이어라키에서 바로 읽을 수 있게 남긴다.
                floor.name = "Floor_" + room.id + " (" + room.displayName + ")";
            }

            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(mapRoot.transform, false);

            List<WallSegment> walls = layout.BuildAllWallSegments();
            for (int i = 0; i < walls.Count; i++)
            {
                WallSegment wall = walls[i];
                GameObject go = CreateSpriteObject(
                    "Wall_" + i, wallsRoot.transform, square,
                    wall.CenterX, wall.CenterY, wall.Width, wall.Height,
                    WallColor, SortWall);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one; // 스케일이 곧 월드 크기다.
            }

            var doorsRoot = new GameObject("Doors");
            doorsRoot.transform.SetParent(mapRoot.transform, false);

            for (int i = 0; i < layout.Doors.Count; i++)
            {
                DoorDefinition door = layout.Doors[i];
                // 콜라이더 없는 장식. 벽이 뚫린 자리를 눈으로 확인할 수 있게 문턱을 깔아 둔다.
                CreateSpriteObject(
                    "Door_" + door.id, doorsRoot.transform, square,
                    door.CenterX, door.CenterY, door.width, door.height,
                    DoorColor, SortDoor);
            }
        }

        /// <summary>
        /// 바닥 한 장. art.json에 이미지가 있으면 그것을, 없으면 종류별 생성 질감을 Tiled 모드로 깐다.
        /// 질감 색은 tint(없으면 rooms.json의 floorColor)를 곱해 방마다 톤을 달리한다.
        /// </summary>
        private static GameObject CreateFloor(RoomDefinition room, RoomArt roomArt, Transform parent, Sprite square)
        {
            string kind = roomArt != null && !string.IsNullOrEmpty(roomArt.floor) ? roomArt.floor : "wood";
            float tileSize = roomArt != null && roomArt.tileSize > 0.01f ? roomArt.tileSize : 4f;
            Color tint = ParseColor(roomArt != null ? roomArt.tint : null, ParseColor(room.floorColor, DefaultFloorColor));

            Sprite sprite = null;
            if (roomArt != null && !string.IsNullOrEmpty(roomArt.floorTexture))
            {
                sprite = Resources.Load<Sprite>(roomArt.floorTexture);
                if (sprite == null) Debug.LogWarning("[SceneBuilder] " + room.id + ": Resources/" + roomArt.floorTexture + " 이 없어 생성 질감을 쓴다.");
                else if (sprite.texture.wrapMode != TextureWrapMode.Repeat)
                    Debug.LogWarning("[SceneBuilder] " + roomArt.floorTexture + ": Tiled로 깔려면 임포트 설정을 Wrap Mode=Repeat, Mesh Type=Full Rect로 바꿔야 한다.");
            }
            if (sprite == null) sprite = SpriteAssetFactory.GetOrCreateFloorSprite(kind);
            if (sprite == null) sprite = square;

            var go = new GameObject("Floor_" + room.id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(room.CenterX, room.CenterY, 0f);
            go.transform.localScale = new Vector3(tileSize, tileSize, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = SortFloor;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(room.width / tileSize, room.height / tileSize);
            return go;
        }

        // ----- 플레이어 / 카메라 ------------------------------------------------

        private static GameObject BuildPlayer(RoomLayout layout, Sprite square)
        {
            float x, y;
            if (!layout.TryGetSpawnPosition(out x, out y))
            {
                x = 0f;
                y = 0f;
                Debug.LogWarning("[SceneBuilder] 시작 방을 찾지 못해 (0,0)에 배치한다.");
            }

            Sprite disc = SpriteAssetFactory.GetOrCreateDiscSprite() ?? square;
            GameObject player = CreateSpriteObject("Player", null, disc, x, y, 0.8f, 0.8f, PlayerColor, SortPlayer);

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = player.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f; // 로컬 반지름 × 스케일 0.8 = 월드 반지름 0.4

            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerInteraction>();

            // 청취점. 방이 바뀔 때마다 알려서 방 환경음과 엿듣기 가청 판정이 따라오게 한다.
            player.AddComponent<PlayerRoomTracker>();

            // 루트 스케일이 0.8이라 글자표 자식은 그만큼 작아진다. 그걸 감안해 크기를 잡는다.
            var label = player.AddComponent<WorldLabel>();
            label.text = "탐정";
            label.localOffset = new Vector3(0f, 1.05f, 0f);
            label.characterSize = 0.05f;
            label.color = new Color(0.64f, 0.575f, 0.45f);

            return player;
        }

        private static void BuildCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(target.position.x, target.position.y, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraBackground;

            cameraObject.AddComponent<AudioListener>();

            // 후처리. 내장 파이프라인이라 이미지 이펙트로 직접 건다(채도·대비·비네팅·입자).
            // 셰이더가 없으면 원본을 그대로 통과시키므로 화면이 죽지 않는다.
            cameraObject.AddComponent<CameraGrade>();

            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = target;
        }

        // ----- 인물 -------------------------------------------------------------

        /// <summary>
        /// 인물마다 루트(트리거 콜라이더 + NPCController)와 몸통 스프라이트 자식을 만든다.
        /// 루트는 스케일 1로 두어 이름표(TextMesh)가 몸통 크기에 끌려가지 않게 한다.
        /// 최종 위치는 실행 시 NpcDirector가 스케줄을 보고 다시 잡는다.
        /// </summary>
        private static void BuildNpcs(RoomLayout layout, IList<NpcDefinition> npcs, Sprite square)
        {
            var npcsRoot = new GameObject("NPCs");
            npcsRoot.AddComponent<NpcDirector>();
            Sprite disc = SpriteAssetFactory.GetOrCreateDiscSprite() ?? square;
            ArtManifest art = ArtLibrary.Instance.Manifest;

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcDefinition npc = npcs[i];
                NpcArt npcArt = art.NpcOf(npc.id);
                Color tokenColor = ParseColor(npcArt != null ? npcArt.tokenColor : null, ParseColor(npc.color, Color.white));
                string room = NpcSchedule.LastKnownRoom(npc, GameTime.PresentMs);
                float cx, cy;
                if (!layout.TryGetRoomCenter(room, out cx, out cy)) { cx = 0f; cy = 0f; }

                var root = new GameObject("NPC_" + npc.id + " (" + npc.displayName + ")");
                root.transform.SetParent(npcsRoot.transform, false);
                root.transform.position = new Vector3(cx + npc.presentOffsetX, cy + npc.presentOffsetY, 0f);

                // 보드게임 말처럼 보이는 원판. 걷는 애니메이션이 없어도 어색하지 않다.
                CreateSpriteObject("Body", root.transform, disc, 0f, 0f, 0.9f, 0.9f, tokenColor, SortNpc)
                    .transform.localPosition = Vector3.zero;

                var collider = root.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true; // 플레이어를 막지 않고 상호작용 탐색에만 잡힌다.

                // 트랜스폼으로 움직이는 콜라이더는 Kinematic 바디를 달아야 물리 질의(OverlapCircleAll)에 제때 반영된다.
                var body = root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;

                var controller = root.AddComponent<NPCController>();
                controller.npcId = npc.id;
                controller.displayName = npc.displayName;
                controller.isVictim = npc.isVictim;
            }
        }

        // ----- 조사 대상 -------------------------------------------------------

        /// <summary>evidence.json의 단서와 분위기용 소품을 방마다 배치한다.</summary>
        private static void BuildProps(CaseDatabase database, Sprite square)
        {
            var propsRoot = new GameObject("Props");
            RoomLayout layout = database.Layout;

            // 단서는 현장 표식(작은 종이 원판)처럼, 소품은 어두운 가구처럼 보이게 한다.
            Sprite marker = SpriteAssetFactory.GetOrCreateDiscSprite() ?? square;
            IList<EvidenceDefinition> evidence = database.Evidence.All;
            for (int i = 0; i < evidence.Count; i++)
            {
                EvidenceDefinition item = evidence[i];
                InspectableObject inspectable = CreateProp(propsRoot.transform, layout, marker, item.foundRoom,
                    item.offsetX, item.offsetY, item.name, item.description, EvidenceColor, "Evidence_" + item.id);
                if (inspectable != null) inspectable.evidenceId = item.id;
            }

            IList<PropDefinition> props = database.Evidence.Props;
            for (int i = 0; i < props.Count; i++)
            {
                PropDefinition prop = props[i];
                CreateProp(propsRoot.transform, layout, square, prop.room,
                    prop.offsetX, prop.offsetY, prop.name, prop.description, PropColor, "Prop_" + prop.name);
            }
        }

        private static InspectableObject CreateProp(Transform parent, RoomLayout layout, Sprite square,
            string roomId, float offsetX, float offsetY, string displayName, string description, Color color, string objectName)
        {
            float cx, cy;
            if (!layout.TryGetRoomCenter(roomId, out cx, out cy))
            {
                Debug.LogWarning("[SceneBuilder] " + roomId + " 가 없어 '" + displayName + "' 를 배치하지 못했다.");
                return null;
            }

            GameObject prop = CreateSpriteObject(
                objectName, parent, square,
                cx + offsetX, cy + offsetY, 0.7f, 0.7f, color, SortProp);

            var collider = prop.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = true; // 플레이어를 막지 않고 탐색에만 잡힌다.

            var inspectable = prop.AddComponent<InspectableObject>();
            inspectable.displayName = displayName;
            inspectable.description = description;
            inspectable.repeatable = true;
            return inspectable;
        }

        // ----- UI --------------------------------------------------------------

        private static void BuildUI(PlayerInteraction playerInteraction)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<UIFontApplier>();

            // 조사 결과 메시지 패널
            GameObject messagePanel = CreateUIObject("MessagePanel", canvasObject.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 130f), new Vector2(1400f, 210f));
            // 종이 질감은 실행 시 HudUI가 입힌다(런타임 생성 스프라이트는 씬에 남지 않는다).
            var messageBackground = messagePanel.AddComponent<Image>();
            messageBackground.color = UIFactory.PanelColor;
            messageBackground.raycastTarget = false;

            GameObject messageTextObject = CreateUIObject("MessageText", messagePanel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var messageRect = (RectTransform)messageTextObject.transform;
            messageRect.offsetMin = new Vector2(24f, 16f);
            messageRect.offsetMax = new Vector2(-24f, -16f);
            Text messageText = CreateText(messageTextObject, 28, TextAnchor.MiddleLeft, UIFactory.Ink);

            // 상호작용 안내문
            GameObject promptObject = CreateUIObject("PromptLabel", canvasObject.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 50f), new Vector2(1200f, 60f));
            Text promptText = CreateText(promptObject, 34, TextAnchor.MiddleCenter, UIFactory.Cream);

            canvasObject.AddComponent<TimelineController>();
            canvasObject.AddComponent<NotebookUI>();
            canvasObject.AddComponent<EavesdropUI>(); // 발화 버블. 컨트롤러는 실행 시 스스로 찾는다.
            canvasObject.AddComponent<DialogueUI>();
            canvasObject.AddComponent<AccusationUI>();
            canvasObject.AddComponent<IntroUI>();

            var hud = canvasObject.AddComponent<HudUI>();
            hud.player = playerInteraction;
            hud.promptLabel = promptText;
            hud.messageLabel = messageText;
            hud.messageBackground = messageBackground;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static GameObject CreateUIObject(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return go;
        }

        private static Text CreateText(GameObject target, int fontSize, TextAnchor alignment, Color color)
        {
            var text = target.AddComponent<Text>();
            // 씬에 저장 가능한 내장 폰트. 한글 글리프는 없으므로 실행 시 UIFontApplier가 OS 폰트로 바꾼다.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = string.Empty;
            return text;
        }

        // ----- 공통 -------------------------------------------------------------

        private static GameObject CreateSpriteObject(string name, Transform parent, Sprite sprite,
            float centerX, float centerY, float width, float height, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            go.transform.position = new Vector3(centerX, centerY, 0f);
            go.transform.localScale = new Vector3(width, height, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            return go;
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;

            Color parsed;
            if (ColorUtility.TryParseHtmlString(hex, out parsed)) return parsed;

            Debug.LogWarning("[SceneBuilder] 색을 해석하지 못했다: " + hex);
            return fallback;
        }
    }
}
