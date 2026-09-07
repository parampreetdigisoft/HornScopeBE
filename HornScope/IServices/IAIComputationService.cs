using AssessmentPlatform.Dtos.AiDto;
using HealthIntelligence.Dtos.AiDto;
using Microsoft.AspNetCore.Mvc;
using HornScope.Common.Models;
using HornScope.Dtos.AiDto;
using HornScope.Dtos.CommonDto;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface IAIComputationService
    {
        Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels();
        Task<PaginationResponse<AiProgramSummeryDto>> GetAIPrograms(AiProgramSummaryRequestDto request, int userID, UserRole userRole);
        Task<ResultResponseDto<AiProgramPillarResponseDto>> GetAIProgramPillars(int climateProgramID, int userID, UserRole userRole);
        Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiProgramPillarSummeryRequestDto r, int userID, UserRole userRole);
        Task<IQueryable<AiProgramSummeryDto>> GetProgramAiSummeryDetails(int userID, UserRole userRole, int? climateProgramID);
        Task<byte[]> GenerateProgramDetailsReport(AiProgramSummeryDto programDetails, UserRole userRole, int userID, DocumentFormat format = DocumentFormat.Pdf, string reportType = "AI");
        Task<byte[]> GeneratePillarDetailsReport(AiProgramPillarResponse programDetails, UserRole userRole,DocumentFormat format = DocumentFormat.Pdf);
        Task<ResultResponseDto<AiCrossProgramsResponseDto>> GetAICrossProgramPillars(AiClimateProgramIDsDto ids, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> ChangedAiProgramEvaluationStatus(ChangedAiProgramEvaluationStatusDto aiClimateProgramIDsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegenerateAiSearch(RegenerateAiSearchDto aiClimateProgramIDsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> AddComment(AddCommentDto aiClimateProgramIDsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegeneratePillarAiSearch(RegeneratePillarAiSearchDto aiClimateProgramIDsDto, int userID, UserRole userRole);
        Task<AiProgramSummeryDto> GetProgramAiSummeryDetail(int userID, UserRole userRole, int? climateProgramID, string reportType = "AI");
        Task<List<AiProgramSummeryDto>> GetAllProgramAiSummeryDetail(int userID, UserRole userRole);   
        Task<byte[]> GenerateAllProgramDetailsReport(List<AiProgramSummeryDto> programDetails, UserRole userRole, int userID, DocumentFormat format = DocumentFormat.Pdf);
        Task<ResultResponseDto<string>> AITransferAssessment(AITransferAssessmentRequestDto r, int userID, UserRole userRole);
        Task<ResultResponseDto<string>> ReCalculateKpis(int userID, UserRole userRole);
        Task<ResultResponseDto<string>> UploadAiDocuments(UploadAiDocumentRequest r, int userID, UserRole userRole);
        Task<PaginationResponse<GetProgramDocumentResponseDto>> GetAIProgramDocuments(AiProgramDocumentRequestDto request,int userID, UserRole userRole);
        Task<ResultResponseDto<List<GetProgramPillarDocumentResponseDto>>> GetAIProgramPillarDocuments(AiProgramPillarDocumentRequestDto request,int userID, UserRole userRole);
        Task<ResultResponseDto<string>> DeleteDocument(DeleteProgramDocumentRequestDto request, int userID, UserRole userRole);
        Task<FileResult> DownloadDocument(int ProgramDocumentID, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> UpdateAIProgramScore(UpdateAIProgramScoreDto dto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> UpdateAIPillarScore(UpdateAIPillarScoreDto dto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> UpdateAIDataSourceCitation(UpdateAIDataSourceCitationDto dto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> UpdateAIEstimatedQuestionScore(UpdateAIEstimatedQuestionScoreDto dto, int userID, UserRole userRole);
    }
}
