using System.ComponentModel;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OrbitFlame.Api.Contracts;
using OrbitFlame.Api.Data;
using OrbitFlame.Api.Services;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16_384);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrbitFlame")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=OrbitFlame;Trusted_Connection=True;TrustServerCertificate=True"));

builder.Services.AddSingleton<ICatalogRepository, CatalogRepository>();
builder.Services.AddSingleton<ResearchService>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddHttpClient<ExplanationService>();


builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("explanations", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "OrbitFlame API",
    Version = "v1",
    Description = "Explore, compare, and ask evidence-grounded questions about the NASA FLEX, BASS, BASS-II, "
        + "and SPICE combustion investigations. Relevance ranking measures fit to a research topic, not fire danger. "
        + "AI routes return labeled retrieved or curated evidence when no model provider is configured."
}));

var app = builder.Build();
// Fail early if the published catalog or its provenance is invalid.
_ = app.Services.GetRequiredService<ICatalogRepository>();
_ = app.Services.GetRequiredService<KnowledgeService>();
app.UseCors();
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "OrbitFlame API v1");
    options.DocumentTitle = "OrbitFlame API documentation";
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", (ICatalogRepository catalog) => Results.Ok(new HealthResponse(
        "ok", catalog.Data.Version, catalog.Data.Investigations.Length)))
    .WithTags("System")
    .WithName("GetHealth")
    .WithSummary("Check service health")
    .WithDescription("Returns the API status, catalog version, and number of investigations in the catalog.")
    .Produces<HealthResponse>(StatusCodes.Status200OK);
app.MapGet("/api/documents", (KnowledgeService knowledge) => Results.Ok(new DocumentListResponse(
        knowledge.Data.Documents.Select(d => new DocumentSummary(d.Id, d.Title, d.InvestigationIds, d.SourceUrl)).ToArray(),
        knowledge.Data.Passages.Length)))
    .WithTags("Documents")
    .WithName("ListDocuments")
    .WithSummary("List indexed reports")
    .WithDescription("Returns metadata for the reports indexed for passage retrieval, plus the total passage count. "
        + "BASS has catalog evidence but no indexed report.")
    .Produces<DocumentListResponse>(StatusCodes.Status200OK);
app.MapGet("/api/documents/{id}/file", ([Description("Document identifier from GET /api/documents.")] string id,
        KnowledgeService knowledge) =>
    {
        var doc = knowledge.Data.Documents.FirstOrDefault(d => d.Id == id);
        return doc is null ? Results.NotFound() : Results.File(Path.Combine(AppContext.BaseDirectory, "Data", "Documents", doc.FileName),
            "application/pdf", enableRangeProcessing: true);
    })
    .WithTags("Documents")
    .WithName("DownloadDocument")
    .WithSummary("Download an indexed report PDF")
    .WithDescription("Streams the original NASA report PDF. HTTP range requests are supported for page-level citations.")
    .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
    .Produces(StatusCodes.Status404NotFound);
app.MapGet("/api/meta", (ICatalogRepository catalog) => Results.Ok(new MetaResponse(
        ResearchService.Topics.Select(t => new TopicSummary(t.Key, t.Value)).ToArray(),
        catalog.Data.SourceAuditDate, catalog.Data.UpdatedOn, catalog.Data.CoverageGaps,
        "Relevance to your research need, not a fire-danger score.")))
    .WithTags("Catalog")
    .WithName("GetMetadata")
    .WithSummary("Get research topics and catalog metadata")
    .WithDescription("Lists supported research topics, source audit and update dates, explicit coverage gaps, "
        + "and an explanation of what relevance ranking means.")
    .Produces<MetaResponse>(StatusCodes.Status200OK);
app.MapGet("/api/investigations", ([Description("Research topic: all, material_flammability, suppression, smoke_formation, or detection. Defaults to all.")] string? topic,
        [Description("Fuel family filter: solid, droplet, or gas_jet. Omit for all families.")] string? fuelFamily,
        [Description("Free-text search over short name, title, summary, materials, and variables. Maximum 120 characters.")] string? q,
        [Description("Material name filter. Maximum 120 characters.")] string? material,
        [Description("Variable name filter. Maximum 120 characters.")] string? variable,
        ResearchService research, ICatalogRepository catalog) =>
    {
        topic ??= "all";
        if (!ResearchService.ValidTopic(topic) || (fuelFamily is not null and not ("" or "solid" or "droplet" or "gas_jet"))
            || new[] { q, material, variable }.Any(s => s?.Length > 120))
            return Results.BadRequest(new ApiError("Invalid topic, fuel family, or search value (maximum 120 characters)."));
        var rows = research.Rank(topic, fuelFamily ?? "", q ?? "", material ?? "", variable ?? "");
        return Results.Ok(new InvestigationListResponse(rows, rows.Length, catalog.Data.Investigations.Length,
            catalog.Data.SourceAuditDate, catalog.Data.CoverageGaps));
    })
    .WithTags("Investigations")
    .WithName("ListInvestigations")
    .WithSummary("List and rank investigations")
    .WithDescription("Filters the catalog by research topic, fuel family, material, and variable, then ranks matches "
        + "by relevance to the selected topic and search terms. Relevance order is not a fire-danger score.")
    .Produces<InvestigationListResponse>(StatusCodes.Status200OK)
    .Produces<ApiError>(StatusCodes.Status400BadRequest);
app.MapGet("/api/investigations/{id}", ([Description("Investigation identifier, for example PSI-69.")] string id,
        ICatalogRepository catalog) =>
        catalog.Find(id) is { } study ? Results.Ok(study) : Results.NotFound(new ApiError("Investigation not found.")))
    .WithTags("Investigations")
    .WithName("GetInvestigation")
    .WithSummary("Get one investigation")
    .WithDescription("Returns a study with its evidence, relevance reasons, limitations, and data availability notes.")
    .Produces<Study>(StatusCodes.Status200OK)
    .Produces<ApiError>(StatusCodes.Status404NotFound);
app.MapGet("/api/experiments/flex", (ICatalogRepository catalog) => Results.Ok(new FlexSampleResponse(
        catalog.FlexSample,
        "12 source-checked transcriptions. Descriptive only; unresolved conditions prevent controlled comparisons.")))
    .WithTags("Experiments")
    .WithName("GetFlexSample")
    .WithSummary("Get the reviewed FLEX sample")
    .WithDescription("Returns the 12 reviewed FLEX records and a scope note. The rows are descriptive transcriptions, "
        + "not independent experimental sample sizes.")
    .Produces<FlexSampleResponse>(StatusCodes.Status200OK);
app.MapPost("/api/compare", (SelectionRequest request, ResearchService research) =>
    {
        try { return Results.Ok(research.Compare(research.Select(request), request.ResearchTopic ?? "all")); }
        catch (ArgumentException error) { return Results.BadRequest(new ApiError(error.Message)); }
    })
    .WithTags("Comparisons")
    .WithName("CompareInvestigations")
    .WithSummary("Compare two or three investigations")
    .WithDescription("Builds a deterministic, field-by-field comparison of two or three distinct known investigations. "
        + "The result compares research scope and available evidence; it does not pool experiments or predict outcomes.")
    .Produces<Comparison>(StatusCodes.Status200OK)
    .Produces<ApiError>(StatusCodes.Status400BadRequest);
app.MapPost("/api/explain", async (SelectionRequest request, ResearchService research,
        ExplanationService explanation, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await explanation.Explain(research.Select(request), request.ResearchTopic ?? "all", cancellationToken)); }
        catch (ArgumentException error) { return Results.BadRequest(new ApiError(error.Message)); }
    })
    .WithTags("Explanations")
    .WithName("ExplainComparison")
    .WithSummary("Explain a comparison")
    .WithDescription("Returns an explanation of the selected investigations. Generated sections are validated against "
        + "known evidence; when generation is unavailable the response is labeled retrieved, curated, or insufficient_evidence. "
        + "Rate-limited to 12 requests per minute per client IP.")
    .RequireRateLimiting("explanations")
    .Produces<Explanation>(StatusCodes.Status200OK)
    .Produces<ApiError>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status429TooManyRequests);
app.MapPost("/api/ask", async (QuestionRequest request, ICatalogRepository catalog,
        ExplanationService explanation, CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Trim().Length < 8 || request.Question.Length > 1000
            || (request.InvestigationIds is { } ids && (ids.Length > 3 || ids.Any(id => catalog.Find(id) is null) || ids.Distinct().Count() != ids.Length)))
            return Results.BadRequest(new ApiError("Enter a question of 8–1000 characters and up to three known study IDs."));
        return Results.Ok(await explanation.Ask(request.Question.Trim(), request.InvestigationIds ?? [], cancellationToken));
    })
    .WithTags("Explanations")
    .WithName("AskQuestion")
    .WithSummary("Ask an evidence question")
    .WithDescription("Retrieves relevant report passages and optionally generates an answer grounded in those passages. "
        + "Questions must be 8–1000 characters; up to three unique known study IDs may constrain retrieval. "
        + "Rate-limited to 12 requests per minute per client IP.")
    .RequireRateLimiting("explanations")
    .Produces<Explanation>(StatusCodes.Status200OK)
    .Produces<ApiError>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status429TooManyRequests);
// Static Flutter release files may be placed in wwwroot for one-origin hosting.
app.MapFallback((HttpContext context) =>
{
    if (context.Request.Path.StartsWithSegments("/api")) return Results.NotFound(new ApiError("API route not found."));
    var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    return File.Exists(index) ? Results.File(index, "text/html") : Results.NotFound();
});
app.Run();
