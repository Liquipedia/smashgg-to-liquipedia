using System.Collections.Generic;
using System.Linq;
using smashgg_to_liquipedia.Smashgg.Schema;

namespace smashgg_to_liquipedia.Liquipedia
{
    /// <summary>
    /// Assigns the sets of one start.gg bracket to the matches of one or more match2 bracket templates.
    /// <para>Both brackets are trees rooted at their last matches. The template links each match to the
    /// matches feeding its opponents (toupper/tolower), and start.gg links each set slot to the set it was
    /// filled from (prereqId). The two trees are walked together from their roots, so every set lands on
    /// the template match in the same structural position.</para>
    /// <para>A start.gg bracket can be split over several templates (e.g. a final bracket plus the winners
    /// and losers rounds leading up to it). Map the template containing the last matches first; each
    /// following template continues from the unplaced sets that feed the matches already placed.</para>
    /// </summary>
    class Match2BracketMapper
    {
        /// <summary>
        /// Which start.gg sets a template may start from
        /// </summary>
        public enum Side
        {
            /// <summary>Any set</summary>
            Any,
            /// <summary>Winners bracket sets (positive rounds)</summary>
            Upper,
            /// <summary>Losers bracket sets (negative rounds)</summary>
            Lower
        }

        private const string PREREQ_SET = "set";
        private const int PLACEMENT_WINNER = 1;
        private const int PLACEMENT_LOSER = 2;

        private readonly Dictionary<string, Set> setsById = new Dictionary<string, Set>();
        private readonly HashSet<Set> resets;
        private readonly HashSet<Set> thirdPlaces;
        private readonly HashSet<Set> roots;

        // Sets placed by any template, and the placed template matches in placement order
        private readonly HashSet<Set> assignedSets = new HashSet<Set>();
        private readonly List<Placement> placements = new List<Placement>();
        private int templateCount = 0;

        private class Placement
        {
            public int templateIndex;
            public LpdbMatch2 match;
            public Set set;
        }

        /// <param name="sets">All sets of one start.gg phase group, queried with slot prereq fields</param>
        public Match2BracketMapper(IEnumerable<Set> sets)
        {
            foreach (Set set in sets)
            {
                if (!string.IsNullOrEmpty(set.id) && !setsById.ContainsKey(set.id))
                {
                    setsById.Add(set.id, set);
                }
            }

            // A reset is fed both slots by the same set (the grand final), and a third place match is fed
            // only losers. Neither continues the tree that the templates link up.
            resets = new HashSet<Set>(setsById.Values.Where(IsReset));
            var fedSets = new HashSet<Set>(setsById.Values.Where(set => !resets.Contains(set)).SelectMany(WinnerFeeders));
            thirdPlaces = new HashSet<Set>(setsById.Values.Where(set => !fedSets.Contains(set) && IsThirdPlace(set)));
            roots = new HashSet<Set>(setsById.Values.Where(
                set => !fedSets.Contains(set) && !resets.Contains(set) && !thirdPlaces.Contains(set)
            ));
        }

        /// <summary>
        /// Strips the template page prefix from a full match2id ("Bracket/8-2Q_R01-M001" -> "R01-M001")
        /// </summary>
        public static string ToShortId(string match2id)
        {
            int separator = match2id.LastIndexOf('_');
            return separator >= 0 ? match2id.Substring(separator + 1) : match2id;
        }

        /// <summary>
        /// Maps sets onto a template. The template's last matches are paired with the bracket's last sets if
        /// nothing has been mapped yet, and otherwise with the unplaced sets feeding the sets already mapped.
        /// </summary>
        /// <param name="templateMatches">Template matches, as returned by <see cref="LpdbBracketQuery.FetchBracket"/></param>
        /// <param name="side">Restricts which sets the template's last matches may be paired with</param>
        /// <param name="warnings">Receives a message for every part of the template that could not be paired</param>
        /// <returns>Sets keyed by short match id (e.g. "R01-M001"). Template matches without a set are absent.</returns>
        public Dictionary<string, Set> Map(IEnumerable<LpdbMatch2> templateMatches, Side side, List<string> warnings)
        {
            var template = new TemplateState(templateMatches, templateCount++, this, warnings);
            template.Run(StartingSets(side));
            return template.result;
        }

        /// <summary>
        /// Ids of the sets that no template has placed so far
        /// </summary>
        public List<string> UnplacedSetIds()
        {
            return setsById.Values.Where(set => !assignedSets.Contains(set)).Select(set => set.id).ToList();
        }

        private List<Set> StartingSets(Side side)
        {
            IEnumerable<Set> candidates;
            if (placements.Count == 0)
            {
                candidates = roots
                    .OrderBy(set => set.round)
                    .ThenBy(set => set.match)
                    .ThenBy(set => set.id);
            }
            else
            {
                // Top to bottom along the edge of what is already placed
                candidates = placements
                    .OrderBy(placement => placement.templateIndex)
                    .ThenBy(placement => Data(placement.match).coordinates?.sectionIndex ?? 0)
                    .ThenBy(placement => Data(placement.match).coordinates?.roundIndex ?? 0)
                    .ThenBy(placement => Data(placement.match).coordinates?.matchIndexInRound ?? 0)
                    .ThenBy(placement => placement.match.match2id)
                    .SelectMany(placement => WinnerFeeders(placement.set))
                    .Where(set => !assignedSets.Contains(set))
                    .Distinct();
            }

            return candidates.Where(set => OnSide(set, side)).ToList();
        }

        private static bool OnSide(Set set, Side side)
        {
            switch (side)
            {
                case Side.Upper:
                    return set.round > 0;
                case Side.Lower:
                    return set.round < 0;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Sets whose winners play in this set, in slot order
        /// </summary>
        private List<Set> WinnerFeeders(Set set)
        {
            return SetPrereqSlots(set)
                .Where(slot => slot.prereqPlacement == PLACEMENT_WINNER)
                .Select(slot => setsById[slot.prereqId])
                .ToList();
        }

        private List<SetSlot> SetPrereqSlots(Set set)
        {
            if (set.slots == null)
            {
                return new List<SetSlot>();
            }
            return set.slots
                .Where(slot => slot != null && slot.prereqType == PREREQ_SET
                    && slot.prereqId != null && setsById.ContainsKey(slot.prereqId))
                .ToList();
        }

        private bool IsReset(Set set)
        {
            List<SetSlot> prereqs = SetPrereqSlots(set);
            return prereqs.Count == 2 && prereqs[0].prereqId == prereqs[1].prereqId;
        }

        private bool IsThirdPlace(Set set)
        {
            List<SetSlot> prereqs = SetPrereqSlots(set);
            return prereqs.Count == 2 && !IsReset(set) && prereqs.All(slot => slot.prereqPlacement == PLACEMENT_LOSER);
        }

        private static Match2BracketData Data(LpdbMatch2 match)
        {
            return match.match2bracketdata ?? new Match2BracketData();
        }

        /// <summary>
        /// Mapping of one template
        /// </summary>
        private class TemplateState
        {
            private readonly Match2BracketMapper bracket;
            private readonly int templateIndex;
            private readonly List<string> warnings;
            private readonly Dictionary<string, LpdbMatch2> templateById = new Dictionary<string, LpdbMatch2>();
            private readonly HashSet<string> templateResets;
            private readonly HashSet<string> templateThirdPlaces;

            public readonly Dictionary<string, Set> result = new Dictionary<string, Set>();

            public TemplateState(IEnumerable<LpdbMatch2> templateMatches, int templateIndex, Match2BracketMapper bracket, List<string> warnings)
            {
                this.bracket = bracket;
                this.templateIndex = templateIndex;
                this.warnings = warnings;

                foreach (LpdbMatch2 match in templateMatches)
                {
                    if (!string.IsNullOrEmpty(match.match2id))
                    {
                        templateById[match.match2id] = match;
                    }
                }

                // Matches attached to another match instead of being linked into the tree
                templateResets = new HashSet<string>(
                    templateById.Values.Select(match => Data(match).bracketreset).Where(id => !string.IsNullOrEmpty(id))
                );
                templateThirdPlaces = new HashSet<string>(
                    templateById.Values.Select(match => Data(match).thirdplace).Where(id => !string.IsNullOrEmpty(id))
                );
            }

            public void Run(List<Set> startingSets)
            {
                List<LpdbMatch2> templateRoots = templateById.Values
                    .Where(match => string.IsNullOrEmpty(Data(match).upperMatchId)
                        && !templateResets.Contains(match.match2id)
                        && !templateThirdPlaces.Contains(match.match2id))
                    .OrderBy(match => Data(match).coordinates?.rootIndex ?? 0)
                    .ThenBy(match => Data(match).coordinates?.matchIndexInRound ?? 0)
                    .ThenBy(match => match.match2id)
                    .ToList();

                if (templateRoots.Count != startingSets.Count)
                {
                    warnings.Add(
                        "Template has " + templateRoots.Count + " last matches but start.gg has " + startingSets.Count
                        + " sets to start from; pairing from the top"
                    );
                }
                for (int i = 0; i < System.Math.Min(templateRoots.Count, startingSets.Count); i++)
                {
                    Assign(templateRoots[i], startingSets[i]);
                }

                AttachResets();
                AttachThirdPlaces();

                // A reset that was never needed has no set, which is not a problem
                List<string> unfilled = templateById.Keys
                    .Where(id => !templateResets.Contains(id))
                    .Select(ToShortId)
                    .Where(id => !result.ContainsKey(id))
                    .OrderBy(id => id)
                    .ToList();
                if (unfilled.Count > 0)
                {
                    warnings.Add("Template matches without a set: " + string.Join(", ", unfilled));
                }
            }

            /// <summary>
            /// Pairs a template match with a set, then pairs the matches feeding each of them in slot order
            /// </summary>
            private void Assign(LpdbMatch2 templateRoot, Set setRoot)
            {
                var pending = new Stack<KeyValuePair<LpdbMatch2, Set>>();
                pending.Push(new KeyValuePair<LpdbMatch2, Set>(templateRoot, setRoot));

                while (pending.Count > 0)
                {
                    LpdbMatch2 match = pending.Peek().Key;
                    Set set = pending.Pop().Value;

                    if (!AssignSingle(match, set))
                    {
                        continue;
                    }

                    List<LpdbMatch2> templateFeeders = TemplateFeeders(match);
                    List<Set> setFeeders = bracket.WinnerFeeders(set);

                    // The template ends here; earlier sets are left for a following template
                    if (templateFeeders.Count == 0)
                    {
                        continue;
                    }
                    if (templateFeeders.Count != setFeeders.Count)
                    {
                        warnings.Add(
                            ToShortId(match.match2id) + ": template match is fed by " + templateFeeders.Count
                            + " matches but start.gg set " + set.id + " by " + setFeeders.Count
                            + "; earlier rounds below it are not filled"
                        );
                        continue;
                    }

                    // Slot 1 is opponent 1, which the template feeds from toupper
                    for (int i = templateFeeders.Count - 1; i >= 0; i--)
                    {
                        pending.Push(new KeyValuePair<LpdbMatch2, Set>(templateFeeders[i], setFeeders[i]));
                    }
                }
            }

            private bool AssignSingle(LpdbMatch2 match, Set set)
            {
                string shortId = ToShortId(match.match2id);
                if (result.ContainsKey(shortId) || bracket.assignedSets.Contains(set))
                {
                    warnings.Add("Skipped pairing " + shortId + " with start.gg set " + set.id + ": already paired");
                    return false;
                }
                result.Add(shortId, set);
                bracket.assignedSets.Add(set);
                bracket.placements.Add(new Placement { templateIndex = templateIndex, match = match, set = set });
                return true;
            }

            /// <summary>
            /// Attaches each reset to the template match whose set is its grand final
            /// </summary>
            private void AttachResets()
            {
                foreach (Set reset in bracket.resets)
                {
                    Set grandFinal = bracket.setsById[reset.slots.First(slot => slot.prereqType == PREREQ_SET).prereqId];
                    LpdbMatch2 templateFinal = templateById.Values.FirstOrDefault(
                        match => result.TryGetValue(ToShortId(match.match2id), out Set mapped) && mapped == grandFinal
                    );
                    if (templateFinal == null)
                    {
                        // The grand final belongs to another template
                        continue;
                    }

                    string resetId = Data(templateFinal).bracketreset;
                    if (string.IsNullOrEmpty(resetId) || !templateById.ContainsKey(resetId))
                    {
                        warnings.Add("Template has no bracket reset match for start.gg set " + reset.id);
                        continue;
                    }
                    AssignSingle(templateById[resetId], reset);
                }
            }

            /// <summary>
            /// Attaches each third place match whose feeding sets were placed by this template
            /// </summary>
            private void AttachThirdPlaces()
            {
                var placedHere = new HashSet<Set>(result.Values);
                List<Set> setThirdPlaces = bracket.thirdPlaces
                    .Where(set => bracket.SetPrereqSlots(set).Any(slot => placedHere.Contains(bracket.setsById[slot.prereqId])))
                    .OrderBy(set => set.match)
                    .ThenBy(set => set.id)
                    .ToList();
                if (setThirdPlaces.Count == 0)
                {
                    return;
                }

                List<string> thirdPlaceIds = templateThirdPlaces.Where(templateById.ContainsKey).OrderBy(id => id).ToList();
                if (thirdPlaceIds.Count != setThirdPlaces.Count)
                {
                    warnings.Add(
                        "Template has " + thirdPlaceIds.Count + " third place matches but start.gg has "
                        + setThirdPlaces.Count
                    );
                }
                for (int i = 0; i < System.Math.Min(thirdPlaceIds.Count, setThirdPlaces.Count); i++)
                {
                    AssignSingle(templateById[thirdPlaceIds[i]], setThirdPlaces[i]);
                }
            }

            /// <summary>
            /// Template matches whose winners play in this match, opponent 1's first
            /// </summary>
            private List<LpdbMatch2> TemplateFeeders(LpdbMatch2 match)
            {
                Match2BracketData data = Data(match);
                IEnumerable<string> ids = !string.IsNullOrEmpty(data.toupper) || !string.IsNullOrEmpty(data.tolower)
                    ? new[] { data.toupper, data.tolower }
                    : (IEnumerable<string>)(data.lowerMatchIds ?? new List<string>());

                var feeders = new List<LpdbMatch2>();
                foreach (string id in ids)
                {
                    if (!string.IsNullOrEmpty(id) && templateById.TryGetValue(id, out LpdbMatch2 feeder))
                    {
                        feeders.Add(feeder);
                    }
                }
                return feeders;
            }
        }
    }
}
