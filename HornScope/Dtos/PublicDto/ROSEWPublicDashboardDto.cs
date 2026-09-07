namespace HornScope.Dtos.PublicDto
{
    public class ROSEWPublicDashboardDto
    {
        public decimal? Score { get; set; }
        public string? OverallCondition { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<ROSEWPublicProgramDto> Programs { get; set; } = new();
        public List<ROSEWPublicQuestionDto> Questions { get; set; } = new();
    }
    public class ROSEWPublicProgramDto
    {
        public string ProgramName { get; set; } 
        public string Location { get; set; }
        public DateTime? UpdatedAt { get; set; } 
        public string? Condition { get; set; }
    }
    public class ROSEWPublicQuestionDto
    {
        public string QuestionDescription { get; set; } = string.Empty;
        public string? Condition { get; set; }
    }

}
