using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.AiDto
{
    public class AiProgramSummaryRequestDto : PaginationRequest
    {
        public int? ClimateProgramID { get; set; }
    }

    public class AiProgramPillarSummeryRequestDto : AiProgramSummaryRequestDto
    {
        public int? PillarID { get; set; }
    }

    public class AiProgramSummeryRequestPdfDto : AiProgramPillarRequestDto
    {
        public int? PillarID { get; set; }
        public HornScope.IServices.DocumentFormat Format { get; set; } = HornScope.IServices.DocumentFormat.Pdf;
        public string ReportType { get; set; } = "ai";
    }
    public class AiProgramPillarRequestDto
    {
        public int ClimateProgramID { get; set; }
    }
    public class AiProgramDocumentRequestDto : PaginationRequest
    {
        public int? ClimateProgramID { get; set; }
    }

    public class AiProgramPillarDocumentRequestDto 
    {
        public int ClimateProgramID { get; set; }
    }
    public class DeleteProgramDocumentRequestDto 
    {
        public int ClimateProgramID { get; set; }
        public int? ProgramDocumentID { get; set; }
        public bool IsAll { get; set; } = false;
    }

}
