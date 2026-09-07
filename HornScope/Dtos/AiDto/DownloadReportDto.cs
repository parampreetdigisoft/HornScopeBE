namespace HornScope.Dtos.AiDto
{
    public class DownloadReportDto
    {
        public List<int>? ClimateProgramIDs { get; set; }
        public IServices.DocumentFormat Format { get; set; } = IServices.DocumentFormat.Pdf;

    }
}
