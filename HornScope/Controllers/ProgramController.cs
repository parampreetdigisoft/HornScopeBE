using HornScope.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HornScope.IServices;
using HornScope.Dtos.ProgramDto;

namespace HornScope.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "StaffOnly")]
    public class ProgramController : ControllerBase
    {
        private readonly IProgramService _programService;
        public ProgramController(IProgramService programService)
        {
            _programService = programService;
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

        [HttpGet("programs")]
        public async Task<IActionResult> GetPrograms([FromQuery] ProgramPaginationRequest request)
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

            request.UserId = userId;
            return Ok(await _programService.GetProgramsAsync(request, userRole));
        }

        [HttpGet("getAllProgramsByUserId/{userId}")]
        public async Task<IActionResult> GetAllProgramsByUserId(int userId)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _programService.GetAllProgramsByUserId(claimUserId.GetValueOrDefault(), userRole));
        }

        [HttpGet("programs/{id}")]
        public async Task<IActionResult> GetByIdAsync(int id) => Ok(await _programService.GetByIdAsync(id));

        [HttpPost("addUpdateProgram")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddUpdateProgram([FromForm] AddUpdateProgramDto q)
        {
            var result = await _programService.AddUpdateProgram(q);
            return Ok(result);
        }

        [HttpPost("addBulkPrograms")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddBulkPrograms([FromBody] BulkAddProgramDto q)
        {
            var result = await _programService.AddBulkProgramsAsync(q);
            return Ok(result);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditProgram(int id, [FromBody] AddUpdateProgramDto q)
        {
            var result = await _programService.EditProgramAsync(id, q);
            return Ok(result);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProgram(int id)
        {
            var success = await _programService.DeleteProgramAsync(id);
            return Ok(success);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("assignProgram")]
        public async Task<IActionResult> AssignProgram([FromBody] StaffProgramMappingRequestDto q)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found.");

            q.UserId = userId.Value;
            var result = await _programService.AssignProgramToUser(q.UserId, q.ClimateProgramID, q.AssignedByUserId);
            return Ok(result);
        }

        [HttpPut]
        [Authorize(Roles = "Admin")]
        [Route("assignProgram/{id}")]
        public async Task<IActionResult> EditAssignProgram(int id, [FromBody] StaffProgramMappingRequestDto q)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != q.AssignedByUserId)
                return Unauthorized("User ID not found.");

            var result = await _programService.EditAssignProgram(id, q.UserId, q.ClimateProgramID, q.AssignedByUserId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        [Route("unAssignProgram")]
        public async Task<IActionResult> UnAssignProgram([FromBody] StaffProgramUnMappingRequestDto requestDto)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != requestDto.AssignedByUserId)
                return Unauthorized("User ID not found.");

            var result = await _programService.UnAssignProgram(requestDto);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getProgramByUserIdForAssessment/{userId}")]
        public async Task<IActionResult> GetProgramByUserIdForAssessment(int userId)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != userId)
                return Unauthorized("User ID not found.");

            var result = await _programService.GetProgramByUserIdForAssessment(userId);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getProgramHistory")]
        public async Task<IActionResult> GetProgramHistory()
        {
            var claimUserId = GetUserIdFromClaims();                                    
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _programService.GetProgramHistory(claimUserId.GetValueOrDefault(), userRole);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getProgramsProgressByUserId")]
        public async Task<IActionResult> getProgramsProgressByUserId()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found.");
            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _programService.GetProgramsProgressByUserId(userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }

         
        [HttpGet("getAiAccessProgram")]
        public async Task<IActionResult> GetAiAccessProgram()
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _programService.GetAiAccessProgram(claimUserId.GetValueOrDefault(), userRole));
        }

        [HttpGet("exportPrograms")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportPrograms([FromQuery] ExportProgramsWithOptionDto request)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _programService.ExportPrograms(request, claimUserId.GetValueOrDefault(), userRole);

            if (!result.Succeeded)
                return BadRequest(result.Messages);

            string fileName = $"Programs_Progress_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

            return File(
                result.Result ?? new byte[1],
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }
    }
}
