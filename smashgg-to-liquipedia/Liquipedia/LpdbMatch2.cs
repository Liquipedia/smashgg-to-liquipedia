using System.Collections.Generic;

namespace smashgg_to_liquipedia.Liquipedia
{
    /// <summary>
    /// A match of a bracket template, as stored in LPDB
    /// </summary>
    public class LpdbMatch2
    {
        /// <summary>Full match id, e.g. "Bracket/8-2Q_R01-M001"</summary>
        public string match2id { get; set; }
        public Match2BracketData match2bracketdata { get; set; }
    }

    /// <summary>
    /// Position of a match within its bracket template.
    /// <para>The template is a tree whose roots are the last matches (e.g. the final).
    /// "upper" means towards the root and "lower" means towards the first round.</para>
    /// </summary>
    public class Match2BracketData
    {
        /// <summary>"bracket" for positional matches</summary>
        public string type { get; set; }
        /// <summary>"upper" or "lower" section of a double elimination bracket</summary>
        public string bracketsection { get; set; }
        /// <summary>The match the winner advances to; empty for a root</summary>
        public string upperMatchId { get; set; }
        /// <summary>The matches whose winners play in this match</summary>
        public List<string> lowerMatchIds { get; set; }
        /// <summary>The match whose winner becomes opponent 1</summary>
        public string toupper { get; set; }
        /// <summary>The match whose winner becomes opponent 2</summary>
        public string tolower { get; set; }
        /// <summary>Id of this match's bracket reset match, if any</summary>
        public string bracketreset { get; set; }
        /// <summary>Id of the third place match attached to this match, if any</summary>
        public string thirdplace { get; set; }
        public string header { get; set; }
        public Match2Coordinates coordinates { get; set; }
    }

    public class Match2Coordinates
    {
        public int sectionIndex { get; set; }
        public int sectionCount { get; set; }
        public int roundIndex { get; set; }
        public int roundCount { get; set; }
        public int matchIndexInRound { get; set; }
        public int rootIndex { get; set; }
        public int depth { get; set; }
        public int depthCount { get; set; }
        public int semanticDepth { get; set; }
        public int semanticRoundIndex { get; set; }
    }
}
