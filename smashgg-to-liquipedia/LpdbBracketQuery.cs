using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using smashgg_to_liquipedia.Liquipedia;

namespace smashgg_to_liquipedia
{
    class LpdbBracketQuery
    {
        private string token;
        private readonly IFormMain form;
        private readonly HttpClient httpClient;
        static string Version => Application.ProductVersion;

        public LpdbBracketQuery(string token, IFormMain form)
        {
            this.form = form;
            this.token = token;

            httpClient = new HttpClient(
                new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip
                }
            )
            {
                BaseAddress = new Uri("https://api.liquipedia.net"),
            };
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("smashgg-to-liquipedia", $"{Version}"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/Liquipedia/smashgg-to-liquipedia)"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", token);
        }

        public void SetToken(string token)
        {
            this.token = token;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", token);
        }

        /// <summary>
        /// Accepts a bracket type with or without its namespace prefix ("8-2Q" or "Bracket/8-2Q")
        /// </summary>
        /// <returns>The template page name, or an empty string if none was given</returns>
        public static string NormalizeBracketType(string bracketType)
        {
            string trimmed = (bracketType ?? string.Empty).Trim();
            if (trimmed == string.Empty || trimmed.StartsWith("Bracket/"))
            {
                return trimmed;
            }
            return "Bracket/" + trimmed;
        }

        /// <summary>
        /// Gets the matches of a bracket template, from the local cache when possible and from LPDB otherwise
        /// </summary>
        /// <param name="bracketType">Bracket template name, with or without the "Bracket/" prefix (e.g. "8-2Q")</param>
        /// <returns>Template matches ordered by match2id; empty if the template does not exist</returns>
        public async Task<List<LpdbMatch2>> GetBracketAsync(string bracketType, CancellationToken cancellationToken = default)
        {
            bracketType = NormalizeBracketType(bracketType);

            if (memoryCache.TryGetValue(bracketType, out List<LpdbMatch2> cached))
            {
                return cached;
            }

            cached = ReadCacheFile(bracketType);
            if (cached != null)
            {
                form.Log += "Read " + bracketType + " from cache\r\n";
                memoryCache[bracketType] = cached;
                return cached;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new LpdbException("Set your LiquipediaDB API key first.");
            }

            List<LpdbMatch2> fetched = await FetchBracket(bracketType, cancellationToken);

            // A missing template is not cached, so it is found once it is created
            if (fetched != null && fetched.Count > 0)
            {
                memoryCache[bracketType] = fetched;
                WriteCacheFile(bracketType, fetched);
            }
            return fetched ?? new List<LpdbMatch2>();
        }

        #region Local cache
        private const string CACHE_DIRECTORY = "BracketTemplates";

        // Commons bracket templates rarely change once created
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);

        private readonly Dictionary<string, List<LpdbMatch2>> memoryCache = new Dictionary<string, List<LpdbMatch2>>();

        private static string CacheFilePath(string bracketType)
        {
            string name = bracketType.Substring("Bracket/".Length);
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }
            return Path.Combine(CACHE_DIRECTORY, name + ".json");
        }

        /// <returns>The cached template, or null if there is no usable cache file</returns>
        private List<LpdbMatch2> ReadCacheFile(string bracketType)
        {
            string path = CacheFilePath(bracketType);
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }
                if (DateTime.Now - File.GetLastWriteTime(path) > CacheLifetime)
                {
                    File.Delete(path);
                    return null;
                }

                List<LpdbMatch2> template = FileIO.ReadFromJsonFile<List<LpdbMatch2>>(path);
                return template != null && template.Count > 0 ? template : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                // An unreadable cache file is ignored and replaced by a fresh copy
                form.Log += "Ignoring cached " + bracketType + ": " + ex.Message + "\r\n";
                return null;
            }
        }

        private void WriteCacheFile(string bracketType, List<LpdbMatch2> template)
        {
            try
            {
                Directory.CreateDirectory(CACHE_DIRECTORY);
                FileIO.WriteToJsonFile(CacheFilePath(bracketType), template);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                form.Log += "Couldn't cache " + bracketType + ": " + ex.Message + "\r\n";
            }
        }
        #endregion

        /// <summary>
        /// Fetches the matches of a bracket template on commons from LPDB, bypassing the cache
        /// </summary>
        /// <param name="bracketType">Bracket template name, with or without the "Bracket/" prefix (e.g. "8-2Q")</param>
        /// <returns>Template matches ordered by match2id (e.g. "Bracket/8-2Q_R01-M001"), with their bracket data</returns>
        public async Task<List<LpdbMatch2>> FetchBracket(string bracketType, CancellationToken cancellationToken = default)
        {
            bracketType = NormalizeBracketType(bracketType);
            var queryParameters = new Dictionary<string, string>
            {
                { "conditions", $"[[namespace::10]] and [[pagename::{bracketType}]]" },
                { "query", "match2id, match2bracketdata" },
                { "order", "match2id asc" },
                // LPDB's default page size is far smaller than a large bracket
                { "limit", "1000" },
            };
            string query = string.Join("&", queryParameters.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"
            ));
            
            using (HttpResponseMessage response = await httpClient.GetAsync(
                $"api/v3/match?" + query,
                cancellationToken
            ))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var rawResponse = await JsonDocument.ParseAsync(stream, default, cancellationToken))
                {
                    if (rawResponse.RootElement.TryGetProperty("error", out JsonElement rawLpdbErrors))
                    {
                        throw new LpdbException(rawLpdbErrors.Deserialize<string[]>());
                    }

                    return rawResponse.RootElement.GetProperty("result")
                        .Deserialize<List<LpdbMatch2>>(JsonExtensions.Options);
                }
            }
        }
    }

    class LpdbException: Exception
    {
        public LpdbException() : base() { }

        public LpdbException(string message) : base(message) { }

        public LpdbException(string[] messages): this(string.Join("\r\n", messages)) { }
    }
}