using HornScope.Common.Models;
using HornScope.Dtos.dashboard;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface ISignalDashboardService
    {
        Task<ResultResponseDto<DashboardModeResponseDto>> GetRelationalDiagnosticsDashboard(int countryID, int userId, string familyGroup, UserRole userRole, int year);
        Task<ResultResponseDto<DashboardModeResponseDto>> GetCompositeDiagnosticsDashboard(int countryID, int userId, UserRole userRole, int year);
    }
}
