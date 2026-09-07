using HornScope.Models;

namespace HornScope.Dtos.AiDto
{

    public class AiProgramPillarResponseDto
    {
        public List<AiProgramPillarResponse> Pillars { get; set; }
        public decimal? AIProgress { get; set; }
    }
    public class AiProgramPillarResponse
    {
        public int PillarScoreID { get; set; }
        public int ClimateProgramID { get; set; }
        public string? ProgramName { get; set; }        
        public string? Location { get; set; }        
        public int PillarID { get; set; }
        public string? PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public string? ImagePath { get; set; }
        public bool IsAccess { get; set; } = false;
        public int AIDataYear { get; set; }
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
        public string? TemporalScope { get; set; }
        public string? DistortionScreening { get; set; }
        public string? RelationalIntegrity { get; set; }

        public string? StressGeopoliticalShock { get; set; }
        public string? StressFinanceShock { get; set; }
        public string? StressLegitimacyShock { get; set; }
        public string? StressOverallResilience { get; set; }
        public string? StressScoreAdjustment { get; set; }

        public string? InclusionEquityAdjustment { get; set; }
        public string? OpacityRisk { get; set; }
        public string? NonCompensationNote { get; set; }
        public string? InclusionAccessNote { get; set; }
        public string? InstitutionalAssessment { get; set; }
        public string? DataGapAnalysis { get; set; }

        public string? RedFlag { get; set; }   // renamed from RedFlags ? RedFlag (matches DB)

        public decimal? AICompletionRate { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public ICollection<AIDataSourceCitation>? DataSourceCitations { get; set; }
    }
}
