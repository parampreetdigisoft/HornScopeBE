using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;
using System.Security.Claims;

namespace HornScope.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ISignalDashboardService _signalDashboardService;

        public DashboardController(ISignalDashboardService signalDashboardService)
        {
            _signalDashboardService = signalDashboardService;
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
        private (int? UserId, UserRole UserRole, IActionResult? Error) ValidateRequest()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return (null, default, Unauthorized("User ID not found in token."));

            var role = GetRoleFromClaims();
            if (role == null || !Enum.TryParse<UserRole>(role, true, out var userRole))
                return (null, default, Unauthorized("You don't have access."));

            if (!HasDashboardAccess(userRole, GetTierFromClaims()))
                return (null, default, Unauthorized("You don't have access."));

            return (userId, userRole, null);
        }
        private bool HasDashboardAccess(UserRole userRole, string? tierName)
        {
            if (userRole == UserRole.Admin || userRole == UserRole.Analyst || userRole == UserRole.Evaluator)
                return true;

            if (userRole != UserRole.ProgramUser)
                return false;

            return tierName == TieredAccessPlan.Standard.ToString() ||
                   tierName == TieredAccessPlan.Premium.ToString() ||
                   tierName == TieredAccessPlan.Basic.ToString();
        }

        [HttpGet("getAmbitionDeliveryIndexDashboard")]
        public async Task<IActionResult> GetAmbitionDeliveryIndexDashboard([FromQuery] int climateProgramID)
        {
            var (userId, userRole, error) = ValidateRequest();
            if (error != null)
                return error;

            var result = await _signalDashboardService.GetAmbitionDeliveryIndexDashboard(climateProgramID, userId!.Value, userRole);
            return Ok(result);
        }

        [HttpGet("getDiplomaticRiskDashboard")]
        public async Task<IActionResult> GetDiplomaticRiskDashboard([FromQuery] int climateProgramID)
        {
            var (userId, userRole, error) = ValidateRequest();
            if (error != null)
                return error;

            var result = await _signalDashboardService.GetDiplomaticRiskDashboard(climateProgramID, userId!.Value, userRole);
            return Ok(result);
        }

        [HttpGet("getReadinessScorecardDashboard")]
        public async Task<IActionResult> GetReadinessScorecardDashboard([FromQuery] int climateProgramID)
        {
            var (userId, userRole, error) = ValidateRequest();
            if (error != null)
                return error;

            var result = await _signalDashboardService.GetReadinessScorecardDashboard(climateProgramID, userId!.Value, userRole);
            return Ok(result);
        }        
    }
}
