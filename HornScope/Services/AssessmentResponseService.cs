using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using HornScope.Backgroundjob;
using HornScope.Common.Constants;
using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Common.Models.settings;
using HornScope.Data;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.dashboard;
using HornScope.Dtos.ProgramDto;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Services
{
    public class AssessmentResponseService : IAssessmentResponseService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly Download _download;
        private readonly ICommonService _commonService;
        private readonly AppSettings _appSettings;
        public AssessmentResponseService(ApplicationDbContext context, IAppLogger appLogger, Download download, ICommonService commonService,
            IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appLogger = appLogger;
            _download = download;
            _commonService = commonService;
            _appSettings = appSettings.Value;
        }

        public async Task<List<AssessmentResponse>> GetAllAsync()
        {
            try
            {
                return await _context.AssessmentResponses.ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllAsync", ex);
                return new List<AssessmentResponse>();
            }
        }
        public async Task<AssessmentResponse> GetByIdAsync(int id)
        {
            try
            {
                return await _context.AssessmentResponses.FindAsync(id);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync ", ex);
                return new AssessmentResponse();
            }

        }
        public async Task<AssessmentResponse> AddAsync(AssessmentResponse response)
        {
            try
            {
                _context.AssessmentResponses.Add(response);
                await _context.SaveChangesAsync();
                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddAsync", ex);
                return new AssessmentResponse();
            }
        }
        public async Task<AssessmentResponse> UpdateAsync(int id, AssessmentResponse response)
        {
            try
            {
                var existing = await _context.AssessmentResponses.FindAsync(id);
                if (existing == null) return null;
                existing.Score = response.Score;
                existing.Justification = response.Justification;
                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UpdateAsync", ex);
                return new AssessmentResponse();
            }

        }
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var resp = await _context.AssessmentResponses.FindAsync(id);
                if (resp == null) return false;
                _context.AssessmentResponses.Remove(resp);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure DeleteAsync", ex);
                return false;
            }

        }

        public async Task<ResultResponseDto<string>> SaveAssessment(AddAssessmentDto request)
        {
            try
            {
                var now = DateTime.Now;
                var assessment = await _context.Assessments
                    .Include(x=>x.StaffProgramMapping)
                    .Include(x => x.PillarAssessments)
                    .ThenInclude(x => x.Responses)
                    .FirstOrDefaultAsync(x =>
                        x.IsActive && x.UpdatedAt.Year == now.Year &&
                        (x.AssessmentID == request.AssessmentID ||
                         x.StaffProgramMappingID == request.StaffProgramMappingID));

                // If no assessment found, create a new one
                if (assessment == null)
                {
                    var ucm = await _context.StaffProgramMappings
                        .FirstOrDefaultAsync(x => x.StaffProgramMappingID == request.StaffProgramMappingID);

                    if (ucm == null)
                        return ResultResponseDto<string>.Failure(new[] { "Climate Program is not assigned" });

                    assessment = new Assessment
                    {
                        StaffProgramMappingID = ucm.StaffProgramMappingID,
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsActive = true,
                        StaffProgramMapping = ucm,
                        AssessmentPhase = AssessmentPhase.InProgress
                    };
                    _context.Assessments.Add(assessment);
                }
                if (assessment.AssessmentPhase == AssessmentPhase.Completed && request.PillarID != 22)
                {
                    return ResultResponseDto<string>.Failure(new[] { "Need approval to edit this pillar" });
                }


                if (request.PillarID > 0)
                {
                    var pillarAssessment = assessment.PillarAssessments
                        .FirstOrDefault(x => x.PillarID == request.PillarID);

                    if (pillarAssessment == null)
                    {
                        // Create new pillar assessment
                        pillarAssessment = new PillarAssessment
                        {
                            PillarID = request.PillarID,
                            Assessment = assessment
                        };
                        assessment.PillarAssessments.Add(pillarAssessment);
                    }

                    var existingResponses = pillarAssessment.Responses.ToList();
                    
                    if (!request.IsAutoSave) // removed if entire assessement is update for all responses
                    {
                        //var pillar = (await _commonService.GetPillars()).OrderByDescending(x => x.DisplayOrder).FirstOrDefault();
                        //assessment.AssessmentPhase = pillar?.PillarID == request.PillarID ? AssessmentPhase.Completed : AssessmentPhase.InProgress;

                        var requestResponseIds = request.Responses
                            .Where(r => r.QuestionID > 0)
                            .Select(r => r.QuestionID)
                            .ToHashSet();

                        var toDeleteList = existingResponses.Where(r => !requestResponseIds.Contains(r.QuestionID));

                        foreach (var existing in toDeleteList)
                        {
                            _context.AssessmentResponses.Remove(existing); // <-- delete instead of unlink
                        }
                    }

                    // ADD or UPDATE responses
                    foreach (var response in request.Responses)
                    {
                        var existing = existingResponses
                            .FirstOrDefault(r => r.ResponseID == response.ResponseID || r.QuestionID == response.QuestionID);

                        var scoreValue = _context.QuestionOptions
                            .Where(x => x.OptionID == response.QuestionOptionID)
                            .Select(x => x.ScoreValue)
                            .FirstOrDefault();

                        int? calculatedScore = null;
                        if (!string.IsNullOrEmpty(scoreValue) && 
                            !scoreValue.Equals("N/A", StringComparison.OrdinalIgnoreCase) && 
                            !scoreValue .Equals("Indeterminate", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(scoreValue, out int parsedScore))
                            {
                                calculatedScore = parsedScore;
                            }
                        }

                        if (existing == null && !string.IsNullOrEmpty(response.Justification))
                        {
                            // Add new
                            pillarAssessment.Responses.Add(new AssessmentResponse
                            {
                                QuestionID = response.QuestionID,
                                QuestionOptionID = response.QuestionOptionID,
                                Justification = response.Justification,
                                Source = response.Source,
                                UpdatedAt = now,
                                Score =  calculatedScore
                            });
                        }
                        else if(existing !=null)
                        {
                            // Update existing
                            existing.QuestionID = response.QuestionID;
                            existing.QuestionOptionID = response.QuestionOptionID;
                            existing.Justification = response.Justification;
                            existing.Score =  calculatedScore;
                            existing.Source = response.Source;
                            existing.UpdatedAt = now;
                        }
                    }
                    if (request.IsFinalized)
                    {
                        assessment.AssessmentPhase = AssessmentPhase.Completed;
                    }

                    assessment.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();

                if (assessment.AssessmentPhase == AssessmentPhase.Completed)
                {
                    _download.InsertAnalyticalLayerResults(assessment.StaffProgramMapping.ClimateProgramID);
                }

                return ResultResponseDto<string>.Success("", new[] { "Pillar saved successfully" }, assessment.AssessmentID);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in SaveAssessment", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to save assessment" });
            }
        }

        public async Task<PaginationResponse<GetProgramAssessmentResponseDto>> GetAssessmentResult(GetAssessmentRequestDto request, UserRole role)
        {
            try
            {
                List<int> allowedMappingIds = new();

                if (role != UserRole.Admin)
                {
                    IQueryable<StaffProgramMapping> mappingQuery =
                        _context.StaffProgramMappings.Where(x => !x.IsDeleted);

                    mappingQuery = role switch
                    {
                        UserRole.Analyst =>
                            request.SubUserID.HasValue
                                ? mappingQuery.Where(x => x.UserID == request.SubUserID.Value)
                                : mappingQuery.Where(x => x.AssignedByUserId == request.UserId),

                        UserRole.Evaluator =>
                            mappingQuery.Where(x => x.UserID == request.UserId),

                        _ => mappingQuery
                    };

                    allowedMappingIds = await mappingQuery
                        .Select(x => x.StaffProgramMappingID)
                        .ToListAsync();
                }
                var pillarCount = (await _commonService.GetPillars()).Count;

                var baseRecords = await (
                  from a in _context.Assessments
                  where a.IsActive
                        && (!request.ClimateProgramID.HasValue || a.StaffProgramMapping.ClimateProgramID == request.ClimateProgramID.Value)
                        && (role == UserRole.Admin || allowedMappingIds.Contains(a.StaffProgramMappingID))

                  join c in _context.ClimatePrograms.Where(x => !x.IsDeleted)
                      on a.StaffProgramMapping.ClimateProgramID equals c.ClimateProgramID

                  join u in _context.Users.Where(x =>
                          !x.IsDeleted &&
                          (!request.Role.HasValue || x.Role == request.Role.Value))
                      on a.StaffProgramMapping.UserID equals u.UserID

                  join createdBy in _context.Users.Where(x => !x.IsDeleted)
                      on a.StaffProgramMapping.AssignedByUserId equals createdBy.UserID

                      select new
                      {
                          a.AssessmentID,
                          a.StaffProgramMappingID,
                          a.CreatedAt,
                          a.AssessmentPhase,
                          c.ClimateProgramID,
                          c.ProgramName,
                          u.UserID,
                          a.UpdatedAt,
                          Role = (UserRole)u.Role,
                          u.FullName,
                          AssignedByUser = createdBy.FullName,
                          AssignedByUserId = createdBy.UserID
                        })
                    .ToListAsync();

                if (baseRecords.Count == 0)
                {
                    return new PaginationResponse<GetProgramAssessmentResponseDto>
                    {
                        Data = new List<GetProgramAssessmentResponseDto>(),
                        TotalRecords = 0,
                        PageNumber = request.PageNumber,
                        PageSize = request.PageSize
                    };
                }

                var assessmentIds = baseRecords.Select(x => x.AssessmentID).ToList();
                var responsesByPillar = await (
                        from r in _context.AssessmentResponses
                        where assessmentIds.Contains(r.PillarAssessment.AssessmentID)
                        select new
                        {
                            r.PillarAssessment.AssessmentID,
                            r.PillarAssessment.PillarID,
                            r.Score,
                            ClimateProgramID = r.PillarAssessment.Assessment.StaffProgramMapping.ClimateProgramID,
                            r.QuestionOptionID,
                            r.QuestionID,
                            Weight = r.Question.Weight,
                            Option = r.Question.QuestionOptions
                            .Where(o => o.OptionID == r.QuestionOptionID)
                            .Select(o => new
                            {
                                o.OptionText,
                                o.ScoreValue 
                            }).FirstOrDefault()
                        })
                    .ToListAsync();

                var results = baseRecords.Select(b =>
                {
                    var responses = responsesByPillar
                        .Where(r => r.AssessmentID == b.AssessmentID)
                        .ToList();

                    var scoredResponses = responses
                    .Where(r => r.Score.HasValue)
                    .Select(r => new
                    {
                        r.PillarID,
                        r.ClimateProgramID,
                        Score = r.Score!.Value,
                        Weight = r.Weight
                    })
                    .ToList();


                    // ? NA and Indeterminate counts
                    var totalNA = responses.Count(r =>
                        !string.IsNullOrEmpty(r.Option?.ScoreValue) &&
                        (r.Option?.ScoreValue == "N/A" || r.Option?.ScoreValue == "NA"));

                    var totalIndeterminate = responses.Count(r =>
                        !string.IsNullOrEmpty(r.Option?.ScoreValue) &&
                        r.Option?.ScoreValue == "Indeterminate");

                    var pillarScores = scoredResponses
                    .GroupBy(r => new { r.PillarID, r.ClimateProgramID }).Select(g =>
                    {
                        // Step 1 & 2: Σ(Score × Weight)
                        var weightedScoreSum = g.Sum(r => (r.Score * r.Weight));
                        
                        // Step 3: Σ(Weight)
                        var totalWeight = g.Sum(r => r.Weight);
                        if (totalWeight <= 0) return 0m;

                        var criticalFailureCount = g.Count(r =>
                            r.Weight == Constants.CriticalIndicatorWeight &&
                            r.Score <= Constants.LeastCriticalIndicatorValue);
                        var criticalFailurePenalty = CommonStaticMethods.GetCriticalFailurePenalty(criticalFailureCount);

                        // Step 4: Average on -4 to +4 scale
                        var pillarAvg = weightedScoreSum / totalWeight;
                        // Step 5: Convert to 0-100 scale
                        var pillarScore = (((decimal)pillarAvg + 4m) / 8m) * 100m;
                       
                        return pillarScore - criticalFailurePenalty;
                    }).ToList();

                    // Overall Score = SUM(PillarScores) / TotalPillars
                    var overallScore = pillarCount > 0
                        ? Math.Round(pillarScores.Sum() / pillarCount, 2)
                        : 0m;


                    return new GetProgramAssessmentResponseDto
                    {
                        AssessmentID = b.AssessmentID,
                        ClimateProgramID = b.ClimateProgramID,
                        StaffProgramMappingID = b.StaffProgramMappingID,
                        CreatedAt = b.CreatedAt,
                        ProgramName = b.ProgramName ?? "",
                        UserID = b.UserID,
                        UserName = b.FullName ?? "",
                        UserRole = b.Role.ToString(),
                        AssignedByUser = b.AssignedByUser ?? "",
                        AssignedByUserId = b.AssignedByUserId,
                        AssessmentPhase = b.AssessmentPhase,
                        Score = overallScore,
                        TotalNA = totalNA,
                        TotalIndeterminate = totalIndeterminate
                    };
                }).ToList();

                var totalRecords = results.Count;
                var data = results
                    .OrderByDescending(x => x.Score)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                return new PaginationResponse<GetProgramAssessmentResponseDto>
                {
                    Data = data,
                    TotalRecords = totalRecords,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAssessmentResult", ex);

                return new PaginationResponse<GetProgramAssessmentResponseDto>
                {
                    Data = new List<GetProgramAssessmentResponseDto>(),
                    PageNumber = 1,
                    PageSize = 10,
                    TotalRecords = 0
                };
            }
        }
        public async Task<PaginationResponse<GetAssessmentQuestionResponseDto>> GetAssessmentQuestion(GetAssessmentQuestionRequestDto request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserID == request.UserId);
                if (user == null) return null;

                var userIDs = new List<int>();
                var query = _context.Assessments    
                    .Include(a => a.PillarAssessments)
                    .ThenInclude(pa => pa.Responses)
                        .ThenInclude(r => r.Question)
                            .ThenInclude(q => q.QuestionOptions)
                    .Where(a => a.AssessmentID == request.AssessmentID)
                    .SelectMany(a => a.PillarAssessments)
                    .Where(x => !request.PillarID.HasValue || x.PillarID == request.PillarID.Value)
                    .SelectMany(x => x.Responses)
                    .Select(r => new GetAssessmentQuestionResponseDto
                    {
                        AssessmentID = request.AssessmentID,
                        PillerID = r.PillarAssessment.PillarID,
                        PillarName = r.Question.Pillar.PillarName,
                        QuestoinID = r.QuestionID,
                        Score = (ScoreValue)r.Score,
                        UserID = user.UserID,
                        Justification = r.Justification,
                        Source = r.Source ?? "",
                        QuestionOptionText = r.Question.QuestionOptions
                            .Where(o => o.OptionID == r.QuestionOptionID)
                            .Select(o => o.OptionText)
                            .FirstOrDefault() ?? string.Empty,
                        QuestionText = r.Question.QuestionText
                    });

                var response = await query.ApplyPaginationAsync(request);

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAssessmentQuestion", ex);
                return new PaginationResponse<GetAssessmentQuestionResponseDto>
                {
                    Data = new List<GetAssessmentQuestionResponseDto>(),
                    PageNumber = 1,
                    PageSize = 10,
                    TotalRecords = 0
                };
            }
        }
        private const int FIRST_Q_ROW = 9;
        private const int ROWS_PER_Q = 4;
        public async Task<ResultResponseDto<string>> ImportAssessmentAsync(IFormFile file, int userID)
        {
            try
            {
                // Load all options once
                var allOptions = _context.QuestionOptions.ToList();
                int recordSaved = 0;

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);

                using var workbook = new XLWorkbook(stream);

                foreach (var ws in workbook.Worksheets)
                {
                    // Skip the hidden options data sheet
                    if (ws.Name.StartsWith("__")) continue;

                    // -- Read meta from first question's source row ----
                    // First question: ansRow=9, sourceRow=9+2=11
                    int staffProgramMappingID = ws.Cell(11, 11).GetValue<int>();
                    int pillarID = ws.Cell(11, 12).GetValue<int>();

                    if (staffProgramMappingID == 0 || pillarID == 0)
                        continue; // empty or corrupt sheet - skip

                    // Validate that the file belongs to the uploading user
                    if (!_context.StaffProgramMappings.Any(x =>
                            !x.IsDeleted &&
                            x.UserID == userID &&
                            x.StaffProgramMappingID == staffProgramMappingID))
                    {
                        return ResultResponseDto<string>.Failure(new[] { "Invalid file uploaded" });
                    }

                    int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                    var assessmentResponses = new List<AddAssesmentResponseDto>();

                    // -- Walk question blocks (4 rows each, starting row 9) --
                    for (int row = FIRST_Q_ROW; row <= lastRow - 2; row += ROWS_PER_Q)
                    {
                        int sourceRow = row + 2;

                        int questionID = ws.Cell(sourceRow, 13).GetValue<int?>() ?? 0;
                        int responseID = ws.Cell(sourceRow, 15).GetValue<int?>() ?? 0;

                        // Once we reach rows without question IDs we're past the questions
                        if (questionID == 0) break;

                        string answerText = ws.Cell(row, 4).GetString().Trim(); // dropdown value
                        string comment = ws.Cell(row + 1, 4).GetString().Trim(); // comment
                        string source = ws.Cell(row + 2, 4).GetString().Trim(); // source

                        int? score = null;
                        int matchedOptionID = 0;

                        var qOptions = allOptions.Where(x => x.QuestionID == questionID).ToList();

                        if (!string.IsNullOrWhiteSpace(answerText))
                        {
                            // 1. Exact full-text match against "N - Option text" or plain option text
                            foreach (var opt in qOptions)
                            {
                                string prefix = !string.IsNullOrEmpty(opt.ScoreValue) ? $"{opt.ScoreValue} - " : "";
                                string fullText = (prefix + opt.OptionText.Trim()).Trim();

                                if (fullText.Equals(answerText, StringComparison.OrdinalIgnoreCase) ||
                                    opt.OptionText.Trim().Equals(answerText, StringComparison.OrdinalIgnoreCase))
                                {
                                    matchedOptionID = opt.OptionID;
                                    var scoreValue = _context.QuestionOptions.Where(x => x.OptionID == opt.OptionID).Select(x => x.ScoreValue).FirstOrDefault();
                                    if (!string.IsNullOrEmpty(scoreValue) && !scoreValue.Equals("N/A", StringComparison.OrdinalIgnoreCase) && !scoreValue.Equals("Indeterminate", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (int.TryParse(scoreValue, out int parsedScore))
                                        {
                                            score = parsedScore;
                                        }
                                    }
                                    break;
                                }
                            } 
                        }

                        if (matchedOptionID > 0)
                        {
                            assessmentResponses.Add(new AddAssesmentResponseDto
                            {
                                AssessmentID = 0,
                                QuestionID = questionID,
                                ResponseID = responseID,
                                QuestionOptionID = matchedOptionID,
                                Score = score.HasValue ? score : null,
                                Justification = comment,
                                Source = string.IsNullOrWhiteSpace(source) ? null : source
                            });
                        }
                    }

                    // -- Save this pillar's responses ------------------
                    var assessment = new AddAssessmentDto
                    {
                        AssessmentID = 0,
                        StaffProgramMappingID = staffProgramMappingID,
                        PillarID = pillarID,
                        Responses = assessmentResponses
                    };

                    var saveResult = await SaveAssessment(assessment);
                    if (!saveResult.Succeeded)
                        return saveResult;

                    recordSaved++;
                }

                return ResultResponseDto<string>.Success("", new[]
                {
                    recordSaved > 0
                    ? $"{recordSaved} Pillar(s) Assessment saved successfully"
                        : "Please fill the sheet properly before submitting"
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in ImportAssessmentAsync", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to save assessment" });
            }
        }
        public async Task<GetProgramQuestionHistoryResponseDto> GetProgramQuestionHistory(UserProgramRequestDto userProgramRequestDto)
        {
            try
            {
                var userID = userProgramRequestDto.UserID;
                var programID = userProgramRequestDto.ClimateProgramID;

                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userID && x.Role != UserRole.ProgramUser);
                if (user == null)
                {
                    return new GetProgramQuestionHistoryResponseDto
                    {
                        ClimateProgramID = programID,
                        Score = 0,
                        TotalPillar = 0,
                        TotalAnsPillar = 0,
                        TotalQuestion = 0,
                        AnsQuestion = 0,
                        TotalAssessment = 0,
                        Pillars = new List<ProgramPillarQuestionHistoryResponseDto>()
                    };
                }
                var programHistory = new ProgramHistoryDto();

                Expression<Func<StaffProgramMapping, bool>> predicate = user.Role switch
                {
                    UserRole.Analyst => x => !x.IsDeleted && x.ClimateProgramID == programID && (x.AssignedByUserId == userID || x.UserID == userID),
                    UserRole.Evaluator => x => !x.IsDeleted && x.ClimateProgramID == programID && x.UserID == userID,
                    _ => x => !x.IsDeleted && x.ClimateProgramID == programID
                };

                // 1. Get all StaffProgramMapping IDs for the program
                var ucmIds = await _context.StaffProgramMappings
                    .Where(predicate)
                    .Select(x => x.StaffProgramMappingID)
                    .ToListAsync();

                var pillarAssessments = _context.Assessments
                    .Where(a => ucmIds.Contains(a.StaffProgramMappingID) && a.IsActive)
                    .SelectMany(x => x.PillarAssessments);

                // 2. Fetch program-wise pillar/question details, now pulling each response'stier weight (from Question.Weight) so we can compute a weighted average
                //    instead of a plain average. Indeterminate/N/A responses (Score == null) are still excluded from both numerator and denominator.
                var programPillarQuery =
                    from p in _context.Pillars.Where(x => !x.IsDeleted)
                    join pa in pillarAssessments on p.PillarID equals pa.PillarID into paGroup
                    from pa in paGroup.DefaultIfEmpty()
                    select new
                    {
                        p.PillarID,
                        p.PillarName,
                        UserID = pa != null && pa.Responses
                                .Where(r => r.Score.HasValue)
                                .Count() > 0 ? pa.Assessment.StaffProgramMapping.UserID : 0,

                        // Σ (Score × Weight) for this assessment's answered responses
                        WeightedScoreSum = pa != null
                            ? pa.Responses
                                .Where(r => r.Score.HasValue)
                                .Sum(r => (decimal?)(r.Score.Value * r.Question.Weight)) ?? 0m
                            : 0m,

                        // Σ (Weight) for this assessment's answered responses
                        WeightSum = pa != null
                            ? pa.Responses
                                .Where(r => r.Score.HasValue)
                                .Sum(r => (decimal?)r.Question.Weight) ?? 0m
                            : 0m,

                        ScoreCount = pa != null ? pa.Responses.Where(r => r.Score.HasValue).Count() : 0,
                        TotalQuestion = p.Questions.Count(x => !x.IsDeleted),
                        AnsQuestion = pa != null ? pa.Responses.Count() : 0,
                        HasAnswer = pa != null
                    };

                var list = await programPillarQuery.Distinct().ToListAsync();

                var programPillars = list
                    .GroupBy(x => new { x.PillarID, x.PillarName })
                    .Select(g =>
                    {
                        var totalWeightedScore = g.Sum(x => x.WeightedScoreSum);   // Σ (Score × Weight)
                        var totalWeight = g.Sum(x => x.WeightSum);                 // Σ (Weight)
                        var ansUserCount = g.Where(x => x.UserID > 0).Distinct().Count();
                        var totalQuestionsInPillar = g.Max(x => x.TotalQuestion) * ansUserCount;

                        // Step 4: weighted average on the -4..+4 scale
                        decimal pillarAvgRaw = totalWeight != 0m
                            ? totalWeightedScore / totalWeight
                            : 0m;

                        // Step 5: convert -4..+4 average to a 0-100 pillar score
                        decimal pillarScore0to100 = totalWeight != 0m
                            ? ((pillarAvgRaw + 4m) / 8m) * 100m
                            : 0m;

                        return new ProgramPillarQuestionHistoryResponseDto
                        {
                            PillarID = g.Key.PillarID,
                            PillarName = g.Key.PillarName,
                            Score = totalWeightedScore,     
                            ScoreProgress = pillarScore0to100,
                            AnsPillar = g.Sum(x => x.HasAnswer ? 1 : 0),
                            TotalQuestion = totalQuestionsInPillar,
                            AnsQuestion = g.Sum(x => x.AnsQuestion)
                        };
                    })
                    .ToList();

                // 5. Final payload
                var payload = new GetProgramQuestionHistoryResponseDto
                {
                    ClimateProgramID = programID,
                    ScoreProgress = programPillars.Count > 0 ? programPillars.Average(x => x.ScoreProgress) : 0m,
                    Pillars = programPillars
                };

                return payload;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetProgramQuestionHistory", ex);
                return new GetProgramQuestionHistoryResponseDto
                {
                    ClimateProgramID = 0,
                    Score = 0,
                    TotalPillar = 0,
                    TotalAnsPillar = 0,
                    TotalQuestion = 0,
                    AnsQuestion = 0,
                    TotalAssessment = 0,
                    Pillars = new List<ProgramPillarQuestionHistoryResponseDto>()
                };
            }
        }

        public async Task<ResultResponseDto<GetAssessmentHistoryDto>> GetAssessmentProgressHistory(GetProgramProgressHistoryRequestDto progressHistoryRequest)
        {
            try
            {
                // Fetch assessment with pillars & responses in one query
                var assessment = await _context.Assessments
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(pa => pa.Responses)
                    .FirstOrDefaultAsync(a => a.AssessmentID == progressHistoryRequest.AssessmentID || a.StaffProgramMappingID == progressHistoryRequest.StaffProgramMappingID);
                
                // Get total questions directly (avoid Include if not needed)
                var totalQuestions = await _context.Questions.Where(x=>!x.IsDeleted).CountAsync();
                var totalPillars = (await _commonService.GetPillars()).Count;

                if (assessment == null)
                {
                    var emptyResult = new GetAssessmentHistoryDto
                    {
                        AssessmentID = progressHistoryRequest.AssessmentID,
                        Score = 0,
                        TotalPillar = totalPillars,
                        TotalAnsPillar = 0,
                        TotalAnsQuestion = 0,
                        TotalQuestion = totalQuestions,
                        CurrentProgress = 0
                    };

                    return ResultResponseDto<GetAssessmentHistoryDto>.Success(emptyResult, new[] { "No assessment found. Returning default progress." });
                }


                // Calculate answered questions
                var totalAnsweredQuestions = assessment.PillarAssessments
                    .SelectMany(pa => pa.Responses)
                    .Count();

                // Calculate score (sum only valid scores <= Score1)
                var score = assessment.PillarAssessments
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r.Score.HasValue && r.Score.Value <= (int)ScoreValue.Score1)
                    .Sum(r => r.Score.Value);


                // Build response
                var result = new GetAssessmentHistoryDto
                {
                    AssessmentID = progressHistoryRequest.AssessmentID,
                    Score = score,
                    TotalPillar = totalPillars,
                    TotalAnsPillar = assessment.PillarAssessments.Count,
                    TotalAnsQuestion = totalAnsweredQuestions,
                    TotalQuestion = totalQuestions,
                    CurrentProgress = totalQuestions > 0
                        ? Math.Round((totalAnsweredQuestions / (double)totalQuestions) * 100)
                        : 0
                };

                return ResultResponseDto<GetAssessmentHistoryDto>.Success(result, new[] { "Assessment history fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetAssessmentProgressHistory", ex);
                return ResultResponseDto<GetAssessmentHistoryDto>.Failure(new[] { "Failed to get assessment history" });

            }
        }

        public async Task<ResultResponseDto<string>> ChangeAssessmentStatus(ChangeAssessmentStatusRequestDto r)
        {
            try
            {
                var assessment = await _context.Assessments.FirstOrDefaultAsync(x=>x.AssessmentID == r.AssessmentID);
                if(assessment != null)
                {
                    assessment.AssessmentPhase = r.AssessmentPhase;

                    _context.Assessments.Update(assessment);
                    await _context.SaveChangesAsync();

                    return ResultResponseDto<string>.Success("", new[] { "Assessment Status Changed successfully" });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangeAssessmentStatus", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to Changed assessment status" });

            }
            return ResultResponseDto<string>.Failure(new[] { "Failed to Changed assessment status" });
        }

        public async Task<ResultResponseDto<string>> TransferAssessment(TransferAssessmentRequestDto r, int userID, UserRole userRole)
        {
            try
            {
                var currentDate = DateTime.Now;

                var transferAssessment = await _context.Assessments
                    .Include(x => x.StaffProgramMapping)
                    .Include(x => x.PillarAssessments)
                        .ThenInclude(x => x.Responses)
                    .FirstOrDefaultAsync(x => x.AssessmentID == r.AssessmentID);

                if (transferAssessment == null)
                    return ResultResponseDto<string>.Failure(new[] { "Invalid assessment." });

                var programAssigned = await _context.StaffProgramMappings
                    .FirstOrDefaultAsync(x => x.ClimateProgramID == transferAssessment.StaffProgramMapping.ClimateProgramID &&
                                              x.UserID == r.TransferToUserID);

                if (programAssigned == null)
                    return ResultResponseDto<string>.Failure(new[] { "This assessment can't be imported because the selected user hasn't been assigned to this program yet." });

                // Load existing assessment for that user/program/year (with pillars/responses)
                var existingAssessment = await _context.Assessments
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(p => p.Responses)
                    .FirstOrDefaultAsync(a => a.StaffProgramMappingID == programAssigned.StaffProgramMappingID &&
                                              a.UpdatedAt.Year == currentDate.Year);

                if (existingAssessment == null)
                {
                    existingAssessment = new Assessment
                    {
                        StaffProgramMappingID = programAssigned.StaffProgramMappingID,
                        CreatedAt = currentDate,
                        UpdatedAt = currentDate,
                        IsActive = true,
                        AssessmentPhase = transferAssessment.AssessmentPhase == AssessmentPhase.Completed ? transferAssessment.AssessmentPhase : AssessmentPhase.InProgress,
                        PillarAssessments = new List<PillarAssessment>()
                    };

                    _context.Assessments.Add(existingAssessment);
                }
                else if (existingAssessment.AssessmentPhase == AssessmentPhase.Completed && userRole != UserRole.Admin)
                {
                    return ResultResponseDto<string>.Failure(new[] { "Need approval for this assessment , Please send request to admin to edit" });
                }
                else
                {
                    existingAssessment.UpdatedAt = currentDate;
                    existingAssessment.AssessmentPhase = transferAssessment.AssessmentPhase == AssessmentPhase.Completed ? transferAssessment.AssessmentPhase : AssessmentPhase.InProgress;
                }

                // Transfer pillar data
                foreach (var pillar in transferAssessment.PillarAssessments)
                {
                    var existingPillar = existingAssessment.PillarAssessments
                        .FirstOrDefault(x => x.PillarID == pillar.PillarID);

                    if (existingPillar == null)
                    {
                        existingPillar = new PillarAssessment
                        {
                            PillarID = pillar.PillarID,
                            Responses = new List<AssessmentResponse>()
                        };
                        existingAssessment.PillarAssessments.Add(existingPillar);
                    }

                    // Add/Update responses
                    foreach (var response in pillar.Responses)
                    {
                        var existingResponse = existingPillar.Responses
                            .FirstOrDefault(rp => rp.QuestionID == response.QuestionID);

                        if (existingResponse == null)
                        {
                            existingPillar.Responses.Add(new AssessmentResponse
                            {
                                QuestionID = response.QuestionID,
                                QuestionOptionID = response.QuestionOptionID,
                                Justification = response.Justification,
                                Score = response.Score,
                                Source =response.Source
                            });
                        }
                        else
                        {
                            existingResponse.QuestionOptionID = response.QuestionOptionID;
                            existingResponse.Justification = response.Justification;
                            existingResponse.Score = response.Score;
                        }
                    }

                    // Delete responses not present in transferAssessment
                    var transferQuestionIds = pillar.Responses.Select(x => x.QuestionID).ToHashSet();
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
                var transferPillarIds = transferAssessment.PillarAssessments.Select(x => x.PillarID).ToHashSet();
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
                    _download.InsertAnalyticalLayerResults(transferAssessment.StaffProgramMapping.ClimateProgramID);
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
        public async Task<ResultResponseDto<AiProgramPillarDashboardResponseDto>> GetProgramPillarHistory(UserProgramDashBoardRequestDto request, int userId, UserRole userRole)
        {
            try
            {
                int pillarCount = (await _commonService.GetPillars()).Count;
                // 1. Validate program access
                var hasAccess = await _context.StaffProgramMappings
                    .AnyAsync(x =>
                        !x.IsDeleted &&
                        (userRole == UserRole.Admin ||
                         (x.UserID == userId && x.ClimateProgramID == request.ClimateProgramID)));

                if (!hasAccess)
                {
                    return ResultResponseDto<AiProgramPillarDashboardResponseDto>
                        .Failure(new[] { "Unauthorized or invalid program access" });
                }

                // 2. Fetch required data in parallel
                var pillarEvaluationsList = await _commonService
                    .GetProgramProgressAsync(userId, (int)userRole, request.ClimateProgramID);

                var pillars = await _commonService.GetPillars();

                var aiProgramProgress = await _context.AIProgramScores
                    .Where(x => x.ClimateProgramID == request.ClimateProgramID)
                    .MaxAsync(x => x.AIProgress);

                var program = await _context.ClimatePrograms
                    .AsNoTracking()
                    .Where(x => x.ClimateProgramID == request.ClimateProgramID)
                    .Select(x => new { x.ClimateProgramID, x.ProgramName })
                    .FirstOrDefaultAsync();

                 var pillarEvaluations = pillarEvaluationsList.Where(x=>x.ClimateProgramID == request.ClimateProgramID);

                // 3. Map pillar results
                var pillarResults = pillars
                    .GroupJoin(
                        pillarEvaluations,
                        p => p.PillarID,
                        e => e.PillarID,
                        (pillar, evals) => new ProgramPillarDashboardPillarValueDto
                        {
                            PillarID = pillar.PillarID,
                            PillarName = pillar.PillarName,
                            DisplayOrder = pillar.DisplayOrder,
                            AiValue = evals.FirstOrDefault()?.AIProgress ?? 0,
                            EvaluationValue = evals.FirstOrDefault()?.ScoreProgress ?? 0
                        })
                    .ToList();

                // 4. Prepare response
                var response = new AiProgramPillarDashboardResponseDto
                {
                    ClimateProgramID = request.ClimateProgramID,
                    ProgramName = program?.ProgramName ?? string.Empty,
                    AiValue = aiProgramProgress ?? 0,
                    EvaluationValue = Math.Round(pillarEvaluations.Select(x => x.ScoreProgress).DefaultIfEmpty(0).Sum()/pillarCount, 2),
                    Pillars = pillarResults
                };

                return ResultResponseDto<AiProgramPillarDashboardResponseDto>
                    .Success(response, new[] { "Pillars fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(nameof(GetProgramPillarHistory), ex);

                return ResultResponseDto<AiProgramPillarDashboardResponseDto>
                    .Failure(new[] { "Error in getting pillar details" });
            }
        }
    }
}