using OrbitFlame.Api.Contracts;

namespace OrbitFlame.Api.Services;

public sealed class ResearchService(ICatalogRepository catalog)
{
    public static readonly Dictionary<string, string> Topics = new()
    {
        ["all"] = "All research",
        ["material_flammability"] = "Material flammability",
        ["suppression"] = "Fire suppression",
        ["smoke_formation"] = "Smoke formation",
        ["detection"] = "Fire detection"
    };
    public static bool ValidTopic(string topic) => Topics.ContainsKey(topic);

    public RankedStudy[] Rank(string topic, string family, string query, string material, string variable)
    {
        var source = catalog.Data.Investigations.AsEnumerable();
        if (!string.IsNullOrEmpty(family)) source = source.Where(s => s.FuelFamily == family);
        if (!string.IsNullOrWhiteSpace(material)) source = source.Where(s => s.Materials.Any(m => Contains(m, material)));
        if (!string.IsNullOrWhiteSpace(query)) source = source.Where(s => Contains(string.Join(' ',
            new[] { s.ShortName, s.Title, s.Summary }.Concat(s.Materials).Concat(s.Variables)), query));
        // Detection is an explicit coverage gap, not an inferred use of SPICE.
        if (topic == "detection") source = source.Where(s => s.ResearchTopics.Contains(topic));
        return source.Select(s => new
        {
            Study = s,
            Match = topic == "all" || s.ResearchTopics.Contains(topic),
            VariableMatch = !string.IsNullOrWhiteSpace(variable) && s.Variables.Any(v => Contains(v, variable))
        })
        .OrderByDescending(x => x.Match).ThenByDescending(x => x.VariableMatch)
        .ThenBy(x => x.Study.ShortName, StringComparer.Ordinal)
        .Select(x => new RankedStudy(x.Study, x.Match, Reasons(x.Study, topic, x.VariableMatch))).ToArray();
    }

    private static bool Contains(string value, string query) => value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string[] Reasons(Study study, string topic, bool variableMatch)
    {
        var reasons = new List<string>();
        if (topic == "all") reasons.Add("Browse this investigation's objectives and available evidence.");
        else if (study.RelevanceReasons.TryGetValue(topic, out var reason)) reasons.Add(reason.Text);
        else reasons.Add("Related context; this study does not directly target the selected topic.");
        if (variableMatch) reasons.Add("Studies a variable matching your interest.");
        return reasons.ToArray();
    }

    public Study[] Select(SelectionRequest request)
    {
        var ids = request.InvestigationIds;
        if (ids is null || ids.Length < 2 || ids.Length > 3 || ids.Any(string.IsNullOrWhiteSpace)
            || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Select two or three distinct investigations.");
        if (!ValidTopic(request.ResearchTopic ?? "all")) throw new ArgumentException("Unknown research topic.");
        return ids.Select(id => catalog.Find(id) ?? throw new ArgumentException($"Unknown investigation: {id}")).ToArray();
    }

    public Comparison Compare(Study[] studies, string topic) => new(studies, topic,
    [
        new("Research objective", studies.Select(s => new[] { s.Summary }).ToArray()),
        new("Fuel family", studies.Select(s => new[] { s.FuelFamily.Replace('_', ' ') }).ToArray()),
        new("Materials / fuels", studies.Select(s => s.Materials).ToArray()),
        new("Variables studied", studies.Select(s => s.Variables).ToArray()),
        new("Measurements described by the study", studies.Select(s => s.MeasurementTypes).ToArray()),
        new("Fields in the downloaded CSV", studies.Select(s => s.CsvFields).ToArray()),
        new("Source CSV records", studies.Select(s => new[] { s.CsvRowCount.ToString() + " rows (not independent-test counts)" }).ToArray()),
        new("Interpretation limits", studies.Select(s => s.Limitations).ToArray())
    ],
    ["This is a comparison of research scope and available evidence, not a pooled experimental analysis.",
     "Different quantities and experimental conditions cannot be treated as equivalent."]);
}
