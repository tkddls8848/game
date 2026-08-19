using NUnit.Framework;
using Detective.Core;

namespace Detective.Tests
{
    /// <summary>
    /// Phase 0 스모크 테스트.
    /// 이 테스트가 green이면 다음이 모두 확인된 것이다:
    ///   1. 프로젝트가 컴파일된다
    ///   2. 테스트 어셈블리가 런타임 어셈블리를 참조할 수 있다
    ///   3. batchmode 테스트 러너 파이프라인이 동작한다
    /// </summary>
    public class SmokeTests
    {
        [Test]
        public void RuntimeAssembly_IsReferencable()
        {
            Assert.AreEqual("DetectivePrototype", ProjectInfo.PrototypeName);
        }

        [Test]
        public void Timeline_Has7Ticks()
        {
            Assert.AreEqual(7, ProjectInfo.TimelineTickCount);
        }
    }
}
