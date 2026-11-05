namespace smashgg_to_liquipedia.Smashgg
{
    public class TreeNodeData
    {
        public enum NodeType { Tournament, Event, Phase, Wave, PhaseGroup, Unknown }

        public int id { get; set; }
        public string name { get; set; }
        public NodeType nodetype { get; set; }

        public int playersPerEntrant { get; set; }
    }
}
