using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace smashgg_to_liquipedia.Smashgg
{
    class Standardization
    {
        Dictionary<string, string> flagList = new Dictionary<string, string>();

        public Standardization()
        {
            // Populate flag abbreviation list: country name -> two letter code
            using (FileStream file = File.OpenRead("Flag List.json"))
            {
                Dictionary<string, string> flags = JsonSerializer.Deserialize<Dictionary<string, string>>(file);
                foreach (KeyValuePair<string, string> flag in flags)
                {
                    flagList.Add(flag.Key.ToLower(), flag.Value.ToLower());
                }
            }
        }

        /// <summary>
        /// Takes an input country and abbreviates it to two letters
        /// </summary>
        /// <param name="country">Input country</param>
        /// <returns>Two letter abbreviation of the coutnry, or string.Empty</returns>
        public string CountryAbbreviation(string country)
        {
            if ( country == null ) return string.Empty;

            string lcCountry = country.ToLower();

            // Assume a two-letter country is already an abbreviation
            if (lcCountry.Length == 2)
            {
                return lcCountry;
            }

            // Look in the dictionary for the country
            if (flagList.ContainsKey(lcCountry))
            {
                return flagList[lcCountry];
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
