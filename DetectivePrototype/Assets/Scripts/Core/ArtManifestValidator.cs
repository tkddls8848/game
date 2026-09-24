using System.Collections.Generic;
using Detective.Data;

namespace Detective.Core
{
    /// <summary>art.json 참조 검사(순수 C#). 없는 방·인물·단서를 가리키거나 모르는 바닥 종류를 쓰면 잡아낸다.</summary>
    public static class ArtManifestValidator
    {
        public static readonly string[] FloorKinds = { "wood", "marble", "carpet", "stone", "rug" };

        public static List<string> Validate(ArtManifest manifest, CaseDatabase database)
        {
            var errors = new List<string>();
            if (manifest == null)
            {
                errors.Add("art.json을 읽지 못했다.");
                return errors;
            }
            manifest.Normalized();

            var seenRooms = new HashSet<string>();
            for (int i = 0; i < manifest.rooms.Length; i++)
            {
                RoomArt room = manifest.rooms[i];
                string label = "art.rooms[" + i + "]";
                RoomDefinition definition;
                if (!database.Layout.TryGetRoom(room.roomId, out definition)) errors.Add(label + ": roomId '" + room.roomId + "' 이 없는 방이다.");
                else if (!seenRooms.Add(room.roomId)) errors.Add(label + ": roomId '" + room.roomId + "' 가 중복된다.");

                if (!string.IsNullOrEmpty(room.floor) && System.Array.IndexOf(FloorKinds, room.floor) < 0)
                    errors.Add(label + ": floor '" + room.floor + "' 는 모르는 종류다(" + string.Join("/", FloorKinds) + ").");
                if (room.tileSize <= 0f) errors.Add(label + ": tileSize는 0보다 커야 한다.");
                if (!string.IsNullOrEmpty(room.tint) && !LooksLikeHexColor(room.tint)) errors.Add(label + ": tint '" + room.tint + "' 는 #RRGGBB 형식이 아니다.");
            }

            for (int i = 0; i < manifest.npcs.Length; i++)
            {
                NpcArt npc = manifest.npcs[i];
                NpcDefinition definition;
                if (!database.Npcs.TryGet(npc.npcId, out definition)) errors.Add("art.npcs[" + i + "]: npcId '" + npc.npcId + "' 가 없는 인물이다.");
                if (!string.IsNullOrEmpty(npc.tokenColor) && !LooksLikeHexColor(npc.tokenColor))
                    errors.Add("art.npcs[" + i + "]: tokenColor '" + npc.tokenColor + "' 는 #RRGGBB 형식이 아니다.");
            }

            for (int i = 0; i < manifest.evidence.Length; i++)
            {
                EvidenceDefinition definition;
                if (!database.Evidence.TryGet(manifest.evidence[i].evidenceId, out definition))
                    errors.Add("art.evidence[" + i + "]: evidenceId '" + manifest.evidence[i].evidenceId + "' 가 없는 단서다.");
            }

            AudioArt audio = manifest.audio;
            if (audio.bgmVolume < 0f || audio.bgmVolume > 1f) errors.Add("art.audio.bgmVolume은 0~1이어야 한다.");
            if (audio.ambientVolume < 0f || audio.ambientVolume > 1f) errors.Add("art.audio.ambientVolume은 0~1이어야 한다.");
            if (audio.sfxVolume < 0f || audio.sfxVolume > 1f) errors.Add("art.audio.sfxVolume은 0~1이어야 한다.");

            return errors;
        }

        private static bool LooksLikeHexColor(string text)
        {
            if (text.Length != 7 || text[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char c = text[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }
    }
}
