using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HornScope.Common.Implementation;
using HornScope.Common.Models.settings;
using HornScope.Data;
using HornScope.Dtos.chatDto;
using HornScope.IServices;
using HornScope.Common.Interface;
namespace HornScope.Services
{
    public class AIAnalyzeService : IAIAnalyzeService
    {
        private readonly HttpService _httpService;
        private readonly  string aiUrl = "http://127.0.0.1:8000";
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private Dictionary<string, string> headers;
        private readonly ICommonService _commonService;
        public AIAnalyzeService(HttpService httpService, IOptions<AppSettings> appSettings, 
            ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService)
        {
            _httpService = httpService;
            aiUrl = appSettings?.Value?.AiUrl ?? aiUrl;
            _context = context;
            _appLogger = appLogger;
            headers = new Dictionary<string, string> { { "X-API-Key", appSettings?.Value?.AiToken ?? "" } };
            _commonService = commonService;
        }
        public async Task RunMonthlyJob()
        {
            try
            {
                var newProgramIds = _context.ClimatePrograms.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.ClimateProgramID).ToList();
                foreach (var id in newProgramIds)
                {
                    await AnalyzeSingleProgramFull(id);
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Run Monthly Job", ex);
            }
            
        }

        public async Task RunEvery2HoursJob()
        {
            try
            {
                //await ImportAiScore();
            }
            catch (Exception ex)
            {
               await _appLogger.LogAsync("Error in Running job in Every 2-hour AI ", ex);
            }

        }
        public async Task RunDailyJob()
        {
            try
            {
                await ImportAllProgramImmediateSummary();
                await ImportRemainingDocumentsToVectorDB();
                await DeleteRemainingDocumentsToVectorDB();

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Running job in Run daily job ", ex);
            }
        }
        public async Task ImportAiScore()
        {
            // if new city added
            var totalPillar = (await _commonService.GetPillars()).Count;
            var allProgramIds = _context.ClimatePrograms.Where(x=>x.IsActive && !x.IsDeleted).Select(x=>x.ClimateProgramID).ToList();
            var importedProgramIds = _context.AIProgramScores.Select(x => x.ClimateProgramID);

            var newProgramIds = allProgramIds.Where(x=> !importedProgramIds.Contains(x)).ToList();
            foreach (var id in newProgramIds)
            {
                await AnalyzeSingleProgramFull(id);
            }

            var now = DateTime.UtcNow;

            // Run at 1st day of every month at 01:00 AM UTC
            var date = new DateTime(now.Year, now.Month, 1, 1, 0, 0, DateTimeKind.Utc)
                            .AddMonths(-1);

            var importPillarsClimateProgramIDs = _context.AIPillarScores
                .GroupBy(x => x.ClimateProgramID)
                .Where(g => g.Max(x => x.UpdatedAt) < date || g.Count() < totalPillar)
                .Select(g => g.Key)
                .ToList();


            foreach (var id in importPillarsClimateProgramIDs)
            {
                await AnalyzeProgramPillars(id);
            }


            var needtoImportClimateProgramIDs = _context.AIProgramScores.Where(x => x.UpdatedAt < date).Select(x=>x.ClimateProgramID);
            foreach (var id in needtoImportClimateProgramIDs)
            {
                await AnalyzeSingleProgram(id);
            }
        }

        public async Task ImportAllProgramImmediateSummary()
        {
            var allProgramIds = await _context.ClimatePrograms
                     .Where(x => x.IsActive && !x.IsDeleted)
                     .Select(x => x.ClimateProgramID)
                     .ToListAsync();

            foreach (var id in allProgramIds)
            {
                await AnalyzeProgramImmediateSituation(id);
                await Task.Delay(200);
            }

        }

        public async Task ImportRemainingDocumentsToVectorDB()
        {
            var activeDocumentIds = _context.ProgramDocuments
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.ProgramDocumentID);

            var data = await _context.DocumentChunks
                .Where(x => !activeDocumentIds.Contains(x.ProgramDocumentID))
                .Select(x => x.ProgramDocumentID)

                .Union(
                    _context.DocumentTOC
                        .Where(x => !activeDocumentIds.Contains(x.ProgramDocumentID))
                        .Select(x => x.ProgramDocumentID)
                )
                .Distinct()
                .ToListAsync();


            foreach (var documentID in data)
            {
                await ProcessDocument(documentID);
                await Task.Delay(200);
            }
        }
        public async Task DeleteRemainingDocumentsToVectorDB()
        {
            var activeDocumentIds = _context.ProgramDocuments
                    .Where(x => x.IsDeleted)
                    .Select(x => x.ProgramDocumentID);

            var data = await _context.DocumentChunks
                .Where(x => activeDocumentIds.Contains(x.ProgramDocumentID))
                .Select(x => x.ProgramDocumentID)

                .Union(
                    _context.DocumentTOC
                        .Where(x => activeDocumentIds.Contains(x.ProgramDocumentID))
                        .Select(x => x.ProgramDocumentID)
                )
                .Distinct()
                .ToListAsync();

            foreach (var documentID in data)
            {
                await DeleteDocument(documentID);
                await Task.Delay(200);
            }
        }

        #region Ai api calls       

        public async Task AnalyzeAllProgramsFull()
        {
            var url = aiUrl + AiEndpoints.AnalyzeAllProgramsFull;
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeSingleProgramFull(int climateProgramID)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSingleProgramFull(climateProgramID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeSingleProgram(int climateProgramID)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSingleProgram(climateProgramID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeProgramPillars(int climateProgramID)
        {
            var url = aiUrl + AiEndpoints.AnalyzeProgramPillars(climateProgramID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task AnalyzeSinglePillar(int climateProgramID, int pillarId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSinglePillar(climateProgramID, pillarId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeQuestionsOfProgram(int climateProgramID)
        {
            var url = aiUrl + AiEndpoints.AnalyzeProgramQuestions(climateProgramID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeQuestionsOfProgramPillar(int climateProgramID, int pillarId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeProgramPillarQuestions(climateProgramID, pillarId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task AnalyzeProgramImmediateSituation(int climateProgramID)
        {
            var url = aiUrl + AiEndpoints.AnalyzeProgramImmediateSituation(climateProgramID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task ProcessDocument(int documentID)
        {
            var url = aiUrl + AiEndpoints.ProcessDocument(documentID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task DeleteDocument(int documentID)
        {
            var url = aiUrl + AiEndpoints.DeleteDocument(documentID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task<ChatProgramAskQuestionResponse> ChatProgramAsk(ChatProgramAskQuestionRequest request)
        {
            var url = aiUrl + AiEndpoints.ChatProgramAsk();
            var result =  await _httpService.SendAsync<ChatProgramAskQuestionResponse>(HttpMethod.Post, url, request, headers);

            return result;
        }
        public async Task<ChatProgramAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request)
        {
                var url = aiUrl + AiEndpoints.ChatGlobalAsk();
                var result = await _httpService.SendAsync<ChatProgramAskQuestionResponse>(HttpMethod.Post, url, request, headers);
    
                return result;
        }

        public async Task<KpiSummaryAiResponse?> SummarizeKpiPerformance(KpiSummaryAiRequest request)
        {
            var url = aiUrl + AiEndpoints.KpiSummary();
            return await _httpService.SendAsync<KpiSummaryAiResponse>(HttpMethod.Post, url, request, headers);
        }

        public async Task<ChatProgramAskQuestionResponse> CrossComparision(CrossComparisionRequest request)
        {
            var url = aiUrl + AiEndpoints.CrossComparision();
            var result = await _httpService.SendAsync<ChatProgramAskQuestionResponse>(HttpMethod.Post, url, request, headers);

            return result;
        }
        public async Task<ChatProgramExecutiveSlidesResponse?> GetProgramSlides(int climateProgramID)
        {
            var url = aiUrl + AiEndpoints.ProgramSlides();

            return await _httpService.SendAsync<ChatProgramExecutiveSlidesResponse>(
                HttpMethod.Post,
                url,
                new ProgramSlidesRequest
                {
                    ClimateProgramID = climateProgramID
                },
                headers
            );
        }

        public async Task<ChatEmergingTrendsResponse?> GetEmergingTrendsAndIssues(int ProgramCount)
        {
            var url = aiUrl + AiEndpoints.EmergingTrendsAndIssues(ProgramCount);

            return await _httpService.SendAsync<ChatEmergingTrendsResponse>(
                HttpMethod.Get,
                url,
                null,
                headers
            );
        }

        public async Task<ChatPillarLiveSignalsResponse?> GetPillarLiveSignals()
        {
            var url = aiUrl + AiEndpoints.PillarLiveSignals();

            return await _httpService.SendAsync<ChatPillarLiveSignalsResponse>(
                HttpMethod.Get,
                url,
                null,
                headers
            );
        }

        public async Task AnalyzeProgramMissingQuestions(MissingProgramQuestionRequest r)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCityMissingQuestions();
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, r, headers);
        }

        #endregion Ai api calls 
    }

    #region AiEndpoints

    public static class AiEndpoints
    {
        private const string BasePath = "/api/programs-score-analysis";
        private const string DocumentPath = "/api/rag";
        private const string ChatPath = "/api/chat";

        public static string AnalyzeAllProgramsFull =>
            $"{BasePath}/analyze/full";

        public static string AnalyzeSingleProgramFull(int climateProgramID) =>
            $"{BasePath}/analyze/{climateProgramID}/full";

        public static string AnalyzeSingleProgram(int climateProgramID) =>
            $"{BasePath}/analyze/{climateProgramID}";

        public static string AnalyzeProgramPillars(int climateProgramID) =>
            $"{BasePath}/analyze/{climateProgramID}/pillars";
        public static string AnalyzeSinglePillar(int climateProgramID, int pillarId) =>
            $"{BasePath}/analyze/{climateProgramID}/single-pillar/{pillarId}";

        public static string AnalyzeProgramQuestions(int climateProgramID) =>
            $"{BasePath}/analyze/{climateProgramID}/questions";

        public static string AnalyzeProgramPillarQuestions(int climateProgramID, int pillarId) =>
            $"{BasePath}/analyze/{climateProgramID}/pillars/{pillarId}/questions";
        public static string AnalyzeProgramImmediateSituation(int climateProgramID) =>
            $"{BasePath}/analyze/{climateProgramID}/immediateSituation";

        public static string ProcessDocument(int documentId) =>
            $"{DocumentPath}/process-document/{documentId}";
        public static string DeleteDocument(int documentId) =>
            $"{DocumentPath}/delete-document/{documentId}";

        public static string ChatProgramAsk() => $"{ChatPath}/program";
        public static string ChatGlobalAsk() => $"{ChatPath}/global";
        public static string KpiSummary() => $"{ChatPath}/kpi-summary";

        public static string CrossComparision() => $"{ChatPath}/cross-comparision";
        public static string ProgramSlides() => $"{ChatPath}/executive-slides";
        public static string EmergingTrendsAndIssues(int ProgramCount) =>
            $"{ChatPath}/emerging-trends-and-issues?ProgramCount={ProgramCount}";
        public static string PillarLiveSignals() => $"{ChatPath}/pillar-live-signals";
        public static string AnalyzeCityMissingQuestions() =>
          $"{BasePath}/analyze/missing-pillar-questions";

    }
    #endregion


    #region Ai Models 

    public class MissingProgramQuestionRequest 
    {
        public int ClimateProgramID { get; set; }
        public int? PillarID { get; set; }
    }

    public class ChatProgramAskQuestionRequest : ChatGlobalAskQuestionRequest
    {
        public int ClimateProgramID { get; set; }
        public int? PillarID { get; set; }
    }
    public class ChatGlobalAskQuestionRequest
    {
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }

    public class CrossComparisionRequest
    {
        public List<int> ClimateProgramIDs { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
    }
    public class ChatProgramAskQuestionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Result { get; set; }
    }

    public class KpiSummaryAiRequest
    {
        public string? ProgramName { get; set; }
        public string LayerName { get; set; } = string.Empty;
        public string LayerCode { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public decimal? ManualScore { get; set; }
        public decimal? AiScore { get; set; }
        public string? ManualCondition { get; set; }
        public string? AiCondition { get; set; }
        public List<KpiInterpretationBandAiDto> InterpretationBands { get; set; } = new();
        public string? CategoryDetails { get; set; }
    }

    public class KpiInterpretationBandAiDto
    {
        public decimal? MinRange { get; set; }
        public decimal? MaxRange { get; set; }
        public string? Condition { get; set; }
        public string? Descriptor { get; set; }
        public string? StrategicAction { get; set; }
    }

    public class KpiSummaryAiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public KpiSummaryAiResultDto? Result { get; set; }
    }

    public class KpiSummaryAiResultDto
    {
        public string Summary { get; set; } = string.Empty;
        public string? ScoreInterpretation { get; set; }
        public List<string> KeyTakeaways { get; set; } = new();
        public string? Outlook { get; set; }
    }

    #endregion
}
