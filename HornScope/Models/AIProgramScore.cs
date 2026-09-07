namespace HornScope.Models
{   
    public class AIProgramScore
    { 
        public int ProgramScoreID { get; set; }        
        public int ClimateProgramID { get; set; }        
        public int Year { get; set; }        
        //public decimal? AIScore { get; set; }       
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public decimal? Discrepancy { get; set; }     
        public string ConfidenceLevel { get; set; }     
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
        public DateTime UpdatedAt { get; set; }
        public bool IsVerified { get; set; } = false;
        public int? VerifiedBy { get; set; }
        public ClimateProgram Program { get; set; }      
        public string? KeyFindings { get; set; } // Stores point-wise Key Findings
        public string? Recommendations { get; set; } // Stores point-wise Recommendations

    }

}
