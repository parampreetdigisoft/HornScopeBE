// ═══════════════════════════════════════════════════════════════════════════
//  DocxGeneratorService.cs
//
//  NuGet package required (free):
//    DocumentFormat.OpenXml  >=  3.0.0   (MIT, by Microsoft)
//    SkiaSharp                            (already present via QuestPDF)
//
//  Architecture:
//   • Charts are rendered to PNG via SkiaSharp (reusing the same paint
//     methods used by PdfGeneratorService) and embedded as images.
//   • Text sections, progress bars, and data tables use native OpenXML.
//   • The IDocxGeneratorService interface mirrors IPdfGeneratorService so
//     the DocumentGeneratorService facade can swap them transparently.
// ═══════════════════════════════════════════════════════════════════════════


using SkiaSharp;
using DocumentFormat.OpenXml;
using HornScope.Models;
using HornScope.IServices;
using HornScope.Dtos.AiDto;
using DocumentFormat.OpenXml.Packaging;
using HornScope.Common.Interface;
using DocumentFormat.OpenXml.Wordprocessing;
using static HornScope.Services.AIComputationService;

// Aliases to avoid clashes with System.Drawing / Wordprocessing
using A    = DocumentFormat.OpenXml.Drawing;
using DW   = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC  = DocumentFormat.OpenXml.Drawing.Pictures;
using QPDF = QuestPDF.Infrastructure;

namespace HornScope.Common.Implementation
{
    public sealed partial class DocxGeneratorService : IDocxGeneratorService
    {
        // ── Constants ────────────────────────────────────────────────────────
        // All DXA values are "twentieths of a point" (1 inch = 1440 DXA).
        // All EMU values are "English Metric Units"  (1 inch = 914 400 EMU).

        private const uint   PageWidthDxa    = 11906;   // A4 width
        private const uint   PageHeightDxa   = 16838;   // A4 height
        private const int    MarginDxa       = 720;     // 0.5 inch margins
        private const int    ContentDxa      = (int)(PageWidthDxa - 2 * MarginDxa); // 10 466 DXA
        private const long   ContentWidthEmu = 6_645_000L;   // ≈ 7.27 inch in EMU
        private const long   HalfWidthEmu    = 3_220_000L;   // ≈ 3.52 inch in EMU
        private const string DarkBlue       = ReportThemeColors.DarkBlue;
        private const string White           = ReportThemeColors.WhiteHex;

        // Unique image ID counter — reset per document
        private uint _imgId;

        private readonly IAppLogger _appLogger;

        public DocxGeneratorService(IAppLogger appLogger)
            => _appLogger = appLogger;

        // ════════════════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════
        public async Task<byte[]> GenerateProgramDetailsDocx(
            AiProgramSummeryDto programDetails,
            List<AiProgramPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerProgramHistoryReportDto> peerPrograms,
            UserRole userRole)
        {
            try
            {
                return BuildDocument(mainPart =>
                {
                    AddProgramDetailsSections(mainPart.Document.Body!, mainPart, programDetails, pillars, kpis, peerPrograms, userRole);
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GenerateProgramDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GeneratePillarDetailsDocx(AiProgramPillarResponse pillarData, UserRole userRole)
        {
            try
            {
                var programDetails = new AiProgramSummeryDto
                {
                    ClimateProgramID = pillarData.ClimateProgramID,
                    ProgramName = pillarData.ProgramName,
                    Location = pillarData.Location,                   
                    Year = pillarData.AIDataYear,
                    AIProgress = pillarData.AIProgress

                };

                return BuildDocument(mainPart =>
                {
                    AppendProgramHeader(mainPart, programDetails, pillarData.PillarName);
                    AddPillarSection(mainPart.Document.Body!, mainPart, pillarData, userRole);
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GeneratePillarDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GenerateAllProgramsDetailsDocx(
            List<AiProgramSummeryDto> programs,
            Dictionary<int, List<AiProgramPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole)
        {
            try
            {
                return BuildDocument(mainPart =>
                {
                    foreach (var program in programs)
                    {
                        if (!pillarsDict.TryGetValue(program.ClimateProgramID, out var pillars) || !pillars.Any())
                            continue;

                        var programKpis = kpis?.Where(k => k.ClimateProgramID == program.ClimateProgramID).ToList()
                                       ?? new List<KpiChartItem>();

                        AddProgramDetailsSections(mainPart.Document.Body!, mainPart, program, pillars, programKpis,
                                               new List<PeerProgramHistoryReportDto>(), userRole, isAllPrograms: true);
                    }
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GenerateAllProgramsDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DOCUMENT SHELL HELPER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Creates a blank A4 document, calls <paramref name="populate"/>, returns bytes.</summary>
        private byte[] BuildDocument(Action<MainDocumentPart> populate)
        {
            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());

                _imgId = 1;
                _pendingHeaderRelId = null;

                populate(mainPart);
                FinalizeLastSection(mainPart);

                mainPart.Document.Save();
            }
            return ms.ToArray();
        }

        // ════════════════════════════════════════════════════════════════════
        //  PROGRAM REPORT  –  SECTION COMPOSITION
        // ════════════════════════════════════════════════════════════════════
        private void AddProgramDetailsSections(
            Body body, MainDocumentPart mainPart,
            AiProgramSummeryDto programDetails,
            List<AiProgramPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerProgramHistoryReportDto> peerPrograms,
            UserRole userRole,
            bool isAllPrograms = false)
        {
            var kpiChartItems = kpis.OrderByDescending(x => x.Value).ToList();
            var pillarChartItems = pillars
                .Select(p => new PillarChartItem(
                    p.PillarName?.Length > 20 ? p.PillarName[..20] : p.PillarName ?? "—",
                    p.PillarName ?? "—",
                    p.AIProgress))
                .ToList();

            // ── 1. Global Dashboard ──────────────────────────────────────────────────
            AppendProgramHeader(mainPart, programDetails, "Program Performance Dashboard");
            AddDashboardSection(body, mainPart, programDetails, pillarChartItems, kpiChartItems);

            // ── 2. Program Summary ──────────────────────────────────────────────────────
            AppendProgramHeader(mainPart, programDetails, null);
            AddProgramSummarySection(body, mainPart, programDetails, userRole, isAllPrograms);

            // ── 3. Pillar Radial Overview (same as PDF, including all-programs) ───────
            if (pillars.Any())
            {
                AppendProgramHeader(mainPart, programDetails, "Pillar Performance Overview");
                AddPillarOverviewSection(body, mainPart, pillarChartItems);
            }

            if (!isAllPrograms)
            {
                if (peerPrograms.Any())
                {
                    AddPeerComparisonSections(body, mainPart, peerPrograms, programDetails, userRole);
                }

                var accessiblePillars = pillars.Where(x =>
                    (x.IsAccess && userRole == UserRole.ProgramUser) || userRole != UserRole.ProgramUser).ToList();

                foreach (var pillar in accessiblePillars)
                {
                    AppendProgramHeader(mainPart, programDetails, pillar.PillarName);
                    AddPillarSection(body, mainPart, pillar, userRole);
                }
            }

            // ── 6. KPI Dashboard ────────────────────────────────────────────────────
            if (kpiChartItems.Any())
            {
                AppendProgramHeader(mainPart, programDetails, "KPI Dashboard");
                AddKpiDashboardSection(body, mainPart, kpiChartItems, isAllPrograms);
            }


            // ── 7. Findings and Recommendations (LAST, after KPI) ───────────────────

            if (!isAllPrograms && !string.IsNullOrEmpty(programDetails.Recommendations))
            {
                AppendProgramHeader(mainPart, programDetails, "Recommendations");
                AppendContentSection(body, "Recommendations", programDetails.Recommendations, "b2dfdb");
            }

        }

        // ════════════════════════════════════════════════════════════════════
        //  DASHBOARD SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddDashboardSection(
            Body body, MainDocumentPart mainPart,
            AiProgramSummeryDto program,
            List<PillarChartItem> pillars,
            List<KpiChartItem> kpis)
        {
            float overall = (float)program.AIProgress.GetValueOrDefault();
            var validPillars = pillars.ToList();
            // ── Call site ────────────────────────────────────────────────────────────────
            var donutPng = RenderPng((c, s) => PaintDonut(c, s, overall), 320, 220);
            var radarPng = RenderPng((c, s) => PaintSpiderChart(c, s, validPillars), 460, 280);

            var best = validPillars.OrderByDescending(x => x.Value).First();
            var worst = validPillars.OrderBy(x => x.Value).First();
            body.AppendChild(
                CreateScoreAndRadarRow(
                    mainPart,
                    donutPng, radarPng,
                    program,
                    pillars.Count, kpis.Count,
                    best, worst,
                    validPillars));

            body.AppendChild(Gap(20));

            // Row 2: KPI stats band
            int green = kpis.Count(x => x.Value > 40);
            int amber = kpis.Count(x => x.Value >= 4 && x.Value < 40);
            int red = kpis.Count(x => x.Value == null || x.Value < 4);
            foreach (var el in CreateKpiStatSection(kpis.Count, green, amber, red))
                body.Append(el);


            body.AppendChild(Gap(20));

            if (kpis.Any())
            {
                var avg = kpis.Average(x => x.Value).ToString("0.0") + "%";

                body.Append(CreateKpiOverviewHeader(avg));

                var sparkPng = RenderPng((c, s) => PaintKpiSparkline(c, s, kpis), 700, 130);
                body.AppendChild(CreateFullWidthImage(mainPart, sparkPng, 130));
            }
        }
        private static Table CreateKpiOverviewHeader(string avgText)
        {
            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }
                ),
                new TableRow(
                    // LEFT: Title
                    new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = (ContentDxa * 3 / 4).ToString(), Type = TableWidthUnitValues.Dxa },
                            new TableCellBorders(
                                new TopBorder { Val = BorderValues.None },
                                new BottomBorder { Val = BorderValues.None },
                                new LeftBorder { Val = BorderValues.None },
                                new RightBorder { Val = BorderValues.None }
                            )
                        ),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                            new Run(
                                new RunProperties(new Bold(), new FontSize { Val = "18" }),
                                new Text("KPI Overview — All Indicators (sorted high to low)")
                            )
                        )
                    ),

                    // RIGHT: Avg
                    new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = (ContentDxa / 4).ToString(), Type = TableWidthUnitValues.Dxa },
                            new TableCellBorders(
                                new TopBorder { Val = BorderValues.None },
                                new BottomBorder { Val = BorderValues.None },
                                new LeftBorder { Val = BorderValues.None },
                                new RightBorder { Val = BorderValues.None }
                            )
                        ),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                            new Run(
                                new RunProperties(new Bold(), new FontSize { Val = "18" }),
                                new Text($"Avg: {avgText}")
                            )
                        )
                    )
                )
            );
        }
        // ── Master row builder ────────────────────────────────────────────────────────

        private Table CreateScoreAndRadarRow(
            MainDocumentPart mainPart,
            byte[] donutPng,
            byte[] radarPng,
            AiProgramSummeryDto Program,
            int pillarCount,
            int kpiCount,
            PillarChartItem? best,
            PillarChartItem? worst,
            List<PillarChartItem> pillars)
        {
            float overallScore = (float)Program.AIProgress.GetValueOrDefault();

            var leftCell = BuildDonutCell(mainPart, donutPng, Program, pillarCount, kpiCount, best, worst);
            var rightCell = BuildRadarCell(mainPart, radarPng, pillars);

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None })),
                new TableRow(leftCell, rightCell));
        }

        // ── LEFT: Donut card (40 % width) ────────────────────────────────────────────
        private TableCell BuildDonutCell(
            MainDocumentPart mainPart,
            byte[] donutPng,
            AiProgramSummeryDto program,
            int pillarCount,
            int kpiCount,
            PillarChartItem? best,
            PillarChartItem? worst)
        {
            int leftDxa = (int)(ContentDxa * 0.30);   // 40 % of page content width
            long imgEmuW = (long)leftDxa * 914400L / 1440L;
            long imgEmuH = imgEmuW * 220 / 320;        // keep aspect of 320×220 render

            var cell = new TableCell();

            // ── Cell properties ──
            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = leftDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                CellNoBorder(),
                new TableCellMargin(
                    new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }),
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "FFFFFF" }));

            // ── Heading ──
            cell.Append(CenteredBoldPara("Overall Program Score", "212529", "20"));
            // ── Ranking Labels ──
            var globalRankLabel = program.Rank.HasValue && program.TotalProgram.HasValue && program.TotalProgram >=1
                ? $"Program Rank: {program.Rank} / {program.TotalProgram}"
                : "Program Rank: N/A";


            // ── Donut image ──
            cell.Append(EmbedImage(mainPart, donutPng, imgEmuW, imgEmuH));


            // ── Pillars | KPIs row ──
            cell.Append(BuildPillarKpiTable(pillarCount, kpiCount, leftDxa));


            if (best != null)
            {
                cell.Append(
                    BuildDualBadgeRow(
                        $"▲ {Shorten(best.Name, 16)} ({best.Value:F0})",
                        "E8F5E9",
                        "003D44",

                        globalRankLabel,
                        "F5F8F7",
                        "003D44"
                    ));
            }

            // ─────────────────────────────────────────────
            // Worst Domain
            // ─────────────────────────────────────────────
            if (worst != null)
            {
                cell.Append(
                    BuildBadgeRow(
                        $"▼ {Shorten(worst.Name, 16)} ({worst.Value:F0})",
                        "FDECEA",
                        "B71C1C"
                    ));
            }
            return cell;
        }
        private Table BuildDualBadgeRow(
            string leftText,
            string leftBg,
            string leftColor,
            string rightText,
            string rightBg,
            string rightColor)
        {
            var table = new Table(
                new TableProperties(
                    new TableWidth
                    {
                        Width = "5000", // 100%
                        Type = TableWidthUnitValues.Pct
                    },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None }
                    )
                )
            );

            var row = new TableRow();

            // Left badge
            row.Append(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth
                        {
                            Width = "3250", // 65%
                            Type = TableWidthUnitValues.Pct
                        },
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = leftBg
                        },
                        CellNoBorder()
                    ),
                    new Paragraph(
                        new ParagraphProperties(
                            new SpacingBetweenLines
                            {
                                Before = "40",
                                After = "40"
                            }),
                        new Run(
                            new RunProperties(
                                new Color { Val = leftColor },
                                new FontSize { Val = "14" }
                            ),
                            new Text(leftText)
                        )
                    )
                )
            );

            // Right badge
            row.Append(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth
                        {
                            Width = "1750", // 35%
                            Type = TableWidthUnitValues.Pct
                        },
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = rightBg
                        },
                        CellNoBorder()
                    ),
                    new Paragraph(
                        new ParagraphProperties(
                            new Justification
                            {
                                Val = JustificationValues.Center
                            },
                            new SpacingBetweenLines
                            {
                                Before = "40",
                                After = "40"
                            }),
                        new Run(
                            new RunProperties(
                                new Bold(),
                                new Color { Val = rightColor },
                                new FontSize { Val = "14" }
                            ),
                            new Text(rightText)
                        )
                    )
                )
            );

            table.Append(row);

            return table;
        }

        private Table BuildBadgeRow(string text, string bg, string color)
        {
            var table = new Table(
                new TableProperties(
                    new TableWidth
                    {
                        Width = "5000",
                        Type = TableWidthUnitValues.Pct
                    },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None }
                    )
                )
            );

            table.Append(new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth
                        {
                            Width = "5000",
                            Type = TableWidthUnitValues.Pct
                        },
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = bg
                        },
                        CellNoBorder()
                    ),
                    new Paragraph(
                        new ParagraphProperties(
                            new SpacingBetweenLines
                            {
                                Before = "40",
                                After = "40"
                            }),
                        new Run(
                            new RunProperties(
                                new Color { Val = color },
                                new FontSize { Val = "14" }
                            ),
                            new Text(text)
                        )
                    )
                )
            ));

            return table;
        }

        // ── RIGHT: Radar card (60 % width) ───────────────────────────────────────────
        private TableCell BuildRadarCell(
            MainDocumentPart mainPart,
            byte[] radarPng,
            List<PillarChartItem> pillars)
        {
            int rightDxa = (int)(ContentDxa * 0.60);   // 60 % of page content width
            long imgEmuW = (long)rightDxa * 914400L / 1440L;
            long imgEmuH = imgEmuW * 280 / 460;        // keep aspect of 460×280 render

            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = rightDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                CellNoBorder(),
                new TableCellMargin(
                    new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }),
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "FFFFFF" }));

            // ── Heading ──
            cell.Append(CenteredBoldPara("Pillar Performance Radar", "003D44", "20"));

            // ── Radar image ──
            cell.Append(EmbedImage(mainPart, radarPng, imgEmuW, imgEmuH));

            return cell;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// Pillars | KPIs two-column mini-table
        private Table BuildPillarKpiTable(int pillarCount, int kpiCount, int parentDxa)
        {
            int half = parentDxa / 2;

            TableCell CountCell(string number, string label) =>
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = half.ToString(), Type = TableWidthUnitValues.Dxa },
                        CellNoBorder(),
                        new TableCellMargin(new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                    new Paragraph(new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(
                                new Bold(), new Color { Val = "4CAF50" }, new FontSize { Val = "36" }),
                            new Text(number))),
                    new Paragraph(new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(
                                new Color { Val = "4A5F62" }, new FontSize { Val = "16" }),
                            new Text(label))));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = parentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.Single, Color = "E4E4E4", Size = 4 })),
                new TableRow(
                    CountCell(pillarCount.ToString(), "Pillars"),
                    CountCell(kpiCount.ToString(), "KPIs")));
        }

        /// Centered bold paragraph (headings)
        private static Paragraph CenteredBoldPara(string text, string hexColor, string halfPtSize) =>
            new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { After = "40" }),
                new Run(new RunProperties(
                        new Bold(),
                        new Color { Val = hexColor },
                        new FontSize { Val = halfPtSize }),
                    new Text(text)));



        /// TableCellBorders — all None
        private static TableCellBorders CellNoBorder()
        {
            var n = new EnumValue<BorderValues>(BorderValues.None);
            return new TableCellBorders(
                new TopBorder { Val = n },
                new BottomBorder { Val = n },
                new LeftBorder { Val = n },
                new RightBorder { Val = n });
        }

        // ════════════════════════════════════════════════════════════════════
        //  Program SUMMARY SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddProgramSummarySection(Body body, MainDocumentPart mainPart, AiProgramSummeryDto data, UserRole userRole, bool isAllPrograms = false)
        {
            // =========================
            // PROGRESS SECTION
            // =========================
            body.AppendChild(SectionHeading("Total Score", DarkBlue));
            body.AppendChild(CreateProgressBar("Score", (float)(data.AIProgress ?? 0), DarkBlue));
            // Rankings Section
            body.AppendChild(CreateRankingHeader("Rankings"));

            body.AppendChild(CreateRankRow("Program Rank",
                data.Rank, data.TotalProgram, "16A34A"));

            body.AppendChild(Gap(160));

            // =========================
            // EXECUTIVE SUMMARY
            // =========================
            AppendContentSection(body, "Executive Summary", data.EvidenceSummary, "163329");
           
            if (!isAllPrograms)
            {
                // =====================================================
                // EVIDENCE SECTION
                // =====================================================
                AppendContentSection(body, "Structural Evidence", data.StructuralEvidence, "e6ccff");
                AppendContentSection(body, "Operational Evidence", data.OperationalEvidence, "c2f0f0");

                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Outcome Evidence", data.OutcomeEvidence, "ffe6cc");
                AppendContentSection(body, "Perception Evidence", data.PerceptionEvidence, "e6f7ff");

                // =====================================================
                // INTEGRITY CHECKS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Temporal Scope", data.TemporalScope, "d9e6ff");
                AppendContentSection(body, "Distortion Screening", data.DistortionScreening, "f2d9e6");
                AppendContentSection(body, "Relational Integrity", data.RelationalIntegrity, "f0ffe6");

                // =====================================================
                // STRESS TESTS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Geopolitical Shock", data.GeopoliticalShock, "ffd9cc");
                AppendContentSection(body, "Finance Shock", data.FinanceShock, "fff2cc");
                AppendContentSection(body, "Legitimacy Shock", data.LegitimacyShock, "e6f2ff");

                //body.AppendChild(PageBreak());

                //AppendContentSection(body, "Overall Stress Resilience", data.OverallStressResilience, "e6ffe6");
                AppendContentSection(body, "Stress Score Adjustment", data.StressScoreAdjustment, "ffe6f2");

                // =====================================================
                // GOVERNANCE ADJUSTMENTS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Inclusion & Equity Adjustment", data.InclusionEquityAdjustment, "f9e6ff");
                AppendContentSection(body, "Opacity Risk", data.OpacityRisk, "fff0e6");
                AppendContentSection(body, "Non Compensation Note", data.NonCompensationNote, "e6fff9");

                // =====================================================
                // SYSTEM ANALYSIS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Cross-Pillar System Dynamics", data.CrossPillarPatterns, "6e9688");
                AppendContentSection(body, "Institutional Capacity Assessment", data.InstitutionalCapacity, "0d8057");

                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Equity Assessment", data.EquityAssessment, "e8f5e9");
                AppendContentSection(body, "Governance Trajectory", data.GovernanceTrajectory, "fce4ec");

                // =====================================================
                // STRATEGIC OUTPUT
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Strategic Policy Priorities", data.StrategicRecommendation, "2e9975");
                AppendContentSection(body, "Why This Assessment Matters", data.AssessmentValueNote, "63a68f");
                AppendContentSection(body, "Key Findings", data.KeyFindings, "1f4e79");
            }
        }

        private static Paragraph CreateRankingHeader(string text)
        {
            return new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "120" }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = "4A5F62" }, new FontSize { Val = "22" }),
                    new Text(text)));
        }
        private static Table CreateRankRow(string label, int? rank, int? total, string color)
        {
            var noBorder = new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None });

            var leftCell = new TableCell(
                new TableCellProperties(noBorder.CloneNode(true)),
                new Paragraph(
                    new Run(
                        new RunProperties(new Color { Val = "4A5F62" }, new FontSize { Val = "20" }),
                        new Text(label))));

            var rightPara = new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Right }));

            if (rank.HasValue && total.HasValue)
            {
                rightPara.Append(
                    new Run(new RunProperties(new Bold(), new Color { Val = color }),
                        new Text((rank ?? 0).ToString())),
                    new Run(new RunProperties(new Color { Val = "4A5F62" }),
                        new Text($" / {total}"))
                );
            }
            else
            {
                rightPara.Append(new Run(new Text("-")));
            }

            var rightCell = new TableCell(
                new TableCellProperties(noBorder.CloneNode(true)),
                rightPara);

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(leftCell, rightCell));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PILLAR OVERVIEW SECTION  (radial + horizontal bars)
        // ════════════════════════════════════════════════════════════════════

        private void AddPillarOverviewSection(
            Body body, MainDocumentPart mainPart,
            List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).OrderByDescending(p => p.Value).ToList();
            if (!data.Any()) return;

            var radialPng = RenderPng((c, s) => PaintPillarRadialChart(c, s, data), 340, 340);
            var barPng    = RenderPng((c, s) => PaintPillarHorizontalBars(c, s, data), 400, 340);
            body.AppendChild(CreateSideBySideImages(mainPart, radialPng, barPng, 340));
            body.AppendChild(Gap(160));
            body.AppendChild(CreatePillarFooterTable(data));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PER-PILLAR SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddPillarSection(
            Body body, MainDocumentPart mainPart,
            AiProgramPillarResponse data, UserRole userRole)
        {
            // =========================
            // Score SECTION
            // =========================
            body.AppendChild(SectionHeading("Score Metrics", DarkBlue));
            body.AppendChild(CreateProgressBar("Score", (float)(data.AIProgress ?? 0), DarkBlue));
            body.AppendChild(Gap(160));

            // =========================
            // EVIDENCE SUMMARY
            // =========================
            AppendContentSection(body, "Executive Summary", data.EvidenceSummary, "163329");

            // =====================================================
            // EVIDENCE SECTION
            // =====================================================
            AppendContentSection(body, "Structural Evidence", data.StructuralEvidence, "1f4e79");
            AppendContentSection(body, "Operational Evidence", data.OperationalEvidence, "2e75b6");

            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Outcome Evidence", data.OutcomeEvidence, "5b9bd5");
            AppendContentSection(body, "Perception Evidence", data.PerceptionEvidence, "9dc3e6");

            // =====================================================
            // INTEGRITY CHECKS
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Temporal Scope", data.TemporalScope, "5f497a");
            AppendContentSection(body, "Distortion Screening", data.DistortionScreening, "8064a2");
            AppendContentSection(body, "Relational Integrity", data.RelationalIntegrity, "b1a0c7");

            // =====================================================
            // STRESS TEST
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Geopolitical Shock", data.StressGeopoliticalShock, "7f6000");
            AppendContentSection(body, "Finance Shock", data.StressFinanceShock, "bf9000");
            AppendContentSection(body, "Legitimacy Shock", data.StressLegitimacyShock, "ffd966");

            //body.AppendChild(PageBreak());

            //AppendContentSection(body, "Stress Overall Resilience", data.StressOverallResilience, "c55a11");
            AppendContentSection(body, "Stress Score Adjustment", data.StressScoreAdjustment, "e26b0a");

            // =====================================================
            // GOVERNANCE ADJUSTMENTS
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Inclusion & Equity Adjustment", data.InclusionEquityAdjustment, "274e13");
            AppendContentSection(body, "Opacity Risk", data.OpacityRisk, "38761d");
            AppendContentSection(body, "Non-Compensation Note", data.NonCompensationNote, "6aa84f");

            // =====================================================
            // ALERTS & EQUITY
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Red Flags", data.RedFlag, "ED561A", "eb4634");
            AppendContentSection(body, "Inclusion & Access Note", data.InclusionAccessNote, "0d8057");

            // =====================================================
            // INSTITUTIONAL ANALYSIS
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Institutional Assessment", data.InstitutionalAssessment, "2e9975");

            AppendContentSection(
                body,
                "Analytical Foundations and Data Integration",
                data.DataGapAnalysis,
                "a4bab2"
            );

            // =====================================================
            // DATA SOURCES
            // =====================================================
            if (data.DataSourceCitations?.Any() == true)
            {
                body.AppendChild(PageBreak());
                AppendDataSourcesSection(body, data.DataSourceCitations.ToList());
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATA SOURCES SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AppendDataSourcesSection(Body body, List<AIDataSourceCitation> sources)
        {
            body.AppendChild(SectionHeading("Data Source Citations", "396154"));
            foreach (var src in sources.Take(10))
            {
                body.AppendChild(BoldParagraph(src.SourceName ?? "", "2C423B", 22));
                body.AppendChild(NormalParagraph(
                    $"Trust Level: {src.TrustLevel}/7  |  Year: {src.DataYear}  |  Type: {src.SourceType ?? "—"}",
                    "4A5F62", 18));
                if (!string.IsNullOrEmpty(src.DataExtract))
                    body.AppendChild(NormalParagraph(TruncateText(src.DataExtract, 200), "616161", 18, italic: true));
                if (!string.IsNullOrEmpty(src.SourceURL))
                    body.AppendChild(NormalParagraph(src.SourceURL, "305246", 16));
                body.AppendChild(Gap(120));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  KPI DASHBOARD SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddKpiDashboardSection(
            Body body, MainDocumentPart mainPart,
            List<KpiChartItem> kpis, bool isAllPrograms = false)
        {
            if (!kpis.Any()) return;

            int total = kpis.Count;
            int green = kpis.Count(x => x.Value > 40);
            int amber = kpis.Count(x => x.Value >= 4 && x.Value < 40);
            int red   = kpis.Count(x => x.Value == null || x.Value < 4);
            float avg = (float)kpis.Average(x => x.Value);

            body.AppendChild(CreateKpiSummaryBandTable(total, green, amber, red, avg));
            body.AppendChild(Gap(100));

            // Groups of 18 KPIs — bar chart + interpretation cards
            var groups = kpis
                .Select((k, i) => new { k, i })
                .GroupBy(x => x.i / 13)
                .Select(g => g.Select(x => x.k).ToList())
                .ToList();

            int offset = 0;
            foreach (var group in groups.Where(g => g.Any()))
            {
                int localOffset = offset;
                var barPng = RenderPng(
                    (c, s) => PaintKpiBarChart(c, s, group, localOffset),
                    700, 155);

                body.AppendChild(CreateFullWidthImage(mainPart, barPng, 155));
                body.AppendChild(Gap(80));
                if (!isAllPrograms)
                {
                    body.AppendChild(CreateKpiCardTable(mainPart, group));
                    body.AppendChild(Gap(160));
                }
                offset += group.Count;
            }
        }

       

        /// <summary>
        /// Registers a repeating page header (appears on every page) that mirrors
        /// the QuestPDF CityComposeHeader layout:
        ///
        ///  ┌─────────────────────────────────────────┬────────────┐
        ///  │  [Title — bold white 21pt]              │ [  LOGO  ] │
        ///  │  City, State, Program | Data Year: YYYY │ [  white ] │
        ///  │  Generated: Mon DD, YYYY               │ [   box  ] │
        ///  └─────────────────────────────────────────┴────────────┘
        ///  ─────────── divider (#E4E4E4) ───────────────────────────
        /// </summary>


        // ── Field: holds the pending header relId until the section is closed ──────
        private string? _pendingHeaderRelId = null;

        /// <summary>
        /// Attaches the last pending header and A4 page chrome to the document's final sectPr.
        /// Must run once after all programs/sections have been written.
        /// </summary>
        private void FinalizeLastSection(MainDocumentPart mainPart)
        {
            var body = mainPart.Document.Body!;

            foreach (var extra in body.Elements<SectionProperties>().ToList())
                extra.Remove();

            body.AppendChild(BuildSectionProperties(_pendingHeaderRelId, nextPage: false));
            _pendingHeaderRelId = null;
        }

        /// <summary>
        /// Creates a header for the upcoming section and closes the previous section
        /// with a next-page section break so that header always matches the following content.
        /// </summary>
        private void AppendProgramHeader(MainDocumentPart mainPart, AiProgramSummeryDto data, string? sectionTitle = null)
        {
            var body = mainPart.Document.Body!;

            if (_pendingHeaderRelId != null)
                body.AppendChild(CreateSectionBreakParagraph(_pendingHeaderRelId));

            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();

            string title = string.IsNullOrEmpty(sectionTitle) ? data.ProgramName : sectionTitle;
            string headerFill = DarkBlue.TrimStart('#');

            string logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/vcp.png");

            int logoColW = 2600;
            int leftColW = ContentDxa - logoColW;

            const long logoWidthEmu = 885_600L;
            const long logoHeightEmu = 856_800L;

            // MAIN TABLE
            var layoutTable = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableLayout { Type = TableLayoutValues.Fixed },
                    new TableCellSpacing() { Width = "0", Type = TableWidthUnitValues.Dxa }
                )
            );

            var mainRow = new TableRow(
                new TableRowProperties(
                    new TableRowHeight
                    {
                        Val = 900, // prevent compression
                        HeightType = HeightRuleValues.AtLeast
                    }
                )
            );

            // LEFT CELL
            var leftCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = leftColW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Fill = headerFill },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "250", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "150", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            leftCell.Append(
                HeaderParagraph(title, "42", "FFFFFF", true, "40"),
                HeaderParagraph($"{data.ProgramName}, {data.Location} | Conference Year: {data.Year}", "20", "B8E8EC", false, "20"),
                HeaderParagraph($"Generated: {DateTime.Now:MMM dd, yyyy}", "16", ReportThemeColors.LightBgHex, false, "0")
            );

            mainRow.Append(leftCell);

            // RIGHT CELL — logo on same dark-blue background as the header
            var rightCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = logoColW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Fill = headerFill },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            if (File.Exists(logoPath))
            {
                var logoBytes = PrepareHeaderLogoPng(File.ReadAllBytes(logoPath), headerFill);
                var logoPara = EmbedImageInPart(
                    headerPart,
                    logoBytes,
                    logoWidthEmu,
                    logoHeightEmu
                );

                logoPara.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines
                    {
                        Before = "0",
                        After = "0",
                        Line = "240",
                        LineRule = LineSpacingRuleValues.Auto
                    }
                );

                rightCell.Append(logoPara);
            }
            else
            {
                rightCell.Append(new Paragraph());
            }

            mainRow.Append(rightCell);

            layoutTable.Append(mainRow);

            // DIVIDER
            var divider = new Paragraph(
                new ParagraphProperties(
                    new ParagraphBorders(
                        new BottomBorder
                        {
                            Val = BorderValues.Single,
                            Size = 6,
                            Color = "E4E4E4"
                        }
                    )
                )
            );

            header.Append(layoutTable, divider);

            headerPart.Header = header;
            header.Save();

            _pendingHeaderRelId = mainPart.GetIdOfPart(headerPart);
        }

        /// <summary>
        /// Builds section properties. Intermediate sections use next-page breaks.
        /// The final section must NOT use next-page (it is the last child of Body).
        /// </summary>
        private static SectionProperties BuildSectionProperties(string? headerRelId, bool nextPage)
        {
            var sp = new SectionProperties();

            if (!string.IsNullOrEmpty(headerRelId))
                sp.AppendChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = headerRelId });

            if (nextPage)
                sp.AppendChild(new SectionType { Val = SectionMarkValues.NextPage });

            sp.AppendChild(new PageSize { Width = PageWidthDxa, Height = PageHeightDxa });
            sp.AppendChild(new PageMargin
            {
                Top = MarginDxa,
                Right = MarginDxa,
                Bottom = MarginDxa,
                Left = MarginDxa
            });

            return sp;
        }

        /// <summary>
        /// Last paragraph of a section. Includes a run so Word does not drop the section break.
        /// </summary>
        private static Paragraph CreateSectionBreakParagraph(string headerRelId)
        {
            var pPr = new ParagraphProperties();
            pPr.Append(BuildSectionProperties(headerRelId, nextPage: true));

            return new Paragraph(
                pPr,
                new Run(new Text("") { Space = SpaceProcessingModeValues.Preserve }));
        }
        // ── Helper: single-line paragraph for the header ─────────────────────────────
        private static Paragraph HeaderParagraph(
            string text,
            string fontSize,
            string color,
            bool bold,
            string spacingAfter)
        {
            var rp = new RunProperties(
                new Color { Val = color },
                new FontSize { Val = fontSize },
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });
            if (bold) rp.PrependChild(new Bold());

            return new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "0", After = spacingAfter }),
                new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }


        /// <summary>
        /// Removes the logo's dark/black background so the header fill shows through.
        /// </summary>
        private static byte[] PrepareHeaderLogoPng(byte[] pngBytes, string headerFillHex)
        {
            using var input = SKBitmap.Decode(pngBytes);
            if (input == null)
                return pngBytes;

            var headerColor = SKColor.Parse("#" + headerFillHex);
            var info = new SKImageInfo(input.Width, input.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var output = new SKBitmap(info);

            for (int y = 0; y < input.Height; y++)
            {
                for (int x = 0; x < input.Width; x++)
                {
                    var color = input.GetPixel(x, y);
                    if (color.Red <= 48 && color.Green <= 48 && color.Blue <= 48)
                        output.SetPixel(x, y, headerColor.WithAlpha(color.Alpha));
                    else
                        output.SetPixel(x, y, color);
                }
            }

            using var image = SKImage.FromBitmap(output);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }

        /// <summary>
        /// Mirrors EmbedImage() exactly, but targets a <see cref="HeaderPart"/> instead of
        /// <see cref="MainDocumentPart"/> so the relationship is resolved inside the header.
        /// </summary>
        private Paragraph EmbedImageInPart(HeaderPart headerPart, byte[] pngBytes,
            long widthEmu, long heightEmu)
        {
            var imgPart = headerPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(pngBytes))
                imgPart.FeedData(ms);

            string relId = headerPart.GetIdOfPart(imgPart);
            uint id = _imgId++;

            var blip = new A.Blip { Embed = relId };

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = id, Name = $"img{id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"img{id}.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    blip,
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                    { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                    {
                        DistanceFromTop = 0U,
                        DistanceFromBottom = 0U,
                        DistanceFromLeft = 0U,
                        DistanceFromRight = 0U
                    });

            return new Paragraph(
                 new ParagraphProperties(
                     new Justification { Val = JustificationValues.Center },
                     new SpacingBetweenLines { Before = "0", After = "0" }
                 ),new Run(drawing)
             );
        }
        /// <summary>Coloured section heading with accent left-border effect.</summary>


        /// <summary>Horizontal progress bar implemented as a two-cell table.</summary>
        private static Table CreateProgressBar(string label, float percentage, string hexColor)
        {
            percentage = Math.Clamp(percentage, 0f, 100f);
            int filled  = (int)(ContentDxa * percentage / 100f);
            int empty   = ContentDxa - filled;

            var border = new EnumValue<BorderValues>(BorderValues.None);
            var noBorders = new TableCellBorders(
                new TopBorder    { Val = border },
                new BottomBorder { Val = border },
                new LeftBorder   { Val = border },
                new RightBorder  { Val = border });

            // Label row
            var labelRow = new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                        noBorders.CloneNode(true)),
                    new Paragraph(
                        new Run(new RunProperties(
                            new Color { Val = "424242" }, new FontSize { Val = "22" }),
                            new Text(label)))));

            // Bar row
            TableCell filledCell = filled > 0
                ? new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = filled.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = hexColor },
                        noBorders.CloneNode(true)),
                    new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { After = "0" })))
                : new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = "1", Type = TableWidthUnitValues.Dxa },
                    noBorders.CloneNode(true)),
                    new Paragraph());

            TableCell emptyCell = empty > 0
                ? new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = empty.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F5F5F5" },
                        noBorders.CloneNode(true)),
                    new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { After = "0" })))
                : new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = "1", Type = TableWidthUnitValues.Dxa },
                    noBorders.CloneNode(true)),
                    new Paragraph());

            var barRow = new TableRow(filledCell, emptyCell);
            barRow.AppendChild(new TableRowProperties(new TableRowHeight { Val = 300, HeightType = HeightRuleValues.Exact }));

            // Score label row
            var scoreRow = new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                        noBorders.CloneNode(true)),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                        new Run(new RunProperties(
                            new Bold(), new Color { Val = hexColor }, new FontSize { Val = "22" }),
                            new Text($"{percentage:F1}")))));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder   { Val = BorderValues.None })),
                labelRow, barRow, scoreRow);
        }

        /// <summary>Two-column content block: accent bar on left, title + body text on right.</summary>
        /// 
        private static void AppendContentSection(Body body, string title, string? content, string accentHex, string bgColor = "444444")
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var paragraphs = content
                .Replace("||", "\n")
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // ─────────────────────────────────────────
            // TABLE (Single column now)
            // ─────────────────────────────────────────
            var table = new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Color = "E4E4E4", Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Color = "E4E4E4", Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Color = "E4E4E4", Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Color = "E4E4E4", Size = 6 }
                    )
                )
            );

            // ─────────────────────────────────────────
            // TITLE ROW (with accent line)
            // ─────────────────────────────────────────
            var titleCell = new TableCell(
                new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "EAEAEA" },
                    new TableCellMargin(
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new TopMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
                    ),
                    // Accent line ONLY here (left border trick)
                    new TableCellBorders(
                        new LeftBorder
                        {
                            Val = BorderValues.Single,
                            Size = 18, // thickness of accent line
                            Color = accentHex
                        }
                    )
                ),
                new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new Color { Val = "2E2E2E" },
                            new FontSize { Val = "28" }
                        ),
                        new Text(title)
                    )
                )
            );

            table.AppendChild(new TableRow(titleCell));

            // ─────────────────────────────────────────
            // CONTENT ROW
            // ─────────────────────────────────────────
            var contentCell = new TableCell(
                new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "F7F7F7" },
                    new TableCellMargin(
                        new TopMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            if (paragraphs.Length > 0)
            {
                foreach (var para in paragraphs)
                {
                    contentCell.AppendChild(new Paragraph(
                        new ParagraphProperties(
                            new Justification { Val = JustificationValues.Both },

                            // Force white background
                            new Shading
                            {
                                Val = ShadingPatternValues.Clear,
                                Fill = "FFFFFF"
                            },

                            new SpacingBetweenLines
                            {
                                Line = "276", // 1.15
                                LineRule = LineSpacingRuleValues.Auto,
                                After = "120"
                            }
                        ),
                        new Run(
                            new RunProperties(
                                new Color { Val = bgColor },
                                new FontSize { Val = "22" }
                            ),
                            new Text(para) { Space = SpaceProcessingModeValues.Preserve }
                        )
                    ));
                }
            }
            else
            {
                // Ensure cell always has at least one paragraph (required by OpenXML spec)
                contentCell.AppendChild(new Paragraph());
            }

            table.AppendChild(new TableRow(contentCell));

            body.AppendChild(table);

            body.AppendChild(Gap(140));
        }

        // ── KPI stat band (4 colored boxes) ─────────────────────────────────

        private static IEnumerable<OpenXmlElement> CreateKpiStatSection(int total, int green, int amber, int red)
        {
            // Heading (Top - Left aligned)
            var heading = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "20" } // space below heading
                ),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new FontSize { Val = "18" } // adjust if needed
                    ),
                    new Text("KPI Performance Distribution")
                )
            );

            // Existing table (UNCHANGED)
            var table = CreateKpiStatTable(total, green, amber, red);

            return new List<OpenXmlElement>
            {
                heading,
                table
            };
        }
        private static Table CreateKpiStatTable(int total, int green, int amber, int red)
        {
            int cellW = ContentDxa / 4;

            TableCell Stat(string val, string label, string bg, string fg)
            {
                var noBorder = new EnumValue<BorderValues>(BorderValues.None);

                return new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cellW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = bg },
                        new TableCellMargin(
                            new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                            new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                            new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }),
                        new TableCellBorders(
                            new TopBorder { Val = noBorder }, new BottomBorder { Val = noBorder },
                            new LeftBorder { Val = noBorder }, new RightBorder { Val = noBorder })
                    ),

                    // VALUE (Reduced size)
                    new Paragraph(
                        new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center },
                            new SpacingBetweenLines { Before = "0", After = "0", Line = "200", LineRule = LineSpacingRuleValues.Auto }
                        ),
                        new Run(
                            new RunProperties(
                                new Bold(),
                                new Color { Val = fg },
                                new FontSize { Val = "28" } // ↓ from 40
                            ),
                            new Text(val)
                        )
                    ),

                    // LABEL (Compact)
                    new Paragraph(
                        new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center },
                            new SpacingBetweenLines { Before = "0", After = "0", Line = "180", LineRule = LineSpacingRuleValues.Auto }
                        ),
                        new Run(
                            new RunProperties(
                                new Color { Val = fg },
                                new FontSize { Val = "18" } // ↑ slightly from 12 for readability
                            ),
                            new Text(label)
                        )
                    )
                );
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }
                ),
                new TableRow(
                    Stat(green.ToString(), "Performing ≥ 40%", "E8F5E9", "2E7D32"),
                    Stat(amber.ToString(), "Developing 0-39%", "FFF8E1", "E65100"),
                    Stat(red.ToString(), "Needs Improvement < 0%", "FDECEA", "C62828"),
                    Stat(total.ToString(), "Total KPIs", "EEF5F1", "003D44")
                )
            );
        }

        // ── KPI summary band (dark green strip) ──────────────────────────────

        private static Table CreateKpiSummaryBandTable(
            int total, int green, int amber, int red, float avg)
        {
            int cellW = ContentDxa / 5;
            string avgColor = avg >= 70 ? "4CAF50" : avg >= 40 ? "FFC107" : "EF5350";

            TableCell Pill(string val, string label, string fg)
            {
                var noBorder = new EnumValue<BorderValues>(BorderValues.None);
                return new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cellW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "003D44" },
                        new TableCellBorders(
                            new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                            new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder })),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Bold(), new Color { Val = fg }, new FontSize { Val = "30" }),
                            new Text(val))),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Color { Val = "FFFFFFBB" }, new FontSize { Val = "13" }),
                            new Text(label))));
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(
                    Pill(total.ToString(),  "Total KPIs",        "4CAF50"),
                    Pill(green.ToString(),  "Performing ≥40%",   "4CAF50"),
                    Pill(amber.ToString(),  "Developing 0–39%", "FFC107"),
                    Pill(red.ToString(), "Needs Improvement < 0%", "EF5350"),
                    Pill($"{avg:F1}%",      "Average Score",     avgColor)));
        }

        // ── KPI card grid (2 per row, with interpretation table) ─────────────

        private Table CreateKpiCardTable(MainDocumentPart mainPart, List<KpiChartItem> kpis)
        {
            int gap = 120;
            int cardW = (ContentDxa - gap) / 2;

            var table = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var pairs = kpis
                .Select((k, i) => (k, i))
                .GroupBy(t => t.i / 2)
                .Select(g => g.ToList())
                .ToList();

            int globalIdx = 0;
            foreach (var pair in pairs)
            {
                var row = new TableRow();

                for (int pIdx = 0; pIdx < pair.Count; pIdx++)
                {
                    var (kpi, localI) = pair[pIdx];
                    int cardNum = globalIdx + localI + 1;

                    decimal v = kpi.Value;
                    v = v == 100 ? Math.Round(v, 0) : Math.Round(v, 1);
                    string accent = GetBarColor((float)v).TrimStart('#');

                    var interps = kpi.InterPretation ?? new List<FiveLevelInterpretationsDto>();
                    var matched = interps.FirstOrDefault(x =>
                        x.MinRange.HasValue && x.MaxRange.HasValue &&
                        v >= x.MinRange.Value && v <= x.MaxRange.Value);

                    if (matched == null && interps.Any())
                        matched = interps
                            .Where(x => x.MinRange.HasValue && x.MaxRange.HasValue)
                            .OrderBy(x => Math.Min(
                                Math.Abs(v - x.MinRange!.Value),
                                Math.Abs(v - x.MaxRange!.Value)))
                            .FirstOrDefault();

                    var cardTable = BuildKpiCardTable(kpi, cardNum, v, accent, interps, matched, cardW);

                    // Wrap card table in a cell
                    var cell = new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                            CellNoBorders()),
                        cardTable);
                    row.AppendChild(cell);

                    // Gap cell between the two cards
                    if (pair.Count > 1 && pIdx == 0)
                        row.AppendChild(new TableCell(
                            new TableCellProperties(
                                new TableCellWidth { Width = gap.ToString(), Type = TableWidthUnitValues.Dxa },
                                CellNoBorders()),
                            new Paragraph()));
                }

                // Pad to keep layout when row has only 1 card
                if (pair.Count == 1)
                    row.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                            CellNoBorders()),
                        new Paragraph()));

                table.AppendChild(row);
                table.AppendChild(new TableRow(SpacerCell(ContentDxa, 80)));
                globalIdx += pair.Count;
            }

            return table;
        }

        /// <summary>
        /// Builds a single KPI card as a nested table, matching the QuestPDF card layout:
        ///   ┌─────────────────────────────────────────┐  ← accent border
        ///   │  [#N]  ShortName            score%       │  ← accent bg header
        ///   │         Name (subtitle)                  │
        ///   ├──────────┬──────────────────────────────┤  ← #F0F0F0 sub-header
        ///   │  Range   │ Condition                     │
        ///   ├──────────┼──────────────────────────────┤
        ///   │  0–20    │ Very Low                      │  ← stripe rows, matched = accent
        ///   │  …       │ …                             │
        ///   └──────────┴──────────────────────────────┘
        /// </summary>
        private Table BuildKpiCardTable(
            KpiChartItem kpi,
            int cardNum,
            decimal v,
            string accent,
            List<FiveLevelInterpretationsDto> interps,
            FiveLevelInterpretationsDto? matched,
            int cardW)
        {
            int rangeColW = 920;
            int condColW = cardW - rangeColW;

            var card = new Table(new TableProperties(
                new TableWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            // ── ROW 1: Header band ───────────────────────────────────────────────────
            int bubbleW = 260;
            int scoreW = 720;
            int nameW = cardW - bubbleW - scoreW;

            var headerInner = new Table(new TableProperties(
                new TableWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var hRow = new TableRow();

            // Number bubble
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = bubbleW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "13" }),
                        new Text(cardNum.ToString())))));

            // ShortName + Name
            var nameCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = nameW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "40", Type = TableWidthUnitValues.Dxa })));

            nameCell.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                    new Text(kpi.ShortName ?? "") { Space = SpaceProcessingModeValues.Preserve })));

            nameCell.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                new Run(
                    new RunProperties(new Color { Val = "DDDDDD" }, new FontSize { Val = "11" }),
                    new Text(kpi.Name ?? "") { Space = SpaceProcessingModeValues.Preserve })));

            hRow.AppendChild(nameCell);

            // Score
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = scoreW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "20" }),
                        new Text($"{v}%") { Space = SpaceProcessingModeValues.Preserve }))));

            headerInner.AppendChild(hRow);

            var headerRow = new TableRow();
            headerRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new GridSpan { Val = 2 },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent }),
                headerInner));
            card.AppendChild(headerRow);

            // ── ROW 2: Definition strip (only when Definition has content) ───────────
            if (!string.IsNullOrWhiteSpace(kpi.Definition))
            {
                // "DEF" label + definition text share one paragraph with a run each
                var defPara = new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { Before = "0", After = "0" }));

                // "DEF " label — small, bold, accent colour
                defPara.AppendChild(new Run(
                    new RunProperties(
                        new Bold(),
                        new Color { Val = accent },
                        new FontSize { Val = "9" },          // 4.5 pt
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
                    new Text("DEF :  ") { Space = SpaceProcessingModeValues.Preserve }));

                // Definition body — italic, dark grey, wraps naturally
                defPara.AppendChild(new Run(
                    new RunProperties(
                        new Italic(),
                        new Color { Val = "444444" },
                        new FontSize { Val = "11" },          // 5.5 pt
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
                    new Text(kpi.Definition) { Space = SpaceProcessingModeValues.Preserve }));

                var defCell = new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new GridSpan { Val = 2 },               // spans Range + Condition columns
                        new TableCellBorders(
                            new TopBorder
                            {
                                Val = BorderValues.Single,
                                Size = 2,
                                Color = accent,
                                Space = 0
                            },
                            new BottomBorder
                            {
                                Val = BorderValues.Single,
                                Size = 2,
                                Color = "DDDDDD",
                                Space = 0
                            },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None }),
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F6F4" },
                        new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                        new TableCellMargin(
                            new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                            new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                    defPara);

                var defRow = new TableRow();
                defRow.AppendChild(defCell);
                card.AppendChild(defRow);
            }

            // ── ROW 3: Sub-header (Range | Condition) ────────────────────────────────
            var subHdrRow = new TableRow();
            subHdrRow.AppendChild(MakeInterpCell("Range", rangeColW, "F0F0F0", "666666", "11", bold: true, bottomDivider: false));
            subHdrRow.AppendChild(MakeInterpCell("Condition", condColW, "F0F0F0", "666666", "11", bold: true, bottomDivider: false));
            card.AppendChild(subHdrRow);

            // ── ROWS 4+: Interpretation rows ─────────────────────────────────────────
            for (int i = 0; i < interps.Count; i++)
            {
                var interp = interps[i];
                bool isHit = interp == matched;
                bool isLast = i == interps.Count - 1;

                string rowBg = isHit ? accent : (i % 2 == 0 ? "FFFFFF" : "F7F7F7");
                string rangeFg = isHit ? White : "888888";
                string condFg = isHit ? White : "333333";

                string rangeStr = interp.MinRange.HasValue && interp.MaxRange.HasValue
                    ? $"{Math.Round(interp.MinRange.Value, 0)}–{Math.Round(interp.MaxRange.Value, 0)}"
                    : "—";

                var interpRow = new TableRow();
                interpRow.AppendChild(MakeInterpCell(rangeStr, rangeColW, rowBg, rangeFg, "12", bold: false, bottomDivider: !isLast));
                interpRow.AppendChild(MakeInterpCell(interp.Condition ?? "—", condColW, rowBg, condFg, "13", bold: isHit, bottomDivider: !isLast));
                card.AppendChild(interpRow);
            }

            return card;
        }

        /// <summary>Creates a single interpretation table cell.</summary>
        private static TableCell MakeInterpCell(
            string text,
            int width,
            string bgColor,
            string fgColor,
            string fontSize,
            bool bold,
            bool bottomDivider)
        {
            var borders = new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new BottomBorder
                {
                    Val = bottomDivider ? BorderValues.Single : BorderValues.None,
                    Size = 2,
                    Color = "E4E4E4"
                });

            var rp = new RunProperties(
                new Color { Val = fgColor },
                new FontSize { Val = fontSize });
            if (bold) rp.AppendChild(new Bold());

            return new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                    borders,
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bgColor },
                    new TableCellMargin(
                        new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        }

        /// <summary>Returns a TableCellBorders with all sides set to None.</summary>
        private static TableCellBorders CellNoBorders() =>
            new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None });

        // ── Pillar footer band (avg, best, worst) ────────────────────────────

        private static Table CreatePillarFooterTable(List<PillarChartItem> data)
        {
            float avg   = (float)data.Average(x => x.Value ?? 0);
            var   best  = data.OrderByDescending(x => x.Value).First();
            var   worst = data.OrderBy(x => x.Value).First();
            int   w3    = ContentDxa / 3;
            var noBorder = new EnumValue<BorderValues>(BorderValues.None);

            TableCell Cell(string[] lines, string[] fgs, string bg)
            {
                var noBord = new TableCellBorders(
                    new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                    new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder });
                var tc = new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = w3.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bg },
                    noBord));
                for (int i = 0; i < lines.Length; i++)
                    tc.AppendChild(new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(
                            new Color { Val = fgs[i] },
                            new FontSize { Val = i == 0 ? "40" : "18" }),
                            new Text(lines[i]))));
                return tc;
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(
                    Cell(new[] { $"{avg:F1}", "Average Score" },
                         new[] { GetBarColor(avg).TrimStart('#'), "A8E063" }, "003D44"),
                    Cell(new[] { $"▲ {Shorten(best.Name ?? "—", 22)}", $"{best.Value:F1}%" },
                         new[] { "003D44", "2E7D32" }, "E8F5E9"),
                    Cell(new[] { $"▼ {Shorten(worst.Name ?? "—", 22)}", $"{worst.Value:F1}%" },
                         new[] { "B71C1C", "C62828" }, "FDECEA")));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  PEER COMPARISON SECTIONS  –  mirrors PDF layout exactly
        // ════════════════════════════════════════════════════════════════════════

        private void AddPeerComparisonSections(
             Body body, MainDocumentPart mainPart,
             List<PeerProgramHistoryReportDto> activePrograms,
             AiProgramSummeryDto ProgramDetails, UserRole userRole)
        {
            if (!activePrograms.Any()) return;

            var main = FindMainProgram(activePrograms, ProgramDetails);
            var peers = activePrograms.Where(p => !IsSameProgram(p.ProgramName, ProgramDetails.ProgramName)).ToList();
            var all = BuildAllPrograms(main, peers);
            AppendProgramHeader(mainPart, ProgramDetails, "Relative Ranking Among Peer Programs");
            AddRankingSection(body, mainPart, all, ProgramDetails);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  RANKING SECTION  –  hero banner + histogram + full table
        // ════════════════════════════════════════════════════════════════════════

        private void AddRankingSection(
            Body body, MainDocumentPart mainPart,
            List<PeerProgramHistoryReportDto> all,
            AiProgramSummeryDto programDetails)
        {
            var ranked = all
                .Select(c => (Program: c, Score: GetLatestScoreOrZero(c)))
                .OrderByDescending(x => x.Score)
                .ToList();

            int mainRank = ranked.FindIndex(r => IsSameProgram(r.Program.ProgramName, programDetails.ProgramName)) + 1;
            float mainScore = mainRank > 0 ? ranked[mainRank - 1].Score : 0f;
            float pctile = mainRank > 0 ? (1f - (float)mainRank / ranked.Count) * 100f : 0f;

            // Hero banner
            body.AppendChild(CreateHeroBanner(programDetails, mainRank, ranked.Count, mainScore, pctile));
            body.AppendChild(Gap(120));



            // Full ranking table
            body.AppendChild(SectionHeading("Full Program Ranking", DarkBlue));
            var rows = ranked.Select((r, i) => new[]
            {
                 (i + 1).ToString(),
                 r.Program.ProgramName ?? "—",
                 r.Program.Year?.ToString() ?? "—",
                 r.Program.Description ?? "—",
                 r.Program.Location ?? "—",
                 $"{r.Score:F1}"
            }).ToArray();

            body.AppendChild(CreateStyledTable(
                new[] { "#", "Program", "Conference Year", "Description", "Location", "Score" },
                new[] { 450, 1800, 1200, 2800, 1800, 900 },
                rows,
                highlightRow: i => IsSameProgram(ranked[i].Program.ProgramName, programDetails.ProgramName)));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  NEW ELEMENT BUILDERS
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Green insight band matching the PDF DrawInsightBand strip.</summary>
        private static Paragraph CreateInsightBand(string text) =>
            new(new ParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "E8F5E9" },
                    new SpacingBetweenLines { Before = "60", After = "80" }),
                new Run(
                    new RunProperties(
                        new Color { Val = "003D44" },
                        new FontSize { Val = "17" },
                        new RunFonts { Ascii = "Arial" }),
                    new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        /// <summary>Bold section heading (matches PDF FontSize 11 Bold).</summary>
        private static Paragraph SectionHeading(string text, string hexColor) =>
            new(new ParagraphProperties(new SpacingBetweenLines { Before = "80", After = "60" }),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new Color { Val = hexColor.TrimStart('#') },
                        new FontSize { Val = "22" },
                        new RunFonts { Ascii = "Arial" }),
                    new Text(text)));

        /// <summary>
        /// Dark-green hero banner: rank left, score right — mirrors the PDF RelativeRankingPage banner.
        /// </summary>
        private static Table CreateHeroBanner(
            AiProgramSummeryDto ProgramDetails,
            int rank, int total, float score, float pctile)
        {
            var noBorder = new EnumValue<BorderValues>(BorderValues.None);
            TableCellBorders NoBorders() => new(
                new TopBorder { Val = noBorder }, new BottomBorder { Val = noBorder },
                new LeftBorder { Val = noBorder }, new RightBorder { Val = noBorder },
                new InsideHorizontalBorder { Val = noBorder }, new InsideVerticalBorder { Val = noBorder });

            var table = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }));

            var row = new TableRow();

            // Left cell – rank + city line
            int leftW = ContentDxa - 1900;
            row.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = leftW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "003D44" },
                    NoBorders()),
                new Paragraph(
                    new ParagraphProperties(new SpacingBetweenLines { Before = "60", After = "20" }),
                    new Run(
                        new RunProperties(
                            new Bold(), new Color { Val = "F0B429" },
                            new FontSize { Val = "64" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"#{rank} of {total}"))),
                new Paragraph(
                    new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "80" }),
                    new Run(
                        new RunProperties(
                            new Color { Val = "A5D6C2" },
                            new FontSize { Val = "22" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"{ProgramDetails.ProgramName}  ·  {ProgramDetails.Location}")))));

            // Right cell – score + percentile
            row.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = "1900", Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "003D44" },
                    NoBorders()),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "60", After = "20" }),
                    new Run(
                        new RunProperties(
                            new Color { Val = "A5A8AD" },
                            new FontSize { Val = "18" }, new RunFonts { Ascii = "Arial" }),
                        new Text("Score"))),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "0", After = "20" }),
                    new Run(
                        new RunProperties(
                            new Bold(), new Color { Val = "FFFFFF" },
                            new FontSize { Val = "56" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"{score:F1}"))),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "0", After = "80" }),
                    new Run(
                        new RunProperties(
                            new Color { Val = "4CAF8A" },
                            new FontSize { Val = "18" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"Top {100 - pctile:F0}% of peers")))));

            table.AppendChild(row);
            return table;
        }

        // ── General styled data table ─────────────────────────────────────────

        private static Table CreateStyledTable(
            string[] headers, int[] colWidthsDxa, string[][] rows,
            Func<int, bool>? highlightRow = null)
        {
            var borderSingle = new EnumValue<BorderValues>(BorderValues.Single);

            TableCellBorders DataBorders() => new TableCellBorders(
                new BottomBorder
                {
                    Val = borderSingle,
                    Size = 4,
                    Color = "E4E4E4"
                });

            var totalWidth = colWidthsDxa.Sum();

            var table = new Table(
                new TableProperties(
                    new TableWidth
                    {
                        Width = totalWidth.ToString(),
                        Type = TableWidthUnitValues.Dxa
                    },
                    new TableLayout
                    {
                        Type = TableLayoutValues.Fixed
                    }
                )
            );

            // Force the exact column widths
            var grid = new TableGrid();

            foreach (var width in colWidthsDxa)
            {
                grid.AppendChild(
                    new GridColumn
                    {
                        Width = width.ToString()
                    });
            }

            table.AppendChild(grid);


            // Header row
            var hRow = new TableRow();
            for (int c = 0; c < headers.Length; c++)
            {
                hRow.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = colWidthsDxa[c].ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "003D44" }),
                    new Paragraph(
                        new Run(new RunProperties(
                            new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                            new Text(headers[c])))));
            }
            table.AppendChild(hRow);

            // Data rows
            for (int r = 0; r < rows.Length; r++)
            {
                bool highlight = highlightRow?.Invoke(r) ?? false;
                string rowBg = highlight ? "FFF9E6" : (r % 2 == 0 ? "FFFFFF" : "FAFAFA");
                var dRow = new TableRow();
                for (int c = 0; c < rows[r].Length && c < headers.Length; c++)
                {
                    dRow.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = colWidthsDxa[c].ToString(), Type = TableWidthUnitValues.Dxa },
                            new Shading { Val = ShadingPatternValues.Clear, Fill = rowBg },
                            DataBorders()),
                        new Paragraph(
                            new Run(new RunProperties(
                                new Color { Val = highlight ? "003D44" : "333333" },
                                new FontSize { Val = "16" }),
                                new Text(rows[r][c])))));
                }
                table.AppendChild(dRow);
            }
            return table;
        }

        // ════════════════════════════════════════════════════════════════════
        //  IMAGE EMBEDDING HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Embeds a PNG byte-array as a full-width image in the document.
        /// <paramref name="naturalHeightPx"/> is used to compute the aspect ratio.
        /// </summary>
        private Paragraph CreateFullWidthImage(
            MainDocumentPart mainPart, byte[] pngBytes, int naturalHeightPx,
            int naturalWidthPx = 700)
        {
            long widthEmu  = ContentWidthEmu;
            long heightEmu = ContentWidthEmu * naturalHeightPx / naturalWidthPx;
            return EmbedImage(mainPart, pngBytes, widthEmu, heightEmu);
        }

        /// <summary>Creates a two-cell table, each half containing one image.</summary>
        private Table CreateSideBySideImages(
            MainDocumentPart mainPart,
            byte[] leftPng, byte[] rightPng,
            int naturalHeightPx)
        {
            long hw     = HalfWidthEmu;
            long hh     = hw * naturalHeightPx / 320; // approx aspect

            var noBorder = new EnumValue<BorderValues>(BorderValues.None);
            TableCell ImgCell(byte[] png) => new(
                new TableCellProperties(
                    new TableCellWidth { Width = (ContentDxa / 2).ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableCellBorders(
                        new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                        new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder })),
                EmbedImage(mainPart, png, hw, hh));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(ImgCell(leftPng), ImgCell(rightPng)));
        }

        private Paragraph EmbedImage(
            MainDocumentPart mainPart, byte[] pngBytes, long widthEmu, long heightEmu)
        {
            var imgPart = mainPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(pngBytes))
                imgPart.FeedData(ms);

            string relId = mainPart.GetIdOfPart(imgPart);
            uint id = _imgId++;

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = id, Name = $"img{id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"img{id}.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                        { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

            return new Paragraph(new Run(drawing));
        }

        // ════════════════════════════════════════════════════════════════════
        //  SKIA CHART RENDERERS  →  PNG bytes
        //  All Paint* methods below replicate the logic from PdfGeneratorService
        //  but operate on a SkiaSharp surface instead of a QuestPDF canvas.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Renders any SkiaSharp paint action to a PNG byte array.</summary>
        private static byte[] RenderPng(
            Action<SKCanvas, QPDF.Size> paintAction,
            int width, int height)
        {
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            paintAction(canvas, new QPDF.Size(width, height));
            canvas.Flush();
            using var snap    = surface.Snapshot();
            using var encoded = snap.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
        private static void PaintDonut(SKCanvas c, QPDF.Size s, float score) =>
            PdfGeneratorService.PaintDonutPublic(c, s, score);

        private static void PaintSpiderChart(SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.PaintSpiderChartPublic(c, s, pillars);

        private static void PaintKpiSparkline(SKCanvas c, QPDF.Size s, List<KpiChartItem> kpis) =>
            PdfGeneratorService.PaintKpiSparklinePublic(c, s, kpis);

        private static void PaintKpiBarChart(
            SKCanvas c, QPDF.Size s, List<KpiChartItem> kpis, int offset) =>
            PdfGeneratorService.DrawKpiBarChartCanvas(c, s, kpis, offset);

        private static void PaintPillarRadialChart(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.DrawPillarsRadialChartCanvas(c, s, pillars);

        private static void PaintPillarHorizontalBars(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.DrawPillarHorizontalBarsCanvas(c, s, pillars);

        private static Paragraph NormalParagraph(
            string text, string hexColor, int halfPtSize,
            bool italic = false, string bg = "FFFFFF")
        {
            var rPr = new RunProperties(
                new Color { Val = hexColor },
                new FontSize { Val = halfPtSize.ToString() },
                new RunFonts { Ascii = "Arial" });
            if (italic) rPr.AppendChild(new Italic());

            return new Paragraph(
                new ParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bg }),
                new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        private static Paragraph BoldParagraph(string text, string hexColor, int halfPtSize) =>
            new(new Run(
                new RunProperties(
                    new Bold(), new Color { Val = hexColor },
                    new FontSize { Val = halfPtSize.ToString() },
                    new RunFonts { Ascii = "Arial" }),
                new Text(text)));

        /// <summary>Empty paragraph with controlled spacing (spacing in twentieths of a point).</summary>
        private static Paragraph Gap(int spacingAfter) =>
            new(new ParagraphProperties(new SpacingBetweenLines { After = spacingAfter.ToString() }));

        private static Paragraph PageBreak() =>
            new(new Run(new Break { Type = BreakValues.Page }));


        private static TableCell SpacerCell(int widthDxa, uint heightTwips) =>
            new(new TableCellProperties(
                    new TableCellWidth { Width = widthDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { After = heightTwips.ToString() })));

        // ════════════════════════════════════════════════════════════════════
        //  COLOUR / FORMAT UTILITIES  (mirrors PdfGeneratorService statics)
        // ════════════════════════════════════════════════════════════════════

        static string GetBarColor(float value)
        {
            if (value >= 80) return "#C62828";
            else if (value >= 60) return "#E65100";
            else if (value >= 40) return "#FFC107";
            else if (value >= 20) return ReportThemeColors.AccentGreen;
            return ReportThemeColors.Primary;
        }

        private static string Shorten(string text, int max) =>
            string.IsNullOrWhiteSpace(text) ? "" :
            text.Length <= max ? text : text[..max] + "…";

        private static string TruncateText(string text, int maxLength) =>
            string.IsNullOrEmpty(text) || text.Length <= maxLength
                ? text : text[..maxLength] + "...";

        private static float GetLatestScoreOrZero(PeerProgramHistoryReportDto program) =>
            program.ProgramHistory?.OrderByDescending(h => h.Year).FirstOrDefault() is { } last
                ? (float)last.ScoreProgress : -1f;

        private static PeerProgramHistoryReportDto? FindMainProgram(
            List<PeerProgramHistoryReportDto> all, AiProgramSummeryDto program) =>
            all.FirstOrDefault(p => IsSameProgram(p.ProgramName, program.ProgramName));

        private static bool IsSameProgram(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static List<PeerProgramHistoryReportDto> BuildAllPrograms(
            PeerProgramHistoryReportDto? main, List<PeerProgramHistoryReportDto> peers)
        {
            var list = new List<PeerProgramHistoryReportDto>();
            if (main != null) list.Add(main);
            list.AddRange(peers);
            return list;
        }
    }
}
