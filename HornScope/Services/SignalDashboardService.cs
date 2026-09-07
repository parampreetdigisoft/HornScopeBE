using Microsoft.EntityFrameworkCore;
using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.ClientDto;
using HornScope.Dtos.dashboard;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Services
{
    public class SignalDashboardService : ISignalDashboardService
    {
        private const int AmbitionDeliveryIndexModeId = 1;
        private const int DiplomaticRiskModeId = 2;
        private const int InstitutionalReadinessModeId = 3;

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;

        public SignalDashboardService(ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
        }

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetAmbitionDeliveryIndexDashboard(int climateProgramID, int userId, UserRole userRole)
            => GetDashboardMode(AmbitionDeliveryIndexModeId, climateProgramID, userId, userRole, "Ambition–Delivery Index dashboard generated successfully.");

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetDiplomaticRiskDashboard(int climateProgramID, int userId, UserRole userRole)
            => GetDashboardMode(DiplomaticRiskModeId, climateProgramID, userId, userRole, "Diplomatic Risk & Trust Index dashboard generated successfully.");

        public Task<ResultResponseDto<DashboardModeResponseDto>> GetReadinessScorecardDashboard(int climateProgramID, int userId, UserRole userRole)
            => GetDashboardMode(InstitutionalReadinessModeId, climateProgramID, userId, userRole, "Institutional Readiness Scorecard generated successfully.");

        private async Task<ResultResponseDto<DashboardModeResponseDto>> GetDashboardMode(
            int dashboardModeId,
            int climateProgramID,
            int userId,
            UserRole userRole,
            string successMessage)
        {
            try
            {
                if (userRole == UserRole.ProgramUser && !await ValidateProgramAccess(climateProgramID, userId))
                {
                    return ResultResponseDto<DashboardModeResponseDto>.Failure(new[] { "You don't have access to this program data." });
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

                var accessibleLayerIds = await GetAccessibleLayerIds(userId);
                var kpiResults = await LoadLayerResults(climateProgramID, layerIds);
                var vcpAIScores = await LoadProgramAIVcpScore(climateProgramID, userRole);
                var vcpManualScores = await LoadProgramVcpManualScores(userId, climateProgramID, userRole);
                var primaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel == 1));
                var secondaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel != 1));
                var primarySignals = BuildSignalCards(primaryMappings, kpiResults,layers, accessibleLayerIds, vcpAIScores.Score);

                primarySignals.Insert(0, new SignalCardDto
                {
                    LayerID = 0,
                    LayerCode = "VCP",
                    LayerName = "Program Score",
                    Description = "Represents the program's overall progress score based on the latest assessment.",
                    Descriptor = "Overall assessment of the program's current progress and performance.",
                    Code = "VCP Score",
                    Name = "Program Score",
                    AIValue = vcpAIScores.Score ?? 0m,
                    ManualValue = vcpManualScores.Score ?? -1,
                    AICondition = CommonStaticMethods.GetConditionByScore(vcpAIScores.Score ?? 0m),
                    ManualCondition = CommonStaticMethods.GetConditionByScore(vcpManualScores.Score ?? 0m),
                });

                var secondarySignals = BuildSignalCards(secondaryMappings, kpiResults, layers, accessibleLayerIds, vcpAIScores.Score);
                var vcpLayer = layers.Values.FirstOrDefault(x => x.LayerCode.Equals("VCP", StringComparison.OrdinalIgnoreCase));

                var vcpAIInterpretation = vcpLayer != null 
                    ? MatchInterpretationByValue(vcpLayer, vcpAIScores.Score ?? 0m)
                    : null;
                var vcpManualInterpretation = vcpLayer != null
                    ? MatchInterpretationByValue(vcpLayer, vcpManualScores.Score ?? 0m)
                    : null;
                var vcpAICondition = CommonStaticMethods.GetConditionByScore(vcpAIScores.Score ?? 0m);
                var vcpManualCondition = CommonStaticMethods.GetConditionByScore(vcpManualScores.Score ?? 0m);

                var narratives = primarySignals
                    .Where(x => !x.LayerCode.Equals("VCP", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.IsAlert)
                    .ThenByDescending(x => x.AIValue)
                    .Take(4)
                    .Select(x => new NarrativeDto
                    {
                        Headline = $"{x.LayerName} ({x.AICondition})",
                        Detail = string.IsNullOrWhiteSpace(x.Descriptor) ? x.Narrative : x.Descriptor
                    })
                    .ToList();

                var allSignals = primarySignals.Concat(secondarySignals).ToList();

                return ResultResponseDto<DashboardModeResponseDto>.Success(
                    new DashboardModeResponseDto
                    {
                        ClimateProgramID = climateProgramID,
                        DashboardModeID = dashboardModeId,
                        ModeName = dashboardMode.ModeName ?? string.Empty,
                        Description = dashboardMode.Description,
                        Year = DateTime.Now.Year,
                        Vcp = vcpAIScores.Score ?? 0m,
                        AIProgramScore = vcpAIScores.Score ?? 0m,
                        ManualProgramScore = vcpManualScores.Score ?? 0m,
                        ManualValue = vcpManualScores.Score ?? 0m,
                        VcpDirectionalMovement = 0m,
                        VcpCondition = vcpAICondition,
                        ManualCondition = vcpManualCondition,
                        VcpDescriptor = vcpAIInterpretation?.Descriptor ?? string.Empty,
                        ManualDescriptor = vcpManualInterpretation?.Descriptor ?? string.Empty,
                        PrimarySignals = primarySignals,
                        SecondarySignals = secondarySignals,
                        Signals = allSignals,
                        Narratives = narratives
                    }, 
                    new[] { successMessage });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync($"Error in GetDashboardMode for mode {dashboardModeId}", ex);
                return ResultResponseDto<DashboardModeResponseDto>.Failure(new[] { "There is an error, please try later" });
            }
        }

        private async Task<Dictionary<int, LayerScoreResult>> LoadLayerResults(int climateProgramID, IEnumerable<int> layerIds)
        {
            var ids = layerIds.Distinct().ToList();
            if (!ids.Any())
            {
                return new Dictionary<int, LayerScoreResult>();
            }

            var rows = await _context.AnalyticalLayerResults
                .AsNoTracking()
                .Where(x =>
                    x.ClimateProgramID == climateProgramID &&
                    ids.Contains(x.LayerID) &&
                    x.AiLastUpdated.HasValue)
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
                    var score = g
                        .OrderByDescending(x => x.AiLastUpdated)
                        .First();
                    return new LayerScoreResult
                    {
                        AIValue = Math.Round(score.AiCalValue5 ?? 0m, 2),
                        AIInterpretationId = score.AiInterpretationID,
                        ManualValue = score.CalValue5 ?? 0m,
                        ManualInterpretationId = score.InterpretationID
                    };
                });
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
  

        private async Task<bool> ValidateProgramAccess(int climateProgramID, int userId)
        {
            return await _context.ClientProgramMappings
                .AsNoTracking()
                .AnyAsync(x => x.UserID == userId && x.ClimateProgramID == climateProgramID && x.IsActive);
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
        private async Task<HashSet<int>> GetAccessibleLayerIds(int userId)
        {
            var layerIds = await _context.ClientPillarMappings
                .AsNoTracking()
                .Where(x => x.UserID == userId && x.IsActive)
                .Join(
                    _context.AnalyticalLayerPillarMappings.AsNoTracking(),
                    up => up.PillarID,
                    lp => lp.PillarID,
                    (up, lp) => lp.LayerID)
                .Distinct()
                .ToListAsync();

            return layerIds.ToHashSet();
        }

        private static List<DashboardModeKPIMapping> OrderMappings(IEnumerable<DashboardModeKPIMapping> mappings)
        {
            return mappings
                .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
                .ToList();
        }

        private async Task<ProgramVcpScores> LoadProgramAIVcpScore(int climateProgramID, UserRole userRole)
        {
            var query = _context.AIProgramScores
                .AsNoTracking()
                .Where(x => x.ClimateProgramID == climateProgramID);

            if (userRole == UserRole.ProgramUser)
            {
                query = query.Where(x => x.IsVerified);
            }

            var scores = await query
                .Select(x => new { x.AIProgress })
                .FirstOrDefaultAsync();

            return new ProgramVcpScores
            {
                Score = scores != null ? scores.AIProgress : 0m
            };
        }

        private async Task<ProgramVcpScores> LoadProgramVcpManualScores(
            int userID,
            int climateProgramID,
            UserRole userRole)
        {
            var progress = await _commonService.GetProgramProgressAsync(
                userID,
                (int)userRole,
                climateProgramID);

            var averageScoreProgress = progress != null && progress.Any()
                ? progress.Average(x => x.ScoreProgress)
                : 0m;

            return new ProgramVcpScores
            {
                Score = averageScoreProgress
            };
        }

        private List<SignalCardDto> BuildSignalCards(
           IEnumerable<DashboardModeKPIMapping> mappings,
           IReadOnlyDictionary<int, LayerScoreResult> kpiResults,
           IReadOnlyDictionary<int, AnalyticalLayer> layers,
           IReadOnlySet<int> accessibleLayerIds,
           decimal? vcpOverride = null)
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

                if (vcpOverride.HasValue &&
                    layer.LayerCode.Equals("VCP", StringComparison.OrdinalIgnoreCase))
                {
                    value = vcpOverride.Value;
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
                    AICondition = condition,
                    ManualValue = manualValue,
                    ManualCondition = manualCondition ?? string.Empty,
                    Descriptor = manualInterpretation?.Descriptor ?? string.Empty,
                    Narrative = manualInterpretation?.Descriptor ?? string.Empty,
                    AIInterpretationID = aiInterpretation?.InterpretationID ?? 0,
                    ManualInterpretationID = manualInterpretation?.InterpretationID ?? 0,
                    IsAlert = isAlert,
                    IsAccessible = accessibleLayerIds.Contains(layer.LayerID),
                    Interpretations = MapInterpretations(layer),
                    DisplayOrder = mapping.DisplayOrder

                });
            }

            return cards;
         
        }


        private static List<FiveLevelInterpretationDto> MapInterpretations(AnalyticalLayer layer)
        {
            return layer.FiveLevelInterpretations
                .OrderByDescending(x => x.MaxRange)
                .Select(x => new FiveLevelInterpretationDto
                {
                    InterpretationID = x.InterpretationID,
                    LayerID = x.LayerID,
                    MinRange = x.MinRange,
                    MaxRange = x.MaxRange,
                    Condition = x.Condition ?? string.Empty,
                    Descriptor = x.Descriptor ?? string.Empty
                })
                .ToList();
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

        private sealed class ProgramVcpScores
        {
            public decimal? Score { get; init; }
        }
        private sealed class LayerScoreResult
        {
            public decimal AIValue { get; init; }
            public int? AIInterpretationId { get; init; }
            public decimal ManualValue { get; init; }
            public int? ManualInterpretationId { get; init; }
        }

    }
}
