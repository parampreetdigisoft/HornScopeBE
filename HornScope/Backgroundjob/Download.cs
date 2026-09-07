using HornScope.Models;
using DocumentFormat.OpenXml.Office2010.Excel;

namespace HornScope.Backgroundjob
{
    public class Download
    {
        private readonly ChannelService channelService;
        public Download(ChannelService channelService) 
        {
            this.channelService = channelService;
        }
        public string Type { get; set; } = string.Empty;
        public int? UserID { get; set; }
        public int? ClimateProgramID { get; set; }
        public bool ProgramEnable { get; set; }
        public bool PillarEnable { get; set; }
        public bool QuestionEnable { get; set; }
        public bool ImmediateSummaryEnable { get; set; }
        public bool RegenerateMissingQuestionsEnable { get; set; }
        public string InsertAnalyticalLayerResults(int climateProgramID = 0)
        {
            ClimateProgramID = climateProgramID;
            Type = "InsertAnalyticalLayerResults";
            channelService.Write(this);
            return "Execution has been started";
        }

        public Task AiResearchByClimateProgramID(int climateProgramID , bool programEnable,bool pillarEnable, bool questionEnable, bool regenerateMissingQuestionsEnable = false)
        {
            this.ClimateProgramID = climateProgramID;
            this.ProgramEnable = programEnable;
            this.PillarEnable = pillarEnable;
            this.QuestionEnable = questionEnable;
            this.RegenerateMissingQuestionsEnable = regenerateMissingQuestionsEnable;
            Type = "AiResearchByClimateProgramID";
            channelService.Write(this);
            return Task.CompletedTask;
        }
    }
}
