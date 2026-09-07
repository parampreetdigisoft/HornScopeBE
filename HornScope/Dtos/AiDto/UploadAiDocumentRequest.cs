namespace HornScope.Dtos.AiDto
{
    public class UploadAiDocumentRequest
    {
        public int? ClimateProgramID { get; set; }
        public List<IFormFile> Files { get; set; }
        public List<int> PillarIDs { get; set; } 
    }


}
