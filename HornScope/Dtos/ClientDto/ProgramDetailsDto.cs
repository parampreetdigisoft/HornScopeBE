namespace HornScope.Dtos.ClientDto
{
    public class ProgramDetailsDto
    {
        public int ClimateProgramID { get; set; } = 0;
        public int TotalEvaluation { get; set; } = 0;
        public decimal TotalScore { get; set; } = 0;
        public decimal ScoreProgress { get; set; } = 0;
        public int TotalPillar { get; set; } = 0;
        public int TotalAnsPillar { get; set; } = 0;
        public int TotalQuestion { get; set; } = 0;
        public int AnsQuestion { get; set; } = 0;
        public decimal AvgHighScore { get; set; } = 0;
        public decimal AvgLowerScore { get; set; } = 0;

        public List<ProgramPillarDetailsDto> Pillars { get; set; } = new List<ProgramPillarDetailsDto>();
    }

    public class ProgramPillarDetailsDto
    {
        public int PillarID { get; set; } = 0;
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public decimal TotalScore { get; set; } = 0;
        public decimal ScoreProgress { get; set; } = 0;
        public int TotalPillar { get; set; } = 0;
        public int TotalAnsPillar { get; set; } = 0;
        public int TotalQuestion { get; set; } = 0;
        public int AnsQuestion { get; set; } = 0;
        public decimal AvgHighScore { get; set; } = 0;
        public decimal AvgLowerScore { get; set; } = 0;
        public int TotalUnKnown { get; set; } = 0;
        public int TotalNA { get; set; } = 0;
        public bool IsAccess { get; set; } = false;
    }
}
