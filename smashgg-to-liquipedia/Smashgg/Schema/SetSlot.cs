namespace smashgg_to_liquipedia.Smashgg.Schema
{
    public class SetSlot
    {
        #region GraphQL fields
        public string id { get; set; }
        public Entrant entrant { get; set; }
        public int? seedId { get; set; }
        public string prereqId { get; set; }
        public string prereqType { get; set; }
        /// <summary>Placement in the prerequisite set that fills this slot: 1 = its winner, 2 = its loser</summary>
        public int? prereqPlacement { get; set; }
        #endregion
    }
}
