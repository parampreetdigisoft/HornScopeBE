using HornScope.Models;

namespace HornScope.Dtos.AiDto
{
    public class GetProgramDocumentResponseDto
    {
        public int? ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public int NoOfUsers { get; set; }
        public int NoOfFiles { get; set; }
        public string FileTypes { get; set; }
        public long? FilesSize { get; set; }       
    }

    public class GetProgramPillarDocumentResponseDto
    {
        public int ProgramDocumentID { get; set; }
        public int? ClimateProgramID { get; set; }
        public int? PillarID { get; set; }
        public string? PillarName { get; set; }
        public string FileName { get; set; }
        public string StoredFileName { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public long? FileSize { get; set; }
        public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Pending;
        public int UploadedByUserID { get; set; }
        public string UploadedBy { get; set; }
    }
}
