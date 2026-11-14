using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using OrbitFlame.Api.Contracts;
namespace OrbitFlame.Api.Services;

public sealed partial class KnowledgeService
{
    public Knowledge Data { get; }
    private readonly Dictionary<string, int>[] _terms; private readonly double _averageLength;
    private readonly EmbeddingService _embeddings; private readonly IConfiguration _config;
    private static readonly HashSet<string> StopWords = new("a an the and or of to in on for is are was were how what why does do did it this that with from as by be can has have its which at about explain compare research study studies nasa investigation investigations tell me please when where who will would should could might much many happens happen after before out into such these those than then".Split(' '));
    public KnowledgeService(DataPaths paths, EmbeddingService embeddings, IConfiguration config)
    {
        _embeddings = embeddings; _config = config;
        Data = JsonSerializer.Deserialize<Knowledge>(File.ReadAllText(paths.Knowledge), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("Prepare knowledge first.");
        if (Data.Passages.Length == 0 || Data.Passages.Select(p => p.Id).Distinct().Count() != Data.Passages.Length) throw new InvalidDataException("Empty or duplicated passages.");
        foreach (var doc in Data.Documents) { if (Path.GetFileName(doc.FileName) != doc.FileName) throw new InvalidDataException("Invalid document path."); using var file = File.OpenRead(paths.Document(doc.FileName)); if (!Convert.ToHexString(SHA256.HashData(file)).Equals(doc.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Document checksum mismatch. Rebuild knowledge."); }
        foreach (var figure in Data.Figures ?? []) { if (Path.GetFileName(figure.FileName) != figure.FileName || !Data.Documents.Any(d => d.Id == figure.DocumentId)) throw new InvalidDataException("Invalid figure provenance."); }
        _terms = Data.Passages.Select(p => Tokens(p.Text).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count())).ToArray(); _averageLength = _terms.Average(d => d.Values.Sum());
    }
    public string RetrievalMode => _embeddings.Status == "ready" ? "hybrid BM25 + MiniLM" : "BM25 (local semantic index unavailable)";
    public object RetrievalStatus => new { mode = _embeddings.Status == "ready" ? "hybrid" : "bm25", embeddingStatus = _embeddings.Status, embeddingModel = EmbeddingService.Model, dimensions = 384, fusion = "semantic_weighted_rrf", indexedPassages = _embeddings.Vectors.Count, totalPassages = Data.Passages.Length };
    public async Task<SearchHit[]> SearchAsync(string query, string[]? studyIds = null, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var terms = Tokens(query).Distinct().ToArray();
        var vector = await _embeddings.Encode(query, cancellationToken);
        var wantsFiles = terms.Any(t => t is "file" or "files" or "workbook" or "sheet" or "column" or "columns" or "channel" or "channels" or "xls" or "xlsx" or "csv" or "download");
        var wantsHypothesis = terms.Contains("hypothesis");
        var wantsRows = terms.Any(t => t is "row" or "rows" or "csv" or "record" or "records" or "test" or "tests" or "sample" or "samples" or "run" or "runs" || t.Any(char.IsDigit));
        double Weight(Passage p) => (p.ContentKind == "asset_metadata" && !wantsFiles ? .25 : 1)
            * (p.ContentKind == "table_row" && !wantsRows ? .6 : 1)
            * (wantsHypothesis && Regex.IsMatch(p.Text, @"\bHypothes(?:is|es)\b", RegexOptions.IgnoreCase) ? 1.35 : 1);
        // Contents/figure lists are navigational text, not experimental evidence.
        bool IsContents(Passage p) => p.PdfPage > 0 && Regex.IsMatch(p.Text, @"(?:\.{6,}|…{3,})");
        var dfs = terms.ToDictionary(t => t, t => _terms.Count(d => d.ContainsKey(t)));
        var candidates = Data.Passages.Select((p, i) => (p, i)).Where(x => (!IsContents(x.p) || terms.Any(t => t is "contents" or "index")) && (x.p.ContentKind != "references" || terms.Any(t => t is "references" or "bibliography")) && (studyIds is null || studyIds.Length == 0 || x.p.InvestigationIds.Intersect(studyIds).Any())).Select(x =>
        {
            var bm = terms.Sum(t => { var tf = _terms[x.i].GetValueOrDefault(t); var idf = Math.Log(1 + (Data.Passages.Length - dfs[t] + .5) / (dfs[t] + .5)); return idf * tf * 2.2 / (tf + 1.2 * (.25 + .75 * _terms[x.i].Values.Sum() / _averageLength)); });
            double? cosine = vector is not null && _embeddings.Vectors.TryGetValue(x.p.Id, out var v) ? vector.Zip(v, (a, b) => (double)a * b).Sum() : null;
            return (passage: x.p, bm, cosine);
        }).ToArray();
        var lex = candidates.Where(c => c.bm > 0 && (vector is null || c.cosine >= .20)).OrderByDescending(c => c.bm * Weight(c.passage)).ThenBy(c => c.passage.Id).Take(80).ToArray();
        var semantic = candidates.Where(c => c.cosine >= _config.GetValue("Retrieval:MinimumCosine", .28)).OrderByDescending(c => c.cosine * Weight(c.passage)).ThenBy(c => c.passage.Id).Take(80).ToArray();
        var scores = new Dictionary<string, double>();
        foreach (var (list, weight) in new[] { (lex, 1.0), (semantic, _config.GetValue("Retrieval:SemanticWeight", 4.0)) })
            for (var rank = 0; rank < list.Length; rank++)
                scores[list[rank].passage.Id] = scores.GetValueOrDefault(list[rank].passage.Id) + weight / (60 + rank + 1);
        // Rank agreement is useful, but weak common-word overlap must not flatten strong semantic matches.
        if (vector is not null)
            foreach (var c in candidates.Where(c => scores.ContainsKey(c.passage.Id)))
                scores[c.passage.Id] *= (.35 + .65 * Math.Clamp(((c.cosine ?? 0) - .2) / .4, 0, 1)) * Weight(c.passage);
        var ranked = candidates.Where(c => scores.ContainsKey(c.passage.Id)).OrderByDescending(c => scores[c.passage.Id]).ThenBy(c => c.passage.Id);
        var hits = new List<SearchHit>();
        foreach (var c in ranked)
        {
            if (hits.Any(h => h.Evidence.DocumentId == c.passage.DocumentId && h.Evidence.PdfPage == c.passage.PdfPage && Similarity(h.Evidence.Text, c.passage.Text) > .75)) continue;
            hits.Add(new(ToEvidence(c.passage), c.passage.InvestigationIds, scores[c.passage.Id], c.bm > 0 ? c.bm : null, c.cosine)); if (hits.Count >= limit) break;
        }
        // Explicit table row locators are exact provenance requests, not semantic guesses.
        var rowMatch = Regex.Match(query, @"\b(?:csv\s+)?(?:source\s+)?row\s*#?\s*(\d+)\b", RegexOptions.IgnoreCase);
        if (rowMatch.Success && int.TryParse(rowMatch.Groups[1].Value, out var sourceRow))
        {
            var aliases = new[] { ("SAME-R", "PSI-101"), ("BASS-II", "PSI-25"), ("FLEX", "PSI-69"), ("BASS", "PSI-26"), ("SPICE", "PSI-107"), ("SAME", "PSI-102") };
            var dataset = aliases.FirstOrDefault(a => Regex.IsMatch(query, @"\b" + Regex.Escape(a.Item1) + @"\b", RegexOptions.IgnoreCase) || query.Contains(a.Item2, StringComparison.OrdinalIgnoreCase)).Item2;
            var exact = Data.Passages.Where(p => p.DatasetId == dataset && p.SourceRow == sourceRow && (studyIds is null || studyIds.Length == 0 || p.InvestigationIds.Intersect(studyIds).Any())).Select(p => new SearchHit(ToEvidence(p), p.InvestigationIds, 1, null, null)).ToArray();
            if (dataset is not null && exact.Length == 0) return [];
            if (exact.Length > 0) return exact.Concat(hits.Where(h => !exact.Any(e => e.Evidence.Id == h.Evidence.Id))).Take(limit).ToArray();
        }
        return hits.ToArray();
    }
    public Evidence ToEvidence(Passage p)
    {
        var table=p.DatasetId is not null;
        var location=table ? (p.SourceRow is { } row ? $"Source table · row {row}" : "Original dataset asset") : p.DocumentId == "catalog" ? "Reviewed investigation metadata" : $"PDF page {p.PdfPage}" + (p.PrintedPage is { } n ? $" / printed page {n}" : "");
        return new(p.Id,p.Title,p.Url,location,p.Text,table || p.DocumentId=="catalog" ? null : p.DocumentId,p.PdfPage==0 ? null : p.PdfPage,p.Regions??[],(Data.Figures??[]).Where(f=>(p.FigureIds??[]).Contains(f.Id)).Take(4).ToArray(),p.DatasetId,p.SourceRow,p.AssetId);
    }
    public static IEnumerable<string> Tokens(string text) => WordPattern().Matches(text.ToLowerInvariant()).Select(m => m.Value.StartsWith("hypothes", StringComparison.Ordinal) ? "hypothesis" : m.Value).Where(t => t.Length > 1 && !StopWords.Contains(t));
    private static double Similarity(string a, string b) { var x = Tokens(a).ToHashSet(); var y = Tokens(b).ToHashSet(); return x.Intersect(y).Count() / (double)Math.Max(1, x.Union(y).Count()); }
    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
