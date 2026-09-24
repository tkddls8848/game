using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
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

        private static readonly Color WallColor = new Color(0.16f, 0.17f, 0.21f);
        private static readonly Color DoorColor = new Color(0.62f, 0.55f, 0.35f);
        private static readonly Color DefaultFloorColor = new Color(0.24f, 0.26f, 0.30f);
        private static readonly Color PlayerColor = new Color(0.95f, 0.83f, 0.35f);
        private static readonly Color EvidenceColor = new Color(0.90f, 0.42f, 0.45f);
        private static readonly Color PropColor = new Color(0.55f, 0.62f, 0.72f);

        private const int SortFloor = -20;
        private const int SortDoor = -15;
        private const int SortWall = -10;
        private const int SortProp = 0;
        private const int SortNpc = 5;
        private const int SortPlayer = 10;

        [MenuItem("Tools/Detective/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
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

            BuildMap(layout, square);
            GameObject player = BuildPlayer(layout, square);
            BuildCamera(player.transform);
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

            var floorsRoot = new GameObject("Floors");
            floorsRoot.transform.SetParent(mapRoot.transform, false);

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                RoomDefinition room = layout.Rooms[i];
                GameObject floor = CreateSpriteObject(
                    "Floor_" + room.id, floorsRoot.transform, square,
                    room.CenterX, room.CenterY, room.width, room.height,
                    ParseColor(room.floorColor, DefaultFloorColor), SortFloor);

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

            GameObject player = CreateSpriteObject("Player", null, square, x, y, 0.8f, 0.8f, PlayerColor, SortPlayer);

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
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);

            cameraObject.AddComponent<AudioListener>();

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

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcDefinition npc = npcs[i];
                string room = NpcSchedule.LastKnownRoom(npc, GameTime.PresentTick);
                float cx, cy;
                if (!layout.TryGetRoomCenter(room, out cx, out cy)) { cx = 0f; cy = 0f; }

                var root = new GameObject("NPC_" + npc.id + " (" + npc.displayName + ")");
                root.transform.SetParent(npcsRoot.transform, false);
                root.transform.position = new Vector3(cx + npc.presentOffsetX, cy + npc.presentOffsetY, 0f);

                CreateSpriteObject("Body", root.transform, square, 0f, 0f, 0.9f, 0.9f,
                    ParseColor(npc.color, Color.white), SortNpc).transform.localPosition = Vector3.zero;

                var collider = root.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true; // 플레이어를 막지 않고 상호작용 탐색에만 잡힌다.

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

            IList<EvidenceDefinition> evidence = database.Evidence.All;
            for (int i = 0; i < evidence.Count; i++)
            {
                EvidenceDefinition item = evidence[i];
                InspectableObject inspectable = CreateProp(propsRoot.transform, layout, square, item.foundRoom,
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
            var messageBackground = messagePanel.AddComponent<Image>();
            messageBackground.color = new Color(0f, 0f, 0f, 0.72f);
            messageBackground.raycastTarget = false;

            GameObject messageTextObject = CreateUIObject("MessageText", messagePanel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var messageRect = (RectTransform)messageTextObject.transform;
            messageRect.offsetMin = new Vector2(24f, 16f);
            messageRect.offsetMax = new Vector2(-24f, -16f);
            Text messageText = CreateText(messageTextObject, 28, TextAnchor.MiddleLeft, Color.white);

            // 상호작용 안내문
            GameObject promptObject = CreateUIObject("PromptLabel", canvasObject.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 50f), new Vector2(1200f, 60f));
            Text promptText = CreateText(promptObject, 34, TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.7f));

            canvasObject.AddComponent<TimelineController>();
            canvasObject.AddComponent<NotebookUI>();

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
