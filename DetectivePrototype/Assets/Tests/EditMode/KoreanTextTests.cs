using Detective.Core;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>자동 생성 대사의 조사 선택.</summary>
    public class KoreanTextTests
    {
        [Test]
        public void EulReul_FollowsFinalConsonant()
        {
            Assert.AreEqual("클라라를", KoreanText.EulReul("클라라"));
            Assert.AreEqual("줄리안을", KoreanText.EulReul("줄리안"));
        }

        [Test]
        public void OtherParticles()
        {
            Assert.AreEqual("헬렌 모로와", KoreanText.WaGwa("헬렌 모로"));
            Assert.AreEqual("마르코 벨리니와", KoreanText.WaGwa("마르코 벨리니"));
            Assert.AreEqual("줄리안 헤일과", KoreanText.WaGwa("줄리안 헤일"));
            Assert.AreEqual("서재는", KoreanText.EunNeun("서재"));
            Assert.AreEqual("식당이", KoreanText.IGa("식당"));
        }

        [Test]
        public void NonHangul_UsesVowelForm()
        {
            Assert.AreEqual("NPC를", KoreanText.EulReul("NPC"));
            Assert.AreEqual("를", KoreanText.EulReul(""));
        }
    }
}
