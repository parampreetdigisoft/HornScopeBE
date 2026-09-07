using HornScope.Enums;
using HornScope.Models;

namespace HornScope.Dtos.AssessmentDto
{
    public class AddAssessmentDto
    {
        public int AssessmentID { get; set; }
        public int StaffProgramMappingID { get; set; }
        public int PillarID { get; set; }
        public List<AddAssesmentResponseDto> Responses { get; set; }
        public bool IsAutoSave { get; set; } = false;
        public bool IsFinalized { get; set; } = false;
    }
    public class AddAssesmentResponseDto
    {
        public int ResponseID { get; set; }
        public int AssessmentID { get; set; }
        public int QuestionID { get; set; }
        public int QuestionOptionID { get; set; }
        public int? Score { get; set; }
        public string Justification { get; set; }
        public string? Source { get; set; }
    }
}
