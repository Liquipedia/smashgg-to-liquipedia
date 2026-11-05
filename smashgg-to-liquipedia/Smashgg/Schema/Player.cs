namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class Player
    {
        public int id;

        // Blank constructor
        public Player() { }

        // Overloaded constructor
        public Player(int inputID, string inputName, string inputCountry)
        {
            id = inputID;
        }
    }
}
