using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.AssessmentDto
{
    public class GetAssessmentQuestoinRequestDto : PaginationRequest
    {
        public int AssessmentID { get; set; } 
        public int? PillarID { get; set; }
    }
}
