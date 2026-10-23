using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows.Adapters;

internal static class AdapterFailure
{
    public static AdapterResult<T> FromException<T>(Exception exception)
    {
        var (code, message) = Internal.Wmi.MapException(exception);
        return code is "wmi_access_denied" or "access_denied" or "timeout" or "wmi_timeout"
            ? AdapterResult<T>.Unknown(code, message)
            : AdapterResult<T>.Failed(code, message);
    }
}
