namespace HornScope.Dtos.chatDto
{
    public class PerformanceSummary
    {
        public string Trend { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
    }

    public class CombinedRiskItem
    {
        public int Rank { get; set; }

        public string Title { get; set; } = string.Empty;

        public int RiskScore { get; set; }

        public string Severity { get; set; } = string.Empty;

        public string Trend { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Recommendation { get; set; } = string.Empty;
    }

    public class EarlyWarningItem
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Timeframe { get; set; } = string.Empty;

        public string ImpactLevel { get; set; } = string.Empty;
    }

    public class ProgramExecutiveSlidesResult
    {
        public ProgramRankingResponseDto Program { get; set; }

        public PerformanceSummary RecentPerformance { get; set; } = new();

        public List<CombinedRiskItem> CombinedRisks { get; set; } = new();

        public List<EarlyWarningItem> EarlyWarnings { get; set; } = new();
    }

    public class ChatProgramExecutiveSlidesResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public ProgramExecutiveSlidesResult Result { get; set; } = new();
    }

    public class ProgramSlidesRequest
    {
        public int ClimateProgramID { get; set; }
    }

    public class ProgramRankingResponseDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public int TotalProgram { get; set; }
        public int TotalProgramInRegion { get; set; }
        public int ProgramRank { get; set; }
        public int RegionRank { get; set; }
        public decimal? ProgramAIScore { get; set; }
        public int? DataYear { get; set; }
        public string Location { get; set; }
        public List<PillarsUserHistroyResponseDto> Pillars { get; set; }
    }
    public class PillarsUserHistroyResponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string ImagePath { get; set; }
        public decimal PillarScore { get; set; }
        public int DisplayOrder { get; set; }
    }

}
