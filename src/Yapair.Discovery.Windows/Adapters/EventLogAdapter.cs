using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using Microsoft.Win32;
using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed record EventLogBundle(
    IReadOnlyList<EventLogInfo> Logs,
    IReadOnlyList<EventRecordInfo> Records);

internal sealed class EventLogAdapter
{
    private static readonly string[] Channels =
    [
        "Microsoft-Windows-PrintService/Admin",
        "System",
        "Application",
    ];

    public AdapterResult<EventLogBundle> Read(DiscoveryOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var logs = new List<EventLogInfo>();
        var records = new List<EventRecordInfo>();
        var cutoff = now.AddHours(-options.EventLookbackHours);
        var perChannelLimit = Math.Max(1, options.MaxEvents / Channels.Length);

        foreach (var channel in Channels)
        {
            var remaining = Math.Min(perChannelLimit, options.MaxEvents - records.Count);
            if (remaining <= 0)
            {
                logs.Add(new EventLogInfo
                {
                    Name = channel,
                    Status = SourceStatus.Success,
                    Message = "Event limit reached before this channel was read.",
                    EventsRead = 0,
                });
                continue;
            }

            if (!IsChannelEnabled(channel))
            {
                logs.Add(new EventLogInfo
                {
                    Name = channel,
                    Status = SourceStatus.Disabled,
                    Message = "Channel is disabled in the Windows event log configuration.",
                    EventsRead = 0,
                });
                continue;
            }

            try
            {
                var query = new EventLogQuery(channel, PathType.LogName, BuildXPath(options.EventLookbackHours))
                {
                    TolerateQueryErrors = true,
                };

                using var reader = new EventLogReader(query);
                var read = 0;
                EventRecord? record;
                while (read < remaining && (record = reader.ReadEvent()) is not null)
                {
                    using (record)
                    {
                        if (record.TimeCreated is { } created && created < cutoff.LocalDateTime)
                        {
                            break;
                        }

                        records.Add(Map(record, channel));
                        read++;
                    }
                }

                logs.Add(new EventLogInfo
                {
                    Name = channel,
                    Status = SourceStatus.Success,
                    EventsRead = read,
                });
            }
            catch (EventLogNotFoundException)
            {
                logs.Add(new EventLogInfo
                {
                    Name = channel,
                    Status = SourceStatus.Unknown,
                    ErrorCode = "event_log_not_found",
                    Message = "The event log channel does not exist on this system.",
                    EventsRead = 0,
                });
            }
            catch (UnauthorizedAccessException)
            {
                logs.Add(new EventLogInfo
                {
                    Name = channel,
                    Status = SourceStatus.Unknown,
                    ErrorCode = "event_log_access_denied",
                    Message = "The event log channel could not be read with the current permissions.",
                    EventsRead = 0,
                });
            }
            catch (EventLogException)
            {
                logs.Add(new EventLogInfo
                {
                    Name = channel,
                    Status = SourceStatus.Unknown,
                    ErrorCode = "event_log_read_error",
                    Message = "The event log channel could not be read.",
                    EventsRead = 0,
                });
            }
        }

        var successCount = logs.Count(static log => log.Status == SourceStatus.Success);
        var problemCount = logs.Count(static log => log.Status is SourceStatus.Unknown or SourceStatus.Failed);
        var anyUnknown = logs.Any(static log => log.Status == SourceStatus.Unknown);

        SourceStatus status;
        if (successCount > 0)
        {
            status = problemCount > 0 ? SourceStatus.Partial : SourceStatus.Success;
        }
        else if (problemCount > 0)
        {
            status = anyUnknown ? SourceStatus.Unknown : SourceStatus.Failed;
        }
        else
        {
            status = SourceStatus.Partial;
        }

        var bundle = new EventLogBundle(logs, records);
        return status switch
        {
            SourceStatus.Success => new AdapterResult<EventLogBundle>
            {
                Status = SourceStatus.Success,
                Items = [bundle],
                ItemCountOverride = records.Count,
            },
            SourceStatus.Partial => new AdapterResult<EventLogBundle>
            {
                Status = SourceStatus.Partial,
                Items = [bundle],
                ErrorCode = "event_log_partial",
                Message = "One or more PrintService event channels could not be read.",
                ItemCountOverride = records.Count,
            },
            SourceStatus.Unknown => AdapterResult<EventLogBundle>.Unknown(
                "event_log_unavailable",
                "No PrintService event channel could be read with the current permissions."),
            _ => AdapterResult<EventLogBundle>.Failed(
                "event_log_unavailable",
                "No PrintService event channel could be read."),
        };
    }

    private static EventRecordInfo Map(EventRecord record, string channel)
    {
        var message = record.FormatDescription() ?? string.Empty;
        if (message.Length > 2000)
        {
            message = message[..2000];
        }

        return new EventRecordInfo
        {
            LogName = channel,
            EventId = record.Id,
            LevelCode = record.Level ?? 0,
            Level = record.LevelDisplayName
                ?? (record.Level is { } level ? level.ToString(CultureInfo.InvariantCulture) : "unknown"),
            Provider = record.ProviderName ?? string.Empty,
            TimeCreated = record.TimeCreated is { } created
                ? new DateTimeOffset(created)
                : DateTimeOffset.MinValue,
            Message = message,
        };
    }

    private static string BuildXPath(int lookbackHours)
    {
        var milliseconds = lookbackHours * 60L * 60L * 1000L;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"*[System[TimeCreated[timediff(@SystemTime) <= {milliseconds}]]]");
    }

    private static bool IsChannelEnabled(string channel)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Channels\{channel}");

            var value = key?.GetValue("Enabled");
            return value is not int enabled || enabled != 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Let the event reader decide; failing open here does not grant any
            // write access and avoids hiding a channel because of registry ACLs.
            return true;
        }
    }
}
