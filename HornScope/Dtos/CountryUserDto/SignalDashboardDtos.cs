namespace HornScope.Dtos.CountryUserDto
{

    public class SignalCardDto
    {
        public int LayerID { get; set; }

        public string LayerCode { get; set; } = string.Empty;

        public string LayerName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public decimal AIValue { get; set; }

        public DateTime? ManualUpdatedAt { get; set; }

        public DateTime? AiUpdatedAt { get; set; }

        public string AICondition { get; set; } = string.Empty;
        
        public decimal ManualValue { get; set; }

        public string ManualCondition { get; set; } = string.Empty;


        public string AiDescriptor { get; set; } = string.Empty;
        public string ManualDescriptor { get; set; } = string.Empty;

        public string StrategicAction { get; set; } = string.Empty;

        public int AIInterpretationID { get; set; }

        public string? AIInterpretationValue { get; set; }

        public int ManualInterpretationID { get; set; }

        public string? ManualInterpretationValue { get; set; }

        public bool IsAlert { get; set; }

        public bool IsAccessible { get; set; } = true;

        public List<FiveLevelInterpretationDto> Interpretations { get; set; } = new();

        public int? DisplayOrder { get; set; }

    }
    public class FiveLevelInterpretationDto
    {

        public int InterpretationID { get; set; }

        public int LayerID { get; set; }

        public decimal? MinRange { get; set; }

        public decimal? MaxRange { get; set; }

        public string Condition { get; set; } = string.Empty;

        public string Descriptor { get; set; } = string.Empty;

    }

    public class YearSignalPointDto
    {

        public int Year { get; set; }

        public decimal Value { get; set; }

    }

    public class SignalTrendDto
    {

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public int? DisplayOrder { get; set; }

        public List<YearSignalPointDto> Series { get; set; } = new();

    }

    public class NarrativeDto
    {

        public string Headline { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

    }


    public class PeaceStressTestDashboardDto
    {

        public int CountryID { get; set; }

        public int Year { get; set; }

        public decimal Vcp { get; set; }

        public decimal ProgramScore { get; set; }

        public decimal VcpDirectionalMovement { get; set; }

        public string VcpCondition { get; set; } = string.Empty;

        public string VcpDescriptor { get; set; } = string.Empty;

        public string VcpStrategicAction { get; set; } = string.Empty;

        public List<SignalCardDto> Signals { get; set; } = new();

        public List<SignalCardDto> PrimarySignals { get; set; } = new();

        public List<SignalCardDto> SecondarySignals { get; set; } = new();

        public List<NarrativeDto> Narratives { get; set; } = new();

    }



    public class EarlyWarningDashboardDto
    {

        public int CountryID { get; set; }

        public int Year { get; set; }

        public List<SignalCardDto> Alerts { get; set; } = new();

        public List<SignalTrendDto> TrendSeries { get; set; } = new();

        public string Outlook { get; set; } = string.Empty;

    }



    public class PeerResilienceDto
    {

        public int CountryID { get; set; }

        public string CountryName { get; set; } = string.Empty;

        public decimal Scs { get; set; }
        public int ScsRank { get; set; }

    }



    public class ReadinessScorecardDto
    {

        public int CountryID { get; set; }

        public int Year { get; set; }

        public decimal Scs { get; set; }

        public int RegionalRank { get; set; }

        public int RegionSampleSize { get; set; }

        public decimal PeerAverageScs { get; set; }

        public string InvestmentImplication { get; set; } = string.Empty;

        public List<SignalCardDto> ResilienceSignals { get; set; } = new();

        public List<SignalCardDto> PrimarySignals { get; set; } = new();

        public List<SignalCardDto> SecondarySignals { get; set; } = new();

        public List<PeerResilienceDto> Peers { get; set; } = new();

    }

}
