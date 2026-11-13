namespace OrbitFlame.Api.Entities;

// Persistence model for the catalog and knowledge base. These types are kept separate from
// Contracts/Models.cs so EF Core mapping never leaks into the API contract.
public sealed class CatalogMetadata
{
    public int Id { get; set; } = 1;
    public int Version { get; set; }
    public DateOnly UpdatedOn { get; set; }
    public DateOnly SourceAuditDate { get; set; }
    public string? Purpose { get; set; }
    public string? RankingMeaning { get; set; }
}

public sealed class Investigation
{
    public string Id { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Doi { get; set; }
    public string FuelFamily { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    // Source CSV rows are not independent experiment counts.
    public int CsvRowCount { get; set; }
    public string DataAvailability { get; set; } = string.Empty;
    public string? LocalMetadataPath { get; set; }
    public string? LocalCsvPath { get; set; }
    public string? LocalCsvSha256 { get; set; }
    public string? CurationStatus { get; set; }

    public ICollection<InvestigationTopic> TopicLinks { get; set; } = [];
    public ICollection<InvestigationMaterial> MaterialLinks { get; set; } = [];
    public ICollection<InvestigationVariable> VariableLinks { get; set; } = [];
    public ICollection<InvestigationMeasurementType> MeasurementTypeLinks { get; set; } = [];
    public ICollection<CsvField> CsvFields { get; set; } = [];
    public ICollection<Limitation> Limitations { get; set; } = [];
    public ICollection<Evidence> EvidenceItems { get; set; } = [];
    public ICollection<RelevanceReason> RelevanceReasons { get; set; } = [];
    public ICollection<DocumentInvestigation> DocumentLinks { get; set; } = [];
    public ICollection<PassageInvestigation> PassageLinks { get; set; } = [];
    public ICollection<FlexRecord> FlexRecords { get; set; } = [];
}

public sealed class ResearchTopic
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public CoverageGap? CoverageGap { get; set; }
    public ICollection<InvestigationTopic> InvestigationLinks { get; set; } = [];
    public ICollection<RelevanceReason> RelevanceReasons { get; set; } = [];
}

public sealed class CoverageGap
{
    public string TopicId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public ResearchTopic Topic { get; set; } = null!;
}

public sealed class InvestigationTopic
{
    public string InvestigationId { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public int Position { get; set; }

    public Investigation Investigation { get; set; } = null!;
    public ResearchTopic Topic { get; set; } = null!;
}

public sealed class Material
{
    public string Name { get; set; } = string.Empty;
    public ICollection<InvestigationMaterial> InvestigationLinks { get; set; } = [];
}

public sealed class InvestigationMaterial
{
    public string InvestigationId { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public int Position { get; set; }

    public Investigation Investigation { get; set; } = null!;
    public Material Material { get; set; } = null!;
}

public sealed class Variable
{
    public string Name { get; set; } = string.Empty;
    public ICollection<InvestigationVariable> InvestigationLinks { get; set; } = [];
}

public sealed class InvestigationVariable
{
    public string InvestigationId { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    public int Position { get; set; }

    public Investigation Investigation { get; set; } = null!;
    public Variable Variable { get; set; } = null!;
}

public sealed class MeasurementType
{
    public string Name { get; set; } = string.Empty;
    public ICollection<InvestigationMeasurementType> InvestigationLinks { get; set; } = [];
}

public sealed class InvestigationMeasurementType
{
    public string InvestigationId { get; set; } = string.Empty;
    public string MeasurementTypeName { get; set; } = string.Empty;
    public int Position { get; set; }

    public Investigation Investigation { get; set; } = null!;
    public MeasurementType MeasurementType { get; set; } = null!;
}

public sealed class CsvField
{
    public string InvestigationId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Name { get; set; } = string.Empty;

    public Investigation Investigation { get; set; } = null!;
}

public sealed class Limitation
{
    public string InvestigationId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;

    public Investigation Investigation { get; set; } = null!;
}

public sealed class Evidence
{
    public string Id { get; set; } = string.Empty;
    public string InvestigationId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    public string? DocumentId { get; set; }
    public int? PdfPage { get; set; }

    public Investigation Investigation { get; set; } = null!;
    public KnowledgeDocument? Document { get; set; }
    public ICollection<RelevanceReasonEvidence> RelevanceReasonLinks { get; set; } = [];
}

public sealed class RelevanceReason
{
    public string InvestigationId { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    public Investigation Investigation { get; set; } = null!;
    public ResearchTopic Topic { get; set; } = null!;
    public ICollection<RelevanceReasonEvidence> EvidenceLinks { get; set; } = [];
}

public sealed class RelevanceReasonEvidence
{
    public string InvestigationId { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public string EvidenceId { get; set; } = string.Empty;

    public RelevanceReason Reason { get; set; } = null!;
    public Evidence Evidence { get; set; } = null!;
}

public sealed class KnowledgeDocument
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;

    public ICollection<Passage> Passages { get; set; } = [];
    public ICollection<DocumentInvestigation> InvestigationLinks { get; set; } = [];
    public ICollection<Evidence> EvidenceItems { get; set; } = [];
}

public sealed class DocumentInvestigation
{
    public string DocumentId { get; set; } = string.Empty;
    public string InvestigationId { get; set; } = string.Empty;

    public KnowledgeDocument Document { get; set; } = null!;
    public Investigation Investigation { get; set; } = null!;
}

public sealed class Passage
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int PdfPage { get; set; }
    public string? PrintedPage { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? ExtractionStatus { get; set; }

    public KnowledgeDocument Document { get; set; } = null!;
    public ICollection<PassageInvestigation> InvestigationLinks { get; set; } = [];
}

public sealed class PassageInvestigation
{
    public string PassageId { get; set; } = string.Empty;
    public string InvestigationId { get; set; } = string.Empty;

    public Passage Passage { get; set; } = null!;
    public Investigation Investigation { get; set; } = null!;
}

public sealed class FlexRecord
{
    public string RecordId { get; set; } = string.Empty;
    public string InvestigationId { get; set; } = string.Empty;
    public string ReportTestId { get; set; } = string.Empty;
    public string? PsiTestNumberRaw { get; set; }
    public string? CirIdentifier { get; set; }
    public DateOnly? TestDate { get; set; }
    public TimeOnly? TestTimeGmt { get; set; }
    public string Fuel { get; set; } = string.Empty;

    public double? PressureMmHg { get; set; }
    public double? InitialNominalMoleFractionO2 { get; set; }
    public double? InitialNominalMoleFractionN2 { get; set; }
    public double? InitialNominalMoleFractionCO2 { get; set; }
    public double? InitialNominalMoleFractionHe { get; set; }
    public double? InitialDiameterMm { get; set; }
    public double? VisibleExtinctionDiameterMm { get; set; }
    public double? BurningRateConstantMm2S { get; set; }
    public double? BurnTimeS { get; set; }

    public string? TestEndRaw { get; set; }
    public string? SupportFiber { get; set; }
    public double? DriftVelocityMmS { get; set; }
    public double? AmbientTemperatureK { get; set; }
    public string? VerificationStatus { get; set; }
    public string? ComparisonReadiness { get; set; }
    public string? MissingNumericReason { get; set; }

    public string? SourceReportUrl { get; set; }
    public string? SourcePsiUrl { get; set; }
    public int? PdfPage { get; set; }
    public string? PrintedPage { get; set; }
    public string? TableLabel { get; set; }
    public int? CsvRecordIndex { get; set; }
    public int? CsvPhysicalLine { get; set; }
    public string? BurningRateUnitCorrectionBasis { get; set; }
    public string? SourceCsvSha256 { get; set; }

    public Investigation Investigation { get; set; } = null!;
    public ICollection<FlexQualityFlag> QualityFlags { get; set; } = [];
    public ICollection<FlexRawPsiField> RawPsiFields { get; set; } = [];
}

public sealed class FlexQualityFlag
{
    public string FlexRecordId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Value { get; set; } = string.Empty;

    public FlexRecord FlexRecord { get; set; } = null!;
}

public sealed class FlexRawPsiField
{
    public string FlexRecordId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;

    public FlexRecord FlexRecord { get; set; } = null!;
}
