using System.Text.Json;
using SpScaffoldTester.Core.Scanning;

namespace SpScaffoldTester.Cli;

public static class ScanCommandRunner
{
    public static int Run(string[] args, TextWriter output, IScanService? scanService = null)
    {
        if (args.Length >= 1 && string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseOptions(args, out var outputPath, out var sqlPath))
            {
                output.WriteLine("Usage: sp-scaffold-tester scan [--out <path>] [--sql <path>]");
                return 1;
            }

            try
            {
                IScanService service = sqlPath is not null
                    ? new SqlFileScanService(sqlPath)
                    : scanService ?? new StubScanService();

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
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or InvalidOperationException)
            {
                output.WriteLine($"Configuration error: {ex.Message}");
                return 4;
            }
        }

        output.WriteLine("Usage: sp-scaffold-tester scan [--out <path>] [--sql <path>]");
        return 1;
    }

    private static bool TryParseOptions(string[] args, out string? outputPath, out string? sqlPath)
    {
        outputPath = null;
        sqlPath = null;

        if (args.Length == 1)
        {
            return true;
        }

        if ((args.Length - 1) % 2 != 0)
        {
            return false;
        }

        for (var i = 1; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
            {
                return false;
            }

            var option = args[i];
            var value = args[i + 1];
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (option.Equals("--out", StringComparison.OrdinalIgnoreCase) || option.Equals("--output", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath is not null)
                {
                    return false;
                }

                outputPath = value;
                continue;
            }

            if (option.Equals("--sql", StringComparison.OrdinalIgnoreCase))
            {
                if (sqlPath is not null)
                {
                    return false;
                }

                sqlPath = value;
                continue;
            }

            return false;
        }

        return true;
    }
}
