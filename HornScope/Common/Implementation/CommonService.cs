using HornScope.Common.Interface;
using HornScope.Common.Models.settings;
using HornScope.Common.Models.views;
using HornScope.Data;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.ProgramDto;
using HornScope.IServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HornScope.Dtos.PillarDto;

namespace HornScope.Common.Implementation
{
    public class CommonService : ICommonService
    {
        #region constructor
        private readonly IMemoryCache _memoryCache;
        private const string PILLAR_CACHE_KEY = "PILLAR_CACHE";

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly AppSettings _appSettings;
        public CommonService(
            ApplicationDbContext context,
            IAppLogger appLogger,
            IWebHostEnvironment env,
            IOptions<AppSettings> appSettings,
            IMemoryCache memoryCache)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _appSettings = appSettings.Value;
            _memoryCache = memoryCache;
        }

        #endregion
        public static string ProgramScoreSummery(decimal? progress, string? programName = "The program", int pillarCount = 21, int kpiCount = 65)
        {
            var evidenceSummaryStaringLine = $"{programName ?? "The program"} records an overall VCP score of {progress ?? 0}, reflecting performance across {pillarCount} pillars and {kpiCount} KPIs.";

            return evidenceSummaryStaringLine;
        }
        public static string InitailLineOfExecutiveSummery(
            string evidenceSummary,
            decimal? progress,
            string? programName = "The program", int pillarCount = 23, int kpiCount = 37)
        {
            var initialSummery = ProgramScoreSummery(progress, programName, pillarCount, kpiCount); 
            return initialSummery + " " + evidenceSummary;
        }

        public async Task<List<EvaluationProgramProgressResultDto>> GetProgramProgressAsync(int userId, int role, int climateProgramID = 0)
        {
            try
            {
                return await _context.ProgramProgressResults
                 .FromSqlRaw(
                     "EXEC usp_getProgramsProgressByUserId @userID, @role, @climateProgramID",
                     new SqlParameter("@userID", userId),
                     new SqlParameter("@role", role),
                     new SqlParameter("@climateProgramID", climateProgramID)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getProgramsProgressByUserId", ex);
                return new List<EvaluationProgramProgressResultDto>();
            }
        }

        public async Task<List<GetAssessmentResponseDto>> GetUserDetailsAssignedToProgram(int climateProgramID = 0)
        {
            try
            {
                return await _context.GetAssessmentResponseDto
                 .FromSqlRaw(
                     "EXEC usp_GetUsersAssignedToProgram  @climateProgramID",
                     new SqlParameter("@climateProgramID", climateProgramID)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_GetUsersAssignedToProgram", ex);
                return new List<GetAssessmentResponseDto>();
            }
        }

        public async Task<List<ProgramRankingResultDto>> GetProgramRankings(int climateProgramID = 0)
        {
            try
            {
                return await _context.ProgramRankingResults
                 .FromSqlRaw(
                     "EXEC usp_getProgramRanking @climateProgramID",
                     new SqlParameter("@climateProgramID", climateProgramID)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getProgramRanking", ex);
                return new List<ProgramRankingResultDto>();
            }
        }

        public async Task<List<EvaluationProgramProgressHistoryResultDto>> GetProgramProgressHistoryAsync(int userId, int role)
        {
            try
            {
                return await _context.ProgramProgressHistoryResults
                 .FromSqlRaw(
                     "EXEC usp_getProgramProgressByUserIdHistory @userID, @role",
                     new SqlParameter("@userID", userId),
                     new SqlParameter("@role", role)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getProgramProgressByUserIdHistory", ex);
                return new List<EvaluationProgramProgressHistoryResultDto>();
            }
        }

        public async Task<List<GetProgramsProgressAdminDto>> GetProgramProgressForAdmin(int userId, int role)
        {
            try
            {
                return await _context.GetProgramsProgressAdminDto
                 .FromSqlRaw("EXEC usp_getProgramProgress_Admin")
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getProgramsProgress_Admin", ex);
                return new List<GetProgramsProgressAdminDto>();
            }
        }

        public async Task<List<GetPillarDTO>> GetPillars()
        {
            try
            {
                if (_memoryCache.TryGetValue(PILLAR_CACHE_KEY, out List<GetPillarDTO> pillars))
                {
                    return pillars;
                }

                pillars = await _context.Pillars
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .OrderBy(x=>x.DisplayOrder)
                    .Select(x => new GetPillarDTO
                    {
                        PillarID = x.PillarID,
                        PillarName = x.PillarName,
                        Description = x.Description,
                        DisplayOrder = x.DisplayOrder,
                        ImagePath = x.ImagePath,
                        Weight = x.Weight,
                        Reliability = x.Reliability,
                        PillarCode = x.PillarCode,
                        IsActive = x.IsActive,
                        QuestionCount = x.Questions.Where(x => !x.IsDeleted).Count()
                    })
                    .ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1));

                _memoryCache.Set(PILLAR_CACHE_KEY, pillars, cacheOptions);

                return pillars;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetPillars", ex);
                return new List<GetPillarDTO>();
            }
        }
        public void ClearPillarCache()
        {
            _memoryCache.Remove(PILLAR_CACHE_KEY);
        }

        public async Task<List<GetDashboardModeResult>> GetDashboardModeResults(int userId, int role, int dashboardModeID, int climateProgramID = 0)
        {
            try
            {
                var result = await _context.GetDashboardModeResults
                 .FromSqlRaw(
                     "EXEC usp_getDashboardModeResult @userID, @role, @dashboardModeID, @climateProgramID",
                     new SqlParameter("@userID", userId),
                     new SqlParameter("@role", role),
                     new SqlParameter("@dashboardModeID", dashboardModeID),
                     new SqlParameter("@climateProgramID", climateProgramID)
                 )
                 .AsNoTracking()
                 .ToListAsync();

                return result ?? new List<GetDashboardModeResult>();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getDashboardModeResult", ex);
                return new List<GetDashboardModeResult>();
            }
        }
    }
}
