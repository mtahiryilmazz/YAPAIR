using System.Globalization;
using System.Management;

namespace Yapair.Discovery.Windows.Internal;

/// <summary>Small, defensive helpers over System.Management (WMI/CIM).</summary>
internal static class Wmi
{
    public static List<T> Query<T>(string wql, TimeSpan timeout, Func<ManagementBaseObject, T> map)
    {
        using var searcher = new ManagementObjectSearcher(wql);
        searcher.Options.Timeout = timeout;

        using var results = searcher.Get();
        var mapped = new List<T>();
        foreach (ManagementBaseObject item in results)
        {
            using (item)
            {
                mapped.Add(map(item));
            }
        }

        return mapped;
    }

    public static object? Value(ManagementBaseObject item, string propertyName)
    {
        foreach (PropertyData property in item.Properties)
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    public static string String(ManagementBaseObject item, string propertyName)
    {
        var value = Value(item, propertyName);
        return value is null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    public static int Int32(ManagementBaseObject item, string propertyName)
    {
        var value = Value(item, propertyName);
        if (value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return 0;
        }
    }

    public static long Int64(ManagementBaseObject item, string propertyName)
    {
        var value = Value(item, propertyName);
        if (value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return 0;
        }
    }

    public static bool Boolean(ManagementBaseObject item, string propertyName)
    {
        var value = Value(item, propertyName);
        return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<string> StringArray(ManagementBaseObject item, string propertyName)
    {
        var value = Value(item, propertyName);
        return value switch
        {
            null => [],
            string single => [single],
            string[] array => array.Where(static entry => !string.IsNullOrWhiteSpace(entry)).ToArray(),
            _ => [],
        };
    }

    public static (string ErrorCode, string Message) MapException(Exception exception) => exception switch
    {
        ManagementException management => management.ErrorCode switch
        {
            ManagementStatus.AccessDenied => ("wmi_access_denied", "Access denied while querying Windows management instrumentation."),
            ManagementStatus.NotFound => ("wmi_not_found", "The requested WMI class or instance was not found."),
            ManagementStatus.InvalidClass => ("wmi_invalid_class", "The requested WMI class is not available on this Windows version."),
            ManagementStatus.Timedout => ("wmi_timeout", "The WMI query timed out."),
            _ => ("wmi_error", "A Windows management instrumentation query failed."),
        },
        UnauthorizedAccessException => ("access_denied", "Access was denied while reading a Windows source."),
        TimeoutException => ("timeout", "The source did not respond before its timeout."),
        PlatformNotSupportedException => ("platform_not_supported", "The source is not supported on this platform."),
        _ => ("source_error", "The Windows source could not be read."),
    };
}
