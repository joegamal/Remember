namespace OrbitFlame.Api.Services;

public sealed class DataPaths
{
    public string Root { get; }
    public DataPaths(IConfiguration config, IHostEnvironment env)
    {
        Root = Path.GetFullPath(config["Data:Root"] ?? Path.Combine(env.ContentRootPath, "..", "..", "data"));
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("Set Data__Root to the prepared data directory.");
    }
    public string Knowledge => Path.Combine(Root, "processed", "knowledge.json");
    public string Document(string name) => Path.Combine(Root, "raw", name);
    public string Page(string id, int page) => Path.Combine(Root, "processed", "pages", id, $"{page}.json");
    public string Figure(string name) => Path.Combine(Root, "processed", "figures", name);
}
