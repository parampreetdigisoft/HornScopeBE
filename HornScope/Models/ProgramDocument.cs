using DocumentFormat.OpenXml.Bibliography;

namespace HornScope.Models
{
    public class ProgramDocument
    {
        public int ProgramDocumentID { get; set; }
        public int? ClimateProgramID { get; set; } // if program id is null then document treated as global docx for the site 
        public int? PillarID { get; set; }
        public string FileName { get; set; }
        public string StoredFileName { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public long? FileSize { get; set; }
        public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Pending;
        public int? UploadedByUserID { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string DocumentLevel { get; set; } // Global,Program,Program_Pillar
    }

    public enum DocumentProcessingStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }
}
