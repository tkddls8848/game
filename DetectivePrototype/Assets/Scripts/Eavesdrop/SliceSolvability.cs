using System.Collections.Generic;
using Detective.Core;
using Detective.Data;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 청취점이 한 회차 동안 어느 방에 있었는가. <c>atMs</c>부터 다음 정지점 전까지 <c>room</c>에 있다.
    /// 첫 정지점 이전(또는 정지점이 없을 때)은 어디에도 없다. 이동은 순간적이라고 본다.
    /// </summary>
    public sealed class ListeningPlan
    {
        private readonly List<int> _atMs = new List<int>();
        private readonly List<string> _rooms = new List<string>();

        public static ListeningPlan Fixed(string room)
        {
            return new ListeningPlan().Then(0, room);
        }

        /// <summary>atMs부터 room으로 옮긴다. 시각이 오름차순이 아니면 무시한다.</summary>
        public ListeningPlan Then(int atMs, string room)
        {
            if (_atMs.Count > 0 && atMs <= _atMs[_atMs.Count - 1]) return this;
            _atMs.Add(atMs);
            _rooms.Add(room);
            return this;
        }

        /// <summary>timeMs 순간 청취점이 있는 방. 아직 아무 데도 없으면 null.</summary>
        public string RoomAt(int timeMs)
        {
            string room = null;
            for (int i = 0; i < _atMs.Count; i++)
            {
                if (_atMs[i] > timeMs) break;
                room = _rooms[i];
            }
            return room;
        }

        /// <summary>[startMs, endMs) 내내 한 방에 머무르면 그 방, 중간에 옮기면 null.</summary>
        public string StayedRoomDuring(int startMs, int endMs)
        {
            string room = RoomAt(startMs);
            if (room == null) return null;
            for (int i = 0; i < _atMs.Count; i++)
            {
                if (_atMs[i] > startMs && _atMs[i] < endMs && _rooms[i] != room) return null;
            }
            return room;
        }
    }

    /// <summary>SliceSolvability.Check의 결과.</summary>
    public sealed class SliceSolvabilityReport
    {
        /// <summary>대본 무결성 문제(없는 방, 없는 발화 참조, 회차 밖 발화 등).</summary>
        public readonly List<string> Problems = new List<string>();

        /// <summary>결론들이 요구하는 사실 ID(중복 없음, 등장 순).</summary>
        public readonly List<string> RequiredFacts = new List<string>();

        /// <summary>사실 ID → 그 사실을 Full로 들을 수 있는 고정 청취 방 목록.</summary>
        public readonly Dictionary<string, List<string>> FullRoomsByFact = new Dictionary<string, List<string>>();

        /// <summary>방 ID → 그 방에 회차 내내 고정돼 있을 때 알게 되는 사실 목록.</summary>
        public readonly Dictionary<string, List<string>> FactsByFixedRoom = new Dictionary<string, List<string>>();

        /// <summary>(b)를 깨는 방: 거기 고정돼 있기만 해도 필요한 사실을 전부 듣는다.</summary>
        public readonly List<string> RoomsHearingEverything = new List<string>();

        /// <summary>(a) 필요한 사실이 각각 어딘가에서는 Full로 들린다.</summary>
        public bool EveryFactAudibleSomewhere;

        /// <summary>(b) 어느 방에 고정돼 있어도 필요한 사실 전부는 못 듣는다.</summary>
        public bool NoSingleRoomSuffices;

        /// <summary>
        /// 참고: 한 번의 재생에서 방을 옮겨 다니며 전부 들을 수 있는가.
        /// false면 "되돌려 다시 들어야" 풀린다 — 필요한 발화들이 서로 다른 방에서 동시에 울린다.
        /// </summary>
        public bool SinglePassPossible;

        public bool IsSolvable
        {
            get { return Problems.Count == 0 && EveryFactAudibleSomewhere && NoSingleRoomSuffices; }
        }
    }

    /// <summary>
    /// 엿듣기 수직 슬라이스의 추리 가능성 검사(DEVELOPMENT_PLAN_UNHEARD.md §5 중 1·2번).
    /// (a) 결론에 필요한 사실이 각각 어딘가에서는 Full로 들린다
    /// (b) 한 방에 회차 내내 고정돼 있으면 전부는 못 듣는다
    /// 순수 C#. 시각은 정수 밀리초만 비교한다.
    /// </summary>
    public static class SliceSolvability
    {
        /// <summary>발화를 처음부터 끝까지 같은 방에서 Full로 들었는가. 도중에 방을 옮기면 못 들은 것이다.</summary>
        public static bool HeardFully(Utterance utterance, ListeningPlan plan, AudibilityModel model)
        {
            if (utterance == null || plan == null || model == null) return false;
            string room = plan.StayedRoomDuring(utterance.startMs, utterance.EndMs);
            return room != null && model.Judge(room, utterance.room) == Audibility.Full;
        }

        public static HashSet<string> HeardUtteranceIds(ScriptTimeline timeline, AudibilityModel model, ListeningPlan plan)
        {
            var heard = new HashSet<string>();
            IList<Utterance> all = timeline.Utterances;
            for (int i = 0; i < all.Count; i++)
            {
                if (HeardFully(all[i], plan, model)) heard.Add(all[i].id);
            }
            return heard;
        }

        /// <summary>들은 발화로부터 알게 된 사실. 사실의 발화 중 하나라도 들었으면 안다.</summary>
        public static HashSet<string> KnownFacts(ScriptDefinition script, ICollection<string> heardUtteranceIds)
        {
            var known = new HashSet<string>();
            for (int i = 0; i < script.facts.Length; i++)
            {
                ScriptFact fact = script.facts[i];
                if (fact == null) continue;
                for (int u = 0; u < fact.utteranceIds.Length; u++)
                {
                    if (heardUtteranceIds.Contains(fact.utteranceIds[u])) { known.Add(fact.id); break; }
                }
            }
            return known;
        }

        /// <summary>아는 사실로 도출되는 결론 ID.</summary>
        public static List<string> DerivedConclusions(ScriptDefinition script, ICollection<string> knownFacts)
        {
            var derived = new List<string>();
            for (int i = 0; i < script.conclusions.Length; i++)
            {
                ScriptConclusion c = script.conclusions[i];
                if (c == null || c.requiresFacts.Length == 0) continue;
                bool all = true;
                for (int f = 0; f < c.requiresFacts.Length; f++)
                {
                    if (!knownFacts.Contains(c.requiresFacts[f])) { all = false; break; }
                }
                if (all) derived.Add(c.id);
            }
            return derived;
        }

        public static SliceSolvabilityReport Check(ScriptDefinition script, RoomLayout layout)
        {
            var report = new SliceSolvabilityReport();
            if (script == null) { report.Problems.Add("대본이 없다"); return report; }
            if (layout == null) { report.Problems.Add("맵이 없다"); return report; }
            script.Normalized();

            var timeline = new ScriptTimeline(script);
            var model = new AudibilityModel(layout);
            var factsById = ValidateReferences(script, layout, report.Problems);

            for (int i = 0; i < script.conclusions.Length; i++)
            {
                ScriptConclusion c = script.conclusions[i];
                if (c == null) continue;
                for (int f = 0; f < c.requiresFacts.Length; f++)
                {
                    if (!report.RequiredFacts.Contains(c.requiresFacts[f])) report.RequiredFacts.Add(c.requiresFacts[f]);
                }
            }
            if (report.RequiredFacts.Count == 0) report.Problems.Add("결론이 요구하는 사실이 하나도 없다");

            // 방마다 회차 내내 고정 청취 → 무엇을 알게 되는가
            IList<RoomDefinition> rooms = layout.Rooms;
            for (int r = 0; r < rooms.Count; r++)
            {
                string roomId = rooms[r].id;
                HashSet<string> heard = HeardUtteranceIds(timeline, model, ListeningPlan.Fixed(roomId));
                HashSet<string> known = KnownFacts(script, heard);

                var knownRequired = new List<string>();
                for (int f = 0; f < report.RequiredFacts.Count; f++)
                {
                    string factId = report.RequiredFacts[f];
                    if (!known.Contains(factId)) continue;
                    knownRequired.Add(factId);

                    List<string> where;
                    if (!report.FullRoomsByFact.TryGetValue(factId, out where))
                    {
                        where = new List<string>();
                        report.FullRoomsByFact[factId] = where;
                    }
                    where.Add(roomId);
                }
                report.FactsByFixedRoom[roomId] = knownRequired;

                if (report.RequiredFacts.Count > 0 && knownRequired.Count == report.RequiredFacts.Count)
                {
                    report.RoomsHearingEverything.Add(roomId);
                }
            }

            // (a)
            report.EveryFactAudibleSomewhere = report.RequiredFacts.Count > 0;
            for (int f = 0; f < report.RequiredFacts.Count; f++)
            {
                if (!report.FullRoomsByFact.ContainsKey(report.RequiredFacts[f]))
                {
                    report.EveryFactAudibleSomewhere = false;
                    report.Problems.Add("사실 '" + report.RequiredFacts[f] + "'은(는) 어느 방에서도 온전히 들리지 않는다");
                }
            }

            // (b)
            report.NoSingleRoomSuffices = report.RoomsHearingEverything.Count == 0;
            for (int i = 0; i < report.RoomsHearingEverything.Count; i++)
            {
                report.Problems.Add("방 '" + report.RoomsHearingEverything[i] + "'에 고정돼 있기만 해도 필요한 사실을 전부 듣는다");
            }

            report.SinglePassPossible = report.EveryFactAudibleSomewhere
                && CanCollectInOnePass(report.RequiredFacts, factsById, timeline, model);
            return report;
        }

        /// <summary>
        /// 한 회차 안에서 방을 옮겨 가며 필요한 사실을 모두 모을 수 있는가.
        /// 사실마다 발화 하나씩 고른 조합에서, 시간이 겹치는 두 발화가 전부 같은 방이면 한 번에 들을 수 있다.
        /// (이동은 순간적이라고 본다 — 가장 너그러운 가정이라 false면 확실히 되돌려 들어야 한다.)
        /// </summary>
        private static bool CanCollectInOnePass(List<string> requiredFacts, Dictionary<string, ScriptFact> factsById,
                                                ScriptTimeline timeline, AudibilityModel model)
        {
            var candidates = new List<List<Utterance>>();
            for (int f = 0; f < requiredFacts.Count; f++)
            {
                var carriers = new List<Utterance>();
                ScriptFact fact;
                if (factsById.TryGetValue(requiredFacts[f], out fact))
                {
                    for (int u = 0; u < fact.utteranceIds.Length; u++)
                    {
                        Utterance utt;
                        if (!timeline.TryGet(fact.utteranceIds[u], out utt)) continue;
                        if (model.Judge(utt.room, utt.room) != Audibility.Full) continue;
                        carriers.Add(utt);
                    }
                }
                if (carriers.Count == 0) return false;
                candidates.Add(carriers);
            }
            return Search(candidates, 0, new List<Utterance>());
        }

        private static bool Search(List<List<Utterance>> candidates, int index, List<Utterance> chosen)
        {
            if (index == candidates.Count) return true;
            List<Utterance> options = candidates[index];
            for (int i = 0; i < options.Count; i++)
            {
                Utterance next = options[i];
                bool compatible = true;
                for (int c = 0; c < chosen.Count; c++)
                {
                    if (chosen[c].room != next.room && ScriptTimeline.Overlaps(chosen[c], next)) { compatible = false; break; }
                }
                if (!compatible) continue;

                chosen.Add(next);
                if (Search(candidates, index + 1, chosen)) return true;
                chosen.RemoveAt(chosen.Count - 1);
            }
            return false;
        }

        private static Dictionary<string, ScriptFact> ValidateReferences(ScriptDefinition script, RoomLayout layout, List<string> problems)
        {
            if (script.durationMs <= 0) problems.Add("durationMs가 0 이하다(빠진 int는 0으로 읽힌다)");

            var voices = new HashSet<string>();
            for (int i = 0; i < script.speakers.Length; i++)
            {
                if (script.speakers[i] != null && !string.IsNullOrEmpty(script.speakers[i].voiceId)) voices.Add(script.speakers[i].voiceId);
            }

            var utteranceIds = new HashSet<string>();
            for (int i = 0; i < script.utterances.Length; i++)
            {
                Utterance u = script.utterances[i];
                if (u == null) { problems.Add("utterances[" + i + "]가 비어 있다"); continue; }
                string label = "발화 '" + u.id + "'";
                if (string.IsNullOrEmpty(u.id)) problems.Add("utterances[" + i + "]에 id가 없다");
                else if (!utteranceIds.Add(u.id)) problems.Add(label + " id가 중복된다");
                if (u.startMs < 0) problems.Add(label + " startMs가 음수다");
                if (u.durationMs <= 0) problems.Add(label + " durationMs가 0 이하다");
                if (script.durationMs > 0 && u.EndMs > script.durationMs) problems.Add(label + "이(가) 회차 끝을 넘긴다");
                RoomDefinition room;
                if (!layout.TryGetRoom(u.room, out room)) problems.Add(label + "의 방 '" + u.room + "'이(가) 맵에 없다");
                if (!voices.Contains(u.voiceId ?? string.Empty)) problems.Add(label + "의 목소리 '" + u.voiceId + "'이(가) speakers에 없다");
            }

            var factsById = new Dictionary<string, ScriptFact>();
            for (int i = 0; i < script.facts.Length; i++)
            {
                ScriptFact fact = script.facts[i];
                if (fact == null || string.IsNullOrEmpty(fact.id)) { problems.Add("facts[" + i + "]에 id가 없다"); continue; }
                if (factsById.ContainsKey(fact.id)) { problems.Add("사실 '" + fact.id + "' id가 중복된다"); continue; }
                factsById[fact.id] = fact;
                if (fact.utteranceIds.Length == 0) problems.Add("사실 '" + fact.id + "'을(를) 전하는 발화가 없다");
                for (int u = 0; u < fact.utteranceIds.Length; u++)
                {
                    if (!utteranceIds.Contains(fact.utteranceIds[u] ?? string.Empty))
                        problems.Add("사실 '" + fact.id + "'이(가) 없는 발화 '" + fact.utteranceIds[u] + "'을(를) 가리킨다");
                }
            }

            for (int i = 0; i < script.conclusions.Length; i++)
            {
                ScriptConclusion c = script.conclusions[i];
                if (c == null) continue;
                for (int f = 0; f < c.requiresFacts.Length; f++)
                {
                    if (!factsById.ContainsKey(c.requiresFacts[f] ?? string.Empty))
                        problems.Add("결론 '" + c.id + "'이(가) 없는 사실 '" + c.requiresFacts[f] + "'을(를) 요구한다");
                }
            }
            return factsById;
        }
    }
}
