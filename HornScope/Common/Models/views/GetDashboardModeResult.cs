namespace HornScope.Common.Models.views
{
    public class GetDashboardModeResult
    {
        public int? ClimateProgramID { get; set; }
        public int? PillarID { get; set; }
        public int? QuestionID { get; set; }
        public decimal? QuestionScore { get; set; }
        public int? TotalScore { get; set; }
        public int? TotalAns { get; set; }
        public int? TotalNA { get; set; }
        public int? TotalUnknown { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal? AiQuestionScore { get; set; }
        public int? AiTotalScore { get; set; }
        public int? AiTotalAns { get; set; }
        public int? AiTotalNA { get; set; }
        public int? AiTotalUnknown { get; set; }
        public DateTime? AiUpdatedAt { get; set; }
    }
}
