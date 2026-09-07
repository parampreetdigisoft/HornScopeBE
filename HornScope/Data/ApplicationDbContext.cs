using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using HornScope.Common.Models;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.ProgramDto;
using HornScope.Models;
using HornScope.Common.Models.views;

namespace HornScope.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = default!;
        public DbSet<Pillar> Pillars { get; set; } = default!;
        public DbSet<Question> Questions { get; set; } = default!;
        public DbSet<QuestionOption> QuestionOptions { get; set; } = default!;
        public DbSet<AssessmentResponse> AssessmentResponses { get; set; } = default!;
        public DbSet<Assessment> Assessments { get; set; } = default!;
        public DbSet<PillarAssessment> PillarAssessments { get; set; } = default!;
        public DbSet<ClimateProgram> ClimatePrograms { get; set; } = default!;
        public DbSet<StaffProgramMapping> StaffProgramMappings { get; set; } = default!;
        public DbSet<AppLogs> AppLogs { get; set; } = default!;
        public DbSet<PaymentRecord> PaymentRecords { get; set; } = default!;
        public DbSet<ClientProgramMapping> ClientProgramMappings { get; set; } = default!;
        public DbSet<AnalyticalLayer> AnalyticalLayers { get; set; } = default!;
        public DbSet<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = default!;
        public DbSet<AnalyticalLayerResult> AnalyticalLayerResults { get; set; } = default!;
        public DbSet<ClientPillarMapping> ClientPillarMappings { get; set; } = default!;
        public DbSet<AIDataSourceCitation> AIDataSourceCitations { get; set; } = default!;
        public DbSet<AIProgramScore> AIProgramScores { get; set; } = default!;
        public DbSet<AIEstimatedQuestionScore> AIEstimatedQuestionScores { get; set; } = default!;
        public DbSet<AIPillarScore> AIPillarScores { get; set; } = default!;
        public DbSet<AITrustLevel> AITrustLevels { get; set; } = default!;
        public DbSet<AnalyticalLayerPillarMapping> AnalyticalLayerPillarMappings { get; set; } = default!;
        public DbSet<EvaluationProgramProgressResultDto> ProgramProgressResults { get; set; }
        public DbSet<ProgramRankingResultDto> ProgramRankingResults { get; set; }
        public DbSet<GetProgramsProgressAdminDto> GetProgramsProgressAdminDto { get; set; }
        public DbSet<AIEvaluatorProgramMapping> AIEvaluatorProgramMappings { get; set; }
        public DbSet<ProgramPeer> ProgramPeers { get; set; } = default!;
        public DbSet<EvaluationProgramProgressHistoryResultDto> ProgramProgressHistoryResults { get; set; }
        public DbSet<ProgramDocument> ProgramDocuments { get; set; }
        public DbSet<AiPillarStatsLast4MonthsView> AiPillarStatsLast4MonthsView { get; set; }
        public DbSet<AssistantChatHistory> AssistantChatHistory { get; set; }
        public DbSet<AIAssistantFAQ> AIAssistantFAQ { get; set; }
        public DbSet<DocumentChunks> DocumentChunks { get; set; }
        public DbSet<GetAssessmentResponseDto> GetAssessmentResponseDto { get; set; }
        public DbSet<DocumentTOC> DocumentTOC { get; set; }
        public DbSet<DashboardMode> DashboardModes { get; set; } = default!;
        public DbSet<DashboardModeKPIMapping> DashboardModeKPIMappings { get; set; } = default!;
        public DbSet<GetDashboardModeResult> GetDashboardModeResults { get; set; } = default!;
        public DbSet<DashboardInterpretation> DashboardInterpretations { get; set; } = default!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasKey(ur => ur.UserID);

            modelBuilder.Entity<Pillar>().HasKey(uc => uc.PillarID);
            modelBuilder.Entity<Pillar>()
                .HasMany(q => q.Questions)
                .WithOne(qo => qo.Pillar)
                .HasForeignKey(qo => qo.PillarID)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Question>().HasKey(uc => uc.QuestionID);
            modelBuilder.Entity<QuestionOption>().HasKey(qo => qo.OptionID);

            modelBuilder.Entity<Question>()
                .HasMany(q => q.QuestionOptions)
                .WithOne(qo => qo.Question)
                .HasForeignKey(qo => qo.QuestionID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Assessment>().HasKey(uc => uc.AssessmentID);
            modelBuilder.Entity<AssessmentResponse>().HasKey(uc => uc.ResponseID);
            modelBuilder.Entity<PillarAssessment>().HasKey(uc => uc.PillarAssessmentID);

            modelBuilder.Entity<Assessment>()
                .HasMany(r => r.PillarAssessments)
                .WithOne(a=>a.Assessment)
                .HasForeignKey(r => r.AssessmentID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PillarAssessment>()
            .HasMany(r => r.Responses)
            .WithOne(a => a.PillarAssessment)
            .HasForeignKey(r => r.PillarAssessmentID)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentRecord>(entity =>
            {
                entity.HasKey(p => p.PaymentRecordID);
                entity.Property(e => e.Tier)
                      .HasConversion<byte>();

                entity.Property(e => e.PaymentStatus)
                      .HasConversion<byte>();
            });

            modelBuilder.Entity<StaffProgramMapping>().HasKey(uc => uc.StaffProgramMappingID);
            modelBuilder.Entity<ClientProgramMapping>().HasKey(uc => uc.ClientProgramMappingID);

            modelBuilder.Entity<AnalyticalLayer>(entity =>
            {
                entity.HasKey(al => al.LayerID);

                entity.HasMany(al=>al.AnalyticalLayerResults)
                .WithOne(x=>x.AnalyticalLayer)
                .HasForeignKey(x=>x.LayerID);

                entity.HasMany(al => al.FiveLevelInterpretations)
               .WithOne(x => x.AnalyticalLayer)
               .HasForeignKey(x => x.LayerID);
            });
            modelBuilder.Entity<AnalyticalLayerResult>(entity =>
            {
                entity.HasKey(al => al.LayerResultID);
            });
            modelBuilder.Entity<FiveLevelInterpretation>(entity =>
            {
                entity.HasKey(al => al.InterpretationID);
            });
            modelBuilder.Entity<ClientPillarMapping>().HasKey(ur => ur.ClientPillarMappingID);

            modelBuilder.Entity<AIDataSourceCitation>().HasKey(ur => ur.CitationID);
            modelBuilder.Entity<AIProgramScore>(entity =>
            {
                entity.HasKey(e => e.ProgramScoreID);
            });
            modelBuilder.Entity<AIEstimatedQuestionScore>().HasKey(ur => ur.QuestionScoreID);
            modelBuilder.Entity<AIPillarScore>().HasKey(ur => ur.PillarScoreID);
            modelBuilder.Entity<AITrustLevel>().HasKey(ur => ur.TrustID);
            modelBuilder.Entity<AnalyticalLayerPillarMapping>().HasKey(ur => ur.AnalyticalLayerPillarMappingID);
            modelBuilder.Entity<AIEvaluatorProgramMapping>().HasKey(ur => ur.AIEvaluatorProgramMappingID);
            modelBuilder.Entity<EvaluationProgramProgressResultDto>().HasNoKey().ToView(null); 
            modelBuilder.Entity<ProgramRankingResultDto>().HasNoKey().ToView(null); 
            modelBuilder.Entity<GetProgramsProgressAdminDto>().HasNoKey().ToView(null);
            modelBuilder.Entity<EvaluationProgramProgressHistoryResultDto>().HasNoKey().ToView(null);
            modelBuilder.Entity<ProgramPeer>(entity =>
            {
                entity.HasKey(e => e.ProgramPeerID);
                entity.ToTable("ProgramPeers");
            });
            modelBuilder.Entity<ClimateProgram>(entity =>
            {
                entity.HasKey(e => e.ClimateProgramID);
                entity.ToTable("ClimatePrograms");
            });

            modelBuilder.Entity<ProgramDocument>(entity =>
            {
                entity.HasKey(e => e.ProgramDocumentID);
                entity.ToTable("ProgramDocuments");
            });

            modelBuilder.Entity<AiPillarStatsLast4MonthsView>()
            .HasNoKey()
            .ToView("vw_AiGetPillarStats_Last4Months");

            modelBuilder.Entity<AssistantChatHistory>(entity =>
            {
                entity.HasKey(e => e.ChatID);
                entity.ToTable("AssistantChatHistory");
            });

            modelBuilder.Entity<AIAssistantFAQ>(entity =>
            {
                entity.HasKey(e => e.FAQID);
                entity.ToTable("AIAssistantFAQ");
            });

            modelBuilder.Entity<DocumentTOC>(entity =>
            {
                entity.HasKey(e => e.TOCID);
                entity.ToTable("DocumentTOC");
            });
            modelBuilder.Entity<DocumentChunks>(entity =>
            {
                entity.HasKey(e => e.ChunkID);
                entity.ToTable("DocumentChunks");
            });

            modelBuilder.Entity<GetAssessmentResponseDto>(eb =>
            {
                eb.HasNoKey();   // it's a projection, not a table row - no PK
                eb.ToView(null); // not mapped to any table/view, only used via FromSqlRaw
            });

            modelBuilder.Entity<DashboardMode>(entity =>
            {
                entity.HasKey(e => e.DashboardModeID);
                entity.ToTable("DashboardModes");

                entity.HasMany(e => e.DashboardModeKPIMappings)
                    .WithOne(m => m.DashboardMode)
                    .HasForeignKey(m => m.DashboardModeID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DashboardModeKPIMapping>(entity =>
            {
                entity.HasKey(e => e.DashboardModeKPIMappingID);
                entity.ToTable("DashboardModeKPIMappings");
            });

            modelBuilder.Entity<GetDashboardModeResult>().HasNoKey().ToView(null);

            modelBuilder.Entity<DashboardInterpretation>(entity =>
            {
                entity.HasKey(al => al.DashboardInterpretationID);
            });

            base.OnModelCreating(modelBuilder);
        }

    }
}
