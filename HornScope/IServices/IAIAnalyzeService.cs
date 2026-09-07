using HornScope.Dtos.chatDto;
using HornScope.Services;

namespace HornScope.IServices
{
    public interface IAIAnalyzeService
    {
        Task AnalyzeAllProgramsFull();
        Task AnalyzeSingleProgramFull(int climateProgramID);
        Task AnalyzeSingleProgram(int climateProgramID);
        Task AnalyzeProgramPillars(int climateProgramID);
        Task AnalyzeSinglePillar(int climateProgramID, int pillarId);
        Task AnalyzeQuestionsOfProgram(int climateProgramID);
        Task AnalyzeQuestionsOfProgramPillar(int climateProgramID, int pillarId);
        Task AnalyzeProgramMissingQuestions(MissingProgramQuestionRequest r);
        Task ProcessDocument(int documentID);
        Task DeleteDocument(int documentID);
        Task AnalyzeProgramImmediateSituation(int climateProgramID);
        Task<ChatProgramAskQuestionResponse> ChatProgramAsk(ChatProgramAskQuestionRequest request);
        Task<ChatProgramAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request);
        Task<ChatProgramAskQuestionResponse> CrossComparision(CrossComparisionRequest request);
        Task<KpiSummaryAiResponse?> SummarizeKpiPerformance(KpiSummaryAiRequest request);

        Task<ChatProgramExecutiveSlidesResponse?> GetProgramSlides(int climateProgramID);
        Task<ChatEmergingTrendsResponse?> GetEmergingTrendsAndIssues(int programCount);
        Task<ChatPillarLiveSignalsResponse?> GetPillarLiveSignals();
        Task RunEvery2HoursJob();
        Task RunDailyJob();
        Task RunMonthlyJob();
    }
}
