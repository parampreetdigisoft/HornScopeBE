namespace HornScope.Models
{
    public class FiveLevelInterpretation
    {
        public int InterpretationID { get; set; }
        public int LayerID { get; set; }
        public decimal? MinRange { get; set; }
        public decimal? MaxRange { get; set; } 
        public string Condition { get; set; }
        public string Descriptor { get; set; }        
        public AnalyticalLayer? AnalyticalLayer { get; set; }
    }

    public class DashboardInterpretation
    {
        public int DashboardInterpretationID { get; set; }
        public int DashboardModeID { get; set; }
        public decimal? MinRange { get; set; }
        public decimal? MaxRange { get; set; }
        public string Condition { get; set; }
        public string Description { get; set; }
    }
}
