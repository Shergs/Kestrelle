using System.Diagnostics;
using System.Text.Json;
using Kestrelle.Shared;
using Microsoft.Extensions.Options;

namespace Kestrelle.Api.Sounds;

public sealed record SoundFileMetadata(TimeSpan Duration);

public interface ISoundFileMetadataReader
{
    Task<SoundFileMetadata> ReadAsync(string path, CancellationToken ct);
}

public sealed class FfprobeSoundFileMetadataReader(ILogger<FfprobeSoundFileMetadataReader> logger) : ISoundFileMetadataReader
{
    public async Task<SoundFileMetadata> ReadAsync(string path, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v quiet -print_format json -show_entries format=duration \"{path.Replace("\\", "\\\\")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var _ = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        });

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            logger.LogWarning("ffprobe failed for {Path}: {Error}", path, stderr);
            throw new InvalidOperationException("Unable to inspect the uploaded audio file.");
        }

        using var document = JsonDocument.Parse(stdout);
        if (!document.RootElement.TryGetProperty("format", out var formatElement) ||
            !formatElement.TryGetProperty("duration", out var durationElement))
        {
            throw new InvalidOperationException("Unable to determine the duration of the uploaded audio file.");
        }

        var rawDuration = durationElement.GetString();
        if (!double.TryParse(rawDuration, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            throw new InvalidOperationException("Unable to determine the duration of the uploaded audio file.");

        return new SoundFileMetadata(TimeSpan.FromSeconds(seconds));
    }
}

public interface ISoundStorage
{
    string ResolveAbsolutePath(string storageKey);
    Task SaveAsync(string storageKey, Stream source, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed class LocalDiskSoundStorage : ISoundStorage
{
    private readonly string _rootPath;

    public LocalDiskSoundStorage(IOptions<SoundStorageOptions> options)
    {
        var configuredRoot = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configuredRoot))
            configuredRoot = "sounds";

        _rootPath = Path.GetFullPath(configuredRoot);
        Directory.CreateDirectory(_rootPath);
    }

    public string ResolveAbsolutePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));

        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage key resolves outside the configured sound storage root.");

        return fullPath;
    }

    public async Task SaveAsync(string storageKey, Stream source, CancellationToken ct)
    {
        var path = ResolveAbsolutePath(storageKey);
        var directory = Path.GetDirectoryName(path) ?? _rootPath;
        Directory.CreateDirectory(directory);

        await using var target = File.Create(path);
        await source.CopyToAsync(target, ct);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var path = ResolveAbsolutePath(storageKey);
        Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = ResolveAbsolutePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }
}

