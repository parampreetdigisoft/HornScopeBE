
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HornScope.Common.Models;
using HornScope.Dtos.ClientDto;
using HornScope.Dtos.kpiDto;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class KpiController : ControllerBase
    {
        private readonly IKpiService _kpiService;

        public KpiController(IKpiService kpiService)
        {
            _kpiService = kpiService;
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
        [Route("getAnalyticalLayerResults")]
        public async Task<IActionResult> GetAnalyticalLayerResults([FromQuery] GetAnalyticalLayerRequestDto request)
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

            var tierName = GetTierFromClaims();
            if (tierName == null && userRole == UserRole.ProgramUser)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var userPlan) && userRole == UserRole.ProgramUser)
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _kpiService.GetAnalyticalLayerResults(request, userId.GetValueOrDefault(), userRole, userPlan);
            if (result == null)
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("GetAllKpi")]
        public async Task<IActionResult> GetAllKpi()
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

            var result = await _kpiService.GetAllKpi(userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }

        [HttpPost]
        [Route("ComparePrograms")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> ComparePrograms([FromBody] CompareProgramsRequestDto r)
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
           var result = await _kpiService.ComparePrograms(r, userId.GetValueOrDefault(), userRole, true);
            return Ok(result);
        }

        [HttpGet("ExportComparePrograms")]
        public async Task<IActionResult> ExportComparePrograms( string programs, string? kpis, DateTime updatedAt)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
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

            var content = await _kpiService.ExportComparePrograms(request, userId.Value, userRole);

            return File(content.Item2,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                content.Item1);
        }


        [HttpPost]
        [Route("getMutiplekpiLayerResults")]
        public async Task<IActionResult> GetMutiplekpiLayerResults([FromBody] GetMutiplekpiLayerRequestDto request)
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

            var tierName = GetTierFromClaims();
            if (tierName == null && userRole == UserRole.ProgramUser)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var userPlan) && userRole == UserRole.ProgramUser)
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _kpiService.GetMutiplekpiLayerResults(request, userId.GetValueOrDefault(), userRole, userPlan);
            if (result == null)
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("getKPIDetailsByLayerID/{layerID}")]
        [Authorize]
        public async Task<IActionResult> GetKPIDetailsByLayerID(int layerID)
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

            var result = await _kpiService.GetKPIDetailsByLayerID(layerID);
            if (result == null)
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("GetAllKpiPillarMapping")]
        public async Task<IActionResult> GetAllKpiPillarMapping()
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

            var result = await _kpiService.GetAllKpiPillarMapping(userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }

        [HttpPost]
        [Route("SummarizeKpiPerformance")]
        [Authorize(Roles = "Admin, Analyst, ProgramUser")]
        public async Task<IActionResult> SummarizeKpiPerformance([FromBody] SummarizeKpiRequestDto request)
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

            if (userRole == UserRole.Evaluator)
            {
                return Ok(ResultResponseDto<SummarizeKpiResponseDto>.Failure(
                    new[] { "AI KPI summary is not available for evaluators." }));
            }

            var result = await _kpiService.SummarizeKpiPerformance(request, userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }
    }
}
