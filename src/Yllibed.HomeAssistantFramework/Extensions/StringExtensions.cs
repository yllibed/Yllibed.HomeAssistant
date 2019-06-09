using System.Text.RegularExpressions;

namespace HomeAssistant.AppStarter.Extensions
{
    internal static class StringExtensions
    {
        internal static string WildcardToRegexExpression(this string text)
        {
            return "^" + Regex.Escape(text).Replace("\\*", ".*") + "$";
        }
    }
}
