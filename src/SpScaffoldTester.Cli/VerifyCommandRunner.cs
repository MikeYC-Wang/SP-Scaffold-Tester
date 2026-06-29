using SpScaffoldTester.Core.Scanning;

namespace SpScaffoldTester.Cli;

public static class VerifyCommandRunner
{
    public static int Run(string[] args, TextWriter output, ISnapshotComparer? comparer = null)
    {
        if (!TryParseArgs(args, out var baselinePath, out var currentPath))
        {
            output.WriteLine("Usage: sp-scaffold-tester verify --baseline <path> --current <path>");
            return 4;
        }

        if (!File.Exists(baselinePath) || !File.Exists(currentPath))
        {
            output.WriteLine("Configuration error: baseline/current snapshot file not found.");
            return 4;
        }

        var snapshotComparer = comparer ?? new SnapshotComparer();
        var equivalent = snapshotComparer.AreEquivalent(baselinePath!, currentPath!);

        if (equivalent)
        {
            output.WriteLine("No breaking contract changes.");
            return 0;
        }

        output.WriteLine("Breaking contract changes detected.");
        return 2;
    }

    private static bool TryParseArgs(string[] args, out string? baselinePath, out string? currentPath)
    {
        baselinePath = null;
        currentPath = null;

        if (args.Length != 5 || !args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var i = 1; i < args.Length - 1; i += 2)
        {
            if (args[i].Equals("--baseline", StringComparison.OrdinalIgnoreCase))
            {
                baselinePath = args[i + 1];
            }
            else if (args[i].Equals("--current", StringComparison.OrdinalIgnoreCase))
            {
                currentPath = args[i + 1];
            }
            else
            {
                return false;
            }
        }

        return !string.IsNullOrWhiteSpace(baselinePath) && !string.IsNullOrWhiteSpace(currentPath);
    }
}
