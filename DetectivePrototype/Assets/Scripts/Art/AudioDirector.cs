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

        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private AudioSource _ambient;
        private AudioSource _sfx;

        private AudioSource _activeBgm;
        private AudioClip _currentBgmClip;
        private float _fade; // 0 = A, 1 = B
        private float _fadeTarget;

        private AudioClip _explore, _timeline, _result, _chime, _page, _inspect;
        private AudioArt _settings;

        private void Awake()
        {
            _bgmA = MakeSource("BGM A", true);
            _bgmB = MakeSource("BGM B", true);
            _ambient = MakeSource("Ambient", true);
            _sfx = MakeSource("SFX", false);
            _activeBgm = _bgmA;
        }

        private void Start()
        {
            ArtLibrary art = ArtLibrary.Instance;
            _settings = art.Manifest.audio;

            _explore = art.Clip(_settings.bgmExplore, null);
            _timeline = art.Clip(_settings.bgmTimeline, null);
            _result = art.Clip(_settings.bgmResult, null);
            _chime = art.Clip(_settings.sfxClockChime, ProceduralAudio.ClockChime);
            _page = art.Clip(_settings.sfxPage, ProceduralAudio.PageTurn);
            _inspect = art.Clip(_settings.sfxInspect, ProceduralAudio.Inspect);

            AudioClip ambient = art.Clip(_settings.ambientLoop, null);
            if (ambient != null)
            {
                _ambient.clip = ambient;
                _ambient.volume = _settings.ambientVolume;
                _ambient.Play();
            }
        }

        private void OnEnable()
        {
            GameEvents.SfxRequested += OnSfx;
            GameEvents.TimelineTickChanged += OnTimelineTick;
        }

        private void OnDisable()
        {
            GameEvents.SfxRequested -= OnSfx;
            GameEvents.TimelineTickChanged -= OnTimelineTick;
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
