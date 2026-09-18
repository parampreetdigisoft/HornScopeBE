namespace HornScope.Models
{
    public class AIEstimatedQuestionScore
    {
        public int QuestionScoreID { get; set; }
        public int CountryID { get; set; }
        public int PillarID { get; set; }
        public int QuestionID { get; set; }
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
        public string? RelationalDependencies { get; set; }

        public string? StressGeopoliticalShock { get; set; }
        public string? StressEconomicShock { get; set; }
        public string? StressFinanceShock { get; set; }

        public string? DataOpacityRisk { get; set; }
        public string? RedFlag { get; set; }

        public string? SourceName { get; set; }
        public string? SourceType { get; set; }
        public string? SourceURL { get; set; }
        public int? SourceDataYear { get; set; }
        public int? SourceHierarchyLevel { get; set; }
        public string? SourceDataExtract { get; set; }
        public int? SourcesConsulted { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Country? Country { get; set; }
        public Pillar? Pillar { get; set; }
        public Question? Question { get; set; }
    }

}
