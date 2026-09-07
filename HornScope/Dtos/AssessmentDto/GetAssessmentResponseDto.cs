using HornScope.Models;

namespace HornScope.Dtos.AssessmentDto
{
    public class GetAssessmentResponseDto
    {
        public int AssessmentID { get; set; }
        public int StaffProgramMappingID { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string UserRole { get; set; }
        public decimal Score { get; set; }
        public string AssignedByUser { get; set; }
        public int AssignedByUserId { get; set; }
        public AssessmentPhase? AssessmentPhase { get; set; }
    }

    public class GetProgramAssessmentResponseDto : GetAssessmentResponseDto
    {
        public int TotalIndeterminate { get; set; }
        public int TotalNA { get; set; }
    }
}
