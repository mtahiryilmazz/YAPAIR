using System.Diagnostics;
using System.IO;
using System.Text;

namespace Yapair.Desktop;

public static class CliBridge
{
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Yapair.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }

    public static string FindCliDll(string root)
    {
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var p = Path.Combine(root, "src", "Yapair.Cli", "bin", cfg, "net10.0-windows", "yapair.dll");
            if (File.Exists(p)) return p;
        }
        return Path.Combine(root, "src", "Yapair.Cli", "bin", "Debug", "net10.0-windows", "yapair.dll");
    }

    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string cliDll, string args, TimeSpan timeout, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{cliDll}\" {args}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        var eb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) eb.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return (-1, sb.ToString(), "Zaman aşımı: " + timeout.TotalSeconds + " sn");
        }

        return (p.ExitCode, sb.ToString(), eb.ToString());
    }
}
