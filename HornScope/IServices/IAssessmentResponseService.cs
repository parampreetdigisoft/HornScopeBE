using HornScope.Common.Models;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.dashboard;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface IAssessmentResponseService
    {
        Task<List<AssessmentResponse>> GetAllAsync();
        Task<AssessmentResponse> GetByIdAsync(int id);
        Task<AssessmentResponse> AddAsync(AssessmentResponse response);
        Task<AssessmentResponse> UpdateAsync(int id, AssessmentResponse response);
        Task<bool> DeleteAsync(int id);
        Task<ResultResponseDto<string>> SaveAssessment(AddAssessmentDto request);
        Task<PaginationResponse<GetCountryAssessmentResponseDto>> GetAssessmentResult(GetAssessmentRequestDto request, UserRole role);
        Task<PaginationResponse<GetAssessmentQuestionResponseDto>> GetAssessmentQuestion(GetAssessmentQuestoinRequestDto request);
        Task<ResultResponseDto<string>> ImportAssessmentAsync(IFormFile file,int userID);
        Task<GetCountryQuestionHistoryResponseDto> GetCountryQuestionHistory(UserCountryRequestDto userCountryRequestDto);
        Task<ResultResponseDto<GetAssessmentHistoryDto>> GetAssessmentProgressHistory(GetProgramProgressHistoryRequestDto progressHistoryRequest);
        Task<ResultResponseDto<string>> ChangeAssessmentStatus(ChangeAssessmentStatusRequestDto r);
        Task<ResultResponseDto<string>> TransferAssessment(TransferAssessmentRequestDto r, int userID, UserRole userRole);
        Task<ResultResponseDto<AiCountryPillarDashboardResponseDto>> GetCountryPillarHistory(UserCountryDashBoardRequestDto userCountryRequestDto,int userID, UserRole userRole);
    }
} 