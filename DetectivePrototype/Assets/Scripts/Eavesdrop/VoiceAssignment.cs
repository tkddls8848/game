using System.Collections.Generic;

namespace Detective.Eavesdrop
{
    /// <summary>이름을 배정했을 때 보드에 실제로 일어난 일.</summary>
    public enum VoiceAssignResult
    {
        /// <summary>달라진 것이 없다(이미 같은 이름이 붙어 있었거나, 빈 칸을 또 비웠다).</summary>
        Unchanged = 0,

        /// <summary>빈 칸에 이름이 붙었다.</summary>
        Assigned = 1,

        /// <summary>붙어 있던 이름을 뗐다.</summary>
        Cleared = 2,

        /// <summary>다른 목소리에 붙어 있던 이름을 떼어 왔다. 그쪽 칸은 비었다.</summary>
        Displaced = 3,

        /// <summary>보드에 없는 목소리다. 보드는 그대로다.</summary>
        UnknownVoice = 4
    }

    /// <summary>
    /// 배정 보드 채점 결과. 정답에 적힌 목소리 하나하나를 맞음·틀림·미배정 셋으로 가른다.
    /// 전부 맞아야만 하는 것이 아니라 **몇 개를 맞췄는가**가 남는다(부분 정답).
    /// </summary>
    public sealed class VoiceAssignmentGrade
    {
        private readonly List<string> _correct = new List<string>();
        private readonly List<string> _wrong = new List<string>();
        private readonly List<string> _unassigned = new List<string>();

        internal void AddCorrect(string voiceId) { _correct.Add(voiceId); }
        internal void AddWrong(string voiceId) { _wrong.Add(voiceId); }
        internal void AddUnassigned(string voiceId) { _unassigned.Add(voiceId); }

        /// <summary>이름을 제대로 맞힌 목소리들(정답 순).</summary>
        public IList<string> CorrectVoices { get { return _correct.AsReadOnly(); } }

        /// <summary>이름을 붙였지만 틀린 목소리들.</summary>
        public IList<string> WrongVoices { get { return _wrong.AsReadOnly(); } }

        /// <summary>끝내 이름을 못 붙인 목소리들.</summary>
        public IList<string> UnassignedVoices { get { return _unassigned.AsReadOnly(); } }

        public int CorrectCount { get { return _correct.Count; } }
        public int WrongCount { get { return _wrong.Count; } }
        public int UnassignedCount { get { return _unassigned.Count; } }

        /// <summary>정답이 아는 목소리 수. 0이면 채점할 것이 없었다는 뜻이다.</summary>
        public int TotalCount { get { return _correct.Count + _wrong.Count + _unassigned.Count; } }

        /// <summary>전부 맞혔는가. 채점할 목소리가 하나도 없으면 false다(빈 정답을 만점으로 치지 않는다).</summary>
        public bool IsPerfect { get { return TotalCount > 0 && _correct.Count == TotalCount; } }

        public bool IsCorrect(string voiceId)
        {
            return !string.IsNullOrEmpty(voiceId) && _correct.Contains(voiceId);
        }
    }

    /// <summary>
    /// 목소리 → 이름 배정 보드(순수 C#, Phase U-3).
    ///
    /// 플레이어에게 인물은 처음부터 끝까지 익명이다. 화면에 나오는 것은 목소리 id(v1..v5)뿐이고,
    /// 누가 누구인지 알아내는 것 자체가 퍼즐이다. 이 클래스가 그 답안지를 들고 있는다.
    ///
    /// 정답은 대본의 <see cref="ScriptDefinition.speakers"/>(voiceId → npcId)다.
    /// 보드는 정답을 **갖고 있지 않는다** — 목소리 id만 알고, 채점할 때 정답을 받아 대조한다.
    /// 보드가 답을 들고 있으면 UI에서 새어 나갈 길이 생긴다.
    ///
    /// 한 인물은 한 목소리에만 붙는다. 같은 이름을 다른 목소리에 붙이면 **원래 칸에서 떨어진다**
    /// (<see cref="VoiceAssignResult.Displaced"/>) — 충돌 상태를 만들어 두고 나중에 잡는 대신
    /// 애초에 생기지 않게 한다. 두 목소리가 같은 사람일 리 없다는 것은 플레이어도 아는 규칙이라,
    /// 오류 메시지보다 "저쪽에서 떨어져 나왔다"를 보여 주는 편이 보드로서 정직하다.
    /// </summary>
    public sealed class VoiceAssignment
    {
        private readonly List<string> _voiceIds = new List<string>();
        private readonly Dictionary<string, string> _npcByVoice = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _voiceByNpc = new Dictionary<string, string>();

        /// <summary>보드에 올릴 목소리 id들. 빈 값과 중복은 버리고 준 순서를 지킨다.</summary>
        public VoiceAssignment(IEnumerable<string> voiceIds)
        {
            if (voiceIds == null) return;
            foreach (string voiceId in voiceIds)
            {
                if (string.IsNullOrEmpty(voiceId)) continue;
                if (_voiceIds.Contains(voiceId)) continue;
                _voiceIds.Add(voiceId);
            }
        }

        /// <summary>대본에 등장하는 목소리로 빈 보드를 만든다. npcId(정답)는 읽지 않는다.</summary>
        public static VoiceAssignment ForScript(ScriptDefinition script)
        {
            var voices = new List<string>();
            if (script != null)
            {
                script.Normalized();
                for (int i = 0; i < script.speakers.Length; i++)
                {
                    SpeakerDefinition speaker = script.speakers[i];
                    if (speaker != null) voices.Add(speaker.voiceId);
                }
            }
            return new VoiceAssignment(voices);
        }

        /// <summary>보드에 있는 목소리들(화면 순서).</summary>
        public IList<string> VoiceIds { get { return _voiceIds.AsReadOnly(); } }

        public int VoiceCount { get { return _voiceIds.Count; } }

        /// <summary>이름이 붙은 칸 수.</summary>
        public int AssignedCount { get { return _npcByVoice.Count; } }

        /// <summary>모든 목소리에 이름이 붙었는가. 빈 보드는 완성으로 치지 않는다.</summary>
        public bool IsComplete { get { return _voiceIds.Count > 0 && _npcByVoice.Count == _voiceIds.Count; } }

        public bool HasVoice(string voiceId)
        {
            return !string.IsNullOrEmpty(voiceId) && _voiceIds.Contains(voiceId);
        }

        /// <summary>이 목소리에 붙어 있는 이름. 없으면 빈 문자열.</summary>
        public string AssignedNpc(string voiceId)
        {
            string npcId;
            if (!string.IsNullOrEmpty(voiceId) && _npcByVoice.TryGetValue(voiceId, out npcId)) return npcId;
            return string.Empty;
        }

        /// <summary>이 이름이 붙어 있는 목소리. 없으면 빈 문자열.</summary>
        public string VoiceOf(string npcId)
        {
            string voiceId;
            if (!string.IsNullOrEmpty(npcId) && _voiceByNpc.TryGetValue(npcId, out voiceId)) return voiceId;
            return string.Empty;
        }

        public bool IsAssigned(string voiceId)
        {
            return !string.IsNullOrEmpty(AssignedNpc(voiceId));
        }

        /// <summary>
        /// 지금 이 이름을 이 목소리에 붙이면 어느 칸이 비는가. 비는 칸이 없으면 빈 문자열.
        /// UI가 "마르코를 여기 붙이면 v3이 빕니다"를 미리 보여 줄 때 쓴다.
        /// </summary>
        public string WouldDisplace(string voiceId, string npcId)
        {
            string holder = VoiceOf(npcId);
            return holder != voiceId ? holder : string.Empty;
        }

        /// <summary>
        /// 목소리에 이름을 붙인다. npcId가 비어 있으면 <see cref="Clear"/>와 같다.
        /// 이미 다른 목소리에 붙어 있던 이름이면 그쪽에서 떨어진다.
        /// </summary>
        public VoiceAssignResult Assign(string voiceId, string npcId)
        {
            if (!HasVoice(voiceId)) return VoiceAssignResult.UnknownVoice;
            if (string.IsNullOrEmpty(npcId)) return Clear(voiceId);

            string current = AssignedNpc(voiceId);
            if (current == npcId) return VoiceAssignResult.Unchanged;

            string previousHolder = VoiceOf(npcId);
            bool displaced = !string.IsNullOrEmpty(previousHolder);
            if (displaced) Detach(previousHolder);

            if (!string.IsNullOrEmpty(current)) Detach(voiceId);

            _npcByVoice[voiceId] = npcId;
            _voiceByNpc[npcId] = voiceId;
            return displaced ? VoiceAssignResult.Displaced : VoiceAssignResult.Assigned;
        }

        /// <summary>이 목소리에서 이름을 뗀다.</summary>
        public VoiceAssignResult Clear(string voiceId)
        {
            if (!HasVoice(voiceId)) return VoiceAssignResult.UnknownVoice;
            if (!IsAssigned(voiceId)) return VoiceAssignResult.Unchanged;
            Detach(voiceId);
            return VoiceAssignResult.Cleared;
        }

        /// <summary>보드를 전부 비운다.</summary>
        public void ClearAll()
        {
            _npcByVoice.Clear();
            _voiceByNpc.Clear();
        }

        /// <summary>
        /// 후보 이름들을 앞뒤로 돌린다. 고리는 [비움, 후보…] 순이고 끝에서 처음으로 넘어간다.
        /// 다른 칸이 쥐고 있는 이름으로 넘어가면 그쪽에서 떨어져 나온다(<see cref="Assign"/>과 같은 규칙).
        /// </summary>
        public VoiceAssignResult Cycle(string voiceId, IList<string> candidateNpcIds, int delta)
        {
            if (!HasVoice(voiceId)) return VoiceAssignResult.UnknownVoice;

            var ring = new List<string>();
            ring.Add(string.Empty); // 비움
            if (candidateNpcIds != null)
            {
                for (int i = 0; i < candidateNpcIds.Count; i++)
                {
                    string candidate = candidateNpcIds[i];
                    if (string.IsNullOrEmpty(candidate) || ring.Contains(candidate)) continue;
                    ring.Add(candidate);
                }
            }
            if (ring.Count <= 1) return VoiceAssignResult.Unchanged;

            int index = ring.IndexOf(AssignedNpc(voiceId));
            if (index < 0) index = 0;

            int next = (index + delta) % ring.Count;
            if (next < 0) next += ring.Count;
            return Assign(voiceId, ring[next]);
        }

        /// <summary>
        /// 붙어 있는 배정만 보드 순서대로 내놓는다. 저장하거나 채점기에 넘길 때 쓴다.
        /// 정답과 같은 <see cref="SpeakerDefinition"/> 모양이라 JsonUtility로 그대로 써도 된다.
        /// </summary>
        public SpeakerDefinition[] ToPairs()
        {
            var pairs = new List<SpeakerDefinition>();
            for (int i = 0; i < _voiceIds.Count; i++)
            {
                string voiceId = _voiceIds[i];
                string npcId = AssignedNpc(voiceId);
                if (string.IsNullOrEmpty(npcId)) continue;
                pairs.Add(new SpeakerDefinition { voiceId = voiceId, npcId = npcId });
            }
            return pairs.ToArray();
        }

        /// <summary>
        /// 저장해 둔 배정을 되돌린다. 보드를 먼저 비우고 하나씩 붙이며,
        /// 받아들일 수 없던 줄을 문제 목록으로 돌려준다(비어 있으면 그대로 복원됐다는 뜻).
        /// </summary>
        public List<string> Restore(SpeakerDefinition[] pairs)
        {
            var problems = new List<string>();
            ClearAll();
            if (pairs == null) return problems;

            for (int i = 0; i < pairs.Length; i++)
            {
                SpeakerDefinition pair = pairs[i];
                string label = "배정[" + i + "]";
                if (pair == null) { problems.Add(label + "가 비어 있다."); continue; }

                if (!HasVoice(pair.voiceId))
                {
                    problems.Add(label + ": 보드에 없는 목소리 '" + pair.voiceId + "'다.");
                    continue;
                }

                string stolenFrom = WouldDisplace(pair.voiceId, pair.npcId);
                VoiceAssignResult result = Assign(pair.voiceId, pair.npcId);
                if (result == VoiceAssignResult.Displaced)
                    problems.Add(label + ": '" + pair.npcId + "'이(가) '" + stolenFrom + "'에도 배정돼 있어 그쪽을 비웠다.");
            }
            return problems;
        }

        /// <summary>대본의 정답표와 대조한다.</summary>
        public VoiceAssignmentGrade Grade(ScriptDefinition script)
        {
            if (script == null) return new VoiceAssignmentGrade();
            script.Normalized();
            return Grade(script.speakers);
        }

        /// <summary>
        /// 정답표(voiceId → npcId)와 대조한다. 채점 대상은 **정답이 아는 목소리**들이다 —
        /// 정답에 npcId가 비어 있는 줄은 무엇을 붙여도 맞지 않는다(<c>CaseGrader</c>와 같은 규칙).
        /// </summary>
        public VoiceAssignmentGrade Grade(SpeakerDefinition[] answer)
        {
            var grade = new VoiceAssignmentGrade();
            if (answer == null) return grade;

            var seen = new HashSet<string>();
            for (int i = 0; i < answer.Length; i++)
            {
                SpeakerDefinition expected = answer[i];
                if (expected == null || string.IsNullOrEmpty(expected.voiceId)) continue;
                if (!seen.Add(expected.voiceId)) continue;

                string assigned = AssignedNpc(expected.voiceId);
                if (string.IsNullOrEmpty(assigned)) grade.AddUnassigned(expected.voiceId);
                else if (!string.IsNullOrEmpty(expected.npcId) && assigned == expected.npcId) grade.AddCorrect(expected.voiceId);
                else grade.AddWrong(expected.voiceId);
            }
            return grade;
        }

        private void Detach(string voiceId)
        {
            string npcId;
            if (!_npcByVoice.TryGetValue(voiceId, out npcId)) return;
            _npcByVoice.Remove(voiceId);
            _voiceByNpc.Remove(npcId);
        }
    }
}
