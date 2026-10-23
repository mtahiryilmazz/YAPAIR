using System.Net.NetworkInformation;
using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows.Adapters;

/// <summary>
/// Local-only network posture checks. No ping, DNS lookup, subnet scan, or
/// external request is made; the report describes configuration, not reachability.
/// </summary>
internal sealed class NetworkAdapter
{
    public AdapterResult<Finding> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var findings = new List<Finding>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(static candidate => candidate.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .ToArray();

            if (interfaces.Length == 0)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Network,
                    Code = "no_network_adapter",
                    Domain = HealthDomain.Network,
                    Severity = FindingSeverity.Warning,
                    Subject = "network",
                    Summary = "Kullanılabilir ağ bağdaştırıcısı bulunamadı.",
                });
                return AdapterResult<Finding>.Ok(findings);
            }

            var active = interfaces.Where(static candidate => candidate.OperationalStatus == OperationalStatus.Up).ToArray();

            foreach (var candidate in interfaces)
            {
                if (candidate.OperationalStatus is OperationalStatus.Down or OperationalStatus.LowerLayerDown
                    && candidate.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                {
                    findings.Add(new Finding
                    {
                        Source = SourceNames.Network,
                        Code = "network_adapter_down",
                        Domain = HealthDomain.Network,
                        Severity = FindingSeverity.Warning,
                        Subject = candidate.Name,
                        Summary = $"Ağ bağdaştırıcısı '{candidate.Name}' bağlı değil (durum: {candidate.OperationalStatus}).",
                        Evidence = [$"type={candidate.NetworkInterfaceType}", $"status={candidate.OperationalStatus}"],
                        PlaybookId = "network.adapter.review.v1",
                    });
                }
            }

            if (active.Length == 0)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Network,
                    Code = "no_active_network_adapter",
                    Domain = HealthDomain.Network,
                    Severity = FindingSeverity.Error,
                    Subject = "network",
                    Summary = "Hiçbir ağ bağdaştırıcısı etkin değil; internet veya yerel ağ erişimi çalışmayabilir.",
                    PlaybookId = "network.adapter.review.v1",
                });
            }
            else
            {
                var hasGateway = active.Any(candidate => candidate.GetIPProperties().GatewayAddresses
                    .Any(gateway => gateway.Address is not null && !gateway.Address.Equals(System.Net.IPAddress.Any)));

                if (!hasGateway)
                {
                    findings.Add(new Finding
                    {
                        Source = SourceNames.Network,
                        Code = "no_default_gateway",
                        Domain = HealthDomain.Network,
                        Severity = FindingSeverity.Warning,
                        Subject = "IPv4",
                        Summary = "Etkin ağ bağdaştırıcılarında varsayılan ağ geçidi yok; yerel ağ dışına erişim sağlanamayabilir.",
                        PlaybookId = "network.adapter.review.v1",
                    });
                }

                var hasDns = active.Any(candidate => candidate.GetIPProperties().DnsAddresses.Count > 0);
                if (!hasDns)
                {
                    findings.Add(new Finding
                    {
                        Source = SourceNames.Network,
                        Code = "no_dns_servers",
                        Domain = HealthDomain.Network,
                        Severity = FindingSeverity.Warning,
                        Subject = "DNS",
                        Summary = "Etkin ağ bağdaştırıcılarında DNS sunucusu tanımlı değil; ad çözümleme başarısız olabilir.",
                        PlaybookId = "network.dns.review.v1",
                    });
                }
            }

            return AdapterResult<Finding>.Ok(findings);
        }
        catch (NetworkInformationException)
        {
            return AdapterResult<Finding>.Unknown("network_enumeration_failed", "Network adapters could not be enumerated.");
        }
    }
}
