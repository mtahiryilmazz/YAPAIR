using System.Text;
using System.Text.Json;
using Yapair.Core.Contracts;

namespace Yapair.Cli;

/// <summary>
/// Optional JSONL operational log. It deliberately records only source names,
/// statuses, durations, and stable error codes - never printer names, document
/// names, paths, or exception text.
/// </summary>
internal sealed class DiscoveryLog : IDisposable
{
    private readonly StreamWriter? _writer;

    private DiscoveryLog(StreamWriter? writer)
    {
        _writer = writer;
    }

    public static DiscoveryLog? TryCreate(string? path, TextWriter stderr)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var writer = new StreamWriter(
                new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false))
            {
                AutoFlush = true,
            };

            return new DiscoveryLog(writer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"Uyarı: günlük dosyası açılamadı ({Path.GetFileName(path)}).");
            return null;
        }
    }

    public void Write(
        string level,
        string eventName,
        string? source = null,
        SourceStatus? status = null,
        long? durationMs = null,
        int? itemCount = null,
        string? errorCode = null)
    {
        if (_writer is null)
        {
            return;
        }

        var record = new
        {
            timestamp = DateTimeOffset.UtcNow,
            level,
            event_name = eventName,
            source,
            status = status?.ToString().ToLowerInvariant(),
            duration_ms = durationMs,
            item_count = itemCount,
            error_code = errorCode,
        };

        _writer.WriteLine(JsonSerializer.Serialize(record));
    }

    public void Dispose() => _writer?.Dispose();
}
