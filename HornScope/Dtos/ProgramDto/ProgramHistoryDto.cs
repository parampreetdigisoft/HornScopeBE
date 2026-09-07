namespace HornScope.Dtos.ProgramDto
{
    public class ProgramHistoryDto
    {
        public int TotalProgram{ get; set; }
        public int TotalAnalyst { get; set; }
        public int TotalEvaluator { get; set; }
        public int ActiveProgram { get; set; }
        public int TotalAccessProgram { get; set; }
        public int CompeleteProgram { get; set; }
        public int InprocessProgram { get; set; }
        public decimal AvgHighScore { get; set; }
        public decimal AvgLowerScore { get; set; }
        public decimal OverallVitalityScore { get; set; }
        public int FinalizeProgram { get; set; }
        public int UnFinalizeProgram { get; set; }
    }
}
