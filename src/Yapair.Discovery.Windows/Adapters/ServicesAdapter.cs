using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;

namespace Yapair.Discovery.Windows.Adapters;

/// <summary>
/// Checks services that commonly explain Windows symptoms (audio, printing,
/// networking, updates, RPC, installer). It is deliberately not a "start every
/// stopped service" tool: findings are evidence, not authorisation.
/// </summary>
internal sealed class ServicesAdapter
{
    private static readonly Dictionary<string, ServiceExpectation> Expected = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RpcSs"] = new(FindingSeverity.Critical, HealthDomain.System, WarnWhenStopped: true),
        ["EventLog"] = new(FindingSeverity.Critical, HealthDomain.System, WarnWhenStopped: true),
        ["Winmgmt"] = new(FindingSeverity.Critical, HealthDomain.System, WarnWhenStopped: true),
        ["Dnscache"] = new(FindingSeverity.Error, HealthDomain.Network, WarnWhenStopped: true),
        ["Dhcp"] = new(FindingSeverity.Error, HealthDomain.Network, WarnWhenStopped: true),
        ["LanmanWorkstation"] = new(FindingSeverity.Error, HealthDomain.Network, WarnWhenStopped: true),
        ["Audiosrv"] = new(FindingSeverity.Error, HealthDomain.Audio, WarnWhenStopped: true),
        ["AudioEndpointBuilder"] = new(FindingSeverity.Error, HealthDomain.Audio, WarnWhenStopped: true),
        // Trigger-start services are normally stopped between activations.
        ["wuauserv"] = new(FindingSeverity.Warning, HealthDomain.WindowsUpdate, WarnWhenStopped: false),
        ["BITS"] = new(FindingSeverity.Warning, HealthDomain.WindowsUpdate, WarnWhenStopped: false),
        ["WSearch"] = new(FindingSeverity.Warning, HealthDomain.Services, WarnWhenStopped: true),
        ["Themes"] = new(FindingSeverity.Warning, HealthDomain.Services, WarnWhenStopped: true),
    };

    public AdapterResult<Finding> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var findings = new List<Finding>();
            var services = Wmi.Query(
                "SELECT Name, DisplayName, State, StartMode FROM Win32_Service",
                options.PerSourceTimeout,
                item => new
                {
                    Name = Wmi.String(item, "Name"),
                    DisplayName = Wmi.String(item, "DisplayName"),
                    State = Wmi.String(item, "State"),
                    StartMode = Wmi.String(item, "StartMode"),
                });

            foreach (var service in services)
            {
                if (!Expected.TryGetValue(service.Name, out var expectation))
                {
                    continue;
                }

                // The spooler is reported by the printer module so its finding
                // stays grouped with the affected printers.
                if (string.Equals(service.Name, "Spooler", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isRunning = string.Equals(service.State, "Running", StringComparison.OrdinalIgnoreCase);
                var isDisabled = string.Equals(service.StartMode, "Disabled", StringComparison.OrdinalIgnoreCase);
                var isAutomatic = string.Equals(service.StartMode, "Auto", StringComparison.OrdinalIgnoreCase);

                if (isDisabled)
                {
                    findings.Add(CreateFinding(
                        service.Name,
                        service.DisplayName,
                        "service_disabled",
                        expectation.Severity,
                        expectation.Domain,
                        $"{service.DisplayName} ({service.Name}) hizmeti devre dışı bırakılmış."
                        + " Bu, ilgili Windows işlevinin çalışmamasına neden olabilir.",
                        [$"service={service.Name}", $"state={service.State}", $"start_mode={service.StartMode}"]));
                }
                else if (!isRunning && isAutomatic && expectation.WarnWhenStopped)
                {
                    findings.Add(CreateFinding(
                        service.Name,
                        service.DisplayName,
                        "service_not_running",
                        expectation.Severity,
                        expectation.Domain,
                        $"{service.DisplayName} ({service.Name}) otomatik başlatılacak şekilde ayarlı ancak çalışmıyor."
                        + $" Durum: {service.State}.",
                        [$"service={service.Name}", $"state={service.State}", $"start_mode={service.StartMode}"]));
                }
            }

            return AdapterResult<Finding>.Ok(findings);
        }
        catch (Exception ex)
        {
            return AdapterFailure.FromException<Finding>(ex);
        }
    }

    private static Finding CreateFinding(
        string serviceName,
        string displayName,
        string code,
        FindingSeverity severity,
        HealthDomain domain,
        string summary,
        IReadOnlyList<string> evidence) => new()
    {
        Source = SourceNames.Services,
        Code = code,
        Domain = domain,
        Severity = severity,
        Subject = string.IsNullOrWhiteSpace(displayName) ? serviceName : displayName,
        Summary = summary,
        Evidence = evidence,
        PlaybookId = "services.controlled_restart.v1",
    };

    private sealed record ServiceExpectation(FindingSeverity Severity, HealthDomain Domain, bool WarnWhenStopped);
}
