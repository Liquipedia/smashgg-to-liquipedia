using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        private static readonly Regex RoundMatchRegex = new Regex(@"R(?<round>[0-9]{2})-M(?<match>[0-9]{3})", RegexOptions.Compiled);

        public class Options
        {
            /// <summary>Write scores and winners of sets that are not completed yet</summary>
            public bool IncludeUnfinished = false;

            /// <summary>Write per-game details (characters, stages); needs sets queried with details</summary>
            public bool MatchDetails = false;

            /// <summary>Write "Bye" opponents for bye sets instead of leaving them empty</summary>
            public bool FillByes = false;

            /// <summary>Use Fighters style output</summary>
            public bool UseFightersStyle = false;
        }

        private const string INDENT = "    ";

        private readonly Event selectedEvent;
        private readonly Dictionary<int, Entrant> entrants;
        private readonly Options options;

        /// <param name="selectedEvent">The start.gg event the brackets belong to</param>
        /// <param name="entrants">Event entrants keyed by entrant id</param>
        public Match2BracketWikitext(Event selectedEvent, Dictionary<int, Entrant> entrants, Options options)
        {
            this.selectedEvent = selectedEvent;
            this.entrants = entrants;
            this.options = options;
        }

        /// <summary>
        /// The event the brackets belong to
        /// </summary>
        public Event Event
        {
            get { return selectedEvent; }
        }

        /// <summary>
        /// start.gg id of the event's game, which keys the character and stage lists; 0 if unknown
        /// </summary>
        public int VideogameId
        {
            get { return selectedEvent?.videogame?.id ?? 0; }
        }

        /// <summary>
        /// Opponent template written for each slot, from the event's entrant size
        /// </summary>
        public OpponentType Opponents
        {
            get
            {
                if (selectedEvent != null
                    && (selectedEvent.Type == Event.EventType.Doubles || selectedEvent.teamRosterSize?.maxPlayers == 2))
                {
                    return OpponentType.Duo;
                }
                return OpponentType.Solo;
            }
        }

        /// <summary>
        /// Problems found while converting, such as ids missing from the character and stage lists
        /// </summary>
        public string Log
        {
            get { return log.ToString(); }
        }

        #region Match detail dictionaries
        private readonly StringBuilder log = new StringBuilder();
        private bool loadedMatchDetailDictionary = false;
        private Dictionary<int, string> gameCharacterList = new Dictionary<int, string>();
        private Dictionary<int, string> gameStageList = new Dictionary<int, string>();

        /// <summary>
        /// Loads the character and stage lists of the event's game on first use
        /// </summary>
        private void LoadMatchDetailDictionary()
        {
            if (loadedMatchDetailDictionary)
            {
                return;
            }
            gameCharacterList = LoadDictionaryFromJson(VideogameId, "Character");
            gameStageList = LoadDictionaryFromJson(VideogameId, "Stage");
            loadedMatchDetailDictionary = true;
        }

        /// <summary>
        /// Reads a JSON file mapping start.gg ids to names, e.g. "Character Lists/Character List 1.json"
        /// </summary>
        /// <param name="gameId">start.gg id of the game</param>
        /// <param name="datatype">"Character" or "Stage"</param>
        /// <returns>The mapping, or an empty one if the file is missing or unreadable</returns>
        private Dictionary<int, string> LoadDictionaryFromJson(int gameId, string datatype)
        {
            string filename = Path.Combine(datatype + " Lists", datatype + " List " + gameId + ".json");

            if (!File.Exists(filename))
            {
                log.Append("Couldn't find JSON file: ").Append(filename).Append("\r\n");
                return new Dictionary<int, string>();
            }

            try
            {
                using (FileStream file = File.OpenRead(filename))
                {
                    return JsonSerializer.Deserialize<Dictionary<int, string>>(file) ?? new Dictionary<int, string>();
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                log.Append("Couldn't parse ").Append(datatype).Append(" JSON file ").Append(filename)
                    .Append(": ").Append(ex.Message).Append("\r\n");
                return new Dictionary<int, string>();
            }
        }

        /// <summary>
        /// Name of a start.gg character id: the list entry, empty if none was reported,
        /// or the id itself if it is missing from the list
        /// </summary>
        private string CharacterName(int characterId)
        {
            LoadMatchDetailDictionary();
            if (gameCharacterList.TryGetValue(characterId, out string name))
            {
                return name;
            }
            if (characterId == Consts.UNKNOWN || characterId == 0)
            {
                return string.Empty;
            }
            log.Append("No character entry for number: ").Append(characterId).Append("\r\n");
            return characterId.ToString();
        }

        /// <summary>
        /// Name of a game's stage: the list entry, empty if none was reported, "Unknown" if the game
        /// has no stage data, or the id itself if it is missing from the list
        /// </summary>
        private string StageName(Stage stage)
        {
            LoadMatchDetailDictionary();
            if (stage == null)
            {
                return "Unknown";
            }
            if (gameStageList.TryGetValue(stage.id, out string name))
            {
                return name;
            }
            if (stage.id == Consts.UNKNOWN || stage.id == 0)
            {
                return string.Empty;
            }
            log.Append("No stage entry for number: ").Append(stage.id).Append("\r\n");
            return stage.id.ToString();
        }
        #endregion

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

                text.Append("\r\n|");
                Match m = RoundMatchRegex.Match(matchId);
                if (m.Success)
                {
                    int round = int.Parse(m.Groups["round"].Value);
                    int match = int.Parse(m.Groups["match"].Value);
                    text.Append('R').Append(round).Append('M').Append(match);
                }
                else
                {
                    text.Append(matchId);
                }
                text.Append("=")
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
            // start.gg switches the slots of a bracket reset relative to the grand final, while the
            // template's reset keeps the grand final's opponent order
            if (IsBracketReset(bracket, templateMatch))
            {
                return true;
            }

            // TODO: a match with a single linked feeder (e.g. a losers round fed by one losers match,
            // with the other opponent dropping from winners) puts that feeder on the side named by
            // toupper/tolower. Compare that side with the slot whose prereqId is the feeder's set, and
            // swap when they differ. Two-feeder matches are already aligned by the mapper.
            return false;
        }

        /// <summary>
        /// Whether a template match is the bracket reset of another match in the same template
        /// </summary>
        private static bool IsBracketReset(Match2Bracket bracket, LpdbMatch2 templateMatch)
        {
            return bracket.TemplateMatches.Any(match => match.match2bracketdata != null
                && match.match2bracketdata.bracketreset == templateMatch.match2id);
        }

        /// <summary>
        /// Writes the opponent template for one slot
        /// </summary>
        private string ConvertOpponent(Set set, int slotIndex, bool showResult)
        {
            Entrant entrant = SlotEntrant(set, slotIndex);
            if (entrant == null)
            {
                return options.FillByes ? "{{LiteralOpponent|Bye}}" : string.Empty;
            }

            string score = showResult ? ConvertScore(set, slotIndex) : string.Empty;

            switch (Opponents)
            {
                case OpponentType.Duo:
                    var opponent = new StringBuilder("{{DuoOpponent");
                    var players = entrant.participants;
                    if (players.Count == 0)
                    {
                        return opponent.Append("}}").ToString();
                    }
                    for (int i = 0; i < players.Count; i++)
                    {
                        var opponentPlayer = players[i];
                        opponent.Append("|p").Append(i + 1).Append(opponentPlayer.gamerTag);
                        if (!string.IsNullOrEmpty(opponentPlayer.user.location.country))
                        {
                            opponent.Append("|p").Append(i + 1).Append("flag=").Append(opponentPlayer.user.location.country);
                        }
                    }
                    if (score != string.Empty)
                    {
                        opponent.Append("|score=").Append(score);
                    }
                    return opponent.Append("}}").ToString();

                default:
                    Participant player = entrant.participants.FirstOrDefault();
                    if (player == null)
                    {
                        return string.Empty;
                    }
                    opponent = new StringBuilder("{{SoloOpponent|").Append(player.gamerTag);
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
            var gameWikicode = new StringBuilder("{{Map");
            LoadMatchDetailDictionary();

            // Insert character for player 1
            string o1Character;
            if (gameCharacterList.ContainsKey(game.EntrantChar(set.slots[opponent1Slot].entrant.id)))
            {
                o1Character = gameCharacterList[game.EntrantChar(set.slots[opponent1Slot].entrant.id)];
            }
            else if (game.EntrantChar(set.slots[opponent1Slot].entrant.id) == Consts.UNKNOWN || game.EntrantChar(set.slots[opponent1Slot].entrant.id) == 0)
            {
                o1Character = string.Empty;
            }
            else
            {
                o1Character = game.EntrantChar(set.slots[opponent1Slot].entrant.id).ToString();
                // log += "No character entry for number: " + character + "\r\n";
            }


            // Insert character for player 2
            string o2Character;
            if (gameCharacterList.ContainsKey(game.EntrantChar(set.slots[opponent2Slot].entrant.id)))
            {
                o2Character = gameCharacterList[game.EntrantChar(set.slots[opponent2Slot].entrant.id)];
            }
            else if (game.EntrantChar(set.slots[opponent2Slot].entrant.id) == Consts.UNKNOWN || game.EntrantChar(set.slots[opponent2Slot].entrant.id) == 0)
            {
                o2Character = string.Empty;
            }
            else
            {
                o2Character = game.EntrantChar(set.slots[opponent2Slot].entrant.id).ToString();
                // log += "No character entry for number: " + character + "\r\n";
            }

            if (options.UseFightersStyle)
            {
                gameWikicode.Append("|o1p1={{Chars|").Append(o1Character).Append("}}");
                gameWikicode.Append("|o2p1={{Chars|").Append(o2Character).Append("}}");
                gameWikicode.Append("|score1=");
                gameWikicode.Append("|score2=");
            }
            else
            {
                // Temporary stock counts - only set 0 stocks for the loser
                if (game.winnerId == set.slots[opponent1Slot].entrant.id)
                {
                    gameWikicode.Append("|o1c1={{Chars|").Append(o1Character).Append(",").Append("}}");
                    gameWikicode.Append("|o2c1={{Chars|").Append(o2Character).Append(",0").Append("}}");
                }
                else if (game.winnerId == set.slots[opponent2Slot].entrant.id)
                {
                    gameWikicode.Append("|o1c1={{Chars|").Append(o1Character).Append(",0").Append("}}");
                    gameWikicode.Append("|o2c1={{Chars|").Append(o2Character).Append(",").Append("}}");
                }
                else
                {
                    gameWikicode.Append("|o1c1={{Chars|").Append(o1Character).Append(",").Append("}}");
                    gameWikicode.Append("|o2c1={{Chars|").Append(o2Character).Append(",").Append("}}");
                }
            }
            gameWikicode.Append("|winner=");

            if (game.winnerId == set.slots[opponent1Slot].entrant.id)
            {
                gameWikicode.Append("1");
            }
            else if (game.winnerId == set.slots[opponent2Slot].entrant.id)
            {
                gameWikicode.Append("2");
            }

            if (!options.UseFightersStyle)
            {
                string stage;
                if (game.stage != null)
                {
                    if (gameStageList.ContainsKey(game.stage.id))
                    {
                        stage = gameStageList[game.stage.id];
                    }
                    else if (game.stage.id == Consts.UNKNOWN || game.stage.id == 0)
                    {
                        stage = string.Empty;
                    }
                    else
                    {
                        stage = game.stage.id.ToString();
                        // log += "No stage entry for number: " + stage + "\r\n";
                    }
                }
                else
                {
                    stage = "Unknown";
                }
                gameWikicode.Append("|map=").Append(stage);
            }

            return gameWikicode.Append("}}").ToString();
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
