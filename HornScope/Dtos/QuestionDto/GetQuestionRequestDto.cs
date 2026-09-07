using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.QuestionDto
{
    public class GetQuestionRequestDto : PaginationRequest
    {
        public int? PillarID { get; set; }
    }
}
