using HornScope.Models;

namespace HornScope.Dtos.kpiDto
{
    public class GetMutiplekpiLayerResultsDto
    {
        public int LayerID { get; set; }
        public string LayerCode { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string? CalText4 { get; set; }
        public string? CalText5 { get; set; }
        public List<MutipleProgramskpiLayerResults> Programs { get; set; } = new();
        public ICollection<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = new List<FiveLevelInterpretation>();
    }
    public class MutipleProgramskpiLayerResults
    {
        public int LayerResultID { get; set; }
        public int ClimateProgramID { get; set; }
        public int? InterpretationID { get; set; } 
        public decimal? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public int? AiInterpretationID { get; set; } 
        public decimal? AiCalValue5 { get; set; }
        public DateTime? AiLastUpdated { get; set; }
        public ClimateProgram? Program { get; set; }
    }
}
