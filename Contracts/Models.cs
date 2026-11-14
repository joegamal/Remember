namespace OrbitFlame.Api.Contracts;

public sealed record Evidence(string Id, string Title, string Url, string Location, string Text,
    string? DocumentId = null, int? PdfPage = null, Region[]? Regions = null, Figure[]? Figures = null, string? DatasetId = null, int? SourceRow = null, string? AssetId = null);
public sealed record RelevanceReason(string Text, string[] EvidenceIds);
public sealed record CoverageGap(string Topic, string Message);
public sealed record Study
{
    public string Id { get; init; } = "";
    public string ShortName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Doi { get; init; } = "";
    public string FuelFamily { get; init; } = "";
    public string[] ResearchTopics { get; init; } = [];
    public string Summary { get; init; } = "";
    public string[] Materials { get; init; } = [];
    public string[] Variables { get; init; } = [];
    public string[] MeasurementTypes { get; init; } = [];
    public string[] CsvFields { get; init; } = [];
    public int CsvRowCount { get; init; }
    public string DataAvailability { get; init; } = "";
    public Dictionary<string, RelevanceReason> RelevanceReasons { get; init; } = [];
    public string[] Limitations { get; init; } = [];
    public Evidence[] Evidence { get; init; } = [];
}
public sealed record Catalog
{
    public int Version { get; init; }
    public string UpdatedOn { get; init; } = "";
    public string SourceAuditDate { get; init; } = "";
    public CoverageGap[] CoverageGaps { get; init; } = [];
    public Study[] Investigations { get; init; } = [];
}
public sealed record RankedStudy(Study Study, bool TopicMatch, string[] Reasons, Evidence[]? Matches = null, double? RetrievalScore = null);
public sealed record SelectionRequest(string[]? InvestigationIds, string? ResearchTopic);
public sealed record ComparisonRow(string Label, string[][] Values);
public sealed record Comparison(Study[] Investigations, string ResearchTopic, ComparisonRow[] Rows, string[] Notes);
public sealed record EvidenceSupport(string EvidenceId, string Quote);
public sealed record ExplanationSection(string Heading, string Text, string[] EvidenceIds, string[]? FigureIds = null, EvidenceSupport[]? Supports = null);
public sealed record Explanation(string Mode, string Status, ExplanationSection[] Sections, Evidence[] Sources);
public sealed record QuestionRequest(string? Question, string[]? InvestigationIds);
public sealed record KnowledgeDocument(string Id, string Title, string[] InvestigationIds, string SourceUrl, string FileName, string Sha256);
public sealed record Passage(string Id, string DocumentId, string Title, string[] InvestigationIds,
    int PdfPage, string? PrintedPage, string Url, string Text, Region[]? Regions = null, string[]? FigureIds = null, string ContentKind = "body", string? DatasetId = null, int? SourceRow = null, string? AssetId = null);
public sealed record Knowledge(int Version, string Method, KnowledgeDocument[] Documents, Passage[] Passages, Figure[]? Figures = null);

public sealed record Region(double X, double Y, double Width, double Height);
public sealed record Figure(string Id, string DocumentId, int PdfPage, string Caption, Region Region, string FileName, string Kind, string ReviewStatus);
public sealed record SearchHit(Evidence Evidence, string[] InvestigationIds, double Score, double? LexicalScore, double? SemanticScore);
