using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HornScope.Models
{
    public class AIPillarScore
    {
        public int PillarScoreID { get; set; }
        public int CountryID { get; set; }
        public int PillarID { get; set; }
        public int Year { get; set; }
        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public decimal? Discrepancy { get; set; }
        public string? ConfidenceLevel { get; set; }
        public string? EvidenceSummary { get; set; }
        public string? StructuralEvidence { get; set; }
        public string? OperationalEvidence { get; set; }
        public string? OutcomeEvidence { get; set; }
        public string? PerceptionEvidence { get; set; }
        public string? TemporalReliability { get; set; }
        public string? RelationalIntegrity { get; set; }
        public string? StressGeopoliticalShock { get; set; }
        public string? StressEconomicShock { get; set; }
        public string? StressFinanceShock { get; set; }
        public string? DataOpacityRisk { get; set; }
        public string? ReliabilityAssessment { get; set; }
        public string? DataGapAnalysis { get; set; }
        public string? RedFlag { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }
        public Country? Country { get; set; }
        public Pillar? Pillar { get; set; }
        public ICollection<AIDataSourceCitation>? DataSourceCitations { get; set; }
    }

}
