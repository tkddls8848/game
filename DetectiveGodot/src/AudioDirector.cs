using System.Collections.Generic;
using Detective.Data;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 소리를 맡는다. Unity 판 <c>Art/AudioDirector.cs</c>와 **같은 규칙·같은 시간**이다.
    ///
    ///   BGM          — 한 곡을 물려 재생. 곡을 바꿀 때 1.6초 교차 페이드.
    ///   기본 환경음   — 저택 전체에 깔리는 겨울밤. 끊기지 않는다.
    ///   방 환경음     — art.json의 방마다 다른 소리(로비 시계추, 서재 난롯불).
    ///                  방을 옮기면 0.9초 교차 페이드 — BGM보다 빠르다. 발이 옮겨진
    ///                  속도를 소리가 따라가야 하기 때문이다.
    ///
    /// 경로·음량은 전부 art.json이 정한다. 코드에 파일 이름을 박지 않는다.
    /// 파일이 없으면 침묵한다(§연출 에셋 규칙) — 소리가 없다고 게임이 멈추지는 않는다.
    ///
    /// Godot은 음량을 dB로 받는다. art.json의 선형값(0~1)은 <c>Mathf.LinearToDb</c>로 옮긴다.
    /// 0을 그대로 넘기면 -inf dB가 되어 경고가 나므로 아주 작은 값에서 끊는다.
    /// </summary>
    public partial class AudioDirector : Node
    {
        /// <summary>Unity 판의 <c>crossfadeSeconds</c>와 같은 값.</summary>
        private const float BgmFadeSeconds = 1.6f;

        /// <summary>Unity 판의 <c>ambientCrossfadeSeconds</c>. BGM보다 빠르다.</summary>
        private const float AmbientFadeSeconds = 0.9f;

        /// <summary>이 밑으로는 들리지 않는 것으로 보고 정지한다(-inf dB 경고 방지).</summary>
        private const float Silence = 0.0008f;

        public const string MediaRoot = "res://media/";

        private ArtManifest _art;
        private readonly Dictionary<string, RoomArt> _rooms = new Dictionary<string, RoomArt>();

        private AudioStreamPlayer _bgm;
        private AudioStreamPlayer _bgmOut;      // 교차 페이드 중 빠져나가는 쪽
        private AudioStreamPlayer _baseAmbient;
        private AudioStreamPlayer _roomA;
        private AudioStreamPlayer _roomB;
        private bool _roomAIsCurrent = true;

        private float _bgmTarget;
        private float _bgmOutTarget;
        private float _roomATarget;
        private float _roomBTarget;
        private string _currentRoomAmbient = string.Empty;
        private string _currentBgm = string.Empty;

        public void Setup(ArtManifest art)
        {
            _art = art;
            if (_art == null) return;
            _art.Normalized();
            foreach (RoomArt room in _art.rooms)
                if (!string.IsNullOrEmpty(room.roomId)) _rooms[room.roomId] = room;

            _bgm = AddPlayer("Bgm");
            _bgmOut = AddPlayer("BgmOut");
            _baseAmbient = AddPlayer("BaseAmbient");
            _roomA = AddPlayer("RoomAmbientA");
            _roomB = AddPlayer("RoomAmbientB");

            PlayBgm(_art.audio.bgmExplore);

            // 저택 전체에 깔리는 소리. 방과 무관하게 계속 돈다.
            if (Load(_art.audio.ambientLoop) is AudioStream stream)
            {
                _baseAmbient.Stream = stream;
                SetLooping(stream);
                _baseAmbient.VolumeDb = ToDb(_art.audio.ambientVolume);
                _baseAmbient.Play();
            }
        }

        private AudioStreamPlayer AddPlayer(string name)
        {
            var player = new AudioStreamPlayer { Name = name };
            AddChild(player);
            return player;
        }

        // ── BGM ───────────────────────────────────────────────

        /// <summary>곡을 바꾼다. 같은 곡이면 아무 일도 하지 않는다(다시 시작하면 티가 난다).</summary>
        public void PlayBgm(string resourcePath)
        {
            if (_art == null || string.IsNullOrEmpty(resourcePath)) return;
            if (resourcePath == _currentBgm) return;

            AudioStream stream = Load(resourcePath);
            if (stream == null) return;

            // 지금 울리고 있던 것을 빠지는 쪽으로 넘기고, 새 곡을 0에서 올린다.
            if (_bgm.Playing)
            {
                _bgmOut.Stream = _bgm.Stream;
                _bgmOut.VolumeDb = _bgm.VolumeDb;
                _bgmOut.Seek(_bgm.GetPlaybackPosition());
                _bgmOut.Play();
                _bgmOutTarget = 0f;
            }

            _currentBgm = resourcePath;
            SetLooping(stream);
            _bgm.Stream = stream;
            _bgm.VolumeDb = ToDb(0f);
            _bgm.Play();
            _bgmTarget = _art.audio.bgmVolume;
        }

        // ── 방 환경음 ─────────────────────────────────────────

        /// <summary>
        /// 청취점이 바뀌었다. art.json에 그 방의 소리가 있으면 0.9초에 걸쳐 바꿔 준다.
        /// 방 사이(문간, 빈 문자열)에서는 방 소리를 뺀다 — 기본 환경음만 남는다.
        /// </summary>
        public void OnRoomChanged(string roomId)
        {
            if (_art == null) return;

            string next = string.Empty;
            float volume = 1f;
            if (!string.IsNullOrEmpty(roomId) && _rooms.TryGetValue(roomId, out RoomArt art)
                && !string.IsNullOrEmpty(art.ambient))
            {
                next = art.ambient;
                volume = art.ambientVolume;
            }
            if (next == _currentRoomAmbient) return;
            _currentRoomAmbient = next;

            // 들어오는 쪽을 지금 쉬고 있는 플레이어에 얹고, 쓰던 쪽을 0으로 내린다.
            AudioStreamPlayer incoming = _roomAIsCurrent ? _roomB : _roomA;
            float target = 0f;
            if (!string.IsNullOrEmpty(next))
            {
                AudioStream stream = Load(next);
                if (stream != null)
                {
                    SetLooping(stream);
                    incoming.Stream = stream;
                    incoming.VolumeDb = ToDb(0f);
                    incoming.Play();
                    target = _art.audio.ambientVolume * volume;
                }
            }

            if (_roomAIsCurrent) { _roomBTarget = target; _roomATarget = 0f; }
            else { _roomATarget = target; _roomBTarget = 0f; }
            _roomAIsCurrent = !_roomAIsCurrent;
        }

        // ── 페이드 ────────────────────────────────────────────

        public override void _Process(double delta)
        {
            if (_art == null) return;
            float dt = (float)delta;
            Approach(_bgm, _bgmTarget, dt / BgmFadeSeconds);
            Approach(_bgmOut, _bgmOutTarget, dt / BgmFadeSeconds);
            Approach(_roomA, _roomATarget, dt / AmbientFadeSeconds);
            Approach(_roomB, _roomBTarget, dt / AmbientFadeSeconds);
        }

        /// <summary>
        /// 선형 음량으로 목표에 다가간다. dB에서 직접 보간하면 귀에 고르게 들리지 않는다
        /// (-40dB→-20dB와 -20dB→0dB의 체감 차이가 다르다).
        /// </summary>
        private static void Approach(AudioStreamPlayer player, float target, float step)
        {
            if (player == null || player.Stream == null) return;
            float current = Mathf.DbToLinear(player.VolumeDb);
            float next = Mathf.MoveToward(current, target, step);
            player.VolumeDb = ToDb(next);
            if (next <= Silence && target <= Silence && player.Playing) player.Stop();
        }

        private static float ToDb(float linear)
        {
            return linear <= Silence ? -80f : Mathf.LinearToDb(linear);
        }

        private static void SetLooping(AudioStream stream)
        {
            // BGM·환경음은 물려 돌아야 한다. 임포트 기본값은 한 번 재생이다.
            if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
            else if (stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        }

        /// <summary>art.json의 Resources 경로(확장자 없음) → res://media 아래의 실제 파일.</summary>
        private static AudioStream Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            foreach (string ext in new[] { ".ogg", ".wav", ".mp3" })
            {
                string path = MediaRoot + resourcePath + ext;
                if (ResourceLoader.Exists(path)) return ResourceLoader.Load<AudioStream>(path);
            }
            // 파일이 없으면 침묵. 연출 에셋 규칙대로 게임을 멈추지 않는다.
            return null;
        }
    }
}
