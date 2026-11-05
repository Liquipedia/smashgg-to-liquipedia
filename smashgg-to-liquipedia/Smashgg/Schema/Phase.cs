using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    enum Type { Pool, Bracket }

    public class Phase
    {
        // GraphQL fields
        public int id { get; set; }
        public int numSeeds { get; set; }
        public string name { get; set; }
        public int groupCount { get; set; }
        [JsonPropertyName("phaseGroups")]
        public PhaseGroupConnection phasegroups { get; set; }

        // Internal fields
        public SortedDictionary<string, List<PhaseGroup>> waves = new SortedDictionary<string, List<PhaseGroup>>();
    }
}
