namespace Detective.Core
{
    /// <summary>지금 화면을 차지하고 있는 모드. 한 번에 하나만 열린다.</summary>
    public enum GameMode
    {
        Explore,
        Intro,
        Dialogue,
        Notebook,
        Timeline,
        Accusation,
        Result
    }

    /// <summary>
    /// 입력 소유권(순수 C#). 대화창이 열려 있는데 플레이어가 걸어가거나,
    /// 같은 프레임의 E 키가 대화창을 열자마자 넘겨 버리는 일을 막는다.
    /// 프레임 번호는 호출하는 쪽(Time.frameCount)이 넘겨준다 — UnityEngine 의존을 들이지 않기 위해서다.
    /// </summary>
    public static class ModalState
    {
        public static GameMode Current { get; private set; }

        /// <summary>마지막으로 모드가 바뀐 프레임. 이 프레임에는 새 모드가 입력을 처리하지 않는다.</summary>
        public static int ChangedAtFrame { get; private set; }

        public static bool IsExploring { get { return Current == GameMode.Explore; } }

        /// <summary>탐색 중일 때만 새 모드로 들어간다. 이미 다른 창이 열려 있으면 false.</summary>
        public static bool TryEnter(GameMode mode, int frame)
        {
            if (Current != GameMode.Explore || mode == GameMode.Explore) return false;
            if (frame == ChangedAtFrame) return false; // 방금 닫힌 창의 키 입력으로 다른 창이 열리는 것을 막는다.
            Current = mode;
            ChangedAtFrame = frame;
            return true;
        }

        /// <summary>강제로 모드를 바꾼다(결과 화면처럼 다른 창을 대체할 때).</summary>
        public static void Force(GameMode mode, int frame)
        {
            Current = mode;
            ChangedAtFrame = frame;
        }

        /// <summary>자기 모드일 때만 탐색으로 돌아간다.</summary>
        public static void Exit(GameMode mode, int frame)
        {
            if (Current != mode) return;
            Current = GameMode.Explore;
            ChangedAtFrame = frame;
        }

        /// <summary>이 모드가 지금 입력을 받아도 되는가(열린 바로 그 프레임은 제외).</summary>
        public static bool AcceptsInput(GameMode mode, int frame)
        {
            return Current == mode && frame != ChangedAtFrame;
        }

        public static void Reset()
        {
            Current = GameMode.Explore;
            ChangedAtFrame = -1;
        }
    }
}
