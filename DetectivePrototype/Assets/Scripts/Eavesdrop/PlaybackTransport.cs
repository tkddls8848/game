namespace Detective.Eavesdrop
{
    /// <summary>재생 상태.</summary>
    public enum TransportState
    {
        /// <summary>멈춰 있다. 위치는 유지된다.</summary>
        Paused = 0,

        /// <summary>흐르고 있다.</summary>
        Playing = 1,

        /// <summary>회차 끝에 닿았다. 다시 들으려면 되감아야 한다.</summary>
        Ended = 2
    }

    /// <summary>
    /// 한 회차를 재생·정지·탐색·되감기·배속으로 다루는 장치(순수 C#).
    ///
    /// 위치는 정수 밀리초다. 배속을 걸어도 정수만 쓰기 위해 백분율로 곱한 뒤
    /// 나머지를 누적한다 — 0.5배속으로 1ms씩 300번 넘겨도 정확히 150ms가 되고 오차가 쌓이지 않는다.
    /// 프레임 시간을 float로 받아 곱하면 여기서 드리프트가 생긴다.
    ///
    /// 구간은 [0, DurationMs]다. 끝에 닿으면 Ended가 되고 더 흐르지 않는다 —
    /// 자동으로 처음부터 돌지 않는 것은 의도다. 되돌려 듣는 것이 플레이어의 선택이어야 한다.
    /// </summary>
    public sealed class PlaybackTransport
    {
        /// <summary>1배속.</summary>
        public const int NormalSpeedPercent = 100;

        private const int MinSpeedPercent = 10;    // 0.1배
        private const int MaxSpeedPercent = 800;   // 8배

        private readonly int _durationMs;
        private int _positionMs;
        private int _speedPercent = NormalSpeedPercent;
        private int _remainder;
        private TransportState _state = TransportState.Paused;

        public PlaybackTransport(int durationMs)
        {
            _durationMs = durationMs > 0 ? durationMs : 0;
        }

        public int DurationMs { get { return _durationMs; } }
        public int PositionMs { get { return _positionMs; } }
        public TransportState State { get { return _state; } }
        public bool IsPlaying { get { return _state == TransportState.Playing; } }
        public bool AtEnd { get { return _positionMs >= _durationMs; } }

        /// <summary>배속(백분율). 100이 1배속. 10~800으로 조인다.</summary>
        public int SpeedPercent
        {
            get { return _speedPercent; }
            set
            {
                int clamped = value < MinSpeedPercent ? MinSpeedPercent
                            : value > MaxSpeedPercent ? MaxSpeedPercent : value;
                if (clamped == _speedPercent) return;
                _speedPercent = clamped;
                _remainder = 0; // 배속이 바뀌면 이전 나머지는 의미가 없다
            }
        }

        /// <summary>끝에서 누르면 처음부터 다시 흐른다.</summary>
        public void Play()
        {
            if (AtEnd) Restart();
            _state = TransportState.Playing;
        }

        public void Pause()
        {
            if (_state == TransportState.Playing) _state = TransportState.Paused;
        }

        public void TogglePlay()
        {
            if (_state == TransportState.Playing) Pause();
            else Play();
        }

        /// <summary>처음으로 되감고 멈춘다.</summary>
        public void Restart()
        {
            _positionMs = 0;
            _remainder = 0;
            _state = TransportState.Paused;
        }

        /// <summary>절대 위치로 옮긴다. 구간 밖이면 잘린다.</summary>
        public void SeekTo(int ms)
        {
            _positionMs = ms < 0 ? 0 : ms > _durationMs ? _durationMs : ms;
            _remainder = 0;
            if (_positionMs < _durationMs && _state == TransportState.Ended) _state = TransportState.Paused;
            else if (AtEnd && _state == TransportState.Playing) _state = TransportState.Ended;
        }

        /// <summary>현재 위치에서 상대 이동. 앞뒤 건너뛰기에 쓴다.</summary>
        public void SeekBy(int deltaMs)
        {
            SeekTo(_positionMs + deltaMs);
        }

        /// <summary>
        /// 실제 흐른 시간만큼 위치를 민다. Playing일 때만 움직이며, 옮겨 간 실제 거리(ms)를 돌려준다.
        /// 끝에 닿으면 거기서 멈추고 Ended가 된다.
        /// </summary>
        public int Advance(int realDeltaMs)
        {
            if (_state != TransportState.Playing || realDeltaMs <= 0) return 0;

            int scaled = realDeltaMs * _speedPercent + _remainder;
            int step = scaled / NormalSpeedPercent;
            _remainder = scaled % NormalSpeedPercent;

            int before = _positionMs;
            _positionMs += step;
            if (_positionMs >= _durationMs)
            {
                _positionMs = _durationMs;
                _remainder = 0;
                _state = TransportState.Ended;
            }
            return _positionMs - before;
        }
    }
}
