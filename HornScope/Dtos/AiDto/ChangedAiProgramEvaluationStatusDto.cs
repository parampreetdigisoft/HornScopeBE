using HornScope.Backgroundjob;

namespace HornScope.Dtos.AiDto
{
    public class ChangedAiProgramEvaluationStatusDto
    {
        public int ClimateProgramID { get; set; }
        public bool IsVerified { get; set; }
    }

    public class RegenerateAiSearchDto
    {
        public int ClimateProgramID { get; set; }
        public bool ProgramEnable { get; set; }
        public bool PillarEnable { get; set; }
        public bool QuestionEnable { get; set; }
        public bool RegenerateMissingQuestionsEnable { get; set; }       

        public List<int> ViewerUserIDs { get; set; } = new();
    }
    public class RegeneratePillarAiSearchDto : RegenerateAiSearchDto
    {
        public int PillarID { get; set; }
    }
    public class AddCommentDto
    {
        public int ClimateProgramID { get; set; }
        public string Comment { get; set; }

    }
}
