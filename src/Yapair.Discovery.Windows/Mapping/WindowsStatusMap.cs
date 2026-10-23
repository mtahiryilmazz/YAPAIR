namespace Yapair.Discovery.Windows.Mapping;

/// <summary>
/// Stable, language-neutral status codes. User-facing Turkish/English text is a
/// presentation concern and must not be baked into the evidence contract.
/// </summary>
internal static class WindowsStatusMap
{
    public static string PrinterStatus(int value) => value switch
    {
        1 => "other",
        2 => "unknown",
        3 => "idle",
        4 => "printing",
        5 => "warmup",
        6 => "stopped_printing",
        7 => "offline",
        _ => "unknown",
    };

    public static string DetectedErrorState(int value) => value switch
    {
        0 => "unknown",
        1 => "other",
        2 => "no_error",
        3 => "low_paper",
        4 => "no_paper",
        5 => "low_toner",
        6 => "no_toner",
        7 => "door_open",
        8 => "jammed",
        9 => "offline",
        10 => "service_requested",
        11 => "output_bin_full",
        _ => "unknown",
    };

    public static string ExtendedPrinterStatus(int value) => value switch
    {
        1 => "other",
        2 => "unknown",
        3 => "idle",
        4 => "printing",
        5 => "warmup",
        6 => "stopped_printing",
        7 => "offline",
        8 => "paused",
        9 => "error",
        10 => "busy",
        11 => "not_available",
        12 => "waiting",
        13 => "processing",
        14 => "initialization",
        15 => "power_save",
        16 => "pending",
        17 => "shutdown",
        18 => "in_error",
        19 => "starting",
        20 => "stopping",
        _ => "unknown",
    };

    public static string ConfigManagerError(int value) => value switch
    {
        0 => "no_error",
        1 => "not_configured",
        2 => "device_loader_failed",
        3 => "out_of_memory",
        4 => "wrong_entry_type",
        5 => "lack_of_arbitrator",
        6 => "boot_config_conflict",
        7 => "failed_filter",
        8 => "device_loader_not_found",
        9 => "invalid_data",
        10 => "failed_start",
        11 => "device_reported_failure",
        12 => "resource_conflict",
        13 => "not_verified",
        14 => "restart_required",
        15 => "reenumeration_required",
        16 => "partial_log_configuration",
        17 => "unknown_resource",
        18 => "reinstall_required",
        19 => "registry_error",
        20 => "vxd_loader_error",
        21 => "will_be_removed",
        22 => "disabled",
        23 => "device_loader_not_ready",
        24 => "device_not_present",
        25 => "moved",
        26 => "too_early",
        27 => "no_valid_log_configuration",
        28 => "failed_install",
        29 => "hardware_disabled",
        30 => "cannot_share_irq",
        31 => "failed_add",
        32 => "disabled_service",
        33 => "translation_failed",
        34 => "no_soft_config",
        35 => "bios_table",
        36 => "irq_translation_failed",
        37 => "failed_driver_entry",
        38 => "driver_failed_prior_unload",
        39 => "driver_failed_load",
        40 => "driver_service_key_invalid",
        41 => "legacy_service_no_devices",
        42 => "duplicate_device",
        43 => "failed_post_start",
        44 => "halted",
        45 => "phantom",
        46 => "system_shutdown",
        47 => "held_for_eject",
        48 => "driver_blocked",
        49 => "registry_too_large",
        50 => "set_properties_failed",
        51 => "waiting_on_dependency",
        52 => "unsigned_driver",
        53 => "used_by_debugger",
        _ => $"cm_prob_{value}",
    };

    public static IReadOnlyList<string> PrintJobStatusFlags(int mask)
    {
        var flags = new List<string>();
        Add(0x00000001, "printing");
        Add(0x00000002, "spooling");
        Add(0x00000004, "error");
        Add(0x00000008, "paused");
        Add(0x00000010, "offline");
        Add(0x00000020, "paper_out");
        Add(0x00000040, "paper_jam");
        Add(0x00000080, "door_open");
        Add(0x00000100, "low_toner");
        Add(0x00000200, "no_toner");
        Add(0x00000400, "user_intervention");
        Add(0x00000800, "out_of_memory");
        Add(0x00001000, "deleting");
        Add(0x00002000, "blocked");
        return flags;

        void Add(int bit, string name)
        {
            if ((mask & bit) != 0)
            {
                flags.Add(name);
            }
        }
    }

    public static bool LooksLikeNetworkPort(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return false;
        }

        return portName.StartsWith("WSD", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("IP_", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("\\\\", StringComparison.Ordinal)
            || IsIpv4(portName);
    }

    public static string PortProtocol(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return "unknown";
        }

        if (IsIpv4(portName))
        {
            return "tcp_ip";
        }

        if (portName.StartsWith("WSD", StringComparison.OrdinalIgnoreCase))
        {
            return "wsd";
        }

        if (portName.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
        {
            return "usb";
        }

        if (portName.StartsWith("IP_", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return "tcp_ip";
        }

        if (portName.StartsWith("PORTPROMPT:", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("SHRFAX:", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("NUL", StringComparison.OrdinalIgnoreCase)
            || portName.StartsWith("Ne", StringComparison.OrdinalIgnoreCase)
            || portName.EndsWith(':'))
        {
            return "local";
        }

        return "unknown";
    }

    private static bool IsIpv4(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var octet)
                || octet is < 0 or > 255)
            {
                return false;
            }
        }

        return true;
    }
}
