using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class Participant
    {
        // GraphQL fields
        public int id { get; set; }
        public string gamerTag { get; set; }
        public Player player { get; set; }
        public bool verified { get; set; }
        public User user
        {
            get
            {
                return userInfo;
            }
            set
            {
                if (value != null)
                {
                    userInfo = value;
                }
            }
        }

        public Participant()
        {
            userInfo = new User();
        }

        // Internal fields
        public User userInfo;
    }
}
