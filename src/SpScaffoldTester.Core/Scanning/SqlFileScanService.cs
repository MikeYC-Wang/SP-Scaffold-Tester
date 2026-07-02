using System.Text.RegularExpressions;

namespace SpScaffoldTester.Core.Scanning;

public sealed class SqlFileScanService : IScanService
{
    private static readonly Regex ProcedureRegex = new(
        @"CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?<name>(?:\[[^\]]+\]|[A-Za-z_][\w]*)(?:\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][\w]*))?)\s*(?<params>[\s\S]*?)\bAS\b(?<body>[\s\S]*?)(?=^\s*CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\b|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline
    );

    private static readonly Regex ParameterRegex = new(
        @"@(?<name>(?:\[[^\]]+\]|[A-Za-z_][\w]*))\s+(?<type>(?:\[[^\]]+\]|[A-Za-z_][\w]*)(?:\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][\w]*))*)(?:\s*\([^\)]*\))?(?<tail>[\s\S]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex SelectRegex = new(
        @"SELECT\s+(?<columns>[\s\S]*?)(?:\bFROM\b|;|\bEND\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex CastColumnRegex = new(
        @"^CAST\s*\(\s*(?<expr>NULL|.*?)\s+AS\s+(?<type>(?:\[[^\]]+\]|[A-Za-z_][\w]*)(?:\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][\w]*))*(?:\s*\([^\)]*\))?)\s*\)\s+AS\s+(?<alias>(?:\[[^\]]+\]|[A-Za-z_][\w]*))$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex AliasedColumnRegex = new(
        @"^(?<expr>.+?)\s+AS\s+(?<alias>(?:\[[^\]]+\]|[A-Za-z_][\w]*))$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex IntLiteralRegex = new(
        @"^[-+]?\d+$",
        RegexOptions.Compiled
    );

    private static readonly Regex DecimalLiteralRegex = new(
        @"^[-+]?\d+\.\d+$",
        RegexOptions.Compiled
    );

    private static readonly Regex StringLiteralRegex = new(
        @"^N?'([^']|'')*'$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex LeadingTopClauseRegex = new(
        @"^TOP\s*(?:\(\s*\d+\s*\)|\d+)\s+",
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
        var cleanedBlock = RemoveSqlComments(parameterBlock);
        var segments = SplitTopLevelByComma(cleanedBlock);

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
                Name = RemoveBrackets(match.Groups["name"].Value),
                DbType = NormalizeTypeName(match.Groups["type"].Value),
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

        var columnsText = RemoveLeadingTopClause(selectMatch.Groups["columns"].Value);
        var columnSegments = SplitTopLevelByComma(columnsText);

        var resultColumns = new List<ResultColumnContract>();
        var unresolvedSegments = false;
        foreach (var segment in columnSegments)
        {
            if (TryParseCastColumn(segment, out var castColumn))
            {
                resultColumns.Add(castColumn);
                continue;
            }

            if (TryParseAliasedColumn(segment, out var aliasedColumn))
            {
                resultColumns.Add(aliasedColumn);
                continue;
            }

            unresolvedSegments = true;
        }

        var isMetadataAmbiguous = unresolvedSegments || resultColumns.Count == 0;

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

    private static string NormalizeTypeName(string rawType)
    {
        var withoutPrecision = rawType.Split('(', 2)[0];

        var parts = withoutPrecision.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalizedParts = parts
            .Select(RemoveBrackets)
            .Select(x => x.ToLowerInvariant());

        return string.Join('.', normalizedParts);
    }

    private static bool TryParseCastColumn(string segment, out ResultColumnContract column)
    {
        var match = CastColumnRegex.Match(segment);
        if (!match.Success)
        {
            column = default!;
            return false;
        }

        var expression = match.Groups["expr"].Value.Trim();
        column = new ResultColumnContract
        {
            Name = RemoveBrackets(match.Groups["alias"].Value),
            DbType = NormalizeTypeName(match.Groups["type"].Value),
            IsNullable = expression.Equals("NULL", StringComparison.OrdinalIgnoreCase)
        };

        return true;
    }

    private static bool TryParseAliasedColumn(string segment, out ResultColumnContract column)
    {
        var match = AliasedColumnRegex.Match(segment);
        if (!match.Success)
        {
            column = default!;
            return false;
        }

        var expression = match.Groups["expr"].Value.Trim();
        var alias = RemoveBrackets(match.Groups["alias"].Value);

        if (!TryInferLiteralExpressionType(expression, out var dbType, out var isNullable))
        {
            column = default!;
            return false;
        }

        column = new ResultColumnContract
        {
            Name = alias,
            DbType = dbType,
            IsNullable = isNullable
        };

        return true;
    }

    private static bool TryInferLiteralExpressionType(string expression, out string dbType, out bool isNullable)
    {
        if (expression.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            dbType = "unknown";
            isNullable = true;
            return true;
        }

        if (IntLiteralRegex.IsMatch(expression))
        {
            dbType = "int";
            isNullable = false;
            return true;
        }

        if (DecimalLiteralRegex.IsMatch(expression))
        {
            dbType = "decimal";
            isNullable = false;
            return true;
        }

        if (StringLiteralRegex.IsMatch(expression))
        {
            dbType = "nvarchar";
            isNullable = false;
            return true;
        }

        dbType = string.Empty;
        isNullable = false;
        return false;
    }

    private static string RemoveLeadingTopClause(string columnsText)
    {
        return LeadingTopClauseRegex.Replace(columnsText.TrimStart(), string.Empty);
    }

    private static IReadOnlyList<string> SplitTopLevelByComma(string text)
    {
        var segments = new List<string>();
        var current = new System.Text.StringBuilder();
        var parenDepth = 0;
        var bracketDepth = 0;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (ch == '\'')
            {
                current.Append(ch);

                if (inString && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    current.Append(text[i + 1]);
                    i++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (ch == '[')
                {
                    bracketDepth++;
                }
                else if (ch == ']' && bracketDepth > 0)
                {
                    bracketDepth--;
                }
                else if (ch == '(')
                {
                    parenDepth++;
                }
                else if (ch == ')' && parenDepth > 0)
                {
                    parenDepth--;
                }
                else if (ch == ',' && parenDepth == 0 && bracketDepth == 0)
                {
                    var segment = current.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(segment))
                    {
                        segments.Add(segment);
                    }

                    current.Clear();
                    continue;
                }
            }

            current.Append(ch);
        }

        var lastSegment = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(lastSegment))
        {
            segments.Add(lastSegment);
        }

        return segments;
    }
}