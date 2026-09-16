using HornScope.Dtos.CountryUserDto;
namespace HornScope.Dtos.dashboard
{
    public class DashboardModeResponseDto
    {
        public int CountryID { get; set; }
        public int DashboardModeID { get; set; }
        public string ModeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Year { get; set; }
        public decimal HS { get; set; }
        public decimal AICountryScore { get; set; }
        public decimal ManualCountryScore { get; set; }
        public decimal ManualValue { get; set; }
        public decimal HSDirectionalMovement { get; set; }
        public string HSCondition { get; set; } = string.Empty;
        public string ManualCondition { get; set; } = string.Empty;
        public string HSDescriptor { get; set; } = string.Empty;
        public string ManualDescriptor { get; set; } = string.Empty;
        public string HSStrategicAction { get; set; } = string.Empty;
        public List<SignalCardDto> Signals { get; set; } = new();
    }

    public class DashboardQuestionScoreDto
    {
        public int QuestionID { get; set; }
        public string QuestionDescription { get; set; } = string.Empty;
        public decimal? AiScore { get; set; }
        public int? AiTotalScore { get; set; }
        public int? AiTotalAns { get; set; }
        public int? AiTotalNA { get; set; }
        public int? AiTotalUnknown { get; set; }
        public decimal? EvaluationScore { get; set; }
        public int? EvaluationTotalScore { get; set; }
        public int? EvaluationTotalAns { get; set; }
        public int? EvaluationTotalNA { get; set; }
        public int? EvaluationTotalUnknown { get; set; }
        public DateTime? EvaluationUpdatedAt { get; set; }
        public DateTime? AiUpdatedAt { get; set; }
    }


}
