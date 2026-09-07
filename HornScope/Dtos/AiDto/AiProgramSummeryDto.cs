namespace HornScope.Dtos.AiDto
{
    public class AiProgramSummeryDto
    {       
        public int ClimateProgramID { get; set; }
        public string? ProgramName { get; set; }        
        public string? Image { get; set; }
        public int Year { get; set; }
        public string Location { get; set; }
        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public decimal? Discrepancy { get; set; }

        public string ConfidenceLevel { get; set; }
        public string ProgramScoreSummery { get; set; }
        public string EvidenceSummary { get; set; }

        public string StructuralEvidence { get; set; }
        public string OperationalEvidence { get; set; }
        public string OutcomeEvidence { get; set; }
        public string PerceptionEvidence { get; set; }
        public string TemporalScope { get; set; }
        public string DistortionScreening { get; set; }
        public string GeopoliticalShock { get; set; }
        public string FinanceShock { get; set; }
        public string LegitimacyShock { get; set; }
        public string OverallStressResilience { get; set; }
        public string StressScoreAdjustment { get; set; }
        public string InclusionEquityAdjustment { get; set; }
        public string OpacityRisk { get; set; }
        public string NonCompensationNote { get; set; }
        public string CrossPillarPatterns { get; set; }
        public string RelationalIntegrity { get; set; }
        public string InstitutionalCapacity { get; set; }
        public string EquityAssessment { get; set; }
        public string GovernanceTrajectory { get; set; }
        public string StrategicRecommendation { get; set; }
        public string AssessmentValueNote { get; set; }
        public string PrimarySource { get; set; }
        /// <summary>Point-wise key findings (stored in KeyDevelopments column).</summary>
        public string? KeyFindings { get; set; }
        /// <summary>Point-wise recommendations (stored in CriticalRisks column).</summary>
        public string? Recommendations { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsVerified { get; set; }
        public decimal? AICompletionRate { get; set; }

        public int? Rank { get; set; }
        public int? TotalProgram { get; set; }
        public int? RegionRank { get; set; }
        public int? RegionTotalProgram { get; set; }

    }
}
