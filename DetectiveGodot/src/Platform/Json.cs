using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DetectiveGodot.Platform
{
    /// <summary>
    /// Unity <c>JsonUtility</c>와 **같게 동작하는** JSON 처리. 이 등가성이 이식의 핵심이다.
    ///
    /// 공유 스키마(<c>Detective.Data</c>)는 JsonUtility에 맞춰 설계돼 있다 — public 필드만,
    /// 딕셔너리 없음, 배열 + 문자열 id. 그런데 <c>System.Text.Json</c>은 기본적으로
    /// <b>프로퍼티</b>를 바인딩하고 필드를 무시한다. 그대로 쓰면 필드가 하나도 채워지지 않고,
    /// 예외도 없이 **전부 기본값인 객체**가 나온다. 조용히 빈 게임이 된다.
    ///
    /// 그래서 두 가지를 맞춘다.
    ///   1. <c>IncludeFields</c> — 필드를 본다.
    ///   2. 프로퍼티를 전부 버린다 — 스키마의 <c>MinX</c>·<c>TimeMs</c>·<c>RevealMs</c> 같은
    ///      계산 프로퍼티(경계 함수)가 JSON 키와 얽히지 않게 한다. JsonUtility가 프로퍼티를
    ///      아예 보지 않는 것과 같은 상태로 만든다.
    ///
    /// 대소문자는 **구분한다**(기본값). JsonUtility도 정확히 일치하는 이름만 읽으므로,
    /// 구분을 끄면 데이터의 <c>timeMs</c>가 프로퍼티 <c>TimeMs</c>로 붙으려 하는 사고가 난다.
    ///
    /// 한 가지 의도된 차이: 필드에 초기값이 있고 JSON에 그 키가 없을 때,
    /// JsonUtility는 0으로 덮어쓰지만 여기서는 초기값이 남는다. 이 저장소는 시각 없는 int에
    /// <c>-1</c>을 명시하는 규칙이라 실제 데이터에서는 두 쪽 결과가 같다.
    /// </summary>
    public static class Json
    {
        private static readonly JsonSerializerOptions Options = Build();

        private static JsonSerializerOptions Build()
        {
            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                WriteIndented = true,
            };
            options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { FieldsOnly }
            };
            return options;
        }

        /// <summary>프로퍼티에서 온 멤버를 모두 떼어 낸다 — JsonUtility가 보는 표면과 같게.</summary>
        private static void FieldsOnly(JsonTypeInfo info)
        {
            if (info.Kind != JsonTypeInfoKind.Object) return;
            for (int i = info.Properties.Count - 1; i >= 0; i--)
            {
                if (!(info.Properties[i].AttributeProvider is FieldInfo))
                    info.Properties.RemoveAt(i);
            }
        }

        /// <summary>실패하면 null을 돌려준다(예외를 던지지 않는다). 호출부가 대체값을 쓴다.</summary>
        public static T Parse<T>(string json, string label, out string error) where T : class
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = label + ": 내용이 비어 있다"; return null; }
            try
            {
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (JsonException e)
            {
                error = label + ": " + e.Message;
                return null;
            }
        }

        public static string Write<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }
    }
}
