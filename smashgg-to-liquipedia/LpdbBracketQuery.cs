using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", token);
        }

        public async Task<Dictionary<string, string>[]> FetchBracket(string bracketType, CancellationToken cancellationToken = default)
        {
            if (!bracketType.StartsWith("Bracket/"))
            {
                bracketType = "Bracket/" + bracketType;
            }
            var queryParameters = new Dictionary<string, string>
            {
                { "conditions", $"[[namespace::10]] and [[pagename::{bracketType}]]" },
                { "query", "match2id" },
                { "order", "match2id asc" },
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
                {
                    var rawResponse = await JsonDocument.ParseAsync(stream, default, cancellationToken);
                    if (rawResponse.RootElement.TryGetProperty("error", out JsonElement rawLpdbErrors))
                    {
                        throw new LpdbException(rawLpdbErrors.Deserialize<string[]>());
                    }
                    var rawResults = rawResponse.RootElement.GetProperty("result");
                    return rawResults.Deserialize<Dictionary<string, string>[]>();
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