using System.Text.Json;

namespace SpScaffoldTester.Core.Scanning;

public sealed class SnapshotComparer : ISnapshotComparer
{
    public bool AreEquivalent(string baselinePath, string currentPath)
    {
        var baseline = NormalizeJson(File.ReadAllText(baselinePath));
        var current = NormalizeJson(File.ReadAllText(currentPath));

        return string.Equals(baseline, current, StringComparison.Ordinal);
    }

    private static string NormalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement);
    }
}
