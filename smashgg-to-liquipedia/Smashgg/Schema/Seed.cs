using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class Seed
    {
        public int id { get; set; }
        public Entrant entrant { get; set; }
        public List<Player> players { get; set; }
        public int entrantId { get; set; }
        public int seedNum { get; set; }
        private int placement { get; set; }
        public bool isBye { get; set; }
        public List<Standing> standings { get; set; }

        [JsonPropertyName("placement")]
        public int? Placement
        {
            get
            {
                return placement;
            }
            set
            {
                if (value == null)
                {
                    placement = 0;
                }
                else
                {
                    placement = (int)value;
                }
            }
        }
    }
}
