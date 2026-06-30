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

    private static readonly Regex BlockCommentRegex = new(
        @"/\*[\s\S]*?\*/",
        RegexOptions.Compiled
    );

    private static readonly Regex LineCommentRegex = new(
        @"--.*$",
        RegexOptions.Compiled | RegexOptions.Multiline
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
            var (resultColumns, isMetadataAmbiguous) = ParseResultColumns(match.Groups["body"].Value);

            procedures.Add(new StoredProcedureContract
            {
                Name = name,
                IsMetadataAmbiguous = isMetadataAmbiguous,
                Parameters = parameters,
                ResultColumns = resultColumns
            });
        }

        return procedures
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

        return parameters
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (IReadOnlyList<ResultColumnContract> Columns, bool IsMetadataAmbiguous) ParseResultColumns(string procedureBody)
    {
        var cleanedBody = RemoveSqlComments(procedureBody);

        if (cleanedBody.Contains("sp_executesql", StringComparison.OrdinalIgnoreCase))
        {
            return ([], true);
        }

        var selectMatch = SelectRegex.Match(cleanedBody);
        if (!selectMatch.Success)
        {
            return ([], false);
        }

        var columnsText = selectMatch.Groups["columns"].Value;
        var resultColumns = new List<ResultColumnContract>();
        var matches = CastColumnRegex.Matches(columnsText);
        foreach (Match columnMatch in matches)
        {
            var expression = columnMatch.Groups["expr"].Value.Trim();
            resultColumns.Add(new ResultColumnContract
            {
                Name = columnMatch.Groups["alias"].Value,
                DbType = columnMatch.Groups["type"].Value.ToLowerInvariant(),
                IsNullable = expression.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            });
        }

        var isMetadataAmbiguous = resultColumns.Count == 0;
        if (!isMetadataAmbiguous)
        {
            var remaining = columnsText;
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    remaining = remaining.Replace(match.Value, string.Empty, StringComparison.OrdinalIgnoreCase);
                }
            }

            var residue = remaining.Replace(",", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("\r", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("\t", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (!string.IsNullOrWhiteSpace(residue))
            {
                isMetadataAmbiguous = true;
            }
        }

        var orderedColumns = resultColumns
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return (orderedColumns, isMetadataAmbiguous);
    }

    private static string RemoveSqlComments(string sql)
    {
        var noBlockComments = BlockCommentRegex.Replace(sql, string.Empty);
        return LineCommentRegex.Replace(noBlockComments, string.Empty);
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