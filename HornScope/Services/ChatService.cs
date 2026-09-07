
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.chatDto;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Services
{
    public class ChatService : IChatService
    {
        #region  constructor
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IAIAnalyzeService _aIAnalyzeService;
        private readonly IMemoryCache _cache;
        private readonly ICommonService _commonService;
        public ChatService(ApplicationDbContext context, IMemoryCache cache,
            IAppLogger appLogger, IAIAnalyzeService aIAnalyzeService, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _aIAnalyzeService = aIAnalyzeService;
            _cache = cache;
            _commonService = commonService;
        }
        public async Task<ResultResponseDto<List<AIAssistantFAQDto>>> GetAssistantFAQDs(int userId, UserRole userRole)
        {
            try
            {
                var faqs = _context.AIAssistantFAQ
                    .Where(x => x.IsActive)
                    .Select(x => new AIAssistantFAQDto
                    {
                        FAQID = x.FAQID,
                        Related = x.Related,
                        Category = x.Category,
                        QuestionText = x.QuestionText,
                        DisplayOrder = x.DisplayOrder,
                        IsAnsweredFaq =  !string.IsNullOrEmpty(x.AnswerText)
                    }).ToList();

                return ResultResponseDto<List<AIAssistantFAQDto>>.Success(faqs, new[] { "Faqs get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while getting the GetAssistantFAQDs request.", ex);
                return ResultResponseDto<List<AIAssistantFAQDto>>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> AskAboutProgram(ProgramChatRequestDto request, int userId, UserRole userRole)
        {
            try
            {
                if(userRole == UserRole.ProgramUser)
                {
                    var isValidProgram = _context.ClientProgramMappings.Where(x => x.UserID == userId).Any(c => c.ClimateProgramID == request.ClimateProgramID);
                    if (!isValidProgram)
                    {
                        return ResultResponseDto<ChatResponseDto>.Failure(new[] { "You don't have access to this program data." });
                    }
                }

                var r = new ChatProgramAskQuestionRequest
                {
                    ClimateProgramID = request.ClimateProgramID,
                    PillarID = request.PillarID,
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.ChatProgramAsk(r);
          
                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from VCP Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {
                    ClimateProgramID = request.ClimateProgramID,
                    PillarID = request.PillarID,
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    ResponseText = resutl.Result ?? "No response from ."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutProgram request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> AskAboutGlobal(ChatGlobalAskQuestionRequestDto request, int userId, UserRole userRole)
        {
            try
            {
                var r = new ChatGlobalAskQuestionRequest
                {  
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.ChatGlobalAsk(r);

                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from VCP Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {       
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    ResponseText = resutl.Result ?? "An error occurred or we do not have an answer for that."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutGlobal request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> CrossComparision(CrossComparisionRequestDto request, int userId, UserRole userRole)
        {
            try
            {
                if (userRole == UserRole.ProgramUser)
                {
                    var userClimateProgramIDs = _context.ClientProgramMappings
                        .Where(x=>x.UserID == userId)
                        .Select(x => x.ClimateProgramID)
                        .ToList();

                    var isValidProgram = request.ClimateProgramIDs
                        .All(id => userClimateProgramIDs.Contains(id));

                    if (!isValidProgram)
                    {
                        return ResultResponseDto<ChatResponseDto>
                            .Failure(new[] { "You don't have access to this program data." });
                    }
                }
                var r = new CrossComparisionRequest
                {  
                    ClimateProgramIDs = request.ClimateProgramIDs,
                    QuestionText = request.QuestionText,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.CrossComparision(r);

                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from VCP Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {       
                    QuestionText = request.QuestionText,
                    FAQID = null,
                    ResponseText = resutl.Result ?? "An error occurred or we do not have an answer for that."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutGlobal request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatProgramExecutiveSlidesResponse>> GetProgramSlides(int climateProgramID, int userId, UserRole userRole)
        {
            string cacheKey = $"ProgramSlides_{climateProgramID}";

            try
            {
                if (userRole == UserRole.ProgramUser && climateProgramID != 0)
                {
                    var isValidProgram = _context.ClientProgramMappings.Where(x => x.UserID == userId).Any(c => c.ClimateProgramID == climateProgramID);
                    if (!isValidProgram)
                    {
                        return ResultResponseDto<ChatProgramExecutiveSlidesResponse>.Failure(new[] { "You don't have access to this program data." });
                    }
                }

                var programRanking = await _commonService.GetProgramRankings(climateProgramID);
                var progress = await _commonService.GetProgramProgressAsync(userId, (int)userRole, climateProgramID);
                ProgramRankingResponseDto programResult;
                List<PillarsUserHistroyResponseDto> pillars;

                if (climateProgramID == 0)
                {
                    if (programRanking != null && programRanking.Any())
                    {
                        var averageScore = programRanking.Average(p => p.ProgramAIScore ?? 0);
                        var totalProgramCount = programRanking.Count();


                        // Get active pillars from database
                        var activePillars = await _context.Pillars
                            .Where(x => x.IsActive && !x.IsDeleted)
                            .Select(p => new { p.PillarID, p.PillarName, p.DisplayOrder, p.ImagePath })
                            .ToListAsync();

                        // Calculate average pillar scores from progress data (across all programs)
                        pillars = activePillars.Select(p => new PillarsUserHistroyResponseDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName ?? "",
                            DisplayOrder = p.DisplayOrder,
                            PillarScore = progress
                                .Where(prog => prog.PillarID == p.PillarID)
                                .Any()
                                ? progress.Where(prog => prog.PillarID == p.PillarID)
                                    .Average(prog => prog.AIProgress)
                                : 0,
                            ImagePath = p.ImagePath
                        })
                        .OrderBy(p => p.DisplayOrder)
                        .ToList();

                        // Filter pillars by user access if ProgramUser
                        if (userRole == UserRole.ProgramUser)
                        {
                            var validPillars = _context.ClientPillarMappings
                                .Where(x => x.UserID == userId)
                                .Select(x => x.PillarID)
                                .ToList();
                            pillars = pillars.Where(x => validPillars.Contains(x.PillarID)).ToList();
                        }

                        programResult = new ProgramRankingResponseDto
                        {
                            ClimateProgramID = 0,
                            ProgramName = "All Programs Average",
                            ProgramAIScore = averageScore,
                            DataYear = programRanking.FirstOrDefault()?.DataYear,
                            ProgramRank = 1,
                            TotalProgram = totalProgramCount,
                            Pillars = pillars
                        };
                    }
                    else
                    {
                        return ResultResponseDto<ChatProgramExecutiveSlidesResponse>.Failure(new[] { "No programs found to calculate average." });
                    }
                }
                else
                {
                    var program = programRanking.FirstOrDefault(x => x.ClimateProgramID == climateProgramID);
                    if (program == null)
                    {
                        return ResultResponseDto<ChatProgramExecutiveSlidesResponse>.Failure(new[] { "Program not found." });
                    }

                    // Get pillar progress data for the specific program
                    var programProgress = progress.Where(x => x.ClimateProgramID == climateProgramID).ToList();

                    // Get active pillars from database
                    var activePillars = await _context.Pillars
                        .Where(x => x.IsActive && !x.IsDeleted)
                        .Select(p => new { p.PillarID, p.PillarName, p.DisplayOrder, p.ImagePath })
                        .ToListAsync();

                    // Get pillar scores from progress data for the specific program
                    pillars = activePillars.Select(p => new PillarsUserHistroyResponseDto
                    {
                        PillarID = p.PillarID,
                        PillarName = p.PillarName ?? "",
                        DisplayOrder = p.DisplayOrder,
                        PillarScore = programProgress
                            .Where(prog => prog.PillarID == p.PillarID && prog.ClimateProgramID == climateProgramID)
                            .Select(prog => prog.AIProgress)
                            .FirstOrDefault(),
                        ImagePath = p.ImagePath
                    })
                    .OrderBy(p => p.DisplayOrder)
                    .ToList();

                    // Filter pillars by user access if ProgramUser
                    if (userRole == UserRole.ProgramUser)
                    {
                        var validPillars = _context.ClientPillarMappings
                            .Where(x => x.UserID == userId)
                            .Select(x => x.PillarID)
                            .ToList();
                        pillars = pillars.Where(x => validPillars.Contains(x.PillarID)).ToList();
                    }

                    programResult = new ProgramRankingResponseDto
                    {
                        ClimateProgramID = program.ClimateProgramID,
                        ProgramName = program.ProgramName,
                        ProgramAIScore = program.ProgramAIScore,
                        DataYear = program.DataYear,
                        ProgramRank = program.ProgramRank,
                        TotalProgram = program.TotalPrograms,
                        Pillars = pillars
                    };
                }
                if (_cache.TryGetValue(cacheKey, out ChatProgramExecutiveSlidesResponse cachedResult))
                {
                    cachedResult.Result.Program = programResult;

                    return ResultResponseDto<ChatProgramExecutiveSlidesResponse>.Success(
                        cachedResult,
                        new List<string>
                        {
                            "Program executive slides fetched successfully from cache."
                        }
                    );
                }

                var response = new ChatProgramExecutiveSlidesResponse
                {
                    Success = true,
                    Message = "Program executive slides fetched successfully.",
                    Result = new ProgramExecutiveSlidesResult
                    {
                        Program = programResult,
                        RecentPerformance = new PerformanceSummary(),
                        CombinedRisks = new List<CombinedRiskItem>(),
                        EarlyWarnings = new List<EarlyWarningItem>()
                    }
                };

                return ResultResponseDto<ChatProgramExecutiveSlidesResponse>.Success(
                    response,
                    new List<string>
                    {
                         "Program executive slides fetched successfully."
                    }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while processing the GetProgramSlides request.",
                    ex
                );

                return ResultResponseDto<ChatProgramExecutiveSlidesResponse>.Failure(
                    new[]
                    {
                        "An error occurred while processing your request. Please try again later."
                    }
                );
            }
        }


        #endregion
    }
}
