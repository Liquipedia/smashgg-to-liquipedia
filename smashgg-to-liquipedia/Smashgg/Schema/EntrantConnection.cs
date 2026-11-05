using System.Collections.Generic;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class EntrantConnection
    {
        public PageInfo pageInfo { get; set; }
        public List<Entrant> nodes { get; set; }
    }
}
