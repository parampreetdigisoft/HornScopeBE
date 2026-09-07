using AssessmentPlatform.Dtos.AiDto;
using DocumentFormat.OpenXml.Bibliography;
using HealthIntelligence.Dtos.AiDto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using HornScope.Backgroundjob;
using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.AiDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.ProgramDto;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Services
{
    public class AIComputationService : IAIComputationService
    {
        #region constructor
        
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;
        private readonly Download _download;
        private readonly IAIAnalyzeService _iAIAnalayzeService;        
        private readonly IDocumentGeneratorService _documentGeneratorService;
        //private readonly AppSettings _appSettings;
        private readonly IWebHostEnvironment _env;
        public AIComputationService(ApplicationDbContext context, IAppLogger appLogger,
            ICommonService commonService, 
            Download download, IAIAnalyzeService iAIAnalayzeService
            ,  IDocumentGeneratorService documentGeneratorService, 
             IWebHostEnvironment env)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
            _download = download;
            _iAIAnalayzeService = iAIAnalayzeService;          
            _documentGeneratorService = documentGeneratorService;
            _env = env;
        }
        #endregion

        #region implementation
        public async Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels()
        {
            var r = await _context.AITrustLevels.ToListAsync();

            return ResultResponseDto<List<AITrustLevel>>.Success(r, new[] { "Pillar get successfully" });

        }
        public async Task<PaginationResponse<AiProgramSummeryDto>> GetAIPrograms(AiProgramSummaryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                int pillarCount = (await _commonService.GetPillars()).Count;

                IQueryable<AiProgramSummeryDto> query = await GetProgramAiSummeryDetails(userID, userRole, request.ClimateProgramID);

                var progress = await _commonService.GetProgramProgressAsync(userID, (int)userRole, request.ClimateProgramID ?? 0);
                var programRanks = CalculateProgramRanks(progress, pillarCount);

                var result = await query.ApplyPaginationAsync(request);

                ApplyProgramRanking(result.Data.ToList(), programRanks);

                var ids = result.Data.Select(x => x.ClimateProgramID);
                var programs = progress.Where(x => ids.Contains(x.ClimateProgramID));


                var analyticalLayers = _context.AnalyticalLayers.AsQueryable();

                if (userRole == UserRole.ProgramUser)
                {
                    analyticalLayers =
                        from ar in _context.AnalyticalLayers
                        join alp in _context.AnalyticalLayerPillarMappings
                            on ar.LayerID equals alp.LayerID
                        join cup in _context.ClientPillarMappings
                            on alp.PillarID equals cup.PillarID
                        join puc in _context.ClientProgramMappings
                            on cup.UserID equals puc.UserID
                        where cup.IsActive
                              && puc.IsActive
                              && cup.UserID == userID
                              && puc.UserID == userID
                        select ar;
                }

                var totalValidKpis = await analyticalLayers.Distinct().CountAsync();

                foreach (var c in result.Data)
                {                 
                    c.ProgramScoreSummery = CommonService.ProgramScoreSummery(c.AIProgress, c.ProgramName, pillarCount, totalValidKpis);
                }

                if (userRole != UserRole.ProgramUser)
                {
                    var counts = await _context.Pillars.Where(x=>!x.IsDeleted && x.IsActive)
                        .Select(p => p.Questions.Count(x=>!x.IsDeleted)).ToListAsync();

                    var totalQuestions = counts.Sum();

                    var answeredQuestions = await _context.AIEstimatedQuestionScores
                        .Where(x => ids.Contains(x.ClimateProgramID))
                        .GroupBy(x => x.ClimateProgramID)
                        .Select(g => new
                        {
                            ClimateProgramID = g.Key,
                            CompletionRate = totalQuestions == 0
                                ? 0
                                : g.Count() * 100.0M / totalQuestions
                        })
                        .ToListAsync();

                    foreach (var c in result.Data)
                    {
                        var pillars = programs.Where(x => x.ClimateProgramID == c.ClimateProgramID);
                        var programScore = Math.Round(pillars.Sum(x => x.ScoreProgress) / (decimal)pillarCount, 2);
                        c.EvaluatorScore = programScore;
                        c.Discrepancy = Math.Abs(programScore - (c.AIProgress ?? 0));
                        c.AICompletionRate = answeredQuestions.FirstOrDefault(x=>x.ClimateProgramID == c.ClimateProgramID)?.CompletionRate;                         
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAIPrograms", ex);
                return new PaginationResponse<AiProgramSummeryDto>();
            }
        }
        public async Task<IQueryable<AiProgramSummeryDto>> GetProgramAiSummeryDetails(int userID, UserRole userRole, int? climateProgramID)
        {
            IQueryable<AIProgramScore> baseQuery = _context.AIProgramScores;

            List<int> allowedClimateProgramIDs = new();
            if (userRole == UserRole.Analyst)
            {
                // Allowed Program IDs
                allowedClimateProgramIDs = await _context.StaffProgramMappings
                            .Where(x => !x.IsDeleted && x.UserID == userID && (!climateProgramID.HasValue || x.ClimateProgramID == climateProgramID.Value))
                            .Select(x => x.ClimateProgramID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedClimateProgramIDs.Contains(x.ClimateProgramID));
            }
            else if (userRole == UserRole.Evaluator)
            {
                // Allowed Program IDs
                allowedClimateProgramIDs = await _context.AIEvaluatorProgramMappings
                            .Where(x => x.IsActive && x.UserID == userID && (!climateProgramID.HasValue || x.ClimateProgramID == climateProgramID.Value))
                            .Select(x => x.ClimateProgramID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedClimateProgramIDs.Contains(x.ClimateProgramID));
            }
            else if (userRole == UserRole.ProgramUser)
            {
                allowedClimateProgramIDs = await _context.ClientProgramMappings
                            .Where(x => x.IsActive && x.UserID == userID && (!climateProgramID.HasValue || x.ClimateProgramID == climateProgramID.Value))
                            .Select(x => x.ClimateProgramID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedClimateProgramIDs.Contains(x.ClimateProgramID) && x.IsVerified);
            }
            else
            {
                // Admin
                if (climateProgramID.HasValue)
                {
                    baseQuery = baseQuery.Where(x => x.ClimateProgramID == climateProgramID.Value);
                    allowedClimateProgramIDs = new() { climateProgramID.Value };
                }
            }
            var commentQuery = _context.AIEvaluatorProgramMappings
                .Where(x =>
                    (
                        userRole == UserRole.Admin ||
                        (userRole == UserRole.Analyst && x.AssignBy == userID) ||
                        (userRole == UserRole.Evaluator && x.UserID == userID)
                    )
                )
                .GroupBy(x => x.ClimateProgramID)
                .Select(g => new
                {
                    ClimateProgramID = g.Key,
                    Comment = g
                        .OrderByDescending(x => x.UpdatedAt)
                        .Select(x => x.Comment)
                        .FirstOrDefault()
                });

            var query =
                from c in _context.ClimatePrograms
                where !c.IsDeleted && (allowedClimateProgramIDs.Contains(c.ClimateProgramID) || (userRole == UserRole.Admin && !climateProgramID.HasValue))
                join score in baseQuery
                    on c.ClimateProgramID equals score.ClimateProgramID
                    into scoreJoin
                from score in scoreJoin.DefaultIfEmpty()   // LEFT JOIN score

                join cmt in commentQuery
                    on c.ClimateProgramID equals cmt.ClimateProgramID
                    into cmtJoin
                from cmt in cmtJoin.DefaultIfEmpty()       // LEFT JOIN comment

                select new AiProgramSummeryDto
                {
                    ClimateProgramID = c.ClimateProgramID,
                    ProgramName = c.ProgramName ?? string.Empty,
                    //Program = c.Program ?? string.Empty,
                    Location = c.Location ?? string.Empty,
                    Image = c.Image ?? string.Empty,
                    Year = c.Year,
                    AIProgress = score != null ? score.AIProgress : null,
                    EvaluatorScore = score != null ? score.EvaluatorScore : null,
                    Discrepancy = score != null ? score.Discrepancy : null,

                    ConfidenceLevel = score != null ? score.ConfidenceLevel ?? string.Empty : string.Empty,
                    EvidenceSummary = score != null ? score.EvidenceSummary ?? string.Empty : string.Empty,

                    StructuralEvidence = score != null ? score.StructuralEvidence : null,
                    OperationalEvidence = score != null ? score.OperationalEvidence : null,
                    OutcomeEvidence = score != null ? score.OutcomeEvidence : null,
                    PerceptionEvidence = score != null ? score.PerceptionEvidence : null,

                    TemporalScope = score != null ? score.TemporalScope : null,
                    DistortionScreening = score != null ? score.DistortionScreening : null,

                    GeopoliticalShock = score != null ? score.GeopoliticalShock : null,
                    FinanceShock = score != null ? score.FinanceShock : null,
                    LegitimacyShock = score != null ? score.LegitimacyShock : null,

                    OverallStressResilience = score != null ? score.OverallStressResilience : null,
                    StressScoreAdjustment = score != null ? score.StressScoreAdjustment : null,
                    InclusionEquityAdjustment = score != null ? score.InclusionEquityAdjustment : null,
                    OpacityRisk = score != null ? score.OpacityRisk : null,
                    NonCompensationNote = score != null ? score.NonCompensationNote : null,

                    CrossPillarPatterns = score != null ? score.CrossPillarPatterns : null,
                    RelationalIntegrity = score != null ? score.RelationalIntegrity : null,
                    InstitutionalCapacity = score != null ? score.InstitutionalCapacity : null,
                    EquityAssessment = score != null ? score.EquityAssessment : null,
                    GovernanceTrajectory = score != null ? score.GovernanceTrajectory : null,

                    StrategicRecommendation = score != null ? score.StrategicRecommendation : null,
                    AssessmentValueNote = score != null ? score.AssessmentValueNote : null,
                    PrimarySource = score != null ? score.PrimarySource : null,

                    KeyFindings = score != null ? score.KeyFindings : null,
                    Recommendations = score != null ? score.Recommendations : null,

                    UpdatedAt = score != null ? score.UpdatedAt : default(DateTime),

                    IsVerified = score != null && score.IsVerified
                };
            return query;
        }
    
        public async Task<ResultResponseDto<AiProgramPillarResponseDto>> GetAIProgramPillars(int climateProgramID, int userID, UserRole userRole)
        {
            try
            {               
                int pillarCount = (await _commonService.GetPillars()).Count;
                var res = await _context.AIPillarScores
                    .Where(x => x.ClimateProgramID == climateProgramID)
                    .Include(x=>x.Program)
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds = new();
                if (userRole == UserRole.ProgramUser)
                {
                    pillarIds = await _context.ClientPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID)
                                .Distinct()
                                .ToListAsync();
                }
                var pillars = (await _commonService.GetPillars()).Select(x => new
                {
                    PillarID = x.PillarID,
                    PillarName = x.PillarName,
                    DisplayOrder = x.DisplayOrder,
                    ImagePath = x.ImagePath,
                    TotalQuestions = x.QuestionCount
                }).ToList();

                var result = pillars
                .GroupJoin(
                    res,
                    p => p.PillarID,
                    s => s.PillarID,
                    (pillar, scores) => new { pillar, score = scores.FirstOrDefault() }
                )
                .Select(x =>
                {
                    var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.pillar.PillarID);

                    var r = new AiProgramPillarResponse
                    {
                        PillarScoreID = x.score?.PillarScoreID ?? 0,
                        ClimateProgramID = x.score?.ClimateProgramID ?? climateProgramID,
                        ProgramName = x.score?.Program?.ProgramName ?? "",                       
                        PillarID = x.pillar.PillarID,
                        PillarName = x.pillar.PillarName,
                        DisplayOrder = x.pillar.DisplayOrder,
                        ImagePath = x.pillar.ImagePath,
                        IsAccess = isAccess
                    };

                    if (isAccess && x.score != null)
                    {
                        r.AIDataYear = x.score.Year;
                        r.AIScore = x.score.AIScore;
                        r.AIProgress = x.score.AIProgress;
                        r.EvaluatorScore = x.score.EvaluatorScore;
                        r.Discrepancy = x.score.Discrepancy;
                        r.ConfidenceLevel = x.score.ConfidenceLevel;
                        r.EvidenceSummary = x.score.EvidenceSummary;
                        r.StructuralEvidence = x.score.StructuralEvidence;
                        r.OperationalEvidence = x.score.OperationalEvidence;
                        r.OutcomeEvidence = x.score.OutcomeEvidence;
                        r.PerceptionEvidence = x.score.PerceptionEvidence;
                        r.TemporalScope = x.score.TemporalScope;
                        r.DistortionScreening = x.score.DistortionScreening;
                        r.RelationalIntegrity = x.score.RelationalIntegrity;
                        r.StressGeopoliticalShock = x.score.StressGeopoliticalShock;
                        r.StressFinanceShock = x.score.StressFinanceShock;
                        r.StressLegitimacyShock = x.score.StressLegitimacyShock;
                        r.StressOverallResilience = x.score.StressOverallResilience;
                        r.StressScoreAdjustment = x.score.StressScoreAdjustment;
                        r.InclusionEquityAdjustment = x.score.InclusionEquityAdjustment;
                        r.OpacityRisk = x.score.OpacityRisk;
                        r.NonCompensationNote = x.score.NonCompensationNote;
                        r.InclusionAccessNote = x.score.InclusionAccessNote;
                        r.InstitutionalAssessment = x.score.InstitutionalAssessment;
                        r.DataGapAnalysis = x.score.DataGapAnalysis;
                        r.RedFlag = x.score.RedFlag;
                        r.DataSourceCitations = x.score.DataSourceCitations;
                        r.UpdatedAt = x.score.UpdatedAt;
                    }
                    return r;
                })
                .OrderBy(x => !x.IsAccess)
                .ThenBy(x => x.DisplayOrder)
                .ToList();


                var progress = await _commonService.GetProgramProgressAsync(userID, (int)userRole, climateProgramID);

                var programs = progress.Where(x => x.ClimateProgramID== climateProgramID);

                var answeredQuestions = await _context.AIEstimatedQuestionScores
               .Where(x => x.ClimateProgramID == climateProgramID)
               .GroupBy(x => x.PillarID)
               .Select(g => new
               {
                   PillarID = g.Key,
                   AnsweredQuestions = g.Count() 
               })
               .ToListAsync();

                foreach (var c in result)
                {
                    var totalQuestions = pillars.FirstOrDefault(x => x.PillarID == c.PillarID)?.TotalQuestions ?? 1;
                    var answeredQuestion = answeredQuestions.FirstOrDefault(x => x.PillarID == c.PillarID)?.AnsweredQuestions ?? 0;
                    var pillarScore = programs
                        .Where(x => x.PillarID == c.PillarID)
                        .Select(x => x.ScoreProgress)
                        .DefaultIfEmpty(0)
                        .Sum();
                    c.EvaluatorScore = pillarScore;
                    c.Discrepancy = Math.Abs(pillarScore - (c.AIProgress ?? 0));
                    c.AICompletionRate = totalQuestions == 0 ? 0 :(answeredQuestion * 100.0M) / totalQuestions;
                }

                var finalResutl = new AiProgramPillarResponseDto
                {

                    Pillars = result
                };

                var resposne = ResultResponseDto<AiProgramPillarResponseDto>.Success(finalResutl, new[] { "Pillar get successfully", });

                return resposne;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAICityPillars", ex);
                return ResultResponseDto<AiProgramPillarResponseDto>.Failure(new[] { "Error in getting pillar details", });
            }
        }
        public async Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiProgramPillarSummeryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                if (userRole == UserRole.ProgramUser && request.ClimateProgramID != null && request.PillarID != null)
                {
                    var isPillarAccess = _context.ClientPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID).Contains(request.PillarID.Value);

                    var isProgramAccess = _context.ClientProgramMappings
                               .Where(x => x.IsActive && x.UserID == userID)
                               .Select(x => x.ClimateProgramID).Contains(request.ClimateProgramID.Value);
                    if (!(isProgramAccess && isPillarAccess))
                    {
                        return new PaginationResponse<AIEstimatedQuestionScoreDto>();
                    }
                }

                var res =
                    from q in _context.Questions.Where(x=>x.PillarID== request.PillarID)
                    join s in _context.AIEstimatedQuestionScores
                        .Where(x =>
                            x.ClimateProgramID == request.ClimateProgramID &&
                            x.PillarID == request.PillarID)
                    on q.QuestionID equals s.QuestionID into qs
                    from x in qs.DefaultIfEmpty() // LEFT JOIN
                    select new AIEstimatedQuestionScoreDto
                    {
                        ClimateProgramID = x == null ? request.ClimateProgramID ?? 0 : x.ClimateProgramID,
                        PillarID = x == null ? request.PillarID ?? 0 : x.PillarID,
                        QuestionID = q.QuestionID,
                        Year = x == null ? 0 : x.Year,
                        AIScore = x == null ? null : x.AIScore,
                        AIProgress = x == null ? null : x.AIProgress,
                        EvaluatorScore = x == null ? null : x.EvaluatorScore,
                        Discrepancy = x == null ? null : x.Discrepancy,
                        ConfidenceLevel = x == null ? string.Empty : x.ConfidenceLevel,
                        SourcesConsulted = x == null ? null : x.SourcesConsulted,  // ? renamed
                        EvidenceSummary = x == null ? string.Empty : x.EvidenceSummary,
                        // Evidence Dimensions
                        StructuralEvidence = x == null ? string.Empty : x.StructuralEvidence,
                        OperationalEvidence = x == null ? string.Empty : x.OperationalEvidence,
                        OutcomeEvidence = x == null ? string.Empty : x.OutcomeEvidence,
                        PerceptionEvidence = x == null ? string.Empty : x.PerceptionEvidence,
                        TemporalScope = x == null ? string.Empty : x.TemporalScope,
                        DistortionScreening = x == null ? string.Empty : x.DistortionScreening,
                        RelationalDependencies = x == null ? string.Empty : x.RelationalDependencies,
                        // Stress Tests
                        StressGeopoliticalShock = x == null ? string.Empty : x.StressGeopoliticalShock,
                        StressFinanceShock = x == null ? string.Empty : x.StressFinanceShock,
                        StressLegitimacyShock = x == null ? string.Empty : x.StressLegitimacyShock,
                        StressOverallResilienceShock = x == null ? string.Empty : x.StressOverallResilienceShock,
                        InclusionEquityAdjustment = x == null ? string.Empty : x.InclusionEquityAdjustment,   // ? renamed
                        OpacityRisk = x == null ? string.Empty : x.OpacityRisk,
                        RedFlag = x == null ? string.Empty : x.RedFlag,   // ? renamed
                        // Source Metadata
                        SourceType = x == null ? string.Empty : x.SourceType,
                        SourceName = x == null ? string.Empty : x.SourceName,
                        SourceURL = x == null ? string.Empty : x.SourceURL,
                        SourceDataExtract = x == null ? string.Empty : x.SourceDataExtract,
                        SourceDataYear = x == null ? null : x.SourceDataYear,
                        SourceHierarchyLevel = x == null ? null : x.SourceHierarchyLevel,   // ? renamed
                        UpdatedAt = x == null ? null : x.UpdatedAt,

                        QuestionText = q.QuestionText ?? string.Empty
                    };

                var r = await res.ApplyPaginationAsync(request);

                return r;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAIPillarsQuestion", ex);
                return new PaginationResponse<AIEstimatedQuestionScoreDto>();
            }
        }        
        private async Task<List<PeerProgramHistoryReportDto>> GetPeerPrograms(int userID, UserRole role, int climateProgramID, bool isAiScore = true)
        {
            var peerPrograms = new List<PeerProgramHistoryReportDto>();
            int pillarCount = (await _commonService.GetPillars()).Count;
            var peersClimateProgramIDs = await _context.ClimatePrograms
                   .Where(x => x.ClimateProgramID == climateProgramID && x.IsActive && !x.IsDeleted)
                   .SelectMany(x => x.ProgramPeers)
                   .Where(x => x.IsActive && !x.IsDeleted)
                   .Select(x => x.PeerProgramID)
                   .ToListAsync();
            if (peersClimateProgramIDs.Count > 0)
            {
                peersClimateProgramIDs.Add(climateProgramID);
            }

            peerPrograms = await _context.ClimatePrograms
                .Where(c => peersClimateProgramIDs.Contains(c.ClimateProgramID))
                .Select(c => new PeerProgramHistoryReportDto
                {
                    ClimateProgramID = c.ClimateProgramID,
                    ProgramName = c.ProgramName,
                    Location = c.Location,
                    UpdatedDate = c.UpdatedAt,
                    Image = c.Image,
                    Year = c.Year,
                    Description = c.Description
                }).ToListAsync();

            if (isAiScore)
            {
                foreach (var c in peerPrograms)
                {
                    c.ProgramHistory = _context.AIPillarScores
                    .Include(x => x.Pillar)
                    .Where(x =>
                        x.ClimateProgramID == c.ClimateProgramID)
                    .GroupBy(x => x.Year)
                    .Select(yearGroup => new PeerProgramYearHistoryDto
                    {
                        ClimateProgramID = c.ClimateProgramID,
                        Year = yearGroup.Key,

                        ScoreProgress = yearGroup.Average(x => x.AIProgress ?? 0),

                        Pillars = yearGroup.Where(x => !x.Pillar.IsDeleted)
                            .GroupBy(p => new
                            {
                                p.PillarID,
                                p.Pillar.PillarName,
                                p.Pillar.DisplayOrder
                            })
                            .Select(pillarGroup => new PeerProgramPillarHistoryReportDto
                            {
                                PillarID = pillarGroup.Key.PillarID,
                                PillarName = pillarGroup.Key.PillarName,
                                DisplayOrder = pillarGroup.Key.DisplayOrder,
                                ScoreProgress = pillarGroup.Average(x => x.AIProgress ?? 0)
                            })
                            .OrderBy(x => x.DisplayOrder)
                            .ToList()
                    })
                    .OrderBy(x => x.Year)
                    .ToList();
                }
            }
            else
            {
                var pillars = await _commonService.GetPillars();

                var programProgress = await _commonService.GetProgramProgressHistoryAsync(userID, (int)role);
                var filterPrograms = programProgress
                    .Where(x => peersClimateProgramIDs.Contains(x.ClimateProgramID))
                    .ToList();

                foreach (var program in peerPrograms)
                {
                    var progress = filterPrograms
                        .Where(x => x.ClimateProgramID == program.ClimateProgramID)
                        .ToList();

                    // ? Build Year-wise history first
                    program.ProgramHistory = progress
                        .GroupBy(x => x.Year)
                        .Select(yearGroup => new PeerProgramYearHistoryDto
                        {
                            ClimateProgramID = program.ClimateProgramID,
                            Year = yearGroup.Key,

                            // {Program} level score
                            ScoreProgress = Math.Round(
                                    yearGroup.Select(x => x.ScoreProgress)
                                             .DefaultIfEmpty(0)
                                             .Sum() / pillarCount, 2),

                            // Pillar level score
                            Pillars = pillars
                                .Select(p => new PeerProgramPillarHistoryReportDto
                                {
                                    PillarID = p.PillarID,
                                    PillarName = p.PillarName,
                                    DisplayOrder = p.DisplayOrder,

                                    ScoreProgress = Math.Round(
                                        yearGroup
                                            .Where(x => x.PillarID == p.PillarID)
                                            .Select(x => x.ScoreProgress)
                                            .DefaultIfEmpty(0)
                                            .Average(), 2)
                                })
                                .OrderBy(x => x.DisplayOrder)
                                .ToList()
                        })
                        .OrderBy(x => x.Year)
                        .ToList();
                }
            }

            return peerPrograms;
        }

        // -----------------------------------------------------------------------------
        //  ENTRY POINTS  (GenerateProgramDetailsPdf / GeneratePillarDetailsPdf)
        // -----------------------------------------------------------------------------

        public async Task<byte[]> GenerateProgramDetailsReport(AiProgramSummeryDto programDetails, UserRole userRole, int userID,
           IServices.DocumentFormat format = IServices.DocumentFormat.Pdf, string reportType = "ai")
        {
            try
            {
                var isManual = reportType != "ai" && userRole == UserRole.Admin ? true : false;

                var pillars = await GetAIProgramPillars(programDetails.ClimateProgramID, userID, userRole);

                var kpis = await GetAccessKpis(userID, userRole, programDetails.ClimateProgramID, !isManual);

                if (isManual)
                {
                    programDetails.AIProgress = programDetails.EvaluatorScore;

                    foreach (var pillar in pillars.Result.Pillars)
                    {
                        pillar.AIProgress = pillar.EvaluatorScore;
                    }
                }

                var peerPrograms = await GetPeerPrograms(userID, userRole, programDetails.ClimateProgramID, !isManual);

                var document = await _documentGeneratorService.GenerateProgramDetails(programDetails, pillars.Result.Pillars, kpis, peerPrograms, userRole, format);

                return document;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateProgramDetailsReport", ex);
                return Array.Empty<byte>();
            }
        }
        public async Task<byte[]> GeneratePillarDetailsReport(AiProgramPillarResponse pillarData, UserRole userRole, IServices.DocumentFormat format = IServices.DocumentFormat.Pdf)
        {
            try
            {
                var document = await _documentGeneratorService.GeneratePillarDetails(pillarData, userRole, format);


                return document;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GeneratePillarDetailsReport", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<ResultResponseDto<AiCrossProgramsResponseDto>> GetAICrossProgramPillars(AiClimateProgramIDsDto climateProgramIDs, int userID, UserRole userRole)
        {
            try
            {
                var response = new AiCrossProgramsResponseDto();


                var aiPillarScores = await _context.AIPillarScores
                    .Where(x => climateProgramIDs.ClimateProgramIDs.Contains(x.ClimateProgramID))
                    .ToListAsync();

                var programs = await _context.ClimatePrograms
                    .Where(x => climateProgramIDs.ClimateProgramIDs.Contains(x.ClimateProgramID))
                    .ToListAsync();

                // Pillar access based on role
                List<int> pillarIds = new();
                if (userRole == UserRole.ProgramUser)
                {
                    pillarIds = await _context.ClientPillarMappings
                        .Where(x => x.IsActive && x.UserID == userID)
                        .Select(x => x.PillarID)
                        .Distinct()
                        .ToListAsync();
                }

                var pillars = await _commonService.GetPillars();

                // Categories
                response.Categories.AddRange(
                    pillars
                        .Where(x => pillarIds.Count == 0 || pillarIds.Contains(x.PillarID))
                        .OrderBy(x=>x.DisplayOrder)
                        .Select(x => x.PillarName)
                );
                // Per Program processing

                var aiPrograms = await _context.AIProgramScores
                    .Where(x => climateProgramIDs.ClimateProgramIDs.Contains(x.ClimateProgramID) &&
                                ((userRole == UserRole.ProgramUser && x.IsVerified) || userRole != UserRole.ProgramUser))
                    .GroupBy(x => x.ClimateProgramID)
                    .Select(g => new
                    {
                        ClimateProgramID = g.Key,
                        UpdatedAt = g.Max(x=>x.UpdatedAt),
                        AIProgress = g.Max(x => x.AIProgress)
                    })
                    .ToDictionaryAsync(x => x.ClimateProgramID, x => new { x.AIProgress ,x.UpdatedAt });


                foreach (var program in programs)
                {
                    var pillarResults = pillars
                    .GroupJoin(
                        aiPillarScores.Where(x => x.ClimateProgramID == program.ClimateProgramID),
                        p => p.PillarID,
                        s => s.PillarID,
                        (pillar, scores) => new
                        {
                            Pillar = pillar,
                            Score = scores.FirstOrDefault()
                        })
                    .Select(x =>
                    {
                        var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.Pillar.PillarID);

                        return new CrossProgramsPillarValueDto
                        {
                            PillarID = x.Pillar.PillarID,
                            PillarName = x.Pillar.PillarName,
                            Value = isAccess ? x.Score?.AIProgress ?? 0 : 0,
                            IsAccess = isAccess,
                            DisplayOrder = x.Pillar.DisplayOrder
                        };
                    })
                    .OrderBy(x => !x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();
                    var chartRow = new CrossProgramsChartTableRowDto
                    {
                        ClimateProgramID = program.ClimateProgramID,
                        ProgramName = program.ProgramName,
                        PillarValues = pillarResults.ToList()
                    };
                    if (aiPrograms?.TryGetValue(program.ClimateProgramID,out var aiProgramValue) ?? false)
                    {
                        chartRow.Value = aiProgramValue.AIProgress ?? 0;
                        chartRow.UpdatedAt = string.IsNullOrEmpty(aiProgramValue.UpdatedAt.ToString()) ? DateTime.UtcNow : DateTime.Parse(aiProgramValue.UpdatedAt.ToString());
                    }
                    response.TableData.Add(chartRow);

                    var series = new CrossProgramsChartSeriesDto
                    {
                        Name = program.ProgramName,
                        Data = pillarResults
                            .Where(x => x.IsAccess)
                            .Select(x => x.Value).ToList()
                    };
                    response.Series.Add(series);
                }

                return ResultResponseDto<AiCrossProgramsResponseDto>.Success(response,new[] { "Pillars fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAICrossProgramPillars", ex);
                return ResultResponseDto<AiCrossProgramsResponseDto>.Failure(new[] { "Error in getting pillar details" });
            }
        }

        public async Task<ResultResponseDto<bool>> ChangedAiProgramEvaluationStatus(ChangedAiProgramEvaluationStatusDto dto, int userID, UserRole userRole)
        {
            try
            {
                var v = _context.StaffProgramMappings.Any(x => x.UserID == userID && x.ClimateProgramID == dto.ClimateProgramID);
                if ((v && userRole == UserRole.Analyst) || userRole == UserRole.Admin)
                {

                    var aiResponse = await _context.AIProgramScores.Where(x => x.ClimateProgramID == dto.ClimateProgramID).FirstOrDefaultAsync();
                    if (aiResponse != null)
                    {
                        aiResponse.IsVerified = dto.IsVerified;
                        aiResponse.VerifiedBy = userID;
                        
                        await _context.SaveChangesAsync();

                        _download.InsertAnalyticalLayerResults(dto.ClimateProgramID);
                        return ResultResponseDto<bool>.Success(true, new[] { dto.IsVerified ? "Finalize and lock the AI-generated score successfully" : "Reject the current AI-generated score Successfully" });
                    }
                }
                return ResultResponseDto<bool>.Failure(new[] { "Invalid Program, please try again" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangedAiProgramEvaluationStatus", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Error in Changed AiProgram Evaluation Status" });
            }
        }
        public async Task<ResultResponseDto<bool>> RegenerateAiSearch(RegenerateAiSearchDto dto,int userID, UserRole userRole)
        {
            try
            {
                if (dto.QuestionEnable)
                {
                    var aiQuestionList = await _context.AIEstimatedQuestionScores
                        .Where(x => x.ClimateProgramID == dto.ClimateProgramID)
                        .ToListAsync();

                    if (aiQuestionList.Count > 0)
                    {
                        _context.AIEstimatedQuestionScores.RemoveRange(aiQuestionList);
                        await _context.SaveChangesAsync();
                    }
                }


                await _download.AiResearchByClimateProgramID(dto.ClimateProgramID, dto.ProgramEnable, dto.PillarEnable,
                    dto.QuestionEnable, dto.RegenerateMissingQuestionsEnable);
                var aiResponse = await _context.AIProgramScores.FirstOrDefaultAsync(x => x.ClimateProgramID == dto.ClimateProgramID);
                if(aiResponse != null)
                {
                    aiResponse.IsVerified = false;
                }
                // Assign viewers (optional)

                var aiEvaluatorProgramMappingsList = await _context.AIEvaluatorProgramMappings.Where(x => x.ClimateProgramID == dto.ClimateProgramID).ToListAsync();

                var um = _context.StaffProgramMappings.Where(x => !x.IsDeleted && x.ClimateProgramID == dto.ClimateProgramID && dto.ViewerUserIDs.Contains(x.UserID));
                var valid = um.All(x => dto.ViewerUserIDs.Contains(x.UserID));

                string msg = "Evaluator not have access of this county please try again";

                if (dto.ViewerUserIDs != null && dto.ViewerUserIDs.Any() && valid)
                {
                    var existingMappings = aiEvaluatorProgramMappingsList.Where(x => dto.ViewerUserIDs.Contains(x.UserID));


                    var existingUserIds = existingMappings.Select(x => x.UserID).ToHashSet();

                    // Update existing mappings
                    foreach (var mapping in existingMappings)
                    {
                        mapping.IsActive = true;
                        mapping.UpdatedAt = DateTime.UtcNow;
                        mapping.AssignBy = userID;
                        mapping.Comment = string.Empty;
                    }

                    // Insert new mappings
                    var newMappings = dto.ViewerUserIDs
                        .Where(userId => !existingUserIds.Contains(userId))
                        .Select(userId => new AIEvaluatorProgramMapping
                        {
                            UserID = userId,
                            ClimateProgramID = dto.ClimateProgramID,
                            AssignBy = userID,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        });

                    await _context.AIEvaluatorProgramMappings.AddRangeAsync(newMappings);
                    msg = "Evaluator have access to view the Program";
                }
                else if(aiEvaluatorProgramMappingsList.Count > 0)
                {
                    foreach (var mapping in aiEvaluatorProgramMappingsList)
                    {
                        mapping.IsActive = false;
                        mapping.UpdatedAt = DateTime.UtcNow;
                        mapping.AssignBy = userID;
                        mapping.Comment = string.Empty;
                    }
                }

                var msglist = new List<string>
                {
                    "AI research import has been initiated successfully"
                };

                if (dto.ViewerUserIDs != null && dto.ViewerUserIDs.Any())
                {
                    msglist.Add(msg);
                }
                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, msglist);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RegenerateAiSearch", ex);

                return ResultResponseDto<bool>.Failure(new[] { "Something went wrong while importing AI research. Please try again later." });
            }
        }
        public async Task<ResultResponseDto<bool>> AddComment(AddCommentDto dto, int userID, UserRole userRole)
        {
            try
            {
                var aiEvaluatorProgramMappings = await _context.AIEvaluatorProgramMappings.FirstOrDefaultAsync(x => x.UserID == userID && x.IsActive && x.ClimateProgramID == dto.ClimateProgramID);
                if (aiEvaluatorProgramMappings !=null && userRole == UserRole.Evaluator)
                {
                    aiEvaluatorProgramMappings.Comment = dto.Comment;

                    await _context.SaveChangesAsync();


                    await _context.SaveChangesAsync();
                    return ResultResponseDto<bool>.Success(true, new[] {"Comment Added Successfully"});

                }
                return ResultResponseDto<bool>.Failure(new[] { "Invalid Program, please try again" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangedAiProgramEvaluationStatus", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Error in Changed AiProgram Evaluation Status" });
            }
        }
        public async Task<ResultResponseDto<bool>> RegeneratePillarAiSearch(RegeneratePillarAiSearchDto channel, int userID, UserRole userRole)
        {
            try
            {
                if (channel.QuestionEnable)
                {
                    var currentYear = DateTime.Now.Year;
                    var aiQuestionList = await _context.AIEstimatedQuestionScores.Where(x => x.ClimateProgramID == channel.ClimateProgramID && x.PillarID == channel.PillarID && x.Year == currentYear).ToListAsync();
                    if (aiQuestionList.Count > 0)
                    {
                        _context.AIEstimatedQuestionScores.RemoveRange(aiQuestionList);
                        await _context.SaveChangesAsync();
                    }

                    await _iAIAnalayzeService.AnalyzeQuestionsOfProgramPillar(channel.ClimateProgramID, channel.PillarID);
                }

                if (channel.PillarEnable)
                    await _iAIAnalayzeService.AnalyzeSinglePillar(channel.ClimateProgramID,channel.PillarID);

                if (!channel.QuestionEnable && channel.RegenerateMissingQuestionsEnable)
                {
                    var payload = new MissingProgramQuestionRequest
                    {
                        PillarID = channel.PillarID,
                        ClimateProgramID = channel.ClimateProgramID
                    };
                    await _iAIAnalayzeService.AnalyzeProgramMissingQuestions(payload);
                }

                var msglist = new List<string>
                {
                    "AI research import has been initiated successfully"
                };
               
                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, msglist);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RegenerateAiSearch", ex);

                return ResultResponseDto<bool>.Failure(new[] { "Something went wrong while importing AI research. Please try again later." });
            }
        }

        public async Task<AiProgramSummeryDto> GetProgramAiSummeryDetail(int userID, UserRole userRole, int? climateProgramID, string reportType = "AI")
        {
            reportType = reportType.ToUpper();
            var query = await GetProgramAiSummeryDetails(userID, userRole, climateProgramID);
            var programsDetails = await query.ToListAsync();
            var progress = await _commonService.GetProgramProgressAsync(userID, (int)userRole, 0);

            var analyticalLayers = _context.AnalyticalLayers.AsQueryable();

            if (userRole == UserRole.ProgramUser)
            {
                analyticalLayers =
                    from ar in _context.AnalyticalLayers
                    join alp in _context.AnalyticalLayerPillarMappings
                        on ar.LayerID equals alp.LayerID
                    join cup in _context.ClientPillarMappings
                        on alp.PillarID equals cup.PillarID
                    join puc in _context.ClientProgramMappings
                        on cup.UserID equals puc.UserID
                    where cup.IsActive
                          && puc.IsActive
                          && cup.UserID == userID
                          && puc.UserID == userID
                    select ar;
            }

            var totalValidKpis = await analyticalLayers.Distinct().CountAsync();

            int pillarCount = (await _commonService.GetPillars()).Count;

            var programRanks = CalculateProgramRanks(progress, pillarCount, reportType);

            var totalProgramCount = await _context.ClimatePrograms.Where(x => !x.IsDeleted && x.IsActive).CountAsync();

            ApplyProgramRanking(programsDetails, programRanks , reportType , totalProgramCount);

            var programs = progress.Where(x => x.ClimateProgramID == climateProgramID);

            var programDetails = programsDetails.FirstOrDefault(x => x.ClimateProgramID == climateProgramID);

            if (programDetails != null)
            {
                var score =  programDetails.AIProgress;

                if (userRole != UserRole.ProgramUser && programs != null)
                {
                    var programScore = programs
                        .Select(x => x.ScoreProgress)
                        .DefaultIfEmpty(0)
                        .Sum();
                    programScore = Math.Round(programScore / (decimal)pillarCount, 2);

                    programDetails.EvaluatorScore = Math.Round(programScore, 2);
                    programDetails.Discrepancy = Math.Abs(programScore - (programDetails.AIProgress ?? 0));

                    if(reportType != "AI")
                    {
                        score = programDetails.EvaluatorScore;
                    }
                }
                programDetails.EvidenceSummary = CommonService.InitailLineOfExecutiveSummery(programDetails.EvidenceSummary, score, programDetails.ProgramName, pillarCount, totalValidKpis);

            }
            return programDetails ?? new AiProgramSummeryDto();
        }

        private void ApplyProgramRanking(List<AiProgramSummeryDto> programsDetails, List<dynamic> programRanks, string reportType = "AI", int? totalProgramCount = 0)
        {
            totalProgramCount = (totalProgramCount == null || totalProgramCount == 0) ?  programsDetails.Count : totalProgramCount;

            // Global rank lookup
            var programRankLookup = programRanks.ToDictionary(x => x.ClimateProgramID);

            //// Region -> ClimateProgramIDs lookup
            //var regionLookup = programsDetails      
            //    .GroupBy(x => x.Location)
            //    .ToDictionary(
            //        g => g.Key,
            //        g => g.Select(x => x.ClimateProgramID).ToList()
            //    );

            //// Region rank lookup
            //var regionRankLookup = new Dictionary<int, (int Rank, int TotalProgram)>();

            //foreach (var region in regionLookup)
            //{
            //    var rankedPrograms = programRanks
            //        .Where(x => region.Value.Contains(x.ClimateProgramID))
            //        .OrderByDescending(x => reportType == "AI"
            //            ? (x.AiProgress ?? 0)
            //            : (x.ScoreProgress ?? 0))

            //        .Select((x, index) => new
            //        {
            //            x.ClimateProgramID,
            //            Rank = index + 1
            //        })
            //        .ToList();

            //    var regionTotal = rankedPrograms.Count;

            //    foreach (var Program in rankedPrograms)
            //    {
            //        regionRankLookup[Program.ClimateProgramID] = (Program.Rank, regionTotal);
            //    }
            //}

            // Final mapping
            foreach (var program in programsDetails)
            {
                if (programRankLookup.TryGetValue(program.ClimateProgramID, out var globalRank))
                {
                    program.Rank = globalRank.Rank;
                    program.TotalProgram = totalProgramCount;
                }

                //if (regionRankLookup.TryGetValue(program.ClimateProgramID, out var regionRank))
                //{
                //    program.RegionRank = regionRank.Rank;
                //    program.RegionTotalProgram = regionRank.TotalProgram;
                //}
            }
        }
        private List<dynamic> CalculateProgramRanks(List<EvaluationProgramProgressResultDto> progress, decimal pillarCount, string reportType = "AI")
        {
            var groupedProgress = progress
                .GroupBy(x => x.ClimateProgramID)
                .Select(g => new
                {
                    ClimateProgramID = g.Key,
                    ScoreProgress = Math.Round((g.Select(x => x.ScoreProgress).DefaultIfEmpty(0).Sum()) / (decimal)pillarCount, 2),
                    AiProgress = Math.Round((g.Select(x => x.AIProgress).DefaultIfEmpty(0).Sum()) / (decimal)pillarCount, 2),
                });

            return groupedProgress
                    .OrderByDescending(x => reportType == "AI" ? x.AiProgress : x.ScoreProgress)
                    .Select((x, index) => new
                    {
                        x.ClimateProgramID,
                        x.ScoreProgress,
                        x.AiProgress,
                        Rank = index + 1
                    })
                    .ToList<dynamic>();
        }


        private async Task<List<KpiChartItem>> GetAccessKpis(int userID, UserRole role, int? climateProgramID, bool isAiScore = true)
        {
            IQueryable<AnalyticalLayerResult> baseQuery = _context.AnalyticalLayerResults
                .AsNoTracking()
                .Include(ar => ar.AnalyticalLayer)
                .ThenInclude(al => al.FiveLevelInterpretations)
                .Include(ar => ar.Program);

            if (role == UserRole.ProgramUser)
            {
                var validPrograms = _context.ClientProgramMappings
                    .Where(x => x.IsActive && x.UserID == userID)
                    .Select(x => x.ClimateProgramID);

                var validPillarIds = _context.ClientPillarMappings
                    .Where(x => x.IsActive && x.UserID == userID)
                    .Select(x => x.PillarID);

                var validLayerIds = _context.AnalyticalLayerPillarMappings
                    .Where(x => validPillarIds.Contains(x.PillarID))
                    .Select(x => x.LayerID)
                    .Distinct();

                baseQuery = baseQuery
                    .Where(ar =>
                        validPrograms.Contains(ar.ClimateProgramID) &&
                        validLayerIds.Contains(ar.LayerID));
            }

            var kpiRaw = baseQuery
            .Where(x => !climateProgramID.HasValue || x.ClimateProgramID == climateProgramID)
            .Select(x => new
            {
                KpiShortName = x.AnalyticalLayer.LayerCode,
                KpiName = x.AnalyticalLayer.LayerName,
                ClimateProgramID = x.ClimateProgramID,
                AiCalValue5 = x.AiCalValue5,
                CalValue5 = x.CalValue5,
                Definition = StripHtml(x.AnalyticalLayer.Purpose),
                AnalyticalLayer = x.AnalyticalLayer
            })
            .Select(x => new
            {
                x.KpiShortName,
                x.KpiName,
                x.ClimateProgramID,
                x.AiCalValue5,
                x.CalValue5,
                LayerID = x.AnalyticalLayer.LayerID,
                Definition = x.Definition,
                Interpretation = x.AnalyticalLayer.FiveLevelInterpretations.Select(i => new FiveLevelInterpretationsDto
                (
                   i.InterpretationID,
                   i.LayerID,
                   i.MinRange,
                   i.MaxRange,
                   i.Condition,
                   i.Descriptor
                )).ToList()

            }).OrderBy(x => x.LayerID);

            var kpis = await kpiRaw
                .Select(k => new KpiChartItem(k.KpiShortName, k.KpiName, (isAiScore && role == UserRole.Admin ? k.AiCalValue5 : k.CalValue5) ?? 0, k.Definition, k.ClimateProgramID, k.Interpretation))
                .ToListAsync();

            return kpis ?? new List<KpiChartItem>();
        }
        public async Task<List<AiProgramSummeryDto>> GetAllProgramAiSummeryDetail(int userID, UserRole userRole)
        {
            var query = await GetProgramAiSummeryDetails(userID, userRole, null);
            var programsDetails = await query.ToListAsync();
            int pillarCount = (await _commonService.GetPillars()).Count;

            var progress = await _commonService.GetProgramProgressAsync(userID, (int)userRole);
            var programRanks = CalculateProgramRanks(progress, pillarCount);
            ApplyProgramRanking(programsDetails, programRanks);

            var analyticalLayers = _context.AnalyticalLayers.AsQueryable();

            if (userRole == UserRole.ProgramUser)
            {
                analyticalLayers =
                    from ar in _context.AnalyticalLayers
                    join alp in _context.AnalyticalLayerPillarMappings
                        on ar.LayerID equals alp.LayerID
                    join cup in _context.ClientPillarMappings
                        on alp.PillarID equals cup.PillarID
                    join puc in _context.ClientProgramMappings
                        on cup.UserID equals puc.UserID
                    where cup.IsActive
                          && puc.IsActive
                          && cup.UserID == userID
                          && puc.UserID == userID
                    select ar;
            }

            var totalValidKpis = await analyticalLayers.Distinct().CountAsync();


            foreach (var programDetail in programsDetails)
            {
                programDetail.EvidenceSummary = CommonService.InitailLineOfExecutiveSummery(programDetail.EvidenceSummary, programDetail.AIProgress, programDetail.ProgramName, pillarCount,totalValidKpis);

                if (userRole != UserRole.ProgramUser)
                {
                    var programs = progress.Where(x => x.ClimateProgramID == programDetail.ClimateProgramID);

                    if (programs != null)
                    {
                        var programScore = programs
                            .Select(x => x.ScoreProgress)
                            .DefaultIfEmpty(0)
                            .Sum();
                        programScore = Math.Round(programScore / (decimal)pillarCount, 2);

                        programDetail.EvaluatorScore = Math.Round(programScore, 2);
                        programDetail.Discrepancy = Math.Abs(programScore - (programDetail.AIProgress ?? 0));
                    }
                }

            }
            return programsDetails;
        }        

        public async Task<byte[]> GenerateAllProgramDetailsReport(List<AiProgramSummeryDto> programDetails, UserRole userRole, int userID, IServices.DocumentFormat format = IServices.DocumentFormat.Pdf)
        {
            try
            {
                var pillars = await GetAllProgramAIPillars(userID, userRole);

                var kpis = await GetAccessKpis(userID, userRole, null);

                var recordAvailable = pillars.Result.Any(x => programDetails.Select(x => x.ClimateProgramID).Contains(x.Key));
                if (recordAvailable)
                {
                    var document = await _documentGeneratorService.GenerateAllProgramsDetails(programDetails, pillars.Result, kpis, userRole, format);

                    return document;
                }
                else
                {
                    return Array.Empty<byte>();
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateProgramDetailsReport", ex);
                return Array.Empty<byte>();
            }
        }
        public async Task<ResultResponseDto<Dictionary<int, List<AiProgramPillarResponse>>>> GetAllProgramAIPillars(
         int userID, UserRole userRole)
        {
            try
            {
                int pillarCount = (await _commonService.GetPillars()).Count;
                var scores = await _context.AIPillarScores
                    .Include(x => x.Program)
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds = new();
                if (userRole == UserRole.ProgramUser)
                {
                    pillarIds = await _context.ClientPillarMappings
                        .Where(x => x.IsActive && x.UserID == userID)
                        .Select(x => x.PillarID)
                        .Distinct()
                        .ToListAsync();
                }

                var pillars = await _context.Pillars.Where(x => x.IsActive && !x.IsDeleted).Select(x => new
                {
                    x.PillarID,
                    x.PillarName,
                    x.DisplayOrder,
                    x.ImagePath,
                    TotalQuestions = x.Questions.Count(x=>!x.IsDeleted)
                }).ToListAsync();

                var climateProgramIDs = scores.Select(x => x.ClimateProgramID).Distinct().ToList();

                var result = new Dictionary<int, List<AiProgramPillarResponse>>();

                foreach (var climateProgramID in climateProgramIDs)
                {
                    var programScores = scores.Where(x => x.ClimateProgramID == climateProgramID).ToList();

                    var pillarResults = pillars
                        .GroupJoin(
                            programScores,
                            p => p.PillarID,
                            s => s.PillarID,
                            (pillar, score) => new { pillar, score = score.FirstOrDefault() }
                        )
                        .Select(x =>
                        {
                            var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.pillar.PillarID);

                            var r = new AiProgramPillarResponse
                            {
                                PillarScoreID = x.score?.PillarScoreID ?? 0,
                                ClimateProgramID = x.score?.ClimateProgramID ?? climateProgramID,
                                ProgramName = x.score?.Program?.ProgramName ?? "",                         
                                PillarID = x.pillar.PillarID,
                                PillarName = x.pillar.PillarName,
                                DisplayOrder = x.pillar.DisplayOrder,
                                ImagePath = x.pillar.ImagePath,
                                IsAccess = isAccess
                            };

                            if (isAccess && x.score != null)
                            {
                                r.AIDataYear = x.score.Year;
                                r.AIScore = x.score.AIScore;
                                r.AIProgress = x.score.AIProgress;
                                r.EvaluatorScore = x.score.EvaluatorScore;
                                r.Discrepancy = x.score.Discrepancy;
                                r.ConfidenceLevel = x.score.ConfidenceLevel;
                                r.EvidenceSummary = x.score.EvidenceSummary;
                                r.StructuralEvidence = x.score.StructuralEvidence;
                                r.OperationalEvidence = x.score.OperationalEvidence;
                                r.OutcomeEvidence = x.score.OutcomeEvidence;
                                r.PerceptionEvidence = x.score.PerceptionEvidence;
                                r.TemporalScope = x.score.TemporalScope;
                                r.DistortionScreening = x.score.DistortionScreening;
                                r.RelationalIntegrity = x.score.RelationalIntegrity;
                                r.StressGeopoliticalShock = x.score.StressGeopoliticalShock;
                                r.StressFinanceShock = x.score.StressFinanceShock;
                                r.StressLegitimacyShock = x.score.StressLegitimacyShock;
                                r.StressOverallResilience = x.score.StressOverallResilience;
                                r.StressScoreAdjustment = x.score.StressScoreAdjustment;
                                r.InclusionEquityAdjustment = x.score.InclusionEquityAdjustment;
                                r.OpacityRisk = x.score.OpacityRisk;
                                r.NonCompensationNote = x.score.NonCompensationNote;
                                r.InclusionAccessNote = x.score.InclusionAccessNote;
                                r.InstitutionalAssessment = x.score.InstitutionalAssessment;
                                r.DataGapAnalysis = x.score.DataGapAnalysis;
                                r.RedFlag = x.score.RedFlag;
                                r.DataSourceCitations = x.score.DataSourceCitations;
                                r.UpdatedAt = x.score.UpdatedAt;
                            }

                            return r;
                        })
                        .OrderBy(x => !x.IsAccess)
                        .ThenBy(x => x.DisplayOrder)
                        .ToList();

                    result.Add(climateProgramID, pillarResults);
                }

                var progress = await _commonService.GetProgramProgressAsync(userID, (int)userRole);

                var answeredQuestions = await _context.AIEstimatedQuestionScores
                    .GroupBy(x => new { x.ClimateProgramID, x.PillarID })
                    .Select(g => new
                    {
                        g.Key.ClimateProgramID,
                        g.Key.PillarID,
                        AnsweredQuestions = g.Count()
                    })
                    .ToListAsync();

                foreach (var program in result)
                {
                    foreach (var c in program.Value)
                    {
                        var totalQuestions = pillars.FirstOrDefault(x => x.PillarID == c.PillarID)?.TotalQuestions ?? 1;

                        var answeredQuestion = answeredQuestions
                            .FirstOrDefault(x => x.ClimateProgramID == program.Key && x.PillarID == c.PillarID)?.AnsweredQuestions ?? 0;

                        var ProgramScore = progress
                            .Where(x => x.ClimateProgramID == program.Key && x.PillarID == c.PillarID)
                            .Select(x => x.ScoreProgress)
                            .DefaultIfEmpty(0)
                            .Sum();

                        ProgramScore = Math.Round(ProgramScore / (decimal)pillarCount, 2);

                        c.EvaluatorScore = ProgramScore;
                        c.Discrepancy = Math.Abs(ProgramScore - (c.AIProgress ?? 0));
                        c.AICompletionRate = answeredQuestion * 100.0M / totalQuestions;
                    }
                }

                var response = ResultResponseDto<Dictionary<int, List<AiProgramPillarResponse>>>
                    .Success(result, new[] { "All programs pillars fetched successfully" });

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in getAllProgramsAIPillars", ex);

                return ResultResponseDto<Dictionary<int, List<AiProgramPillarResponse>>>
                    .Failure(new[] { "Error in getting programs pillar details" });
            }
        }
        public record KpiChartItem(string ShortName, string Name, decimal Value, string? Definition, int? ClimateProgramID, List<FiveLevelInterpretationsDto> InterPretation);

        public record FiveLevelInterpretationsDto(
        int InterpretationID,
        int LayerID,
        decimal? MinRange,
        decimal? MaxRange,
        string Condition,
        string Descriptor
   );
        public record PillarChartItem(string ShortName, string Name, decimal? Value);
        #region TransferAssessment
        public async Task<ResultResponseDto<string>> AITransferAssessment(AITransferAssessmentRequestDto r, int userID, UserRole userRole)
        {
            try
            {
                var currentDate = DateTime.Now;

                if (userRole == UserRole.ProgramUser || userRole == UserRole.Evaluator)
                {
                    return ResultResponseDto<string>.Failure(new[] { "Failed to transfer assessment, You don't have access." });
                }

                if (userRole == UserRole.Analyst)
                {
                    r.TransferToUserID = userID;

                    var validCity = _context.StaffProgramMappings.Any(x => !x.IsDeleted && x.ClimateProgramID == r.ClimateProgramID && x.UserID == userID);

                    if (!validCity)
                    {
                        return ResultResponseDto<string>.Failure(new[] { "This assessment can't be imported because the selected user hasn’t been assigned to this Program yet." });
                    }
                }

                var aiAssessmentData = await _context.AIEstimatedQuestionScores
                                    .Where(x => x.ClimateProgramID == r.ClimateProgramID)
                                    .ToListAsync();

                var aiAssessmentQuestions = aiAssessmentData
                    .GroupBy(x => x.PillarID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                if (aiAssessmentQuestions == null || aiAssessmentQuestions.Count==0)
                    return ResultResponseDto<string>.Failure(new[] { "There is no ai assessment is available for this Program" });


                var StaffProgramMapping = await _context.StaffProgramMappings.FirstOrDefaultAsync(x => !x.IsDeleted && x.ClimateProgramID == r.ClimateProgramID && x.UserID == r.TransferToUserID);

                if (StaffProgramMapping == null)
                    return ResultResponseDto<string>.Failure(new[] { "This assessment can't be imported because the selected user hasn’t been assigned to this Program yet." });


                // Load existing assessment for that user/Program/year (with pillars/responses)
                var existingAssessment = await _context.Assessments
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(p => p.Responses)
                    .FirstOrDefaultAsync(a => a.StaffProgramMappingID == StaffProgramMapping.StaffProgramMappingID);

                if (existingAssessment == null)
                {
                    existingAssessment = new Assessment
                    {
                        StaffProgramMappingID = StaffProgramMapping.StaffProgramMappingID,
                        CreatedAt = currentDate,
                        UpdatedAt = currentDate,
                        IsActive = true,
                        AssessmentPhase = userRole == UserRole.Admin ? AssessmentPhase.Completed : AssessmentPhase.InProgress,
                        PillarAssessments = new List<PillarAssessment>()
                    };

                    _context.Assessments.Add(existingAssessment);
                }
                else if (existingAssessment.AssessmentPhase == AssessmentPhase.Completed)
                {
                    return ResultResponseDto<string>.Failure(new[] { "Permission from the Admin is required to edit this assessment." });
                }
                else
                {
                    existingAssessment.UpdatedAt = currentDate;
                    existingAssessment.AssessmentPhase = AssessmentPhase.InProgress;
                }

                var questions = await _context.Questions.Include(x => x.QuestionOptions).ToDictionaryAsync(q => q.QuestionID, q => q);

                // Transfer pillar data
                foreach (var pillar in aiAssessmentQuestions)
                {
                    var existingPillar = existingAssessment.PillarAssessments
                        .FirstOrDefault(x => x.PillarID == pillar.Key);

                    if (existingPillar == null)
                    {
                        existingPillar = new PillarAssessment
                        {
                            PillarID = pillar.Key,
                            Responses = new List<AssessmentResponse>()
                        };
                        existingAssessment.PillarAssessments.Add(existingPillar);
                    }

                    // Add/Update responses
                    foreach (var response in pillar.Value)
                    {
                        var existingResponse = existingPillar.Responses
                            .FirstOrDefault(rp => rp.QuestionID == response.QuestionID);

                        var qustion = questions.ContainsKey(response.QuestionID) ? questions[response.QuestionID] : null;
                        if (qustion == null)
                            continue;

                        int? score = response.AIScore != null ? (int?)Math.Round(response.AIScore.Value, 0) : null;

                        var option = qustion.QuestionOptions.FirstOrDefault(x => x.ScoreValue == score.ToString());
                        if (option == null)
                            continue;

                        if (existingResponse == null)
                        {

                            existingPillar.Responses.Add(new AssessmentResponse
                            {
                                QuestionID = response.QuestionID,
                                QuestionOptionID = option.OptionID,
                                Justification = response.EvidenceSummary,
                                Source = response.SourceDataExtract + "SourceURL : " + response.SourceURL,
                                Score = score
                            });
                        }
                        else
                        {
                            existingResponse.QuestionOptionID = option.OptionID;
                            existingResponse.Justification = response.EvidenceSummary;
                            existingResponse.Score = score;
                            existingResponse.Source = response.SourceDataExtract + " SourceURL : " + response.SourceURL;
                        }
                    }

                    // Delete responses not present in transferAssessment
                    var transferQuestionIds = pillar.Value.Select(x => x.QuestionID).ToHashSet();
                    var toDeleteResponses = existingPillar.Responses
                        .Where(x => !transferQuestionIds.Contains(x.QuestionID))
                        .ToList();

                    foreach (var resp in toDeleteResponses)
                    {
                        //existingPillar.Responses.Remove(resp);
                        _context.AssessmentResponses.Remove(resp);
                    }
                }

                // Delete pillars not present in transferAssessment
                var transferPillarIds = aiAssessmentQuestions.Select(x => x.Key).ToHashSet();
                var toDeletePillars = existingAssessment.PillarAssessments
                    .Where(x => !transferPillarIds.Contains(x.PillarID))
                    .ToList();

                foreach (var pillar in toDeletePillars)
                {
                    //existingAssessment.PillarAssessments.Remove(pillar);
                    _context.PillarAssessments.Remove(pillar);
                }
                if (existingAssessment.AssessmentPhase == AssessmentPhase.Completed)
                {
                    _download.InsertAnalyticalLayerResults(r.ClimateProgramID);
                }                
                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Assessment transferred successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in TransferAssessment", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to transfer assessment, please try again later." });
            }
        }
        #endregion TransferAssessment
        public async Task<ResultResponseDto<string>> ReCalculateKpis(int userID, UserRole userRole)
        {
            try
            {
                if (userRole != UserRole.Admin)
                {
                    return ResultResponseDto<string>.Failure(new[] { "Failed to recalculate KPIs, You don't have access." });
                }

                await _context.Database.ExecuteSqlRawAsync("EXEC sp_AiRecalculateProgramScore");

                await _context.Database.ExecuteSqlRawAsync("EXEC sp_InsertAnalyticalLayerResults");

                await _context.Database.ExecuteSqlRawAsync("EXEC sp_AiInsertAnalyticalLayerResults");

                return ResultResponseDto<string>.Success("", new[] { "KPI recalculation has been initiated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ReCalculateKpis", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to recalculate KPIs, please try again later." });
            }
        }

        public static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove HTML tags
            var noTags = Regex.Replace(input, "<.*?>", string.Empty);

            // Decode HTML entities (e.g., &mdash;)
            return WebUtility.HtmlDecode(noTags);
        }


        #region ai document for local context

        public async Task<ResultResponseDto<string>> UploadAiDocuments(
            UploadAiDocumentRequest request,
            int userID,
            UserRole userRole)
        {
            try
            {
                if (userRole != UserRole.Admin)
                {
                    return ResultResponseDto<string>.Failure(
                        new[] { "Failed to Upload Ai Documents, You don't have access." });
                }

                var basePath = Path.Combine(_env.WebRootPath,"aidocuments");

                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                for (int i = 0; i < request.Files.Count; i++)
                {
                    var file = request.Files[i];
                    var pillarId = request.PillarIDs[i];

                    var ext = Path.GetExtension(file.FileName).ToLower();

                    if (ext != ".pdf" && ext != ".docx")
                        continue;

                    if (!Directory.Exists(basePath))
                        Directory.CreateDirectory(basePath);

                    var storedFileName = $"{Guid.NewGuid()}{ext}";
                    var fullPath = Path.Combine(basePath, storedFileName);

                    // ? Save file
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // ? Save DB record
                    var doc = new ProgramDocument
                    {
                        FileName = file.FileName,
                        StoredFileName = storedFileName,
                        FilePath = fullPath,
                        ClimateProgramID = request.ClimateProgramID,
                        PillarID = pillarId == 0 || request.ClimateProgramID == null ? null : pillarId,
                        FileType = ext,
                        FileSize = file.Length / 1024,//kb file will be store now
                        ProcessingStatus = DocumentProcessingStatus.Pending,
                        UpdatedAt = DateTime.UtcNow,
                        UploadedByUserID = userID,
                        DocumentLevel = GetDocumentLevel(request.ClimateProgramID, pillarId)
                    };

                    _context.ProgramDocuments.Add(doc);
                    await _context.SaveChangesAsync();
                    await _iAIAnalayzeService.ProcessDocument(doc.ProgramDocumentID);
                }

               return ResultResponseDto<string>.Success(
                   "",
                   new[] { "Upload Ai Documents has been initiated successfully." });            
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Upload Ai Documents", ex);

                return ResultResponseDto<string>.Failure(
                    new[] { "Failed to Upload Ai Documents, please try again later." });
            }
        }

        static string GetDocumentLevel(int? ClimateProgramID,int? pillarID)
        {
            if (ClimateProgramID == null)
            {
                return "Global";
            }
            else if(ClimateProgramID > 0 && (pillarID == null || pillarID == 0))
            {
                return "Program_Pillar";
            }
            else
            {
                return "Program";
            }
        }

        public async Task<PaginationResponse<GetProgramDocumentResponseDto>> GetAIProgramDocuments(
            AiProgramDocumentRequestDto request,
            int userID,
            UserRole userRole)
        {
            try
            {
                Expression<Func<StaffProgramMapping, bool>> filter = userRole switch
                {
                    UserRole.Admin => x => !x.IsDeleted,
                    UserRole.Analyst => x => !x.IsDeleted && (x.UserID == userID || x.AssignedByUserId == userID),
                    UserRole.Evaluator => x => !x.IsDeleted && x.UserID == userID,
                    _ => x => false
                };

                var userClimateProgramIDs = await _context.StaffProgramMappings
                    .Where(filter)
                    .Select(x => x.ClimateProgramID)
                    .Distinct()
                    .ToListAsync();

                var query = _context.ClimatePrograms
                    .Where(c =>
                        (
                            !request.ClimateProgramID.HasValue
                            || c.ClimateProgramID == request.ClimateProgramID
                        )
                        && (userClimateProgramIDs.Contains(c.ClimateProgramID) || userRole == UserRole.Admin)
                        && c.IsActive
                        && !c.IsDeleted
                    )
                    .Select(x => new GetProgramDocumentResponseDto
                    {
                        ClimateProgramID = x.ClimateProgramID,
                        ProgramName = x.ProgramName,
                        FileTypes = ""
                    });

                var result = await query.ApplyPaginationAsync(request);

                // ?? FileTypes (optimized for selected programs only)
                var ClimateProgramIDs = result.Data.Select(x => x.ClimateProgramID).ToList();

                var fileTypesData = await _context.ProgramDocuments
                    .Where(x => !x.IsDeleted && ClimateProgramIDs.Contains(x.ClimateProgramID))
                    .GroupBy(x => x.ClimateProgramID)
                    .Select(g => new
                    {
                        ClimateProgramID = g.Key,
                        FileTypes = g.Select(x => x.FileType).Distinct().ToList(),

                        NoOfFiles = g.Count(),
                        NoOfUsers = g.Select(d => d.UploadedByUserID).Distinct().Count(),
                        FilesSize = g.Sum(d => (long?)d.FileSize) ?? 0,
                    })
                    .ToListAsync();

                foreach (var item in result.Data)
                {
                    var ft = fileTypesData.FirstOrDefault(x => x.ClimateProgramID == item.ClimateProgramID);
                    if (ft != null)
                    {
                        item.FileTypes = string.Join(", ", ft.FileTypes);
                        item.NoOfFiles = ft.NoOfFiles;
                        item.NoOfUsers = ft.NoOfUsers;
                        item.FilesSize = ft.FilesSize;
                    }                   
                }

                return result;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAIDocuments", ex);

                return new PaginationResponse<GetProgramDocumentResponseDto>();
            }
        }

        public async Task<ResultResponseDto<List<GetProgramPillarDocumentResponseDto>>> GetAIProgramPillarDocuments(
             AiProgramPillarDocumentRequestDto request,
             int userID,
             UserRole userRole)
        {
            try
            {
                var result = await _context.ProgramDocuments
                   .Where(x => !x.IsDeleted && (x.ClimateProgramID == request.ClimateProgramID || x.ClimateProgramID == null))
                   .Select(x => new GetProgramPillarDocumentResponseDto
                   {
                       ProgramDocumentID = x.ProgramDocumentID,
                       ClimateProgramID = x.ClimateProgramID,
                       PillarID = x.PillarID,

                       PillarName = x.ClimateProgramID.HasValue ? _context.Pillars
                           .Where(p => p.PillarID == x.PillarID && !x.IsDeleted)
                           .Select(p => p.PillarName)
                           .FirstOrDefault() : x.DocumentLevel,

                       FileName = x.FileName,
                       FilePath = x.FilePath,
                       FileSize = x.FileSize,
                       FileType = x.FileType,
                       ProcessingStatus = x.ProcessingStatus,
                       StoredFileName = x.StoredFileName,

                       UploadedBy = "",
                       UploadedByUserID = x.UploadedByUserID ?? 0
                   })
                   .OrderBy(x => x.PillarID)
                   .ToListAsync();

                var users = _context.Users
                    .Where(x => result.Select(x => x.UploadedByUserID)
                    .Contains(x.UserID) && !x.IsDeleted)
                    .ToDictionary(x=>x.UserID , y=>y.FullName); 

                foreach(var r in result)
                {
                    r.UploadedBy = users.TryGetValue(r.UploadedByUserID, out var userName) ? userName : "";
                }

                return ResultResponseDto<List<GetProgramPillarDocumentResponseDto>>.Success(result, new[] { "Get documents successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAIDocuments", ex);
                return  ResultResponseDto<List<GetProgramPillarDocumentResponseDto>>.Failure(new[] { "Failed to get Documents, please try again later." });
            }
        }

        public async Task<ResultResponseDto<string>> DeleteDocument(
            DeleteProgramDocumentRequestDto request,
            int userID,
            UserRole userRole)
        {
            try
            {
                var query = _context.ProgramDocuments
                    .Where(x => !x.IsDeleted && x.ClimateProgramID == request.ClimateProgramID || (!request.ProgramDocumentID.HasValue || x.ProgramDocumentID == request.ProgramDocumentID));

                // ?? If not admin ? only own documents
                if (userRole != UserRole.Admin)
                {
                    query = query.Where(x => x.UploadedByUserID == userID );
                }              

                var documents = await query.ToListAsync();

                if (!documents.Any())
                {
                    return ResultResponseDto<string>.Failure(
                        new[] { "No documents found or you don't have permission." });
                }

                // ?? Soft delete
                foreach (var doc in documents)
                {
                    doc.IsDeleted = true;
                    await _iAIAnalayzeService.DeleteDocument(doc.ProgramDocumentID);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success(
                    "",
                    new[] { "Document(s) deleted successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in DeleteDocument", ex);

                return ResultResponseDto<string>.Failure(
                    new[] { "Failed to delete document, please try again later." });
            }
        }
        public async Task<FileResult> DownloadDocument(int ProgramDocumentID, int userID, UserRole userRole)
        {
            try
            {
                var doc = await _context.ProgramDocuments
                .FirstOrDefaultAsync(x => x.ProgramDocumentID == ProgramDocumentID && !x.IsDeleted);

                if (doc == null)
                    throw new Exception("Document not found.");

                if (!System.IO.File.Exists(doc.FilePath))
                    throw new Exception("File not found on server.");

                var ext = Path.GetExtension(doc.FileName).ToLower();

                var contentType = ext switch
                {
                    ".pdf" => "application/pdf",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    _ => "application/octet-stream"
                };

                var stream = new FileStream(doc.FilePath, FileMode.Open, FileAccess.Read);

                return new FileStreamResult(stream, contentType)
                {
                    FileDownloadName = doc.FileName
                };
            }
            catch(Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in getDocument", ex);

                var emptyStream = new MemoryStream();

                return new FileStreamResult(emptyStream, "application/pdf")
                {
                    FileDownloadName ="file.txt"
                };
            }
        }

        #endregion ai document
        #region ai score manual edit

        private async Task<bool> CanUserEditAiDataAsync(int userID, UserRole userRole, int climateProgramID)
        {
            if (userRole == UserRole.Admin)
                return true;

            if (userRole == UserRole.Analyst)
            {
                return await _context.StaffProgramMappings
                    .AnyAsync(x => !x.IsDeleted && x.UserID == userID && x.ClimateProgramID == climateProgramID);
            }

            return false;
        }

        private static decimal? CalculateDiscrepancy(decimal? evaluatorScore, decimal? aiProgress)
        {
            if (!evaluatorScore.HasValue && !aiProgress.HasValue)
                return null;

            return Math.Abs((evaluatorScore ?? 0) - (aiProgress ?? 0));
        }

        public async Task<ResultResponseDto<bool>> UpdateAIProgramScore(UpdateAIProgramScoreDto dto, int userID, UserRole userRole)
        {
            try
            {
                if (!await CanUserEditAiDataAsync(userID, userRole, dto.ClimateProgramID))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this program data." });

                var entity = await _context.AIProgramScores
                    .FirstOrDefaultAsync(x => x.ClimateProgramID == dto.ClimateProgramID && x.Year == dto.Year);

                if (entity == null || dto == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Program score record not found." });

                entity.ConfidenceLevel = dto.ConfidenceLevel ?? entity.ConfidenceLevel;
                entity.EvidenceSummary = dto.EvidenceSummary ?? entity.EvidenceSummary;
                entity.KeyFindings = dto.KeyFindings;
                entity.Recommendations = dto.Recommendations;
                entity.StructuralEvidence = dto.StructuralEvidence;
                entity.OperationalEvidence = dto.OperationalEvidence;
                entity.OutcomeEvidence = dto.OutcomeEvidence;
                entity.PerceptionEvidence = dto.PerceptionEvidence;
                entity.TemporalScope = dto.TemporalScope;
                entity.DistortionScreening = dto.DistortionScreening;
                entity.GeopoliticalShock = dto.GeopoliticalShock;
                entity.FinanceShock = dto.FinanceShock;
                entity.LegitimacyShock = dto.LegitimacyShock;
                entity.StressScoreAdjustment = dto.StressScoreAdjustment;
                entity.InclusionEquityAdjustment = dto.InclusionEquityAdjustment;
                entity.OpacityRisk = dto.OpacityRisk;
                entity.NonCompensationNote = dto.NonCompensationNote;
                entity.RelationalIntegrity = dto.RelationalIntegrity;
                entity.InstitutionalCapacity = dto.InstitutionalCapacity;
                entity.PrimarySource = dto.PrimarySource;
                entity.CrossPillarPatterns = dto.CrossPillarPatterns;
                entity.EquityAssessment = dto.EquityAssessment;
                entity.StrategicRecommendation = dto.StrategicRecommendation;
                entity.AssessmentValueNote = dto.AssessmentValueNote;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Program AI data updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAIProgramScore", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update program AI data." });
            }
        }

        public async Task<ResultResponseDto<bool>> UpdateAIPillarScore(UpdateAIPillarScoreDto dto, int userID, UserRole userRole)
        {
            try
            {
                var entity = await _context.AIPillarScores
                    .Include(x => x.DataSourceCitations)
                    .FirstOrDefaultAsync(x => x.PillarScoreID == dto.PillarScoreID);

                if (entity == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Pillar score record not found." });

                if (!await CanUserEditAiDataAsync(userID, userRole, entity.ClimateProgramID))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this pillar data." });

                entity.ConfidenceLevel = dto.ConfidenceLevel;
                entity.EvidenceSummary = dto.EvidenceSummary;
                entity.StructuralEvidence = dto.StructuralEvidence;
                entity.OperationalEvidence = dto.OperationalEvidence;
                entity.OutcomeEvidence = dto.OutcomeEvidence;
                entity.PerceptionEvidence = dto.PerceptionEvidence;
                entity.TemporalScope = dto.TemporalScope;
                entity.DistortionScreening = dto.DistortionScreening;
                entity.RelationalIntegrity = dto.RelationalIntegrity;
                entity.StressGeopoliticalShock = dto.StressGeopoliticalShock;
                entity.InclusionEquityAdjustment = dto.InclusionEquityAdjustment;
                entity.StressLegitimacyShock = dto.StressLegitimacyShock;
                entity.StressScoreAdjustment = dto.StressScoreAdjustment;
                entity.InclusionAccessNote = dto.InclusionAccessNote;
                entity.OpacityRisk = dto.OpacityRisk;
                entity.NonCompensationNote = dto.NonCompensationNote;
                entity.StressFinanceShock = dto.StressFinanceShock;
                entity.InstitutionalAssessment = dto.InstitutionalAssessment;
                entity.DataGapAnalysis = dto.DataGapAnalysis;
                entity.RedFlag = dto.RedFlag;
                entity.UpdatedAt = DateTime.UtcNow;

                if (dto.DataSourceCitations != null && entity.DataSourceCitations != null)
                {
                    foreach (var citationDto in dto.DataSourceCitations)
                    {
                        var citation = entity.DataSourceCitations.FirstOrDefault(x => x.CitationID == citationDto.CitationID);
                        if (citation == null)
                            continue;

                        citation.SourceType = citationDto.SourceType ?? citation.SourceType;
                        citation.SourceName = citationDto.SourceName ?? citation.SourceName;
                        citation.SourceURL = citationDto.SourceURL ?? citation.SourceURL;
                        citation.DataYear = citationDto.DataYear;
                        citation.DataExtract = citationDto.DataExtract ?? citation.DataExtract;
                        citation.TrustLevel = citationDto.TrustLevel;
                    }
                }

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Pillar AI data updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAIPillarScore", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update pillar AI data." });
            }
        }

        public async Task<ResultResponseDto<bool>> UpdateAIDataSourceCitation(UpdateAIDataSourceCitationDto dto, int userID, UserRole userRole)
        {
            try
            {
                var entity = await _context.AIDataSourceCitations
                    .Include(x => x.PillarScore)
                    .FirstOrDefaultAsync(x => x.CitationID == dto.CitationID);

                if (entity?.PillarScore == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Citation record not found." });

                if (!await CanUserEditAiDataAsync(userID, userRole, entity.PillarScore.ClimateProgramID))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this citation." });

                entity.SourceType = dto.SourceType ?? entity.SourceType;
                entity.SourceName = dto.SourceName ?? entity.SourceName;
                entity.SourceURL = dto.SourceURL ?? entity.SourceURL;
                entity.DataYear = dto.DataYear;
                entity.DataExtract = dto.DataExtract ?? entity.DataExtract;
                entity.TrustLevel = dto.TrustLevel;

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Citation updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAIDataSourceCitation", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update citation." });
            }
        }

        public async Task<ResultResponseDto<bool>> UpdateAIEstimatedQuestionScore(UpdateAIEstimatedQuestionScoreDto dto, int userID, UserRole userRole)
        {
            try
            {
                if (!await CanUserEditAiDataAsync(userID, userRole, dto.ClimateProgramID))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this question data." });

                var entity = await _context.AIEstimatedQuestionScores
                    .FirstOrDefaultAsync(x =>
                        x.ClimateProgramID == dto.ClimateProgramID &&
                        x.PillarID == dto.PillarID &&
                        x.QuestionID == dto.QuestionID &&
                        x.Year == dto.Year);

                if (entity == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Question score record not found." });

                entity.AIScore = dto.AIScore;
                entity.Discrepancy = CalculateDiscrepancy(entity.EvaluatorScore, dto.AIScore);
                entity.ConfidenceLevel = dto.ConfidenceLevel;
                entity.SourcesConsulted = dto.SourcesConsulted;
                entity.EvidenceSummary = dto.EvidenceSummary;
                entity.StructuralEvidence = dto.StructuralEvidence;
                entity.OperationalEvidence = dto.OperationalEvidence;
                entity.OutcomeEvidence = dto.OutcomeEvidence;
                entity.PerceptionEvidence = dto.PerceptionEvidence;
                entity.TemporalScope = dto.TemporalScope;
                entity.DistortionScreening = dto.DistortionScreening;
                entity.RelationalDependencies = dto.RelationalDependencies;
                entity.StressGeopoliticalShock = dto.StressGeopoliticalShock;
                entity.StressFinanceShock = dto.StressFinanceShock;
                entity.StressLegitimacyShock = dto.StressLegitimacyShock;
                entity.StressOverallResilienceShock = dto.StressOverallResilienceShock;
                entity.InclusionEquityAdjustment = dto.InclusionEquityAdjustment;
                entity.OpacityRisk = dto.OpacityRisk;
                entity.RedFlag = dto.RedFlag;
                entity.SourceType = dto.SourceType;
                entity.SourceName = dto.SourceName;
                entity.SourceURL = dto.SourceURL;
                entity.SourceDataYear = dto.SourceDataYear;
                entity.SourceHierarchyLevel = dto.SourceHierarchyLevel;
                entity.SourceDataExtract = dto.SourceDataExtract;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Question AI data updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAIEstimatedQuestionScore", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update question AI data." });
            }
        }

        #endregion ai score manual edit

        #endregion
    }
}
