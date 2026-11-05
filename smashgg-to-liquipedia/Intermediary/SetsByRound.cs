using smashgg_to_liquipedia.Smashgg.Schema;
using System.Collections.Generic;

namespace smashgg_to_liquipedia
{
    class SetsByRound
    {
        public SetsByRound(int inputRound, List<Set> inputSets)
        {
            round = inputRound;
            sets = inputSets;
        }

        public SetsByRound() { }

        public int round;
        public List<Set> sets;
    }
}
