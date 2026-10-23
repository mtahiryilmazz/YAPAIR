namespace Yapair.Storage;

/// <summary>
/// Two-level grouping: first by issue family (printers, audio, ...), then by the
/// specific symptom code. For unknown text, token Jaccard similarity provides a
/// conservative fallback instead of pretending a known code exists.
/// </summary>
public static class CaseClusterer
{
    public const double DefaultThreshold = 0.35;

    public static CaseClusterReport Cluster(
        IReadOnlyList<CaseRecord> cases,
        double threshold = DefaultThreshold,
        DateTimeOffset? generatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(cases);
        var effectiveThreshold = Math.Clamp(threshold, 0.1, 0.9);
        var clusters = new List<CaseCluster>();

        var known = cases.Where(static item => !string.Equals(item.IssueFamily, "unknown", StringComparison.OrdinalIgnoreCase));
        foreach (var family in known.GroupBy(static item => item.IssueFamily, StringComparer.OrdinalIgnoreCase))
        {
            var items = family.ToArray();
            var subclusters = items
                .GroupBy(static item => item.SymptomCode, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(static group => group.Count())
                .Select(group => new CaseSubcluster(
                    group.Key,
                    group.First().SymptomLabel,
                    group.Count(),
                    Samples(group)))
                .ToArray();

            clusters.Add(new CaseCluster(
                family.Key,
                FamilyLabel(family.Key),
                items.Length,
                subclusters,
                Samples(items),
                items.Select(static item => item.CaseId).ToArray()));
        }

        clusters.AddRange(ClusterUnknown(cases, effectiveThreshold));
        clusters = clusters
            .OrderByDescending(static cluster => cluster.Count)
            .ThenBy(static cluster => cluster.Family, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CaseClusterReport(
            generatedAt ?? DateTimeOffset.UtcNow,
            effectiveThreshold,
            cases.Count,
            clusters);
    }

    private static IReadOnlyList<CaseCluster> ClusterUnknown(IReadOnlyList<CaseRecord> cases, double threshold)
    {
        var unknown = cases
            .Where(static item => string.Equals(item.IssueFamily, "unknown", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (unknown.Length == 0)
        {
            return [];
        }

        var tokens = unknown.Select(item => TokenSet(item.NormalizedText)).ToArray();
        var visited = new bool[unknown.Length];
        var clusters = new List<CaseCluster>();

        for (var index = 0; index < unknown.Length; index++)
        {
            if (visited[index])
            {
                continue;
            }

            var component = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(index);
            visited[index] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                for (var candidate = 0; candidate < unknown.Length; candidate++)
                {
                    if (visited[candidate])
                    {
                        continue;
                    }

                    if (Jaccard(tokens[current], tokens[candidate]) >= threshold)
                    {
                        visited[candidate] = true;
                        queue.Enqueue(candidate);
                    }
                }
            }

            var items = component.Select(position => unknown[position]).ToArray();
            var label = component.Count == 1 ? "Sınıflandırılamayan sorun" : "Benzer belirtiler";
            clusters.Add(new CaseCluster(
                "unknown",
                label,
                items.Length,
                [
                    new CaseSubcluster(
                        "unknown_symptom",
                        label,
                        items.Length,
                        Samples(items)),
                ],
                Samples(items),
                items.Select(static item => item.CaseId).ToArray()));
        }

        return clusters;
    }

    private static HashSet<string> TokenSet(string normalizedText) =>
        SymptomNormalizer.Tokenize(normalizedText).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static double Jaccard(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Count(right.Contains);
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static IReadOnlyList<string> Samples(IEnumerable<CaseRecord> cases) =>
        cases
            .Select(static item => item.RawText ?? item.NormalizedText)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

    private static string FamilyLabel(string family) => family switch
    {
        "printers" => "Yazıcı sorunları",
        "audio" => "Ses sorunları",
        "network" => "Ağ/İnternet sorunları",
        "security" => "Güvenlik/UAC sorunları",
        "windows_update" => "Windows Update sorunları",
        "system" => "Sistem/uygulama sorunları",
        "storage" => "Depolama sorunları",
        "performance" => "Performans sorunları",
        "applications" => "Uygulama sorunları",
        _ => "Diğer sorunlar",
    };
}
