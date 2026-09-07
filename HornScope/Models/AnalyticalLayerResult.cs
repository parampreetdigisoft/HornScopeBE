namespace HornScope.Models
{
    public class AnalyticalLayerResult
    {
        public int LayerResultID { get; set; }
        public int LayerID { get; set; }
        public int ClimateProgramID { get; set; }
        public int? InterpretationID { get; set; }   
        public decimal? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public int? AiInterpretationID { get; set; }      
        public decimal? AiCalValue5 { get; set; }
        public DateTime? AiLastUpdated { get; set; }
        public AnalyticalLayer AnalyticalLayer { get; set; } = new();
        public ClimateProgram? Program { get; set; }
    }
}
