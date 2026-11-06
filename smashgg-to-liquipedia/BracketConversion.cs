using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace smashgg_to_liquipedia
{
    public class BracketConversion
    {
        private const string DATA_FILENAME = @"BracketConversions.json";
        private static readonly Lazy<BracketConversion> _lazyInstance = new Lazy<BracketConversion>(
            () => new BracketConversion()
        );
        public static BracketConversion Instance => _lazyInstance.Value;
        private static readonly ISet<string> BracketIds = new HashSet<string>();
        private static readonly Random Random = new Random();
        private const string ALPHANUMERICS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        private static readonly Regex BracketNameRegex = new Regex(
            @"\{\{(?<bracketName>[^\r\n]+)", RegexOptions.Compiled
        );

        // `|id=` with nothing after it but optional spaces before the next parameter, `}}` or line end
        private static readonly Regex EmptyIdRegex = new Regex(
            @"\|id=(?=[ \t]*(?:[\r\n|}]|$))", RegexOptions.Compiled
        );

        private Dictionary<string, string> SinglesConversionData;

        private Dictionary<string, string> DoublesConversionData;

        private BracketConversion()
        {
            try
            {
                Reload();
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is JsonException)
            {
            }
        }

        /// <summary>
        /// Rereads the conversion data from disk. If the file is malformed, the
        /// exception propagates and the previously loaded conversions are kept.
        /// </summary>
        public void Reload()
        {
            Dictionary<string, Dictionary<string, string>> conversionData = null;
            try
            {
                conversionData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                    File.ReadAllText(DATA_FILENAME)
                );
            }
            finally
            {
                SinglesConversionData = GetConversionsOrEmpty(conversionData, "singles");
                DoublesConversionData = GetConversionsOrEmpty(conversionData, "doubles");
            }
        }

        private static Dictionary<string, string> GetConversionsOrEmpty(
            Dictionary<string, Dictionary<string, string>> conversionData,
            string key
        )
        {
            if (conversionData != null
                && conversionData.TryGetValue(key, out Dictionary<string, string> conversions)
                && conversions != null)
            {
                return conversions;
            }
            return new Dictionary<string, string>();
        }

        public string ConvertLegacySinglesBrackets(string singlesWikiCode)
        {
            ConvertLegacyBrackets("solo", SinglesConversionData, ref singlesWikiCode);
            return singlesWikiCode;
        }

        public string ConvertLegacyDoublesBrackets(string doublesWikiCode)
        {
            ConvertLegacyBrackets("duo", DoublesConversionData, ref doublesWikiCode);
            return doublesWikiCode;
        }

        private static string GenerateBracketId()
        {
            var chars = new char[10];
            for (int i = 0; i < 10; i++)
            {
                chars[i] = ALPHANUMERICS[Random.Next(ALPHANUMERICS.Length)];
            }
            var bracketId = new string(chars);

            if (!BracketIds.Contains(bracketId))
            {
                BracketIds.Add(bracketId);
                return bracketId;
            }
            return GenerateBracketId();
        }

        private void ConvertLegacyBrackets(string bracketType, Dictionary<string, string> data, ref string wikiCode)
        {
            wikiCode = BracketNameRegex.Replace(wikiCode, match =>
            {
                Group nameGroup = match.Groups["bracketName"];
                string match1BracketName = nameGroup.Value.TrimEnd();

                if (data.TryGetValue(match1BracketName, out string match2BracketType))
                {
                    return "{{LegacyBracket|" + match2BracketType + "|" + match1BracketName + "|type=" + bracketType + "|id=";
                }

                return match.Value;
            });

            wikiCode = EmptyIdRegex.Replace(wikiCode, match => match.Value + GenerateBracketId());
        }
    }
}