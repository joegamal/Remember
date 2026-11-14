using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace OrbitFlame.Api.Services;
/// <summary>Normalized SentenceTransformer embeddings. Persistent local worker, no Gemini or remote inference.</summary>
public sealed class EmbeddingService : IDisposable
{
    private readonly IConfiguration _config; private readonly DataPaths _paths; private readonly ILogger<EmbeddingService> _logger;
    private readonly SemaphoreSlim _gate = new(1); private Process? _worker; private readonly Dictionary<string, float[]> _cache = new();
    public Dictionary<string, float[]> Vectors { get; } = [];
    public string Status { get; private set; } = "not_indexed";
    public const string Model = "sentence-transformers/all-MiniLM-L6-v2";
    public EmbeddingService(IConfiguration config, DataPaths paths, ILogger<EmbeddingService> logger)
    {
        _config = config; _paths = paths; _logger = logger;
        var file = Path.Combine(paths.Root, "indexes", "minilm.json");
        if (!File.Exists(file)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file)); var json = doc.RootElement;
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(paths.Knowledge))).ToLowerInvariant();
            if (json.GetProperty("knowledgeSha256").GetString() != hash || json.GetProperty("model").GetString() != Model) { Status = "stale_index"; return; }
            foreach (var item in json.GetProperty("vectors").EnumerateObject())
            {
                var vector = item.Value.Deserialize<float[]>()!;
                if (vector.Length != 384 || vector.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid embedding vector.");
                Vectors[item.Name] = vector;
            }
            using var corpus = JsonDocument.Parse(File.ReadAllText(paths.Knowledge));
            var expected = corpus.RootElement.GetProperty("passages").EnumerateArray().Select(p => p.GetProperty("id").GetString()!).ToHashSet();
            if (!expected.SetEquals(Vectors.Keys)) throw new InvalidDataException("Embedding coverage mismatch.");
            Status = Vectors.Count > 0 ? "ready" : "not_indexed";
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException) { Vectors.Clear(); Status = "invalid_index"; logger.LogWarning("Embedding index unavailable ({Type}).", e.GetType().Name); }
    }
    public async Task<float[]?> Encode(string query, CancellationToken cancellationToken)
    {
        if (Vectors.Count == 0) return null;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(query, out var cached)) return cached;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(45));
            if (_worker is null || _worker.HasExited)
            {
                var python = _config["Retrieval:Python"] ?? Path.Combine(_paths.Root, "..", ".runtime", "knowledge-venv", "bin", "python");
                var worker = _config["Retrieval:Worker"] ?? Path.Combine(_paths.Root, "..", "scripts", "embedding_worker.py");
                var modelPath = _config["Retrieval:ModelPath"] ?? Path.Combine(_paths.Root, "indexes", "model");
                var start = new ProcessStartInfo(python) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                start.ArgumentList.Add("-u"); start.ArgumentList.Add(worker); start.ArgumentList.Add(modelPath);
                start.Environment["TOKENIZERS_PARALLELISM"] = "false"; start.Environment["HF_HUB_OFFLINE"] = "1";
                _worker = Process.Start(start) ?? throw new IOException("Embedding worker did not start.");
                _worker.ErrorDataReceived += (_, _) => { }; _worker.BeginErrorReadLine();
            }
            await _worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { text = query }).AsMemory(), timeout.Token); await _worker.StandardInput.FlushAsync(timeout.Token);
            var line = await _worker.StandardOutput.ReadLineAsync(timeout.Token);
            var vector = JsonSerializer.Deserialize<float[]>(line ?? "") ?? throw new InvalidDataException("Empty query vector.");
            if (vector.Length != 384 || vector.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid query vector.");
            if (_cache.Count >= 256) _cache.Clear(); _cache[query] = vector; Status = "ready"; return vector;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Stop(); throw; }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or JsonException or InvalidDataException or InvalidOperationException or OperationCanceledException) { Stop(); Status = "worker_unavailable"; _logger.LogWarning("Local embedding query unavailable ({Type}).", e.GetType().Name); return null; }
        finally { _gate.Release(); }
    }
    private void Stop() { if (_worker is not null) { if (!_worker.HasExited) _worker.Kill(true); _worker.Dispose(); _worker = null; } }
    public void Dispose() { Stop(); _gate.Dispose(); }
}
