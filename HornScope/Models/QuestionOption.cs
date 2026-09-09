using System.Text.Json.Serialization;

namespace HornScope.Models
{
    public class QuestionOption
    {
        public int OptionID { get; set; }
        public int QuestionID { get; set; }
        public string OptionText { get; set; }
        public string ScoreValue { get; set; }
        public string? Label { get; set; }
        public int? DisplayOrder { get; set; }
        [JsonIgnore]
        public Question? Question { get; set; }  
    }
}
