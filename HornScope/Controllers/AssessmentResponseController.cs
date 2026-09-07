using HornScope.Dtos.AssessmentDto;
using HornScope.IServices;
using HornScope.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HornScope.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StaffOnly")]
    public class AssessmentResponseController : ControllerBase
    {
        private readonly IAssessmentResponseService _responseService;
        public AssessmentResponseController(IAssessmentResponseService responseService)
        {
            _responseService = responseService;
        }
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
        private string? GetTierFromClaims()
        {
            return User.FindFirst("Tier")?.Value;
        }
        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _responseService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var resp = await _responseService.GetByIdAsync(id);
            if (resp == null) return NotFound();
            return Ok(resp);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AssessmentResponse response)
        {
            var result = await _responseService.AddAsync(response);
            return Created($"/api/assessmentresponse/{result.ResponseID}", result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] AssessmentResponse response)
        {
            var result = await _responseService.UpdateAsync(id, response);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _responseService.DeleteAsync(id);
            if (!success) return NotFound();
            return Ok();
        }

        [HttpPost]
        [Route("saveAssessment")]
        public async Task<IActionResult> SaveAssessment([FromBody] AddAssessmentDto response)
        {
            var result = await _responseService.SaveAssessment(response);
            return Ok(result);
        }

        [HttpGet]
        [Route("getAssessmentResults")]
        [Authorize]
        public async Task<IActionResult> GetAssessmentResult([FromQuery] GetAssessmentRequestDto response)
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

            response.UserId = userId.GetValueOrDefault();

            var result = await _responseService.GetAssessmentResult(response, userRole);
            return Ok(result);
        }
        [HttpGet]
        [Route("getAssessmentQuestions")]
        [Authorize]
        public async Task<IActionResult> GetAssessmentQuestions([FromQuery] GetAssessmentQuestionRequestDto response)
        {
            var result = await _responseService.GetAssessmentQuestion(response);
            return Ok(result);
        }
        [HttpPost("ImportAssessment")]
        [Authorize]
        public async Task<IActionResult> ImportAssessmentAsync(IFormFile file, [FromForm] int userID)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

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
            var content = await _responseService.ImportAssessmentAsync(file, userId.GetValueOrDefault());
            return Ok(content);
        }
        /// <summary>
        /// This API is used to get the program question history  global history for admin
        /// </summary>
        /// <param name="ClimateProgramID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getProgramQuestionHistory")]
        [Authorize]
        public async Task<IActionResult> GetProgramQuestionHistory([FromQuery] UserProgramRequestDto userProgramRequestDto)
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
            var result = await _responseService.GetProgramQuestionHistory(userProgramRequestDto);
            return Ok(result);
        }
        [HttpGet]
        [Route("getAssessmentProgressHistory")]
        [Authorize]
        public async Task<IActionResult> getAssessmentProgressHistory([FromQuery]GetProgramProgressHistoryRequestDto progressHistoryRequest)
        {
            var result = await _responseService.GetAssessmentProgressHistory(progressHistoryRequest);
            return Ok(result);
        }

        [HttpPost]
        [Route("changeAssessmentStatus")]
        [Authorize]
        public async Task<IActionResult> ChangeAssessmentStatus([FromBody] ChangeAssessmentStatusRequestDto requestDto)
        {
            var result = await _responseService.ChangeAssessmentStatus(requestDto);
            return Ok(result);
        }

        [HttpPost]
        [Route("transferAssessment")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> TransferAssessment([FromBody] TransferAssessmentRequestDto requestDto)
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

            var result = await _responseService.TransferAssessment(requestDto, userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }

        /// <summary>
        /// This API is used to get the program pillar history global history for admin
        /// </summary>
        /// <param name="ClimateProgramID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getProgramPillarHistory")]
        [Authorize]
        public async Task<IActionResult> GetProgramPillarHistory([FromQuery] UserProgramDashBoardRequestDto userProgramDashBoardRequestDto)
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
            var result = await _responseService.GetProgramPillarHistory(userProgramDashBoardRequestDto, userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }

    }
}