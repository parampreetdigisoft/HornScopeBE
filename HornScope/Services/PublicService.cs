using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.chatDto;
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
        public async Task<ResultResponseDto<List<PartnerCountryResponseDto>>> getAllCountries()
        {
            try
            {
                var result = await _context.Countries.Where(c => c.IsActive && !c.IsDeleted).
                 Select(c => new PartnerCountryResponseDto
                 {
                     CountryID = c.CountryID,                     
                     CountryName = c.CountryName,
                     CountryCode = c.CountryCode,
                     Continent = c.Continent,
                     
                 }).OrderBy(x => x.CountryName).ToListAsync();

                return ResultResponseDto<List<PartnerCountryResponseDto>>.Success(result, new string[] { "get All Countries successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCountries", ex);
                return ResultResponseDto<List<PartnerCountryResponseDto>>.Failure(new string[] { "There is an error please try later" });
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
                return ResultResponseDto<List<PillarResponseDto>>.Success(res, new List<string> { "Get Countries history successfully" });

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllPillarAsync", ex);
                return ResultResponseDto<List<PillarResponseDto>>.Failure(new string[] { "Failed to get Piilar detail" });
            }
        }
        public async Task<CountryCityResponse> GetCountriesAndCountries_WithStaleSupport()
        {
            try
            {
                string jsonFilePath = Path.Combine(_env.WebRootPath, "data\\countries_cache.json");
                if (!File.Exists(jsonFilePath))
                    return new CountryCityResponse(); // ? NEVER return null

                var json = await File.ReadAllTextAsync(jsonFilePath);

                var data = JsonSerializer.Deserialize<CountryCityResponse>(json);

                return data ?? new CountryCityResponse();
            }
            catch (Exception ex)
            {
                // ? Optional: log error
                // _logger.LogError(ex, "Failed to load country-city file");

                return new CountryCityResponse(); // ? Safe fallback
            }
        }

        public async Task<ResultResponseDto<List<PromotedPillarsResponseDto>>> GetPromotedCountries()
        {
            const string cacheKey = "GetPromotedCountries";

            try
            {
                if (_cache.TryGetValue(cacheKey, out List<PromotedPillarsResponseDto> cachedData))
                {
                    return ResultResponseDto<List<PromotedPillarsResponseDto>>.Success(
                        cachedData,
                        new List<string> { "Promoted Countries fetched successfully" });
                }

                int currentYear = DateTime.UtcNow.Year;

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

                var pillarScores = await _commonService.GetCountriesProgressAsync(userId, role, currentYear);

                int[] selectedPillars = { 1, 4, 7, 15, 22 };
                pillarScores = pillarScores.Where(x => selectedPillars.Contains(x.PillarID)).ToList();

                var topCountriesByPillar = pillarScores
                    .GroupBy(x => x.PillarID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(y => y.ScoreProgress)
                              .Take(3)
                              .ToList()
                    );

                var countryIds = topCountriesByPillar
                    .SelectMany(x => x.Value)
                    .Select(x => x.CountryID)
                    .Distinct()
                    .ToList();

                var scoreLookup = pillarScores
                    .GroupBy(x => new { x.CountryID, x.PillarID })
                    .ToDictionary(
                        g => (g.Key.CountryID, g.Key.PillarID),
                        g => g.First().ScoreProgress
                    );

                var result = await _context.AIPillarScores
                    .AsNoTracking()
                    .Where(x =>
                        x.Year == currentYear &&
                        countryIds.Contains(x.CountryID) &&
                        selectedPillars.Contains(x.PillarID) &&
                        x.Country.IsActive &&
                        !x.Country.IsDeleted)
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

                        Countries = g
                            .OrderByDescending(x => x.AIProgress)
                            .Select(c => new PromotedCountryResponseDto
                            {
                                CountryID = c.CountryID,
                                CountryName = c.Country.CountryName,
                                CountryCode = c.Country.CountryCode,
                                Continent = c.Country.Continent,
                                Region = c.Country.Region,
                                Image = c.Country.Image,
                                Description = c.EvidenceSummary,
                                ScoreProgress = 0 
                            })
                            .ToList()
                    })
                    .OrderBy(x => x.DisplayOrder)
                    .ToListAsync();

                foreach (var pillar in result)
                {
                    foreach (var country in pillar.Countries)
                    {
                        if (scoreLookup.TryGetValue(
                            (country.CountryID, pillar.PillarID),
                            out var score))
                        {
                            country.ScoreProgress = Math.Round(score,2);
                        }
                    }

                    pillar.Countries = pillar.Countries
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
                    new List<string> { "Promoted Countries fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetPromotedCountries", ex);

                return ResultResponseDto<List<PromotedPillarsResponseDto>>.Failure(
                    new[] { "Failed to get promoted Countries" });
            }
        }

        #region Emerging Trends and Issues Cache Management
            
        private static readonly JsonSerializerOptions EmergingTrendsCloneOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static string EmergingTrendsCacheKey(int countryCount) =>
            $"EmergingTrendsAndIssues_{countryCount}";

        private static string EmergingTrendsStaleCacheKey(int countryCount) =>
            $"EmergingTrendsAndIssues_Stale_{countryCount}";

        private TimeSpan EmergingTrendsCacheDuration =>
            TimeSpan.FromHours(_configuration.GetValue("EmergingTrendsCache:CacheExpirationHours", 12));

        private TimeSpan EmergingTrendsStaleCacheDuration =>
            TimeSpan.FromHours(_configuration.GetValue("EmergingTrendsCache:StaleCacheExpirationHours", 168));

        private static bool IsEmergingTrendsCacheValid(EmergingTrendsResult? data) =>
            data?.Countries?.Any(c =>
                !string.IsNullOrWhiteSpace(c.Country) &&
                !string.IsNullOrWhiteSpace(c.SourceUrl)) == true;

        private static EmergingTrendsResult CloneEmergingTrendsResult(EmergingTrendsResult data) =>
            JsonSerializer.Deserialize<EmergingTrendsResult>(
                JsonSerializer.Serialize(data, EmergingTrendsCloneOptions),
                EmergingTrendsCloneOptions
            ) ?? new EmergingTrendsResult();

        private bool TryGetEmergingTrendsFromCache(
            int countryCount,
            out EmergingTrendsResult? result,
            bool allowStale = false)
        {
            result = null;

            if (_cache.TryGetValue(EmergingTrendsCacheKey(countryCount), out EmergingTrendsResult? cached))
            {
                if (IsEmergingTrendsCacheValid(cached))
                {
                    result = CloneEmergingTrendsResult(cached!);
                    return true;
                }

                _cache.Remove(EmergingTrendsCacheKey(countryCount));
            }

            if (allowStale
                && _cache.TryGetValue(EmergingTrendsStaleCacheKey(countryCount), out EmergingTrendsResult? stale)
                && IsEmergingTrendsCacheValid(stale))
            {
                result = CloneEmergingTrendsResult(stale!);
                return true;
            }

            return false;
        }

        private void SetEmergingTrendsCache(
            int countryCount,
            EmergingTrendsResult data,
            bool updateStale = true)
        {
            var primarySnapshot = CloneEmergingTrendsResult(data);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = EmergingTrendsCacheDuration,
                Priority = CacheItemPriority.NeverRemove
            };
            _cache.Set(EmergingTrendsCacheKey(countryCount), primarySnapshot, cacheOptions);

            if (updateStale)
            {
                _cache.Set(
                    EmergingTrendsStaleCacheKey(countryCount),
                    CloneEmergingTrendsResult(primarySnapshot),
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = EmergingTrendsStaleCacheDuration,
                        Priority = CacheItemPriority.NeverRemove
                    }
                );
            }
        }

        private bool PreserveEmergingTrendsCacheOnRefreshFailure(int countryCount)
        {
            if (!TryGetEmergingTrendsFromCache(countryCount, out var lastGood, allowStale: true)
                || lastGood == null)
            {
                return false;
            }

            // Re-write both cache entries so TTLs are extended and snapshots stay isolated.
            SetEmergingTrendsCache(countryCount, lastGood, updateStale: true);
            return true;
        }

        public async Task<ResultResponseDto<EmergingTrendsResult>> GetEmergingTrendsAndIssues()
        {
            try
            {
                var countryCount = _configuration.GetValue("EmergingTrendsCache:CountryCount", 8);

                if (TryGetEmergingTrendsFromCache(countryCount, out var cachedResult, allowStale: true)
                    && cachedResult != null)
                {
                    var fromPrimary = _cache.TryGetValue(
                        EmergingTrendsCacheKey(countryCount),
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
            int countryCount,
            CancellationToken cancellationToken = default)
        {
            try
            {
                countryCount = _configuration.GetValue("EmergingTrendsCache:CountryCount", countryCount);

                var enriched = await FetchAndEnrichEmergingTrendsAsync(countryCount, cancellationToken);

                if (IsEmergingTrendsCacheValid(enriched))
                {
                    SetEmergingTrendsCache(countryCount, enriched!);
                    return true;
                }

                return PreserveEmergingTrendsCacheOnRefreshFailure(countryCount);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while refreshing the emerging trends cache.",
                    ex
                );

                return PreserveEmergingTrendsCacheOnRefreshFailure(countryCount);
            }
        }

        private async Task<EmergingTrendsResult?> FetchAndEnrichEmergingTrendsAsync(
            int countryCount,
            CancellationToken cancellationToken = default)
        {
            var result = await _aIAnalyzeService.GetEmergingTrendsAndIssues(countryCount);

            if (result == null || result.Success != true || result.Result == null)
            {
                return null;
            }

            if (!IsEmergingTrendsCacheValid(result.Result))
            {
                return null;
            }

            var countryCodes = result.Result.Countries
                .Select(c => c.CountryCode?.Trim().ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var countries = result.Result.Countries
                .Select(c => c.Country?.Trim().ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var countryLookup = await _context.Countries
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    !c.IsDeleted &&
                    (
                        countryCodes.Contains(c.CountryCode.ToLower()) ||
                        countries.Contains(c.CountryName.ToLower())
                    ))
                .Select(c => new
                {
                    CountryCode = c.CountryCode.ToLower(),
                    CountryName = c.CountryName.ToLower(),
                    c.Image,
                    c.Region,
                    c.Continent,
                    c.CountryID
                })
                .ToListAsync(cancellationToken);

            foreach (var trendCountry in result.Result.Countries)
            {
                var countryCode = trendCountry.CountryCode?.Trim().ToLower();
                var countryName = trendCountry.Country?.Trim().ToLower();

                var matchedCountry = countryLookup.FirstOrDefault(x =>
                    x.CountryCode == countryCode ||
                    x.CountryName == countryName);

                trendCountry.ImagePath = matchedCountry?.Image ?? "";
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
                            "Domain live signals fetched successfully from cache."
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
                    pillarCard.PillarName = matched?.PillarName ?? $"Domain {pillarCard.PillarId}";
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
                        "Domain live signals fetched successfully."
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

        public async Task<ResultResponseDto<OverallHornscopeResponse>> GetOverAllHornscopeScore()
        {
            const string cacheKey = "OverAllHornscopeScore";

            try
            {
                if (_cache.TryGetValue(cacheKey, out OverallHornscopeResponse cachedResult))
                {
                    return ResultResponseDto<OverallHornscopeResponse>.Success(
                        cachedResult,
                        new List<string>
                        {
                            "Overall Hornscope score fetched successfully from cache."
                        }
                    );
                }

                var year = DateTime.UtcNow.Year;

                var result = await _context.AIPillarScores
                    .AsNoTracking()
                    .Where(x => x.Country.IsActive && !x.Country.IsDeleted && x.Year == year)
                    .GroupBy(x => 1)
                    .Select(g => new OverallHornscopeResponse
                    {
                        OverallScore = Math.Round(g.Average(x => x.AIProgress) ?? 0,2)
                    })
                    .FirstOrDefaultAsync() ?? new OverallHornscopeResponse();

                _cache.Set(
                    cacheKey,
                    result,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        Priority = CacheItemPriority.High
                    }
                );

                return ResultResponseDto<OverallHornscopeResponse>.Success(
                    result,
                    new List<string>
                    {
                        "Overall Hornscope score fetched successfully."
                    }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while processing the GetOverAllHornscopeScore request.",
                    ex
                );

                return ResultResponseDto<OverallHornscopeResponse>.Failure(
                    new[]
                    {
                        "An error occurred while processing your request. Please try again later."
                    }
                );
            }
        }
    }
}

public class CountryCityResponse
{
    public bool error { get; set; }
    public string msg { get; set; }
    public List<CountryData> data { get; set; }
}

public class CountryData
{
    public string Country { get; set; }
    public List<string> Countries { get; set; }
}

