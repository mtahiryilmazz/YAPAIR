using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows;

/// <summary>
/// Adapter outcome. Items are still returned for partial/unknown outcomes when
/// they were observed before the failure; callers must not interpret an empty
/// collection as proof that nothing exists.
/// </summary>
public sealed record AdapterResult<T>
{
    public SourceStatus Status { get; init; } = SourceStatus.Success;

    public IReadOnlyList<T> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public int? ItemCountOverride { get; init; }

    public static AdapterResult<T> Ok(IReadOnlyList<T> items) => new()
    {
        Status = SourceStatus.Success,
        Items = items,
    };

    public static AdapterResult<T> Ok(T item) => new()
    {
        Status = SourceStatus.Success,
        Items = [item],
    };

    public static AdapterResult<T> Partial(IReadOnlyList<T> items, string errorCode, string message) => new()
    {
        Status = SourceStatus.Partial,
        Items = items,
        ErrorCode = errorCode,
        Message = message,
    };

    public static AdapterResult<T> Unknown(string errorCode, string message) => new()
    {
        Status = SourceStatus.Unknown,
        ErrorCode = errorCode,
        Message = message,
    };

    public static AdapterResult<T> Failed(string errorCode, string message) => new()
    {
        Status = SourceStatus.Failed,
        ErrorCode = errorCode,
        Message = message,
    };

    public static AdapterResult<T> Disabled(string message) => new()
    {
        Status = SourceStatus.Disabled,
        Message = message,
    };
}
