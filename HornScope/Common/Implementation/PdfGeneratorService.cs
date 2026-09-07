using HornScope.Common.Interface;
using HornScope.Dtos.AiDto;
using HornScope.IServices;
using HornScope.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System.Text;
using static HornScope.Services.AIComputationService;

namespace HornScope.Common.Implementation
{
    public partial class PdfGeneratorService : IPdfGeneratorService
    {
        #region constructor

        private readonly IAppLogger _appLogger;
        public PdfGeneratorService(IAppLogger appLogger)
        {
            _appLogger = appLogger;
        }
        #endregion


        #region pdf pillars and program report

        public async Task<byte[]> GenerateAllProgramsDetailsPdf(List<AiProgramSummeryDto> programs, Dictionary<int, List<AiProgramPillarResponse>> pillarsDict, List<KpiChartItem> kpis, UserRole userRole)
        {
            try
            {
                QuestPDF.Settings.EnableDebugging = true;
                var document = Document.Create(container =>
                {
                    foreach(var programDetails in programs)
                    {
                        if(pillarsDict.TryGetValue(programDetails.ClimateProgramID, out var pillars) && pillars.Count > 0)
                        {
                            var kpiChartItems = kpis?
                            .Where(x => x.ClimateProgramID == programDetails.ClimateProgramID)
                            .ToList() ?? new List<KpiChartItem>();

                            AddProgramDetailsPdf(container, programDetails, pillars, kpiChartItems,new(), userRole, true);
                        }
                    }
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateAllProgramsDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GenerateProgramDetailsPdf(AiProgramSummeryDto programDetails, List<AiProgramPillarResponse> pillars, List<KpiChartItem> kpis, List<PeerProgramHistoryReportDto> peerPrograms, UserRole userRole)
        {
            try
            {
                QuestPDF.Settings.EnableDebugging = true;
                var document = Document.Create(container =>
                {
                    AddProgramDetailsPdf(container, programDetails, pillars, kpis, peerPrograms, userRole, false);
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateProgramDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GeneratePillarDetailsPdf(AiProgramPillarResponse pillarData, UserRole userRole)
        {
            try
            {
                QuestPDF.Settings.EnableDebugging = true;

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(25);
                        page.PageColor(ReportThemeColors.PageBg);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));
                        page.Header().Element(header => PillarComposeHeader(header, pillarData));
                        page.Content().Element(content =>
                            PillarComposeContent(content, pillarData, userRole));
                        page.Footer().Element(PillarComposeFooter);
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GeneratePillarDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        public void AddProgramDetailsPdf(IDocumentContainer container, AiProgramSummeryDto programDetails, List<AiProgramPillarResponse> pillars, List<KpiChartItem> kpis,
            List<PeerProgramHistoryReportDto> peerPrograms, UserRole userRole, bool isAllPrograms = false)
        {
            var kpiChartItems = kpis.OrderByDescending(x => x.Value).ToList();

            // Build pillar chart items (max 14)
            var pillarChartItems = pillars.Select(p => new PillarChartItem(SanitizeText(p.PillarName)?.Length > 20   ? SanitizeText(p.PillarName)[..20]
                  : SanitizeText(p.PillarName) ?? "-",  SanitizeText(p.PillarName) ?? "-", p.AIProgress)).ToList();

            // -- Section 1 : Global Dashboard ---------------------------------
            AddGlobalDashboardPage(container, programDetails, pillarChartItems, kpis, userRole);


            // -- Section 2 : Program Summary -------------------------------------
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    ProgramComposeHeader(x, programDetails, userRole, null));
                page.Content().Element(content =>
                {
                    content.Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Element(x =>
                            ProgramSummeryComposeContent(x, programDetails, userRole, isAllPrograms));
                    });
                });
                PageFooter(page);
            });


            // -- Section 3 : Pillar Radial Overview ---------------------------
            if (pillars.Any())
            {
                container.Page(page =>
                {
                    ApplyPageDefaults(page);
                    page.Header().Element(x =>
                        ProgramComposeHeader(x, programDetails, userRole, "Pillar Performance Overview"));
                    page.Content().Element(content =>
                        PillarLineChartPage(content, pillarChartItems));
                    PageFooter(page);
                });
            }

            // -- Section 1 : Global Dashboard ---------------------------------
            if (!isAllPrograms)
            {
                AddPeerProgramComparisonSection(container, peerPrograms, programDetails, userRole);
                //AddPerformanceTrendsSection(container, peerPrograms, programDetails, userRole);
            }

            if (!isAllPrograms)
            {
                // -- Section 4+ : Per-Pillar Detail ------------------------------
                var accessiblePillars = pillars.Where(x => x.IsAccess && UserRole.ProgramUser == userRole || UserRole.ProgramUser != userRole).ToList();
                foreach (var p in accessiblePillars)
                {
                    container.Page(page =>
                    {
                        ApplyPageDefaults(page);
                        page.Header().Element(x =>
                            ProgramComposeHeader(x, programDetails, userRole, SanitizeText(p.PillarName)));
                        page.Content().Element(content =>
                        {
                            content.Column(column =>
                            {
                                column.Spacing(10);
                                column.Item().Element(x =>
                                    PillarComposeContent(x, p, userRole));
                            });
                        });
                        PageFooter(page);
                    });
                }
            }

            // -- Section 5 : KPI Dashboard ------------------------------------
            if (kpiChartItems.Any())
            {
                container.Page(page =>
                {
                    ApplyPageDefaults(page);
                    page.Header().Element(x =>
                        ProgramComposeHeader(x, programDetails, userRole, "KPI Dashboard"));
                    page.Content().Element(content =>
                        KpiDashboardPage(content, kpiChartItems, isAllPrograms));
                    PageFooter(page);
                });
            }

            if (!isAllPrograms)
            {
                // -- Section 2 : Recommendations -------------------------------------
                container.Page(page =>
                {
                    ApplyPageDefaults(page);
                    page.Header().Element(x =>
                        ProgramComposeHeader(x, programDetails, userRole, null));
                    page.Content().Element(content =>
                    {
                        content.Column(column =>
                        {
                            column.Spacing(10);
                            column.Item().Element(x =>
                                AssessmentRecommendations(x, programDetails, userRole));
                        });
                    });
                    PageFooter(page);
                });
            }
        }

        // -----------------------------------------------------------------------------
        //  PAGE LAYOUT HELPERS  (reusable)
        // -----------------------------------------------------------------------------

        /// <summary>Applies standard A4 + font defaults to any page.</summary>
        static void ApplyPageDefaults(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(25);
            page.PageColor(ReportThemeColors.White);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
        }

        /// <summary>Standard numeric footer for Program pages.</summary>
        static void PageFooter(PageDescriptor page)
        {
            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
            });
        }        
        void AddGlobalDashboardPage(
            IDocumentContainer doc,
            AiProgramSummeryDto Program,
            List<PillarChartItem> pillars,   // already filtered to max 14
            List<KpiChartItem> kpis,      // already filtered to max 107
            UserRole userRole)
        {
            var vPillars = pillars.ToList();

            doc.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    ProgramComposeHeader(x, Program, userRole, "Program Performance Dashboard"));
                page.Content().Element(x =>
                    RenderDashboardContent(x, vPillars, kpis, Program));
                PageFooter(page);
            });
        }

        void RenderDashboardContent(
            IContainer container,
            List<PillarChartItem> pillars,
            List<KpiChartItem> kpis,
            AiProgramSummeryDto program)
        {
            //var vKpis = kpis.Where(k => k.Value.HasValue).ToList();

            float overall = (float)program.AIProgress.GetValueOrDefault();
            int kpiGreen = kpis.Count(k => k.Value > 40);
            int kpiAmber = kpis.Count(k => k.Value >= 4 && k.Value < 40);
            int kpiRed = kpis.Count(k => k.Value == null || k.Value < 4);
            var best = pillars.OrderByDescending(p => p.Value).FirstOrDefault();
            var worst = pillars.OrderBy(p => p.Value).FirstOrDefault();

            container.PaddingTop(6).Column(col =>
            {
                col.Spacing(10);

                // -- Row 1 : Score Donut (left)  +  Pillar Radar (right) ----------
                col.Item().Height(280).Row(row =>
                {
                    row.RelativeItem(5).Element(x =>
                        RenderScoreDonutCard(x, program, pillars.Count, kpis.Count, best, worst));

                    row.ConstantItem(10);

                    row.RelativeItem(5).Element(x =>
                        RenderPillarRadarCard(x, pillars));
                });

                // -- Row 2 : KPI distribution stat cards --------------------------
                //var topKpis = kpis
                //    .Where(x =>
                //        string.Equals(x.ShortName, "UDRI", StringComparison.OrdinalIgnoreCase) ||
                //        string.Equals(x.ShortName, "PRUPS", StringComparison.OrdinalIgnoreCase))
                //    .ToList();

                //if (topKpis.Any())
                //    col.Item().Height(130).Element(x =>
                //    DrawTopKpiBand(x, topKpis));

                col.Item().Height(100).Element(x =>
                    RenderKpiDistributionBand(x, kpis.Count, kpiGreen, kpiAmber, kpiRed));

                // -- Row 3 : KPI sorted sparkline ---------------------------------
                if (kpis.Any())
                    col.Item().Height(120).Element(x =>
                        RenderKpiSparklineCard(x, kpis));
            });
        }
        

        // -----------------------------------------------------------------------------
        //  DASHBOARD WIDGET . Score Donut Card
        // -----------------------------------------------------------------------------

        void RenderScoreDonutCard(
            IContainer container,
            AiProgramSummeryDto program,
            int pillarCount,
            int kpiCount,
            PillarChartItem? best,
            PillarChartItem? worst)
        {

            float score = (float)program.AIProgress.GetValueOrDefault();
            
            // Overall rank label
            var globalRankLabel = program.Rank.HasValue && program.TotalProgram.HasValue && program.TotalProgram > 1
                ? $"Program Rank: {program.Rank} / {program.TotalProgram}"
                : "Program Rank: N/A";

            container
                .Background(ReportThemeColors.White)
                .Border(1)
                .BorderColor(ReportThemeColors.BorderGreen)
                .Padding(8)
                .Column(col =>
                {
                    col.Spacing(0);

                    // ---------------------------------------------
                    // Title
                    // ---------------------------------------------
                    col.Item()
                        .AlignCenter()
                        .Text("Overall Program Score")
                        .FontSize(10)
                        .Bold()
                        .FontColor(ReportThemeColors.PdfDarkGreen);

                    // ---------------------------------------------
                    // Donut chart
                    // ---------------------------------------------
                    col.Item()
                        .Height(130)
                        .Canvas((canvas, size) =>
                            PaintDonut(canvas, size, score));

                    // Divider
                    col.Item()
                        .Height(1)
                        .Background(ReportThemeColors.SurfaceGreen);

                    // ---------------------------------------------
                    // Pillars + KPI Counts
                    // ---------------------------------------------
                    col.Item()
                        .PaddingTop(5)
                        .Row(row =>
                        {
                            // Pillars
                            row.RelativeItem()
                                .AlignCenter()
                                .Column(c =>
                                {
                                    c.Item()
                                        .AlignCenter()
                                        .Text(pillarCount.ToString())
                                        .FontSize(16)
                                        .Bold()
                                        .FontColor(ReportThemeColors.DarkBlue);

                                    c.Item()
                                        .AlignCenter()
                                        .Text("Pillars")
                                        .FontSize(8)
                                        .FontColor(ReportThemeColors.Gray650);
                                });

                            // Divider
                            row.ConstantItem(1)
                                .Background(ReportThemeColors.Gray350);

                            // KPIs
                            row.RelativeItem()
                                .AlignCenter()
                                .Column(c =>
                                {
                                    c.Item()
                                        .AlignCenter()
                                        .Text(kpiCount.ToString())
                                        .FontSize(16)
                                        .Bold()
                                        .FontColor(ReportThemeColors.DarkBlue);

                                    c.Item()
                                        .AlignCenter()
                                        .Text("KPIs")
                                        .FontSize(8)
                                        .FontColor(ReportThemeColors.Gray650);
                                });
                        });

                    // ---------------------------------------------
                    // Best Pillar + Program Rank
                    // ---------------------------------------------
                    if (best != null)
                    {
                        col.Item()
                            .PaddingTop(6)
                            .Row(row =>
                            {
                                // Best pillar
                                row.RelativeItem()
                                    .Background(ReportThemeColors.AccentEquityAssessment)
                                    .PaddingVertical(3)
                                    .PaddingHorizontal(5)
                                    .Text(
                                        $"▲ {Shorten(best.Name, 16)} ({best.Value:F0})")
                                    .FontSize(7)
                                    .FontColor(ReportThemeColors.SuccessGreenText);

                                row.ConstantItem(4);
                            });
                    }

                    // ---------------------------------------------
                    // Worst Pillar
                    // ---------------------------------------------
                    if (worst != null)
                    {
                        col.Item()
                            .PaddingTop(3)
                            .Background(ReportThemeColors.DangerRedBg)
                            .PaddingVertical(3)
                            .PaddingHorizontal(5)
                            .Text(
                                $"▼ {Shorten(worst.Name, 16)} ({worst.Value:F0})")
                            .FontSize(7)
                            .FontColor(ReportThemeColors.DangerRedDark);
                    }
                });
        }

        /// <summary>Renders the donut / gauge on an SKCanvas.</summary>
        static void PaintDonut(SKCanvas canvas, Size size, float score)
        {
            float cx = size.Width / 2f;
            float cy = size.Height / 2f;
            float outerR = Math.Min(cx, cy) - 8f;
            float thick = outerR * 0.30f;
            float mid = outerR - thick / 2f;

            var rect = new SKRect(cx - mid, cy - mid, cx + mid, cy + mid);

            // Background track
            using var bgPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thick,
                Color = SKColor.Parse(ReportThemeColors.SurfaceGreenAlt),
                IsAntialias = true
            };
            canvas.DrawOval(rect, bgPaint);

            // Score arc
            using var arcPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thick,
                Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawArc(rect, -90f, 360f * score / 100f, false, arcPaint);

            // Inner shadow ring
            using var shadowPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                Color = SKColor.Parse(ReportThemeColors.ChartBlueMint),
                IsAntialias = true
            };
            canvas.DrawOval(
                new SKRect(cx - mid + thick / 2f + 2, cy - mid + thick / 2f + 2,
                           cx + mid - thick / 2f - 2, cy + mid - thick / 2f - 2),
                shadowPaint);

            // Center: score value
            using var bigTxt = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                TextSize = 26,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };
            canvas.DrawText($"{score:F1}", cx, cy + 9, bigTxt);

            // Center: sub-label
            using var subTxt = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.Gray500),
                TextSize = 8,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText("Program score", cx, cy + 21, subTxt);
        }

        // -----------------------------------------------------------------------------
        //  DASHBOARD WIDGET . Pillar Radar / Spider Card
        // -----------------------------------------------------------------------------

        void RenderPillarRadarCard(IContainer container, List<PillarChartItem> pillars)
        {
            container
                .Background(ReportThemeColors.White)
                .Border(1).BorderColor(ReportThemeColors.BorderGreen)
                .Padding(4)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text("Pillar Performance Radar")
                        .FontSize(10).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                    col.Item().Height(230).Canvas((canvas, size) =>
                        PaintSpiderChart(canvas, size, pillars));
                });
        }

        /// <summary>Renders a filled spider/radar chart onto an SKCanvas.</summary>
        static void PaintSpiderChart(SKCanvas canvas, Size size, List<PillarChartItem> pillars)
        {
            int n = pillars.Count;
            if (n < 3) return;

            float cx = size.Width / 2f;
            float cy = size.Height / 2f;
            float radius = Math.Min(cx, cy) - 42f;

            // -- concentric grid rings --------------------------------------------
            using var ringPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse(ReportThemeColors.ChartBluePale),
                StrokeWidth = 0.7f,
                IsAntialias = true
            };
            using var ringLblPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.Gray450),
                TextSize = 7,
                IsAntialias = true,
                TextAlign = SKTextAlign.Left
            };

            for (int r = 1; r <= 4; r++)
            {
                float rr = radius * r / 4f;
                var pts = BuildRadarPoints(cx, cy, rr, n);
                var path = BuildPath(pts);
                canvas.DrawPath(path, ringPaint);
                canvas.DrawText($"{r * 25}", cx + rr + 2, cy - 2, ringLblPaint);
            }

            // -- spoke axes ------------------------------------------------------
            using var axisPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                StrokeWidth = 0.7f,
                IsAntialias = true
            };
            for (int i = 0; i < n; i++)
            {
                var tip = RadarPt(cx, cy, radius, i, n);
                canvas.DrawLine(cx, cy, tip.X, tip.Y, axisPaint);
            }

            // -- data polygon -----------------------------------------------------
            var dataPath = new SKPath();
            for (int i = 0; i < n; i++)
            {
                float v = (float)(pillars[i].Value ?? 0) / 100f;
                var pt = RadarPt(cx, cy, radius * v, i, n);
                if (i == 0) dataPath.MoveTo(pt);
                else dataPath.LineTo(pt);
            }
            dataPath.Close();

            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse(ReportThemeColors.DarkBlue).WithAlpha(55),
                IsAntialias = true
            };
            using var edgePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                IsAntialias = true
            };
            canvas.DrawPath(dataPath, fillPaint);
            canvas.DrawPath(dataPath, edgePaint);

            // -- data-point dots --------------------------------------------------
            using var dotPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                IsAntialias = true
            };
            using var dotBorder = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.2f,
                Color = new SKColor(),
                IsAntialias = true
            };
            for (int i = 0; i < n; i++)
            {
                float v = (float)(pillars[i].Value ?? 0) / 100f;
                var pt = RadarPt(cx, cy, radius * v, i, n);
                canvas.DrawCircle(pt.X, pt.Y, 4f, dotPaint);
                canvas.DrawCircle(pt.X, pt.Y, 4f, dotBorder);
            }

            // -- axis labels ------------------------------------------------------
            using var lblPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.ChartDarkBlue),
                TextSize = 8f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            using var valPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.ChartSteelBlue),
                TextSize = 7f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            for (int i = 0; i < n; i++)
            {
                var tip = RadarPt(cx, cy, radius + 26f, i, n);
                canvas.DrawText(
                    Shorten(pillars[i].ShortName ?? pillars[i].Name, 5),
                    tip.X, tip.Y + 3f, lblPaint);
            }
        }

        // -- Radar geometry helpers ---------------------------------------------------

        static SKPoint RadarPt(float cx, float cy, float r, int i, int n)
        {
            float angle = (-90f + 360f * i / n) * (float)Math.PI / 180f;
            return new SKPoint(cx + r * (float)Math.Cos(angle),
                               cy + r * (float)Math.Sin(angle));
        }

        static SKPoint[] BuildRadarPoints(float cx, float cy, float r, int n)
            => Enumerable.Range(0, n).Select(i => RadarPt(cx, cy, r, i, n)).ToArray();

        static SKPath BuildPath(SKPoint[] pts)
        {
            var p = new SKPath();
            if (pts.Length == 0) return p;
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.Close();
            return p;
        }

        // -----------------------------------------------------------------------------
        //  DASHBOARD WIDGET . KPI Distribution Band  (4 stat cards)
        // -----------------------------------------------------------------------------

        static void RenderKpiDistributionBand(
            IContainer container, int total, int green, int amber, int red)
        {
            container
                .Background(ReportThemeColors.White)
                .Border(1).BorderColor(ReportThemeColors.BorderGreen)
                .Padding(10)
                .Column(col =>
                {
                    col.Item()
                        .Text("KPI Performance Distribution")
                        .FontSize(9).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                    col.Item().PaddingTop(7).Row(row =>
                    {
                        DashboardStatCard(row.RelativeItem(),
                            green.ToString(), "Performing ≥ 40%", ReportThemeColors.AccentEquityAssessment, ReportThemeColors.SuccessGreen);
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            amber.ToString(), "Developing 0-39%", ReportThemeColors.WarningAmberBg, ReportThemeColors.WarningOrangeDark);
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            red.ToString(), "Needs Improvement < 0%", ReportThemeColors.DangerRedBg, ReportThemeColors.DangerRed);
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            total.ToString(), "Total KPIs", ReportThemeColors.SurfaceGreenAlt, ReportThemeColors.PdfDarkGreen);
                    });
                });
        }

       

        /// <summary>Single coloured stat card used inside the distribution band.</summary>
        static void DashboardStatCard(IContainer container, string value, string label, string bg, string textColor)
        {
            container
                .Background(bg)
                .Padding(8)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text(value).FontSize(20).Bold().FontColor(textColor);
                    col.Item().AlignCenter()
                        .Text(label).FontSize(7).FontColor(textColor);
                });
        }

        // -----------------------------------------------------------------------------
        //  DASHBOARD WIDGET . KPI Sparkline (gradient area chart)
        // -----------------------------------------------------------------------------

        void RenderKpiSparklineCard(IContainer container, List<KpiChartItem> kpis)
        {
            float avg = (float)kpis.Average(k => k.Value);

            container
                .Background(ReportThemeColors.White)
                .Border(1).BorderColor(ReportThemeColors.BorderGreen)
                .Padding(10)
                .Column(col =>
                {
                    col.Item().Row(hdr =>
                    {
                        hdr.RelativeItem()
                            .Text("KPI Overview - All Indicators (sorted high to low)")
                            .FontSize(9).Bold().FontColor(ReportThemeColors.DarkBlue);
                        hdr.AutoItem()
                            .Text($"Avg: {avg:F1}%")
                            .FontSize(9).Bold().FontColor(GetBarColor(avg));
                    });

                    col.Item().PaddingTop(6).Height(78).Canvas((canvas, size) =>
                        PaintKpiSparkline(canvas, size, kpis));
                });
        }

        /// <summary>
        /// Gradient area sparkline for up to 107 KPIs, sorted descending.
        /// Includes dashed 70 % threshold line.
        /// </summary>
        static void PaintKpiSparkline(SKCanvas canvas, Size size, List<KpiChartItem> kpis)
        {
            var data = kpis.OrderByDescending(k => k.Value).ToList();
            int n = data.Count;
            if (n < 2) return;

            const float lp = 28f, bp = 12f, tp = 4f;
            const float domainMax = 100f, domainMin = -100f;
            const float domainRange = domainMax - domainMin; // 200

            float w = size.Width - lp;
            float h = size.Height - bp - tp;
            float sx = w / (n - 1);

            // Single source of truth for value -> y. Used by grid, line, fill, threshold.
            float MapY(double value) => tp + h * (domainMax - (float)value) / domainRange;

            // Grid lines
            using var gp = new SKPaint { Color = SKColor.Parse(ReportThemeColors.SurfaceGreenLight), StrokeWidth = 0.7f };
            using var gl = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.Gray450),
                TextSize = 7,
                TextAlign = SKTextAlign.Right,
                IsAntialias = true
            };

            foreach (float m in new[] { 100f, 60f, 30f, 0f, -30f, -60f, -100f })
            {
                float y = MapY(m);
                canvas.DrawLine(lp, y, size.Width, y, gp);
                canvas.DrawText($"{(int)m}", lp - 3, y + 3, gl);
            }

            // Gradient fill under line (anchored at the zero line, not the bottom of the canvas,
            // so negative values fill upward from zero instead of down to -100)
            float yZero = MapY(0);

            var fPath = new SKPath();
            fPath.MoveTo(lp, yZero);
            fPath.LineTo(lp, MapY((float)data[0].Value));
            for (int i = 1; i < n; i++)
                fPath.LineTo(lp + i * sx, MapY((float)data[i].Value));
            fPath.LineTo(lp + (n - 1) * sx, yZero);
            fPath.Close();

            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, tp), new SKPoint(0, tp + h),
                new[] { SKColor.Parse(ReportThemeColors.DarkBlue).WithAlpha(95),
        SKColor.Parse(ReportThemeColors.DarkBlue).WithAlpha(8) },
                null, SKShaderTileMode.Clamp);
            using var fp = new SKPaint { Shader = shader, Style = SKPaintStyle.Fill };
            canvas.DrawPath(fPath, fp);

            // Line
            var lPath = new SKPath();
            for (int i = 0; i < n; i++)
            {
                float x = lp + i * sx;
                float y = MapY((float)data[i].Value);
                if (i == 0) lPath.MoveTo(x, y); else lPath.LineTo(x, y);
            }
            using var lPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.6f,
                Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                IsAntialias = true
            };
            canvas.DrawPath(lPath, lPaint);

            // Dashed 70% threshold
            float y70 = MapY(70f);
            using var thPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.SuccessGreen).WithAlpha(140),
                StrokeWidth = 0.9f,
                PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0)
            };
            canvas.DrawLine(lp, y70, size.Width, y70, thPaint);

            using var thLbl = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.SuccessGreen),
                TextSize = 7,
                IsAntialias = true
            };
            canvas.DrawText("70%", size.Width - 24, y70 - 2, thLbl);
        }

        // -------------------------------------------------------------------------------
        //  REDESIGNED KPI DASHBOARD + PILLAR OVERVIEW
        //  Drop-in replacements for KpiDashboardPage / DrawKpiLineChart /
        //  PillarLineChartPage / DrawPillarsRadialChart
        // -------------------------------------------------------------------------------

        // -----------------------------------------------------------------------------
        //  KPI DASHBOARD PAGE  .  numbered bar chart + full-name reference tables
        // -----------------------------------------------------------------------------

        void KpiDashboardPage(IContainer container, List<KpiChartItem> kpis, bool isAllPrograms = false)
        {
            
            if (!kpis.Any()) return;

            int total = kpis.Count;
            int kpiGreen = kpis.Count(k => k.Value > 40);
            int kpiAmber = kpis.Count(k => k.Value >= 4 && k.Value < 40);
            int kpiRed = kpis.Count(k => k.Value == null || k.Value < 4);
            float avg = (float)kpis.Average(x => x.Value);

            // 18 bars per chart row . compact but legible
            var groups = kpis
                .Select((k, i) => new { k, i })
                .GroupBy(x => x.i / 13)
                .Select(g => g.Select(x => x.k).ToList())
                .ToList();


            container.Padding(14).Column(col =>
            {
                col.Spacing(12);

                // -- top summary strip ---------------------------------------------
                col.Item().Height(70).Element(x =>
                    DrawKpiSummaryBand(x, total, kpiGreen, kpiAmber, kpiRed, avg));

                //if(topKpis.Any())
                //    col.Item().Height(130).Element(x =>
                //    DrawTopKpiBand(x,topKpis));

                // -- chart + reference-table sections -----------------------------
                int offset = 0;
                foreach (var group in groups.Where(g => g.Any()))
                {
                    int localOffset = offset;          // capture for lambda
                    col.Item().Element(x => DrawKpiGroupSection(x, group, localOffset , isAllPrograms));
                    offset += group.Count;
                }
            });
        }

        // -----------------------------------------------------------------------------
        //  SUMMARY BAND  .  five stat cards in a dark-green strip
        // -----------------------------------------------------------------------------

        static void DrawKpiSummaryBand(
            IContainer container,
            int total, int green, int amber, int red, float avg)
        {
            container
                .Background(ReportThemeColors.PdfDarkGreen)
                .Padding(10)
                .Row(row =>
                {
                    KpiStatPill(row.RelativeItem(), total.ToString(), "Total KPIs", ReportThemeColors.AccentGreen, ReportThemeColors.AccentGreenAlpha15);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), green.ToString(), "Performing ≥ 40 %", ReportThemeColors.AccentGreen, ReportThemeColors.AccentGreenAlpha15);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), amber.ToString(), "Developing 0-39 %", ReportThemeColors.WarningOrange, ReportThemeColors.WarningOrangeAlpha15);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), red.ToString(), "Needs Improvement < 0 %", ReportThemeColors.DangerRedLight, ReportThemeColors.DangerRedAlpha15);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), $"{avg:F1}%", "Average Score",
                        avg >= 70 ? ReportThemeColors.AccentGreen : avg >= 20 ? ReportThemeColors.WarningOrange : ReportThemeColors.DangerRedLight,
                        ReportThemeColors.AccentGreenAlpha15);
                });
        }


        // -----------------------------------------------------------------------------
        //  GROUP SECTION  .  bar chart on top, two-column legend table below
        // -----------------------------------------------------------------------------

        void DrawKpiGroupSection(IContainer container, List<KpiChartItem> group, int offset, bool isAllPrograms = false )
        {
            container
                .Border(1).BorderColor(ReportThemeColors.BorderGreenMid)
                .Column(col =>
                {
                    // bar chart . numbers printed below each bar
                    col.Item().Height(148).Element(x => DrawKpiBarChart(x, group, offset, isAllPrograms));

                    if (!isAllPrograms)
                    {
                        // hairline separator between chart and table
                        col.Item().Height(1).Background(ReportThemeColors.BorderGreenMid);

                        // two-column reference table
                        col.Item().Padding(6).Element(x => DrawKpiReferenceTable(x, group, offset));
                    }
                });
        }


        // -----------------------------------------------------------------------------
        //  BAR CHART  .  sequential index numbers below each bar (not cryptic codes)
        // -----------------------------------------------------------------------------
        void DrawKpiBarChart(IContainer container, List<KpiChartItem> data, int offset, bool isAllPrograms = false)
        {
            container
                .Background(ReportThemeColors.White)
                .Canvas((canvas, size) =>
                {
                    if (!data.Any()) return;

                    const float lp = 30f;   // left pad (increased for negative labels)
                    const float rp = 12f;   // right pad
                    const float tp = 22f;  // top pad  (value labels)
                    const float bp = 26f;  // bottom pad (index labels)

                    float chartW = size.Width - lp - rp;
                    float chartH = size.Height - tp - bp;
                    int n = data.Count;
                    float barW = chartW / n;
                    float innerW = barW * 0.62f;
                    float barGap = (barW - innerW) / 2f;

                    // Calculate zero baseline (middle of chart for -100 to 100 range)
                    float zeroY = tp + chartH / 2f;

                    // -- background grid lines -------------------------------------
                    using var gridPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.SurfaceGreenPale),
                        StrokeWidth = 0.6f,
                        IsAntialias = false
                    };
                    using var gridLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.BlueGrayLight),
                        TextSize = 7f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Right
                    };

                    // Draw grid lines for range -100 to 100
                    foreach (float pct in new[] { -100f, -80f, -60f, -40f, -20f, 0f, 20f, 40f, 60f, 80f, 100f })
                    {
                        // Map value from -100..100 to y coordinate
                        float gy = zeroY - (pct / 100f * (chartH / 2f));
                        canvas.DrawLine(lp, gy, lp + chartW, gy, gridPaint);
                        canvas.DrawText($"{(int)pct}", lp - 4, gy + 3, gridLblPaint);
                    }

                    // -- dashed 70 % performance threshold ------------------------
                    float y70 = zeroY - (70f / 100f * (chartH / 2f));
                    using var threshPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.SuccessGreen).WithAlpha(100),
                        StrokeWidth = 0.9f,
                        PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0),
                        IsAntialias = true
                    };
                    canvas.DrawLine(lp, y70, lp + chartW, y70, threshPaint);

                    // -- stronger zero baseline ------------------------------------
                    using var zeroLinePaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.BlueGrayDark),
                        StrokeWidth = 1.2f,
                        IsAntialias = true
                    };
                    canvas.DrawLine(lp, zeroY, lp + chartW, zeroY, zeroLinePaint);

                    // -- paint reused across bars ----------------------------------
                    using var valLblPaint = new SKPaint
                    { TextSize = 6.5f, IsAntialias = true, TextAlign = SKTextAlign.Center };
                    using var numLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.BlueGrayDark),
                        TextSize = 6.5f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };

                    // -- bars ------------------------------------------------------
                    for (int i = 0; i < n; i++)
                    {
                        float v = (float)(data[i].Value);
                        var shortName = data[i].ShortName;
                        float bx = lp + i * barW + barGap;

                        // Calculate bar height for -100 to 100 range
                        float bh = Math.Abs(v) / 100f * (chartH / 2f);
                        float by, barBottom;

                        if (v >= 0)
                        {
                            // Positive values: bar goes up from zero
                            by = zeroY - bh;
                            barBottom = zeroY;
                        }
                        else
                        {
                            // Negative values: bar goes down from zero
                            by = zeroY;
                            barBottom = zeroY + bh;
                        }

                        SKColor color = GetColor(v);
                        SKColor textcolor = SKColor.Parse(ReportThemeColors.Black);

                        // ghost (full-height tinted background) - only in bar direction
                        using var ghostPaint = new SKPaint
                        { Color = color.WithAlpha(35), IsAntialias = true };
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(bx, by, bx + innerW, barBottom), 2, 2),
                            ghostPaint);

                        // filled bar with linear gradient
                        using var shader = SKShader.CreateLinearGradient(
                            new SKPoint(0, by), new SKPoint(0, barBottom),
                            new[] { color, color.WithAlpha(180) },
                            null, SKShaderTileMode.Clamp);
                        using var barPaint = new SKPaint { Shader = shader, IsAntialias = true };
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(bx, by, bx + innerW, barBottom), 2, 2),
                            barPaint);

                        // top cap accent line
                        using var capPaint = new SKPaint
                        {
                            Color = color,
                            StrokeWidth = 2.5f,
                            StrokeCap = SKStrokeCap.Round,
                            IsAntialias = true
                        };
                        if (v >= 0)
                        {
                            canvas.DrawLine(bx + 1, by, bx + innerW - 1, by, capPaint);
                        }
                        else
                        {
                            canvas.DrawLine(bx + 1, barBottom, bx + innerW - 1, barBottom, capPaint);
                        }

                        // value label - position based on bar direction
                        float vly;
                        if (v >= 0)
                        {
                            vly = by - 3f;
                            if (vly < tp + 8f) vly = by + 10f;
                        }
                        else
                        {
                            vly = barBottom + 10f;
                            if (vly > size.Height - bp - 8f) vly = barBottom - 3f;
                        }

                        valLblPaint.Color = textcolor;
                        canvas.DrawText($"{v:F1}%", bx + innerW / 2f, vly, valLblPaint);

                        // -- sequential index number below bar (e.g. "1", "2", .) --
                        // Users cross-reference this with the legend table below.
                        canvas.DrawText(
                            $"{offset + i + 1}. " + shortName,
                            bx + innerW / 2f,
                            size.Height - 6f,
                            numLblPaint);
                    }
                });
        }

        // -----------------------------------------------------------------------------
        //  REFERENCE TABLE  .  two-column layout, colored status bar, full KPI names
        // -----------------------------------------------------------------------------
        void DrawKpiReferenceTable(IContainer container, List<KpiChartItem> group, int offset)
        {
            container.Row(row =>
            {
                row.RelativeItem().Element(x => DrawKpiInterpretationSection(x, group, offset));
            });
        }


        /// <summary>
        /// Renders all KPIs as paired cards . 2 per row.
        /// Each card: coloured header (code, name, score) + 5-row interpretation mini-table
        /// with the matching range row highlighted.
        /// </summary>
        void DrawKpiInterpretationSection(IContainer container, List<KpiChartItem> allItems,int offset)
        {
            // -- split into rows of 2 ---------------------------------------------
            var pairs = allItems
                .Select((item, idx) => (item, idx))
                .GroupBy(t => t.idx / 2)
                .Select(g => g.ToList())
                .ToList();

            container.Column(col =>
            {
                col.Spacing(4);

                foreach (var pair in pairs)
                {
                    col.Item().Row(row =>
                    {
                        row.Spacing(4);

                        foreach (var (kpi, idx) in pair)
                            row.RelativeItem().Column(card => DrawKpiCard(card, kpi, offset + idx + 1));

                        // pad last row if odd number of KPIs
                        if (pair.Count == 1)
                            row.RelativeItem().Element(_ => { });
                    });
                }
            });
        }
        void DrawKpiCard(ColumnDescriptor card, KpiChartItem kpi, int num)
        {
            var value = kpi.Value;
            var v = value == 100 ? Math.Round(value, 0) : Math.Round(value, 1);
            string accent = GetBarColor((float)v);

            var interps = kpi.InterPretation ?? new List<FiveLevelInterpretationsDto>();
            FiveLevelInterpretationsDto? matched = interps.FirstOrDefault(x =>
                x.MinRange.HasValue && x.MaxRange.HasValue &&
                value >= x.MinRange.Value && value <= x.MaxRange.Value);

            if (matched == null && interps.Any())
                matched = interps
                    .Where(x => x.MinRange.HasValue && x.MaxRange.HasValue)
                    .OrderBy(x => Math.Min(
                        Math.Abs(value - x.MinRange!.Value),
                        Math.Abs(value - x.MaxRange!.Value)))
                    .FirstOrDefault();

            card.Item()
                .Border(0.5f).BorderColor(accent)
                .Column(inner =>
                {
                    // -- 1. KPI header band ------------------------------------------
                    // Definition removed from here . gets its own strip below
                    inner.Item()
                         .Background(accent)
                         .PaddingHorizontal(5).PaddingVertical(3)
                         .Row(h =>
                         {
                             // Number bubble
                             h.ConstantItem(16)
                              .AlignMiddle()
                              .Background(ReportThemeColors.OverlayBlackAlpha)
                              .AlignCenter()
                              .Text($"{num}")
                              .FontSize(6f).Bold().FontColor(ReportThemeColors.White);

                             // Code + Name
                             h.RelativeItem()
                              .PaddingLeft(4)
                              .AlignMiddle()
                              .Column(nc =>
                              {
                                  nc.Item()
                                    .Text(kpi.ShortName ?? "")
                                    .FontSize(7.5f).Bold().FontColor(ReportThemeColors.White);
                                  nc.Item()
                                    .Text(kpi.Name ?? "")
                                    .FontSize(5f).FontColor(ReportThemeColors.WhiteAlpha73);
                              });

                             // Score
                             h.ConstantItem(34)
                              .AlignMiddle().AlignRight()
                              .Text($"{v}%")
                              .FontSize(9.5f).Bold().FontColor(ReportThemeColors.White);
                         });

                    // -- 2. Definition strip -----------------------------------------
                    // Shown only when definition exists; wraps gracefully for long text
                    if (!string.IsNullOrWhiteSpace(kpi.Definition))
                    {
                        inner.Item()
                             .Background(ReportThemeColors.SurfaceGreenMint)                      // very pale green-grey
                             .BorderTop(0.3f).BorderColor(accent)
                             .BorderBottom(0.3f).BorderColor(ReportThemeColors.Gray400)
                             .PaddingHorizontal(5).PaddingVertical(3)
                             .Row(dr =>
                             {
                                 // Small label pill
                                 dr.ConstantItem(28)
                                   .AlignTop()
                                   .PaddingTop(0.5f)
                                   .Text("DEF")
                                   .FontSize(4.5f).Bold()
                                   .FontColor(accent);

                                 // Definition text . italic, wraps, keeps card compact
                                 dr.RelativeItem()
                                   .Text(kpi.Definition)
                                   .FontSize(5.5f).Italic()
                                   .FontColor(ReportThemeColors.Gray850)
                                   .LineHeight(1.25f);
                             });
                    }

                    // -- 3. Interpretation column sub-header -------------------------
                    inner.Item()
                         .Background(ReportThemeColors.Gray200)
                         .PaddingHorizontal(4).PaddingVertical(2)
                         .Row(sh =>
                         {
                             sh.ConstantItem(46)
                               .Text("Range")
                               .FontSize(5.5f).Bold().FontColor(ReportThemeColors.Gray700);
                             sh.RelativeItem()
                               .Text("Condition")
                               .FontSize(5.5f).Bold().FontColor(ReportThemeColors.Gray700);
                         });

                    // -- 4. Five interpretation rows ---------------------------------
                    for (int i = 0; i < interps.Count; i++)
                    {
                        var interp = interps[i];
                        bool isHit = interp == matched;

                        string rowBg = isHit ? accent : (i % 2 == 0 ? ReportThemeColors.White : ReportThemeColors.SurfaceRowAlt);
                        string rangeFg = isHit ? ReportThemeColors.White : ReportThemeColors.Gray600;
                        string condFg = isHit ? ReportThemeColors.White : ReportThemeColors.Gray900;

                        string rangeStr = (interp.MinRange.HasValue && interp.MaxRange.HasValue)
                            ? $"({Math.Round(interp.MinRange.Value, 0)}) - ({Math.Round(interp.MaxRange.Value, 0)})"
                            : "-";

                        inner.Item()
                             .BorderBottom(0.3f).BorderColor(ReportThemeColors.Gray350)
                             .Background(rowBg)
                             .PaddingHorizontal(4).PaddingVertical(2)
                             .Row(r =>
                             {
                                 r.ConstantItem(46)
                                  .Text(rangeStr)
                                  .FontSize(6f).FontColor(rangeFg);

                                 r.RelativeItem()
                                  .Text(interp.Condition ?? ".")
                                  .FontSize(6.5f)
                                  .Bold()
                                  .FontColor(condFg);
                             });
                    }
                });
        }

        static void KpiStatPill(
            IContainer container, string value, string label, string valueColor, string bg)
        {
            container
                .Background(bg)
                .Padding(6)
                .Column(c =>
                {
                    c.Item().AlignCenter()
                        .Text(value).FontSize(15).Bold().FontColor(valueColor);
                    c.Item().AlignCenter()
                        .Text(label).FontSize(6.5f).FontColor(ReportThemeColors.WhiteAlpha73);
                });
        }

        // -----------------------------------------------------------------------------
        //  PILLAR OVERVIEW PAGE  .  redesigned horizontal bar layout + ring chart
        // -----------------------------------------------------------------------------

        void PillarLineChartPage(IContainer container, List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).ToList();
            if (!data.Any()) return;

            float avg = (float)data.Average(x => x.Value ?? 0);
            var best = data.OrderByDescending(x => x.Value).First();
            var worst = data.OrderBy(x => x.Value).First();

            container.Padding(16).Column(col =>
            {
                col.Spacing(10);

                // -- two-column layout: ring chart (left) + bar list (right) ------
                col.Item().Height(500).Row(row =>
                {
                    // Left: radial ring chart
                    row.RelativeItem(5).Element(x => DrawPillarsRadialChart(x, data));

                    row.ConstantItem(12);

                    // Right: horizontal bar list
                    row.RelativeItem(6).Element(x =>
                        DrawPillarHorizontalBars(x, data));
                });

                // -- bottom: avg score + best/worst -------------------------------
                col.Item().Element(x =>
                    DrawPillarFooterBand(x, avg, best, worst));
            });
        }

        // -- horizontal bar list for pillars -----------------------------------------

        static void DrawPillarHorizontalBars(IContainer container, List<PillarChartItem> data)
        {
            var sorted = data.OrderByDescending(x => x.Value).ToList();

            container
                .Background(ReportThemeColors.White)
                .Border(1).BorderColor(ReportThemeColors.BorderGreenLight)
                .Padding(14)
                .Column(col =>
                {
                    col.Item().PaddingBottom(8)
                        .Text("Pillar Overview").FontSize(11).Bold().FontColor(ReportThemeColors.DarkBlue);

                    col.Spacing(6);
                    int index = 1;
                    foreach (var item in sorted)
                    {
                        float v = (float)(item.Value ?? 0);
                        var color = GetPillarBarColors(v);

                        col.Item().Row(row =>
                        {
                            // Pillar label
                            row.ConstantItem(102).AlignMiddle()
                                .Text(Shorten(item.Name ?? item.ShortName ?? ".", 18))
                                .FontSize(8).FontColor(ReportThemeColors.BlueGray);

                            // Bar track
                            row.RelativeItem().AlignMiddle().Height(13)
                                .Background(ReportThemeColors.SurfaceGreenLight)
                                .Canvas((canvas, size) =>
                                {
                                    // filled portion with gradient
                                    float fillW = size.Width * v / 100f;
                                    SKColor barC = SKColor.Parse(color);

                                    using var shader = SKShader.CreateLinearGradient(
                                        new SKPoint(0, 0),
                                        new SKPoint(fillW, 0),
                                        new[] { barC.WithAlpha(210), barC },
                                        null,
                                        SKShaderTileMode.Clamp);
                                    using var fp = new SKPaint
                                    { Shader = shader, IsAntialias = true };
                                    canvas.DrawRoundRect(
                                        new SKRoundRect(
                                            new SKRect(0, 0, fillW, size.Height), 3, 3), fp);
                                });

                            // Score badge
                            row.ConstantItem(65).AlignMiddle().AlignRight()
                                .Text($"{v:F1}, Rank {index++}/{data.Count}")
                                .FontSize(8).Bold().FontColor(color);
                        });
                    }
                });
        }

        // -- footer band: avg + best + worst -----------------------------------------

        static void DrawPillarFooterBand(
            IContainer container, float avg, PillarChartItem best, PillarChartItem worst)
        {
            container.Row(row =>
            {
                // Average score
                row.RelativeItem(2)
                    .Background(ReportThemeColors.PdfDarkGreen)
                    .Padding(12)
                    .Column(c =>
                    {
                        c.Item().AlignCenter()
                            .Text("Average Score").FontSize(9).FontColor(ReportThemeColors.SuccessGreenMuted);
                        c.Item().AlignCenter()
                            .Text($"{avg:F1}")
                            .FontSize(22).Bold()
                            .FontColor(GetBarColor(avg) == ReportThemeColors.SuccessGreen ? ReportThemeColors.SuccessGreenLight
                                     : GetBarColor(avg) == ReportThemeColors.WarningAmber ? ReportThemeColors.WarningAmberLight : ReportThemeColors.DangerRedLight);
                    });

                row.ConstantItem(6);

                // Best pillar
                row.RelativeItem(3)
                    .Background(ReportThemeColors.AccentEquityAssessment)
                    .Border(1).BorderColor(ReportThemeColors.SuccessGreenBorder)
                    .Padding(10)
                    .Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem()
                                .Background(ReportThemeColors.SuccessGreen).Padding(3)
                                .Text("▲ BEST").FontSize(7).Bold().FontColor(ReportThemeColors.White);
                            r.ConstantItem(6);
                            r.RelativeItem()
                                .Text(Shorten(best.Name ?? ".", 26))
                                .FontSize(9).Bold().FontColor(ReportThemeColors.SuccessGreenText);
                        });
                        c.Item().PaddingTop(4)
                            .Text($"{best.Value:F1}").FontSize(16).Bold().FontColor(ReportThemeColors.SuccessGreen);
                    });

                row.ConstantItem(6);

                // Worst pillar
                row.RelativeItem(3)
                    .Background(ReportThemeColors.DangerRedBg)
                    .Border(1).BorderColor(ReportThemeColors.DangerRedBorder)
                    .Padding(10)
                    .Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem()
                                .Background(ReportThemeColors.DangerRed).Padding(3)
                                .Text("▼ LOWEST").FontSize(7).Bold().FontColor(ReportThemeColors.White);
                            r.ConstantItem(6);
                            r.RelativeItem()
                                .Text(Shorten(worst.Name ?? ".", 26))
                                .FontSize(9).Bold().FontColor(ReportThemeColors.DangerRedDark);
                        });
                        c.Item().PaddingTop(4)
                            .Text($"{worst.Value:F1}").FontSize(16).Bold().FontColor(ReportThemeColors.DangerRed);
                    });
            });
        }

        // -- radial ring chart (left panel) ------------------------------------------

        void DrawPillarsRadialChart(IContainer container, List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).OrderByDescending(p => p.Value).ToList();
            if (!data.Any()) return;

            float avg = (float)data.Average(x => x.Value ?? 0);

            container
                .Background(ReportThemeColors.White)
                .Border(1).BorderColor(ReportThemeColors.BorderGreenLight)
                .Canvas((canvas, size) =>
                {
                    float cx = size.Width / 2f;
                    float cy = size.Height / 2f;

                    // Use concentric rings: outermost = first pillar
                    int n = data.Count;
                    float maxRadius = Math.Min(cx, cy) - 18f;
                    float minRadius = maxRadius * 0.28f;
                    float ringStep = (maxRadius - minRadius) / n;
                    float ringThick = ringStep * 0.68f;

                    // Chart title
                    using var titlePaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                        TextSize = 10f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center,
                        FakeBoldText = true
                    };

                    canvas.DrawText("Pillar Performance", cx, 14f, titlePaint);

                    for (int i = 0; i < n; i++)
                    {
                        float v = (float)(data[i].Value ?? 0);
                        float r = maxRadius - i * ringStep;
                        float mid = r - ringThick / 2f;

                        var rect = new SKRect(cx - mid, cy - mid, cx + mid, cy + mid);

                        SKColor barCol = GetPillarColors(v);

                        // Track ring
                        using var trackPaint = new SKPaint
                        {
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = ringThick,
                            Color = barCol.WithAlpha(22),
                            IsAntialias = true
                        };
                        canvas.DrawOval(rect, trackPaint);

                        // Filled arc
                        using var arcPaint = new SKPaint
                        {
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = ringThick,
                            Color = barCol,
                            StrokeCap = SKStrokeCap.Round,
                            IsAntialias = true
                        };
                        float sweep = 360f * v / 100f;
                        canvas.DrawArc(rect, -90f, sweep, false, arcPaint);

                        // Label at end of arc
                        float labelAngle = (-90f + sweep) * (float)Math.PI / 180f;
                        float labelR = mid + ringThick / 2f + 6f;
                        float lx = cx + labelR * (float)Math.Cos(labelAngle);
                        float ly = cy + labelR * (float)Math.Sin(labelAngle);

                        // dot at arc end
                        using var dotPaint = new SKPaint
                        {
                            Color = barCol,
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawCircle(
                            cx + mid * (float)Math.Cos(labelAngle),
                            cy + mid * (float)Math.Sin(labelAngle),
                            ringThick / 2f + 1.5f, dotPaint);
                    }

                    // -- centre: average score ----------------------------------
                    using var circleFill = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.PdfDarkGreen),
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    float cr = minRadius - ringStep * 0.6f;
                    canvas.DrawCircle(cx, cy, cr, circleFill);

                    using var circleRing = new SKPaint
                    {
                        Color = GetPillarColors(avg).WithAlpha(180),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2f,
                        IsAntialias = true
                    };
                    canvas.DrawCircle(cx, cy, cr, circleRing);

                    using var avgNumPaint = new SKPaint
                    {
                        Color = GetPillarColors(avg),
                        TextSize = cr * 0.60f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center,
                        FakeBoldText = true
                    };
                    canvas.DrawText($"{avg:F1}", cx, cy + avgNumPaint.TextSize * 0.36f, avgNumPaint);

                    using var avgLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.White),
                        TextSize = cr * 0.26f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };
                    canvas.DrawText("avg", cx, cy + avgNumPaint.TextSize * 0.36f + avgLblPaint.TextSize + 1f, avgLblPaint);

                    // -- legend on the right side -------------------------------
                    float legendX = cx + Math.Min(cx, cy) + 2f;  // just outside chart . won't fit; draw below instead
                                                                 // (legend is in the horizontal bar panel on the right; no need to repeat here)
                });
        }

        // -----------------------------------------------------------------------------
        //  HEADERS / FOOTERS
        // -----------------------------------------------------------------------------
        void ProgramComposeHeader(
            IContainer container,
            AiProgramSummeryDto data,
            UserRole userRole,
            string? pillarName)
        {
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/vcp.png");

            container.Column(column =>
            {
                column.Item().Background(ReportThemeColors.DarkBlue).Padding(8).Row(row =>
                {
                    // Left content
                    row.RelativeItem().Column(col =>
                    {
                        col.Spacing(2);

                        string? title = string.IsNullOrEmpty(pillarName) ? data.ProgramName : pillarName!;

                        col.Item().Text(title)
                            .FontSize(21)
                            .Bold()
                            .FontColor(ReportThemeColors.White);

                        col.Item().Text($"{data.ProgramName} | Conference Year: {data.Year}")
                            .FontSize(10)
                            .FontColor(ReportThemeColors.HeaderTextPale);

                        col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                            .FontSize(8)
                            .FontColor(ReportThemeColors.HeaderTextMuted);
                    });

                    // Right logo
                    row.ConstantItem(60)
                        .AlignRight()
                        .AlignMiddle()
                        .Background(ReportThemeColors.DarkBlue)
                        .Padding(4)
                        .Image(logoPath)
                        .FitArea();
                });

                // Divider
                column.Item().LineHorizontal(1).LineColor(ReportThemeColors.BorderDivider);
            });
        }        

        void PillarComposeHeader(IContainer container, AiProgramPillarResponse data)
        {
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/vcp.png");

            container.Column(column =>
            {
                column.Item().Background(ReportThemeColors.DarkBlue).Padding(10).Row(row =>
                {
                    // Left content
                    row.RelativeItem().Column(col =>
                    {
                        col.Spacing(2);

                        col.Item().Text(data.PillarName)
                            .FontSize(21)
                            .Bold()
                            .FontColor(ReportThemeColors.White);

                        col.Item().Text($"{data.ProgramName} | Conference Year: {data.AIDataYear}")
                            .FontSize(10)
                            .FontColor(ReportThemeColors.HeaderTextPale);

                        col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                            .FontSize(8)
                            .FontColor(ReportThemeColors.HeaderTextMuted);
                    });

                    // Logo
                    row.ConstantItem(60)
                        .AlignRight()
                        .AlignMiddle()
                        .Background(ReportThemeColors.DarkBlue)
                        .Padding(1)
                        .Image(logoPath)
                        .FitArea();
                });

                column.Item().LineHorizontal(1).LineColor(ReportThemeColors.BorderDivider);
            });
        }

        static void PillarComposeFooter(IContainer container)
        {
            container.AlignCenter().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text(text =>
                    {
                        text.Span("Page "); text.CurrentPageNumber();
                        text.Span(" of "); text.TotalPages();
                    });
                    col.Item().PaddingTop(5).AlignCenter()
                        .Text("Program Assessment Platform").FontSize(8).FontColor(ReportThemeColors.Gray500);
                });
            });
        }
        string SanitizeText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace('\u2011', '-') // non-breaking hyphen
                .Replace('\u2010', '-') // hyphen
                .Replace('\u2012', '-') // figure dash
                .Replace('\u2013', '-') // en dash
                .Replace('\u2014', '-') // em dash
                .Replace('\u2212', '-') // minus sign
                .Replace('\u00AD', ' ') // soft hyphen (invisible troublemaker)
                .Normalize(NormalizationForm.FormKC);
        }

        void ProgramSummeryComposeContent(IContainer container, AiProgramSummeryDto data, UserRole userRole, bool isAllPrograms = false)
        {
            container.PaddingTop(4).Column(column =>
            {
                // =========================
                // PROGRESS SECTION
                // =========================

                column.Item().PaddingTop(10)
                    .Element(c => ProgramProgressSection(c, data, userRole));

                // =========================
                // EXECUTIVE SUMMARY
                // =========================
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Executive Summary", SanitizeText(data.EvidenceSummary), ReportThemeColors.AccentExecutiveSummary));
                if (!isAllPrograms)
                {
                    // =====================================================
                    // EVIDENCE SECTION
                    // =====================================================              


                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Structural Evidence", SanitizeText(data.StructuralEvidence), ReportThemeColors.AccentStructuralEvidence));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Operational Evidence", SanitizeText(data.OperationalEvidence), ReportThemeColors.AccentOperationalEvidence));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Outcome Evidence", SanitizeText(data.OutcomeEvidence), ReportThemeColors.AccentOutcomeEvidence));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Perception Evidence", SanitizeText(data.PerceptionEvidence), ReportThemeColors.AccentPerceptionEvidence));

                    // =====================================================
                    // INTEGRITY CHECKS
                    // =====================================================
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(15).Text("Integrity Checks")
                    //    .FontSize(16).Bold();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Temporal Scope", SanitizeText(data.TemporalScope), ReportThemeColors.AccentTemporalScope));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Distortion Screening", SanitizeText(data.DistortionScreening), ReportThemeColors.AccentDistortionScreening));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Relational Integrity", SanitizeText(data.RelationalIntegrity), ReportThemeColors.AccentRelationalIntegrity));

                    // =====================================================
                    // STRESS TESTS
                    // =====================================================
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(15).Text("Stress Tests")
                    //    .FontSize(16).Bold();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Geopolitical Shock", SanitizeText(data.GeopoliticalShock), ReportThemeColors.AccentGeopoliticalShock));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Finance Shock", SanitizeText(data.FinanceShock), ReportThemeColors.AccentFinanceShock));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Legitimacy Shock", SanitizeText(data.LegitimacyShock), ReportThemeColors.AccentLegitimacyShock));
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(8).Element(c =>
                    //    PillarContentSection(c, "Overall Stress Resilience", SanitizeText(data.OverallStressResilience), ReportThemeColors.AccentStressResilience));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Stress Score Adjustment", SanitizeText(data.StressScoreAdjustment), ReportThemeColors.AccentStressAdjustment));

                    // =====================================================
                    // GOVERNANCE ADJUSTMENTS
                    // =====================================================
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(15).Text("Governance Adjustments")
                    //    .FontSize(16).Bold();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Inclusion & Equity Adjustment", SanitizeText(data.InclusionEquityAdjustment), ReportThemeColors.AccentInclusionEquityAdj));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Opacity Risk", SanitizeText(data.OpacityRisk), ReportThemeColors.AccentOpacityRisk));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Non Compensation Note", SanitizeText(data.NonCompensationNote), ReportThemeColors.AccentNonCompensation));

                    // =====================================================
                    // SYSTEM ANALYSIS
                    // =====================================================
                    //column.Item().PageBreak();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Cross-Pillar System Dynamics", SanitizeText(data.CrossPillarPatterns), ReportThemeColors.AccentCrossPillar));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Institutional Capacity Assessment", SanitizeText(data.InstitutionalCapacity), ReportThemeColors.DeepTeal));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Equity Assessment", SanitizeText(data.EquityAssessment), ReportThemeColors.AccentEquityAssessment));

                    //column.Item().PageBreak();
                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Governance Trajectory", SanitizeText(data.GovernanceTrajectory), ReportThemeColors.AccentGovernanceTrajectory));

                    // =====================================================
                    // STRATEGIC OUTPUT
                    // =====================================================
                    //column.Item().PageBreak();                

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Strategic Policy Priorities", SanitizeText(data.StrategicRecommendation), ReportThemeColors.AccentStrategicPolicy));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Why This Assessment Matters", SanitizeText(data.AssessmentValueNote), ReportThemeColors.AccentAssessmentValue));

                    if (!string.IsNullOrWhiteSpace(data.KeyFindings))
                    {
                        column.Item().PaddingTop(8).Element(c =>
                            PillarContentSection(c, "Key Findings", SanitizeText(data.KeyFindings), ReportThemeColors.AccentKeyDevelopments));
                    }   

                }
            });
        }

        void AssessmentRecommendations(IContainer container, AiProgramSummeryDto data, UserRole userRole, bool isAllCountries = false)
        {
            container.PaddingTop(4).Column(column =>
            {
                if (!isAllCountries)
                {
                    if (!string.IsNullOrWhiteSpace(data.Recommendations))
                        column.Item().PaddingTop(8).Element(c =>
                            PillarContentSection(c, "Recommendations", SanitizeText(data.Recommendations), ReportThemeColors.AccentStrategicPolicy));
                }
            });
        }
        void PillarComposeContent(
     IContainer container, AiProgramPillarResponse data, UserRole userRole)
        {
            container.PaddingTop(8).Column(column =>
            {
                // =========================
                // PROGRESS SECTION
                // =========================
                column.Item().PaddingTop(10)
                    .Element(c => PillarProgressSection(c, data, userRole));

                // =========================
                // EXECUTIVE SUMMARY
                // =========================
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Executive Summary", SanitizeText(data.EvidenceSummary), ReportThemeColors.AccentExecutiveSummary));

               
                // =====================================================
                // EVIDENCE SECTION
                // =====================================================
                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Structural Evidence", SanitizeText(data.StructuralEvidence), ReportThemeColors.AccentKeyDevelopments));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Operational Evidence", SanitizeText(data.OperationalEvidence), ReportThemeColors.AccentCriticalRisks));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Outcome Evidence", SanitizeText(data.OutcomeEvidence), ReportThemeColors.AccentGaps));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Perception Evidence", SanitizeText(data.PerceptionEvidence), ReportThemeColors.AccentPerceptionEvidenceAlt));

                // =====================================================
                // INTEGRITY CHECKS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Temporal Scope", SanitizeText(data.TemporalScope), ReportThemeColors.AccentTemporalScopeAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Distortion Screening", SanitizeText(data.DistortionScreening), ReportThemeColors.AccentDistortionScreeningAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Relational Integrity", SanitizeText(data.RelationalIntegrity), ReportThemeColors.AccentRelationalIntegrityAlt));

                // =====================================================
                // STRESS TESTS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Geopolitical Shock", SanitizeText(data.StressGeopoliticalShock), ReportThemeColors.AccentGeopoliticalShockAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Finance Shock", SanitizeText(data.StressFinanceShock), ReportThemeColors.AccentFinanceShockAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Legitimacy Shock", SanitizeText(data.StressLegitimacyShock), ReportThemeColors.AccentLegitimacyShockAlt));

                //column.Item().PageBreak();

                //column.Item().PaddingTop(8).Element(c =>
                //    PillarContentSection(c, "Overall Stress Resilience", SanitizeText(data.StressOverallResilience), ReportThemeColors.AccentStressResilienceAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Stress Score Adjustment", SanitizeText(data.StressScoreAdjustment), ReportThemeColors.AccentStressAdjustmentAlt));

                // =====================================================
                // GOVERNANCE ADJUSTMENTS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Inclusion & Equity Adjustment", SanitizeText(data.InclusionEquityAdjustment), ReportThemeColors.AccentInclusionEquityAdjAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Opacity Risk", SanitizeText(data.OpacityRisk), ReportThemeColors.AccentOpacityRiskAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Non Compensation Note", SanitizeText(data.NonCompensationNote), ReportThemeColors.AccentNonCompensationAlt));

                // =====================================================
                // ALERTS & EQUITY
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Red Flags", SanitizeText(data.RedFlag), ReportThemeColors.DangerRedFlag, ReportThemeColors.DangerRedFlagAlt));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Inclusion & Access Note", SanitizeText(data.InclusionAccessNote), ReportThemeColors.DeepTeal));

                // =====================================================
                // SYSTEM / INSTITUTIONAL ANALYSIS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Institutional Assessment", SanitizeText(data.InstitutionalAssessment), ReportThemeColors.AccentStrategicPolicy));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Analytical Foundations and Data Integration", SanitizeText(data.DataGapAnalysis), ReportThemeColors.AccentDataGap));

                // =====================================================
                // DATA SOURCES
                // =====================================================
                if (data.DataSourceCitations?.Any() == true)
                {
                    column.Item().PageBreak();

                    column.Item().PaddingTop(8).Element(c =>
                        DataSourcesSection(c, data.DataSourceCitations.ToList()));
                }
            });
        }

        void ProgramProgressSection(IContainer container, AiProgramSummeryDto data, UserRole userRole)
        {
            container
                .Background(ReportThemeColors.White)
                .Border(1).BorderColor(ReportThemeColors.Gray300)
                .Padding(18)
                .Column(column =>
                {
                    // Header
                    column.Item().Text("Overview")
                        .FontSize(16)
                        .SemiBold()
                        .FontColor(ReportThemeColors.GrayTailwind800);

                    column.Item().PaddingTop(8).Column(col =>
                    {
                        // Score Section
                        PillarProgressBar(col, "Total Score", data.AIProgress, ReportThemeColors.DarkBlue);

                        col.Item().PaddingVertical(12);

                        // Divider
                        col.Item().Height(1).Background(ReportThemeColors.Gray150);

                        col.Item().PaddingTop(12);

                        // Ranking Section Title
                        col.Item().Text("Rankings")
                            .FontSize(13)
                            .SemiBold()
                            .FontColor(ReportThemeColors.GrayTailwind700);

                        col.Item().PaddingTop(8);

                        RankRowModern(col, "Program Rank", data.Rank, data.TotalProgram, ReportThemeColors.DarkBlue);
                    });
                });
        }
        void RankRowModern(ColumnDescriptor column, string label, int? rank, int? total, string color)
        {
            column.Item().PaddingVertical(4).Row(row =>
            {
                row.RelativeItem()
                    .Text(label)
                    .FontSize(11)
                    .FontColor(ReportThemeColors.GrayTailwind500);

                row.AutoItem().AlignRight().Element(e =>
                {
                    e.PaddingHorizontal(10)
                     .PaddingVertical(4)
                     .Padding(2)
                     .Background(ReportThemeColors.Gray50)
                     .Border(1)
                     .BorderColor(ReportThemeColors.Gray300)
                     .Text(txt =>
                     {
                         if (rank.HasValue && total.HasValue)
                         {
                             txt.Span($"{rank}")
                                .Bold()
                                .FontColor(color);

                             txt.Span($" / {total}")
                                .FontColor(ReportThemeColors.GrayTailwind400);
                         }
                         else
                         {
                             txt.Span("-").FontColor(ReportThemeColors.Gray550);
                         }
                     });
                });
            });
        }

        void PillarProgressSection(
            IContainer container, AiProgramPillarResponse data, UserRole userRole, bool isProgram = false)
        {
            container
                .Background(ReportThemeColors.White)
                .Border(1).BorderColor(ReportThemeColors.Gray300)
                .Padding(18)
                .Column(column =>
                {
                    column.Item().Text(isProgram ? "Total Overview" : "Pillar Score")
                        .FontSize(16)
                        .SemiBold()
                        .FontColor(ReportThemeColors.GrayTailwind800);

                    column.Item().PaddingTop(15);

                    PillarProgressBar(column, "Score", data.AIProgress, ReportThemeColors.DarkBlue);
                });
        }
        void PillarProgressBar(ColumnDescriptor column, string label, decimal? percentage, string color)
        {
            float per = (float)Math.Min((double)(percentage ?? 0), 100);

            column.Item().Column(col =>
            {
                // Label + Value Row
                col.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Text(label)
                        .FontSize(11)
                        .FontColor(ReportThemeColors.GrayTailwind500);

                    row.AutoItem()
                        .Text($"{percentage ?? 0:F1}")
                        .FontSize(11)
                        .Bold()
                        .FontColor(ReportThemeColors.GrayTailwind900);
                });

                col.Item().PaddingTop(6);

                // Progress Bar
                col.Item().Height(8).Background(ReportThemeColors.Gray300).Row(barRow =>
                {
                    barRow.RelativeItem(per)
                        .Background(color);

                    barRow.RelativeItem(100 - per);
                });
            });
        }

        /// <summary>Generic titled content block with accent bar.</summary>
        static void PillarContentSection(
            IContainer container, string title, string content, string accentColor, string textcolor = ReportThemeColors.SectionContentText)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background(accentColor);
                    row.RelativeItem().Background(ReportThemeColors.Gray100).Padding(12)
                        .Text(title).FontSize(15).Bold().FontColor(ReportThemeColors.Gray950);
                });

                column.Item()
                    .Background(ReportThemeColors.White)
                    .Border(1).BorderColor(ReportThemeColors.Gray350)
                    .Padding(18)
                    .Text(NormalizeListLineBreaks(content))
                    .FontSize(10).LineHeight(1.6f).FontColor(textcolor);
            });
        }

        static string NormalizeListLineBreaks(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;
            var text = content.Replace("\r\n", "\n").Replace("\r", "\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\|\|\s*", "\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+(?=\d+\))", "\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{2,}", "\n");
            return text.Trim();
        }
        void DataSourcesSection(IContainer container, List<AIDataSourceCitation> sources)
        {
            // ?? Sanitize entire list first (best practice)
            var safeSources = sources?
                .Select(s => new AIDataSourceCitation
                {
                    SourceName = SanitizeText(s.SourceName),
                    SourceType = SanitizeText(s.SourceType),
                    DataExtract = SanitizeText(s.DataExtract),
                    SourceURL = SanitizeText(s.SourceURL),
                    TrustLevel = s.TrustLevel,
                    DataYear = s.DataYear
                })
                .ToList() ?? new List<AIDataSourceCitation>();

            container.Column(column =>
            {
                // Header
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background(ReportThemeColors.SectionAccentBar);

                    row.RelativeItem()
                        .Background(ReportThemeColors.Gray100)
                        .Padding(12)
                        .Text(SanitizeText("Data Source Citations")) // ?? safe
                        .FontSize(15).Bold().FontColor(ReportThemeColors.Gray950);
                });

                // Content Box
                column.Item().PaddingTop(10)
                    .Background(ReportThemeColors.White)
                    .Border(1)
                    .BorderColor(ReportThemeColors.Gray350)
                    .Padding(15)
                    .Column(col =>
                    {
                        foreach (var source in safeSources.Take(10))
                        {
                            col.Item().PaddingBottom(15).Column(sourceCol =>
                            {
                                // -- Row 1: Name + Type ---------------------
                                sourceCol.Item().Row(row =>
                                {
                                    row.RelativeItem()
                                        .Text(source.SourceName ?? "-")
                                        .FontSize(11).Bold().FontColor(ReportThemeColors.SectionTitleGreen);

                                    row.ConstantItem(100).AlignRight()
                                        .Background(GetSourceTypeBadgeColor(source.SourceType))
                                        .Padding(3)
                                        .Text(source.SourceType ?? "-")
                                        .FontSize(8)
                                        .FontColor(ReportThemeColors.White);
                                });

                                // -- Row 2: Trust + Year --------------------
                                sourceCol.Item().PaddingTop(4).Row(row =>
                                {
                                    row.AutoItem()
                                        .Text(SanitizeText($"Trust Level: {source.TrustLevel}/7"))
                                        .FontSize(9).FontColor(ReportThemeColors.Gray650);

                                    row.AutoItem()
                                        .PaddingLeft(15)
                                        .Text(SanitizeText($"Year: {source.DataYear}"))
                                        .FontSize(9).FontColor(ReportThemeColors.Gray650);
                                });

                                // -- Data Extract ---------------------------
                                if (!string.IsNullOrWhiteSpace(source.DataExtract))
                                {
                                    sourceCol.Item().PaddingTop(6)
                                        .Text(TruncateText(source.DataExtract, 200))
                                        .FontSize(9)
                                        .FontColor(ReportThemeColors.Gray750)
                                        .Italic();
                                }

                                // -- URL ------------------------------------
                                if (!string.IsNullOrWhiteSpace(source.SourceURL))
                                {
                                    sourceCol.Item().PaddingTop(4)
                                        .Text(source.SourceURL)
                                        .FontSize(8)
                                        .FontColor(ReportThemeColors.LabelGreen)
                                        .Underline();
                                }
                            });

                            // Divider
                            if (source != safeSources.Last())
                            {
                                col.Item()
                                    .PaddingBottom(10)
                                    .LineHorizontal(1)
                                    .LineColor(ReportThemeColors.BorderLight);
                            }
                        }
                    });
            });
        }

        // -----------------------------------------------------------------------------
        //  COLOR / FORMAT UTILITIES  (all static, reusable across pages)
        // -----------------------------------------------------------------------------


        static SKColor GetColor(float value)
        {
            if (value > 40)
                return SKColor.Parse(ReportThemeColors.DarkGreen);

            else if (value > 20)
                return SKColor.Parse(ReportThemeColors.SuccessGreen);

            else if (value > 5)
                return SKColor.Parse(ReportThemeColors.WarningAmber);

            else if (value > -20)
                return SKColor.Parse(ReportThemeColors.Yellow);

            else if (value > -39)
                return SKColor.Parse(ReportThemeColors.DangerRed);

            return SKColor.Parse(ReportThemeColors.DarkRed);
        }

        static string GetBarColor(float value)
        {
            if (value > 40)
                return ReportThemeColors.DarkGreen;

            else if (value > 20)
                return ReportThemeColors.SuccessGreen;

            else if (value > 5)
                return ReportThemeColors.WarningAmber;

            else if (value > -20)
                return ReportThemeColors.Yellow;

            else if (value > -39)
                return ReportThemeColors.DangerRed;

            return ReportThemeColors.DarkRed;
        }

        static string GetPillarBarColors(float value)
        {
            if (value >= 80) return ReportThemeColors.SuccessGreen;
            else if (value >= 60) return ReportThemeColors.BarGreenLow;
            else if (value >= 40) return ReportThemeColors.WarningAmber;
            else if (value >= 20) return ReportThemeColors.BarOrangeMid;
            return ReportThemeColors.DangerRed;
        }

        static SKColor GetPillarColors(float value)
        {
            if (value >= 80) return SKColor.Parse(ReportThemeColors.SuccessGreen);
            else if (value >= 60) return SKColor.Parse(ReportThemeColors.BarGreenLow);
            else if (value >= 40) return SKColor.Parse(ReportThemeColors.WarningAmber);
            else if (value >= 20) return SKColor.Parse(ReportThemeColors.BarOrangeMid);
            return SKColor.Parse(ReportThemeColors.DangerRed);
        }

        static string GetSourceTypeBadgeColor(string sourceType) => sourceType?.ToLower() switch
        {
            "government" => ReportThemeColors.SourceGovernment,
            "academic" => ReportThemeColors.SourceAcademic,
            "international" => ReportThemeColors.SourceInternational,
            "news/ngo" => ReportThemeColors.SourceNewsNgo,
            _ => ReportThemeColors.SourceDefault
        };

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Length <= max ? text : text[..max] + ".";
        }

        static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text[..maxLength] + "...";
        }

        #endregion pdf pillars and Program report

    }

    public partial class PdfGeneratorService
    {   

        // Palette: index 0 = selected Program (gold), 1-5 = peer programs
        private static readonly string[] ProgramPalette = ReportThemeColors.ProgramChartPalette;

        // Pillar palette (up to 14 distinct colours)
        private static readonly string[] PillarPalette = ReportThemeColors.PillarChartPalette;

        // --------------------------------------------------------------------------
        //  ENTRY POINTS  . called from AddProgramDetailsPdf
        // --------------------------------------------------------------------------

        void AddPeerProgramComparisonSection(
            IDocumentContainer container,
            List<PeerProgramHistoryReportDto> peerPrograms,
            AiProgramSummeryDto ProgramDetails,
            UserRole userRole)
        {
            if (peerPrograms == null || !peerPrograms.Any()) return;

            // Separate: main Program entry + actual peer entries (cap at MaxpeerPrograms)
            var main = FindMainProgram(peerPrograms, ProgramDetails);
            var peers = peerPrograms
                .Where(p => !IsSameProgram(p.ProgramName, ProgramDetails.ProgramName))
                .ToList();

            // -- 5.5  Relative Ranking --------------------------------------------
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    ProgramComposeHeader(x, ProgramDetails, userRole, "Relative Ranking Among Peer programs"));
                page.Content().Element(c =>
                    RelativeRankingPage(c, peers, main, ProgramDetails));
                PageFooter(page);
            });
        }

        //  5.5  RELATIVE RANKING AMONG PEER programs
        // --------------------------------------------------------------------------

        void RelativeRankingPage(
            IContainer container,
            List<PeerProgramHistoryReportDto> peers,
            PeerProgramHistoryReportDto? main,
            AiProgramSummeryDto programDetails)
        {
            // Build ranked list including main Program; include 0-score programs
            var all = BuildAllPrograms(main, peers)
                .Select(c => (Program: c, Score: GetLatestScoreOrZero(c)))
                .OrderByDescending(x => x.Score)
                .ToList();

            int total = all.Count;
            int mainRank = all.FindIndex(r => IsSameProgram(r.Program.ProgramName, programDetails.ProgramName)) + 1;
            float mainScore = mainRank > 0 ? all[mainRank - 1].Score : 0f;
            float pctile = mainRank > 0 ? (1f - (float)mainRank / total) * 100f : 0f;

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                // -- Hero rank banner ------------------------------------------
                col.Item().Background(ReportThemeColors.PdfDarkGreen).Padding(14).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"#{mainRank} of {total}")
                            .FontSize(32).Bold().FontColor(ReportThemeColors.WarningGold);
                        c.Item().Text($"{programDetails.ProgramName}  \u00b7  {programDetails.Location}")
                            .FontSize(12).FontColor(ReportThemeColors.SuccessGreenSoft);
                    });
                    row.ConstantItem(130).Column(c =>
                    {
                        c.Item().AlignRight().Text("Score").FontSize(9).FontColor(ReportThemeColors.GraySilver);
                        c.Item().AlignRight().Text($"{mainScore:F1}")
                            .FontSize(28).Bold().FontColor(ReportThemeColors.White);
                        c.Item().AlignRight().Text($"Top {100 - pctile:F0}% of peers")
                            .FontSize(10).FontColor(ReportThemeColors.PdfTealGreen);
                    });
                });

                // -- Score distribution histogram ------------------------------
                //col.Item().Text("Score Distribution Among All programs")
                //    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                //col.Item().Height(150).Canvas((canvas, size) =>
                //    DrawHistogram(canvas, size,
                //        all.Select(r => r.Score).ToList(), mainScore, 10));

                // -- Full ranking table ----------------------------------------
                col.Item().Text("Full Program Ranking").FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(24);   // rank
                        cols.ConstantColumn(65);   // Program name
                        cols.ConstantColumn(70);   // conference year
                        cols.RelativeColumn();     //Description
                        cols.ConstantColumn(65);   // Location
                        cols.ConstantColumn(65);   // Score
                    });

                    DrawTableHeader(table, new[] { "#", "Program", "Conference Year", "Description" ,"Location", "Score" });


                    foreach (var (entry, idx) in all.Select((e, i) => (e, i)))
                    {
                        bool isMain = IsSameProgram(entry.Program.ProgramName, programDetails.ProgramName);
                        string bg = isMain ? ReportThemeColors.SurfaceSelected : (idx % 2 == 0 ? ReportThemeColors.White : ReportThemeColors.PageBg);
                        string rankColor = idx == 0 ? ReportThemeColors.WarningGold
                                         : idx == 1 ? ReportThemeColors.GraySilver
                                         : idx == 2 ? ReportThemeColors.WarningBronze : ReportThemeColors.Gray800;

                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.Gray250)
                            .Padding(4).Text($"{idx + 1}").FontSize(8).FontColor(rankColor);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.Gray250)
                            .Padding(4).Text(entry.Program.ProgramName).FontSize(8)
                            .FontColor(isMain ? ReportThemeColors.PdfDarkGreen : ReportThemeColors.Gray900);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.Gray250)
                            .Padding(4).Text(entry.Program.Year?.ToString() ?? ".").FontSize(8).FontColor(ReportThemeColors.Gray800);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.Gray250)
                            .Padding(4).Text(entry.Program.Description ?? ".").FontSize(8).FontColor(ReportThemeColors.Gray800);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.Gray250)
                            .Padding(4).Text(entry.Program.Location ?? ".").FontSize(8).FontColor(ReportThemeColors.Gray800);
                        //table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.Gray250)
                        //    .Padding(4).AlignRight()
                        //    .Text(FormatPop(entry.Program.Population)).FontSize(8).FontColor(ReportThemeColors.Gray800);

                        table.Cell()
                        .Background(bg)
                        .BorderBottom(0.5f)
                        .BorderColor(ReportThemeColors.Gray250)
                        .Padding(4)
                        .Row(r =>
                        {
                            var percent = entry.Score / 100f;
                            r.ConstantItem(24).AlignRight().Text($"{entry.Score:F1}")
                                .FontSize(8)
                                .FontColor(ReportThemeColors.Gray900);
                        });
                    }
                });
            });
        }

        static void DrawTableHeader(TableDescriptor table, string[] headers)
        {
            foreach (string h in headers)
                table.Cell().Background(ReportThemeColors.PdfDarkGreen).Padding(5)
                    .Text(h).FontSize(8).Bold().FontColor(ReportThemeColors.White);
        }

        // --------------------------------------------------------------------------
        //  UTILITY HELPERS
        // --------------------------------------------------------------------------

        /// <summary>
        /// Returns the latest year's score including 0.
        /// Returns -1 only when there is genuinely NO history entry at all.
        /// </summary>
        static float GetLatestScoreOrZero(PeerProgramHistoryReportDto program)
        {
            var last = program.ProgramHistory?
                .OrderByDescending(h => h.Year)
                .FirstOrDefault();
            return last != null ? (float)last.ScoreProgress : 0f;
        }

        /// <summary>Returns main-Program entry from the combined list; null if not found.</summary>
        static PeerProgramHistoryReportDto? FindMainProgram(
            List<PeerProgramHistoryReportDto> all, AiProgramSummeryDto ProgramDetails) =>
            all.FirstOrDefault(p => IsSameProgram(p.ProgramName, ProgramDetails.ProgramName));

        /// <summary>Case-insensitive Program name equality check.</summary>
        static bool IsSameProgram(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Builds a deduplicated list: main Program first, then peers.</summary>
        static List<PeerProgramHistoryReportDto> BuildAllPrograms(
            PeerProgramHistoryReportDto? main,
            List<PeerProgramHistoryReportDto> peers)
        {
            var list = new List<PeerProgramHistoryReportDto>();
            if (main != null) list.Add(main);
            list.AddRange(peers);
            return list;
        }
       
    }

}
