namespace HornScope.Dtos.kpiDto
{
    public class CompareProgramResponseDto
    {
        public List<string> Categories { get; set; }
        public List<ChartSeriesDto> Series { get; set; }
        public List<ChartTableRowDto> TableData { get; set; }
    }

    public class ChartSeriesDto
    {
        public string Name { get; set; }
        public List<decimal> Data { get; set; }
        public List<decimal> AiData { get; set; }
    }

    public class ChartTableRowDto
    {
        public int LayerID { get; set; }
        public string LayerCode { get; set; }
        public string LayerName { get; set; }

        public string Purpose { get; set; }
        public List<ProgramValueDto> ProgramValues { get; set; }
        public decimal PeerProgramScore { get; set; }
    }

    public class ProgramValueDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public decimal Value { get; set; }
        public decimal AiValue { get; set; }
    }
}
