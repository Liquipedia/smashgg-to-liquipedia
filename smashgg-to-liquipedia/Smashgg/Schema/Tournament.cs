using System.Collections.Generic;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class Tournament
    {
        public enum ActivityState { Created, Active, Completed, Ready, Invalid, Called, Queued }

        public int id;
        public string slug;

        public List<Event> events;
    }
}
