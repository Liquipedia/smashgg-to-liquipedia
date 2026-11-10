
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using smashgg_to_liquipedia.Smashgg.Schema;

namespace smashgg_to_liquipedia.Liquipedia
{
    class LpOutput
    {
        private string log;
        bool loadedMatchDetailDictionary;

        Event selectedEvent;
        Dictionary<int, Entrant> entrantList;
        List<Set> setList;
        string stockPrefix;

        Dictionary<int, string> gameCharacterList = new Dictionary<int, string>();
        Dictionary<int, string> gameStageList = new Dictionary<int, string>();

        /// <summary>
        /// LpOutput constructor. Writes group tables; brackets are written by <see cref="Match2BracketWikitext"/>.
        /// </summary>
        public LpOutput(Event selectedEvent, ref Dictionary<int, Entrant> entrantList, ref List<Set> setList, string stockPrefix = "stock")
        {
            log = string.Empty;

            this.selectedEvent = selectedEvent;
            this.entrantList = entrantList;
            this.setList = setList;
            this.stockPrefix = stockPrefix;

            loadedMatchDetailDictionary = false;
        }

        /// <summary>
        /// Creates match details for a set
        /// </summary>
        /// <param name="set">Set information</param>
        /// <param name="game">Game information</param>
        /// <param name="identifier">Liquipedia identifier</param>
        /// <returns></returns>
        public string GenerateMatchDetailsSingles(Set set, Game game, string identifier)
        {
            if (!loadedMatchDetailDictionary)
            {
                LoadDictionaryFromJson(ref gameCharacterList, selectedEvent.videogame.id, "Character");
                LoadDictionaryFromJson(ref gameStageList, selectedEvent.videogame.id, "Stage");

                loadedMatchDetailDictionary = true;
            }

            string insertiontext = string.Empty;

            // Insert character for player 1
            string character;
            if (gameCharacterList.ContainsKey(game.EntrantChar(set.slots[0].entrant.id)))
            {
                character = gameCharacterList[game.EntrantChar(set.slots[0].entrant.id)];
            }
            else if (game.EntrantChar(set.slots[0].entrant.id) == Consts.UNKNOWN || game.EntrantChar(set.slots[0].entrant.id) == 0)
            {
                character = string.Empty;
            }
            else
            {
                character = game.EntrantChar(set.slots[0].entrant.id).ToString();
                log += "No character entry for number: " + character + "\r\n";
            }

            insertiontext += "|" + identifier + LpStrings.P1 + LpStrings.Character + game.orderNum + "=" + character;

            // Insert character for player 2
            if (gameCharacterList.ContainsKey(game.EntrantChar(set.slots[1].entrant.id)))
            {
                character = gameCharacterList[game.EntrantChar(set.slots[1].entrant.id)];
            }
            else if (game.EntrantChar(set.slots[1].entrant.id) == Consts.UNKNOWN || game.EntrantChar(set.slots[1].entrant.id) == 0)
            {
                character = string.Empty;
            }
            else
            {
                character = game.EntrantChar(set.slots[1].entrant.id).ToString();
                log += "No character entry for number: " + character + "\r\n";
            }

            insertiontext += " |" + identifier + LpStrings.P2 + LpStrings.Character + game.orderNum + "=" + character;

            // Insert stock counts - DOESN'T WORK BECAUSE STOCK COUNTS ARE NOT AVAILABLE IN THE API
            //if (game.entrant1p1stocks == Consts.UNKNOWN)
            //{
            //    insertiontext += " |" + identifier + LpStrings.P1 + LpStrings.Stock + game.orderNum + "=";
            //}
            //else
            //{
            //    insertiontext += " |" + identifier + LpStrings.P1 + LpStrings.Stock + game.orderNum + "=" + game.entrant1p1stocks;
            //}

            //if (game.entrant2p1stocks == Consts.UNKNOWN)
            //{
            //    insertiontext += " |" + identifier + LpStrings.P2 + LpStrings.Stock + game.orderNum + "=";
            //}
            //else
            //{
            //    insertiontext += " |" + identifier + LpStrings.P2 + LpStrings.Stock + game.orderNum + "=" + game.entrant2p1stocks;
            //}

            // Fighters uses score, not stock
            if (stockPrefix == "score")
            {
                insertiontext += " |" + identifier + LpStrings.P1 + LpStrings.Score + game.orderNum + "=";
                insertiontext += " |" + identifier + LpStrings.P2 + LpStrings.Score + game.orderNum + "=";
            }
            // Temporary stock counts - only set 0 stocks for the loser
            else
            {
                if (game.winnerId == set.slots[0].entrant.id)
                {
                    insertiontext += " |" + identifier + LpStrings.P1 + LpStrings.Stock + game.orderNum + "=";
                    insertiontext += " |" + identifier + LpStrings.P2 + LpStrings.Stock + game.orderNum + "=0";
                }
                else if (game.winnerId == set.slots[1].entrant.id)
                {
                    insertiontext += " |" + identifier + LpStrings.P1 + LpStrings.Stock + game.orderNum + "=0";
                    insertiontext += " |" + identifier + LpStrings.P2 + LpStrings.Stock + game.orderNum + "=";
                }
                else
                {
                    insertiontext += " |" + identifier + LpStrings.P1 + LpStrings.Stock + game.orderNum + "=";
                    insertiontext += " |" + identifier + LpStrings.P2 + LpStrings.Stock + game.orderNum + "=";
                }
            }

            // Insert game winner
            insertiontext += " |" + identifier + LpStrings.Win + game.orderNum + "=";
            if (game.winnerId == set.slots[0].entrant.id)
            {
                insertiontext += "1";
            }
            else if (game.winnerId == set.slots[1].entrant.id)
            {
                insertiontext += "2";
            }

            // Insert stage - omit for fighters
            string stage = string.Empty;
            if (stockPrefix != "score")
            {
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
                        log += "No stage entry for number: " + stage + "\r\n";
                    }
                }
                else
                {
                    stage = "Unknown";
                }

                insertiontext += " |" + identifier + LpStrings.Stage + game.orderNum + "=" + stage;
            }

            insertiontext += "\r\n";

            return insertiontext;
        }


        /// <summary>
        /// Loads a dictionary with values from a JSON file mapping IDs to names
        /// </summary>
        /// <param name="targetDictionary">The dictionary to fill</param>
        /// <param name="gameId">ID of the game. Melee = 1, Wii U = 3, 64 = 4</param>
        /// <param name="datatype"></param>
        private void LoadDictionaryFromJson(ref Dictionary<int, string> targetDictionary, int gameId, string datatype)
        {
            // Clear the list
            targetDictionary.Clear();

            string filename = Path.Combine(datatype + " Lists", datatype + " List " + gameId + ".json");

            if (!File.Exists(filename))
            {
                log += "Couldn't find JSON file: " + filename + "\r\n";
                return;
            }

            try
            {
                using (FileStream file = File.OpenRead(filename))
                {
                    targetDictionary = JsonSerializer.Deserialize<Dictionary<int, string>>(file) ?? new Dictionary<int, string>();
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                log += "Couldn't parse " + datatype + " JSON file " + filename + ": " + ex.Message + "\r\n";
            }
        }

        /// <summary>
        /// Output all data from the singles phase group
        /// </summary>
        /// <param name="groupTitle">Title of the group</param>
        /// <param name="poolData">Pool records for each entrant</param>
        /// <returns>A group table</returns>
        public string OutputSinglesGroup(string groupTitle, Dictionary<int, PoolRecord> poolData, Tournament.ActivityState groupState, int advanceWinners, int advanceLosers, bool matchDetails, PoolRecord.PoolType poolType)
        {
            string output = string.Empty;
            int totalWinners = advanceWinners;

            // Group header
            output = LpStrings.GroupStart + "Bracket " + groupTitle + LpStrings.GroupStartWidth + "\r\n";

            // Group slots
            for (int i = 0; i < poolData.Count; i++)
            {
                // Skip bye
                if (poolData.ElementAt(i).Key == Consts.PLAYER_BYE)
                {
                    continue;
                }

                Participant currentParticipant = entrantList[poolData.ElementAt(i).Key].participants[0];

                output += LpStrings.SlotStart + currentParticipant.gamerTag +
                          LpStrings.SlotFlag + currentParticipant.user.location.country;
                if ((poolData[poolData.ElementAt(i).Key].MatchesWin + poolData[poolData.ElementAt(i).Key].MatchesLoss > 0) && poolData[poolData.ElementAt(i).Key].matchesActuallyPlayed == 0)
                {
                    output += LpStrings.DQ + "true";
                }
                else
                {
                    output += LpStrings.SlotMWin + poolData[poolData.ElementAt(i).Key].MatchesWin +
                              LpStrings.SlotMLoss + poolData[poolData.ElementAt(i).Key].MatchesLoss;
                }
                          

                if (poolData[poolData.ElementAt(i).Key].rank != Consts.UNKNOWN)
                {
                    if (advanceWinners > 0 && poolData[poolData.ElementAt(i).Key].rank != 0 && poolType != PoolRecord.PoolType.RoundRobin)
                    {
                        // Ignore smash.gg placements for advancements since they don't work atm
                        output += LpStrings.SlotPlace + "1";
                        //output += LpStrings.SlotPlace + (poolData[poolData.ElementAt(i).Key].rank - totalWinners);
                    }
                    else if (advanceLosers > 0 && poolData[poolData.ElementAt(i).Key].rank != 0 && poolType != PoolRecord.PoolType.RoundRobin)
                    {
                        // Ignore smash.gg placements for advancements since they don't work atm
                        output += LpStrings.SlotPlace + (totalWinners+1);
                        //output += LpStrings.SlotPlace + (poolData[poolData.ElementAt(i).Key].rank - 1);
                    }
                    else
                    {
                        output += LpStrings.SlotPlace + poolData[poolData.ElementAt(i).Key].rank;
                    }
                }
                else
                {
                    output += LpStrings.SlotPlace;
                }

                // Set background colors (or placement colors) to differentiate people who progressed vs those who are eliminated
                if (groupState == Tournament.ActivityState.Completed || poolData[poolData.ElementAt(i).Key].MatchesComplete)
                {
                    output += LpStrings.SlotBg;
                }
                else
                {
                    output += LpStrings.SlotPbg;
                }

                // Set colors
                if (advanceWinners > 0)
                {
                    output += "up";
                    advanceWinners--;
                }
                else if (advanceLosers > 0)
                {
                    output += "stay";
                    advanceLosers--;
                }
                else
                {
                    output += "down";
                }

                output += LpStrings.SlotEnd + "\r\n";
            }

            // Group footer
            output += LpStrings.GroupEnd + "\r\n";


            // Match details
            if (matchDetails)
            {
                output += LpStrings.MatchListStart + LpStrings.GroupStartWidth + "\r\n";
                foreach (Set currentSet in setList)
                {
                    if (currentSet.slots[0].entrant == null || currentSet.slots[1].entrant == null) continue;
                    Participant p1 = entrantList[currentSet.slots[0].entrant.id].participants[0];
                    Participant p2 = entrantList[currentSet.slots[1].entrant.id].participants[0];

                    output += LpStrings.MatchStages;

                    // Player information and overall set score
                    // Check for DQs
                    if (currentSet.DisplayScore == "DQ" && currentSet.winnerId == currentSet.slots[1].entrant.id)
                    {
                        output += "\r\n|" + LpStrings.P1 + "=" + p1.gamerTag + " |" + LpStrings.P1 + LpStrings.Flag + "=" + p1.user.location.country + " |" + LpStrings.P1 + LpStrings.Score + "=DQ" +  "\r\n" +
                              "|" + LpStrings.P2 + "=" + p2.gamerTag + " |" + LpStrings.P2 + LpStrings.Flag + "=" + p2.user.location.country + " |" + LpStrings.P2 + LpStrings.Score + "=-" + "\r\n" +
                              "|" + LpStrings.Win + "=";
                    }
                    else if (currentSet.DisplayScore == "DQ" && currentSet.winnerId == currentSet.slots[0].entrant.id)
                    {
                        output += "\r\n|" + LpStrings.P1 + "=" + p1.gamerTag + " |" + LpStrings.P1 + LpStrings.Flag + "=" + p1.user.location.country + " |" + LpStrings.P1 + LpStrings.Score + "=-" + "\r\n" +
                              "|" + LpStrings.P2 + "=" + p2.gamerTag + " |" + LpStrings.P2 + LpStrings.Flag + "=" + p2.user.location.country + " |" + LpStrings.P2 + LpStrings.Score + "=DQ" + "\r\n" +
                              "|" + LpStrings.Win + "=";
                    }
                    else
                    {
                        output += "\r\n|" + LpStrings.P1 + "=" + p1.gamerTag + " |" + LpStrings.P1 + LpStrings.Flag + "=" + p1.user.location.country + " |" + LpStrings.P1 + LpStrings.Score + "=" + currentSet.entrant1wins + "\r\n" +
                              "|" + LpStrings.P2 + "=" + p2.gamerTag + " |" + LpStrings.P2 + LpStrings.Flag + "=" + p2.user.location.country + " |" + LpStrings.P2 + LpStrings.Score + "=" + currentSet.entrant2wins + "\r\n" +
                              "|" + LpStrings.Win + "=";
                    }

                    // Set the winner
                    if (currentSet.winnerId == currentSet.slots[0].entrant.id) { output += "1\r\n"; }
                    else if (currentSet.winnerId == currentSet.slots[1].entrant.id) { output += "2\r\n"; }

                    // Output games if present
                    if (currentSet.games != null)
                    {
                        foreach (Game game in currentSet.games)
                        {
                            output += GenerateMatchDetailsSingles(currentSet, game, string.Empty);
                        }
                    }

                    output += LpStrings.MatchStagesEnd + "\r\n";
                }
                output += LpStrings.MatchListEnd + "\r\n";
            }

            return output;
        }

        /// <summary>
        /// Output all data from the doubles phase group
        /// </summary>
        /// <param name="groupTitle">Title of the group</param>
        /// <param name="poolData">Pool records for each entrant</param>
        /// <returns>A group table</returns>
        public string OutputDoublesGroup(string groupTitle, Dictionary<int, PoolRecord> poolData, Tournament.ActivityState groupState, int advanceWinners, int advanceLosers, bool matchDetails, PoolRecord.PoolType poolType)
        {
            string output = string.Empty;
            int totalWinners = advanceWinners;

            // Group header
            output = LpStrings.GroupStart + "Bracket " + groupTitle + LpStrings.GroupStartWidth + "\r\n";

            // Group slots
            for (int i = 0; i < poolData.Count; i++)
            {
                // Skip bye
                if (poolData.ElementAt(i).Key == Consts.PLAYER_BYE)
                {
                    continue;
                }

                // Output players
                output += LpStrings.DoublesSlotStart +
                          LpStrings.DoublesSlotP1 + entrantList[poolData.ElementAt(i).Key].participants[0].gamerTag +
                          LpStrings.DoublesSlotP1Flag + entrantList[poolData.ElementAt(i).Key].participants[0].user.location.country +
                          LpStrings.DoublesSlotP2 + entrantList[poolData.ElementAt(i).Key].participants[1].gamerTag +
                          LpStrings.DoublesSlotP2Flag + entrantList[poolData.ElementAt(i).Key].participants[1].user.location.country;

                if ((poolData[poolData.ElementAt(i).Key].MatchesWin + poolData[poolData.ElementAt(i).Key].MatchesLoss > 0) && poolData[poolData.ElementAt(i).Key].matchesActuallyPlayed == 0)
                {
                    output += LpStrings.DQ + "true";
                }
                else
                {
                    output += LpStrings.SlotMWin + poolData[poolData.ElementAt(i).Key].MatchesWin +
                              LpStrings.SlotMLoss + poolData[poolData.ElementAt(i).Key].MatchesLoss;
                }

                if (poolData[poolData.ElementAt(i).Key].rank != Consts.UNKNOWN)
                {
                    if (advanceWinners > 0 && poolData[poolData.ElementAt(i).Key].rank != 0 && poolType != PoolRecord.PoolType.RoundRobin)
                    {
                        // Ignore smash.gg placements for advancements since they don't work atm
                        output += LpStrings.SlotPlace + "1";
                        //output += LpStrings.SlotPlace + (poolData[poolData.ElementAt(i).Key].rank - totalWinners);
                    }
                    else if (advanceLosers > 0 && poolData[poolData.ElementAt(i).Key].rank != 0 && poolType != PoolRecord.PoolType.RoundRobin)
                    {
                        // Ignore smash.gg placements for advancements since they don't work atm
                        output += LpStrings.SlotPlace + (totalWinners + 1);
                        //output += LpStrings.SlotPlace + (poolData[poolData.ElementAt(i).Key].rank - 1);
                    }
                    else
                    {
                        output += LpStrings.SlotPlace + poolData[poolData.ElementAt(i).Key].rank;
                    }
                }
                else
                {
                    output += LpStrings.SlotPlace;
                }

                // Set background colors (or placement colors) to differentiate people who progressed vs those who are eliminated
                if (groupState == Tournament.ActivityState.Completed || poolData[poolData.ElementAt(i).Key].MatchesComplete)
                {
                    output += LpStrings.SlotBg;
                }
                else
                {
                    output += LpStrings.SlotPbg;
                }

                // Set colors
                if (advanceWinners > 0)
                {
                    output += "up";
                    advanceWinners--;
                }
                else if (advanceLosers > 0)
                {
                    output += "stay";
                    advanceLosers--;
                }
                else
                {
                    output += "down";
                }

                output += LpStrings.SlotEnd + "\r\n";
            }


            // Group footer
            output += LpStrings.GroupEnd + "\r\n";


            // Match details
            if (matchDetails)
            {
                output += LpStrings.MatchListStart + LpStrings.GroupStartWidth + "\r\n";
                foreach (Set currentSet in setList)
                {
                    Participant p1 = entrantList[currentSet.slots[0].entrant.id].participants[0];
                    Participant p2 = entrantList[currentSet.slots[1].entrant.id].participants[0];

                    output += LpStrings.MatchStages;

                    output += "|" + LpStrings.P1 + "=" + p1.gamerTag + " |" + LpStrings.P1 + LpStrings.Flag + "=" + p1.user.location.country + " |" + LpStrings.P1 + LpStrings.Score + "=" + currentSet.entrant1wins + "\r\n" +
                              "|" + LpStrings.P2 + "=" + p2.gamerTag + " |" + LpStrings.P2 + LpStrings.Flag + "=" + p2.user.location.country + " |" + LpStrings.P2 + LpStrings.Score + "=" + currentSet.entrant2wins + "\r\n" +
                              "|" + LpStrings.Win + "=";

                    if (currentSet.winnerId == currentSet.slots[0].entrant.id) { output += "1\r\n"; }
                    else if (currentSet.winnerId == currentSet.slots[1].entrant.id) { output += "2\r\n"; }

                    foreach (Game game in currentSet.games)
                    {
                        output += GenerateMatchDetailsSingles(currentSet, game, string.Empty);
                    }

                    output += LpStrings.MatchStagesEnd + "\r\n";
                }
                output += LpStrings.MatchListEnd + "\r\n";
            }

            return output;
        }


        public string Log
        {
            get
            {
                string outputlog = log;
                log = string.Empty;
                return outputlog;
            }
        }
    }
}
