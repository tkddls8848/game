using System.Collections.Generic;
using Detective.Core;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 지금 이 순간 누가 어디 서 있는가. 거리감을 내려면 "어느 방"이 아니라 <b>어느 점</b>이 필요하다.
    ///
    /// 위치는 이동 트랙에서 온다 — 방 안에 머물면 방 중심, 방을 옮기는 중이면 두 방 중심 사이를
    /// 진행도로 보간한 점이다. 그래서 복도를 지나가는 사람의 목소리가 실제로 스쳐 지나간다.
    ///
    /// <b>트랙과 대본이 어긋날 때의 규칙</b>이 하나 있다. 발화에는 방이 적혀 있고(그 방이
    /// 가청 판정의 근거다) 트랙에도 방이 적혀 있다. 둘이 다르면 <see cref="AudibilityModel"/>은
    /// 발화의 방으로 판정했으므로, 위치도 <b>발화의 방</b>을 따라야 한다. 트랙을 믿고 위치를
    /// 잡으면 "옆 방에서 들린다고 판정된 소리가 이 방 한가운데서 난다"는 모순이 생긴다.
    /// 그 경우 트랙을 무시하고 발화 방의 중심을 쓴다.
    ///
    /// 순수 C#이다(§18-1).
    /// </summary>
    public sealed class SpeakerPositions
    {
        private readonly RoomLayout _layout;
        private readonly MovementTracks _tracks;

        /// <summary>목소리 → 인물. 대본의 speakers가 정한다.</summary>
        private readonly Dictionary<string, string> _npcByVoice = new Dictionary<string, string>();

        public SpeakerPositions(ScriptDefinition script, MovementTracks tracks, RoomLayout layout)
        {
            _layout = layout;
            _tracks = tracks;

            if (script != null)
            {
                script.Normalized();
                for (int i = 0; i < script.speakers.Length; i++)
                {
                    SpeakerDefinition speaker = script.speakers[i];
                    if (speaker == null) continue;
                    if (string.IsNullOrEmpty(speaker.voiceId) || string.IsNullOrEmpty(speaker.npcId)) continue;
                    _npcByVoice[speaker.voiceId] = speaker.npcId;
                }
            }
        }

        /// <summary>이 목소리의 주인. 없으면 빈 문자열.</summary>
        public string NpcOf(string voiceId)
        {
            if (string.IsNullOrEmpty(voiceId)) return string.Empty;
            string npcId;
            return _npcByVoice.TryGetValue(voiceId, out npcId) ? npcId : string.Empty;
        }

        /// <summary>
        /// 이 발화가 울리는 지점. <paramref name="room"/>은 발화에 적힌 방이고 이것이 우선이다.
        /// </summary>
        public bool TryGetVoicePoint(string voiceId, string room, int ms, out float x, out float y)
        {
            return TryGetPoint(NpcOf(voiceId), room, ms, out x, out y);
        }

        /// <summary>이벤트가 울리는 지점. 이벤트에도 방과 인물이 적혀 있다.</summary>
        public bool TryGetEventPoint(string npcId, string room, int ms, out float x, out float y)
        {
            return TryGetPoint(npcId, room, ms, out x, out y);
        }

        /// <summary>
        /// 인물의 지금 위치. 트랙이 없거나 트랙의 방이 <paramref name="room"/>과 어긋나면
        /// 그 방의 중심으로 물러선다.
        /// </summary>
        public bool TryGetPoint(string npcId, string room, int ms, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (_layout == null) return false;

            if (_tracks != null && !string.IsNullOrEmpty(npcId))
            {
                TrackPosition at = _tracks.SampleAt(npcId, ms);

                if (at.InTransit)
                {
                    // 이동 중이면 두 방 중심 사이. 어긋남을 따질 방이 없으므로 그대로 쓴다.
                    if (_layout.TryGetRoomCenter(at.FromRoom, out float fx, out float fy) &&
                        _layout.TryGetRoomCenter(at.ToRoom, out float tx, out float ty))
                    {
                        float t = at.ProgressPermille / 1000f;
                        if (t < 0f) t = 0f;
                        if (t > 1f) t = 1f;
                        x = fx + (tx - fx) * t;
                        y = fy + (ty - fy) * t;
                        return true;
                    }
                }
                else if (at.HasPlace && (string.IsNullOrEmpty(room) || at.Room == room))
                {
                    // 트랙과 발화의 방이 같을 때만 트랙을 믿는다.
                    if (_layout.TryGetRoomCenter(at.Room, out float cx, out float cy))
                    {
                        x = cx;
                        y = cy;
                        return true;
                    }
                }
            }

            // 물러섬: 발화에 적힌 방의 중심.
            if (!string.IsNullOrEmpty(room) && _layout.TryGetRoomCenter(room, out float rx, out float ry))
            {
                x = rx;
                y = ry;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 지금 들리는 발화들에 거리감을 입힌다. 청취점은 <b>점</b>이다(방이 아니다) —
        /// 같은 방 안에서도 어디 서 있느냐로 소리가 달라진다.
        /// </summary>
        public List<MixedUtterance> Mix(IList<PerceivedUtterance> perceived, int ms,
                                        float listenerX, float listenerY)
        {
            var result = new List<MixedUtterance>();
            if (perceived == null) return result;

            for (int i = 0; i < perceived.Count; i++)
            {
                PerceivedUtterance p = perceived[i];
                float x, y;
                if (!TryGetVoicePoint(p.VoiceId, p.Room, ms, out x, out y))
                {
                    // 자리를 못 찾으면 거리 없이 그대로 들려준다 — 소리를 잃는 쪽이 더 나쁘다.
                    result.Add(new MixedUtterance
                    {
                        Perceived = p,
                        Mix = new VoiceMixLevel { Gain = 1f, Pan = 0f, LowPassHz = VoiceMix.OpenLowPassHz }
                    });
                    continue;
                }

                result.Add(new MixedUtterance
                {
                    Perceived = p,
                    SourceX = x,
                    SourceY = y,
                    Mix = VoiceMix.For(listenerX, listenerY, x, y, p.Level)
                });
            }
            return result;
        }
    }

    /// <summary>발화 하나 + 그것을 실제로 어떻게 울릴지.</summary>
    public struct MixedUtterance
    {
        public PerceivedUtterance Perceived;
        public float SourceX;
        public float SourceY;
        public VoiceMixLevel Mix;
    }
}
