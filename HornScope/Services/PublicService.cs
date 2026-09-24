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
        public async Task<ResultResponseDto<List<PartnerCountryResponseDto>>> GetAllCountries()
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

        private static readonly object EmergingTrendsDiskLock = new();

        private static readonly JsonSerializerOptions EmergingTrendsJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private static string EmergingTrendsCacheKey(int countryCount) =>
            $"EmergingTrendsAndIssues_{countryCount}";

        private static string EmergingTrendsStaleCacheKey(int countryCount) =>
            $"EmergingTrendsAndIssues_Stale_{countryCount}";

        private TimeSpan EmergingTrendsCacheDuration =>
            TimeSpan.FromHours(_configuration.GetValue("EmergingTrendsCache:CacheExpirationHours", 48));

        private TimeSpan EmergingTrendsStaleCacheDuration =>
            TimeSpan.FromHours(_configuration.GetValue("EmergingTrendsCache:StaleCacheExpirationHours", 48));

        private int ConfiguredEmergingTrendsCountryCount(int fallback = 8) =>
            _configuration.GetValue("EmergingTrendsCache:CountryCount", fallback);

        private string EmergingTrendsDiskPath(int countryCount)
        {
            var root = !string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? _env.WebRootPath
                : Path.Combine(_env.ContentRootPath, "wwwroot");

            return Path.Combine(root, "data", $"emerging_trends_cache_{countryCount}.json");
        }

        private static bool HasUsableEmergingTrends(EmergingTrendsResult? data) =>
            data?.Countries != null && data.Countries.Any(IsUsableCountryCard);

        private static bool IsUsableCountryCard(EmergingTrendCountryCard? card)
        {
            return card != null
                && !string.IsNullOrWhiteSpace(card.Country)
                && !string.IsNullOrWhiteSpace(card.Title)
                && !string.IsNullOrWhiteSpace(card.SourceUrl);
        }

        private static EmergingTrendsResult? FilterToUsableFeed(EmergingTrendsResult? data)
        {
            if (data?.Countries == null)
            {
                return null;
            }

            var countries = data.Countries.Where(IsUsableCountryCard).ToList();
            if (countries.Count == 0)
            {
                return null;
            }

            data.Countries = countries;
            return data;
        }

        private bool TryGetEmergingTrendsFromCache(
            int countryCount,
            out EmergingTrendsResult? result,
            bool allowStale = false)
        {
            result = null;

            if (_cache.TryGetValue(EmergingTrendsCacheKey(countryCount), out EmergingTrendsResult? cached)
                && HasUsableEmergingTrends(cached))
            {
                result = cached;
                return true;
            }

            if (allowStale
                && _cache.TryGetValue(EmergingTrendsStaleCacheKey(countryCount), out EmergingTrendsResult? stale)
                && HasUsableEmergingTrends(stale))
            {
                result = stale;
                return true;
            }

            if (allowStale && TryReadEmergingTrendsFromDisk(countryCount, out var disk) && disk != null)
            {
                result = disk;
                SetEmergingTrendsCache(countryCount, disk, updateStale: true, persistToDisk: false);
                return true;
            }

            return false;
        }

        private bool TryReadEmergingTrendsFromDisk(int countryCount, out EmergingTrendsResult? result)
        {
            result = null;
            var path = EmergingTrendsDiskPath(countryCount);

            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                string json;
                lock (EmergingTrendsDiskLock)
                {
                    json = File.ReadAllText(path);
                }

                var snapshot = JsonSerializer.Deserialize<EmergingTrendsDiskSnapshot>(json, EmergingTrendsJsonOptions);
                var data = FilterToUsableFeed(snapshot?.Data);
                if (data == null)
                {
                    return false;
                }

                result = data;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void WriteEmergingTrendsToDisk(int countryCount, EmergingTrendsResult data)
        {
            try
            {
                var path = EmergingTrendsDiskPath(countryCount);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var snapshot = new EmergingTrendsDiskSnapshot
                {
                    SavedAtUtc = DateTime.UtcNow,
                    Data = data
                };

                var json = JsonSerializer.Serialize(snapshot, EmergingTrendsJsonOptions);
                lock (EmergingTrendsDiskLock)
                {
                    File.WriteAllText(path, json);
                }
            }
            catch (Exception ex)
            {
                _ = _appLogger.LogAsync("Failed to persist emerging trends cache to disk.", ex);
            }
        }

        private void SetEmergingTrendsCache(
            int countryCount,
            EmergingTrendsResult data,
            bool updateStale = true,
            bool persistToDisk = true)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = EmergingTrendsCacheDuration,
                Priority = CacheItemPriority.NeverRemove
            };

            _cache.Set(EmergingTrendsCacheKey(countryCount), data, cacheOptions);

            if (updateStale)
            {
                _cache.Set(
                    EmergingTrendsStaleCacheKey(countryCount),
                    data,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = EmergingTrendsStaleCacheDuration,
                        Priority = CacheItemPriority.NeverRemove
                    }
                );
            }

            if (persistToDisk)
            {
                WriteEmergingTrendsToDisk(countryCount, data);
            }
        }

        private bool PreserveEmergingTrendsCacheOnRefreshFailure(int countryCount)
        {
            if (!TryGetEmergingTrendsFromCache(countryCount, out var stale, allowStale: true)
                || stale == null)
            {
                return false;
            }

            SetEmergingTrendsCache(countryCount, stale, updateStale: false, persistToDisk: false);
            return true;
        }

        public bool HydrateEmergingTrendsCacheFromDisk(int countryCount)
        {
            countryCount = ConfiguredEmergingTrendsCountryCount(countryCount);

            if (!TryReadEmergingTrendsFromDisk(countryCount, out var disk) || disk == null)
            {
                return false;
            }

            SetEmergingTrendsCache(countryCount, disk, updateStale: true, persistToDisk: false);
            return true;
        }

        public async Task<ResultResponseDto<EmergingTrendsResult>> GetEmergingTrendsAndIssues(int countryCount)
        {
            try
            {
                countryCount = ConfiguredEmergingTrendsCountryCount(8);

                if (TryGetEmergingTrendsFromCache(countryCount, out var cachedResult, allowStale: true)
                    && HasUsableEmergingTrends(cachedResult))
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

                countryCount = ConfiguredEmergingTrendsCountryCount(8);
                if (TryGetEmergingTrendsFromCache(countryCount, out var fallback, allowStale: true)
                    && HasUsableEmergingTrends(fallback))
                {
                    return ResultResponseDto<EmergingTrendsResult>.Success(
                        fallback,
                        new List<string>
                        {
                            "Emerging trends and issues fetched successfully from last known data."
                        }
                    );
                }

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
                countryCount = ConfiguredEmergingTrendsCountryCount(countryCount);

                var enriched = await FetchAndEnrichEmergingTrendsAsync(countryCount, cancellationToken);

                if (HasUsableEmergingTrends(enriched) && enriched != null)
                {
                    SetEmergingTrendsCache(countryCount, enriched);
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

            var filtered = FilterToUsableFeed(result.Result);
            if (filtered == null)
            {
                return null;
            }

            var countryCodes = filtered.Countries
                .Select(c => c.CountryCode?.Trim().ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var countries = filtered.Countries
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

            foreach (var trendCountry in filtered.Countries)
            {
                var countryCode = trendCountry.CountryCode?.Trim().ToLower();
                var countryName = trendCountry.Country?.Trim().ToLower();

                var matchedCountry = countryLookup.FirstOrDefault(x =>
                    x.CountryCode == countryCode ||
                    x.CountryName == countryName);

                trendCountry.ImagePath = matchedCountry?.Image ?? "";
            }

            return FilterToUsableFeed(filtered);
        }

        #endregion Emerging Trends

        #region Pillar Overview Cache

        private static readonly object PillarOverviewDiskLock = new();
        private const string PillarOverviewCacheKey = "PillarOverview";

        private string PillarOverviewDiskPath()
        {
            var root = !string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? _env.WebRootPath
                : Path.Combine(_env.ContentRootPath, "wwwroot");
            return Path.Combine(root, "data", "pillar_overview_cache.json");
        }

        private PillarOverviewDiskSnapshot? ReadPillarOverviewSnapshot()
        {
            var path = PillarOverviewDiskPath();
            if (!File.Exists(path))
                return null;

            try
            {
                string json;
                lock (PillarOverviewDiskLock)
                    json = File.ReadAllText(path);

                var snapshot = JsonSerializer.Deserialize<PillarOverviewDiskSnapshot>(json, EmergingTrendsJsonOptions);
                if (snapshot?.Data?.Pillars == null || snapshot.Data.Pillars.Count == 0)
                    return null;
                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        private void SavePillarOverview(PillarOverviewResult data)
        {
            var path = PillarOverviewDiskPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(
                new PillarOverviewDiskSnapshot { SavedAtUtc = DateTime.UtcNow, Data = data },
                EmergingTrendsJsonOptions);
            lock (PillarOverviewDiskLock)
                File.WriteAllText(path, json);

            _cache.Set(PillarOverviewCacheKey, data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(8),
                Priority = CacheItemPriority.NeverRemove
            });
        }

        public DateTime? GetPillarOverviewCacheSavedAtUtc() => ReadPillarOverviewSnapshot()?.SavedAtUtc;

        public bool HydratePillarOverviewCacheFromDisk()
        {
            var snapshot = ReadPillarOverviewSnapshot();
            if (snapshot?.Data == null)
                return false;

            _cache.Set(PillarOverviewCacheKey, snapshot.Data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(8),
                Priority = CacheItemPriority.NeverRemove
            });
            return true;
        }

        public async Task<ResultResponseDto<PillarOverviewResult>> GetPillarOverview()
        {
            try
            {
                if (!_cache.TryGetValue(PillarOverviewCacheKey, out PillarOverviewResult? cached) || cached == null)
                {
                    cached = ReadPillarOverviewSnapshot()?.Data;
                    if (cached != null)
                        HydratePillarOverviewCacheFromDisk();
                }

                if (cached == null)
                {
                    return ResultResponseDto<PillarOverviewResult>.Failure(
                        new[] { "Pillar overview is being updated. Please try again shortly." });
                }

                return ResultResponseDto<PillarOverviewResult>.Success(
                    cached,
                    new List<string> { "Pillar overview fetched successfully from cache." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the GetPillarOverview request.", ex);
                return ResultResponseDto<PillarOverviewResult>.Failure(
                    new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<bool> RefreshPillarOverviewCacheAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var pillars = (await _commonService.GetPillars()).Select(x => new
                {
                    x.PillarID,
                    x.PillarName,
                    x.DisplayOrder,
                    x.ImagePath
                }).ToList();

                var result = await _aIAnalyzeService.GetPillarOverview();
                if (result?.Success == true && result.Result?.Pillars != null && result.Result.Pillars.Count > 0)
                {
                    foreach (var card in result.Result.Pillars)
                    {
                        var matched = pillars.FirstOrDefault(p => p.PillarID == card.PillarId);
                        if (matched == null)
                            continue;

                        card.PillarName = matched.PillarName;
                        card.ImagePath = matched.ImagePath ?? "";
                        card.DisplayOrder = matched.DisplayOrder;
                    }

                    result.Result.Pillars = result.Result.Pillars
                        .OrderBy(p => p.DisplayOrder)
                        .ToList();

                    SavePillarOverview(result.Result);
                    return true;
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while refreshing the pillar overview cache.", ex);
            }

            return ReadPillarOverviewSnapshot() != null;
        }

        #endregion Pillar Overview Cache

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

