using System.Diagnostics;
using OrbitFlame.Api.Contracts;
namespace OrbitFlame.Api.Services;

public sealed class PageService(DataPaths paths, KnowledgeService knowledge, IConfiguration config)
{
    private readonly SemaphoreSlim _renderGate = new(1);
    public bool Exists(string id, int page) => page > 0 && knowledge.Data.Documents.Any(d => d.Id == id) && File.Exists(paths.Page(id, page));
    public async Task<string> Render(string id, int page, CancellationToken cancellationToken)
    {
        var cacheRoot = config["Data:CacheRoot"] ?? Path.Combine(paths.Root, "cache");
        var target = Path.Combine(cacheRoot, "pages", id, $"{page}.png"); if (File.Exists(target)) return target;
        await _renderGate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(target)) return target;
            var doc = knowledge.Data.Documents.First(d => d.Id == id);
            var start = new ProcessStartInfo(config["Data:Python"] ?? (File.Exists(Path.Combine(paths.Root, "..", ".runtime", "knowledge-venv", "bin", "python")) ? Path.Combine(paths.Root, "..", ".runtime", "knowledge-venv", "bin", "python") : "python3")) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            start.ArgumentList.Add(Path.Combine(paths.Root, "..", "scripts", "render_page.py")); start.ArgumentList.Add(paths.Document(doc.FileName)); start.ArgumentList.Add(page.ToString()); start.ArgumentList.Add(target);
            using var process = Process.Start(start) ?? throw new IOException("Page renderer did not start.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token); var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(stderr, stdout); } catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }
            if (process.ExitCode != 0 || !File.Exists(target)) throw new IOException("Original page preview could not be rendered."); return target;
        }
        finally { _renderGate.Release(); }
    }
}
