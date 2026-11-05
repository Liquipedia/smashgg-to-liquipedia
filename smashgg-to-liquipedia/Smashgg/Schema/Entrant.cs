using System.Collections.Generic;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class Entrant
    {
        public enum EntrantType { Singles, Doubles, Team }

#region GraphQL fields
        public string name { get; set; }
        public int id { get; set; }
        public List<Participant> participants = new List<Participant>();
        public int eventId;
 #endregion

        EntrantType entrantType { get; set; }
    }
}
