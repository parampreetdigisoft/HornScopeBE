using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.chatDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.PublicDto;
using HornScope.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace HornScope.Services
{
    [AllowAnonymous]
    public class PublicService : IPublicService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ICommonService _commonService;
        private readonly IAIAnalyzeService _aIAnalyzeService;
        private readonly IConfiguration _configuration;
        public PublicService(
            ApplicationDbContext context,
            IAppLogger appLogger,
            IWebHostEnvironment env,
            IMemoryCache cache,
            ICommonService commonService,
            IAIAnalyzeService aIAnalyzeService,
            IConfiguration configuration)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _cache = cache;
            _commonService = commonService;
            _aIAnalyzeService = aIAnalyzeService;
            _configuration = configuration;
        }
        public async Task<ResultResponseDto<List<PartnerProgramResponseDto>>> GetAllPrograms()
        {
            try
            {
                var result = await _context.ClimatePrograms.Where(c => c.IsActive && !c.IsDeleted).
                 Select(c => new PartnerProgramResponseDto
                 {
                     ClimateProgramID = c.ClimateProgramID,                     
                     ProgramName = c.ProgramName,
                     Location = c.Location
                 }).OrderBy(x => x.ProgramName).ToListAsync();

                return ResultResponseDto<List<PartnerProgramResponseDto>>.Success(result, new string[] { "Get All Programs successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAllPrograms", ex);
                return ResultResponseDto<List<PartnerProgramResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<PartnerProgramFilterResponse>> GetPartnerProgramsFilterRecord()
        {
            try
            {
                // Fetch all active Climate Programs once
                var activePrograms = await _context.ClimatePrograms
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var res = new PartnerProgramFilterResponse
                {
                    Programs = activePrograms.Select(x=>x.ProgramName)
                        .Distinct()
                        .ToList(),

                    //Programs = activePrograms
                    //    .Select(x => new PartnerProgramDto
                    //    {
                    //        ClimateProgramID = x.ClimateProgramID,
                    //        ProgramName = x.ProgramName
                    //    })
                    //    .ToList(),

                    //Regions = activePrograms        
                    //    .Select(x => x.Region)
                    //    .Where(r => !string.IsNullOrEmpty(r))
                    //    .Distinct()
                    //    .ToList()
                };

                return ResultResponseDto<PartnerProgramFilterResponse>.Success(
                    res,
                    new List<string> { "Get program filter data successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetPartnerProgramsFilterRecord", ex);
                return ResultResponseDto<PartnerProgramFilterResponse>.Failure(
                    new string[] { "Failed to get Partner program filter data" }
                );
            }
        }

        public async Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync()
        {
            try
            {
                var res =  (await _commonService.GetPillars())
                .OrderBy(p => p.DisplayOrder)
                .Select(x => new PillarResponseDto
                {
                    DisplayOrder = x.DisplayOrder,
                    PillarID = x.PillarID,
                    PillarName = x.PillarName,
                    ImagePath = x.ImagePath
                }).ToList();
                return ResultResponseDto<List<PillarResponseDto>>.Success(res, new List<string> { "Get Pillars history successfully" });

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllPillarAsync", ex);
                return ResultResponseDto<List<PillarResponseDto>>.Failure(new string[] { "Failed to get Pillar detail" });
            }
        }
        public async Task<PaginationResponse<PartnerProgramResponseDto>> GetPartnerPrograms(PartnerProgramRequestDto request)
        {
            try
            {
                var programQuery =
                   from c in _context.ClimatePrograms.Where(x => !request.ClimateProgramID.HasValue || x.ClimateProgramID == request.ClimateProgramID)
                   join uc in _context.StaffProgramMappings on c.ClimateProgramID equals uc.ClimateProgramID into ucg
                   from uc in ucg.DefaultIfEmpty()
                   join a in _context.Assessments on uc.StaffProgramMappingID equals a.StaffProgramMappingID into ag
                   from a in ag.DefaultIfEmpty()
                   join pa in _context.PillarAssessments.Where(x=> !request.PillarID.HasValue || x.PillarID == request.PillarID) 
                   on a.AssessmentID equals pa.AssessmentID into pag
                   from pa in pag.DefaultIfEmpty()
                   join r in _context.AssessmentResponses on pa.PillarAssessmentID equals r.PillarAssessmentID into rg
                   from r in rg.DefaultIfEmpty()
                   where !c.IsDeleted && 
                    (uc == null || !uc.IsDeleted) &&
                    (a == null) 
                   group r by new
                   {
                       c.ClimateProgramID,                       
                       c.ProgramName,
                       c.Image,
                       c.Location,
                       EvaluatorCount = _context.StaffProgramMappings
                                           .Count(x => x.ClimateProgramID == c.ClimateProgramID && !x.IsDeleted)
                   }
                   into g
                   select new PartnerProgramResponseDto
                   {
                       ClimateProgramID = g.Key.ClimateProgramID,
                       ProgramName = g.Key.ProgramName,
                       Image = g.Key.Image,
                       Location = g.Key.Location,
                       Score = (decimal)g.Sum(x => (int?)x.Score ?? 0) / (g.Key.EvaluatorCount == 0 ? 1 : g.Key.EvaluatorCount),
                       HighScore = g.Max(x=>(int?)x.Score ?? 0),
                       LowerScore = g.Min(x => (int?)x.Score ?? 0),
                       Progress = ((decimal)g.Sum(x => (int?)x.Score ?? 0)) / ((g.Key.EvaluatorCount == 0 ? 1 : g.Key.EvaluatorCount) * g.Count()),
                   };

                if (!string.IsNullOrWhiteSpace(request.Program))
                {
                    programQuery = programQuery.Where(c => c.ProgramName.Contains(request.Program));
                }

                var response = await programQuery.ApplyPaginationAsync(request);

                return response;

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetPartnerPrograms", ex);
                return new();
            }
        }

        public async Task<ProgramResponse> GetProgramsAndPrograms_WithStaleSupport()
        {
            try
            {
                string jsonFilePath = Path.Combine(_env.WebRootPath, "data\\programs_cache.json");
                if (!File.Exists(jsonFilePath))
                    return new ProgramResponse(); // ? NEVER return null

                var json = await File.ReadAllTextAsync(jsonFilePath);

                var data = JsonSerializer.Deserialize<ProgramResponse>(json);

                return data ?? new ProgramResponse();
            }
            catch (Exception ex)
            {
                // ? Optional: log error
                // _logger.LogError(ex, "Failed to load programs file");

                return new ProgramResponse(); // ? Safe fallback
            }
        }

        public async Task<ResultResponseDto<List<PromotedPillarsResponseDto>>> GetPromotedPrograms()
        {
            const string cacheKey = "GetPromotedPrograms";

            try
            {
                if (_cache.TryGetValue(cacheKey, out List<PromotedPillarsResponseDto> cachedData))
                {
                    return ResultResponseDto<List<PromotedPillarsResponseDto>>.Success(
                        cachedData,
                        new List<string> { "Promoted Programs fetched successfully" });
                }

                var admin = await _context.Users
                    .AsNoTracking()
                    .Where(x => x.Role == Models.UserRole.Admin)
                    .Select(x => new
                    {
                        x.UserID,
                        x.Role
                    })
                    .FirstOrDefaultAsync();

                int userId = admin?.UserID ?? 0;
                int role = (int)(admin?.Role ?? Models.UserRole.Admin);

                var pillarScores = await _commonService.GetProgramProgressAsync(userId, role);

                int[] selectedPillars = { 4, 5, 8, 11, 16, 17, 20, 21 };
                pillarScores = pillarScores.Where(x => selectedPillars.Contains(x.PillarID)).ToList();

                var topProgramsByPillar = pillarScores
                    .GroupBy(x => x.PillarID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(y => y.ScoreProgress)
                              .Take(3)
                              .ToList()
                    );

                var climateProgramIDs = topProgramsByPillar
                    .SelectMany(x => x.Value)
                    .Select(x => x.ClimateProgramID)
                    .Distinct()
                    .ToList();

                var scoreLookup = pillarScores
                    .GroupBy(x => new { x.ClimateProgramID, x.PillarID })
                    .ToDictionary(
                        g => (g.Key.ClimateProgramID, g.Key.PillarID),
                        g => g.First().ScoreProgress
                    );

                var result = await _context.AIPillarScores
                    .AsNoTracking()
                    .Where(x => climateProgramIDs.Contains(x.ClimateProgramID) && 
                                selectedPillars.Contains(x.PillarID) &&
                                x.Program.IsActive &&
                                !x.Program.IsDeleted)
                    .GroupBy(x => new
                    {
                        x.PillarID,
                        x.Pillar.PillarName,
                        x.Pillar.DisplayOrder,
                        x.Pillar.ImagePath
                    })
                    .Select(g => new PromotedPillarsResponseDto
                    {
                        PillarID = g.Key.PillarID,
                        PillarName = g.Key.PillarName,
                        DisplayOrder = g.Key.DisplayOrder,
                        ImagePath = g.Key.ImagePath,

                        Programs = g
                            .OrderByDescending(x => x.AIProgress)
                            .Select(c => new PromotedProgramResponseDto
                            {
                                ClimateProgramID = c.ClimateProgramID,
                                ProgramName = c.Program.ProgramName,
                                Location = c.Program.Location,
                                Image = c.Program.Image,
                                Description = c.EvidenceSummary,
                                ScoreProgress = 0 
                            })
                            .ToList()
                    })
                    .OrderBy(x => x.DisplayOrder)
                    .ToListAsync();

                foreach (var pillar in result)
                {
                    foreach (var program in pillar.Programs)
                    {
                        if (scoreLookup.TryGetValue(
                            (program.ClimateProgramID, pillar.PillarID),
                            out var score))
                        {
                            program.ScoreProgress = Math.Round(score, 2);
                        }
                    }

                    pillar.Programs = pillar.Programs
                        .OrderByDescending(x => x.ScoreProgress)
                        .Take(3)
                        .ToList();
                }

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2),
                    Priority = CacheItemPriority.High
                });

                return ResultResponseDto<List<PromotedPillarsResponseDto>>.Success(
                    result,
                    new List<string> { "Promoted Programs fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetPromotedPrograms", ex);

                return ResultResponseDto<List<PromotedPillarsResponseDto>>.Failure(
                    new[] { "Failed to get promoted Programs" });
            }
        }

        #region Emerging Trends and Issues Cache Management
            
        private static readonly JsonSerializerOptions EmergingTrendsCloneOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static string EmergingTrendsCacheKey(int programCount) =>
            $"EmergingTrendsAndIssues_{programCount}";

        private static string EmergingTrendsStaleCacheKey(int programCount) =>
            $"EmergingTrendsAndIssues_Stale_{programCount}";

        private TimeSpan EmergingTrendsCacheDuration =>
            TimeSpan.FromHours(_configuration.GetValue("EmergingTrendsCache:CacheExpirationHours", 12));

        private TimeSpan EmergingTrendsStaleCacheDuration =>
            TimeSpan.FromHours(_configuration.GetValue("EmergingTrendsCache:StaleCacheExpirationHours", 168));

        private static bool IsEmergingTrendsCacheValid(EmergingTrendsResult? data) =>
            data?.Programs?.Any(c =>
                !string.IsNullOrWhiteSpace(c.ProgramName) &&
                !string.IsNullOrWhiteSpace(c.SourceUrl)) == true;

        private static EmergingTrendsResult CloneEmergingTrendsResult(EmergingTrendsResult data) =>
            JsonSerializer.Deserialize<EmergingTrendsResult>(
                JsonSerializer.Serialize(data, EmergingTrendsCloneOptions),
                EmergingTrendsCloneOptions
            ) ?? new EmergingTrendsResult();

        private bool TryGetEmergingTrendsFromCache(
            int programCount,
            out EmergingTrendsResult? result,
            bool allowStale = false)
        {
            result = null;

            if (_cache.TryGetValue(EmergingTrendsCacheKey(programCount), out EmergingTrendsResult? cached))
            {
                if (IsEmergingTrendsCacheValid(cached))
                {
                    result = CloneEmergingTrendsResult(cached!);
                    return true;
                }

                _cache.Remove(EmergingTrendsCacheKey(programCount));
            }

            if (allowStale
                && _cache.TryGetValue(EmergingTrendsStaleCacheKey(programCount), out EmergingTrendsResult? stale)
                && IsEmergingTrendsCacheValid(stale))
            {
                result = CloneEmergingTrendsResult(stale!);
                return true;
            }

            return false;
        }

        private void SetEmergingTrendsCache(
            int programCount,
            EmergingTrendsResult data,
            bool updateStale = true)
        {
            var primarySnapshot = CloneEmergingTrendsResult(data);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = EmergingTrendsCacheDuration,
                Priority = CacheItemPriority.NeverRemove
            };
            _cache.Set(EmergingTrendsCacheKey(programCount), primarySnapshot, cacheOptions);

            if (updateStale)
            {
                _cache.Set(
                    EmergingTrendsStaleCacheKey(programCount),
                    CloneEmergingTrendsResult(primarySnapshot),
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = EmergingTrendsStaleCacheDuration,
                        Priority = CacheItemPriority.NeverRemove
                    }
                );
            }
        }

        private bool PreserveEmergingTrendsCacheOnRefreshFailure(int programCount)
        {
            if (!TryGetEmergingTrendsFromCache(programCount, out var lastGood, allowStale: true)
                || lastGood == null)
            {
                return false;
            }

            // Re-write both cache entries so TTLs are extended and snapshots stay isolated.
            SetEmergingTrendsCache(programCount, lastGood, updateStale: true);
            return true;
        }

        public async Task<ResultResponseDto<EmergingTrendsResult>> GetEmergingTrendsAndIssues()
        {
            try
            {
                var programCount = _configuration.GetValue("EmergingTrendsCache:ProgramCount", 8);

                if (TryGetEmergingTrendsFromCache(programCount, out var cachedResult, allowStale: true)
                    && cachedResult != null)
                {
                    var fromPrimary = _cache.TryGetValue(
                        EmergingTrendsCacheKey(programCount),
                        out EmergingTrendsResult _);

                    return ResultResponseDto<EmergingTrendsResult>.Success(
                        cachedResult,
                        new List<string>
                        {
                            fromPrimary
                                ? "Emerging trends and issues fetched successfully from cache."
                                : "Emerging trends and issues fetched successfully from last known data."
                        }
                    );
                }

                return ResultResponseDto<EmergingTrendsResult>.Failure(
                    new[]
                    {
                        "Emerging trends feed is being updated. Please try again shortly."
                    }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while processing the GetEmergingTrendsAndIssues request.",
                    ex
                );

                return ResultResponseDto<EmergingTrendsResult>.Failure(
                    new[]
                    {
                        "An error occurred while processing your request. Please try again later."
                    }
                );
            }
        }

        public async Task<bool> RefreshEmergingTrendsCacheAsync(
            int programCount,
            CancellationToken cancellationToken = default)
        {
            try
            {
                programCount = _configuration.GetValue("EmergingTrendsCache:programCount", programCount);

                var enriched = await FetchAndEnrichEmergingTrendsAsync(programCount, cancellationToken);

                if (IsEmergingTrendsCacheValid(enriched))
                {
                    SetEmergingTrendsCache(programCount, enriched!);
                    return true;
                }

                return PreserveEmergingTrendsCacheOnRefreshFailure(programCount);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while refreshing the emerging trends cache.",
                    ex
                );

                return PreserveEmergingTrendsCacheOnRefreshFailure(programCount);
            }
        }

        private async Task<EmergingTrendsResult?> FetchAndEnrichEmergingTrendsAsync(
            int programCount,
            CancellationToken cancellationToken = default)
        {
            var result = await _aIAnalyzeService.GetEmergingTrendsAndIssues(programCount);

            if (result == null || result.Success != true || result.Result == null)
            {
                return null;
            }

            if (!IsEmergingTrendsCacheValid(result.Result))
            {
                return null;
            }

            var programNames = result.Result.Programs
                .Select(c => c.ProgramName?.Trim().ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var locations = result.Result.Programs
                .Select(c => c.Location?.Trim().ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var programLookup = await _context.ClimatePrograms
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    !c.IsDeleted &&
                    (
                        programNames.Contains(c.ProgramName.ToLower()) ||
                        locations.Contains(c.Location.ToLower())
                    ))
                .Select(c => new
                {
                    ProgramName = c.ProgramName.ToLower(),
                    Location = c.Location.ToLower(),
                    c.Image,
                    c.ClimateProgramID
                })
                .ToListAsync(cancellationToken);

            foreach (var trendProgram in result.Result.Programs)
            {
                var programName = trendProgram.ProgramName?.Trim().ToLower();
                var location = trendProgram.Location?.Trim().ToLower();

                var matchedProgram = programLookup.FirstOrDefault(x =>
                    x.ProgramName == programName ||
                    x.Location == location);

                trendProgram.ImagePath = matchedProgram?.Image ?? "";
            }

            return result.Result;
        }

        #endregion Emerging Trends
        public async Task<ResultResponseDto<PillarLiveSignalsResult>> GetPillarLiveSignals()
        {
            const string cacheKey = "PillarLiveSignals";

            try
            {
                if (_cache.TryGetValue(cacheKey, out PillarLiveSignalsResult cachedResult))
                {
                    return ResultResponseDto<PillarLiveSignalsResult>.Success(
                        cachedResult,
                        new List<string>
                        {
                            "Pillar live signals fetched successfully from cache."
                        }
                    );
                }

                var result = await _aIAnalyzeService.GetPillarLiveSignals();

                if (result == null || result.Success != true)
                {
                    return ResultResponseDto<PillarLiveSignalsResult>.Failure(
                        new[]
                        {
                            result?.Message ??
                            "Failed to fetch pillar live signals."
                        }
                    );
                }

                var pillarLookup = await _commonService.GetPillars();

                foreach (var pillarCard in result.Result.Pillars)
                {
                    var matched = pillarLookup.FirstOrDefault(p => p.PillarID == pillarCard.PillarId);
                    pillarCard.PillarName = matched?.PillarName ?? $"Pillar {pillarCard.PillarId}";
                    pillarCard.ImagePath = matched?.ImagePath ?? "";
                }

                result.Result.Pillars = result.Result.Pillars
                    .OrderBy(p =>
                    {
                        var order = pillarLookup.FirstOrDefault(x => x.PillarID == p.PillarId)?.DisplayOrder;
                        return order ?? p.PillarId;
                    })
                    .ToList();

                _cache.Set(
                    cacheKey,
                    result.Result,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
                        SlidingExpiration = TimeSpan.FromHours(10),
                        Priority = CacheItemPriority.High
                    }
                );

                return ResultResponseDto<PillarLiveSignalsResult>.Success(
                    result.Result,
                    new List<string>
                    {
                        "Pillar live signals fetched successfully."
                    }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while processing the GetPillarLiveSignals request.",
                    ex
                );

                return ResultResponseDto<PillarLiveSignalsResult>.Failure(
                    new[]
                    {
                        "An error occurred while processing your request. Please try again later."
                    }
                );
            }
        }

        public async Task<ResultResponseDto<ROSEWPublicDashboardDto>> GetResilienceScorecard()
        {
            int dashboardModeId = 3;
        
            try
            {
                var dashboardMode = await _context.DashboardModes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.DashboardModeID == dashboardModeId);

                if (dashboardMode == null)
                {
                    return ResultResponseDto<ROSEWPublicDashboardDto>.Failure(new[] { "Dashboard configuration not found." });
                }

                var mappings = await _context.DashboardModeKPIMappings
                .AsNoTracking()
                .Where(x => x.DashboardModeID == dashboardModeId && x.IsActive && !x.IsDeleted)
                .ToListAsync(); 

                if (!mappings.Any())
                {
                    return ResultResponseDto<ROSEWPublicDashboardDto>.Failure(new[] { "Dashboard KPI mappings not found." });
                }

                var interpretations = await _context.DashboardInterpretations
                    .AsNoTracking()
                    .Where(x => x.DashboardModeID == dashboardModeId)
                    .ToListAsync();

                var spResults = await _commonService.GetDashboardModeResults(1, 1, dashboardModeId);
                var spResultsByQuestion = spResults
                    .Where(x => x.QuestionID.HasValue)
                    .GroupBy(x => x.QuestionID!.Value)
                    .ToDictionary(g => g.Key, g => g.Average(x => x.AiQuestionScore));


                var questions = new List<ROSEWPublicQuestionDto>();

                foreach (var mapping in mappings)
                {
                    spResultsByQuestion.TryGetValue(mapping.LayerID, out var totalScore);
                     var Score = (decimal)totalScore.GetValueOrDefault();
                    var questionScore = new ROSEWPublicQuestionDto
                    {
                        QuestionDescription = mapping.Description ?? "",
                        Condition = interpretations.FirstOrDefault(i => i.MaxRange >= Score && i.MinRange <= Score)?.Condition ?? "Moderate Stress (Watch)"
                    };
                    questions.Add(questionScore);
                }

                var spResultsByProgram = spResults
                    .Where(x => x.ClimateProgramID.HasValue)
                    .GroupBy(x => x.ClimateProgramID!.Value)
                        .ToDictionary(g => g.Key, g => g.Average(x => x.AiQuestionScore)).OrderByDescending(x=>x.Value).Take(3);


                var dbPrograms = _context.ClimatePrograms.Where(x=> spResultsByProgram.Select(k=>k.Key).Contains(x.ClimateProgramID)).ToList();
                var programs = new List<ROSEWPublicProgramDto>();

                foreach (var program in spResultsByProgram)
                {
                    var Score = (decimal)program    .Value.GetValueOrDefault();
                    var programScore = new ROSEWPublicProgramDto
                    {
                        ProgramName = dbPrograms.FirstOrDefault(x=>x.ClimateProgramID == program.Key)?.ProgramName ?? "",
                        Location = dbPrograms.FirstOrDefault(x=>x.ClimateProgramID == program.Key)?.Location ?? "",
                        UpdatedAt = DateTime.UtcNow,
                        Condition = interpretations.FirstOrDefault(i => i.MaxRange >= Score && i.MinRange <= Score)?.Condition ?? "Moderate Stress (Watch)"
                    };
                    programs.Add(programScore);
                }


                var overAllScore = spResultsByQuestion.Any() ? (decimal?)spResultsByQuestion?.Select(x => x.Value)?.Average() : 0m;

                var response = new ROSEWPublicDashboardDto
                {
                    Score = Math.Round(overAllScore ?? 0m, 2),
                    UpdatedAt = spResults.Max(x => x.AiUpdatedAt),
                    OverallCondition = interpretations.FirstOrDefault(i => i.MaxRange >= (overAllScore ?? 0m) && i.MinRange <= (overAllScore ?? 0m))?.Condition ?? "Moderate Stress (Watch)",
                    Programs = programs,
                    Questions = questions

                };

                return ResultResponseDto<ROSEWPublicDashboardDto>.Success(response, new[] { "Response get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync($"Error in GetDashboardMode for mode {dashboardModeId}", ex);
                return ResultResponseDto<ROSEWPublicDashboardDto>.Failure(new[] { "There is an error, please try later" });
            }
        }
    }
}

public class ProgramResponse
{
    public bool error { get; set; }
    public string msg { get; set; }
    public List<ProgramData> data { get; set; }
}

public class ProgramData
{
    public string Program { get; set; }
    public List<string> Programs { get; set; }
}

