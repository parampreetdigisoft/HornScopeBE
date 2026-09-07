using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.CountryUserDto;
using HornScope.Dtos.dashboard;
using HornScope.IServices;
using HornScope.Models;
using Microsoft.EntityFrameworkCore;

namespace HornScope.Services
{
    public class SignalDashboardService : ISignalDashboardService
    {
        private const int MarketStressTestModeId = 1;
        private const int EarlyWarningModeId = 2;
        private const int ResilienceModeId = 3;

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;

        public SignalDashboardService(ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
        }

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetPeaceStressTestDashboard(int countryID, int userId, UserRole userRole, int year)
            => GetDashboardMode(MarketStressTestModeId, countryID, userId, userRole, "Market stress test dashboard generated successfully.", year);

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetEarlyWarningDashboard(int countryID, int userId, UserRole userRole, int year)
            => GetDashboardMode(EarlyWarningModeId, countryID, userId, userRole, "Early warning dashboard generated successfully.", year);

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetResilienceScorecard(int countryID, int userId, UserRole userRole, int year)
            => GetDashboardMode(ResilienceModeId, countryID, userId, userRole, "Resilience scorecard generated successfully.", year);

        private async Task<ResultResponseDto<DashboardModeResponseDto>> GetDashboardMode(
            int dashboardModeId,
            int countryID,
            int userId,
            UserRole userRole,
            string successMessage,
            int year)
        {
            try
            {
                if (userRole == UserRole.CountryUser && !await ValidateCountryAccess(countryID, userId))
                {
                    return ResultResponseDto<DashboardModeResponseDto>.Failure(new[] { "You don't have access to this country data." });
                }

                var dashboardMode = await _context.DashboardModes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.DashboardModeID == dashboardModeId);

                if (dashboardMode == null)
                {
                    return ResultResponseDto<DashboardModeResponseDto>.Failure(new[] { "Dashboard configuration not found." });
                }

                var mappings = await LoadActiveMappings(dashboardModeId);
                if (!mappings.Any())
                {
                    return ResultResponseDto<DashboardModeResponseDto>.Failure(new[] { "Dashboard KPI mappings not found." });
                }

                var layerIds = mappings.Select(x => x.LayerID).Distinct().ToList();
                var layers = await LoadLayers(layerIds);
                var kpiResults = await LoadLayerResultsByYear(countryID, year, layerIds);
                var amiScores = await LoadCountryAIAMIScore(countryID, userRole, year);
                var amiManualScores = await LoadCountryAMIManualScores(userId, countryID, userRole, year);
                var primaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel == 1));
                var secondaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel != 1));
                var primarySignals = BuildSignalCards(primaryMappings, kpiResults, layers, amiScores.Score);
                var amiLayer = layers.Values.FirstOrDefault(x => x.LayerCode.Equals("AMI", StringComparison.OrdinalIgnoreCase));

                var amiAIInterpretation = amiLayer != null
                    ? MatchInterpretationByValue(amiLayer, amiScores.Score ?? 0m)
                    : null;
                var amiManualInterpretation = amiLayer != null
                    ? MatchInterpretationByValue(amiLayer, amiManualScores.Score ?? 0m)
                    : null;
                var amiAICondition = CommonStaticMethods.GetConditionByScore(amiScores.Score ?? 0m);
                var amiManualCondition = CommonStaticMethods.GetConditionByScore(amiManualScores.Score ?? 0m);

                primarySignals.Insert(0, new SignalCardDto
                {
                    LayerID = 0,
                    LayerCode = "AMI",
                    LayerName = "Country Score",
                    Description = "Represents the country's overall resilience score based on the latest assessment.",
                    AiDescriptor = "Overall assessment of the country's current resilience and performance.",
                    ManualDescriptor = "Overall assessment of the country's current resilience and performance.",
                    StrategicAction = "Review the score category and prioritize actions to strengthen resilience and improve overall performance.",
                    Code = "AMI Score",
                    Name = "Country Score",
                    AIValue = amiScores.Score ?? 0m,
                    AiUpdatedAt = amiScores.AiUpdateAt,
                    ManualValue = amiManualScores.Score ?? -1,
                    ManualUpdatedAt = amiManualScores.ManualUpdateAt,
                    AIInterpretationValue =  amiAIInterpretation?.Condition,
                    ManualInterpretationValue =  amiManualInterpretation?.Condition,
                    AICondition = amiAICondition,
                    ManualCondition = amiManualCondition,
                });

                var secondarySignals = BuildSignalCards(secondaryMappings, kpiResults, layers, amiScores.Score);


                var allSignals = primarySignals.Concat(secondarySignals).ToList();

                return ResultResponseDto<DashboardModeResponseDto>.Success(
                    new DashboardModeResponseDto
                    {
                        CountryID = countryID,
                        DashboardModeID = dashboardModeId,
                        ModeName = dashboardMode.ModeName ?? string.Empty,
                        Description = dashboardMode.Description,
                        Year = year,
                        Ami = amiScores.Score ?? 0m,
                        AICountryScore = amiScores.Score ?? 0m,
                        ManualCountryScore = amiManualScores.Score ?? 0m,
                        ManualValue = amiManualScores.Score ?? 0m,
                        AmiDirectionalMovement = amiScores.Delta,
                        AmiCondition = amiAICondition,
                        ManualCondition = amiManualCondition,
                        AmiDescriptor = amiAIInterpretation?.Descriptor ?? string.Empty,
                        ManualDescriptor = amiManualInterpretation?.Descriptor ?? string.Empty,
                        AmiStrategicAction = amiAIInterpretation?.Descriptor ?? string.Empty,
                        PrimarySignals = primarySignals,
                        SecondarySignals = secondarySignals,
                        Signals = allSignals,
                        //Narratives = narratives
                    },
                    new[] { successMessage });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync($"Error in GetDashboardMode for mode {dashboardModeId}", ex);
                return ResultResponseDto<DashboardModeResponseDto>.Failure(new[] { "There is an error, please try later" });
            }
        }

        private async Task<bool> ValidateCountryAccess(int countryID, int userId)
        {
            return await _context.PublicUserCountryMappings
                .AsNoTracking()
                .AnyAsync(x => x.UserID == userId && x.CountryID == countryID && x.IsActive);
        }

        private async Task<List<DashboardModeKPIMapping>> LoadActiveMappings(int dashboardModeId)
        {
            return await _context.DashboardModeKPIMappings
                .AsNoTracking()
                .Where(x => x.DashboardModeID == dashboardModeId && x.IsActive && !x.IsDeleted)
                .ToListAsync();
        }

        private async Task<Dictionary<int, AnalyticalLayer>> LoadLayers(IEnumerable<int> layerIds)
        {
            var ids = layerIds.Distinct().ToList();
            var layers = await _context.AnalyticalLayers
                .AsNoTracking()
                .Include(x => x.FiveLevelInterpretations)
                .Where(x => !x.IsDeleted && ids.Contains(x.LayerID))
                .ToListAsync();

            return layers.ToDictionary(x => x.LayerID);
        }

        private static List<DashboardModeKPIMapping> OrderMappings(IEnumerable<DashboardModeKPIMapping> mappings)
        {
            return mappings
                .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
                .ToList();
        }

        private async Task<Dictionary<int, LayerScoreResult>> LoadLayerResultsByYear(int countryID, int year, IEnumerable<int> layerIds)
        {
            var ids = layerIds.Distinct().ToList();
            if (!ids.Any())
            {
                return new Dictionary<int, LayerScoreResult>();
            }

            var (startDate, endDate) = GetYearDateRange(year);
            var rows = await _context.AnalyticalLayerResults
                .AsNoTracking()
                .Where(x =>
                    x.CountryID == countryID &&
                    ids.Contains(x.LayerID) &&
                    (
                        (x.AiLastUpdated.HasValue && x.AiLastUpdated.Value >= startDate && x.AiLastUpdated.Value < endDate) ||
                        (x.LastUpdated >= startDate && x.LastUpdated < endDate)
                    ))
                .Select(x => new
                {
                    x.LayerID,
                    x.AiCalValue5,
                    x.AiInterpretationID,
                    x.AiLastUpdated,
                    x.CalValue5,
                    x.InterpretationID,
                    x.LastUpdated
                })
                .ToListAsync();

            return rows
                .GroupBy(x => x.LayerID)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var aiScore = g
                            .Where(x => x.AiLastUpdated.HasValue && x.AiLastUpdated.Value >= startDate && x.AiLastUpdated.Value < endDate)
                            .OrderByDescending(x => x.AiLastUpdated)
                            .FirstOrDefault();

                        var manualScore = g
                            .Where(x => x.LastUpdated >= startDate && x.LastUpdated < endDate)
                            .OrderByDescending(x => x.LastUpdated)
                            .FirstOrDefault();

                        return new LayerScoreResult
                        {
                            AIValue = Math.Round(aiScore?.AiCalValue5 ?? 0m, 2),
                            AIInterpretationId = aiScore?.AiInterpretationID,
                            ManualValue = Math.Round(manualScore?.CalValue5 ?? 0m, 2),
                            ManualInterpretationId = manualScore?.InterpretationID,
                            AiUpdatedAt = aiScore?.AiLastUpdated,
                            ManualUpdatedAt = manualScore?.LastUpdated
                        };
                    });
        }

        private async Task<CountryAMIScores> LoadCountryAIAMIScore(int countryID, UserRole userRole, int year)
        {
            var query = _context.AICountryScores
                .AsNoTracking()
                .Where(x =>
                    x.CountryID == countryID &&
                    (x.Year == year || x.Year == year - 1));

            if (userRole == UserRole.CountryUser)
            {
                query = query.Where(x => x.IsVerified);
            }

            var scores = await query
                .Select(x => new { x.Year, x.AIProgress, x.UpdatedAt })
                .ToListAsync();

            var current = scores.FirstOrDefault(x => x.Year == year)?.AIProgress;
            var previous = scores.FirstOrDefault(x => x.Year == year - 1)?.AIProgress;
            var currentYearScore = scores.FirstOrDefault(x => x.Year == year);

            return new CountryAMIScores
            {
                Score = current ?? 0m,
                Previous = previous,
                Delta = previous.HasValue ? Math.Round((current ?? 0m) - previous.Value, 2) : 0m,
                AiUpdateAt = currentYearScore?.UpdatedAt
            };
        }

        private async Task<CountryAMIScores> LoadCountryAMIManualScores(
            int userID,
            int countryID,
            UserRole userRole,
            int year)
        {
            var progress = await _commonService.GetCountriesProgressAsync(
                userID,
                (int)userRole,
                year,
                countryID);

            var averageScoreProgress = progress != null && progress.Any()
                ? progress.Average(x => x.ScoreProgress)
                : 0m;

            // Get the latest manual update timestamp from assessment responses
            var latestManualUpdate = await _context.AssessmentResponses
                .AsNoTracking()
                .Where(ar => ar.PillarAssessment.Assessment.UserCountryMapping.CountryID == countryID &&
                             ar.PillarAssessment.Assessment.UpdatedAt.Year == year &&
                             ar.PillarAssessment.Assessment.IsActive)
                .OrderByDescending(ar => ar.UpdatedAt)
                .Select(ar => (DateTime?)ar.UpdatedAt)
                .FirstOrDefaultAsync();

            return new CountryAMIScores
            {
                Score = averageScoreProgress,
                ManualUpdateAt = latestManualUpdate
            };
        }

        private List<SignalCardDto> BuildSignalCards(
           IEnumerable<DashboardModeKPIMapping> mappings,
           IReadOnlyDictionary<int, LayerScoreResult> kpiResults,
           IReadOnlyDictionary<int, AnalyticalLayer> layers,
           decimal? AMIOverride = null)
        {
            var cards = new List<SignalCardDto>();
            foreach (var mapping in mappings)
            {
                if (!layers.TryGetValue(mapping.LayerID, out var layer))
                {
                    continue;
                }

                kpiResults.TryGetValue(mapping.LayerID, out var kpiResult);

                var value = kpiResult?.AIValue ?? 0m;
                var manualValue = kpiResult?.ManualValue ?? 0m;

                if (AMIOverride.HasValue &&
                    layer.LayerCode.Equals("AMI", StringComparison.OrdinalIgnoreCase))
                {
                    value = AMIOverride.Value;
                }

                var aiInterpretation = ResolveInterpretation(layer, kpiResult?.AIInterpretationId);
                var manualInterpretation = ResolveInterpretation(layer, kpiResult?.ManualInterpretationId);

                var condition = aiInterpretation?.Condition ?? ResolveConditionByValue(layer, value);
                var manualCondition = manualInterpretation?.Condition ?? ResolveConditionByValue(layer, manualValue);

                var isAlert = IsAlertCondition(condition);

                cards.Add(new SignalCardDto
                {
                    LayerID = layer.LayerID,
                    LayerCode = layer.LayerCode,
                    LayerName = layer.LayerName,
                    Description = CommonStaticMethods.StripHtml(layer.Purpose),
                    Code = layer.LayerCode,
                    Name = layer.LayerName,
                    AIValue = value,
                    AiUpdatedAt = kpiResult?.AiUpdatedAt,
                    ManualUpdatedAt = kpiResult?.ManualUpdatedAt,
                    AICondition = condition,
                    ManualValue = manualValue,
                    ManualCondition = manualCondition ?? string.Empty,
                    AiDescriptor = aiInterpretation?.Descriptor ?? string.Empty,
                    ManualDescriptor = manualInterpretation?.Descriptor ?? string.Empty,
                    AIInterpretationValue = aiInterpretation?.Condition,
                    ManualInterpretationValue = manualInterpretation?.Condition,
                    IsAlert = isAlert,
                    DisplayOrder = mapping.DisplayOrder
                });
            }

            return cards;
        }

        private static FiveLevelInterpretationDto? ResolveInterpretation(AnalyticalLayer? layer, int? interpretationId)
        {
            if (layer == null || !interpretationId.HasValue)
            {
                return null;
            }

            var match = layer.FiveLevelInterpretations
                .FirstOrDefault(x => x.InterpretationID == interpretationId.Value);

            return match == null ? null : ToInterpretationDto(match);
        }

        private static FiveLevelInterpretationDto? MatchInterpretationByValue(AnalyticalLayer layer, decimal value)
        {
            var match = layer.FiveLevelInterpretations.FirstOrDefault(x =>
                (!x.MinRange.HasValue || value >= x.MinRange.Value) &&
                (!x.MaxRange.HasValue || value <= x.MaxRange.Value));
            return match == null ? null : ToInterpretationDto(match);
        }

        private static FiveLevelInterpretationDto ToInterpretationDto(FiveLevelInterpretation interpretation)
        {
            return new FiveLevelInterpretationDto
            {
                InterpretationID = interpretation.InterpretationID,
                LayerID = interpretation.LayerID,
                MinRange = interpretation.MinRange,
                MaxRange = interpretation.MaxRange,
                Condition = interpretation.Condition ?? string.Empty,
                Descriptor = interpretation.Descriptor ?? string.Empty
            };
        }

        private static string ResolveConditionByValue(AnalyticalLayer? layer, decimal value)
        {
            return MatchInterpretationByValue(layer ?? new AnalyticalLayer(), value)?.Condition ?? "";
        }

        private static bool IsAlertCondition(string condition)
        {
            var normalized = condition.ToLowerInvariant();
            return normalized.Contains("critical") ||
                   normalized.Contains("high") ||
                   normalized.Contains("elevated") ||
                   normalized.Contains("watch");
        }

        private static (DateTime StartDate, DateTime EndDate) GetYearDateRange(int year)
        {
            return (new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
        }

        private sealed class CountryAMIScores
        {
            public decimal? Score { get; init; }
            public decimal? Previous { get; init; }
            public decimal Delta { get; init; }
            public DateTime? AiUpdateAt { get; init; }
            public DateTime? ManualUpdateAt { get; init; }
            public string? AIInterpretationCondition { get; init; }
            public string? ManualInterpretationCondition { get; init; }
        }

        private sealed class LayerScoreResult
        {
            public decimal AIValue { get; init; }
            public int? AIInterpretationId { get; init; }
            public decimal ManualValue { get; init; }
            public int? ManualInterpretationId { get; init; }
            public DateTime? AiUpdatedAt { get; init; }
            public DateTime? ManualUpdatedAt { get; init; }
        }
    }
}
