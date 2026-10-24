namespace Yapair.Core.Playbooks;

public interface IRepairExecutor
{
    string PlaybookId { get; }
    RiskLevel Risk { get; }
    Task<RepairResult> ExecuteAsync(CancellationToken ct = default);
}

public sealed record RepairResult(bool Success, string Message);
