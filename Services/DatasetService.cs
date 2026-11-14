using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace OrbitFlame.Api.Services;

public sealed class DatasetService
{
    private readonly DataPaths _paths;
    private readonly JsonElement _data;
    private readonly ConcurrentDictionary<string,(long Length,DateTime Modified,bool Valid)> _verified = new();
    public DatasetService(DataPaths paths)
    {
        _paths = paths;
        var file = Path.Combine(paths.Root, "processed", "datasets.json");
        _data = File.Exists(file) ? JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file)) : JsonSerializer.Deserialize<JsonElement>("{\"datasets\":[],\"assets\":[]}");
    }
    public JsonElement? Find(string id) => _data.GetProperty("datasets").EnumerateArray().Where(d => d.GetProperty("id").GetString() == id).Select(d => (JsonElement?)d).FirstOrDefault();
    public object Summary => _data.GetProperty("datasets").EnumerateArray().Select(d => new { id=d.GetProperty("id").GetString(), name=d.GetProperty("study").GetString(), rows=d.GetProperty("rows").GetInt32(), notes=d.GetProperty("notes"), sourceUrl=d.GetProperty("source_url").GetString() }).ToArray();
    private IEnumerable<JsonElement> RawAssets(string id) => _data.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("accession").GetString() == id);
    public JsonElement[] Assets(string id) => RawAssets(id).Select(asset => {
        var node=JsonNode.Parse(asset.GetRawText())!;
        node["available"]=VerifiedAsset(asset) is not null;
        return JsonSerializer.SerializeToElement(node);
    }).ToArray();
    public (string Path,string Name)? Asset(string id, string assetId)
    {
        var asset=RawAssets(id).FirstOrDefault(a => a.GetProperty("id").GetString()==assetId);
        return asset.ValueKind==JsonValueKind.Undefined ? null : VerifiedAsset(asset);
    }
    private (string Path,string Name)? VerifiedAsset(JsonElement asset)
    {
        if(!asset.TryGetProperty("localFile",out var local) || !asset.TryGetProperty("sha256",out var hash) || hash.ValueKind!=JsonValueKind.String)return null;
        var root=Path.GetFullPath(Path.Combine(_paths.Root,"raw"))+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(Path.Combine(_paths.Root,local.GetString()!));
        if(!path.StartsWith(root,StringComparison.Ordinal) || !File.Exists(path))return null;
        var info=new FileInfo(path);
        if(info.Length!=asset.GetProperty("expected_bytes").GetInt64())return null;
        if(!_verified.TryGetValue(path,out var cached) || cached.Length!=info.Length || cached.Modified!=info.LastWriteTimeUtc)
        {
            using var stream=File.OpenRead(path);
            cached=(info.Length,info.LastWriteTimeUtc,Convert.ToHexString(SHA256.HashData(stream)).Equals(hash.GetString(),StringComparison.OrdinalIgnoreCase));
            _verified[path]=cached;
        }
        return cached.Valid ? (path,asset.GetProperty("file_name").GetString()!) : null;
    }
}
