using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.NPC;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 대본 참조 무결성 검사(순수 C#). 없는 방·없는 인물·중복 ID·구간을 벗어난 발화를 잡아낸다.
    ///
    /// 여기서 잡히는 것은 "대본이 말이 되는가"이지 "풀 수 있는가"가 아니다.
    /// 풀 수 있는지는 <see cref="SliceSolvability"/>가 본다.
    /// </summary>
    public static class ScriptValidator
    {
        /// <summary>문제 목록. 비어 있으면 통과. layout·roster는 null이면 그 항목 검사를 건너뛴다.</summary>
        public static List<string> Validate(ScriptDefinition script, RoomLayout layout, NpcRoster roster)
        {
            var errors = new List<string>();
            if (script == null)
            {
                errors.Add("대본을 읽지 못했다.");
                return errors;
            }
            script.Normalized();

            if (string.IsNullOrEmpty(script.caseId)) errors.Add("caseId가 비어 있다.");

            // JsonUtility는 빠진 int를 0으로 채운다. 스키마 기본값이 -1이므로 둘 다 걸러 낸다.
            if (script.durationMs <= 0)
                errors.Add("durationMs가 " + script.durationMs + "다. 한 회차 길이를 0보다 크게 적어야 한다(빠뜨리면 0이 된다).");

            var voiceIds = new HashSet<string>();
            for (int i = 0; i < script.speakers.Length; i++)
            {
                SpeakerDefinition speaker = script.speakers[i];
                string label = "speakers[" + i + "]";
                if (speaker == null) { errors.Add(label + "가 비어 있다."); continue; }

                if (string.IsNullOrEmpty(speaker.voiceId)) errors.Add(label + ": voiceId가 비어 있다.");
                else if (!voiceIds.Add(speaker.voiceId)) errors.Add(label + ": voiceId '" + speaker.voiceId + "'가 중복된다.");

                if (string.IsNullOrEmpty(speaker.npcId)) errors.Add(label + ": npcId가 비어 있다.");
                else if (roster != null)
                {
                    NpcDefinition npc;
                    if (!roster.TryGet(speaker.npcId, out npc))
                        errors.Add(label + ": npcId '" + speaker.npcId + "'가 없는 인물이다.");
                }
            }

            var utteranceIds = new HashSet<string>();
            for (int i = 0; i < script.utterances.Length; i++)
            {
                Utterance u = script.utterances[i];
                string label = "utterances[" + i + "]";
                if (u == null) { errors.Add(label + "가 비어 있다."); continue; }

                if (string.IsNullOrEmpty(u.id)) errors.Add(label + ": id가 비어 있다.");
                else
                {
                    label = label + "('" + u.id + "')";
                    if (!utteranceIds.Add(u.id)) errors.Add(label + ": id가 중복된다.");
                }

                if (u.startMs < 0) errors.Add(label + ": startMs가 음수다(" + u.startMs + ").");
                if (u.durationMs <= 0) errors.Add(label + ": durationMs가 0 이하다(" + u.durationMs + ").");
                if (script.durationMs > 0 && u.EndMs > script.durationMs)
                    errors.Add(label + ": " + u.EndMs + "ms에 끝나 회차 길이 " + script.durationMs + "ms를 넘는다.");

                if (string.IsNullOrEmpty(u.text)) errors.Add(label + ": text가 비어 있다.");

                if (string.IsNullOrEmpty(u.voiceId)) errors.Add(label + ": voiceId가 비어 있다.");
                else if (script.speakers.Length > 0 && !voiceIds.Contains(u.voiceId))
                    errors.Add(label + ": voiceId '" + u.voiceId + "'가 speakers에 없다.");

                if (string.IsNullOrEmpty(u.room)) errors.Add(label + ": room이 비어 있다.");
                else if (layout != null)
                {
                    RoomDefinition room;
                    if (!layout.TryGetRoom(u.room, out room))
                        errors.Add(label + ": room '" + u.room + "'이 없는 방이다.");
                }
            }

            var factIds = new HashSet<string>();
            for (int i = 0; i < script.facts.Length; i++)
            {
                ScriptFact fact = script.facts[i];
                string label = "facts[" + i + "]";
                if (fact == null) { errors.Add(label + "가 비어 있다."); continue; }

                if (string.IsNullOrEmpty(fact.id)) errors.Add(label + ": id가 비어 있다.");
                else
                {
                    label = label + "('" + fact.id + "')";
                    if (!factIds.Add(fact.id)) errors.Add(label + ": id가 중복된다.");
                }

                if (fact.utteranceIds.Length == 0)
                    errors.Add(label + ": 이 사실을 알려 주는 발화가 하나도 없다.");
                for (int k = 0; k < fact.utteranceIds.Length; k++)
                {
                    if (!utteranceIds.Contains(fact.utteranceIds[k]))
                        errors.Add(label + ": 없는 발화 '" + fact.utteranceIds[k] + "'를 가리킨다.");
                }
            }

            var conclusionIds = new HashSet<string>();
            for (int i = 0; i < script.conclusions.Length; i++)
            {
                ScriptConclusion conclusion = script.conclusions[i];
                string label = "conclusions[" + i + "]";
                if (conclusion == null) { errors.Add(label + "가 비어 있다."); continue; }

                if (string.IsNullOrEmpty(conclusion.id)) errors.Add(label + ": id가 비어 있다.");
                else
                {
                    label = label + "('" + conclusion.id + "')";
                    if (!conclusionIds.Add(conclusion.id)) errors.Add(label + ": id가 중복된다.");
                }

                if (string.IsNullOrEmpty(conclusion.text)) errors.Add(label + ": text가 비어 있다.");

                if (conclusion.requiresFacts.Length == 0)
                    errors.Add(label + ": 필요한 사실이 하나도 적혀 있지 않다.");
                for (int k = 0; k < conclusion.requiresFacts.Length; k++)
                {
                    if (!factIds.Contains(conclusion.requiresFacts[k]))
                        errors.Add(label + ": 없는 사실 '" + conclusion.requiresFacts[k] + "'를 가리킨다.");
                }
            }

            return errors;
        }
    }
}
