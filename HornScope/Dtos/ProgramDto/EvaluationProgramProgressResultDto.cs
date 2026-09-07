namespace HornScope.Dtos.ProgramDto
{
    public class EvaluationProgramProgressResultDto
    {
        public int PillarID { get; set; }
        public int ClimateProgramID { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal ScoreProgress { get; set; }
        public decimal AIProgress { get; set; }
        public int TotalAssessments { get; set; }
        public int UserID { get; set; }
    }

    public class ProgramRankingResultDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public decimal? ProgramAIScore { get; set; }
        public int TotalPrograms { get; set; }
        public int ProgramRank { get; set; }
        public int? DataYear { get; set; }
    }

    public class EvaluationProgramProgressHistoryResultDto
    {
        public int PillarID { get; set; }
        public int ClimateProgramID { get; set; }
        public int UserID { get; set; }
        public int TotalAns { get; set; }
        public int TotalAssessments { get; set; }
        public int Year { get; set; }
        public int ManualCriticalFailureCount { get; set; }
        public decimal ScoreProgress { get; set; }
    }
}
