namespace Yapair.Core.Auth;

public enum UserRole
{
    ReadOnly = 0,
    Technician = 1,
    Admin = 2
}

public static class UserRoleExtensions
{
    public static bool HasPermission(this UserRole current, UserRole required)
    {
        return current >= required;
    }

    public static string ToRoleString(this UserRole role) => role switch
    {
        UserRole.Admin => "admin",
        UserRole.Technician => "technician",
        UserRole.ReadOnly => "readonly",
        _ => "readonly"
    };

    public static UserRole ParseRole(string? roleStr)
    {
        if (string.IsNullOrWhiteSpace(roleStr)) return UserRole.ReadOnly;
        return roleStr.Trim().ToLowerInvariant() switch
        {
            "admin" => UserRole.Admin,
            "technician" => UserRole.Technician,
            "readonly" => UserRole.ReadOnly,
            _ => UserRole.ReadOnly
        };
    }
}
