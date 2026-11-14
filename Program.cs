using System.Threading.RateLimiting;
using OrbitFlame.Api.Contracts;
using OrbitFlame.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
// Parse only known local settings. Never execute .env content or log values.
var envPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env");
if (File.Exists(envPath))
{
    var local = new Dictionary<string, string?>();
    foreach (var raw in File.ReadLines(envPath))
    {
        var line = raw.Trim(); if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue; var equal = line.IndexOf('='); if (equal < 1) continue;
        var name = line[..equal].Trim(); var value = line[(equal + 1)..].Trim();
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
        var setting = name switch { "Gemini_API" or "GEMINI_API_KEY" or "Gemini__ApiKey" => "Gemini:ApiKey", "GEMINI_MODEL" or "Gemini__Model" => "Gemini:Model", "groq" or "GROQ_API_KEY" or "Groq__ApiKey" => "Groq:ApiKey", "GROQ_MODEL" or "Groq__Model" => "Groq:Model", "LLM_PROVIDER" => "Llm:Provider", _ => null };
        if (setting is not null) local[setting] = value;
    }
    builder.Configuration.AddInMemoryCollection(local);
}
builder.Configuration.AddEnvironmentVariables();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16_384);
builder.Services.AddSingleton<ICatalogRepository, CatalogRepository>();
builder.Services.AddSingleton<ResearchService>();
builder.Services.AddSingleton<DataPaths>();
builder.Services.AddSingleton<DatasetService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddSingleton<PageService>();
builder.Services.AddHttpClient<ExplanationService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("explanations", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = Math.Clamp(builder.Configuration.GetValue("Generation:RequestsPerMinute", 12), 1, 240), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
// Fail early if the published catalog or its provenance is invalid.
_ = app.Services.GetRequiredService<ICatalogRepository>();
_ = app.Services.GetRequiredService<KnowledgeService>();
app.UseCors();
app.UseRateLimiter();
// Private configuration is never a public resource, even if copied into wwwroot by mistake.
app.Use(async (context, next) =>
{
    var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
    if (segments.Any(segment => segment.Equals(".git", StringComparison.OrdinalIgnoreCase)
        || segment.Equals(".env", StringComparison.OrdinalIgnoreCase)
        || segment.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
        || segment.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && segment.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", (ICatalogRepository catalog, KnowledgeService knowledge) => Results.Ok(new
{ status = "ok", catalogVersion = catalog.Data.Version, studies = catalog.Data.Investigations.Length, retrieval = knowledge.RetrievalStatus }));
app.MapGet("/api/documents", (KnowledgeService knowledge) => Results.Ok(new
{ documents = knowledge.Data.Documents.Select(d => new { d.Id, d.Title, d.InvestigationIds, d.SourceUrl }), passages = knowledge.Data.Passages.Length }));
app.MapGet("/api/documents/{id}/file", (string id, KnowledgeService knowledge, DataPaths paths) =>
{
    var doc = knowledge.Data.Documents.FirstOrDefault(d => d.Id == id);
    return doc is null ? Results.NotFound() : Results.File(paths.Document(doc.FileName),
        "application/pdf", enableRangeProcessing: true);
});
app.MapGet("/api/meta", (ICatalogRepository catalog) => Results.Ok(new
{
    topics = ResearchService.Topics.Select(t => new { id = t.Key, label = t.Value }),
    catalog.Data.SourceAuditDate,
    catalog.Data.UpdatedOn,
    catalog.Data.CoverageGaps,
    rankingMeaning = "Relevance to your research need, not a fire-danger score."
}));
app.MapGet("/api/investigations", async (string? topic, string? fuelFamily, string? q, string? material, string? variable,
    ResearchService research, ICatalogRepository catalog, KnowledgeService knowledge, CancellationToken cancellationToken) =>
{
    topic ??= "all";
    if (!ResearchService.ValidTopic(topic) || (fuelFamily is not null and not ("" or "solid" or "droplet" or "gas_jet" or "heated_materials"))
        || new[] { q, material, variable }.Any(s => s?.Length > 1000))
        return Results.BadRequest(new { error = "Invalid topic, fuel family, or search value (maximum 1000 characters)." });
    var rows = await research.Rank(topic, fuelFamily ?? "", q ?? "", material ?? "", variable ?? "", cancellationToken);
    return Results.Ok(new
    {
        investigations = rows,
        total = rows.Length,
        totalCatalog = catalog.Data.Investigations.Length,
        sourceAuditDate = catalog.Data.SourceAuditDate,
        coverageGaps = catalog.Data.CoverageGaps,
        retrieval = knowledge.RetrievalStatus
    });
});
app.MapGet("/api/datasets", (DatasetService datasets) => Results.Ok(new { datasets = datasets.Summary }));
app.MapGet("/api/datasets/{id}", (string id, DatasetService datasets) => datasets.Find(id) is { } dataset
    ? Results.Ok(new { dataset, assets = datasets.Assets(id) }) : Results.NotFound(new { error = "Dataset not found." }));
app.MapGet("/api/datasets/{id}/assets/{assetId}", (string id, string assetId, DatasetService datasets) => datasets.Asset(id,assetId) is { } file
    ? Results.File(file.Path, "application/octet-stream", file.Name, enableRangeProcessing:true) : Results.NotFound(new { error = "Original asset is not available locally." }));
app.MapGet("/api/investigations/{id}", (string id, ICatalogRepository catalog) =>
    catalog.Find(id) is { } study ? Results.Ok(study) : Results.NotFound(new { error = "Investigation not found." }));
app.MapGet("/api/experiments/flex", (ICatalogRepository catalog) => Results.Ok(new
{
    records = catalog.FlexSample,
    scope = "12 source-checked transcriptions. Descriptive only; unresolved conditions prevent controlled comparisons."
}));
app.MapPost("/api/compare", (SelectionRequest request, ResearchService research) =>
{
    try { return Results.Ok(research.Compare(research.Select(request), request.ResearchTopic ?? "all")); }
    catch (ArgumentException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.MapPost("/api/explain", async (SelectionRequest request, ResearchService research,
    ExplanationService explanation, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await explanation.Explain(research.Select(request), request.ResearchTopic ?? "all", cancellationToken)); }
    catch (ArgumentException error) { return Results.BadRequest(new { error = error.Message }); }
}).RequireRateLimiting("explanations");
app.MapPost("/api/ask", async (QuestionRequest request, ICatalogRepository catalog,
    ExplanationService explanation, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Trim().Length < 8 || request.Question.Length > 1000
        || (request.InvestigationIds is { } ids && (ids.Length > 3 || ids.Any(id => catalog.Find(id) is null) || ids.Distinct().Count() != ids.Length)))
        return Results.BadRequest(new { error = "Enter a question of 8–1000 characters and up to three known study IDs." });
    return Results.Ok(await explanation.Ask(request.Question.Trim(), request.InvestigationIds ?? [], cancellationToken));
}).RequireRateLimiting("explanations");
app.MapGet("/api/documents/{id}/pages/{page:int}", (string id, int page, PageService pages, DataPaths paths) => pages.Exists(id, page) ? Results.File(paths.Page(id, page), "application/json") : Results.NotFound());
app.MapGet("/api/documents/{id}/pages/{page:int}/image", async (string id, int page, PageService pages, CancellationToken cancellationToken) =>
{
    if (!pages.Exists(id, page)) return Results.NotFound();
    try { return Results.File(await pages.Render(id, page, cancellationToken), "image/png"); }
    catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or OperationCanceledException) { return Results.Problem("The original page image is unavailable. Use the original PDF link.", statusCode: 503); }
});
app.MapGet("/api/figures/{id}/image", (string id, KnowledgeService knowledge, DataPaths paths) =>
{
    var figure = (knowledge.Data.Figures ?? []).FirstOrDefault(f => f.Id == id);
    return figure is not null && File.Exists(paths.Figure(figure.FileName)) ? Results.File(paths.Figure(figure.FileName), "image/png") : Results.NotFound();
});
// Static Flutter release files may be placed in wwwroot for one-origin hosting.
app.MapFallback((HttpContext context) =>
{
    if (context.Request.Path.StartsWithSegments("/api")) return Results.NotFound(new { error = "API route not found." });
    var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    return File.Exists(index) ? Results.File(index, "text/html") : Results.NotFound();
});
app.Run();
