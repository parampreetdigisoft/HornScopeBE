using HornScope.Common.Models;
using HornScope.Common.Models.views;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CountryDto;
using HornScope.Dtos.PillarDto;
using HornScope.Models;

namespace HornScope.Common.Interface
{
    public interface ICommonService
    {
        Task<List<EvaluationCountryProgressResultDto>> GetCountriesProgressAsync(int userId,int role, int year, int countryID = 0);
        Task<List<EvaluationCountryProgressHistoryResultDto>> GetCountriesProgressHistoryAsync(int userId, int role, int fromYear, int toYear);
        Task<List<GetCountriesProgressAdminDto>> GetCountriesProgressForAdmin(int userId, int role, int year);
        Task<List<CountryRankingResultDto>> GetCountriesRankings(int countryId, int year);
        Task<List<CountryPillarRankingResultDto>> GetCountriesPillarRankingAsync(int countryID = 0, int year = 0);
        Task<List<GetPillarDto>> GetPillars();
        void ClearPillarCache();
        Task<List<GetAssessmentResponseDto>> GetUserDetailsAssignedToCountry(int year, int countryID = 0);

        Task<List<GetDashboardModeResult>> GetDashboardModeResults(int userId, int role, int dashboardModeID, int countryID = 0);
        Task<ResultResponseDto<bool>> RevokeCountriesPermission(List<int> countryIds, int userID, int year);
    }
}
