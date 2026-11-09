using System.Globalization;

namespace MoaKnights.Localization
{
    /// <summary>게임 코드가 화면 글을 얻는 유일한 입구. 구현을 바꿔도 호출부는 그대로다.</summary>
    public static class Text
    {
        private static TextTable _table;

        public static string Language { get; private set; } = string.Empty;

        public static void Load(TextTable table, string language)
        {
            _table = table;
            Language = language;
        }

        /// <summary>없는 키는 화면에서 바로 보이도록 [키]를 돌려준다.</summary>
        public static string Get(string key)
        {
            return _table != null && _table.TryGet(key, Language, out var value) ? value : "[" + key + "]";
        }

        /// <summary>표의 문장에 값을 넣는다. {0} 자리표시자는 언어별로 순서를 바꿀 수 있다.</summary>
        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }
    }
}
