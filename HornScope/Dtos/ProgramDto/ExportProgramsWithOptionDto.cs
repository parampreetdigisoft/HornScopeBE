namespace HornScope.Dtos.ProgramDto
{
    public class ExportProgramsWithOptionDto
    {
        public bool? IsRanking { get; set; }
        public bool? IsAllProgram { get; set; }
        public bool? IsPillarLevel { get; set; }
        public List<int>? ClimateProgramIDs { get; set; }
    }
}
