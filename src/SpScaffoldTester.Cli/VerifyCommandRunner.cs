using System.Text.Json;
using System.Text.Json.Serialization;
using SpScaffoldTester.Core.Scanning;

namespace SpScaffoldTester.Cli;

public static class VerifyCommandRunner
{
    public static int Run(string[] args, TextWriter output)
    {
        if (!TryParseArgs(args, out var baselinePath, out var currentPath, out var reportPath, out var strictMode))
        {
            output.WriteLine("Usage: sp-scaffold-tester verify --baseline <path> --current <path> [--report <path>] [--strict]");
            return 4;
        }

        if (!File.Exists(baselinePath) || !File.Exists(currentPath))
        {
            output.WriteLine("Configuration error: baseline/current snapshot file not found.");
            return 4;
        }

        try
        {
            var baseline = LoadSnapshot(baselinePath!);
            var current = LoadSnapshot(currentPath!);
            var engine = new ContractDiffEngine();
            var result = engine.Compare(baseline, current);

            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                WriteReport(reportPath!, result);
                output.WriteLine($"Verify report written to {reportPath}");
            }

            if (result.Severity == ContractDiffSeverity.Breaking)
            {
                output.WriteLine("Breaking contract changes detected.");
                return 2;
            }

            if (result.Severity == ContractDiffSeverity.Unknown)
            {
                output.WriteLine("Contract analysis contains unknown changes.");
                if (strictMode)
                {
                    output.WriteLine("Strict mode escalated unknown changes.");
                    return 2;
                }

                return 0;
            }

            if (result.Severity == ContractDiffSeverity.Warning)
            {
                output.WriteLine("Contract warnings detected.");
                if (strictMode)
                {
                    output.WriteLine("Strict mode escalated warnings.");
                    return 2;
                }

                return 0;
            }

            output.WriteLine("No breaking contract changes.");
            return 0;
        }
        catch (JsonException)
        {
            output.WriteLine("Configuration error: invalid snapshot JSON format.");
            return 4;
        }
    }

    private static ScanSnapshot LoadSnapshot(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ScanSnapshot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new ScanSnapshot();
    }

    private static void WriteReport(string reportPath, ContractDiffResult result)
    {
        var directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });

        File.WriteAllText(reportPath, json);
    }

    private static bool TryParseArgs(string[] args, out string? baselinePath, out string? currentPath, out string? reportPath, out bool strictMode)
    {
        baselinePath = null;
        currentPath = null;
        reportPath = null;
        strictMode = false;

        if (args.Length < 5 || args.Length > 8 || !args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--strict", StringComparison.OrdinalIgnoreCase))
            {
                strictMode = true;
                continue;
            }

            if (i == args.Length - 1)
            {
                return false;
            }

            if (args[i].Equals("--baseline", StringComparison.OrdinalIgnoreCase))
            {
                baselinePath = args[++i];
            }
            else if (args[i].Equals("--current", StringComparison.OrdinalIgnoreCase))
            {
                currentPath = args[++i];
            }
            else if (args[i].Equals("--report", StringComparison.OrdinalIgnoreCase))
            {
                reportPath = args[++i];
            }
            else
            {
                return false;
            }
        }

        return !string.IsNullOrWhiteSpace(baselinePath)
            && !string.IsNullOrWhiteSpace(currentPath)
            && (reportPath is null || !string.IsNullOrWhiteSpace(reportPath));
    }
}
