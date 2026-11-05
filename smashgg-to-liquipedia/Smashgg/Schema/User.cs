namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class User
    {
        // GraphQL fields
        public Location location
        {
            get
            {
                return locationInfo;
            }
            set
            {
                if (value != null)
                {
                    locationInfo = value;
                }
            }
        }

        public User() { locationInfo = new Location(); }

        // Internal fields
        public Location locationInfo;
    }
}
