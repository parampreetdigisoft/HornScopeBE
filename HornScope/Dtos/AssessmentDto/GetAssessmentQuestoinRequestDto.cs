using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.AssessmentDto
{
    public class GetAssessmentQuestionRequestDto : PaginationRequest
    {
        public int AssessmentID { get; set; } 
        public int? PillarID { get; set; }
    }
}
