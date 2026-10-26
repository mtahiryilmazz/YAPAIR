using System.Security.Cryptography;
using System.Text.Json;

namespace Yapair.Core.Auth;

public sealed class ApprovalManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly List<ApprovalRequest> _requests = new();
    private readonly object _lock = new();

    public ApprovalManager(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "approvals.json");

        Load();
    }

    public ApprovalRequest CreateRequest(string machineKey, string requestedMode, string? reason)
    {
        lock (_lock)
        {
            var id = "appr_" + Guid.NewGuid().ToString("N")[..10];
            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            var req = new ApprovalRequest
            {
                Id = id,
                MachineKey = machineKey,
                RequestedMode = requestedMode.ToUpperInvariant(),
                Reason = reason,
                Status = "pending",
                ApprovalCode = code,
                RequestedAt = DateTimeOffset.UtcNow
            };

            _requests.Add(req);
            Save();
            return req;
        }
    }

    public ApprovalRequest? Approve(string requestId, string approvedBy)
    {
        lock (_lock)
        {
            var idx = _requests.FindIndex(r => r.Id == requestId);
            if (idx < 0) return null;

            var updated = _requests[idx] with
            {
                Status = "approved",
                ApprovedBy = approvedBy,
                ApprovedAt = DateTimeOffset.UtcNow
            };

            _requests[idx] = updated;
            Save();
            return updated;
        }
    }

    public ApprovalRequest? Reject(string requestId, string rejectedBy)
    {
        lock (_lock)
        {
            var idx = _requests.FindIndex(r => r.Id == requestId);
            if (idx < 0) return null;

            var updated = _requests[idx] with
            {
                Status = "rejected",
                ApprovedBy = rejectedBy,
                ApprovedAt = DateTimeOffset.UtcNow
            };

            _requests[idx] = updated;
            Save();
            return updated;
        }
    }

    public bool ValidateApprovalCode(string machineKey, string mode, string code)
    {
        lock (_lock)
        {
            var found = _requests.FirstOrDefault(r =>
                r.MachineKey.Equals(machineKey, StringComparison.OrdinalIgnoreCase) &&
                r.RequestedMode.Equals(mode, StringComparison.OrdinalIgnoreCase) &&
                r.ApprovalCode == code &&
                r.Status == "approved" &&
                r.ApprovedAt.HasValue &&
                r.ApprovedAt.Value > DateTimeOffset.UtcNow.AddHours(-1)); // valid for 1 hour

            return found != null;
        }
    }

    public IReadOnlyList<ApprovalRequest> ListRequests(string? status = null)
    {
        lock (_lock)
        {
            var q = _requests.AsEnumerable();
            if (!string.IsNullOrEmpty(status))
            {
                q = q.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }
            return q.OrderByDescending(r => r.RequestedAt).ToList();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var txt = File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize<List<ApprovalRequest>>(txt);
                if (list != null)
                {
                    _requests.Clear();
                    _requests.AddRange(list);
                }
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var txt = JsonSerializer.Serialize(_requests, JsonOpts);
            File.WriteAllText(_filePath, txt);
        }
        catch { }
    }
}
