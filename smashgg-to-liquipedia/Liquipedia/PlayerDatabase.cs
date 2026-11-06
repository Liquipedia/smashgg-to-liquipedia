using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace smashgg_to_liquipedia
{
    public class PlayerDatabase
    {
        private static string SMASH_DB_FILENAME = @"SmashAkaDatabase.json";
        private static string FIGHTERS_DB_FILENAME = @"FightersAkaDatabase.json";

        private static string TEMPLATE_START = @"{{AltSlot";
        private static string TEMPLATE_END = @"}}";
        private static string TEMPLATE_PLAYER = @"|player=";
        private static string TEMPLATE_FLAG = @"|flag=";
        private static string TEMPLATE_ALTS = @"|alts=";
        private static string TEMPLATE_SMASHGG = @"|smashgg=";

        private readonly IFormMain form;

        public enum DbSource { Smash, Fighters };

        /// <summary>
        /// The key is the smash.gg id
        /// </summary>
        public Dictionary<int, PlayerInfo> players;

        private int revID;

        /// <summary>
        /// Creates an empty database. Call <see cref="ReadDatabaseFromFileAsync"/> to load it.
        /// </summary>
        public PlayerDatabase(IFormMain form)
        {
            this.form = form;
            players = new Dictionary<int, PlayerInfo>();
        }

        public int RevID
        {
            get { return revID; }
        }

        /// <summary>
        /// Loads the AKA database from file, replacing the current contents.
        /// The current contents stay in use until the read completes.
        /// </summary>
        /// <param name="source">Which database to read</param>
        /// <param name="cancellationToken">Cancels the read</param>
        /// <returns>True if the database was read successfully</returns>
        public async Task<bool> ReadDatabaseFromFileAsync(DbSource source, CancellationToken cancellationToken = default)
        {
            // Set the filename
            string filename = string.Empty;
            if (source == DbSource.Fighters)
            {
                filename = FIGHTERS_DB_FILENAME;
            }
            else if (source == DbSource.Smash)
            {
                filename = SMASH_DB_FILENAME;
            }
            else
            {
                players = new Dictionary<int, PlayerInfo>();
                return false;
            }

            // Read AKA database json if available
            try
            {
                using (FileStream file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                using (JsonDocument doc = await JsonDocument.ParseAsync(file, default, cancellationToken))
                {
                    JsonElement parse = doc.RootElement.GetProperty("parse");

                    int newRevID = parse.GetProperty("revid").GetInt32();
                    string json = parse.GetProperty("wikitext").GetProperty("*").GetString();

                    // Build into a new dictionary and swap it in, so readers never see a partial database
                    Dictionary<int, PlayerInfo> newPlayers = new Dictionary<int, PlayerInfo>();
                    ParseWikitext(json, newPlayers);

                    players = newPlayers;
                    revID = newRevID;
                    return true;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                players = new Dictionary<int, PlayerInfo>();
                return false;
            }
        }

        private void ParseWikitext(string json, Dictionary<int, PlayerInfo> players)
        {
            string segment = "";
            int i = 0;
            try
            {
                while (json.IndexOf(TEMPLATE_START, i) != -1 && json.IndexOf(TEMPLATE_END, i) != -1)
                {
                    int entrystart = json.IndexOf(TEMPLATE_START, i);
                    int entryend = json.IndexOf(TEMPLATE_END, i) + TEMPLATE_END.Length;

                    segment = json.Substring(entrystart, entryend - entrystart);

                    int playerPos = segment.IndexOf(TEMPLATE_PLAYER);
                    int flagPos = segment.IndexOf(TEMPLATE_FLAG);
                    int altPos = segment.IndexOf(TEMPLATE_ALTS);
                    int smashggPos = segment.IndexOf(TEMPLATE_SMASHGG);

                    string player = string.Empty;
                    string flag = string.Empty;
                    string regex = string.Empty;
                    int smashggID = 0;

                    // Get player
                    if (playerPos != -1 && flagPos != -1)
                    {
                        player = segment.Substring(playerPos + TEMPLATE_PLAYER.Length, flagPos - playerPos - TEMPLATE_PLAYER.Length).Trim();
                    }

                    // Get flag
                    if (flagPos != -1 && altPos != -1)
                    {
                        flag = segment.Substring(flagPos + TEMPLATE_FLAG.Length, altPos - flagPos - TEMPLATE_FLAG.Length).Trim();
                    }

                    // Get alts
                    if (altPos != -1)
                    {
                        regex = segment.Substring(altPos + TEMPLATE_ALTS.Length, smashggPos - altPos - TEMPLATE_ALTS.Length).Trim();
                    }

                    // Get smashgg ID. If no ID exists, skip adding this entry
                    if (smashggPos != -1)
                    {
                        if (!int.TryParse(segment.Substring(smashggPos + TEMPLATE_SMASHGG.Length, segment.Length - smashggPos - TEMPLATE_SMASHGG.Length - TEMPLATE_END.Length).Trim(), out smashggID))
                        {
                            i = entryend; 
                            continue;
                        }
                    }

                    PlayerInfo newPlayer = new PlayerInfo(player, flag, regex, smashggID);
                    players.Add(smashggID, newPlayer);

                    i = entryend;
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                form.Log += "Error during parsing of AKA database: " + ex.ParamName + " = " + ex.ActualValue + "\r\n";
                Console.WriteLine(ex.StackTrace);
            }
            catch (Exception ex)
            {
                form.Log += "Error during parsing of AKA database: Index " + i + "\r\n";

                if (segment != String.Empty)
                {
                    form.Log += segment + "\r\n";
                }
                form.Log += ex.Message + "\r\n";
            }
        }

        /// <summary>
        /// Saves the AKA database json to file
        /// </summary>
        /// <param name="json">Database json, as returned by the wiki API</param>
        /// <param name="source">Which database to save</param>
        /// <returns>True if the file was saved successfully</returns>
        public async Task<bool> SaveDatabaseAsync(string json, DbSource source)
        {
            // Save data to file
            try
            {
                // Set the filename
                string filename;
                if (source == DbSource.Fighters)
                {
                    filename = FIGHTERS_DB_FILENAME;
                }
                else if (source == DbSource.Smash)
                {
                    filename = SMASH_DB_FILENAME;
                }
                else
                {
                    return false;
                }

                using (FileStream file = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                using (StreamWriter writer = new StreamWriter(file))
                {
                    await writer.WriteAsync(json);
                }

                return true;
            }
            catch
            {
                Console.WriteLine("Couldn't save json.");
                return false;
            }
        }
    }
}
