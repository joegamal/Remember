using System.Text.Json;
using OrbitFlame.Api.Contracts;

namespace OrbitFlame.Api.Services;

public interface ICatalogRepository
{
    Catalog Data { get; }
    Study? Find(string id);
    JsonElement[] FlexSample { get; }
}

public sealed class CatalogRepository : ICatalogRepository
{
    public Catalog Data { get; }
    public JsonElement[] FlexSample { get; }

    public CatalogRepository()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var directory = Path.Combine(AppContext.BaseDirectory, "Data");
        Data = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(Path.Combine(directory, "investigations.json")), options)
            ?? throw new InvalidDataException("Catalog is empty.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var evidenceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var study in Data.Investigations)
        {
            if (string.IsNullOrWhiteSpace(study.Id) || !ids.Add(study.Id) || study.Evidence.Length == 0)
                throw new InvalidDataException("Catalog identity/evidence is invalid.");
            foreach (var evidence in study.Evidence)
            {
                if (!evidenceIds.Add(evidence.Id) || !Uri.TryCreate(evidence.Url, UriKind.Absolute, out var uri)
                    || uri.Scheme != "https" || uri.Host is not ("psi.nasa.gov" or "ntrs.nasa.gov"))
                    throw new InvalidDataException("Evidence URL or identifier is invalid.");
            }
            if (study.RelevanceReasons.Values.Any(r => r.EvidenceIds.Any(id => !study.Evidence.Any(e => e.Id == id))))
                throw new InvalidDataException("Relevance evidence is missing.");
        }
        FlexSample = JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(Path.Combine(directory, "flex_verified_sample.json")))
            ?? throw new InvalidDataException("FLEX sample is empty.");
    }

    public Study? Find(string id) => Data.Investigations.FirstOrDefault(s => s.Id == id);
}
