using HornScope.Common.Models;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.UserDtos;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface IUserService
    {
        User GetByEmail(string email);
        Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRoleWithAssignedCountry(GetUserByRoleRequestDto requestDto, int userid, UserRole userRole);
        Task<ResultResponseDto<List<PublicUserResponse>>> GetEvaluatorByAnalyst(GetAssignUserDto requestDto);
        Task<ResultResponseDto<List<GetAssessmentResponseDto>>> GetUsersAssignedToCountry(int countryId);
        Task<ResultResponseDto<UpdateUserResponseDto>> GetUserInfo(int userId);

    }
} 