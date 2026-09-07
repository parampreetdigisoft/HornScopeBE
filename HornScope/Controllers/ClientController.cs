using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HornScope.Dtos.AiDto;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.ClientDto;
using HornScope.Dtos.CommonDto;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "PaidProgramUserOnly")]
    public class ClientController : ControllerBase
    {
        private readonly IClientService _clientService;

        public ClientController(IClientService clientService)
        {
            _clientService = clientService;
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
        [Route("Pillars")]
        public async Task<IActionResult> GetAll()
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

            return Ok(await _clientService.GetAllAsync(userId.GetValueOrDefault(), userRole));
        }

        [HttpGet("getProgramHistory")]
        public async Task<IActionResult> GetProgramHistory()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");
            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var tier = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _clientService.GetProgramHistory(userId.Value, tier);
            return Ok(result);
        }

        [HttpGet("getProgramsProgressByUserId")]
        public async Task<IActionResult> GetProgramsProgressByUserId()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var result = await _clientService.GetProgramProgressByUserId(userId.Value);
            return Ok(result);
        }

        [HttpGet("getProgramQuestionHistory")]
        public async Task<IActionResult> GetProgramQuestionHistory([FromQuery] UserProgramRequestDto userProgramRequestDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userProgramRequestDto.UserID = userId.Value;
            userProgramRequestDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _clientService.GetProgramQuestionHistory(userProgramRequestDto);
            return Ok(result);
        }

        [HttpGet("programs")]
        public async Task<IActionResult> GetProgramsAsync([FromQuery] PaginationRequest request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            request.UserId = userId.Value;

            var result = await _clientService.GetProgramAsync(request);
            return Ok(result);
        }

        [HttpGet("getProgramDetails")]
        public async Task<IActionResult> GetProgramDetails([FromQuery] UserProgramRequestDto userProgramRequestDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userProgramRequestDto.UserID = userId.Value;
            userProgramRequestDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _clientService.GetProgramDetails(userProgramRequestDto);
            return Ok(result);
        }


        [HttpGet("GetProgramPillarDetails")]
        public async Task<IActionResult> GetProgramPillarDetails([FromQuery] StaffProgramGetPillarInfoRequestDto userProgramGetPillarInfoRequestDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userProgramGetPillarInfoRequestDto.UserID = userId.Value;
            userProgramGetPillarInfoRequestDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _clientService.GetProgramPillarDetails(userProgramGetPillarInfoRequestDto);
            return Ok(result);
        }

        [HttpGet("getClientPrograms")]
        public async Task<IActionResult> GetClientPrograms()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var response = await _clientService.GetClientPrograms(userId.Value);
            return Ok(response);
        }

        [HttpPost("addClientKpisProgramAndPillar")]
        public async Task<IActionResult> AddClientKpisProgramAndPillar([FromBody] AddClientKpisProgramAndPillar b)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.IsDefined(typeof(TieredAccessPlan), tierName))
                return Unauthorized("Invalid tier specified.");

            var response = await _clientService.AddClientKpisProgramAndPillar(b, userId.GetValueOrDefault(), tierName);
            return Ok(response);
        }

        [HttpGet]
        [Route("getProgramUserKpi")]
        public async Task<IActionResult> GetProgramUserKpi()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var result = await _clientService.GetProgramUserKpi(userId.GetValueOrDefault(), tierName);
            return Ok(result);
        }

        [HttpPost]
        [Route("comparePrograms")]
        public async Task<IActionResult> ComparePrograms([FromBody] CompareProgramsRequestDto r)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var result = await _clientService.ComparePrograms(r,userId.GetValueOrDefault(), tierName);
            return Ok(result);
        }

        [HttpGet("getAIProgramPillars")]
        public async Task<IActionResult> GetAIProgramPillars([FromQuery] AiProgramPillarRequestDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            return Ok(await _clientService.GetAIProgramPillars(request, userId.Value, tierName));
        }

        [HttpGet("ExportComparePrograms")]
        public async Task<IActionResult> ExportComparePrograms(string programs, string? kpis, DateTime updatedAt)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var climateProgramIDs = programs.Split(',')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(int.Parse)
                .ToList();

            var kpiIds = new List<int>();

            if (!string.IsNullOrWhiteSpace(kpis) && kpis.ToLower() != "null")
            {
                kpiIds = kpis.Split(',')
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(int.Parse)
                    .ToList();
            }

            var request = new CompareProgramsRequestDto
            {
                Programs = climateProgramIDs,
                Kpis = kpiIds,
                UpdatedAt = updatedAt
            };

            var content = await _clientService.ExportComparePrograms(request, userId.Value, tierName);

            return File(content.Item2,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                content.Item1);
        }

    }
}
