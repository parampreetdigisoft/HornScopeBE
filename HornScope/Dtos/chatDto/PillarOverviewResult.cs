namespace HornScope.Dtos.chatDto
{
    public class PillarOverviewCard
    {
        public int PillarId { get; set; }

        public string PillarName { get; set; } = string.Empty;

        public string ImagePath { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string AreasForImprovement { get; set; } = string.Empty;
    }

    public class PillarOverviewResult
    {
        public List<PillarOverviewCard> Pillars { get; set; } = new();
    }

    public class ChatPillarOverviewResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public PillarOverviewResult Result { get; set; } = new();
    }

    public class PillarOverviewDiskSnapshot
    {
        public DateTime SavedAtUtc { get; set; }

        public PillarOverviewResult Data { get; set; } = new();
    }
}
