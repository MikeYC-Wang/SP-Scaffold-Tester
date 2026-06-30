using System.Text.RegularExpressions;

namespace SpScaffoldTester.Core.Scanning;

public sealed class SqlFileScanService : IScanService
{
    private static readonly Regex ProcedureRegex = new(
        @"CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?<name>(?:\[[^\]]+\]|[A-Za-z_][\w]*)(?:\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][\w]*))?)\s*(?<params>[\s\S]*?)\bAS\b(?<body>[\s\S]*?)(?=^\s*CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\b|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline
    );

    private static readonly Regex ParameterRegex = new(
        @"@(?<name>[A-Za-z_][\w]*)\s+(?<type>[A-Za-z_][\w]*)(?:\s*\([^\)]*\))?(?<tail>[\s\S]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex SelectRegex = new(
        @"SELECT\s+(?<columns>[\s\S]*?)(?:\bFROM\b|;)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex CastColumnRegex = new(
        @"CAST\s*\(\s*(?<expr>NULL|[^\)]*?)\s+AS\s+(?<type>[A-Za-z_][\w]*)(?:\s*\([^\)]*\))?\s*\)\s+AS\s+(?<alias>[A-Za-z_][\w]*)",
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
            var resultColumns = ParseResultColumns(match.Groups["body"].Value);

            procedures.Add(new StoredProcedureContract
            {
                Name = name,
                Parameters = parameters,
                ResultColumns = resultColumns
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

    private static IReadOnlyList<ResultColumnContract> ParseResultColumns(string procedureBody)
    {
        var selectMatch = SelectRegex.Match(procedureBody);
        if (!selectMatch.Success)
        {
            return [];
        }

        var columnsText = selectMatch.Groups["columns"].Value;
        var resultColumns = new List<ResultColumnContract>();
        foreach (Match columnMatch in CastColumnRegex.Matches(columnsText))
        {
            var expression = columnMatch.Groups["expr"].Value.Trim();
            resultColumns.Add(new ResultColumnContract
            {
                Name = columnMatch.Groups["alias"].Value,
                DbType = columnMatch.Groups["type"].Value.ToLowerInvariant(),
                IsNullable = expression.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            });
        }

        return resultColumns;
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