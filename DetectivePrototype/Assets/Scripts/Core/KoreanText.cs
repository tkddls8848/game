namespace Detective.Core
{
    /// <summary>
    /// 이름 뒤 조사(을/를, 이/가, 와/과, 은/는) 선택. 자동 생성 대사가 "클라라을" 같은 문장을 만들지 않게 한다.
    /// 한글이 아닌 글자로 끝나면 받침 없는 쪽을 쓴다.
    /// </summary>
    public static class KoreanText
    {
        public static bool HasFinalConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;

            char last = word[word.Length - 1];
            if (last < '가' || last > '힣') return false;
            return (last - '가') % 28 != 0;
        }

        public static string EulReul(string word) { return word + (HasFinalConsonant(word) ? "을" : "를"); }
        public static string IGa(string word) { return word + (HasFinalConsonant(word) ? "이" : "가"); }
        public static string WaGwa(string word) { return word + (HasFinalConsonant(word) ? "과" : "와"); }
        public static string EunNeun(string word) { return word + (HasFinalConsonant(word) ? "은" : "는"); }
    }
}
