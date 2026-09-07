using HornScope.Common.Models;
using HornScope.Dtos.AiDto;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.ClientDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.kpiDto;
using HornScope.Dtos.ProgramDto;
using HornScope.Dtos.PublicDto;
using HornScope.Enums;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface IClientService
    {
        Task<List<Pillar>> GetAllAsync(int userId, UserRole userRole);
        Task<ResultResponseDto<List<PartnerProgramResponseDto>>> GetClientPrograms(int userID);
        Task<ResultResponseDto<ProgramHistoryDto>> GetProgramHistory(int userId, TieredAccessPlan tier);
        Task<ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>> GetProgramProgressByUserId(int userID);
        Task<GetProgramQuestionHistoryResponseDto> GetProgramQuestionHistory(UserProgramRequestDto userProgramRequestDto);
        Task<PaginationResponse<ProgramResponseDto>> GetProgramAsync(PaginationRequest request);
        Task<ResultResponseDto<ProgramDetailsDto>> GetProgramDetails(UserProgramRequestDto userProgramRequestDto);
        Task<ResultResponseDto<List<ProgramPillarQuestionDetailsDto>>> GetProgramPillarDetails(StaffProgramGetPillarInfoRequestDto userProgramGetPillarInfoRequestDto);
        Task<ResultResponseDto<string>> AddClientKpisProgramAndPillar(AddClientKpisProgramAndPillar payload,int userID, string tierName);
        Task<ResultResponseDto<List<GetAllKpisResponseDto>>> GetProgramUserKpi(int userID, string tierName);
        Task<ResultResponseDto<CompareProgramResponseDto>> ComparePrograms(CompareProgramsRequestDto c, int userId, string tierName, bool applyPagination = true);
        Task<ResultResponseDto<AiProgramPillarResponseDto>> GetAIProgramPillars(AiProgramPillarRequestDto r, int userID, string tierName);
        Task<Tuple<string, byte[]>> ExportComparePrograms(CompareProgramsRequestDto request, int userId, string tierName);
    }
}
