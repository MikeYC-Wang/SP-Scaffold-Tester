using System.Text.Json;
using SpScaffoldTester.Core.Scanning;

namespace SpScaffoldTester.Cli;

public static class ScanCommandRunner
{
    public static int Run(string[] args, TextWriter output, IScanService? scanService = null)
    {
        if (args.Length >= 1 && string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseOutputPath(args, out var outputPath))
            {
                output.WriteLine("Usage: sp-scaffold-tester scan [--out <path>]");
                return 1;
            }

            var service = scanService ?? new StubScanService();
            var result = service.RunScan();

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            output.WriteLine(json);

            if (outputPath is not null)
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var snapshotJson = JsonSerializer.Serialize(result.Snapshot, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(outputPath, snapshotJson);
                output.WriteLine($"Snapshot written to {outputPath}");
            }

            return 0;
        }

        output.WriteLine("Usage: sp-scaffold-tester scan [--out <path>]");
        return 1;
    }

    private static bool TryParseOutputPath(string[] args, out string? outputPath)
    {
        outputPath = null;

        if (args.Length == 1)
        {
            return true;
        }

        if (args.Length == 3 && (args[1].Equals("--out", StringComparison.OrdinalIgnoreCase) || args[1].Equals("--output", StringComparison.OrdinalIgnoreCase)))
        {
            outputPath = args[2];
            return !string.IsNullOrWhiteSpace(outputPath);
        }

        return false;
    }
}
