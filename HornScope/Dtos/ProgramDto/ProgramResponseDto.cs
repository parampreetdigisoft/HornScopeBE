using HornScope.Models;

namespace HornScope.Dtos.ProgramDto
{
    public class ProgramResponseDto : ClimateProgram
    {
        public string? AssignedBy { get; set; }
        public decimal? Score { get; set; }// highest score have top rank
        public decimal? AiScore { get; set; }
    }
    public class StaffProgramMappingResponseDto : ProgramResponseDto
    {
        public int StaffProgramMappingID { get; set; }
        public AssessmentPhase? AssessmentPhase { get; set; } = Models.AssessmentPhase.InProgress;
        public double? Distance { get; set; }
        public List<int>? PeerProgramIDs { get; set; }
    }
}
