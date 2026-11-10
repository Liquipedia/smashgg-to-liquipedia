using System.Collections.Generic;
using System.Linq;
using System.Text;
using smashgg_to_liquipedia.Smashgg.Schema;

namespace smashgg_to_liquipedia.Liquipedia
{
    /// <summary>
    /// Turns match2 brackets filled with start.gg sets into <c>{{Bracket|...}}</c> wikitext
    /// </summary>
    /// <example>
    /// <code>
    /// ==Final Bracket==
    /// {{Bracket|Bracket/2U2L1D|id=AbCdEfGhIj
    /// |R01-M001={{Match
    ///     |opponent1={{SoloOpponent|Tag|flag=us|score=3}}
    ///     |opponent2={{SoloOpponent|Tag|flag=jp|score=1}}
    /// }}
    /// ...
    /// }}
    /// </code>
    /// </example>
    class Match2BracketWikitext
    {
        /// <summary>
        /// Kind of opponent template written for each slot
        /// </summary>
        public enum OpponentType { Solo, Duo }

        public class Options
        {
            public OpponentType OpponentType = OpponentType.Solo;

            /// <summary>Write scores and winners of sets that are not completed yet</summary>
            public bool IncludeUnfinished = false;

            /// <summary>Write per-game details (characters, stages); needs sets queried with details</summary>
            public bool MatchDetails = false;

            /// <summary>Write "Bye" opponents for bye sets instead of leaving them empty</summary>
            public bool FillByes = false;
        }

        private const string INDENT = "    ";

        private readonly Dictionary<int, Entrant> entrants;
        private readonly Options options;

        /// <param name="entrants">Event entrants keyed by entrant id</param>
        public Match2BracketWikitext(Dictionary<int, Entrant> entrants, Options options)
        {
            this.entrants = entrants;
            this.options = options;
        }

        /// <summary>
        /// Converts brackets in order, each under its header
        /// </summary>
        public string Convert(IEnumerable<Match2Bracket> brackets)
        {
            return string.Join("\r\n\r\n", brackets.Select(Convert));
        }

        /// <summary>
        /// Converts one bracket, including its header
        /// </summary>
        public string Convert(Match2Bracket bracket)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrEmpty(bracket.Header))
            {
                text.Append(bracket.Header).Append("\r\n");
            }

            text.Append("{{Bracket|").Append(bracket.BracketType)
                .Append("|id=").Append(BracketConversion.GenerateBracketId());

            // Every template match is written, so empty matches stay editable on the wiki
            foreach (LpdbMatch2 templateMatch in bracket.TemplateMatches)
            {
                string matchId = Match2BracketMapper.ToShortId(templateMatch.match2id);
                bracket.Matches.TryGetValue(matchId, out Set set);

                text.Append("\r\n|").Append(matchId).Append("=")
                    .Append(ConvertMatch(bracket, templateMatch, set));
            }

            text.Append("\r\n}}");
            return text.ToString();
        }

        /// <summary>
        /// Writes one <c>{{Match}}</c> call
        /// </summary>
        /// <param name="set">The set placed in this match, or null if none</param>
        private string ConvertMatch(Match2Bracket bracket, LpdbMatch2 templateMatch, Set set)
        {
            if (set == null)
            {
                // TODO: decide whether unfilled matches get an empty {{Match}} or are omitted
                return "{{Match}}";
            }

            // Template opponent 1/2 in terms of start.gg slot indexes
            bool swap = ShouldSwapOpponents(bracket, templateMatch, set);
            int opponent1Slot = swap ? 1 : 0;
            int opponent2Slot = swap ? 0 : 1;

            bool showResult = options.IncludeUnfinished || set.State == Tournament.ActivityState.Completed;

            var text = new StringBuilder("{{Match");
            AppendParameter(text, "opponent1", ConvertOpponent(set, opponent1Slot, showResult));
            AppendParameter(text, "opponent2", ConvertOpponent(set, opponent2Slot, showResult));

            if (showResult)
            {
                string winner = ConvertWinner(set, opponent1Slot, opponent2Slot);
                if (winner != string.Empty)
                {
                    AppendParameter(text, "winner", winner);
                }
            }

            if (options.MatchDetails && showResult && set.games != null)
            {
                foreach (Game game in set.games.OrderBy(game => game.orderNum))
                {
                    AppendParameter(text, "map" + game.orderNum, ConvertGame(set, game, opponent1Slot, opponent2Slot));
                }
            }

            text.Append("\r\n}}");
            return text.ToString();
        }

        /// <summary>
        /// Whether start.gg's slot 0 belongs in the template's opponent 2 position
        /// </summary>
        private bool ShouldSwapOpponents(Match2Bracket bracket, LpdbMatch2 templateMatch, Set set)
        {
            // TODO: a match with a single linked feeder (e.g. a losers round fed by one losers match,
            // with the other opponent dropping from winners) puts that feeder on the side named by
            // toupper/tolower. Compare that side with the slot whose prereqId is the feeder's set, and
            // swap when they differ. Two-feeder matches are already aligned by the mapper.
            //
            // TODO: start.gg also switches the slots of a bracket reset relative to the grand final
            // (see the legacy LpOutput reset handling); check whether the template's reset match
            // expects the grand final's order.
            return false;
        }

        /// <summary>
        /// Writes the opponent template for one slot
        /// </summary>
        private string ConvertOpponent(Set set, int slotIndex, bool showResult)
        {
            Entrant entrant = SlotEntrant(set, slotIndex);
            if (entrant == null)
            {
                // TODO: bye / TBD opponent syntax; options.FillByes
                return string.Empty;
            }

            string score = showResult ? ConvertScore(set, slotIndex) : string.Empty;

            switch (options.OpponentType)
            {
                case OpponentType.Duo:
                    // TODO: verify the duo opponent template name and parameters on the wiki
                    return string.Empty;

                default:
                    // TODO: verify SoloOpponent parameters on the wiki
                    Participant player = entrant.participants.FirstOrDefault();
                    if (player == null)
                    {
                        return string.Empty;
                    }
                    var opponent = new StringBuilder("{{SoloOpponent|").Append(player.gamerTag);
                    if (!string.IsNullOrEmpty(player.user.location.country))
                    {
                        opponent.Append("|flag=").Append(player.user.location.country);
                    }
                    if (score != string.Empty)
                    {
                        opponent.Append("|score=").Append(score);
                    }
                    return opponent.Append("}}").ToString();
            }
        }

        /// <summary>
        /// Score of one slot: games won, "W"/"DQ" for a DQ, or empty if unknown
        /// </summary>
        private string ConvertScore(Set set, int slotIndex)
        {
            // TODO: DQs ("DQ" display score) and sets reported without a game count
            // (winnerId set, both counts 0), which the legacy output marked with a checkmark.
            // Note Set.entrant1wins/entrant2wins come from displayScore order, which is
            // assumed to be slot order.
            int wins = slotIndex == 0 ? set.entrant1wins : set.entrant2wins;
            return wins.ToString();
        }

        /// <summary>
        /// "1" or "2" for the template opponent that won, or empty if there is no winner yet
        /// </summary>
        private string ConvertWinner(Set set, int opponent1Slot, int opponent2Slot)
        {
            if (set.winnerId == null)
            {
                return string.Empty;
            }
            if (SlotEntrantId(set, opponent1Slot) == set.winnerId)
            {
                return "1";
            }
            if (SlotEntrantId(set, opponent2Slot) == set.winnerId)
            {
                return "2";
            }
            return string.Empty;
        }

        /// <summary>
        /// Writes one game's details
        /// </summary>
        private string ConvertGame(Set set, Game game, int opponent1Slot, int opponent2Slot)
        {
            // TODO: {{Map}} parameters per wiki (characters, stage, winner, stocks/score);
            // the character and stage id lists are loaded in LpOutput.LoadDictionaryFromJson
            return "{{Map}}";
        }

        private static void AppendParameter(StringBuilder text, string name, string value)
        {
            text.Append("\r\n").Append(INDENT).Append("|").Append(name).Append("=").Append(value);
        }

        private static int? SlotEntrantId(Set set, int slotIndex)
        {
            if (set.slots == null || slotIndex >= set.slots.Count)
            {
                return null;
            }
            return set.slots[slotIndex]?.entrant?.id;
        }

        private Entrant SlotEntrant(Set set, int slotIndex)
        {
            int? entrantId = SlotEntrantId(set, slotIndex);
            if (entrantId == null || !entrants.TryGetValue(entrantId.Value, out Entrant entrant))
            {
                return null;
            }
            return entrant;
        }
    }
}
