using System.Globalization;
using System.Resources;

namespace FileHistory
{
    /// <summary>
    /// 多言語文字列リソースへのアクセサ
    /// (Strings.resx = 英語, Strings.ja.resx = 日本語)
    /// </summary>
    internal static class Strings
    {
        static readonly ResourceManager _rm = new ResourceManager("FileHistory.Strings", typeof(Strings).Assembly);

        public static string Get(string key)
            => _rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public static string Format(string key, params object[] args)
            => string.Format(Get(key), args);
    }
}
