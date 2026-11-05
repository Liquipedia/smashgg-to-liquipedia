using System.Collections.Generic;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class PhaseGroupConnection
    {
        public PageInfo pageInfo { get; set; }
        public List<PhaseGroup> nodes { get; set; }
    }
}
