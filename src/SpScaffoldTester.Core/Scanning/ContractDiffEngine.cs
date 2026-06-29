namespace SpScaffoldTester.Core.Scanning;

public sealed class ContractDiffEngine
{
    public ContractDiffResult Compare(ScanSnapshot baseline, ScanSnapshot current)
    {
        var baselineByName = baseline.StoredProcedures.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var currentByName = current.StoredProcedures.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var reasons = new List<string>();

        foreach (var (spName, baselineSp) in baselineByName)
        {
            if (!currentByName.TryGetValue(spName, out var currentSp))
            {
                reasons.Add($"Stored procedure removed: {spName}");
                continue;
            }

            reasons.AddRange(GetBreakingParameterDiffReasons(baselineSp, currentSp));
            reasons.AddRange(GetBreakingResultColumnDiffReasons(baselineSp, currentSp));
        }

        foreach (var spName in currentByName.Keys)
        {
            if (!baselineByName.ContainsKey(spName))
            {
                reasons.Add($"Stored procedure added: {spName}");
            }
        }

        return new ContractDiffResult
        {
            Severity = reasons.Count > 0 ? ContractDiffSeverity.Breaking : ContractDiffSeverity.None,
            Reasons = reasons
        };
    }

    private static IReadOnlyList<string> GetBreakingParameterDiffReasons(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var reasons = new List<string>();
        var currentParameters = currentSp.Parameters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var baselineParameter in baselineSp.Parameters)
        {
            if (!currentParameters.TryGetValue(baselineParameter.Name, out var currentParameter))
            {
                reasons.Add($"Parameter removed: {baselineSp.Name}.{baselineParameter.Name}");
                continue;
            }

            if (!string.Equals(baselineParameter.DbType, currentParameter.DbType, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"Parameter type changed: {baselineSp.Name}.{baselineParameter.Name} ({baselineParameter.DbType} -> {currentParameter.DbType})");
            }
        }

        return reasons;
    }

    private static IReadOnlyList<string> GetBreakingResultColumnDiffReasons(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var reasons = new List<string>();
        var currentColumns = currentSp.ResultColumns.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var baselineColumn in baselineSp.ResultColumns)
        {
            if (!currentColumns.TryGetValue(baselineColumn.Name, out var currentColumn))
            {
                reasons.Add($"Result column removed: {baselineSp.Name}.{baselineColumn.Name}");
                continue;
            }

            if (!string.Equals(baselineColumn.DbType, currentColumn.DbType, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"Result column type changed: {baselineSp.Name}.{baselineColumn.Name} ({baselineColumn.DbType} -> {currentColumn.DbType})");
            }
        }

        return reasons;
    }
}
