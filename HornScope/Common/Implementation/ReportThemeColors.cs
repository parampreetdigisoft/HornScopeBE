using SkiaSharp;

namespace HornScope.Common.Implementation
{
    /// <summary>
    /// Brand palette aligned with HornScope web CSS variables
    /// (navy + gold from hornscope-theme.css).
    /// </summary>
    internal static class ReportThemeColors
    {
        public const string BrandName = "HornScope";
        public const string BrandTagline = "Strategic Intelligence for the Horn of Africa and East Africa";

        public static string LogoPath =>
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "images", "Logo-hornscope-large.png");

        public const string Primary = "#C5A05A";
        public const string Secondary = "#D4B86A";
        public const string HoverPrimary = "#8B6B32";
        public const string AccentGreen = "#5A9B8A";
        public const string White = "#FFFFFF";
        public const string Black = "#000000";
        public const string Text = "#002147";
        public const string Border = "#D5DEE8";
        public const string Background = "#F4F7FA";
        public const string LightText = "#8FA3B5";
        public const string LightBg = "#C5A05A";
        public const string DarkBg = "#002147";
        public const string GreenText = "#002147";

        // OpenXML hex (no leading #)
        public const string PrimaryHex = "C5A05A";
        public const string SecondaryHex = "D4B86A";
        public const string AccentGreenHex = "5A9B8A";
        public const string TextHex = "002147";
        public const string BorderHex = "D5DEE8";
        public const string BackgroundHex = "F4F7FA";
        public const string LightTextHex = "8FA3B5";
        public const string LightBgHex = "C5A05A";
        public const string DarkBgHex = "002147";
        public const string WhiteHex = "FFFFFF";
        public const string PdfDarkGreenHex = "002147";
        public const string SurfaceGreenMintHex = "F4F7FA";
        public const string SurfaceGreenRowHex = "E8EEF4";
        public const string SuccessGreenBgHex = "E8F0ED";
        public const string SuccessGreenSoftHex = "D4B86A";
        public const string HeaderSubtitleHex = "E8EEF4";
        public const string CreamHex = "E8EEF4";

        public const string HeaderSubtitle = "#E8EEF4";
        public const string HeaderMeta = "#C5A05A";
        public const string SurfaceAlt = "#E8EEF4";
        public const string AccentLine = "#C5A05A";

        // PDF core palette (names kept for call-site compatibility)
        public const string PdfDarkGreen = "#002147";
        public const string PdfMediumGreen = "#0C2238";
        public const string PdfTealGreen = "#C5A05A";
        public const string NavyBlue = "#002147";
        public const string PageBg = "#F4F7FA";
        public const string OverlayBlackAlpha = "#00000022";

        // PDF status / performance
        public const string SuccessGreen = "#B7A25A";
        public const string SuccessGreenLight = "#D4B45E";
        public const string SuccessGreenBg = "#F5EFE0";
        public const string SuccessGreenBorder = "#E6D9C0";
        public const string SuccessGreenText = "#5C4A1A";
        public const string SuccessGreenMuted = "#C9C7BF";
        public const string SuccessGreenSoft = "#E7C878";
        public const string BarGreenLow = "#C9A24A";
        public const string ProgressGreen = "#C9A24A";
        public const string RankGreen = "#B7A25A";

        public const string WarningAmber = "#E7C878";
        public const string WarningAmberLight = "#F0D78A";
        public const string WarningAmberBg = "#F8F1DE";
        public const string WarningOrange = "#C9A24A";
        public const string WarningOrangeBg = "#F5EFE0";
        public const string WarningOrangeText = "#5C4A1A";
        public const string WarningOrangeDark = "#8A5A2B";
        public const string WarningGold = "#C9A24A";
        public const string WarningGoldAlt = "#E7C878";
        public const string WarningBronze = "#8A5A2B";
        public const string BarOrangeMid = "#8A5A2B";

        public const string DangerRed = "#B5502E";
        public const string DangerRedDark = "#8C3A1F";
        public const string DangerRedBg = "#F8EBE4";
        public const string DangerRedBorder = "#E8C4B4";
        public const string DangerRedLight = "#C46A3A";
        public const string DangerRedAccent = "#B5502E";
        public const string DangerRedFlag = "#B5502E";
        public const string DangerRedFlagAlt = "#C46A3A";
        public const string DangerRedBootstrap = "#B5502E";

        public const string AccentGreenAlpha15 = "#B7A25A25";
        public const string WarningOrangeAlpha15 = "#C9A24A25";
        public const string DangerRedAlpha15 = "#B5502E25";
        public const string WhiteAlpha73 = "#FFFFFFBB";

        // PDF neutrals / greys (warm-shifted)
        public const string Gray50 = "#FAF6EF";
        public const string Gray100 = "#F7F1E6";
        public const string Gray150 = "#F2EBE1";
        public const string Gray200 = "#EDE6D8";
        public const string Gray250 = "#E6DDD0";
        public const string Gray300 = "#E6DDD0";
        public const string Gray350 = "#D9D0C0";
        public const string Gray400 = "#D4CBBB";
        public const string Gray450 = "#C9C7BF";
        public const string Gray500 = "#9C9484";
        public const string Gray550 = "#8B887E";
        public const string Gray600 = "#8B887E";
        public const string Gray650 = "#6B6358";
        public const string Gray700 = "#6B6358";
        public const string Gray750 = "#5C5348";
        public const string Gray800 = "#4A4338";
        public const string Gray850 = "#332C1D";
        public const string Gray900 = "#1A1510";
        public const string Gray950 = "#0A0906";
        public const string GrayMuted = "#9C9484";
        public const string GraySilver = "#C9C7BF";
        public const string GrayLight = "#8B887E";
        public const string GrayTailwind700 = "#4A4338";
        public const string GrayTailwind800 = "#1A1510";
        public const string GrayTailwind900 = "#0A0906";
        public const string GrayTailwind500 = "#6B6358";
        public const string GrayTailwind400 = "#8B887E";
        public const string BlueGray = "#4A4338";
        public const string BlueGrayDark = "#6B6358";
        public const string BlueGrayLight = "#C9C7BF";

        // PDF surfaces / borders
        public const string SurfaceGreen = "#F7F1E6";
        public const string SurfaceGreenAlt = "#F2EBE1";
        public const string SurfaceGreenLight = "#FAF6EF";
        public const string SurfaceGreenPale = "#F9F4EA";
        public const string SurfaceGreenRow = "#F7F1E6";
        public const string SurfaceGreenMint = "#F7F1E6";
        public const string SurfaceSelected = "#F8F1DE";
        public const string SurfaceRowAlt = "#FAF6EF";
        public const string BorderGreen = "#E6DDD0";
        public const string BorderGreenLight = "#EDE6D8";
        public const string BorderGreenMid = "#D9D0C0";
        public const string BorderBlue = "#D4CBBB";
        public const string BorderDivider = "#C9A24A";
        public const string BorderLight = "#EDE6D8";
        public const string DividerGray = "#E6DDD0";

        // PDF chart colours (gold / bronze / silver)
        public const string ChartDarkBlue = "#1A1510";
        public const string ChartNavy = "#8A5A2B";
        public const string ChartMediumBlue = "#C9A24A";
        public const string ChartSteelBlue = "#B7A25A";
        public const string ChartBlue = "#C9A24A";
        public const string ChartBlueLight = "#E7C878";
        public const string ChartBluePale = "#F2EBE1";
        public const string ChartBlueMint = "#EDE6D8";
        public const string RankBlue = "#8A5A2B";
        public const string BootstrapInfo = "#C9C7BF";

        // PDF header text accents
        public const string HeaderTextPale = "#E8EEF4";
        public const string HeaderTextMuted = "#8FA3B5";

        // PDF pillar section accents
        public const string AccentExecutiveSummary = "#1A1510";
        public const string AccentKeyDevelopments = "#8A5A2B";
        public const string AccentCriticalRisks = "#C9A24A";
        public const string AccentGaps = "#B7A25A";
        public const string AccentKeyFindings = "#5C4A1A";
        public const string AccentRecommendations = "#8A5A2B";
        public const string AccentStructuralEvidence = "#C9A24A";
        public const string AccentOperationalEvidence = "#8A5A2B";
        public const string AccentOutcomeEvidence = "#B7A25A";
        public const string AccentPerceptionEvidence = "#C9C7BF";
        public const string AccentTemporalScope = "#6B6358";
        public const string AccentDistortionScreening = "#A67C3D";
        public const string AccentRelationalIntegrity = "#D4B45E";
        public const string AccentPoliticalShock = "#B5502E";
        public const string AccentEconomicShock = "#C9A24A";
        public const string AccentNarrativeShock = "#E7C878";
        public const string AccentStressResilience = "#B7A25A";
        public const string AccentStressAdjustment = "#C46A3A";
        public const string AccentInequalityAdj = "#8A5A2B";
        public const string AccentOpacityRisk = "#A67C3D";
        public const string AccentNonCompensation = "#C9C7BF";
        public const string AccentCrossPillar = "#8A5A2B";
        public const string AccentInstitutionalCapacity = "#8A5A2B";
        public const string AccentEquityAssessment = "#F5EFE0";
        public const string AccentConflictRisk = "#B5502E";
        public const string AccentStrategicPolicy = "#C9A24A";
        public const string AccentDataTransparency = "#B7A25A";
        public const string AccentPerceptionEvidenceAlt = "#C9C7BF";
        public const string AccentTemporalScopeAlt = "#6B6358";
        public const string AccentDistortionScreeningAlt = "#8A5A2B";
        public const string AccentRelationalIntegrityAlt = "#B7A25A";
        public const string AccentPoliticalShockAlt = "#8A5A2B";
        public const string AccentEconomicShockAlt = "#C9A24A";
        public const string AccentNarrativeShockAlt = "#E7C878";
        public const string AccentStressResilienceAlt = "#8A5A2B";
        public const string AccentStressAdjustmentAlt = "#C46A3A";
        public const string AccentInequalityAdjAlt = "#5C4A1A";
        public const string AccentOpacityRiskAlt = "#8A5A2B";
        public const string AccentNonCompensationAlt = "#B7A25A";
        public const string AccentDataGap = "#C9C7BF";

        // PDF source type badges
        public const string SourceGovernment = "#1A1510";
        public const string SourceAcademic = "#332C1D";
        public const string SourceInternational = "#8A5A2B";
        public const string SourceNewsNgo = "#C9A24A";
        public const string SourceDefault = "#B7A25A";

        // PDF section styling
        public const string SectionAccentBar = "#C9A24A";
        public const string SectionTitleGreen = "#1A1510";
        public const string LabelGreen = "#8A5A2B";
        public const string SectionContentText = "#4A4338";
        public const string DeepTeal = "#8A5A2B";

        // PDF income tier colors
        public const string IncomeLow = "#B5502E";
        public const string IncomeLowerMiddle = "#C9A24A";
        public const string IncomeUpperMiddle = "#C9C7BF";
        public const string IncomeHigh = "#B7A25A";

        // PDF chart palette colors
        public const string ChartPurple = "#8B887E";
        public const string ChartOrange = "#C46A3A";
        public const string ChartGreen = "#B7A25A";
        public const string CyanTeal = "#A67C3D";
        public const string BrownGray = "#8A5A2B";
        public const string PinkRed = "#B5502E";
        public const string SlateBlue = "#6B6358";

        public static readonly string[] CountryChartPalette =
        {
            Primary,
            Secondary,
            HoverPrimary,
            GraySilver,
            AccentGreen,
            DangerRed
        };

        public static readonly string[] PillarChartPalette =
        {
            PdfDarkGreen, PdfMediumGreen, CyanTeal, Primary, SuccessGreenLight,
            Secondary, AccentGreen, GraySilver, GrayLight, DangerRed,
            ChartOrange, SlateBlue, HeaderSubtitle, Gray850
        };

        public static readonly string[] IncomeTierPalette =
        {
            IncomeLow, IncomeLowerMiddle, IncomeUpperMiddle, AccentGreen
        };

        public static SKShader CreateAhiGradient(float width, float height) =>
            SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                new[]
                {
                    SKColor.Parse(Secondary),
                    SKColor.Parse(Primary),
                    SKColor.Parse(HoverPrimary)
                },
                new[] { 0f, 0.48f, 1f },
                SKShaderTileMode.Clamp);

        public static void DrawAhiGradient(SKCanvas canvas, float width, float height)
        {
            using var paint = new SKPaint { IsAntialias = true, Shader = CreateAhiGradient(width, height) };
            canvas.DrawRect(0, 0, width, height, paint);
        }
    }
}
