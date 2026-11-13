using System.Text.Json;
using System.Text.RegularExpressions;
using OrbitFlame.Api.Contracts;

namespace OrbitFlame.Api.Services;

public sealed partial class KnowledgeService
{
    public Knowledge Data { get; }
    private readonly Dictionary<string, int>[] _terms;
    private readonly double _averageLength;
    private static readonly HashSet<string> StopWords = new("a an the and or of to in on for is are was were how what why does do did it this that with from as by be can has have its which at about explain compare research study studies nasa investigation investigations".Split(' '));

    public KnowledgeService()
    {
        Data = JsonSerializer.Deserialize<Knowledge>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "knowledge.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("Knowledge is missing.");
        if (Data.Passages.Length == 0 || Data.Passages.Select(p => p.Id).Distinct().Count() != Data.Passages.Length)
            throw new InvalidDataException("Knowledge passages are empty or duplicated.");
        foreach (var document in Data.Documents)
        {
            if (Path.GetFileName(document.FileName) != document.FileName) throw new InvalidDataException("Invalid document path.");
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "Documents", document.FileName);
            using var stream = File.OpenRead(path);
            if (!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).Equals(document.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Knowledge document checksum mismatch.");
        }
        _terms = Data.Passages.Select(p => Tokens(p.Text).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count())).ToArray();
        _averageLength = _terms.Average(t => t.Values.Sum());
    }

    public Evidence[] Search(string query, string[]? studyIds = null, int limit = 5)
    {
        var queryTerms = Tokens(query).Distinct().ToArray();
        if (queryTerms.Length == 0) return [];
        var frequencies = queryTerms.ToDictionary(term => term, term => _terms.Count(doc => doc.ContainsKey(term)));
        return Data.Passages.Select((passage, index) => new { passage, index })
            .Where(x => studyIds is null || studyIds.Length == 0 || x.passage.InvestigationIds.Intersect(studyIds).Any())
            .Select(x => new
            {
                Passage = x.passage,
                Score = queryTerms.Sum(term =>
                {
                    var frequency = _terms[x.index].GetValueOrDefault(term);
                    var idf = Math.Log(1 + (Data.Passages.Length - frequencies[term] + .5) / (frequencies[term] + .5));
                    return idf * frequency * 2.2 / (frequency + 1.2 * (.25 + .75 * _terms[x.index].Values.Sum() / _averageLength));
                })
            }).Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Passage.Id, StringComparer.Ordinal)
            .Take(limit).Select(x => new Evidence(x.Passage.Id, x.Passage.Title, x.Passage.Url,
                $"PDF page {x.Passage.PdfPage}" + (x.Passage.PrintedPage is { } printed ? $" / printed page {printed}" : ""),
                x.Passage.Text, x.Passage.DocumentId, x.Passage.PdfPage)).ToArray();
    }

    private static IEnumerable<string> Tokens(string text) => WordPattern().Matches(text.ToLowerInvariant())
        .Select(m => m.Value).Where(t => t.Length > 1 && !StopWords.Contains(t));

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
