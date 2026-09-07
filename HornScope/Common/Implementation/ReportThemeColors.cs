using SkiaSharp;

namespace HornScope.Common.Implementation
{
    /// <summary>Brand palette aligned with the VCP web application CSS variables.</summary>
    internal static class ReportThemeColors
    {
        public const string Primary = "#001A3E";
        public const string Secondary = "#A8E063";
        public const string HoverPrimary = "#4CAF50";
        public const string AccentGreen = "#4CAF50";
        public const string White = "#FFFFFF";
        public const string Black = "#000000";
        public const string Text = "#003D44";
        public const string Border = "#E4E4E4";
        public const string Background = "#F5F8F7";
        public const string LightText = "#4A5F62";
        public const string LightBg = "#7EC8CF";
        public const string DarkBg = "#005A62";
        public const string GreenText = "#003D44";

        // New interpretation-based colors
        public const string DarkGreen = "#1B5E20";     // Strong positive (>40)
        public const string Green = "#2E7D32";         // 20–40 (already similar to SuccessGreen)
        public const string Yellow = "#E6B800";        // -20 to 5 (neutral zone)
        public const string DarkRed = "#7F0000";       // Extreme negative (< -39)

        // OpenXML hex (no leading #)
        public const string PrimaryHex = "006D77";
        public const string SecondaryHex = "A8E063";
        public const string AccentGreenHex = "4CAF50";
        public const string TextHex = "003D44";
        public const string BorderHex = "E4E4E4";
        public const string BackgroundHex = "F5F8F7";
        public const string LightTextHex = "4A5F62";
        public const string LightBgHex = "7EC8CF";
        public const string DarkBgHex = "005A62";
        public const string WhiteHex = "FFFFFF";

        public const string HeaderSubtitle = "#B8E8EC";
        public const string HeaderMeta = "#7EC8CF";
        public const string SurfaceAlt = "#EEF6F5";
        public const string AccentLine = "#A8E063";

        // PDF core palette
        public const string PdfDarkGreen = "#12352f";
        public const string PdfMediumGreen = "#336b58";
        public const string PdfTealGreen = "#4CAF8A";
        public const string NavyBlue = Primary;
        public const string DarkBlue = "#001A3E";
        public const string PageBg = "#FAFAFA";
        public const string OverlayBlackAlpha = "#00000022";

        // PDF status / performance
        public const string SuccessGreen = "#2E7D32";
        public const string SuccessGreenLight = "#66BB6A";
        public const string SuccessGreenBg = "#E8F5E9";
        public const string SuccessGreenBorder = "#C8E6C9";
        public const string SuccessGreenText = "#1B5E20";
        public const string SuccessGreenMuted = "#A5D6A7";
        public const string SuccessGreenSoft = "#a5d6c2";
        public const string BarGreenLow = "#469449";
        public const string ProgressGreen = "#22A06B";
        public const string RankGreen = "#16A34A";

        public const string WarningAmber = "#F9A825";
        public const string WarningAmberLight = "#FFD54F";
        public const string WarningAmberBg = "#FFF8E1";
        public const string WarningOrange = "#FFC107";
        public const string WarningOrangeBg = "#FFF3E0";
        public const string WarningOrangeText = "#5D3B00";
        public const string WarningOrangeDark = "#E65100";
        public const string WarningGold = "#F0B429";
        public const string WarningGoldAlt = "#F5A623";
        public const string WarningBronze = "#cd7f32";
        public const string BarOrangeMid = "#c66528";

        public const string DangerRed = "#C62828";
        public const string DangerRedDark = "#B71C1C";
        public const string DangerRedBg = "#FDECEA";
        public const string DangerRedBorder = "#FFCDD2";
        public const string DangerRedLight = "#EF5350";
        public const string DangerRedAccent = "#e05252";
        public const string DangerRedFlag = "#ED561A";
        public const string DangerRedFlagAlt = "#eb4634";
        public const string DangerRedBootstrap = "#D9534F";

        public const string AccentGreenAlpha15 = "#4CAF5025";
        public const string WarningOrangeAlpha15 = "#FFC10725";
        public const string DangerRedAlpha15 = "#EF535025";
        public const string WhiteAlpha73 = "#FFFFFFBB";

        // PDF neutrals / greys
        public const string Gray50 = "#F9FAFB";
        public const string Gray100 = "#F5F5F5";
        public const string Gray150 = "#F0F0F0";
        public const string Gray200 = "#EBEBEB";
        public const string Gray250 = "#E8E8E8";
        public const string Gray300 = "#E5E7EB";
        public const string Gray350 = "#E0E0E0";
        public const string Gray400 = "#DDDDDD";
        public const string Gray450 = "#C0C0C0";
        public const string Gray500 = "#9E9E9E";
        public const string Gray550 = "#9CA3AF";
        public const string Gray600 = "#888888";
        public const string Gray650 = "#757575";
        public const string Gray700 = "#666666";
        public const string Gray750 = "#616161";
        public const string Gray800 = "#555555";
        public const string Gray850 = "#444444";
        public const string Gray900 = "#333333";
        public const string Gray950 = "#212121";
        public const string GrayMuted = "#aaaaaa";
        public const string GraySilver = "#a5a8ad";
        public const string GrayLight = "#999999";
        public const string GrayTailwind700 = "#374151";
        public const string GrayTailwind800 = "#1F2937";
        public const string GrayTailwind900 = "#111827";
        public const string GrayTailwind500 = "#4B5563";
        public const string GrayTailwind400 = "#6B7280";
        public const string BlueGray = "#37474F";
        public const string BlueGrayDark = "#546E7A";
        public const string BlueGrayLight = "#B0BEC5";

        // PDF surfaces / borders
        public const string SurfaceGreen = "#E8F0EC";
        public const string SurfaceGreenAlt = "#EEF5F1";
        public const string SurfaceGreenLight = "#F0F4F1";
        public const string SurfaceGreenPale = "#F2F7F4";
        public const string SurfaceGreenRow = "#f4f7f5";
        public const string SurfaceGreenMint = "#F2F6F4";
        public const string SurfaceSelected = "#fff9e6";
        public const string SurfaceRowAlt = "#F7F7F7";
        public const string BorderGreen = "#D8E8E2";
        public const string BorderGreenLight = "#DDE8E3";
        public const string BorderGreenMid = "#C5D9D0";
        public const string BorderBlue = "#afc4db";
        public const string BorderDivider = "#d9e2df";
        public const string BorderLight = "#EEEEEE";
        public const string DividerGray = "#eeeeee";

        // PDF chart blues
        public const string ChartDarkBlue = "#1B2F44";
        public const string ChartNavy = "#1F4E79";
        public const string ChartMediumBlue = "#2C6EA3";
        public const string ChartSteelBlue = "#4F7FA8";
        public const string ChartBlue = "#1E88E5";
        public const string ChartBlueLight = "#B8CCE0";
        public const string ChartBluePale = "#D6E3F0";
        public const string ChartBlueMint = "#D0E8DC";
        public const string RankBlue = "#2563EB";
        public const string BootstrapInfo = "#5BC0DE";

        // PDF header text accents
        public const string HeaderTextPale = "#E8F3F0";
        public const string HeaderTextMuted = "#CFE3DD";

        // PDF pillar section accents
        public const string AccentExecutiveSummary = "#163329";
        public const string AccentKeyDevelopments = "#1f4e79";
        public const string AccentCriticalRisks = "#2e75b6";
        public const string AccentGaps = "#5b9bd5";
        public const string AccentStructuralEvidence = "#e6ccff";
        public const string AccentOperationalEvidence = "#c2f0f0";
        public const string AccentOutcomeEvidence = "#ffe6cc";
        public const string AccentPerceptionEvidence = "#e6f7ff";
        public const string AccentTemporalScope = "#d9e6ff";
        public const string AccentDistortionScreening = "#f2d9e6";
        public const string AccentRelationalIntegrity = "#f0ffe6";
        public const string AccentGeopoliticalShock = "#ffd9cc";
        public const string AccentFinanceShock = "#fff2cc";
        public const string AccentLegitimacyShock = "#e6f2ff";
        public const string AccentStressResilience = "#e6ffe6";
        public const string AccentStressAdjustment = "#ffe6f2";
        public const string AccentInclusionEquityAdj = "#f9e6ff";
        public const string AccentOpacityRisk = "#fff0e6";
        public const string AccentNonCompensation = "#e6fff9";
        public const string AccentCrossPillar = "#6e9688";
        public const string AccentInstitutionalCapacity = "#0d8057";
        public const string AccentEquityAssessment = "#e8f5e9";
        public const string AccentGovernanceTrajectory = "#fce4ec";
        public const string AccentStrategicPolicy = "#2e9975";
        public const string AccentAssessmentValue = "#63a68f";
        public const string AccentPerceptionEvidenceAlt = "#9dc3e6";
        public const string AccentTemporalScopeAlt = "#5f497a";
        public const string AccentDistortionScreeningAlt = "#8064a2";
        public const string AccentRelationalIntegrityAlt = "#b1a0c7";
        public const string AccentGeopoliticalShockAlt = "#7f6000";
        public const string AccentFinanceShockAlt = "#bf9000";
        public const string AccentLegitimacyShockAlt = "#ffd966";
        public const string AccentStressResilienceAlt = "#c55a11";
        public const string AccentStressAdjustmentAlt = "#e26b0a";
        public const string AccentInclusionEquityAdjAlt = "#274e13";
        public const string AccentOpacityRiskAlt = "#38761d";
        public const string AccentNonCompensationAlt = "#6aa84f";
        public const string AccentDataGap = "#a4bab2";

        // PDF source type badges
        public const string SourceGovernment = "#133328";
        public const string SourceAcademic = "#172923";
        public const string SourceInternational = "#4d7d6d";
        public const string SourceNewsNgo = "#1ec990";
        public const string SourceDefault = "#0eeba1";

        // PDF section styling
        public const string SectionAccentBar = "#396154";
        public const string SectionTitleGreen = "#2c423b";
        public const string LabelGreen = "#305246";
        public const string SectionContentText = "#424242";
        public const string DeepTeal = "#0d8057";

        // PDF income tier colors
        public const string IncomeLow = "#D9534F";
        public const string IncomeLowerMiddle = "#F0AD4E";
        public const string IncomeUpperMiddle = "#5BC0DE";
        public const string IncomeHigh = "#2E7D32";

        // PDF chart palette colors
        public const string ChartPurple = "#7B61FF";
        public const string ChartOrange = "#FB8C00";
        public const string ChartGreen = "#43A047";
        public const string CyanTeal = "#0097A7";
        public const string BrownGray = "#8D6E63";
        public const string PinkRed = "#E91E63";
        public const string SlateBlue = "#607D8B";

        public static readonly string[] ProgramChartPalette =
        {
            WarningGold,
            PdfTealGreen,
            ChartBlue,
            ChartOrange,
            ChartPurple,
            DangerRedAccent
        };

        public static readonly string[] PillarChartPalette =
        {
            PdfDarkGreen, PdfMediumGreen, PdfTealGreen, WarningGold, WarningGoldAlt,
            DangerRedAccent, ChartPurple, ChartBlue, ChartGreen, ChartOrange,
            CyanTeal, BrownGray, PinkRed, SlateBlue
        };

        public static readonly string[] IncomeTierPalette =
        {
            IncomeLow, IncomeLowerMiddle, IncomeUpperMiddle, AccentGreen
        };

        public static SKShader CreateVcpGradient(float width, float height) =>
            SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                new[]
                {
                    SKColor.Parse(Primary),
                    SKColor.Parse(AccentGreen),
                    SKColor.Parse(Secondary)
                },
                new[] { 0f, 0.52f, 1f },
                SKShaderTileMode.Clamp);

        public static void DrawVcpGradient(SKCanvas canvas, float width, float height)
        {
            using var paint = new SKPaint { IsAntialias = true, Shader = CreateVcpGradient(width, height) };
            canvas.DrawRect(0, 0, width, height, paint);
        }
    }
}
