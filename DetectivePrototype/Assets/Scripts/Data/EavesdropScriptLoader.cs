using Detective.Eavesdrop;
using UnityEngine;

namespace Detective.Data
{
    /// <summary>
    /// 엿듣기 대본을 Resources에서 읽는다. UnityEngine에 기대는 부분은 여기까지다 —
    /// 대본을 다루는 로직(ScriptTimeline·AudibilityModel·ScriptValidator)은 전부 순수 C#이라
    /// Unity 없이 검증된다(설계 원칙 18-1).
    /// </summary>
    public static class EavesdropScriptLoader
    {
        /// <summary>수직 슬라이스 대본. 방 2개·목소리 2개·90초.</summary>
        public const string SliceResourcePath = "GameData/cases/slice/script_slice";

        /// <summary>사건 하나의 기본 대본 경로. cases/&lt;caseId&gt;/script.json</summary>
        public static string ScriptResourcePath(string caseId)
        {
            return GameDataLoader.CasesResourceFolder + caseId + "/script";
        }

        /// <summary>Resources 경로에서 대본을 읽는다. 없거나 깨졌으면 빈 대본(검증기가 잡아낸다).</summary>
        public static ScriptDefinition Load(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError("[EavesdropScriptLoader] Resources/" + resourcePath + ".json 을 찾지 못했다.");
                return new ScriptDefinition().Normalized();
            }
            return Parse(asset.text, resourcePath);
        }

        /// <summary>텍스트에서 직접 파싱. 에디터 스크립트가 파일을 읽어 넘길 때도 쓴다.</summary>
        public static ScriptDefinition Parse(string json, string label)
        {
            ScriptDefinition script = GameDataLoader.Parse<ScriptDefinition>(json, label);
            return (script ?? new ScriptDefinition()).Normalized();
        }
    }
}
