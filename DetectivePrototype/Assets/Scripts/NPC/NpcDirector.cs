using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using UnityEngine;

namespace Detective.NPC
{
    /// <summary>
    /// 인물들을 어디에 세울지 정한다. "현재(19:00)" 배치와 타임라인 관찰 배치 두 가지뿐이다.
    /// 스케줄·기록 해석은 순수 C#(NpcSchedule, ITimelinePlacementSource)에 맡기고 여기서는 좌표만 계산한다.
    /// </summary>
    public class NpcDirector : MonoBehaviour
    {
        [Tooltip("타임라인 관찰 중 이동 속도 배율.")]
        public float timelineSpeedMultiplier = 3f;

        [Tooltip("같은 방에 여러 명이 있을 때 서로 떨어뜨리는 간격.")]
        public float slotSpacing = 2.2f;

        private readonly List<NPCController> _controllers = new List<NPCController>();
        private readonly Dictionary<string, NPCController> _byId = new Dictionary<string, NPCController>();
        private CaseDatabase _database;

        public IList<NPCController> Controllers { get { return _controllers.AsReadOnly(); } }

        private void Start()
        {
            if (GameManager.Instance == null) return;
            _database = GameManager.Instance.Database;

            NPCController[] found = GetComponentsInChildren<NPCController>(true);
            for (int i = 0; i < found.Length; i++)
            {
                _controllers.Add(found[i]);
                if (!string.IsNullOrEmpty(found[i].npcId)) _byId[found[i].npcId] = found[i];
            }

            ReturnToPresent(true);
        }

        public bool TryGet(string npcId, out NPCController controller)
        {
            controller = null;
            return !string.IsNullOrEmpty(npcId) && _byId.TryGetValue(npcId, out controller);
        }

        /// <summary>19:00 현재 자리로. 피해자는 시신(조사 대상)으로 따로 있으므로 숨긴다.</summary>
        public void ReturnToPresent(bool instant)
        {
            if (_database == null) return;

            for (int i = 0; i < _database.Npcs.All.Count; i++)
            {
                NpcDefinition npc = _database.Npcs.All[i];
                NPCController controller;
                if (!TryGet(npc.id, out controller)) continue;

                controller.SetCaption(string.Empty);
                string room = NpcSchedule.RoomAt(npc, GameTime.PresentTick);
                float cx, cy;
                if (npc.isVictim || !_database.Layout.TryGetRoomCenter(room, out cx, out cy))
                {
                    controller.SetVisible(false);
                    continue;
                }

                MoveTo(controller, room, cx + npc.presentOffsetX, cy + npc.presentOffsetY, instant);
                controller.SetVisible(true);
            }
        }

        /// <summary>타임라인의 한 시각을 재현한다. 소스가 모른다고 하는 인물은 숨긴다.</summary>
        public void ShowTick(int tick, ITimelinePlacementSource source, bool instant)
        {
            if (_database == null || source == null) return;

            IList<NpcDefinition> all = _database.Npcs.All;
            for (int i = 0; i < all.Count; i++)
            {
                NpcDefinition npc = all[i];
                NPCController controller;
                if (!TryGet(npc.id, out controller)) continue;

                NpcPlacement placement = source.PlacementOf(npc, tick);
                float cx, cy;
                if (!placement.Known || !_database.Layout.TryGetRoomCenter(placement.RoomId, out cx, out cy))
                {
                    controller.SetVisible(false);
                    controller.SetCaption(string.Empty);
                    continue;
                }

                float sx, sy;
                SlotOffset(i, out sx, out sy);

                // 숨어 있다가 나타나는 인물은 이전 위치가 의미 없으므로 바로 그 자리에 놓는다.
                bool appear = !controller.IsVisible;
                MoveTo(controller, placement.RoomId, cx + sx, cy + sy, instant || appear);
                controller.SetVisible(true);
                controller.SetCaption(placement.Caption);
            }
        }

        private void MoveTo(NPCController controller, string roomId, float x, float y, bool instant)
        {
            if (instant || string.IsNullOrEmpty(controller.CurrentRoomId))
            {
                controller.TeleportTo(x, y);
            }
            else
            {
                List<Point2> route = _database.Layout.BuildRoute(controller.CurrentRoomId, roomId, x, y);
                controller.FollowRoute(route, timelineSpeedMultiplier);
            }
            controller.CurrentRoomId = roomId;
        }

        /// <summary>인물 순번별 고정 자리. 윗줄 3명, 아랫줄 나머지.</summary>
        private void SlotOffset(int index, out float x, out float y)
        {
            int column = index % 3;
            int row = index / 3;
            x = (column - 1) * slotSpacing;
            y = row == 0 ? slotSpacing * 0.55f : -slotSpacing * 0.55f;
        }
    }
}
