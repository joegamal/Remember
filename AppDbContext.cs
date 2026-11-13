using Microsoft.EntityFrameworkCore;
using OrbitFlame.Api.Entities;

namespace OrbitFlame.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CatalogMetadata> CatalogMetadata => Set<CatalogMetadata>();
    public DbSet<Investigation> Investigations => Set<Investigation>();
    public DbSet<ResearchTopic> ResearchTopics => Set<ResearchTopic>();
    public DbSet<CoverageGap> CoverageGaps => Set<CoverageGap>();
    public DbSet<InvestigationTopic> InvestigationTopics => Set<InvestigationTopic>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<InvestigationMaterial> InvestigationMaterials => Set<InvestigationMaterial>();
    public DbSet<Variable> Variables => Set<Variable>();
    public DbSet<InvestigationVariable> InvestigationVariables => Set<InvestigationVariable>();
    public DbSet<MeasurementType> MeasurementTypes => Set<MeasurementType>();
    public DbSet<InvestigationMeasurementType> InvestigationMeasurementTypes => Set<InvestigationMeasurementType>();
    public DbSet<CsvField> CsvFields => Set<CsvField>();
    public DbSet<Limitation> Limitations => Set<Limitation>();
    public DbSet<Evidence> EvidenceItems => Set<Evidence>();
    public DbSet<RelevanceReason> RelevanceReasons => Set<RelevanceReason>();
    public DbSet<RelevanceReasonEvidence> RelevanceReasonEvidence => Set<RelevanceReasonEvidence>();
    public DbSet<KnowledgeDocument> Documents => Set<KnowledgeDocument>();
    public DbSet<DocumentInvestigation> DocumentInvestigations => Set<DocumentInvestigation>();
    public DbSet<Passage> Passages => Set<Passage>();
    public DbSet<PassageInvestigation> PassageInvestigations => Set<PassageInvestigation>();
    public DbSet<FlexRecord> FlexRecords => Set<FlexRecord>();
    public DbSet<FlexQualityFlag> FlexQualityFlags => Set<FlexQualityFlag>();
    public DbSet<FlexRawPsiField> FlexRawPsiFields => Set<FlexRawPsiField>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CatalogMetadata>(entity =>
        {
            entity.ToTable("CatalogMetadata");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Purpose).HasMaxLength(1000);
            entity.Property(x => x.RankingMeaning).HasMaxLength(200);
        });

        modelBuilder.Entity<Investigation>(entity =>
        {
            entity.ToTable("Investigations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.ShortName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(400).IsRequired();
            entity.Property(x => x.Doi).HasMaxLength(128);
            entity.Property(x => x.FuelFamily).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.DataAvailability).HasMaxLength(128).IsRequired();
            entity.Property(x => x.LocalMetadataPath).HasMaxLength(400);
            entity.Property(x => x.LocalCsvPath).HasMaxLength(400);
            entity.Property(x => x.LocalCsvSha256).HasMaxLength(64);
            entity.Property(x => x.CurationStatus).HasMaxLength(200);

            entity.HasIndex(x => x.ShortName).IsUnique();
            entity.HasIndex(x => x.Doi).IsUnique();
            entity.HasIndex(x => x.FuelFamily);
        });

        modelBuilder.Entity<ResearchTopic>(entity =>
        {
            entity.ToTable("ResearchTopics");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.Label).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<CoverageGap>(entity =>
        {
            entity.ToTable("CoverageGaps");
            entity.HasKey(x => x.TopicId);
            entity.Property(x => x.TopicId).HasMaxLength(64);
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();

            entity.HasOne(x => x.Topic)
                .WithOne(x => x.CoverageGap)
                .HasForeignKey<CoverageGap>(x => x.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvestigationTopic>(entity =>
        {
            entity.ToTable("InvestigationTopics");
            entity.HasKey(x => new { x.InvestigationId, x.TopicId });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.TopicId).HasMaxLength(64);
            entity.HasIndex(x => new { x.InvestigationId, x.Position }).IsUnique();

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.TopicLinks)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Topic)
                .WithMany(x => x.InvestigationLinks)
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("Materials");
            entity.HasKey(x => x.Name);
            entity.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<InvestigationMaterial>(entity =>
        {
            entity.ToTable("InvestigationMaterials");
            entity.HasKey(x => new { x.InvestigationId, x.MaterialName });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.MaterialName).HasMaxLength(200);
            entity.HasIndex(x => new { x.InvestigationId, x.Position }).IsUnique();

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.MaterialLinks)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Material)
                .WithMany(x => x.InvestigationLinks)
                .HasForeignKey(x => x.MaterialName)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Variable>(entity =>
        {
            entity.ToTable("Variables");
            entity.HasKey(x => x.Name);
            entity.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<InvestigationVariable>(entity =>
        {
            entity.ToTable("InvestigationVariables");
            entity.HasKey(x => new { x.InvestigationId, x.VariableName });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.VariableName).HasMaxLength(200);
            entity.HasIndex(x => new { x.InvestigationId, x.Position }).IsUnique();

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.VariableLinks)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variable)
                .WithMany(x => x.InvestigationLinks)
                .HasForeignKey(x => x.VariableName)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeasurementType>(entity =>
        {
            entity.ToTable("MeasurementTypes");
            entity.HasKey(x => x.Name);
            entity.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<InvestigationMeasurementType>(entity =>
        {
            entity.ToTable("InvestigationMeasurementTypes");
            entity.HasKey(x => new { x.InvestigationId, x.MeasurementTypeName });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.MeasurementTypeName).HasMaxLength(200);
            entity.HasIndex(x => new { x.InvestigationId, x.Position }).IsUnique();

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.MeasurementTypeLinks)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MeasurementType)
                .WithMany(x => x.InvestigationLinks)
                .HasForeignKey(x => x.MeasurementTypeName)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CsvField>(entity =>
        {
            entity.ToTable("CsvFields");
            entity.HasKey(x => new { x.InvestigationId, x.Position });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.InvestigationId, x.Name }).IsUnique();

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.CsvFields)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Limitation>(entity =>
        {
            entity.ToTable("Limitations");
            entity.HasKey(x => new { x.InvestigationId, x.Position });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.Limitations)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Evidence>(entity =>
        {
            entity.ToTable("Evidence");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.InvestigationId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(400).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Text).IsRequired();

            // The alternate key lets a relevance citation enforce that its evidence
            // belongs to the same investigation as the relevance reason.
            entity.HasAlternateKey(x => new { x.InvestigationId, x.Id });

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.EvidenceItems)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Document)
                .WithMany(x => x.EvidenceItems)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RelevanceReason>(entity =>
        {
            entity.ToTable("RelevanceReasons");
            entity.HasKey(x => new { x.InvestigationId, x.TopicId });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.TopicId).HasMaxLength(64);
            entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.RelevanceReasons)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Topic)
                .WithMany(x => x.RelevanceReasons)
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RelevanceReasonEvidence>(entity =>
        {
            entity.ToTable("RelevanceReasonEvidence");
            entity.HasKey(x => new { x.InvestigationId, x.TopicId, x.EvidenceId });
            entity.Property(x => x.InvestigationId).HasMaxLength(64);
            entity.Property(x => x.TopicId).HasMaxLength(64);
            entity.Property(x => x.EvidenceId).HasMaxLength(128);

            entity.HasOne(x => x.Reason)
                .WithMany(x => x.EvidenceLinks)
                .HasForeignKey(x => new { x.InvestigationId, x.TopicId })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Evidence)
                .WithMany(x => x.RelevanceReasonLinks)
                .HasForeignKey(x => new { x.InvestigationId, x.EvidenceId })
                .HasPrincipalKey(x => new { x.InvestigationId, x.Id })
                // SQL Server rejects multiple cascade paths here. The reason link keeps
                // cascade cleanup; deleting evidence with citations is blocked by the FK.
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.Title).HasMaxLength(400).IsRequired();
            entity.Property(x => x.SourceUrl).HasMaxLength(500).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();

            entity.HasIndex(x => x.FileName).IsUnique();
            entity.HasIndex(x => x.Sha256);
        });

        modelBuilder.Entity<DocumentInvestigation>(entity =>
        {
            entity.ToTable("DocumentInvestigations");
            entity.HasKey(x => new { x.DocumentId, x.InvestigationId });
            entity.Property(x => x.DocumentId).HasMaxLength(64);
            entity.Property(x => x.InvestigationId).HasMaxLength(64);

            entity.HasOne(x => x.Document)
                .WithMany(x => x.InvestigationLinks)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.DocumentLinks)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Passage>(entity =>
        {
            entity.ToTable("Passages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.DocumentId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(400).IsRequired();
            entity.Property(x => x.PrintedPage).HasMaxLength(64);
            entity.Property(x => x.Url).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Text).IsRequired();
            entity.Property(x => x.ExtractionStatus).HasMaxLength(300);

            entity.HasIndex(x => new { x.DocumentId, x.PdfPage });

            entity.HasOne(x => x.Document)
                .WithMany(x => x.Passages)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PassageInvestigation>(entity =>
        {
            entity.ToTable("PassageInvestigations");
            entity.HasKey(x => new { x.PassageId, x.InvestigationId });
            entity.Property(x => x.PassageId).HasMaxLength(128);
            entity.Property(x => x.InvestigationId).HasMaxLength(64);

            entity.HasOne(x => x.Passage)
                .WithMany(x => x.InvestigationLinks)
                .HasForeignKey(x => x.PassageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.PassageLinks)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FlexRecord>(entity =>
        {
            entity.ToTable("FlexRecords");
            entity.HasKey(x => x.RecordId);
            entity.Property(x => x.RecordId).HasMaxLength(128);
            entity.Property(x => x.InvestigationId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReportTestId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PsiTestNumberRaw).HasMaxLength(64);
            entity.Property(x => x.CirIdentifier).HasMaxLength(64);
            entity.Property(x => x.Fuel).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TestEndRaw).HasMaxLength(128);
            entity.Property(x => x.SupportFiber).HasMaxLength(200);
            entity.Property(x => x.VerificationStatus).HasMaxLength(200);
            entity.Property(x => x.ComparisonReadiness).HasMaxLength(200);
            entity.Property(x => x.MissingNumericReason).HasMaxLength(500);
            entity.Property(x => x.SourceReportUrl).HasMaxLength(500);
            entity.Property(x => x.SourcePsiUrl).HasMaxLength(500);
            entity.Property(x => x.PrintedPage).HasMaxLength(64);
            entity.Property(x => x.TableLabel).HasColumnName("Table").HasMaxLength(64);
            entity.Property(x => x.BurningRateUnitCorrectionBasis).HasMaxLength(1000);
            entity.Property(x => x.SourceCsvSha256).HasMaxLength(64);

            entity.HasIndex(x => new { x.InvestigationId, x.ReportTestId }).IsUnique();
            // CIR identifiers are deliberately indexed but not unique: 193F001 occurs twice.
            entity.HasIndex(x => x.CirIdentifier);

            entity.HasOne(x => x.Investigation)
                .WithMany(x => x.FlexRecords)
                .HasForeignKey(x => x.InvestigationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FlexQualityFlag>(entity =>
        {
            entity.ToTable("FlexQualityFlags");
            entity.HasKey(x => new { x.FlexRecordId, x.Position });
            entity.Property(x => x.FlexRecordId).HasMaxLength(128);
            entity.Property(x => x.Value).HasMaxLength(300).IsRequired();

            entity.HasOne(x => x.FlexRecord)
                .WithMany(x => x.QualityFlags)
                .HasForeignKey(x => x.FlexRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FlexRawPsiField>(entity =>
        {
            entity.ToTable("FlexRawPsiFields");
            entity.HasKey(x => new { x.FlexRecordId, x.Position });
            entity.Property(x => x.FlexRecordId).HasMaxLength(128);
            entity.Property(x => x.FieldName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.RawValue).IsRequired();

            entity.HasIndex(x => new { x.FlexRecordId, x.FieldName }).IsUnique();

            entity.HasOne(x => x.FlexRecord)
                .WithMany(x => x.RawPsiFields)
                .HasForeignKey(x => x.FlexRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
