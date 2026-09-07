namespace HornScope.Dtos.AiDto
{
    public class AiCrossProgramsResponseDto
    {
        public List<string> Categories { get; set; } = new List<string>();
        public List<CrossProgramsChartSeriesDto> Series { get; set; } = new List<CrossProgramsChartSeriesDto>();
        public List<CrossProgramsChartTableRowDto> TableData { get; set; } = new List<CrossProgramsChartTableRowDto>();
    }

    public class CrossProgramsChartSeriesDto
    {
        public string Name { get; set; }
        public List<decimal> Data { get; set; }
    }

    public class CrossProgramsChartTableRowDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public decimal Value { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CrossProgramsPillarValueDto> PillarValues { get; set; } = new List<CrossProgramsPillarValueDto>();
    }

    public class CrossProgramsPillarValueDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public decimal Value { get; set; }
        public bool IsAccess { get; set; }
    }
}
