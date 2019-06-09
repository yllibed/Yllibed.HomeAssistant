using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable once CheckNamespace
namespace Yllibed.HomeAssistantFramework.Extensions
{
    internal static class StringJsonExtensions
    {
        internal static bool IsValidJson(this string text, out JToken json)
        {
            text = text.Trim();
            if ((text.StartsWith("{") && text.EndsWith("}")) || //For object
                (text.StartsWith("[") && text.EndsWith("]"))) //For array
            {
                try
                {
                    json = JToken.Parse(text);
                    return true;
                }
                catch (JsonReaderException jex)
                {
                    //Exception in parsing json
                    Console.WriteLine(jex.Message);
                    json = null;
                    return false;
                }
                catch (Exception ex) //some other exception
                {
                    Console.WriteLine(ex.ToString());
                    json = null;
                    return false;
                }
            }
            else
            {
                json = null;
                return false;
            }
        }

        internal static string ToPrettyJson(this string text)
        {
            var jsonString = JToken.Parse(text);
            return jsonString.ToString();
        }
    }
}
