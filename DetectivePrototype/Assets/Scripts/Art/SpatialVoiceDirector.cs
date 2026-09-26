using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// 거리감을 실제 소리로 만든다 — Unheard처럼 인물이 어디 서 있느냐에 따라 음량·정위·음색이 달라진다.
    ///
    /// 숫자는 전부 순수 로직이 낸다(<see cref="VoiceMix"/>, <see cref="SpeakerPositions"/>).
    /// 이 클래스가 하는 일은 그 숫자를 AudioSource에 옮기는 것뿐이다. 그래서 거리 감쇠 규칙은
    /// 엔진 없이 테스트로 검증돼 있고, 여기서 규칙을 다시 정하지 않는다.
    ///
    /// <b>Unity의 3D 오디오를 쓰지 않는다.</b> 이유가 있다 — Unity의 공간화는 방과 벽을 모르므로
    /// 벽 하나 건너 옆 방 소리를 같은 방 소리처럼 크게 낸다. 이 게임의 가청 판정은 방 인접성
    /// 기준이라(문이 아니라 벽 맞닿음) 엔진의 거리 모델과 어긋난다. 그래서 2D AudioSource에
    /// 우리가 계산한 volume·panStereo·lowpass를 직접 얹는다.
    ///
    /// 두 층을 함께 울린다.
    ///   말   — <c>Utterance.clip</c>의 음성. <b>아직 파일이 없다</b>(TTS 대기). 있으면 자동으로 울린다.
    ///   소리 — 문·발소리 등. 파일이 이미 있으므로 <b>지금 당장 거리감을 들을 수 있다</b>.
    /// </summary>
    public class SpatialVoiceDirector : MonoBehaviour
    {
        /// <summary>동시에 울릴 수 있는 말의 수. 겹쳐 말하는 회차라 여러 개가 필요하다.</summary>
        public int voiceChannels = 6;

        /// <summary>동시에 울릴 수 있는 소리의 수. 발소리가 겹치므로 넉넉히.</summary>
        public int eventChannels = 8;

        /// <summary>음량이 튀지 않게 따라가는 속도(초당). 0이면 즉시.</summary>
        public float gainFollowPerSecond = 12f;

        private EavesdropController _controller;
        private SpeakerPositions _positions;
        private ArtManifest _art;
        private Transform _listener;

        private readonly List<Channel> _voices = new List<Channel>();
        private readonly List<Channel> _events = new List<Channel>();

        /// <summary>이벤트 id → 이미 울렸는가. 같은 소리를 프레임마다 다시 트는 것을 막는다.</summary>
        private readonly HashSet<string> _firedEvents = new HashSet<string>();

        /// <summary>종류별로 다음에 쓸 파일 번호. 발소리가 매번 같으면 기계처럼 들린다.</summary>
        private readonly Dictionary<string, int> _rotation = new Dictionary<string, int>();

        /// <summary>되감기를 감지해 울린 기록을 지우기 위한 이전 위치.</summary>
        private int _lastPositionMs = -1;

        private sealed class Channel
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public string Key = string.Empty;
            public float TargetGain;
        }

        // ── 준비 ──────────────────────────────────────────────

        public void Bind(EavesdropController controller, SpeakerPositions positions,
                         ArtManifest art, Transform listener)
        {
            _controller = controller;
            _positions = positions;
            _art = art != null ? art.Normalized() : null;
            _listener = listener;
        }

        private void Awake()
        {
            for (int i = 0; i < voiceChannels; i++) _voices.Add(NewChannel("Voice" + i, 1f));
            for (int i = 0; i < eventChannels; i++) _events.Add(NewChannel("Event" + i, 1f));
        }

        private Channel NewChannel(string name, float volume)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0f;
            // 2D로 둔다 — 공간화는 우리가 한다(위 주석 참고).
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;

            AudioLowPassFilter lowPass = host.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = VoiceMix.OpenLowPassHz;

            return new Channel { Source = source, LowPass = lowPass };
        }

        // ── 매 프레임 ─────────────────────────────────────────

        private void Update()
        {
            if (_controller == null || _controller.Session == null || _listener == null) return;
            ListeningSession session = _controller.Session;

            float lx = _listener.position.x;
            float ly = _listener.position.y;
            int nowMs = session.PositionMs;

            // 뒤로 감거나 크게 건너뛰면 "이미 울렸다" 기록을 지운다 — 다시 들어야 하니까.
            if (_lastPositionMs >= 0 && nowMs < _lastPositionMs) _firedEvents.Clear();
            _lastPositionMs = nowMs;

            UpdateVoices(session, nowMs, lx, ly);
            UpdateEvents(session, nowMs, lx, ly);
            Settle(_voices);
            Settle(_events);
        }

        /// <summary>
        /// 말. 이어지는 소리이므로 발화가 살아 있는 동안 채널을 붙들고 음량만 따라 바꾼다.
        /// </summary>
        private void UpdateVoices(ListeningSession session, int nowMs, float lx, float ly)
        {
            List<MixedUtterance> mixed = _positions != null
                ? _positions.Mix(session.Current, nowMs, lx, ly)
                : null;
            if (mixed == null) return;

            var live = new HashSet<string>();

            for (int i = 0; i < mixed.Count; i++)
            {
                MixedUtterance m = mixed[i];
                string id = m.Perceived.UtteranceId;
                live.Add(id);

                Channel channel = Find(_voices, id);
                if (channel == null)
                {
                    AudioClip clip = LoadVoiceClip(id);
                    if (clip == null) continue;          // 음성이 아직 없다(TTS 대기)

                    channel = Claim(_voices, id);
                    if (channel == null) continue;       // 채널이 다 찼다
                    channel.Source.clip = clip;
                    channel.Source.volume = 0f;

                    // 이미 지나간 만큼 건너뛰고 튼다 — 회차 중간에 귀를 옮겨도 맞물린다.
                    Utterance utterance;
                    int offsetMs = session.Timeline.TryGet(id, out utterance) ? nowMs - utterance.startMs : 0;
                    float offset = offsetMs / 1000f;
                    channel.Source.time = offset > 0f && offset < clip.length ? offset : 0f;
                    channel.Source.Play();
                }

                channel.TargetGain = m.Mix.Gain;
                channel.LowPass.cutoffFrequency = m.Mix.LowPassHz;
                channel.Source.panStereo = m.Mix.Pan;
            }

            Release(_voices, live);
        }

        /// <summary>
        /// 소리. 순간음이므로 한 번 트고 끝낸다. 음량·정위는 트는 순간에 정한다.
        /// </summary>
        private void UpdateEvents(ListeningSession session, int nowMs, float lx, float ly)
        {
            IList<PerceivedEvent> heard = session.CurrentEvents;
            if (heard == null || heard.Count == 0) return;

            for (int i = 0; i < heard.Count; i++)
            {
                PerceivedEvent e = heard[i];
                if (_firedEvents.Contains(e.EventId)) continue;

                ScriptEvent definition;
                if (session.Events == null || !session.Events.TryGet(e.EventId, out definition)) continue;

                // 너무 늦게 들어왔으면(빨리 감기로 지나침) 트지 않는다 — 뒤늦게 튀어나오면 어색하다.
                if (nowMs - definition.startMs > 400) { _firedEvents.Add(e.EventId); continue; }

                AudioClip clip = NextClip(definition.kind, out float kindVolume);
                _firedEvents.Add(e.EventId);
                if (clip == null) continue;

                float sx, sy;
                if (_positions == null ||
                    !_positions.TryGetEventPoint(definition.npcId, definition.room, nowMs, out sx, out sy))
                    continue;

                VoiceMixLevel mix = VoiceMix.For(lx, ly, sx, sy, e.Level);
                if (mix.IsSilent) continue;

                Channel channel = Claim(_events, e.EventId);
                if (channel == null) continue;

                channel.Source.clip = clip;
                channel.Source.panStereo = mix.Pan;
                channel.LowPass.cutoffFrequency = mix.LowPassHz;

                float volume = mix.Gain * kindVolume * EventVolume();
                channel.Source.volume = volume;
                channel.TargetGain = volume;      // 순간음은 따라가지 않고 그대로 둔다
                channel.Source.Play();
            }
        }

        /// <summary>음량을 목표로 부드럽게 옮기고, 끝난 채널을 비운다.</summary>
        private void Settle(List<Channel> channels)
        {
            float step = gainFollowPerSecond > 0f ? Time.unscaledDeltaTime * gainFollowPerSecond : 1f;
            for (int i = 0; i < channels.Count; i++)
            {
                Channel channel = channels[i];
                if (channel.Key.Length == 0) continue;

                if (!channel.Source.isPlaying)
                {
                    channel.Key = string.Empty;
                    channel.Source.volume = 0f;
                    continue;
                }
                channel.Source.volume = Mathf.MoveTowards(channel.Source.volume, channel.TargetGain, step);
            }
        }

        // ── 채널 관리 ─────────────────────────────────────────

        private static Channel Find(List<Channel> channels, string key)
        {
            for (int i = 0; i < channels.Count; i++)
                if (channels[i].Key == key) return channels[i];
            return null;
        }

        private static Channel Claim(List<Channel> channels, string key)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i].Key.Length != 0) continue;
                channels[i].Key = key;
                return channels[i];
            }
            return null;
        }

        /// <summary>더 이상 울리지 않는 말의 채널을 놓는다.</summary>
        private static void Release(List<Channel> channels, HashSet<string> live)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                Channel channel = channels[i];
                if (channel.Key.Length == 0 || live.Contains(channel.Key)) continue;
                channel.Source.Stop();
                channel.Source.volume = 0f;
                channel.Key = string.Empty;
            }
        }

        // ── 파일 ──────────────────────────────────────────────

        private AudioClip LoadVoiceClip(string utteranceId)
        {
            if (_controller == null || _controller.Session == null) return null;
            string caseId = _controller.Session.Timeline != null ? CaseIdOf() : string.Empty;
            string path = VoiceClipPlan.ClipPath(caseId, utteranceId);
            return path.Length == 0 ? null : Resources.Load<AudioClip>(path);
        }

        private string CaseIdOf()
        {
            return _controller.CaseId ?? string.Empty;
        }

        /// <summary>이 종류의 다음 파일. 돌아가며 골라 같은 소리가 반복되지 않게 한다.</summary>
        private AudioClip NextClip(string kind, out float volume)
        {
            volume = 1f;
            if (_art == null) return null;

            EventSoundArt sound = _art.EventSoundOf(kind);
            if (sound == null || sound.clips.Length == 0) return null;
            volume = sound.volume;

            int index;
            _rotation.TryGetValue(kind, out index);
            _rotation[kind] = (index + 1) % sound.clips.Length;
            return Resources.Load<AudioClip>(sound.clips[index]);
        }

        private float EventVolume()
        {
            return _art != null && _art.audio != null ? _art.audio.eventVolume : 0.55f;
        }
    }
}
