using Detective.Core;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>창 열림/닫힘과 같은 프레임 키 입력 차단.</summary>
    public class ModalStateTests
    {
        [SetUp]
        public void SetUp()
        {
            ModalState.Reset();
        }

        [Test]
        public void TryEnter_OnlyFromExplore()
        {
            Assert.IsTrue(ModalState.TryEnter(GameMode.Dialogue, 10));
            Assert.IsFalse(ModalState.TryEnter(GameMode.Notebook, 11), "대화 중에는 노트를 열 수 없다");
            Assert.AreEqual(GameMode.Dialogue, ModalState.Current);
        }

        [Test]
        public void OpeningFrame_DoesNotAcceptInput()
        {
            ModalState.TryEnter(GameMode.Dialogue, 10);
            Assert.IsFalse(ModalState.AcceptsInput(GameMode.Dialogue, 10), "여는 데 쓴 E 키가 첫 줄을 넘기면 안 된다");
            Assert.IsTrue(ModalState.AcceptsInput(GameMode.Dialogue, 11));
        }

        [Test]
        public void ClosingFrame_BlocksReopen()
        {
            ModalState.TryEnter(GameMode.Dialogue, 10);
            ModalState.Exit(GameMode.Dialogue, 20);
            Assert.IsFalse(ModalState.TryEnter(GameMode.Dialogue, 20), "닫는 데 쓴 키가 창을 다시 열면 안 된다");
            Assert.IsFalse(ModalState.AcceptsInput(GameMode.Explore, 20));
            Assert.IsTrue(ModalState.TryEnter(GameMode.Dialogue, 21));
        }

        [Test]
        public void Exit_IgnoresOtherModes()
        {
            ModalState.TryEnter(GameMode.Timeline, 5);
            ModalState.Exit(GameMode.Dialogue, 6);
            Assert.AreEqual(GameMode.Timeline, ModalState.Current);
        }

        [Test]
        public void Force_ReplacesCurrentMode()
        {
            ModalState.TryEnter(GameMode.Accusation, 5);
            ModalState.Force(GameMode.Result, 6);
            Assert.AreEqual(GameMode.Result, ModalState.Current);
        }
    }
}
