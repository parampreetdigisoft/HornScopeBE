using HornScope.Common.Models;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.ProgramDto;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface IProgramService
    {
        Task<PaginationResponse<StaffProgramMappingResponseDto>> GetProgramsAsync(ProgramPaginationRequest request, UserRole userRole);
        Task<ResultResponseDto<List<StaffProgramMappingResponseDto>>> GetAllProgramsByUserId(int userId, UserRole userRole);
        Task<ResultResponseDto<ClimateProgram>> GetByIdAsync(int id);
        Task<ResultResponseDto<string>> AddBulkProgramsAsync(BulkAddProgramDto q, string image = "");
        Task<ResultResponseDto<ClimateProgram>> EditProgramAsync(int id, AddUpdateProgramDto q);
        Task<ResultResponseDto<bool>> DeleteProgramAsync(int id);
        Task<ResultResponseDto<object>> AssignProgramToUser(int userId, int ClimateProgramID, int assignedByUserId);
        Task<ResultResponseDto<object>> EditAssignProgram(int id,int userId, int ClimateProgramID, int assignedByUserId);
        Task<ResultResponseDto<object>> UnAssignProgram(StaffProgramUnMappingRequestDto requestDto);
        Task<ResultResponseDto<List<StaffProgramMappingResponseDto>>> GetProgramByUserIdForAssessment(int userId);
        Task<ResultResponseDto<ProgramHistoryDto>> GetProgramHistory(int userID, UserRole userRole);
        Task<ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>> GetProgramsProgressByUserId(int userID, UserRole userRole);
        Task<ResultResponseDto<string>> AddUpdateProgram(AddUpdateProgramDto q);
        Task<ResultResponseDto<List<StaffProgramMappingResponseDto>>> GetAiAccessProgram(int userId, UserRole userRole);        
        Task<ResultResponseDto<byte[]>> ExportPrograms(ExportProgramsWithOptionDto request, int userId, UserRole userRole);
    }
}
