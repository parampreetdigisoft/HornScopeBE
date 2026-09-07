using HornScope.Dtos.CommonDto;
using HornScope.Models;

namespace HornScope.Dtos.AssessmentDto
{
    public class GetAssessmentRequestDto : PaginationRequest
    {
        public int? SubUserID { get; set; } //Means admin or analyst can see result of a user that they has permission
        public int? ClimateProgramID { get; set; }
        public UserRole? Role { get; set; }
    }
}
    