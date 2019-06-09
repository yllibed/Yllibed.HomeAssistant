using System.Text.RegularExpressions;

namespace Yllibed.HomeAssistantFramework.Extensions
{
    internal static class StringExtensions
    {
        internal static string WildcardToRegexExpression(this string text)
        {
            return "^" + Regex.Escape(text).Replace("\\*", ".*") + "$";
        }
    }
}
