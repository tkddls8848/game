using System.Collections.Generic;
using Detective.Dialogue;
using Detective.Investigation;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>대화 조립: 순서, 조건부 대사, 목격 대사 덮어쓰기, 새 대사 우선.</summary>
    public class ConversationTests
    {
        [Test]
        public void AllLines_OrderIsPlainThenSightingsThenConditional()
        {
            List<ConversationLine> lines = ConversationBuilder.AllLines(TestCaseFactory.Database(), "culprit");

            Assert.AreEqual("culprit|alibi", lines[0].Key);
            int firstSighting = lines.FindIndex(l => l.IsSighting);
            int firstConditional = lines.FindIndex(l => l.IsConditional);
            Assert.Greater(firstSighting, 0);
            Assert.Greater(firstConditional, firstSighting);
        }

        [Test]
        public void SightingOverride_ReplacesAutoText_AndIsNotStandalone()
        {
            List<ConversationLine> lines = ConversationBuilder.AllLines(TestCaseFactory.Database(), "witness");

            ConversationLine saw = lines.Find(l => l.IsSighting && l.Sighting.Tick == 1);
            Assert.IsNotNull(saw);
            Assert.AreEqual("18:10에 B방에서 클라라가 서두르더군요.", saw.Text);
            Assert.IsFalse(lines.Exists(l => l.Key == "witness|saw"), "덮어쓰기 대사는 따로 나오지 않는다");
        }

        [Test]
        public void ConditionalLines_NeedEvidence()
        {
            var state = new InvestigationState(TestCaseFactory.Database());

            Assert.IsFalse(ConversationBuilder.Build(state, "culprit").Exists(l => l.Key == "culprit|on_glass"));
            state.CollectEvidence("ev_glass");
            Assert.IsTrue(ConversationBuilder.Build(state, "culprit").Exists(l => l.Key == "culprit|on_glass"));
        }

        [Test]
        public void Build_PrefersUnheardLines_ThenReplaysAll()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            List<ConversationLine> first = ConversationBuilder.Build(state, "culprit");
            for (int i = 0; i < first.Count; i++) state.MarkHeard("culprit", first[i].Key);

            // 새 단서 → 새 대사만
            state.CollectEvidence("ev_glass");
            List<ConversationLine> second = ConversationBuilder.Build(state, "culprit");
            Assert.AreEqual(1, second.Count);
            Assert.AreEqual("culprit|on_glass", second[0].Key);

            // 다 들었으면 처음부터 다시
            state.MarkHeard("culprit", second[0].Key);
            Assert.AreEqual(first.Count + 1, ConversationBuilder.Build(state, "culprit").Count);
        }

        [Test]
        public void Victim_HasNoConversation()
        {
            Assert.AreEqual(0, ConversationBuilder.AllLines(TestCaseFactory.Database(), "victim").Count);
        }
    }
}
