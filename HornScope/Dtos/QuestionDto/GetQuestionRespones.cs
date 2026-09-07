using HornScope.Models;

namespace HornScope.Dtos.QuestionDto
{
    public class GetQuestionResponse : AddUpdateQuestionDto
    {
        public int DisplayOrder { get; set; }
        public string PillarName { get; set; }
    }
    public class GetQuestionByProgramResponse : GetQuestionResponse
    {
        public int AssessmentID { get; set; }
        public int PillarDisplayOrder { get; set; }
    }
    public class GetPillarQuestionByProgramResponse 
    {
        public int AssessmentID { get; set; }
        public int StaffProgramMappingID { get; set; }
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public int SubmittedPillarDisplayOrder { get; set; }
        public List<AssessmentQuestionResponseDto> Questions { get; set; }
    }
}
