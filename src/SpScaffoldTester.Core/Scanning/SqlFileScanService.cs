using System.Text.RegularExpressions;

namespace SpScaffoldTester.Core.Scanning;

public sealed class SqlFileScanService : IScanService
{
    private static readonly Regex ProcedureRegex = new(
        @"CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?<name>(?:\[[^\]]+\]|[A-Za-z_][\w]*)(?:\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][\w]*))?)\s*(?<params>[\s\S]*?)\bAS\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex ParameterRegex = new(
        @"@(?<name>[A-Za-z_][\w]*)\s+(?<type>[A-Za-z_][\w]*)(?:\s*\([^\)]*\))?(?<tail>[\s\S]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private readonly string _sqlFilePath;

    public SqlFileScanService(string sqlFilePath)
    {
        _sqlFilePath = sqlFilePath;
    }

    public ScanStubResult RunScan()
    {
        var sqlText = File.ReadAllText(_sqlFilePath);
        var procedures = ParseStoredProcedures(sqlText);

        return new ScanStubResult
        {
            Status = "scanned",
            Message = "Scan pipeline parsed SQL file metadata.",
            Snapshot = new ScanSnapshot
            {
                StoredProcedures = procedures
            }
        };
    }

    private static IReadOnlyList<StoredProcedureContract> ParseStoredProcedures(string sqlText)
    {
        var procedures = new List<StoredProcedureContract>();

        foreach (Match match in ProcedureRegex.Matches(sqlText))
        {
            var name = NormalizeProcedureName(match.Groups["name"].Value);
            var parameters = ParseParameters(match.Groups["params"].Value);

            procedures.Add(new StoredProcedureContract
            {
                Name = name,
                Parameters = parameters,
                ResultColumns = []
            });
        }

        return procedures;
    }

    private static IReadOnlyList<ParameterContract> ParseParameters(string parameterBlock)
    {
        var parameters = new List<ParameterContract>();
        var segments = parameterBlock.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            var match = ParameterRegex.Match(segment);
            if (!match.Success)
            {
                continue;
            }

            var isOptional = match.Groups["tail"].Value.Contains('=');
            parameters.Add(new ParameterContract
            {
                Name = match.Groups["name"].Value,
                DbType = match.Groups["type"].Value.ToLowerInvariant(),
                IsOptional = isOptional
            });
        }

        return parameters;
    }

    private static string NormalizeProcedureName(string rawName)
    {
        var parts = rawName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalizedParts = parts.Select(RemoveBrackets);
        return string.Join('.', normalizedParts);
    }

    private static string RemoveBrackets(string value)
    {
        if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
        {
            return value[1..^1];
        }

        return value;
    }
}