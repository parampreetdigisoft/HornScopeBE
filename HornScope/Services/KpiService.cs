using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using HornScope.Common.Implementation;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.ClientDto;
using HornScope.Dtos.kpiDto;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;

using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace HornScope.Services
{
    public class KpiService : IKpiService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IAIAnalyzeService _aiAnalyzeService;
        public KpiService(ApplicationDbContext context, IAppLogger appLogger, IAIAnalyzeService aiAnalyzeService)
        {
            _context = context;
            _appLogger = appLogger;
            _aiAnalyzeService = aiAnalyzeService;
        }
        
        #region GetAnalyticalLayerResults
        public async Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending)
        {
            try
            {
                IQueryable<AnalyticalLayerResult> baseQuery = _context.AnalyticalLayerResults
                    .AsNoTracking()
                    .Include(ar => ar.AnalyticalLayer)
                        .ThenInclude(al => al.FiveLevelInterpretations)
                    .Include(ar => ar.Program);
                    

                if (role == UserRole.ProgramUser )
                {
                    var validPrograms = _context.ClientProgramMappings
                        .Where(x =>
                            x.IsActive &&
                            x.UserID == userId &&
                            (!request.ClimateProgramID.HasValue || x.ClimateProgramID == request.ClimateProgramID))
                        .Select(x => x.ClimateProgramID);

                    var validPillarIds = _context.ClientPillarMappings
                        .Where(x => x.IsActive && x.UserID == userId)
                        .Select(x => x.PillarID);

                    var validLayerIds = _context.AnalyticalLayerPillarMappings
                        .Where(x =>
                            validPillarIds.Contains(x.PillarID) &&
                            (!request.LayerID.HasValue || x.LayerID == request.LayerID))
                        .Select(x => x.LayerID)
                        .Distinct();

                    baseQuery = baseQuery
                        .Where(ar =>
                            validPrograms.Contains(ar.ClimateProgramID) &&
                            validLayerIds.Contains(ar.LayerID));
                }
                else if (role == UserRole.Analyst || role == UserRole.Evaluator)
                {
                    var validPrograms = _context.StaffProgramMappings
                        .Where(x =>
                            !x.IsDeleted &&
                            x.UserID == userId &&
                            (!request.ClimateProgramID.HasValue || x.ClimateProgramID == request.ClimateProgramID))
                        .Select(x => x.ClimateProgramID);
                    baseQuery = baseQuery
                        .Where(ar => validPrograms.Contains(ar.ClimateProgramID)&&
                        (!request.LayerID.HasValue || ar.LayerID == request.LayerID));
                }
                else
                {
                    baseQuery = baseQuery.Where(ar =>
                        (!request.ClimateProgramID.HasValue || ar.ClimateProgramID == request.ClimateProgramID) &&
                        (!request.LayerID.HasValue || ar.LayerID == request.LayerID));
                }
                var response = await baseQuery.Select(Projection).ApplyPaginationAsync(request);

                return response;

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAnalyticalLayers", ex);
                return new PaginationResponse<GetAnalyticalLayerResultDto>();
            }
        }

        private static Expression<Func<AnalyticalLayerResult, GetAnalyticalLayerResultDto>> Projection => ar => new GetAnalyticalLayerResultDto
        {
            LayerResultID = ar.LayerResultID,
            LayerID = ar.LayerID,
            ClimateProgramID = ar.ClimateProgramID,
            InterpretationID = ar.InterpretationID,           
            CalValue5 = ar.CalValue5,
            LastUpdated = ar.LastUpdated,
            AiInterpretationID = ar.AiInterpretationID,
            AiCalValue5 = ar.AiCalValue5,         
            AiLastUpdated = ar.AiLastUpdated,
            LayerCode = ar.AnalyticalLayer.LayerCode,
            LayerName = ar.AnalyticalLayer.LayerName,
            Purpose = ar.AnalyticalLayer.Purpose,            
            CalText5 = ar.AnalyticalLayer.CalText5,
            FiveLevelInterpretations = ar.AnalyticalLayer.FiveLevelInterpretations.OrderByDescending(f => f.MaxRange).ToList(),
            Program = ar.Program
        };

        #endregion
        public async Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi(int userId, UserRole role)
        {
            try
            {
                IQueryable<AnalyticalLayer> query = _context.AnalyticalLayers
                    .Where(x => !x.IsDeleted);

                if (role == UserRole.ProgramUser)
                {
                    query =
                        from layer in _context.AnalyticalLayers
                        join map in _context.AnalyticalLayerPillarMappings
                            on layer.LayerID equals map.LayerID
                        join userMap in _context.ClientPillarMappings
                            on map.PillarID equals userMap.PillarID
                        where !layer.IsDeleted
                              && userMap.IsActive
                              && userMap.UserID == userId
                        select layer;
                }

                var result = await query
                    .AsNoTracking()
                    .Distinct()
                    .ToListAsync();

                return ResultResponseDto<List<AnalyticalLayer>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAllKpi", ex);
                return ResultResponseDto<List<AnalyticalLayer>>.Failure(new List<string> { "An error occurred" });
            }
        }

        public async Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpiPillarMapping(int userId, UserRole role)
        {
            try
            {
                // Get LayerIDs that exist in AnalyticalLayerPillarMappings
                var layerIdsWithMappings = await _context.AnalyticalLayerPillarMappings
                    .Select(m => m.LayerID)
                    .Distinct()
                    .ToListAsync();

                IQueryable<AnalyticalLayer> query = _context.AnalyticalLayers
                    .Where(x => !x.IsDeleted && layerIdsWithMappings.Contains(x.LayerID));

                if (role == UserRole.ProgramUser)
                {
                    query =
                        from layer in _context.AnalyticalLayers
                        join map in _context.AnalyticalLayerPillarMappings
                            on layer.LayerID equals map.LayerID
                        join userMap in _context.ClientPillarMappings
                            on map.PillarID equals userMap.PillarID
                        where !layer.IsDeleted
                              && userMap.IsActive
                              && userMap.UserID == userId
                              && layerIdsWithMappings.Contains(layer.LayerID)
                        select layer;
                }

                var result = await query
                    .AsNoTracking()
                    .Distinct()
                    .ToListAsync();

                return ResultResponseDto<List<AnalyticalLayer>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAllKpiWithMappingStatus", ex);
                return ResultResponseDto<List<AnalyticalLayer>>.Failure(new List<string> { "An error occurred while fetching KPIs with mapping status" });
            }
        }

        public async Task<ResultResponseDto<CompareProgramResponseDto>> ComparePrograms(CompareProgramsRequestDto c, int userId, UserRole role, bool applyPagination = true)
        {
            try
            {
                var validKpiIds = new List<int>();

                if (c.Kpis.Count == 0)
                {
                    var query = _context.AnalyticalLayers
                        .Where(x => !x.IsDeleted)
                        .Select(x => x.LayerID)
                        .OrderBy(x => x);

                    if (applyPagination)
                    {
                        var res = await query.ApplyPaginationAsync(c);
                        validKpiIds = res.Data.ToList();
                    }
                    else
                    {
                        validKpiIds = await query.ToListAsync();
                    }
                }
                else
                {
                    validKpiIds = c.Kpis;
                }

                Expression<Func<ClimateProgram, bool>> expression = role switch
                {
                    UserRole.Admin => x => !x.IsDeleted && c.Programs.Contains(x.ClimateProgramID),
                    UserRole.Analyst => x => !x.IsDeleted && c.Programs.Contains(x.ClimateProgramID),
                    UserRole.Evaluator => x => !x.IsDeleted && c.Programs.Contains(x.ClimateProgramID),
                    _ => x => false
                };

                // Step 2: Get all selected programs (even if no analytical data)
                var selectedPrograms = await _context.ClimatePrograms
                    .Where(expression)
                    .Distinct()
                    .ToListAsync();

                var selectedClimateProgramIDs = selectedPrograms.Select(x => x.ClimateProgramID).ToList();

                if(role == UserRole.Analyst || role == UserRole.Evaluator)
                {
                    var validMappedClimateProgramIDs = await _context.StaffProgramMappings
                       .Where(x => x.UserID == userId && !x.IsDeleted)
                       .Select(x => x.ClimateProgramID)
                       .ToListAsync();

                    // ? Check if all selected programs are valid
                    bool allValid = selectedClimateProgramIDs.All(id => validMappedClimateProgramIDs.Contains(id));

                    if (!allValid)
                    {
                        return ResultResponseDto<CompareProgramResponseDto>.Failure(new List<string> { "No valid programs found." });
                    }
                }

                // Step 3: Fetch analytical layer results for selected programs
                var analyticalResults = await _context.AnalyticalLayerResults
                    .Include(ar => ar.AnalyticalLayer)
                    .Where(x => selectedClimateProgramIDs.Contains(x.ClimateProgramID) 
                    && validKpiIds.Contains(x.LayerID))
                    .Select(ar => new
                    {
                        ar.ClimateProgramID,
                        ar.LayerID,
                        ar.AnalyticalLayer.LayerCode,
                        ar.AnalyticalLayer.LayerName,
                        ar.AnalyticalLayer.Purpose,
                        ar.CalValue5,
                        ar.AiCalValue5
                    })
                    .ToListAsync();

                // Step 4: Get all distinct layers
                var allLayers = analyticalResults
                    .Select(x => new { x.LayerID, x.LayerCode, x.LayerName, x.Purpose })
                    .Distinct()
                    .OrderBy(x => x.LayerName)
                    .ToList();

                // Step 5: Prepare response DTO
                var response = new CompareProgramResponseDto
                {
                    Categories = new List<string>(),
                    Series = new List<ChartSeriesDto>(),
                    TableData = new List<ChartTableRowDto>()
                };

                // Initialize chart series for each Program
                foreach (var program in selectedPrograms)
                {
                    response.Series.Add(new ChartSeriesDto
                    {
                        Name = program.ProgramName,
                        Data = new List<decimal>(),
                        AiData = new List<decimal>()
                    });
                }

                // Add Peer Program Score series
                var peerSeries = new ChartSeriesDto
                {
                    Name = "Peer Program Score",
                    Data = new List<decimal>(),
                    AiData = new List<decimal>()
                };

                // Step 6: Build chart and table data
                foreach (var layer in allLayers)
                {
                    response.Categories.Add(layer.LayerCode);

                    // Map KPI values for each Program (0 if missing)
                    var values = new Dictionary<int, List<decimal>>();

                    foreach (var program in selectedPrograms)
                    {
                        var value = analyticalResults
                            .FirstOrDefault(r => r.ClimateProgramID == program.ClimateProgramID && r.LayerID == layer.LayerID);

                        var evaluatedValue = Math.Round(value?.CalValue5 ?? 0, 2);
                        var aiValue = Math.Round(value?.AiCalValue5 ?? 0, 2);
                        values[program.ClimateProgramID] = new List<decimal> { evaluatedValue, aiValue };

                        // Add to series
                        var programSeries = response.Series.First(s => s.Name == program.ProgramName);
                        programSeries.Data.Add(evaluatedValue);

                        programSeries.AiData.Add(aiValue);
                    }
                    // ? Calculate Peer Program Score (average of all programs for this layer)
                    var peerProgramScore = values.Values.Any() ? Math.Round(values.Values.Select(x => x.First()).Average(), 2) : 0;
                    peerSeries.Data.Add(peerProgramScore);
                    var aiPeerProgramScore = values.Values.Any() ? Math.Round(values.Values.Select(x => x.Last()).Average(), 2) : 0;
                    peerSeries.AiData.Add(aiPeerProgramScore);

                    // Add table data
                    response.TableData.Add(new ChartTableRowDto
                    {
                        LayerID = layer.LayerID,
                        LayerCode = layer.LayerCode,
                        LayerName = layer.LayerName,
                        Purpose = layer.Purpose,
                        ProgramValues = selectedPrograms.Select(p => new ProgramValueDto
                        {
                            ClimateProgramID = p.ClimateProgramID,
                            ProgramName = p.ProgramName,
                            Value = values[p.ClimateProgramID].First(),
                            AiValue = values[p.ClimateProgramID].Last()
                        }).ToList(),
                        PeerProgramScore = peerProgramScore // You can rename property if needed
                    });
                }

                // Append Peer Program Score series
                response.Series.Add(peerSeries);

                return ResultResponseDto<CompareProgramResponseDto>.Success(response);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in ComparePrograms", ex);
                return ResultResponseDto<CompareProgramResponseDto>.Failure(new List<string> { "An error occurred while comparing programs." });
            }
        }

        public async Task<ResultResponseDto<GetMutiplekpiLayerResultsDto>> GetMutiplekpiLayerResults(
            GetMutiplekpiLayerRequestDto request,
            int userId,
            UserRole role,
            TieredAccessPlan userPlan = TieredAccessPlan.Pending)
        {
            try
            {
                if (role == UserRole.ProgramUser)
                {
                    var validClimateProgramIDs = await _context.ClientProgramMappings
                        .Where(x =>
                            x.IsActive &&
                            x.UserID == userId)
                        .Select(x => x.ClimateProgramID)
                        .ToListAsync();

                    bool hasInvalidProgram = request.ClimateProgramIDs
						.Any(ClimateProgramID => !validClimateProgramIDs.Contains(ClimateProgramID));

                    if (hasInvalidProgram)
                    {
                        return ResultResponseDto<GetMutiplekpiLayerResultsDto>
                            .Failure(new List<string> { "You are not authorized to access one or more selected programs." });
                    }
                }


                var query = _context.AnalyticalLayerResults
                    .AsNoTracking()
                    .Where(x =>
                        request.ClimateProgramIDs.Contains(x.ClimateProgramID) &&
                        x.LayerID == request.LayerID);

                var response = await query
                    .GroupBy(x => x.LayerID)
                    .Select(g => new GetMutiplekpiLayerResultsDto
                    {
                        LayerID = g.Key,

                        LayerCode = g.Select(x => x.AnalyticalLayer.LayerCode).FirstOrDefault()?? string.Empty,
                        LayerName = g.Select(x => x.AnalyticalLayer.LayerName).FirstOrDefault() ?? string.Empty,
                        Purpose = g.Select(x => x.AnalyticalLayer.Purpose).FirstOrDefault() ?? string.Empty,                        
                        CalText5 = g.Select(x => x.AnalyticalLayer.CalText5).FirstOrDefault(),

                        FiveLevelInterpretations = g.First().AnalyticalLayer.FiveLevelInterpretations,

                        Programs = g.Select(x => new MutipleProgramskpiLayerResults
                        {
                            ClimateProgramID = x.ClimateProgramID,
                            InterpretationID = x.InterpretationID,                        
                            CalValue5 = x.CalValue5,
                            LastUpdated = x.LastUpdated,
                            AiInterpretationID = x.AiInterpretationID,                         
                            AiCalValue5 = x.AiCalValue5,
                            AiLastUpdated = x.AiLastUpdated,
                            Program = x.Program
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                return ResultResponseDto<GetMutiplekpiLayerResultsDto>
                    .Success(response ?? new GetMutiplekpiLayerResultsDto());
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetMutiplekpiLayerResults", ex);

                return ResultResponseDto<GetMutiplekpiLayerResultsDto>
                    .Failure(new List<string> { "An error occurred." });
            }
        }

        public async Task<Tuple<string, byte[]>> ExportComparePrograms(CompareProgramsRequestDto c, int userId, UserRole role)
        {
            try
            {
                var result = await ComparePrograms(c, userId, role, false);
                var data = result.Result;

                if (data == null || data.TableData == null || !data.TableData.Any())
                {
                    return new Tuple<string, byte[]>("Program_Comparison.xlsx", Array.Empty<byte>());
                }

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Program Comparison");

                    // =========================
                    // ?? DYNAMIC HEADER SETUP
                    // =========================
                    var programs = data.TableData.First().ProgramValues;
                    int totalCols = 2 + (programs.Count * 2);

                    // =========================
                    // ?? REPORT HEADER (TOP)
                    // =========================
                    ws.Range(1, 1, 1, totalCols).Merge().Value = "Key Performance Integrated Report";
                    ws.Range(2, 1, 2, totalCols).Merge().Value = $"Generated On: {DateTime.Now:dd-MMM-yyyy HH:mm}";

                    var titleRange = ws.Range(1, 1, 3, totalCols);
                    titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");
                    titleRange.Style.Font.FontColor = XLColor.White;
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Row(1).Height = 28;
                    ws.Row(2).Height = 22;
                    ws.Row(3).Height = 22;

                    // =========================
                    // ?? MULTI-ROW TABLE HEADER
                    // =========================
                    int row = 5;
                    int col = 1;

                    // KPI Name
                    ws.Range(row, col, row + 1, col).Merge().Value = "KPI Name";
                    col++;

                    // Purpose
                    ws.Range(row, col, row + 1, col).Merge().Value = "Purpose";
                    col++;

                    // Dynamic Programs
                    foreach (var program in programs)
                    {
                        int startCol = col;

                        // Program Name (merged)
                        ws.Range(row, startCol, row, startCol + 1).Merge().Value = program.ProgramName;

                        // Sub headers
                        ws.Cell(row + 1, startCol).Value = "Eval";
                        ws.Cell(row + 1, startCol + 1).Value = "AI";

                        col += 2;
                    }

                    // Style header (both rows)
                    var headerRange = ws.Range(row, 1, row + 1, totalCols);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // =========================
                    // ?? DATA ROWS
                    // =========================
                    row += 2;
                    int startDataRow = row;

                    foreach (var kpi in data.TableData)
                    {
                        col = 1;

                        ws.Cell(row, col++).Value = $"{kpi.LayerName} ({kpi.LayerCode})";

                        var cleanPurpose = StripHtml(kpi.Purpose);
                        var purposeCell = ws.Cell(row, col++);
                        purposeCell.Value = string.IsNullOrEmpty(cleanPurpose) ? "NA" : cleanPurpose;

                        if (!string.IsNullOrEmpty(cleanPurpose))
                        {
                            var comment = purposeCell.GetComment();
                            comment.AddText(cleanPurpose);
                            comment.Visible = false;
                        }

                        foreach (var program in kpi.ProgramValues)
                        {
                            ws.Cell(row, col++).Value = program.Value;
                            ws.Cell(row, col++).Value = program.AiValue;
                        }

                        row++;
                    }

                    int endDataRow = row - 1;

                    // =========================
                    // ?? STYLING
                    // =========================

                    // Column widths
                    ws.Column(1).Width = 30;
                    ws.Column(2).Width = 55;

                    for (int i = 3; i <= totalCols; i++)
                    {
                        ws.Column(i).Width = 18;
                    }

                    // Wrap text
                    ws.Column(2).Style.Alignment.WrapText = true;
                    ws.Column(2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // Center numbers
                    ws.Columns(3, totalCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Auto height
                    ws.Rows().AdjustToContents();

                    // Freeze (after 2 header rows)
                    ws.SheetView.FreezeRows(6);

                    // Borders
                    var dataRange = ws.Range(5, 1, endDataRow, totalCols);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    // Zebra rows
                    for (int i = startDataRow; i <= endDataRow; i++)
                    {
                        if (i % 2 == 0)
                        {
                            ws.Range(i, 1, i, totalCols).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                        }
                    }

                    // Auto filter (second header row)
                    ws.Range(6, 1, 6, totalCols).SetAutoFilter();

                    // =========================
                    // ?? SHEET 2
                    // =========================
                    var ws2 = workbook.Worksheets.Add("KPI Details");

                    int r = 1;

                    ws2.Cell(r, 1).Value = "KPI Name";
                    ws2.Cell(r, 2).Value = "Full Purpose";

                    var header2 = ws2.Range(r, 1, r, 2);
                    header2.Style.Font.Bold = true;
                    header2.Style.Font.FontColor = XLColor.White;
                    header2.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");

                    r++;

                    foreach (var kpi in data.TableData)
                    {
                        ws2.Cell(r, 1).Value = $"{kpi.LayerName} ({kpi.LayerCode})";
                        ws2.Cell(r, 2).Value = StripHtml(kpi.Purpose);
                        r++;
                    }

                    ws2.Column(1).Width = 40;
                    ws2.Column(2).Width = 100;
                    ws2.Column(2).Style.Alignment.WrapText = true;

                    ws2.Rows().AdjustToContents();
                    ws2.SheetView.FreezeRows(1);

                    // =========================
                    // ?? EXPORT
                    // =========================
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return new Tuple<string, byte[]>("Program_Comparison.xlsx", stream.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ExportComparePrograms", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }

        #region SummarizeKpiPerformance
        public async Task<ResultResponseDto<SummarizeKpiResponseDto>> SummarizeKpiPerformance(
            SummarizeKpiRequestDto request, int userId, UserRole role)
        {
            try
            {
                if (role == UserRole.Evaluator)
                {
                    return ResultResponseDto<SummarizeKpiResponseDto>.Failure(
                        new[] { "AI KPI summary is not available for evaluators." });
                }

                if (request.LayerResultID <= 0)
                {
                    return ResultResponseDto<SummarizeKpiResponseDto>.Failure(
                        new[] { "A valid KPI result is required to generate a summary." });
                }

                var layerResult = await _context.AnalyticalLayerResults
                    .AsNoTracking()
                    .Include(ar => ar.AnalyticalLayer)
                        .ThenInclude(al => al.FiveLevelInterpretations)
                    .Include(ar => ar.Program)
                    .FirstOrDefaultAsync(ar => ar.LayerResultID == request.LayerResultID);

                if (layerResult?.AnalyticalLayer == null)
                {
                    return ResultResponseDto<SummarizeKpiResponseDto>.Failure(
                        new[] { "KPI result was not found." });
                }

                var accessError = await ValidateKpiSummaryAccess(layerResult, userId, role);
                if (accessError != null)
                {
                    return ResultResponseDto<SummarizeKpiResponseDto>.Failure(new[] { accessError });
                }

                var interpretations = layerResult.AnalyticalLayer.FiveLevelInterpretations?
                    .OrderByDescending(f => f.MaxRange)
                    .ToList() ?? new List<FiveLevelInterpretation>();

                string? ResolveCondition(int? interpretationId, decimal? score)
                {
                    if (interpretationId.HasValue)
                    {
                        var byId = interpretations.FirstOrDefault(x => x.InterpretationID == interpretationId.Value);
                        if (byId != null) return byId.Condition;
                    }

                    if (score.HasValue)
                    {
                        var byRange = interpretations.FirstOrDefault(x =>
                            score >= (x.MinRange ?? decimal.MinValue) &&
                            score <= (x.MaxRange ?? decimal.MaxValue));
                        return byRange?.Condition;
                    }

                    return null;
                }

                // Role-based score filtering:
                // Admin/Analyst → manual + AI; ProgramUser → AI only; Evaluator blocked above.
                decimal? manualScore = null;
                string? manualCondition = null;
                if (role == UserRole.Admin || role == UserRole.Analyst)
                {
                    manualScore = layerResult.CalValue5;
                    manualCondition = ResolveCondition(layerResult.InterpretationID, layerResult.CalValue5);
                }

                var aiScore = layerResult.AiCalValue5;
                var aiCondition = ResolveCondition(layerResult.AiInterpretationID, layerResult.AiCalValue5);

                var categoryParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(layerResult.AnalyticalLayer.CalText5))
                {
                    categoryParts.Add($"Composite metric: {layerResult.AnalyticalLayer.CalText5}");
                }

                var aiRequest = new KpiSummaryAiRequest
                {
                    ProgramName = layerResult.Program?.ProgramName,
                    LayerName = layerResult.AnalyticalLayer.LayerName ?? string.Empty,
                    LayerCode = layerResult.AnalyticalLayer.LayerCode ?? string.Empty,
                    Purpose = StripHtml(layerResult.AnalyticalLayer.Purpose ?? string.Empty),
                    ManualScore = manualScore,
                    AiScore = aiScore,
                    ManualCondition = manualCondition,
                    AiCondition = aiCondition,
                    CategoryDetails = categoryParts.Count > 0 ? string.Join("; ", categoryParts) : null,
                    InterpretationBands = interpretations.Select(i => new KpiInterpretationBandAiDto
                    {
                        MinRange = i.MinRange,
                        MaxRange = i.MaxRange,
                        Condition = i.Condition,
                        Descriptor = i.Descriptor,
                        StrategicAction = null
                    }).ToList()
                };

                var aiResult = await _aiAnalyzeService.SummarizeKpiPerformance(aiRequest);

                if (aiResult == null)
                {
                    return ResultResponseDto<SummarizeKpiResponseDto>.Failure(
                        new[] { "Unable to reach the AI service. Please try again later." });
                }

                if (!aiResult.Success || aiResult.Result == null || string.IsNullOrWhiteSpace(aiResult.Result.Summary))
                {
                    return ResultResponseDto<SummarizeKpiResponseDto>.Failure(
                        new[] { aiResult.Message ?? "Failed to generate KPI summary. Please try again later." });
                }

                return ResultResponseDto<SummarizeKpiResponseDto>.Success(
                    new SummarizeKpiResponseDto
                    {
                        Summary = aiResult.Result.Summary,
                        ScoreInterpretation = aiResult.Result.ScoreInterpretation,
                        KeyTakeaways = aiResult.Result.KeyTakeaways ?? new List<string>(),
                        Outlook = aiResult.Result.Outlook
                    },
                    new[] { "KPI summary generated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in SummarizeKpiPerformance", ex);
                return ResultResponseDto<SummarizeKpiResponseDto>.Failure(
                    new[] { "An error occurred while generating the KPI summary. Please try again later." });
            }
        }

        private async Task<string?> ValidateKpiSummaryAccess(AnalyticalLayerResult layerResult, int userId, UserRole role)
        {
            if (role == UserRole.Admin)
                return null;

            if (role == UserRole.ProgramUser)
            {
                var hasProgram = await _context.ClientProgramMappings
                    .AnyAsync(x => x.IsActive && x.UserID == userId && x.ClimateProgramID == layerResult.ClimateProgramID);

                if (!hasProgram)
                    return "You don't have access to this program data.";

                var hasLayer = await (
                    from map in _context.AnalyticalLayerPillarMappings
                    join userMap in _context.ClientPillarMappings
                        on map.PillarID equals userMap.PillarID
                    where map.LayerID == layerResult.LayerID
                          && userMap.IsActive
                          && userMap.UserID == userId
                    select map.LayerID
                ).AnyAsync();

                if (!hasLayer)
                    return "You don't have access to this KPI.";

                return null;
            }

            if (role == UserRole.Analyst)
            {
                var hasProgram = await _context.StaffProgramMappings
                    .AnyAsync(x => !x.IsDeleted && x.UserID == userId && x.ClimateProgramID == layerResult.ClimateProgramID);

                if (!hasProgram)
                    return "You don't have access to this program data.";

                return null;
            }

            return "You don't have access to generate an AI KPI summary.";
        }
        #endregion

        private string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty).Trim();
        }

       public async Task<ResultResponseDto<List<AnalyticalLayerPillarMappingDTO>>> GetKPIDetailsByLayerID(int layerID)
       {
            try
            {
                var layer = await _context.AnalyticalLayers.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.LayerID == layerID);
                
                if (layer == null)
                {
                    return ResultResponseDto<List<AnalyticalLayerPillarMappingDTO>>.Failure(new List<string> { "Layer not found" });
                }
                
                // Base mapping query, always scoped to this LayerID
                IQueryable<AnalyticalLayerPillarMapping> mappingQuery = _context.AnalyticalLayerPillarMappings.AsNoTracking().Where(m => m.LayerID == layerID);
                
                var result = await mappingQuery
                    .Join(_context.Pillars, mapping => mapping.PillarID, pillar => pillar.PillarID,
                    (mapping, pillar) => new AnalyticalLayerPillarMappingDTO
                    {
                        AnalyticalLayerPillarMappingID = mapping.AnalyticalLayerPillarMappingID,
                        LayerID = mapping.LayerID,
                        PillarID = mapping.PillarID,
                        Category = mapping.Category,
                        CategoryNumber = mapping.CategoryNumber,
                        PillarName = pillar.PillarName
                    }).ToListAsync();
                
                return ResultResponseDto<List<AnalyticalLayerPillarMappingDTO>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetKPIDetailsByLayerID", ex);
                return ResultResponseDto<List<AnalyticalLayerPillarMappingDTO>>.Failure(new List<string> { "An error occurred" });
            }
        }
    }
}
