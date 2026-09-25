using Detective.Core;
using Detective.Data;
using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// 소리 담당. 모드에 따라 배경음을 바꾸고(교차 페이드), 환경음을 깔고, 이벤트에 효과음을 붙인다.
    /// 음원은 art.json 경로에서 읽고, 없으면 배경음·환경음은 침묵, 종소리·종이·조사음은 합성음을 쓴다.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        [Tooltip("배경음이 바뀔 때 교차 페이드에 걸리는 시간(초).")]
        public float crossfadeSeconds = 1.6f;

        [Tooltip("방을 옮겼을 때 환경음이 바뀌는 시간(초). 배경음보다 빨라야 방이 바뀐 것이 느껴진다.")]
        public float ambientCrossfadeSeconds = 0.9f;

        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private AudioSource _ambientA;
        private AudioSource _ambientB;
        private AudioSource _sfx;

        private AudioSource _activeBgm;
        private AudioClip _currentBgmClip;
        private float _fade; // 0 = A, 1 = B
        private float _fadeTarget;

        // 환경음도 같은 2소스 교차 페이드를 쓴다. 다만 방마다 세기가 달라서(벽난로와 괘종시계를
        // 같은 음량으로 깔면 한쪽이 방을 잡아먹는다) 각 소스가 실을 최종 세기를 따로 기억한다.
        private AudioSource _activeAmbient;
        private AudioClip _currentAmbientClip;
        private float _ambFade;
        private float _ambFadeTarget;
        private float _ambVolA = 1f;
        private float _ambVolB = 1f;
        private float _currentAmbientVolume = -1f;
        private bool _ambientStarted;

        private AudioClip _explore, _timeline, _result, _chime, _page, _inspect;
        private AudioClip _defaultAmbient;
        private AudioArt _settings;
        private ArtManifest _manifest;

        private void Awake()
        {
            _bgmA = MakeSource("BGM A", true);
            _bgmB = MakeSource("BGM B", true);
            _ambientA = MakeSource("Ambient A", true);
            _ambientB = MakeSource("Ambient B", true);
            _sfx = MakeSource("SFX", false);
            _activeBgm = _bgmA;
            _activeAmbient = _ambientA;
        }

        private void Start()
        {
            ArtLibrary art = ArtLibrary.Instance;
            _manifest = art.Manifest;
            _settings = _manifest.audio;

            _explore = art.Clip(_settings.bgmExplore, null);
            _timeline = art.Clip(_settings.bgmTimeline, null);
            _result = art.Clip(_settings.bgmResult, null);
            _chime = art.Clip(_settings.sfxClockChime, ProceduralAudio.ClockChime);
            _page = art.Clip(_settings.sfxPage, ProceduralAudio.PageTurn);
            _inspect = art.Clip(_settings.sfxInspect, ProceduralAudio.Inspect);

            // 저택 전체 소리. 방에 제 소리가 없으면 여기로 되돌아간다.
            _defaultAmbient = art.Clip(_settings.ambientLoop, null);

            // 방 소리를 미리 읽어 둔다(ArtLibrary가 캐시하므로 방을 옮길 때 끊기지 않는다).
            for (int i = 0; i < _manifest.rooms.Length; i++)
            {
                RoomArt room = _manifest.rooms[i];
                if (room != null && !string.IsNullOrEmpty(room.ambient)) art.Clip(room.ambient, null);
            }

            // 아직 방을 모르므로 전체 소리부터 깐다. 첫 PlayerRoomChanged가 오면 교체된다.
            ApplyAmbient(_defaultAmbient, _settings.ambientVolume, true);
        }

        private void OnEnable()
        {
            GameEvents.SfxRequested += OnSfx;
            GameEvents.TimelineTickChanged += OnTimelineTick;
            GameEvents.PlayerRoomChanged += OnPlayerRoomChanged;
        }

        private void OnDisable()
        {
            GameEvents.SfxRequested -= OnSfx;
            GameEvents.TimelineTickChanged -= OnTimelineTick;
            GameEvents.PlayerRoomChanged -= OnPlayerRoomChanged;
        }

        /// <summary>청취점이 옮겨 갔다. 그 방 소리로 갈아 끼운다.</summary>
        private void OnPlayerRoomChanged(string roomId)
        {
            if (_settings == null) return; // Start 전에 올 수 있다.

            AudioClip clip = _defaultAmbient;
            float volume = _settings.ambientVolume;

            RoomArt room = string.IsNullOrEmpty(roomId) || _manifest == null ? null : _manifest.RoomOf(roomId);
            if (room != null && !string.IsNullOrEmpty(room.ambient))
            {
                AudioClip roomClip = ArtLibrary.Instance.Clip(room.ambient, null);
                if (roomClip != null)
                {
                    clip = roomClip;
                    volume = _settings.ambientVolume * Mathf.Clamp01(room.ambientVolume);
                }
            }
            ApplyAmbient(clip, volume, false);
        }

        /// <summary>같은 소리를 같은 세기로 다시 요청하면 아무것도 하지 않는다(페이드를 처음부터 다시 걸지 않는다).</summary>
        private void ApplyAmbient(AudioClip clip, float volume, bool immediate)
        {
            if (_ambientStarted && clip == _currentAmbientClip && Mathf.Approximately(volume, _currentAmbientVolume)) return;

            _currentAmbientClip = clip;
            _currentAmbientVolume = volume;
            _ambientStarted = true;

            AudioSource next = _activeAmbient == _ambientA ? _ambientB : _ambientA;
            next.clip = clip;
            if (clip != null) next.Play();

            if (next == _ambientB) _ambVolB = volume; else _ambVolA = volume;
            _activeAmbient = next;
            _ambFadeTarget = next == _ambientB ? 1f : 0f;
            if (immediate) _ambFade = _ambFadeTarget;
        }

        private void Update()
        {
            if (_settings == null) return;

            AudioClip wanted = BgmFor(ModalState.Current);
            if (wanted != _currentBgmClip) SwitchBgm(wanted);

            if (!Mathf.Approximately(_fade, _fadeTarget))
            {
                float step = crossfadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / crossfadeSeconds;
                _fade = Mathf.MoveTowards(_fade, _fadeTarget, step);
            }
            _bgmA.volume = _settings.bgmVolume * (1f - _fade);
            _bgmB.volume = _settings.bgmVolume * _fade;

            // 다 꺼진 쪽은 멈춰 둔다(계속 재생하면 다음 페이드가 중간에서 시작된다).
            if (_fade <= 0f && _bgmB.isPlaying) _bgmB.Stop();
            if (_fade >= 1f && _bgmA.isPlaying) _bgmA.Stop();

            UpdateAmbient();
        }

        /// <summary>방 환경음 교차 페이드. 배경음과 같은 방식이되 세기는 소스마다 다르다.</summary>
        private void UpdateAmbient()
        {
            if (!Mathf.Approximately(_ambFade, _ambFadeTarget))
            {
                float step = ambientCrossfadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / ambientCrossfadeSeconds;
                _ambFade = Mathf.MoveTowards(_ambFade, _ambFadeTarget, step);
            }
            _ambientA.volume = _ambVolA * (1f - _ambFade);
            _ambientB.volume = _ambVolB * _ambFade;

            if (_ambFade <= 0f && _ambientB.isPlaying) _ambientB.Stop();
            if (_ambFade >= 1f && _ambientA.isPlaying) _ambientA.Stop();
        }

        private AudioClip BgmFor(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Timeline: return _timeline ?? _explore;
                case GameMode.Result: return _result ?? _explore;
                case GameMode.Intro: return _explore;
                default: return _explore;
            }
        }

        private void SwitchBgm(AudioClip clip)
        {
            _currentBgmClip = clip;
            AudioSource next = _activeBgm == _bgmA ? _bgmB : _bgmA;
            next.clip = clip;
            if (clip != null) next.Play();
            _activeBgm = next;
            _fadeTarget = next == _bgmB ? 1f : 0f;
        }

        private void OnSfx(string kind)
        {
            AudioClip clip = kind == "chime" ? _chime : kind == "page" ? _page : kind == "inspect" ? _inspect : null;
            if (clip != null) _sfx.PlayOneShot(clip, _settings.sfxVolume);
        }

        private void OnTimelineTick(int tick)
        {
            OnSfx("chime");
        }

        private AudioSource MakeSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
