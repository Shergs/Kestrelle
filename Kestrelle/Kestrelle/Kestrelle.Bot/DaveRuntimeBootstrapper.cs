using System.Runtime.InteropServices;
using Discord.LibDave;
using Discord.LibDave.Binding;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot;

internal static class DaveRuntimeBootstrapper
{
    private static int _initialized;

    public static void EnsureInitialized(ILogger logger)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        if (!OperatingSystem.IsLinux())
        {
            LogAvailability(logger);
            return;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "libdave.so"),
            "/usr/local/lib/libdave.so",
            "/app/libdave.so",
            "libdave",
        };

        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            if (Path.IsPathRooted(candidate) && !File.Exists(candidate))
                continue;

            try
            {
                NativeLibrary.Load(candidate);
                logger.LogInformation("Loaded libdave candidate {LibDavePath}", candidate);
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load libdave candidate {LibDavePath}", candidate);
            }
        }

        try
        {
            Dave.SetLogSink((severity, filePath, lineNumber, message) =>
            {
                logger.Log(
                    severity switch
                    {
                        LoggingSeverity.Verbose => LogLevel.Debug,
                        LoggingSeverity.Info => LogLevel.Information,
                        LoggingSeverity.Warning => LogLevel.Warning,
                        LoggingSeverity.Error => LogLevel.Error,
                        LoggingSeverity.None => LogLevel.Trace,
                        _ => LogLevel.Information,
                    },
                    "[libdave] {FilePath}:{LineNumber} {Message}",
                    filePath,
                    lineNumber,
                    message);
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to install libdave log sink.");
        }

        LogAvailability(logger);
    }

    private static void LogAvailability(ILogger logger)
    {
        try
        {
            var available = Dave.CheckAvailability();
            var discoveredFiles = Directory.Exists(AppContext.BaseDirectory)
                ? Directory.EnumerateFiles(AppContext.BaseDirectory, "libdave*", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .ToArray()
                : Array.Empty<string>();

            logger.LogInformation(
                "libdave availability: {Available}. BaseDirectory: {BaseDirectory}. Files: {LibDaveFiles}",
                available,
                AppContext.BaseDirectory,
                discoveredFiles.Length == 0 ? "<none>" : string.Join(", ", discoveredFiles));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to probe libdave availability.");
        }
    }
}
