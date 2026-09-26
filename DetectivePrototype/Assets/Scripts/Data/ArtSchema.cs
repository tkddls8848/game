using System;

namespace Detective.Data
{
    /// <summary>
    /// art.json — 연출 에셋 대응표. 모든 경로는 Resources 기준(확장자 없음).
    /// 파일이 없으면 코드가 만든 질감·실루엣·합성음으로 대신하므로, 에셋은 있는 것부터 하나씩 넣으면 된다.
    /// </summary>
    [Serializable]
    public class RoomArt
    {
        public string roomId;

        /// <summary>바닥 질감 종류. 파일이 없을 때 코드가 만드는 기본 질감: wood / marble / carpet / stone / rug.</summary>
        public string floor;

        /// <summary>바닥에 씌울 이미지(Resources 경로). 비어 있으면 floor 종류의 생성 질감을 쓴다.</summary>
        public string floorTexture;

        /// <summary>질감 위에 곱하는 색 "#RRGGBB". 방마다 톤을 다르게 준다.</summary>
        public string tint;

        /// <summary>질감 한 장이 덮는 월드 유닛. 클수록 무늬가 커 보인다.</summary>
        public float tileSize = 4f;

        /// <summary>
        /// 이 방에서 흐르는 환경음(Resources 경로). 비어 있으면 audio.ambientLoop(저택 전체 소리)를 쓴다.
        /// 엿듣기에서는 플레이어가 귀를 옮기므로, 방마다 소리가 달라야 눈을 감고도 어디인지 안다.
        /// </summary>
        public string ambient;

        /// <summary>
        /// 이 방 환경음에 곱하는 세기 0~1. 최종 음량은 audio.ambientVolume × 이 값이다.
        /// 벽난로와 괘종시계를 같은 세기로 깔면 한쪽이 방을 잡아먹는다.
        /// </summary>
        public float ambientVolume = 1f;
    }

    [Serializable]
    public class NpcArt
    {
        public string npcId;

        /// <summary>대화창·노트 초상화(Resources 경로). 없으면 이름 첫 글자를 넣은 실루엣.</summary>
        public string portrait;

        /// <summary>맵 위 말(토큰) 색 "#RRGGBB". 비어 있으면 npc 색을 쓴다.</summary>
        public string tokenColor;
    }

    [Serializable]
    public class EvidenceArt
    {
        public string evidenceId;

        /// <summary>노트 증거 탭 이미지(Resources 경로). 없으면 이미지 없이 글만.</summary>
        public string image;
    }

    /// <summary>소리. 전부 Resources 경로. 비어 있거나 파일이 없으면 그 소리는 나지 않는다(종소리·종이는 합성음으로 대체).</summary>
    [Serializable]
    public class AudioArt
    {
        public string bgmExplore;
        public string bgmTimeline;
        public string bgmResult;
        public string ambientLoop;
        public string sfxClockChime;
        public string sfxPage;
        public string sfxInspect;

        /// <summary>0~1.</summary>
        public float bgmVolume = 0.35f;
        public float ambientVolume = 0.4f;
        public float sfxVolume = 0.7f;

        /// <summary>말이 아닌 소리의 음량. 발소리·문소리가 대사를 덮으면 안 된다.</summary>
        public float eventVolume = 0.55f;
    }

    /// <summary>
    /// 이벤트 종류 하나에 붙는 소리들. 여러 개를 적으면 울릴 때마다 돌아가며 고른다 —
    /// 발소리가 매번 같은 파일이면 열 번만 들어도 기계처럼 들린다.
    /// </summary>
    [Serializable]
    public class EventSoundArt
    {
        /// <summary><see cref="Detective.Eavesdrop.EventKind"/>의 값.</summary>
        public string kind;

        /// <summary>Resources 경로들(확장자 없음). 비어 있으면 그 종류는 소리가 없다.</summary>
        public string[] clips = new string[0];

        /// <summary>이 종류만의 음량 배수. 문소리는 발소리보다 커야 한다.</summary>
        public float volume = 1f;

        public EventSoundArt Normalized()
        {
            if (clips == null) clips = new string[0];
            if (kind == null) kind = string.Empty;
            return this;
        }
    }

    [Serializable]
    public class ArtManifest
    {
        /// <summary>UI 폰트(Resources 경로). 없으면 OS 한글 폰트 → 내장 폰트 순으로 찾는다.</summary>
        public string uiFont;

        public RoomArt[] rooms;
        public NpcArt[] npcs;
        public EvidenceArt[] evidence;
        public AudioArt audio;

        /// <summary>이동·사건이 내는 소리. 종류별로 여러 파일을 적어 돌아가며 쓴다.</summary>
        public EventSoundArt[] eventSounds = new EventSoundArt[0];

        public ArtManifest Normalized()
        {
            if (rooms == null) rooms = new RoomArt[0];
            if (npcs == null) npcs = new NpcArt[0];
            if (evidence == null) evidence = new EvidenceArt[0];
            if (audio == null) audio = new AudioArt();
            if (eventSounds == null) eventSounds = new EventSoundArt[0];
            for (int i = 0; i < eventSounds.Length; i++)
                if (eventSounds[i] != null) eventSounds[i].Normalized();
            return this;
        }

        /// <summary>이 종류의 소리 묶음. 없으면 null.</summary>
        public EventSoundArt EventSoundOf(string kind)
        {
            for (int i = 0; i < eventSounds.Length; i++)
                if (eventSounds[i] != null && eventSounds[i].kind == kind) return eventSounds[i];
            return null;
        }

        public RoomArt RoomOf(string roomId)
        {
            for (int i = 0; i < rooms.Length; i++) if (rooms[i] != null && rooms[i].roomId == roomId) return rooms[i];
            return null;
        }

        public NpcArt NpcOf(string npcId)
        {
            for (int i = 0; i < npcs.Length; i++) if (npcs[i] != null && npcs[i].npcId == npcId) return npcs[i];
            return null;
        }

        public EvidenceArt EvidenceOf(string evidenceId)
        {
            for (int i = 0; i < evidence.Length; i++) if (evidence[i] != null && evidence[i].evidenceId == evidenceId) return evidence[i];
            return null;
        }
    }
}
