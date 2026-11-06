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

        private JsonDocument ConversionData;

        private JsonElement SinglesConversionData => ConversionData.RootElement.GetProperty("singles");

        private JsonElement DoublesConversionData => ConversionData.RootElement.GetProperty("doubles");

        private BracketConversion()
        {
            try
            {
                using (FileStream file = File.OpenRead(DATA_FILENAME))
                {
                    ConversionData = JsonDocument.Parse(file);
                }
            }
            catch (FileNotFoundException)
            {
                ConversionData = JsonSerializer.SerializeToDocument(
                    new
                    {
                        singles = new {},
                        doubles = new {}
                    }
                );
            }
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

        private void ConvertLegacyBrackets(string bracketType, JsonElement data, ref string wikiCode)
        {
            wikiCode = BracketNameRegex.Replace(wikiCode, match =>
            {
                Group nameGroup = match.Groups["bracketName"];
                string match1BracketName = nameGroup.Value.TrimEnd();

                if (data.TryGetProperty(match1BracketName, out JsonElement match2BracketType))
                {
                    return "{{LegacyBracket|" + match2BracketType.GetString() + "|" + match1BracketName + "|type=" + bracketType + "|id=" + GenerateBracketId();
                }

                return match.Value;
            });
        }
    }
}