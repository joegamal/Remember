using System.Text.Json;

namespace OrbitFlame.Api.Contracts;

public sealed record ApiError(string Error);
public sealed record HealthResponse(string Status, int CatalogVersion, int Studies);
public sealed record TopicSummary(string Id, string Label);
public sealed record MetaResponse(TopicSummary[] Topics, string SourceAuditDate, string UpdatedOn,
    CoverageGap[] CoverageGaps, string RankingMeaning);
public sealed record InvestigationListResponse(RankedStudy[] Investigations, int Total, int TotalCatalog,
    string SourceAuditDate, CoverageGap[] CoverageGaps);
public sealed record DocumentSummary(string Id, string Title, string[] InvestigationIds, string SourceUrl);
public sealed record DocumentListResponse(DocumentSummary[] Documents, int Passages);
public sealed record FlexSampleResponse(JsonElement[] Records, string Scope);
public sealed record Evidence(string Id, string Title, string Url, string Location, string Text,
    string? DocumentId = null, int? PdfPage = null);
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
public sealed record RankedStudy(Study Study, bool TopicMatch, string[] Reasons);
public sealed record SelectionRequest(string[]? InvestigationIds, string? ResearchTopic);
public sealed record ComparisonRow(string Label, string[][] Values);
public sealed record Comparison(Study[] Investigations, string ResearchTopic, ComparisonRow[] Rows, string[] Notes);
public sealed record ExplanationSection(string Heading, string Text, string[] EvidenceIds);
public sealed record Explanation(string Mode, string Status, ExplanationSection[] Sections, Evidence[] Sources);
public sealed record QuestionRequest(string? Question, string[]? InvestigationIds);
public sealed record KnowledgeDocument(string Id, string Title, string[] InvestigationIds, string SourceUrl, string FileName, string Sha256);
public sealed record Passage(string Id, string DocumentId, string Title, string[] InvestigationIds,
    int PdfPage, string? PrintedPage, string Url, string Text);
public sealed record Knowledge(int Version, string Method, KnowledgeDocument[] Documents, Passage[] Passages);
