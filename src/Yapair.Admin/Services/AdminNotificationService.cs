using System.Text.Json;

namespace Yapair.Admin.Services;

public sealed record HelpNotification(long Id, string MachineLabel, string ProblemText);

public sealed class AdminNotificationService : IDisposable
{
    private readonly AdminApiClient _api;
    private readonly HashSet<long> _seenHelpIds = new();
    private CancellationTokenSource? _cts;
    private bool _firstRun = true;

    public event Action<HelpNotification>? OnNewHelpRequest;

    public AdminNotificationService(AdminApiClient api)
    {
        _api = api;
    }

    public void Start(TimeSpan? interval = null)
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        var delay = interval ?? TimeSpan.FromSeconds(10);

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await PollAsync(_cts.Token);
                }
                catch { }

                try
                {
                    await Task.Delay(delay, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    public async Task PollAsync(CancellationToken ct = default)
    {
        var res = await _api.GetHelpRequestsAsync("open", ct);
        if (res.HasValue && res.Value.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idProp)) continue;
                var id = idProp.GetInt64();
                var status = item.TryGetProperty("status", out var stProp) ? stProp.GetString() : "open";

                if (status == "open")
                {
                    if (!_seenHelpIds.Contains(id))
                    {
                        _seenHelpIds.Add(id);
                        if (!_firstRun)
                        {
                            var machine = item.TryGetProperty("machine_label", out var mProp) ? mProp.GetString() ?? "Bilinmeyen Makine" : "Bilinmeyen Makine";
                            var problem = item.TryGetProperty("problem_text", out var pProp) ? pProp.GetString() ?? "Sorun belirtilmedi" : "Sorun belirtilmedi";
                            OnNewHelpRequest?.Invoke(new HelpNotification(id, machine, problem));
                        }
                    }
                }
            }
            _firstRun = false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
