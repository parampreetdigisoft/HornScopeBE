
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using HornScope.Common.Implementation;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.AiDto;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.ProgramDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.kpiDto;
using HornScope.Dtos.PublicDto;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;
using System.Text.RegularExpressions;
using HornScope.Dtos.ClientDto;
using HornScope.Common.Interface;

namespace HornScope.Services
{
    public class ClientService : IClientService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;
        public ClientService(ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
        }

        public async Task<List<Pillar>> GetAllAsync(int userId, UserRole userRole)
        {
            try
            {
                var userPillar = await _context.ClientPillarMappings
                      .Where(x => x.IsActive && x.UserID == userId)
                      .Select(x => x.Pillar)
                      .Where(x => x != null)
                      .Distinct()
                      .ToListAsync();

                return userPillar!.Where(x => x != null).ToList()!;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllAsync", ex);
                return new List<Pillar>();
            }

        }
        public async Task<ResultResponseDto<ProgramHistoryDto>> GetProgramHistory(int userId, TieredAccessPlan tier)
        {
            try
            {
                int allowedPillars = tier switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 0
                };

                var accessibleClimateProgramIDs = await _context.ClientProgramMappings
                    .AsNoTracking()
                    .Where(x => x.UserID == userId && x.IsActive)
                    .Select(x => x.ClimateProgramID)
                    .ToListAsync();

                if (!accessibleClimateProgramIDs.Any())
                {
                    return ResultResponseDto<ProgramHistoryDto>.Failure(new List<string> { "No programs available for user" });
                }

                var verifiedProgramScores = await _context.AIProgramScores
                    .AsNoTracking()
                    .Where(x =>
                        accessibleClimateProgramIDs.Contains(x.ClimateProgramID) &&
                        x.IsVerified)
                    .Select(x => x.AIProgress)
                    .ToListAsync();

                var programHistory = new ProgramHistoryDto
                {
                    TotalProgram = accessibleClimateProgramIDs.Count,
                    TotalAccessProgram = accessibleClimateProgramIDs.Count,
                    ActiveProgram = verifiedProgramScores.Count
                };

                if (verifiedProgramScores.Any())
                {
                    programHistory.AvgHighScore = verifiedProgramScores.Max() ?? 0;
                    programHistory.AvgLowerScore = verifiedProgramScores.Min() ?? 0;
                    programHistory.OverallVitalityScore = verifiedProgramScores.Average() ?? 0;
                }

                return ResultResponseDto<ProgramHistoryDto>.Success(programHistory,new List<string> { "Get history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetProgramHistory", ex);
                return ResultResponseDto<ProgramHistoryDto>.Failure(new[] { "There is an error, please try later" });
            }
        }


        public async Task<GetProgramQuestionHistoryResponseDto> GetProgramQuestionHistory(UserProgramRequestDto request)
        {
            try
            {
                int userId = request.UserID;
                int ClimateProgramID = request.ClimateProgramID;

                int allowedPillars = request.Tiered switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 0
                };

                // ?? Fetch accessible pillar IDs
                var accessiblePillarIds = await _context.ClientPillarMappings
                    .AsNoTracking()
                    .Where(x => x.UserID == userId)
                    .OrderBy(x => x.PillarID)
                    .Select(x => x.PillarID)
                    .Take(allowedPillars)
                    .ToListAsync();

                var accessiblePillarSet = accessiblePillarIds.ToHashSet();

                // ?? Fetch program score once
                var programScore = await _context.AIProgramScores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ClimateProgramID == ClimateProgramID && x.IsVerified);

                if (programScore == null)
                {
                    return new GetProgramQuestionHistoryResponseDto();
                }
                // ?? Fetch pillars
                var pillars = await _commonService.GetPillars();

                // ?? Fetch pillar scores and map for O(1) lookup
                var pillarScoreMap = await _context.AIPillarScores
                    .AsNoTracking()
                    .Where(x => x.ClimateProgramID == ClimateProgramID)
                    .ToDictionaryAsync(x => x.PillarID);

                // ?? Build DTOs
                var pillarDtos = pillars
                    .Select(p =>
                    {
                        bool isAccess = accessiblePillarSet.Contains(p.PillarID);
                        pillarScoreMap.TryGetValue(p.PillarID, out var aiScore);

                        return new ProgramPillarQuestionHistoryResponseDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName,
                            ImagePath = p.ImagePath,
                            IsAccess = isAccess,
                            Score = isAccess ? aiScore?.AIProgress ?? 0 : 0,
                            ScoreProgress = isAccess ? aiScore?.AIProgress ?? 0 : 0,
                            DisplayOrder = p.DisplayOrder // optional if DTO supports it
                        };
                    })
                    .OrderByDescending(x => x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();

                return new GetProgramQuestionHistoryResponseDto
                {
                    ClimateProgramID = ClimateProgramID,
                    TotalAssessment = pillarScoreMap.Count,
                    Score = programScore?.AIProgress ?? 0,
                    ScoreProgress = programScore?.AIProgress ?? 0,
                    Pillars = pillarDtos
                };
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetProgramQuestionHistory (Optimized)", ex);
                return new GetProgramQuestionHistoryResponseDto();
            }
        }


        public async Task<PaginationResponse<ProgramResponseDto>> GetProgramAsync(PaginationRequest request)
        {
            try
            {
                var programScores = from ac in _context.AIProgramScores
                                   .Where(x => x.IsVerified)
                                 join pc in _context.ClientProgramMappings on ac.ClimateProgramID equals pc.ClimateProgramID
                                 group ac by ac.ClimateProgramID into g
                                 select new
                                 {
                                     ClimateProgramID = g.Key,
                                     Score = g.Average(x => (decimal?)x.AIProgress) ?? 0
                                 };


                // ? Fetch programs mapped to the user
                var query =
                    from c in _context.ClimatePrograms.AsNoTracking()
                    join pc in _context.ClientProgramMappings.AsNoTracking()
                        on c.ClimateProgramID equals pc.ClimateProgramID

                    join s in programScores on pc.ClimateProgramID equals s.ClimateProgramID into scores
                    from s in scores.DefaultIfEmpty() // Left join
                    where !c.IsDeleted && pc.IsActive && pc.UserID == request.UserId
                    select new ProgramResponseDto
                    {
                        ClimateProgramID = c.ClimateProgramID,
                        ProgramName = c.ProgramName,
                        Year = c.Year,
                        Location = c.Location,
                        Image = c.Image,
                        IsActive = c.IsActive,
                        Score = s.Score
                    };


                // ? Apply search filter
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string search = request.SearchText.ToLower();
                    query = query.Where(x => x.ProgramName.ToLower().Contains(search) || x.Location.ToLower().Contains(search));
                }

                // ? Apply ordering and pagination
                var pagedResult = await query.ApplyPaginationAsync(request);


                return pagedResult;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetProgramsAsync", ex);
                return new PaginationResponse<ProgramResponseDto>();
            }
        }
        public async Task<ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>> GetProgramProgressByUserId(int userID)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == userID && x.Role == UserRole.ProgramUser && !x.IsDeleted);

                if (user == null)
                    return ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>.Failure(new[] { "Invalid request" });

                var date = DateTime.Now;

                // Get total pillars and questions
                var pillarStats = await _context.Pillars.Where(x => x.IsActive && !x.IsDeleted)
                    .Select(p => new { p.PillarID, QuestionsCount = p.Questions.Count })
                    .ToListAsync();

                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                // Determine allowed pillars based on tier
                var pillarPredicate = user.Tier switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 15
                };

                var allowedPillarIds = pillarStats
                    .Where(p => p.PillarID < pillarPredicate)
                    .Select(p => p.PillarID)
                    .ToHashSet();

                // Query data with joins and projection
                var programSubmission = await (
                    from uc in _context.StaffProgramMappings
                    where !uc.IsDeleted
                    join c in _context.ClimatePrograms.Where(c => !c.IsDeleted && c.IsActive)
                        on uc.ClimateProgramID equals c.ClimateProgramID
                    join a in _context.Assessments.Where(a => a.IsActive && a.UpdatedAt.Year == date.Year)
                        on uc.StaffProgramMappingID equals a.StaffProgramMappingID into assessments
                    from a in assessments.DefaultIfEmpty()
                    select new
                    {
                        c.ClimateProgramID,
                        c.ProgramName,
                        AssessmentID = (int?)a.AssessmentID,
                        PillarAssessments = a.PillarAssessments.Where(pa => allowedPillarIds.Contains(pa.PillarID)),
                        Responses = a.PillarAssessments
                                      .Where(pa => allowedPillarIds.Contains(pa.PillarID))
                                      .SelectMany(pa => pa.Responses)
                    }
                )
                .AsNoTracking()
                .ToListAsync();

                // Group by program and calculate metrics
                var result = programSubmission
                    .GroupBy(g => new { g.ClimateProgramID, g.ProgramName })
                    .Select(g =>
                    {
                        var allPillars = g.SelectMany(x => x.PillarAssessments).ToList();
                        var aspIds = allPillars.Select(x => x.PillarAssessmentID).ToHashSet();
                        var allResponses = g.SelectMany(x => x.Responses).Where(r => aspIds.Contains(r.PillarAssessmentID)).ToList();

                        var scoreList = allResponses
                            .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Score1)
                            .Select(r => (int?)r.Score ?? 0);

                        int userProgramMappingCount = g.Count();

                        return new GetProgramsSubmissionHistoryResponseDto
                        {
                            ClimateProgramID = g.Key.ClimateProgramID,
                            ProgramName = g.Key.ProgramName,
                            TotalAssessment = g.Select(x => x.AssessmentID).Where(id => id.HasValue).Distinct().Count(),
                            Score = allResponses.Sum(r => (int?)r.Score ?? 0),
                            TotalPillar = totalPillars * userProgramMappingCount,
                            TotalAnsPillar = allPillars.Count,
                            TotalQuestion = totalQuestions * userProgramMappingCount,
                            AnsQuestion = allResponses.Count,
                            ScoreProgress = scoreList.Any() ? (scoreList.Sum() * 100m) / (scoreList.Count() * 4) : 0m
                        };
                    }).ToList();

                return ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>.Success(result, new List<string> { "Get Program history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetProgramProgressByUserId", ex);
                return ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>.Failure(new[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<ProgramDetailsDto>> GetProgramDetails(UserProgramRequestDto userProgramRequestDto)
        {
            try
            {
                var climateProgramID = userProgramRequestDto.ClimateProgramID;
                var userId = userProgramRequestDto.UserID;

                // Validate Program
                var program = await _context.ClimatePrograms
                    .AsNoTracking()
                    .Where(x => x.ClimateProgramID == climateProgramID && x.IsActive && !x.IsDeleted)
                    .Select(x => new { x.ClimateProgramID })
                    .FirstOrDefaultAsync();

                if (program == null)
                    return ResultResponseDto<ProgramDetailsDto>.Failure(new[] { "Invalid program ID" });

                // Get user access pillars
                var accessPillarIds = await _context.ClientPillarMappings
                    .Where(x => x.UserID == userId)
                    .Select(x => x.PillarID)
                    .ToListAsync();

                // Get all active pillars and questions
                var allPillars = await _context.Pillars
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .AsNoTracking()
                    .Select(p => new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.DisplayOrder,
                        Questions = p.Questions.Select(q => new
                        {
                            q.QuestionID,
                            Options = q.QuestionOptions.Select(o => new { o.OptionID, o.OptionText })
                        }).ToList()
                    })
                    .ToListAsync();

                // Preload all assessments + pillar assessments + responses (flattened projection)
                var assessmentsData = await (
                    from a in _context.Assessments
                    join uc in _context.StaffProgramMappings on a.StaffProgramMappingID equals uc.StaffProgramMappingID
                    where uc.ClimateProgramID == climateProgramID &&
                          a.IsActive &&
                          !uc.IsDeleted
                    select new
                    {
                        a.AssessmentID,
                        Pillars = a.PillarAssessments.Select(pa => new
                        {
                            pa.PillarID,
                            Responses = pa.Responses.Select(r => new { r.Score, r.QuestionOptionID })
                        })
                    }
                ).AsNoTracking().ToListAsync();

                var totalAssessments = assessmentsData.Count;

                if (totalAssessments == 0)
                {
                    return ResultResponseDto<ProgramDetailsDto>.Success(
                        new ProgramDetailsDto
                        {
                            ClimateProgramID = climateProgramID,
                            TotalEvaluation = 0,
                            TotalPillar = allPillars.Count,
                            TotalAnsPillar = 0,
                            TotalQuestion = allPillars.SelectMany(x => x.Questions).Count(),
                            AnsQuestion = 0,
                            ScoreProgress = 0,
                            Pillars = new List<ProgramPillarDetailsDto>()
                        },
                        new List<string> { "No assessments found for this program." }
                    );
                }

                // Flatten all pillar assessments and responses
                var allResponses = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Score1)
                    .ToList();

                // Compute Program level stats
                var totalPillars = allPillars.Count * totalAssessments;
                var totalQuestions = allPillars.Sum(p => p.Questions.Count) * totalAssessments;
                var answeredQuestions = allResponses.Count;
                var totalScore = allResponses.Sum(r => (int?)r.Score ?? 0);
                var scoreProgress = answeredQuestions > 0
                    ? (totalScore * 100M) / (answeredQuestions * 4M)
                    : 0M;

                // Group responses by pillar
                var groupedResponses = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .GroupBy(p => p.PillarID)
                    .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Responses).Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Score1).ToList());

                var naUnknownGroup = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .GroupBy(p => p.PillarID)
                    .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Responses).Where(r => !r.Score.HasValue).ToList());


                // Build pillar details
                var pillarDetails = allPillars
                    .Select(p =>
                    {
                        var isAccess = accessPillarIds.Contains(p.PillarID);

                        var payload = new ProgramPillarDetailsDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName,
                            DisplayOrder = p.DisplayOrder,
                            IsAccess = isAccess
                        };

                        if (isAccess)
                        {
                            groupedResponses.TryGetValue(p.PillarID, out var responses);

                            var validResponses = responses?.ToList<dynamic>() ?? new List<dynamic>();

                            var totalQuestionsForPillar = p.Questions.Count * totalAssessments;
                            var answered = validResponses.Count;
                            var totalPillarScore = validResponses.Sum(r => (int?)r.Score ?? 0);
                            var scorePct = answered > 0 ? (totalPillarScore * 100M) / (answered * 4M) : 0M;


                            naUnknownGroup.TryGetValue(p.PillarID, out var naUnknownRes);

                            var naUnknownResponse = naUnknownRes?.ToList<dynamic>() ?? new List<dynamic>();

                            var naUnknownOptionIds = naUnknownResponse.Select(r => r.QuestionOptionID).ToList();

                            var naUnknownOptions = p.Questions
                                .SelectMany(q => q.Options)
                                .Where(o => naUnknownOptionIds.Contains(o.OptionID))
                                .ToList();

                            payload.TotalQuestion = totalQuestionsForPillar;
                            payload.AnsQuestion = answered;
                            payload.TotalScore = totalPillarScore;
                            payload.ScoreProgress = scorePct;
                            payload.AvgHighScore = validResponses.Any() ? validResponses.Max(r => (int?)r.Score ?? 0) : 0;
                            payload.AvgLowerScore = validResponses.Any() ? validResponses.Min(r => (int?)r.Score ?? 0) : 0;
                            payload.TotalNA = naUnknownOptions.Count(o => o.OptionText.Contains("N/A"));
                            payload.TotalUnKnown = naUnknownOptions.Count(o => o.OptionText.Contains("Unknown"));
                        }
                        return payload;
                    })
                    .OrderByDescending(x => x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();

                var programDetails = new ProgramDetailsDto
                {
                    ClimateProgramID = climateProgramID,
                    TotalEvaluation = totalAssessments,
                    TotalPillar = totalPillars,
                    TotalAnsPillar = pillarDetails.Count(p => p.AnsQuestion > 0),
                    TotalQuestion = totalQuestions,
                    AnsQuestion = answeredQuestions,
                    TotalScore = totalScore,
                    ScoreProgress = scoreProgress,
                    AvgHighScore = pillarDetails.Any() ? pillarDetails.Max(p => p.TotalScore) : 0,
                    AvgLowerScore = pillarDetails.Any() ? pillarDetails.Min(p => p.TotalScore) : 0,
                    Pillars = pillarDetails
                };

                return ResultResponseDto<ProgramDetailsDto>.Success(programDetails, new[] { "Get program details successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetProgramDetails", ex);
                return ResultResponseDto<ProgramDetailsDto>.Failure(new[] { "There is an error, please try later" });
            }
        }
        public async Task<ResultResponseDto<List<ProgramPillarQuestionDetailsDto>>> GetProgramPillarDetails(StaffProgramGetPillarInfoRequestDto staffProgramRequestDto)
        {
            try
            {
                var climateProgramID = staffProgramRequestDto.ClimateProgramID;
                var pillarId = staffProgramRequestDto.PillarID;
                var date = staffProgramRequestDto.UpdatedAt;

                // 1. Validate program and pillar
                var program = await _context.ClimatePrograms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ClimateProgramID == climateProgramID && x.IsActive && !x.IsDeleted);

                if (program == null)
                    return ResultResponseDto<List<ProgramPillarQuestionDetailsDto>>.Failure(new[] { "Invalid program ID" });

                var pillar = await _context.Pillars
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .Include(p => p.Questions.Where(x => !x.IsDeleted))
                        .ThenInclude(q => q.QuestionOptions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PillarID == pillarId);

                if (pillar == null)
                    return ResultResponseDto<List<ProgramPillarQuestionDetailsDto>>.Failure(new[] { "Invalid pillar ID" });

                // 2. Get all assessments for this program in the given year
                var assessments = await (
                    from a in _context.Assessments
                        .Include(x => x.PillarAssessments)
                            .ThenInclude(pa => pa.Responses)
                                .ThenInclude(r => r.Question)
                    join uc in _context.StaffProgramMappings.Where(x => !x.IsDeleted)
                        on a.StaffProgramMappingID equals uc.StaffProgramMappingID
                    where uc.ClimateProgramID == climateProgramID && a.IsActive && a.UpdatedAt.Year == date.Year
                    select a
                ).ToListAsync();

                if (!assessments.Any())
                    return ResultResponseDto<List<ProgramPillarQuestionDetailsDto>>.Failure(new[] { "No assessments found for the given program/year." });

                // 3. Flatten pillar assessments for this pillar
                var pillarAssessments = assessments
                    .SelectMany(a => a.PillarAssessments)
                    .Where(pa => pa.PillarID == pillarId)
                    .ToList();

                // 4. Flatten all responses for this pillar
                var allResponses = pillarAssessments
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r != null)
                    .ToList();

                var validResponses = allResponses
                    .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Score1)
                    .ToList();

                // 5. Generate question-level metrics
                var result = pillar.Questions
                    .OrderBy(x => x.DisplayOrder)
                    .Select(q =>
                    {
                        var qResponses = validResponses.Where(r => r.QuestionID == q.QuestionID).ToList();
                        var totalQuestions = 1 * assessments.Count; // Each item represents one question
                        var answeredQuestions = qResponses.Count;
                        var totalScore = qResponses.Sum(r => (decimal?)r.Score ?? 0);

                        // Compute "Unknown" and "N/A" counts
                        var naUnknownIds = allResponses
                            .Where(r => r.QuestionID == q.QuestionID && !r.Score.HasValue)
                            .Select(r => r.QuestionOptionID);

                        var naUnknownOptions = q.QuestionOptions
                            .Where(opt => naUnknownIds.Contains(opt.OptionID))
                            .ToList();

                        var totalNA = naUnknownOptions.Count(opt => opt.OptionText.Contains("N/A"));
                        var totalUnknown = naUnknownOptions.Count(opt => opt.OptionText.Contains("Unknown"));

                        var scoreProgress = answeredQuestions > 0
                            ? (totalScore * 100M) / (answeredQuestions * 4M * assessments.Count)
                            : 0M;

                        return new ProgramPillarQuestionDetailsDto
                        {
                            QuestionID = q.QuestionID,
                            QuestionText = q.QuestionText,
                            TotalQuestion = totalQuestions,
                            AnsQuestion = answeredQuestions,
                            TotalScore = totalScore,
                            ScoreProgress = scoreProgress,
                            AvgHighScore = qResponses.Any() ? qResponses.Max(r => (decimal?)r.Score ?? 0) : 0,
                            AvgLowerScore = qResponses.Any() ? qResponses.Min(r => (decimal?)r.Score ?? 0) : 0,
                            TotalNA = totalNA,
                            TotalUnKnown = totalUnknown
                        };
                    })
                .ToList();

                return ResultResponseDto<List<ProgramPillarQuestionDetailsDto>>.Success(
                    result,
                    new List<string> { "Get program pillar question details successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetProgramPillarDetails", ex);
                return ResultResponseDto<List<ProgramPillarQuestionDetailsDto>>.Failure(new[] { "There is an error, please try later" });
            }
        }
        public async Task<ResultResponseDto<List<PartnerProgramResponseDto>>> GetClientPrograms(int userID)
        {
            try
            {

                // Step 1??: Fetch program score averages as a dictionary
                var programScoresDict = await _context.AIProgramScores
                    .Where(ar => ar.IsVerified)
                    .GroupBy(ar => ar.ClimateProgramID)
                    .Select(g => new
                    {
                        ClimateProgramID = g.Key,
                        Score = g.Average(x => (decimal?)x.EvaluatorScore) ?? 0,
                        AiScore = g.Average(x => (decimal?)x.AIProgress) ?? 0
                    })
                    .ToDictionaryAsync(x => x.ClimateProgramID, x => new { x.Score, x.AiScore });

                // Step 2??: Fetch programs assigned to the user
                var programs = await _context.ClientProgramMappings
                    .Where(x => x.IsActive && x.Program != null && !x.Program.IsDeleted && x.UserID == userID)
                    .Select(c => new PartnerProgramResponseDto
                    {
                        ClimateProgramID = c.Program.ClimateProgramID,
                        ProgramName = c.Program.ProgramName,
                        Location = c.Program.Location,                     
                        Image = c.Program.Image,
                        Year = c.Program.Year
                    })
                    .AsNoTracking()
                    .ToListAsync();

                // Step 3??: Map score from dictionary (safe fallback to 0)
                foreach (var program in programs)
                {
                    if (programScoresDict.TryGetValue(program.ClimateProgramID, out var score))
                    {
                        program.Score = score.AiScore;
                        program.AiScore = score.AiScore;
                    }
                }

                // Step 4??: Sort by score descending
                var result = programs.OrderByDescending(x => x.Score).ToList();

                return ResultResponseDto<List<PartnerProgramResponseDto>>.Success(
                    result,
                    new[] { "Fetched all assigned programs successfully." }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetClientPrograms", ex);
                return ResultResponseDto<List<PartnerProgramResponseDto>>.Failure(
                    new[] { "There was an error. Please try again later." }
                );
            }
        }

        public async Task<ResultResponseDto<string>> AddClientKpisProgramAndPillar(AddClientKpisProgramAndPillar payload, int userId, string tierName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tierName))
                    return ResultResponseDto<string>.Failure(new[] { "Access tier information is missing. Please log in again." });

                if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var tier))
                    return ResultResponseDto<string>.Failure(new[] { "Invalid tier access. Please contact support team." });
               
                var allPillarIds = await _context.Pillars.Select(p => p.PillarID).ToListAsync();
                var allProgramIds = await _context.ClimatePrograms
                    .Where(c => c.IsActive)
                    .Select(c => c.ClimateProgramID)
                    .ToListAsync();

                if (tier == TieredAccessPlan.Premium)
                {
                    // Premium always receives every pillar
                    payload.Pillars = allPillarIds;

                    if (payload.IsAllPrograms)
                    {
                        payload.Programs = allProgramIds;
                    }
                    else if (payload.Programs == null || payload.Programs.Count < 1)
                    {
                        return ResultResponseDto<string>.Failure(new[]
                        {
                            "Premium plan requires at least one program, or all programs."
                        });
                    }
                }
                else
                {
                    var pillarLimits = tier switch
                    {
                        TieredAccessPlan.Basic => new { Min = 1, Max = 7, Name = "Basic" },
                        TieredAccessPlan.Standard => new { Min = 1, Max = 12, Name = "Standard" },
                        _ => new { Min = 0, Max = 0, Name = "Unknown" }
                    };

                    var programCount = payload.Programs?.Count ?? 0;
                    var pillarCount = payload.Pillars?.Count ?? 0;
                    var programsOk = programCount >= 1;
                    var pillarsOk = pillarCount >= pillarLimits.Min && pillarCount <= pillarLimits.Max;

                    if (!programsOk || !pillarsOk)
                    {
                        return ResultResponseDto<string>.Failure(new[]
                        {
                            $"Your {pillarLimits.Name} plan requires at least 1 program and between {pillarLimits.Min} and {pillarLimits.Max} pillars."
                        });
                    }
                }
                //  Remove existing mappings
                var existingPrograms = await _context.ClientProgramMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                var existingPillars = await _context.ClientPillarMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                _context.ClientProgramMappings.RemoveRange(existingPrograms);
                _context.ClientPillarMappings.RemoveRange(existingPillars);

                var utcNow = DateTime.UtcNow;

                var newProgramMappings = payload.Programs.Select(ClimateProgramID => new ClientProgramMapping
                {
                    ClimateProgramID = ClimateProgramID,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                var newPillarMappings = payload.Pillars.Select(pillarId => new ClientPillarMapping
                {
                    PillarID = pillarId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                await _context.ClientProgramMappings.AddRangeAsync(newProgramMappings);
                await _context.ClientPillarMappings.AddRangeAsync(newPillarMappings);

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Your preferences have been saved successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in AddProgramUserKpisProgramAndPillar", ex);
                return ResultResponseDto<string>.Failure(new[]
                {
                    "Something went wrong while saving your selections. Please try again later."
                });
            }
        }
        public async Task<ResultResponseDto<List<GetAllKpisResponseDto>>> GetProgramUserKpi(int userId, string tierName)
        {
            try
            {
                var validPillarIds = await _context.ClientPillarMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.PillarID)
                    .ToListAsync();

                // Step 1: Get valid KPI IDs for this user
                var validKpiIds = await _context.AnalyticalLayerPillarMappings
                    .Where(x => validPillarIds.Contains(x.PillarID))
                    .Select(x => x.LayerID)
                    .Distinct()
                    .ToListAsync();

                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<List<GetAllKpisResponseDto>>.Failure(new List<string> { "you don't have kpi access." });
                }

                // Fetch Analytical Layers that match the user's KPI access
                var result = await _context.AnalyticalLayers
                    .Where(ar => !ar.IsDeleted && validKpiIds.Contains(ar.LayerID))
                    .Select(x=>new GetAllKpisResponseDto
                    {
                        LayerID = x.LayerID,
                        LayerCode = x.LayerCode,
                        LayerName = x.LayerName
                    })
                    .ToListAsync();

                return ResultResponseDto<List<GetAllKpisResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetProgramUserKpi", ex);
                return ResultResponseDto<List<GetAllKpisResponseDto>>.Failure(new List<string> { "An error occurred while fetching user KPIs." });
            }
        }

        public async Task<ResultResponseDto<CompareProgramResponseDto>> ComparePrograms(CompareProgramsRequestDto c, int userId, string tierName, bool applyPagination = true)
        {
            try
            {
                var validKpiIds = new List<int>();
                if (c.Kpis.Count == 0)
                {
                    var validPillarIds = _context.ClientPillarMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.PillarID);

                    // Step 1: Get valid KPI IDs for this user
                    var query = _context.AnalyticalLayerPillarMappings
                        .Where(x => validPillarIds.Contains(x.PillarID))
                        .Select(x => x.LayerID)
                        .Distinct();

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


                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<CompareProgramResponseDto>.Failure(new List<string> { "You don't have KPI access." });
                }

                // Step 2: Get all selected programs (even if no analytical data)
                var selectedPrograms = await _context.ClientProgramMappings
                    .Include(x=>x.Program)
                    .Where(x => c.Programs.Contains(x.ClimateProgramID) && x.UserID== userId && x.IsActive && x.Program != null && x.Program.IsActive)
                    .Select(x => new { x.Program.ClimateProgramID, x.Program.ProgramName })
                    .ToListAsync();

                if (!selectedPrograms.Any())
                {
                    return ResultResponseDto<CompareProgramResponseDto>.Failure(new List<string> { "No valid programs found." });
                }

                // Step 3: Fetch analytical layer results for selected programs
                var analyticalResults = await _context.AnalyticalLayerResults
                    .Include(ar => ar.AnalyticalLayer)
                    .Where(x => c.Programs.Contains(x.ClimateProgramID) && validKpiIds.Contains(x.LayerID))
                    .Select(ar => new
                    {
                        ar.ClimateProgramID,
                        ar.LayerID,
                        ar.AnalyticalLayer.Purpose,
                        ar.AnalyticalLayer.LayerCode,
                        ar.AnalyticalLayer.LayerName,
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

                // Initialize chart series for each program
                foreach (var program in selectedPrograms)
                {
                    response.Series.Add(new ChartSeriesDto
                    {
                        Name = program.ProgramName,
                        AiData = new List<decimal>()
                    });
                }

                // Add Peer Program Score series
                var peerSeries = new ChartSeriesDto
                {
                    Name = "Peer Program Score",
                    AiData = new List<decimal>()
                };

                // Step 6: Build chart and table data
                foreach (var layer in allLayers)
                {
                    response.Categories.Add(layer.LayerCode);

                    // Map KPI values for each program (0 if missing)
                    var values = new Dictionary<int, List<decimal>>();

                    foreach (var program in selectedPrograms)
                    {
                        var value = analyticalResults
                            .FirstOrDefault(r => r.ClimateProgramID == program.ClimateProgramID && r.LayerID == layer.LayerID);

                        var evaluatedValue = Math.Round(value?.CalValue5 ?? 0, 2);
                        var aiValue = Math.Round(value?.AiCalValue5 ?? 0, 2);
                        values[program.ClimateProgramID] = new List<decimal> { evaluatedValue, aiValue };

                        //// Add to series
                        var programSeries = response.Series.First(s => s.Name == program.ProgramName);

                        programSeries.AiData.Add(aiValue);
                    }

                    var aiPeerProgramScore = values.Values.Any() ? Math.Round(values.Values.Select(x=>x.Last()).Average(), 2) : 0;
                    peerSeries.AiData.Add(aiPeerProgramScore);

                    // Add table data
                    response.TableData.Add(new ChartTableRowDto
                    {
                        LayerID=layer.LayerID,
                        LayerCode = layer.LayerCode,
                        LayerName = layer.LayerName,
                        Purpose = layer.Purpose,
                        ProgramValues = selectedPrograms.Select(p => new ProgramValueDto
                        {
                            ClimateProgramID = p.ClimateProgramID,
                            ProgramName = p.ProgramName,
                            AiValue =  values[p.ClimateProgramID].Last()
                        }).ToList(),
                        PeerProgramScore = aiPeerProgramScore // You can rename property if needed
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
        public async Task<ResultResponseDto<AiProgramPillarResponseDto>> GetAIProgramPillars(AiProgramPillarRequestDto request, int userID, string tierName)
        {
            try
            {
                // 1. Check if program is finalized for this user (EXISTS instead of JOIN)
                var isProgramFinalized = await _context.ClientProgramMappings
                    .AnyAsync(pum =>
                        pum.UserID == userID &&
                        pum.ClimateProgramID == request.ClimateProgramID &&
                        _context.AIProgramScores.Any(ac =>
                            ac.ClimateProgramID == request.ClimateProgramID && ac.IsVerified));

                if (!isProgramFinalized)
                {
                    return ResultResponseDto<AiProgramPillarResponseDto>.Failure(new[] { "Program is under review process try after some time", });
                }

                var res = await _context.AIPillarScores
                    .Where(x => x.ClimateProgramID == request.ClimateProgramID) 
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds =  await _context.ClientPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID)
                                .Distinct()
                                .ToListAsync();

                var pillars = await _commonService.GetPillars();

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
                        ClimateProgramID = x.score?.ClimateProgramID ?? request.ClimateProgramID,
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
                        r.EvidenceSummary = x.score.EvidenceSummary;
                        r.RedFlag = x.score.RedFlag;
                        r.InclusionAccessNote = x.score.InclusionAccessNote;
                        r.InstitutionalAssessment = x.score.InstitutionalAssessment;
                        r.DataGapAnalysis = x.score.DataGapAnalysis;
                        r.DataSourceCitations = x.score.DataSourceCitations;
                        r.UpdatedAt = x.score.UpdatedAt;
                    }
                    return r;
                })
                .OrderBy(x => !x.IsAccess)
                .ThenBy(x => x.DisplayOrder)
                .ToList();
                // Fetch AI Progress from AIProgramScore table
                var aiProgramScore = await _context.AIProgramScores
                    .Where(x => x.ClimateProgramID == request.ClimateProgramID)
                    .FirstOrDefaultAsync();

                var finalResutl = new AiProgramPillarResponseDto
                {
                    Pillars = result,
                    AIProgress = aiProgramScore?.AIProgress
                };

                var resposne = ResultResponseDto<AiProgramPillarResponseDto>.Success(finalResutl, new[] { "Pillar get successfully", });

                return resposne;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAIProgramPillars", ex);
                return ResultResponseDto<AiProgramPillarResponseDto>.Failure(new[] { "Error in getting pillar details", });
            }
        }
        public async Task<Tuple<string, byte[]>> ExportComparePrograms(CompareProgramsRequestDto c, int userId, string tierName)
        {
            try
            {
                var result = await ComparePrograms(c, userId, tierName, false);
                var data = result.Result;

                if (data == null || data.TableData == null || !data.TableData.Any())
                {
                    return new Tuple<string, byte[]>("Program_Comparison.xlsx", Array.Empty<byte>());
                }

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Program Comparison");

                    // =========================
                    // ?? REPORT HEADER (TOP)
                    // =========================
                    var programs = data.TableData.First().ProgramValues;
                    int totalCols = 2 + programs.Count; // 2 fixed columns (KPI Name, Purpose) + 1 column per program (Score)

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

                    // Dynamic Programs (only Score)
                    foreach (var program in programs)
                    {
                        ws.Range(row, col, row + 1, col).Merge().Value = program.ProgramName;
                        ws.Cell(row + 1, col).Value = "Score";
                        col++;
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
                            ws.Cell(row, col++).Value = program.AiValue; // Only AI value
                        }

                        row++;
                    }

                    int endDataRow = row - 1;

                    // =========================
                    // ?? STYLING
                    // =========================
                    ws.Column(1).Width = 30;  // KPI Name
                    ws.Column(2).Width = 55;  // Purpose

                    for (int i = 3; i <= totalCols; i++)
                    {
                        ws.Column(i).Width = 18;
                    }

                    ws.Column(2).Style.Alignment.WrapText = true;
                    ws.Column(2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Columns(3, totalCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Rows().AdjustToContents();
                    ws.SheetView.FreezeRows(6);

                    var dataRange = ws.Range(5, 1, endDataRow, totalCols);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    for (int i = startDataRow; i <= endDataRow; i++)
                    {
                        if (i % 2 == 0)
                        {
                            ws.Range(i, 1, i, totalCols).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                        }
                    }

                    ws.Range(6, 1, 6, totalCols).SetAutoFilter();

                    // =========================
                    // ?? SHEET 2: KPI Details
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
        private string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty).Trim();
        }
    }
}
