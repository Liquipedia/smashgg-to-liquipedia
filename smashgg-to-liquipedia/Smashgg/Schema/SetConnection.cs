using System.Collections.Generic;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class SetConnection
    {
        public PageInfo pageInfo { get; set; }
        public List<Set> nodes { get; set; }
    }
}
