using AssessmentPlatform.Dtos.AiDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HornScope.Dtos.AiDto;
using HornScope.IServices;
using HornScope.Models;
using System.Security.Claims;
using HealthIntelligence.Dtos.AiDto;

namespace HornScope.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiComputationController : ControllerBase
    {

        private readonly IAIComputationService _aIComputationService;
        public AiComputationController(IAIComputationService aIComputationService)
        {
            _aIComputationService = aIComputationService;
        }

        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }

        //private string? GetTierFromClaims()
        //{
        //    return User.FindFirst("Tier")?.Value;
        //}
        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        [HttpGet("getAITrustLevels")]
        public async Task<IActionResult> GetAITrustLevels()
        {
            return Ok(await _aIComputationService.GetAITrustLevels());
        }

        [HttpGet("getAIPrograms")]
        public async Task<IActionResult> GetAIPrograms([FromQuery] AiProgramSummaryRequestDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.GetAIPrograms(request, userId.Value, userRole));
        }

        [HttpGet("getAIProgramPillars")]
        public async Task<IActionResult> GetAIProgramPillars([FromQuery] AiProgramPillarRequestDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.GetAIProgramPillars(request.ClimateProgramID, userId.Value, userRole));
        }

        [HttpGet("getAIPillarQuestions")]
        public async Task<IActionResult> GetAIPillarQuestions([FromQuery] AiProgramPillarSummeryRequestDto r)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.GetAIPillarsQuestion(r, userId.Value, userRole));
        }

        [HttpGet("aiProgramDetailsReport")]
        [Authorize(Roles = "Admin, ProgramUser")]
        public async Task<IActionResult> DownloadProgramReport([FromQuery] AiProgramSummeryRequestPdfDto request)
        {
            try
            {
                var userId = GetUserIdFromClaims();
                if (userId == null)
                    return Unauthorized("User ID not found in token.");

                var role = GetRoleFromClaims();
                if (role == null)
                    return Unauthorized("You Don't have access.");

                if (!Enum.TryParse<UserRole>(role, true, out var userRole))
                {
                    return Unauthorized("You Don't have access.");
                }

                var programDetails = await _aIComputationService.GetProgramAiSummeryDetail(userId ?? 0, userRole, request.ClimateProgramID, request.ReportType);

                // Generate PDF               

                string fileName;
                byte[] fileBytes;
                string contentType;

                fileBytes = await _aIComputationService.GenerateProgramDetailsReport(programDetails, userRole, userId ?? 0, request.Format, request.ReportType);

                if (request.Format == IServices.DocumentFormat.Docx)
                {
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    fileName = $"{programDetails.ProgramName}_Details_{DateTime.Now:yyyyMMdd}.docx";
                }
                else
                {
                    contentType = "application/pdf";
                    fileName = $"{programDetails.ProgramName}_Details_{DateTime.Now:yyyyMMdd}.pdf";
                }

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, new
                {
                    message = "Error generating report",
                    error = ex.Message
                });
            }
        }

        [HttpGet("aiPillarDetailsReport")]
        [Authorize(Roles = "Admin, ProgramUser")]
        public async Task<IActionResult> DownloadPillarReport([FromQuery] AiProgramSummeryRequestPdfDto request)
        {
            try
            {
                var userId = GetUserIdFromClaims();
                if (userId == null)
                    return Unauthorized("User ID not found in token.");

                var role = GetRoleFromClaims();
                if (role == null)
                    return Unauthorized("You Don't have access.");

                if (!Enum.TryParse<UserRole>(role, true, out var userRole))
                {
                    return Unauthorized("You Don't have access.");
                }
                if (userRole != UserRole.Admin && userRole != UserRole.ProgramUser)
                    return Unauthorized("You Don't have access.");


                var pillars = await _aIComputationService.GetAIProgramPillars(request.ClimateProgramID, userId.Value, userRole);

                var pillarDetails = pillars.Result.Pillars.FirstOrDefault(x => x.PillarID == request.PillarID);
                if (pillarDetails != null)
                {
                    string contentType;
                    string fileName;

                    // Generate PDF
                    var fileBytes = await _aIComputationService.GeneratePillarDetailsReport(pillarDetails, userRole, request.Format);

                    if (request.Format == IServices.DocumentFormat.Docx)
                    {
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        fileName = $"{pillarDetails.PillarName}_Details_{DateTime.Now:yyyyMMdd}.docx";
                    }
                    else
                    {
                        contentType = "application/pdf";
                        fileName = $"{pillarDetails.PillarName}_Details_{DateTime.Now:yyyyMMdd}.pdf";
                    }

                    return File(fileBytes, contentType, fileName);
                }
                return StatusCode(500, new
                {
                    message = "Error generating Report"
                });

            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, new
                {
                    message = "Error generating Report",
                    error = ex.Message
                });
            }
        }

        [HttpPost("getAICrossProgramPillars")]
        public async Task<IActionResult> GetAICrossProgramPillars([FromBody] AiClimateProgramIDsDto aiClimateProgramIDsDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.GetAICrossProgramPillars(aiClimateProgramIDsDto, userId.Value, userRole));
        }

        [HttpPost("changedAiProgramEvaluationStatus")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> ChangedAiProgramEvaluationStatus([FromBody] ChangedAiProgramEvaluationStatusDto aiClimateProgramIDsDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.ChangedAiProgramEvaluationStatus(aiClimateProgramIDsDto, userId.Value, userRole));
        }

        [HttpPost("regenerateAiSearch")]
        [Authorize(Roles = "Admin, Analyst")]

        public async Task<IActionResult> RegenerateAiSearch([FromBody] RegenerateAiSearchDto aiClimateProgramIDsDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.RegenerateAiSearch(aiClimateProgramIDsDto, userId.Value, userRole));
        }

        [HttpPost("addComment")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> AddComment([FromBody] AddCommentDto aiClimateProgramIDsDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.AddComment(aiClimateProgramIDsDto, userId.Value, userRole));
        }
        [HttpPost("regeneratePillarAiSearch")]
        [Authorize(Roles = "Admin, Analyst")]
        public async Task<IActionResult> RegeneratePillarAiSearch([FromBody] RegeneratePillarAiSearchDto aiClimateProgramIDsDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.RegeneratePillarAiSearch(aiClimateProgramIDsDto, userId.Value, userRole));
        }
        [HttpGet("aiAllProgramDetailsReport")]
        [Authorize(Roles = "Admin, ProgramUser")]
        public async Task<IActionResult> DownloadAllProgramPdf([FromQuery] DownloadReportDto request)
        {
            try
            {
                var userId = GetUserIdFromClaims();
                if (userId == null)
                    return Unauthorized("User ID not found in token.");

                var role = GetRoleFromClaims();
                if (role == null)
                    return Unauthorized("You Don't have access.");

                if (!Enum.TryParse<UserRole>(role, true, out var userRole))
                {
                    return Unauthorized("You Don't have access.");
                }

                var programDetails = await _aIComputationService.GetAllProgramAiSummeryDetail(userId ?? 0, userRole);
                
                if (programDetails.Count > 0)
                {
                    if (request.ClimateProgramIDs?.Count > 0)
                    {
                        programDetails = programDetails.Where(x => request.ClimateProgramIDs.Contains(x.ClimateProgramID)).ToList();
                    }
                    string fileName;
                    string contentType;
                    var pdfBytes = await _aIComputationService.GenerateAllProgramDetailsReport(programDetails, userRole, userId.GetValueOrDefault(), request.Format);

                    if (request.Format == IServices.DocumentFormat.Docx)
                    {
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        fileName = $"Programs_Details_{DateTime.Now:yyyyMMdd}.docx";
                    }
                    else
                    {
                        contentType = "application/pdf";
                        fileName = $"Programs_Details_{DateTime.Now:yyyyMMdd}.pdf";
                    }

                    return File(pdfBytes, contentType, fileName);
                }

                return NotFound("No Program Found.");

            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, new
                {
                    message = "Error generating PDF",
                    error = ex.Message
                });
            }
        }

        [HttpPost("aiResultTransfer")]
        [Authorize(Roles = "Admin, Analyst")]
        public async Task<IActionResult> AiResultTransfer([FromBody] AITransferAssessmentRequestDto aiClimateProgramIDsDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.AITransferAssessment(aiClimateProgramIDsDto, userId.Value, userRole));
        }

        [HttpGet("reCalculateKpis")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> ReCalculateKpis()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.ReCalculateKpis(userId.Value, userRole));
        }

        [HttpPost("uploadAiDocuments")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UploadAiDocuments([FromForm]  UploadAiDocumentRequest uploadAiDocumentRequest)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            if (uploadAiDocumentRequest.Files == null || !uploadAiDocumentRequest.Files.Any())
                return BadRequest("No files uploaded.");


            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.UploadAiDocuments(uploadAiDocumentRequest ,userId.Value, userRole));
        }

        [HttpGet("getAIProgramDocuments")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> GetAIProgramDocuments([FromQuery] AiProgramDocumentRequestDto uploadAiDocumentRequest)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.GetAIProgramDocuments(uploadAiDocumentRequest, userId.Value, userRole));
        }

        [HttpGet("getAIProgramPillarDocuments")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> GetAIProgramPillarDocuments([FromQuery] AiProgramPillarDocumentRequestDto uploadAiDocumentRequest)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.GetAIProgramPillarDocuments(uploadAiDocumentRequest, userId.Value, userRole));
        }

        [HttpPost("deleteDocument")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> DeleteDocument([FromBody] DeleteProgramDocumentRequestDto uploadAiDocumentRequest)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");


            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _aIComputationService.DeleteDocument(uploadAiDocumentRequest, userId.Value, userRole));
        }

        [HttpGet("downloadDocument/{Id}")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> DownloadDocument(int Id)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");


            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _aIComputationService.DownloadDocument(Id, userId.GetValueOrDefault(), userRole);

            return result;
        }

        [HttpPost("updateAIProgramScore")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAIProgramScore([FromBody] UpdateAIProgramScoreDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _aIComputationService.UpdateAIProgramScore(request, userId.Value, userRole));
        }

        [HttpPost("updateAIPillarScore")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAIPillarScore([FromBody] UpdateAIPillarScoreDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _aIComputationService.UpdateAIPillarScore(request, userId.Value, userRole));
        }

        [HttpPost("updateAIDataSourceCitation")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAIDataSourceCitation([FromBody] UpdateAIDataSourceCitationDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _aIComputationService.UpdateAIDataSourceCitation(request, userId.Value, userRole));
        }

        [HttpPost("updateAIEstimatedQuestionScore")]
        [Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> UpdateAIEstimatedQuestionScore([FromBody] UpdateAIEstimatedQuestionScoreDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return Unauthorized("You Don't have access.");

            return Ok(await _aIComputationService.UpdateAIEstimatedQuestionScore(request, userId.Value, userRole));
        }
    }
}
