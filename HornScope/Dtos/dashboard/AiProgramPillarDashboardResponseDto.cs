namespace HornScope.Dtos.dashboard
{
    public class AiProgramPillarDashboardResponseDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public decimal EvaluationValue { get; set; }
        public decimal AiValue { get; set; }
        public List<ProgramPillarDashboardPillarValueDto> Pillars { get; set; } = new List<ProgramPillarDashboardPillarValueDto>();
    }

    public class ProgramPillarDashboardPillarValueDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public decimal EvaluationValue { get; set; }
        public decimal AiValue { get; set; }
    }
}
