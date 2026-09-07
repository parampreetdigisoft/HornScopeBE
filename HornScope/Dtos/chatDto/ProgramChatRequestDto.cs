namespace HornScope.Dtos.chatDto
{
    public class ProgramChatRequestDto : ChatGlobalAskQuestionRequestDto
    {
        public int ClimateProgramID { get; set; }
        public int? PillarID { get; set; }
    }
    public class ChatGlobalAskQuestionRequestDto
    {
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }
    public class CrossComparisionRequestDto
    {
        public List<int> ClimateProgramIDs { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
    }

}
