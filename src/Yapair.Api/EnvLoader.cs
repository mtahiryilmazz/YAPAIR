namespace Yapair.Api;

public static class EnvLoader
{
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var i = line.IndexOf('=');
            if (i < 1) continue;
            var key = line[..i].Trim();
            var value = line[(i + 1)..].Trim().Trim('"').Trim('\'');
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
