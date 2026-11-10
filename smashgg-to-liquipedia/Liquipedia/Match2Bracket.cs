using System.Collections.Generic;
using smashgg_to_liquipedia.Smashgg.Schema;

namespace smashgg_to_liquipedia.Liquipedia
{
    /// <summary>
    /// A match2 bracket template filled with start.gg sets
    /// </summary>
    public class Match2Bracket
    {
        /// <summary>Template page, e.g. "Bracket/8-2Q"</summary>
        public string BracketType { get; }

        /// <summary>Wiki section header placed above the bracket; may be empty</summary>
        public string Header { get; }

        /// <summary>All matches of the template, in match2id order</summary>
        public List<LpdbMatch2> TemplateMatches { get; }

        /// <summary>Sets keyed by short match id (e.g. "R01-M001"). Template matches without a set are absent.</summary>
        public Dictionary<string, Set> Matches { get; }

        /// <summary>Parts of the template that could not be paired with start.gg sets</summary>
        public List<string> Warnings { get; }

        public Match2Bracket(string bracketType, string header, List<LpdbMatch2> templateMatches,
                             Dictionary<string, Set> matches, List<string> warnings)
        {
            BracketType = bracketType;
            Header = header;
            TemplateMatches = templateMatches;
            Matches = matches;
            Warnings = warnings;
        }
    }
}
