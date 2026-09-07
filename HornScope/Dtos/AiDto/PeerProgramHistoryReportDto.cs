namespace HornScope.Dtos.AiDto
{
    public class PeerProgramHistoryReportDto
    {
        public int ClimateProgramID { get; set; }
        public string Location { get; set; }
        public string ProgramName { get; set; }
        public string Program { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? Image { get; set; }   
        public int? Year { get; set; }   
        public string? Description { get; set; }
        public List<PeerProgramYearHistoryDto> ProgramHistory { get; set; }
    }

    public class PeerProgramYearHistoryDto
    {
        public int ClimateProgramID { get; set; }
        public int Year { get; set; } = 0;
        public decimal ScoreProgress { get; set; }
        public List<PeerProgramPillarHistoryReportDto> Pillars { get; set; }
    }

    public class PeerProgramPillarHistoryReportDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public decimal ScoreProgress { get; set; }

    }
}
