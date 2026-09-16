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
        private const int RelationalDiagnosticsModeId = 1;
        private const int CompositeDiagnosticsModeId = 2;

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;

        public SignalDashboardService(ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
        }

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetRelationalDiagnosticsDashboard(int countryID, int userId, string familyGroup, UserRole userRole, int year)
            => GetDashboardMode(RelationalDiagnosticsModeId, countryID, userId, userRole, "Relational diagnostics dashboard generated successfully.", familyGroup, year);

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetCompositeDiagnosticsDashboard(int countryID, int userId, UserRole userRole, int year)
            => GetDashboardMode(CompositeDiagnosticsModeId, countryID, userId, userRole, "Composite diagnostics dashboard generated successfully.", null, year);

        private async Task<ResultResponseDto<DashboardModeResponseDto>> GetDashboardMode(
            int dashboardModeId,
            int countryID,
            int userId,
            UserRole userRole,
            string successMessage,
            string? familyGroup,
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

                var layers = await LoadLayers(familyGroup);
                if (!layers.Any())
                {
                    return ResultResponseDto<DashboardModeResponseDto>.Failure(new[] { "Dashboard KPI mappings not found." });
                }

                var layerIds = layers.Keys.ToList();
                var kpiResults = await LoadLayerResultsByYear(countryID, year, layerIds);
                var HSScores = await LoadCountryAIHSScore(countryID, userRole, year);
                var HSManualScores = await LoadCountryHSManualScores(userId, countryID, userRole, year);
                var orderedLayers = layers.Values.OrderBy(x => x.LayerID).ToList();
                var allSignals = BuildSignalCards(orderedLayers, kpiResults, HSScores.Score);
                var HSLayer = layers.Values.FirstOrDefault(x => x.LayerCode.Equals("HS", StringComparison.OrdinalIgnoreCase));

                var HSAIInterpretation = HSLayer != null
                    ? MatchInterpretationByValue(HSLayer, HSScores.Score ?? 0m)
                    : null;
                var HSManualInterpretation = HSLayer != null
                    ? MatchInterpretationByValue(HSLayer, HSManualScores.Score ?? 0m)
                    : null;
                var HSAICondition = CommonStaticMethods.GetConditionByScore(HSScores.Score ?? 0m);
                var HSManualCondition = CommonStaticMethods.GetConditionByScore(HSManualScores.Score ?? 0m);

                allSignals.Insert(0, new SignalCardDto
                {
                    LayerID = 0,
                    LayerCode = "HS",
                    LayerName = "Country Score",
                    Description = "Represents the country's overall resilience score based on the latest assessment.",
                    AiDescriptor = "Overall assessment of the country's current resilience and performance.",
                    ManualDescriptor = "Overall assessment of the country's current resilience and performance.",
                    StrategicAction = "Review the score category and prioritize actions to strengthen resilience and improve overall performance.",
                    Code = "HS Score",
                    Name = "Country Score",
                    AIValue = HSScores.Score ?? 0m,
                    AiUpdatedAt = HSScores.AiUpdateAt,
                    ManualValue = HSManualScores.Score ?? -1,
                    ManualUpdatedAt = HSManualScores.ManualUpdateAt,
                    AIInterpretationValue =  HSAIInterpretation?.Condition,
                    ManualInterpretationValue =  HSManualInterpretation?.Condition,
                    AICondition = HSAICondition,
                    ManualCondition = HSManualCondition,
                });

                return ResultResponseDto<DashboardModeResponseDto>.Success(
                    new DashboardModeResponseDto
                    {
                        CountryID = countryID,
                        DashboardModeID = dashboardModeId,
                        ModeName = dashboardMode.ModeName ?? string.Empty,
                        Description = dashboardMode.Description,
                        Year = year,
                        HS = HSScores.Score ?? 0m,
                        AICountryScore = HSScores.Score ?? 0m,
                        ManualCountryScore = HSManualScores.Score ?? 0m,
                        ManualValue = HSManualScores.Score ?? 0m,
                        HSDirectionalMovement = HSScores.Delta,
                        HSCondition = HSAICondition,
                        ManualCondition = HSManualCondition,
                        HSDescriptor = HSAIInterpretation?.Descriptor ?? string.Empty,
                        ManualDescriptor = HSManualInterpretation?.Descriptor ?? string.Empty,
                        HSStrategicAction = HSAIInterpretation?.Descriptor ?? string.Empty,
                        Signals = allSignals,
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

        private async Task<Dictionary<int, AnalyticalLayer>> LoadLayers(string? familyGroup)
        {
            var layers = await _context.AnalyticalLayers
                .AsNoTracking()
                .Include(x => x.FiveLevelInterpretations)
                .Where(x => !x.IsDeleted && x.FamilyGroup == familyGroup)
                .ToListAsync();

            return layers.ToDictionary(x => x.LayerID);
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

        private async Task<CountryHSScores> LoadCountryAIHSScore(int countryID, UserRole userRole, int year)
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

            return new CountryHSScores
            {
                Score = current ?? 0m,
                Previous = previous,
                Delta = previous.HasValue ? Math.Round((current ?? 0m) - previous.Value, 2) : 0m,
                AiUpdateAt = currentYearScore?.UpdatedAt
            };
        }

        private async Task<CountryHSScores> LoadCountryHSManualScores(
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

            return new CountryHSScores
            {
                Score = averageScoreProgress,
                ManualUpdateAt = latestManualUpdate
            };
        }

        private List<SignalCardDto> BuildSignalCards(
           IEnumerable<AnalyticalLayer> layersToBuild,
           IReadOnlyDictionary<int, LayerScoreResult> kpiResults,
           decimal? HSOverride = null)
        {
            var cards = new List<SignalCardDto>();
            foreach (var layer in layersToBuild)
            {
                kpiResults.TryGetValue(layer.LayerID, out var kpiResult);

                var value = kpiResult?.AIValue ?? 0m;
                var manualValue = kpiResult?.ManualValue ?? 0m;

                if (HSOverride.HasValue &&
                    layer.LayerCode.Equals("HS", StringComparison.OrdinalIgnoreCase))
                {
                    value = HSOverride.Value;
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
                    DisplayOrder = layer.LayerID
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

        private sealed class CountryHSScores
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
