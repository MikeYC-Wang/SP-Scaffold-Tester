using System.Text.Json;
using SpScaffoldTester.Core.Scanning;

namespace SpScaffoldTester.Cli;

public static class ScanCommandRunner
{
    public static int Run(string[] args, TextWriter output, IScanService? scanService = null)
    {
        if (args.Length == 1 && string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            var service = scanService ?? new StubScanService();
            var result = service.RunScan();

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            output.WriteLine(json);
            return 0;
        }

        output.WriteLine("Usage: sp-scaffold-tester scan");
        return 1;
    }
}
