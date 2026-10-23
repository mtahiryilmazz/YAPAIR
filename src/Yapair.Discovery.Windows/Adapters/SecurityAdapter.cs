using Microsoft.Win32;
using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows.Adapters;

/// <summary>Read-only UAC and firewall posture checks. It never changes policy.</summary>
internal sealed class SecurityAdapter
{
    public AdapterResult<Finding> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var findings = new List<Finding>();

            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var systemPolicy = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var enableLua = GetInt(systemPolicy, "EnableLUA");
            var consentPrompt = GetInt(systemPolicy, "ConsentPromptBehaviorAdmin");
            var secureDesktop = GetInt(systemPolicy, "PromptOnSecureDesktop");

            if (enableLua == 0)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Security,
                    Code = "uac_disabled",
                    Domain = HealthDomain.Security,
                    Severity = FindingSeverity.Critical,
                    Subject = "UAC",
                    Summary = "Kullanıcı Hesabı Denetimi (UAC) devre dışı. Yükseltilmiş işlemler onay istemeden çalışabilir.",
                    Evidence = ["EnableLUA=0"],
                    PlaybookId = "security.uac.review.v1",
                });
            }

            if (consentPrompt == 0)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Security,
                    Code = "uac_no_consent_prompt",
                    Domain = HealthDomain.Security,
                    Severity = FindingSeverity.Critical,
                    Subject = "UAC",
                    Summary = "Yönetici onay istemi kapalı (ConsentPromptBehaviorAdmin=0). Uygulamalar sessizce yükseltilebilir.",
                    Evidence = ["ConsentPromptBehaviorAdmin=0"],
                    PlaybookId = "security.uac.review.v1",
                });
            }

            if (secureDesktop == 0)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Security,
                    Code = "uac_secure_desktop_disabled",
                    Domain = HealthDomain.Security,
                    Severity = FindingSeverity.Warning,
                    Subject = "UAC",
                    Summary = "UAC istemleri güvenli masaüstünde gösterilmiyor; onay penceresi başka uygulamalarca taklit edilebilir.",
                    Evidence = ["PromptOnSecureDesktop=0"],
                    PlaybookId = "security.uac.review.v1",
                });
            }

            foreach (var profile in new[] { "DomainProfile", "StandardProfile", "PublicProfile" })
            {
                using var firewall = baseKey.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{profile}");

                if (GetInt(firewall, "EnableFirewall") == 0)
                {
                    findings.Add(new Finding
                    {
                        Source = SourceNames.Security,
                        Code = "firewall_disabled",
                        Domain = HealthDomain.Security,
                        Severity = FindingSeverity.Warning,
                        Subject = profile,
                        Summary = $"Windows Güvenlik Duvarı '{profile}' profili için kapalı.",
                        Evidence = [$"profile={profile}", "EnableFirewall=0"],
                        PlaybookId = "security.firewall.review.v1",
                    });
                }
            }

            return AdapterResult<Finding>.Ok(findings);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return AdapterResult<Finding>.Unknown(
                "security_policy_unavailable",
                "UAC and firewall policy could not be read with the current permissions.");
        }
    }

    private static int? GetInt(RegistryKey? key, string name)
    {
        if (key?.GetValue(name) is not { } value)
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }
}
