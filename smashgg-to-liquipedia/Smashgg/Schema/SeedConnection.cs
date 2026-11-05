using System.Collections.Generic;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class SeedConnection
    {
        public PageInfo pageInfo { get; set; }
        public List<Seed> nodes { get; set; }
    }
}
